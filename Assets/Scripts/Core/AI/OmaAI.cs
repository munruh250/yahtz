using System;
using System.Collections.Generic;

namespace Yahtzee.Core
{
    /// <summary>Oma's decision-making (TECH_PLAN §5.6): a clean heuristic targeting a
    /// "solid club player" (~200-230 average), deliberately not an optimal solver.
    ///
    /// Stage-by-stage API driven by the controller: <see cref="DecideKeepers"/> after each
    /// roll, <see cref="DecideCategory"/> at turn end. Both are pure functions of the engine
    /// state — deterministic, so skipping her turn animation can never change the outcome.
    /// She only reads the same query API the player UI uses (Joker compliance for free) and
    /// never mutates the engine.
    ///
    /// The single "sloppiness" ε knob is reserved for future difficulty levels; 0 in v1.</summary>
    public sealed class OmaAI
    {
        /// <summary>Baseline expected value of each box (rough solitaire EVs), used as the
        /// opportunity cost of spending it. Dumping a zero naturally lands in the cheapest
        /// open box; burning Yahtzee or a straight is punished by its high cost.</summary>
        private static readonly double[] BoxCost =
        {
            1.9,   // Aces
            5.3,   // Twos
            8.6,   // Threes
            12.2,  // Fours
            15.7,  // Fives
            19.2,  // Sixes
            21.7,  // ThreeOfAKind
            14.9,  // FourOfAKind
            22.6,  // FullHouse
            29.5,  // SmallStraight
            32.7,  // LargeStraight
            16.9,  // Yahtzee
            22.0,  // Chance
        };

        // Tuned via the simulation harness (OmaSimulation) toward the 200-230 band.
        private const double PaceWeight = 2.2;        // upper-bonus pace bonus per surplus pip
        private const double SecureBonusValue = 33.0; // extra pull when a score locks the 63
        private const double UpperKeepWeight = 0.55;  // keeper bias toward needed upper faces
        private const double RunKeepWeight = 7.5;     // keeper value per die of a straight run
        private const double YahtzeeChase3 = 9.0;     // keeper bonus: triple, Yahtzee open
        private const double YahtzeeChase4 = 21.0;    // keeper bonus: quad, Yahtzee open
        private const double TwoPairFullHouse = 13.0; // keeper bonus: two pair, FH open

        private readonly double _sloppiness;
        private readonly Random _slopRng;

        public OmaAI(double sloppiness = 0.0, Random slopRng = null)
        {
            if (sloppiness > 0 && slopRng == null)
                throw new ArgumentException("sloppiness > 0 requires an RNG");
            _sloppiness = sloppiness;
            _slopRng = slopRng;
        }

        // ---- Keepers (rolls 1-2; called after every roll with rolls remaining) ----

        /// <summary>Which dice to keep before the next roll. All five kept = stop rolling
        /// and score now.</summary>
        public bool[] DecideKeepers(GameEngine engine)
        {
            var state = engine.State;
            if (state.Phase != GamePhase.Deciding)
                throw new InvalidOperationException("No dice to evaluate.");
            int[] dice = state.Dice.Values;
            var card = engine.CurrentCard;

            // Made-hand shortcuts: stand pat on hands worth banking.
            if (ScoreCalculator.IsYahtzee(dice))
                return KeepAll();
            if (ScoreCalculator.HasStraight(dice, 5) && card.IsOpen(Category.LargeStraight))
                return KeepAll();
            if (ScoreCalculator.IsFullHouse(dice) && card.IsOpen(Category.FullHouse))
                return KeepAll();
            if (ScoreCalculator.HasStraight(dice, 4) && card.IsOpen(Category.SmallStraight)
                && !card.IsOpen(Category.LargeStraight))
                return KeepAll();

            int bestMask = 0;
            double bestEval = double.MinValue;
            for (int mask = 0; mask < 32; mask++)
            {
                double eval = EvaluateKeepSubset(dice, mask, card);
                if (eval > bestEval + 1e-9)
                {
                    bestEval = eval;
                    bestMask = mask;
                }
            }

            var keep = new bool[DiceState.DieCount];
            for (int i = 0; i < DiceState.DieCount; i++)
                keep[i] = (bestMask & (1 << i)) != 0;
            return keep;
        }

        private static bool[] KeepAll() => new[] { true, true, true, true, true };

        /// <summary>Cheap closed-form estimate of a keep-subset's promise: the best of an
        /// of-a-kind line, a straight-draw line, and a full-house line (TECH_PLAN §5.6).</summary>
        private static double EvaluateKeepSubset(int[] dice, int mask, Scorecard card)
        {
            Span<int> counts = stackalloc int[7];
            int keptCount = 0, keptSum = 0;
            for (int i = 0; i < DiceState.DieCount; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;
                counts[dice[i]]++;
                keptCount++;
                keptSum += dice[i];
            }
            int rolling = DiceState.DieCount - keptCount;
            bool bonusOpen = !card.HasUpperBonus && UpperBonusReachable(card);

            // Of-a-kind line: the strongest kept face, valued by its expected final count.
            double kind = 0;
            for (int face = 1; face <= 6; face++)
            {
                int c = counts[face];
                if (c == 0 && keptCount > 0)
                    continue;
                double expected = c + rolling / 6.0;
                double v = expected * face;
                if (card.IsOpen(CategoryExtensions.UpperCategoryForFace(face)) && bonusOpen)
                    v += expected * face * UpperKeepWeight;
                if (c >= 3 && card.IsOpen(Category.Yahtzee))
                    v += c >= 4 ? YahtzeeChase4 : YahtzeeChase3;
                if (c >= 3 && (card.IsOpen(Category.ThreeOfAKind) || card.IsOpen(Category.FourOfAKind)))
                    v += keptSum * 0.25 + rolling * 0.9;
                if (v > kind)
                    kind = v;
            }

            // Straight-draw line: only clean (duplicate-free) subsets count as draws.
            double straight = 0;
            bool wantsStraight = card.IsOpen(Category.SmallStraight) || card.IsOpen(Category.LargeStraight);
            if (wantsStraight && keptCount > 0 && HasNoDuplicates(counts))
            {
                int run = LongestRun(counts);
                if (run >= 2)
                    straight = run * RunKeepWeight;
            }

            // Full-house line: two pair kept, FH open.
            double fullHouse = 0;
            if (card.IsOpen(Category.FullHouse) && CountPairs(counts) == 2 && keptCount == 4)
                fullHouse = TwoPairFullHouse + keptSum * 0.2;

            double best = Math.Max(kind, Math.Max(straight, fullHouse));
            // Chance floor: high kept pips have residual value even with no plan.
            if (card.IsOpen(Category.Chance))
                best = Math.Max(best, keptSum * 0.55 + rolling * 3.5 * 0.55);
            return best;
        }

        // ---- Category choice (turn end) ------------------------------------

        public Category DecideCategory(GameEngine engine)
        {
            var potentials = engine.GetPotentialScores();
            var card = engine.CurrentCard;

            Category best = default;
            Category second = default;
            double bestValue = double.MinValue, secondValue = double.MinValue;
            foreach (var pair in potentials)
            {
                double value = CategoryValue(pair.Key, pair.Value, card);
                if (value > bestValue)
                {
                    second = best;
                    secondValue = bestValue;
                    best = pair.Key;
                    bestValue = value;
                }
                else if (value > secondValue)
                {
                    second = pair.Key;
                    secondValue = value;
                }
            }

            if (_sloppiness > 0 && secondValue > double.MinValue && _slopRng.NextDouble() < _sloppiness)
                return second;
            return best;
        }

        private static double CategoryValue(Category category, int points, Scorecard card)
        {
            double value = points - BoxCost[(int)category];
            if (category.IsUpper() && !card.HasUpperBonus && UpperBonusReachable(card))
            {
                int face = category.UpperFace();
                double surplus = points - 3 * face; // ≥3-of-face keeps 63 pace
                value += surplus * PaceWeight;
                if (card.UpperSubtotal + points >= Scorecard.UpperBonusThreshold)
                    value += SecureBonusValue;
            }
            return value;
        }

        // ---- Hint API (player-facing "best options" after the last roll) ----

        /// <summary>The strongest currently-legal boxes for a hint UI, best first, using the
        /// same opportunity-cost valuation Oma plays by. Joker-aware for free because it
        /// reads <see cref="GameEngine.GetPotentialScores"/>.</summary>
        public static IReadOnlyList<(Category category, int points)> RankForHint(GameEngine engine, int count)
        {
            var potentials = engine.GetPotentialScores();
            var card = engine.CurrentCard;
            var ranked = new List<(Category category, int points, double value)>();
            foreach (var pair in potentials)
                ranked.Add((pair.Key, pair.Value, CategoryValue(pair.Key, pair.Value, card)));
            ranked.Sort((a, b) => b.value.CompareTo(a.value));

            var result = new List<(Category, int)>();
            for (int i = 0; i < ranked.Count && i < count; i++)
                result.Add((ranked[i].category, ranked[i].points));
            return result;
        }

        // ---- Helpers -------------------------------------------------------

        private static bool UpperBonusReachable(Scorecard card)
        {
            int needed = Scorecard.UpperBonusThreshold - card.UpperSubtotal;
            if (needed <= 0)
                return true;
            int optimistic = 0;
            for (var c = Category.Aces; c <= Category.Sixes; c++)
                if (card.IsOpen(c))
                    optimistic += 4 * c.UpperFace();
            return optimistic >= needed;
        }

        private static bool HasNoDuplicates(Span<int> counts)
        {
            for (int face = 1; face <= 6; face++)
                if (counts[face] > 1)
                    return false;
            return true;
        }

        private static int LongestRun(Span<int> counts)
        {
            int best = 0, current = 0;
            for (int face = 1; face <= 6; face++)
            {
                current = counts[face] > 0 ? current + 1 : 0;
                if (current > best)
                    best = current;
            }
            return best;
        }

        private static int CountPairs(Span<int> counts)
        {
            int pairs = 0;
            for (int face = 1; face <= 6; face++)
                if (counts[face] == 2)
                    pairs++;
            return pairs;
        }
    }
}

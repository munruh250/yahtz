using System.Collections.Generic;

namespace Yahtzee.Core
{
    /// <summary>The Yahtzee-bonus / Joker placement rules (DESIGN_SPEC §3.2). Active when the
    /// rolled dice are five of a kind AND the Yahtzee box is already filled. Forced priority:
    /// matching upper box → any open lower box (FH/SS/LS at fixed value) → forced 0 in an
    /// open upper box. The +100 bonus applies only while the Yahtzee box holds 50.</summary>
    public static class JokerRules
    {
        public static bool IsJokerSituation(int[] dice, Scorecard card) =>
            ScoreCalculator.IsYahtzee(dice) && !card.IsOpen(Category.Yahtzee);

        /// <summary>Whether scoring a Joker roll now earns the +100 Yahtzee bonus.</summary>
        public static bool BonusApplies(Scorecard card) =>
            card.GetScore(Category.Yahtzee) == ScoreCalculator.YahtzeeScore;

        /// <summary>Legal boxes for a Joker roll, per the forced priority order. Assumes
        /// <see cref="IsJokerSituation"/> is true.</summary>
        public static IReadOnlyList<Category> LegalCategories(int[] dice, Scorecard card)
        {
            var matchingUpper = CategoryExtensions.UpperCategoryForFace(dice[0]);
            if (card.IsOpen(matchingUpper))
                return new[] { matchingUpper };

            var lowers = new List<Category>();
            for (var c = Category.ThreeOfAKind; c <= Category.Chance; c++)
                if (c != Category.Yahtzee && card.IsOpen(c))
                    lowers.Add(c);
            if (lowers.Count > 0)
                return lowers;

            var uppers = new List<Category>();
            for (var c = Category.Aces; c <= Category.Sixes; c++)
                if (card.IsOpen(c))
                    uppers.Add(c);
            return uppers;
        }

        /// <summary>Score a Joker roll in <paramref name="category"/>: FH/SS/LS at their fixed
        /// values even though the dice don't literally qualify; everything else scores
        /// naturally (upper = sum of matching face — 0 for a non-matching upper box, which is
        /// exactly the forced-zero case; 3K/4K/Chance = sum of all dice).</summary>
        public static int Score(Category category, int[] dice)
        {
            switch (category)
            {
                case Category.FullHouse:
                    return ScoreCalculator.FullHouseScore;
                case Category.SmallStraight:
                    return ScoreCalculator.SmallStraightScore;
                case Category.LargeStraight:
                    return ScoreCalculator.LargeStraightScore;
                default:
                    return ScoreCalculator.Score(category, dice);
            }
        }
    }
}

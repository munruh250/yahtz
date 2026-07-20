using System;

namespace Yahtzee.Core
{
    /// <summary>Stateless scoring of five dice against a category. Pure functions of the
    /// dice; no scorecard knowledge. Joker overrides live in <see cref="JokerRules"/>.</summary>
    public static class ScoreCalculator
    {
        public const int FullHouseScore = 25;
        public const int SmallStraightScore = 30;
        public const int LargeStraightScore = 40;
        public const int YahtzeeScore = 50;

        /// <summary>Natural score of <paramref name="dice"/> in <paramref name="category"/>.
        /// Five of a kind is NOT a natural Full House (Joker only); five sequential dice DO
        /// count for Small Straight; unmet requirements score 0.</summary>
        public static int Score(Category category, int[] dice)
        {
            Validate(dice);
            switch (category)
            {
                case Category.Aces:
                case Category.Twos:
                case Category.Threes:
                case Category.Fours:
                case Category.Fives:
                case Category.Sixes:
                    return SumOfFace(dice, category.UpperFace());
                case Category.ThreeOfAKind:
                    return HasOfAKind(dice, 3) ? Sum(dice) : 0;
                case Category.FourOfAKind:
                    return HasOfAKind(dice, 4) ? Sum(dice) : 0;
                case Category.FullHouse:
                    return IsFullHouse(dice) ? FullHouseScore : 0;
                case Category.SmallStraight:
                    return HasStraight(dice, 4) ? SmallStraightScore : 0;
                case Category.LargeStraight:
                    return HasStraight(dice, 5) ? LargeStraightScore : 0;
                case Category.Yahtzee:
                    return IsYahtzee(dice) ? YahtzeeScore : 0;
                case Category.Chance:
                    return Sum(dice);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        /// <summary>counts[v] = number of dice showing v, for v in 1..6 (index 0 unused).</summary>
        public static int[] Histogram(int[] dice)
        {
            Validate(dice);
            var counts = new int[7];
            foreach (int d in dice)
                counts[d]++;
            return counts;
        }

        public static int Sum(int[] dice)
        {
            Validate(dice);
            int sum = 0;
            foreach (int d in dice)
                sum += d;
            return sum;
        }

        public static int SumOfFace(int[] dice, int face)
        {
            Validate(dice);
            int sum = 0;
            foreach (int d in dice)
                if (d == face)
                    sum += d;
            return sum;
        }

        public static bool HasOfAKind(int[] dice, int count)
        {
            var counts = Histogram(dice);
            for (int v = 1; v <= 6; v++)
                if (counts[v] >= count)
                    return true;
            return false;
        }

        /// <summary>Exactly a triple plus a pair of a different face.</summary>
        public static bool IsFullHouse(int[] dice)
        {
            var counts = Histogram(dice);
            bool hasTriple = false, hasPair = false;
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] == 3) hasTriple = true;
                if (counts[v] == 2) hasPair = true;
            }
            return hasTriple && hasPair;
        }

        /// <summary>True if the dice contain a run of <paramref name="length"/> consecutive
        /// values. Detection via bitmask of present faces (TECH_PLAN §5.1).</summary>
        public static bool HasStraight(int[] dice, int length)
        {
            Validate(dice);
            int mask = 0;
            foreach (int d in dice)
                mask |= 1 << d;
            int run = ((1 << length) - 1) << 1; // run starting at face 1
            for (int start = 1; start + length - 1 <= 6; start++, run <<= 1)
                if ((mask & run) == run)
                    return true;
            return false;
        }

        public static bool IsYahtzee(int[] dice)
        {
            Validate(dice);
            for (int i = 1; i < dice.Length; i++)
                if (dice[i] != dice[0])
                    return false;
            return true;
        }

        private static void Validate(int[] dice)
        {
            if (dice == null || dice.Length != DiceState.DieCount)
                throw new ArgumentException("Exactly five dice required.", nameof(dice));
            foreach (int d in dice)
                if (d < 1 || d > 6)
                    throw new ArgumentException($"Die value {d} out of range 1–6.", nameof(dice));
        }
    }
}

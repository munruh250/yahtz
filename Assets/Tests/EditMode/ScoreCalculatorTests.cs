using System;
using System.Collections.Generic;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    public class ScoreCalculatorTests
    {
        // ---- Upper section -------------------------------------------------

        [TestCase(new[] { 1, 1, 1, 2, 3 }, Category.Aces, 3)]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, Category.Aces, 0)]
        [TestCase(new[] { 2, 2, 3, 2, 2 }, Category.Twos, 8)]
        [TestCase(new[] { 3, 3, 3, 3, 3 }, Category.Threes, 15)]
        [TestCase(new[] { 4, 1, 4, 1, 4 }, Category.Fours, 12)]
        [TestCase(new[] { 5, 5, 1, 2, 3 }, Category.Fives, 10)]
        [TestCase(new[] { 6, 6, 6, 6, 1 }, Category.Sixes, 24)]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, Category.Sixes, 0)]
        public void UpperCategories_SumMatchingFaceOnly(int[] dice, Category category, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(category, dice));
        }

        // ---- Three / Four of a Kind ---------------------------------------

        [TestCase(new[] { 2, 2, 2, 4, 5 }, 15)] // exactly three
        [TestCase(new[] { 2, 2, 2, 2, 5 }, 13)] // four counts as three
        [TestCase(new[] { 6, 6, 6, 6, 6 }, 30)] // yahtzee counts
        [TestCase(new[] { 2, 2, 3, 3, 4 }, 0)]  // two pair is not enough
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 0)]
        public void ThreeOfAKind_SumOfAllDice_OrZero(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.ThreeOfAKind, dice));
        }

        [TestCase(new[] { 4, 4, 4, 4, 2 }, 18)]
        [TestCase(new[] { 5, 5, 5, 5, 5 }, 25)] // yahtzee counts
        [TestCase(new[] { 4, 4, 4, 2, 2 }, 0)]  // only three
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 0)]
        public void FourOfAKind_SumOfAllDice_OrZero(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.FourOfAKind, dice));
        }

        // ---- Full House ----------------------------------------------------

        [TestCase(new[] { 3, 3, 3, 2, 2 }, 25)]
        [TestCase(new[] { 2, 2, 3, 3, 3 }, 25)]
        [TestCase(new[] { 6, 6, 1, 1, 1 }, 25)]
        [TestCase(new[] { 2, 2, 3, 3, 4 }, 0)]  // two pair
        [TestCase(new[] { 3, 3, 3, 3, 2 }, 0)]  // 4+1
        [TestCase(new[] { 4, 4, 4, 4, 4 }, 0)]  // five of a kind is NOT a natural full house
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 0)]
        public void FullHouse_TriplePlusPair_FiveOfAKindExcluded(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.FullHouse, dice));
        }

        // ---- Straights -----------------------------------------------------

        [TestCase(new[] { 1, 2, 3, 4, 6 }, 30)]
        [TestCase(new[] { 2, 3, 4, 5, 5 }, 30)]
        [TestCase(new[] { 6, 5, 4, 3, 1 }, 30)] // order irrelevant
        [TestCase(new[] { 1, 1, 2, 3, 4 }, 30)] // pair plus run
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 30)] // five sequential DO count for small straight
        [TestCase(new[] { 2, 3, 4, 5, 6 }, 30)]
        [TestCase(new[] { 1, 2, 3, 5, 6 }, 0)]  // gap at 4
        [TestCase(new[] { 1, 1, 2, 2, 3 }, 0)]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, 0)]
        public void SmallStraight_AnyRunOfFour(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.SmallStraight, dice));
        }

        [TestCase(new[] { 1, 2, 3, 4, 5 }, 40)]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, 40)]
        [TestCase(new[] { 5, 3, 2, 6, 4 }, 40)] // order irrelevant
        [TestCase(new[] { 1, 2, 3, 4, 6 }, 0)]
        [TestCase(new[] { 1, 2, 3, 4, 4 }, 0)]  // duplicate breaks it
        [TestCase(new[] { 1, 3, 4, 5, 6 }, 0)]  // 1-3-4-5-6 is not sequential
        public void LargeStraight_FiveSequential(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.LargeStraight, dice));
        }

        // ---- Yahtzee & Chance ---------------------------------------------

        [TestCase(new[] { 5, 5, 5, 5, 5 }, 50)]
        [TestCase(new[] { 1, 1, 1, 1, 1 }, 50)]
        [TestCase(new[] { 5, 5, 5, 5, 4 }, 0)]
        public void Yahtzee_AllFiveEqual(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.Yahtzee, dice));
        }

        [TestCase(new[] { 1, 2, 3, 4, 5 }, 15)]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, 30)]
        [TestCase(new[] { 1, 1, 1, 1, 2 }, 6)]
        public void Chance_AlwaysSumOfAllDice(int[] dice, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Score(Category.Chance, dice));
        }

        // ---- Input validation ---------------------------------------------

        [Test]
        public void Score_RejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Score(Category.Chance, null));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Score(Category.Chance, new[] { 1, 2, 3, 4 }));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Score(Category.Chance, new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Score(Category.Chance, new[] { 0, 2, 3, 4, 5 }));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Score(Category.Chance, new[] { 1, 2, 3, 4, 7 }));
        }

        // ---- Exhaustive sweep over all 6^5 = 7776 combinations -------------

        [Test]
        public void AllCombinations_SatisfyScoringInvariants()
        {
            foreach (var dice in AllDiceCombinations())
            {
                int sum = ScoreCalculator.Sum(dice);
                bool yahtzee = ScoreCalculator.IsYahtzee(dice);

                // Chance is always the raw sum.
                Assert.AreEqual(sum, ScoreCalculator.Score(Category.Chance, dice));

                // Upper boxes are face * count, and the six of them partition the sum.
                int upperTotal = 0;
                var counts = ScoreCalculator.Histogram(dice);
                for (int face = 1; face <= 6; face++)
                {
                    int s = ScoreCalculator.Score(CategoryExtensions.UpperCategoryForFace(face), dice);
                    Assert.AreEqual(face * counts[face], s);
                    upperTotal += s;
                }
                Assert.AreEqual(sum, upperTotal);

                // Fixed-value categories only ever score 0 or their fixed value.
                int fh = ScoreCalculator.Score(Category.FullHouse, dice);
                int ss = ScoreCalculator.Score(Category.SmallStraight, dice);
                int ls = ScoreCalculator.Score(Category.LargeStraight, dice);
                int ya = ScoreCalculator.Score(Category.Yahtzee, dice);
                Assert.IsTrue(fh == 0 || fh == 25);
                Assert.IsTrue(ss == 0 || ss == 30);
                Assert.IsTrue(ls == 0 || ls == 40);
                Assert.IsTrue(ya == 0 || ya == 50);

                // N-of-a-kind scores are 0 or the sum; 4K implies 3K.
                int three = ScoreCalculator.Score(Category.ThreeOfAKind, dice);
                int four = ScoreCalculator.Score(Category.FourOfAKind, dice);
                Assert.IsTrue(three == 0 || three == sum);
                Assert.IsTrue(four == 0 || four == sum);
                if (four > 0)
                    Assert.AreEqual(sum, three);

                // Large straight implies small straight.
                if (ls > 0)
                    Assert.AreEqual(30, ss);

                // Straight detection agrees with a naive distinct-run oracle.
                Assert.AreEqual(NaiveHasRun(dice, 4), ss > 0);
                Assert.AreEqual(NaiveHasRun(dice, 5), ls > 0);

                // A yahtzee scores 3K/4K at the sum and is never a natural full house.
                if (yahtzee)
                {
                    Assert.AreEqual(sum, three);
                    Assert.AreEqual(sum, four);
                    Assert.AreEqual(0, fh);
                    Assert.AreEqual(50, ya);
                }
            }
        }

        private static IEnumerable<int[]> AllDiceCombinations()
        {
            for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            for (int c = 1; c <= 6; c++)
            for (int d = 1; d <= 6; d++)
            for (int e = 1; e <= 6; e++)
                yield return new[] { a, b, c, d, e };
        }

        private static bool NaiveHasRun(int[] dice, int length)
        {
            var present = new HashSet<int>(dice);
            for (int start = 1; start + length - 1 <= 6; start++)
            {
                bool all = true;
                for (int v = start; v < start + length; v++)
                    if (!present.Contains(v))
                    {
                        all = false;
                        break;
                    }
                if (all)
                    return true;
            }
            return false;
        }
    }
}

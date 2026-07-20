using System;
using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    public class ScorecardTests
    {
        [Test]
        public void NewCard_AllOpen_ZeroTotals()
        {
            var card = new Scorecard();
            Assert.AreEqual(13, card.OpenCategories().Count());
            Assert.IsFalse(card.IsComplete);
            Assert.IsNull(card.GetScore(Category.Aces));
            Assert.AreEqual(0, card.UpperSubtotal);
            Assert.AreEqual(0, card.LowerSubtotal);
            Assert.AreEqual(0, card.Total);
            Assert.IsFalse(card.HasUpperBonus);
        }

        [Test]
        public void SetScore_LocksSlot_SecondWriteThrows()
        {
            var card = new Scorecard();
            card.SetScore(Category.Fives, 20);
            Assert.AreEqual(20, card.GetScore(Category.Fives));
            Assert.IsFalse(card.IsOpen(Category.Fives));
            Assert.Throws<InvalidOperationException>(() => card.SetScore(Category.Fives, 25));
            Assert.Throws<InvalidOperationException>(() => card.SetScore(Category.Fives, 0));
        }

        [Test]
        public void SetScore_RejectsNegative()
        {
            var card = new Scorecard();
            Assert.Throws<ArgumentOutOfRangeException>(() => card.SetScore(Category.Aces, -1));
        }

        [Test]
        public void ZeroScore_FillsTheSlot()
        {
            var card = new Scorecard();
            card.SetScore(Category.Yahtzee, 0);
            Assert.IsFalse(card.IsOpen(Category.Yahtzee));
            Assert.AreEqual(0, card.GetScore(Category.Yahtzee));
        }

        [TestCase(62, false, 62)]
        [TestCase(63, true, 98)]  // 63 + 35
        [TestCase(64, true, 99)]  // 64 + 35
        public void UpperBonus_ExactlyAtThreshold(int upperSum, bool expectBonus, int expectedTotal)
        {
            var card = new Scorecard();
            // Distribute upperSum across the six boxes (values need not be dice-plausible).
            card.SetScore(Category.Aces, upperSum - 50);
            card.SetScore(Category.Twos, 10);
            card.SetScore(Category.Threes, 10);
            card.SetScore(Category.Fours, 10);
            card.SetScore(Category.Fives, 10);
            card.SetScore(Category.Sixes, 10);
            Assert.AreEqual(upperSum, card.UpperSubtotal);
            Assert.AreEqual(expectBonus, card.HasUpperBonus);
            Assert.AreEqual(expectedTotal, card.Total);
        }

        [Test]
        public void Total_IncludesMultipleYahtzeeBonuses()
        {
            var card = new Scorecard();
            card.SetScore(Category.Yahtzee, 50);
            card.SetScore(Category.Chance, 20);
            card.YahtzeeBonusCount = 2;
            Assert.AreEqual(50 + 20 + 200, card.Total);
        }

        [Test]
        public void Total_FullCard_AllPartsSum()
        {
            var card = new Scorecard();
            card.SetScore(Category.Aces, 3);
            card.SetScore(Category.Twos, 6);
            card.SetScore(Category.Threes, 9);
            card.SetScore(Category.Fours, 12);
            card.SetScore(Category.Fives, 15);
            card.SetScore(Category.Sixes, 18);   // upper = 63 → bonus
            card.SetScore(Category.ThreeOfAKind, 20);
            card.SetScore(Category.FourOfAKind, 25);
            card.SetScore(Category.FullHouse, 25);
            card.SetScore(Category.SmallStraight, 30);
            card.SetScore(Category.LargeStraight, 40);
            card.SetScore(Category.Yahtzee, 50);
            card.SetScore(Category.Chance, 22);
            card.YahtzeeBonusCount = 1;
            Assert.IsTrue(card.IsComplete);
            Assert.AreEqual(63, card.UpperSubtotal);
            Assert.AreEqual(212, card.LowerSubtotal);
            Assert.AreEqual(63 + 35 + 212 + 100, card.Total);
        }
    }
}

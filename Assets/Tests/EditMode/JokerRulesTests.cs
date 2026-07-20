using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    public class JokerRulesTests
    {
        private static readonly int[] YahtzeeOfFours = { 4, 4, 4, 4, 4 };

        private static Scorecard CardWithYahtzee(int yahtzeeScore)
        {
            var card = new Scorecard();
            card.SetScore(Category.Yahtzee, yahtzeeScore);
            return card;
        }

        // ---- Situation detection ------------------------------------------

        [Test]
        public void NotAJoker_WhenYahtzeeBoxOpen()
        {
            Assert.IsFalse(JokerRules.IsJokerSituation(YahtzeeOfFours, new Scorecard()));
        }

        [Test]
        public void NotAJoker_WhenDiceAreNotYahtzee()
        {
            Assert.IsFalse(JokerRules.IsJokerSituation(new[] { 4, 4, 4, 4, 5 }, CardWithYahtzee(50)));
        }

        [Test]
        public void Joker_WhenYahtzeeBoxFilled_WithFiftyOrZero()
        {
            Assert.IsTrue(JokerRules.IsJokerSituation(YahtzeeOfFours, CardWithYahtzee(50)));
            Assert.IsTrue(JokerRules.IsJokerSituation(YahtzeeOfFours, CardWithYahtzee(0)));
        }

        // ---- Bonus ---------------------------------------------------------

        [Test]
        public void Bonus_OnlyWhenYahtzeeBoxHoldsFifty()
        {
            Assert.IsTrue(JokerRules.BonusApplies(CardWithYahtzee(50)));
            Assert.IsFalse(JokerRules.BonusApplies(CardWithYahtzee(0)));
            Assert.IsFalse(JokerRules.BonusApplies(new Scorecard())); // open box → no joker bonus
        }

        // ---- Forced priority 1: matching upper box ------------------------

        [Test]
        public void MatchingUpperOpen_IsTheOnlyLegalBox_AtSumOfDice()
        {
            var card = CardWithYahtzee(50);
            var legal = JokerRules.LegalCategories(YahtzeeOfFours, card);
            CollectionAssert.AreEqual(new[] { Category.Fours }, legal.ToArray());
            Assert.AreEqual(20, JokerRules.Score(Category.Fours, YahtzeeOfFours));
        }

        // ---- Forced priority 2: any open lower, FH/SS/LS at fixed value ---

        [Test]
        public void MatchingUpperFilled_AnyOpenLowerIsLegal()
        {
            var card = CardWithYahtzee(0);
            card.SetScore(Category.Fours, 12);
            var legal = JokerRules.LegalCategories(YahtzeeOfFours, card).ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    Category.ThreeOfAKind, Category.FourOfAKind, Category.FullHouse,
                    Category.SmallStraight, Category.LargeStraight, Category.Chance,
                },
                legal);
        }

        [Test]
        public void JokerScores_FixedForFhSsLs_SumForKindsAndChance()
        {
            Assert.AreEqual(25, JokerRules.Score(Category.FullHouse, YahtzeeOfFours));
            Assert.AreEqual(30, JokerRules.Score(Category.SmallStraight, YahtzeeOfFours));
            Assert.AreEqual(40, JokerRules.Score(Category.LargeStraight, YahtzeeOfFours));
            Assert.AreEqual(20, JokerRules.Score(Category.ThreeOfAKind, YahtzeeOfFours));
            Assert.AreEqual(20, JokerRules.Score(Category.FourOfAKind, YahtzeeOfFours));
            Assert.AreEqual(20, JokerRules.Score(Category.Chance, YahtzeeOfFours));
        }

        [Test]
        public void SomeLowersFilled_OnlyOpenOnesLegal()
        {
            var card = CardWithYahtzee(50);
            card.SetScore(Category.Fours, 12);
            card.SetScore(Category.FullHouse, 25);
            card.SetScore(Category.Chance, 18);
            var legal = JokerRules.LegalCategories(YahtzeeOfFours, card).ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    Category.ThreeOfAKind, Category.FourOfAKind,
                    Category.SmallStraight, Category.LargeStraight,
                },
                legal);
        }

        // ---- Forced priority 3: zero in an open upper box -----------------

        [Test]
        public void AllLowersAndMatchingUpperFilled_ForcedZeroInOpenUpper()
        {
            var card = CardWithYahtzee(50);
            card.SetScore(Category.Fours, 12);
            card.SetScore(Category.ThreeOfAKind, 20);
            card.SetScore(Category.FourOfAKind, 22);
            card.SetScore(Category.FullHouse, 25);
            card.SetScore(Category.SmallStraight, 30);
            card.SetScore(Category.LargeStraight, 40);
            card.SetScore(Category.Chance, 21);
            card.SetScore(Category.Aces, 2);

            var legal = JokerRules.LegalCategories(YahtzeeOfFours, card).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { Category.Twos, Category.Threes, Category.Fives, Category.Sixes },
                legal);

            // A non-matching upper box scores 0 for a yahtzee of fours.
            foreach (var c in legal)
                Assert.AreEqual(0, JokerRules.Score(c, YahtzeeOfFours));
        }
    }
}

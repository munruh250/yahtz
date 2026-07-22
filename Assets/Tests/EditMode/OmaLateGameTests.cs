using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    /// <summary>Oma's end-of-game category choices on hand-built decisive states — the "is her
    /// late-game logic sound?" question, made concrete. Each scenario has an unambiguous right
    /// answer; if the heuristic ever regresses into taking the wrong box late, one of these fails.
    ///
    /// DecideCategory is a pure query of the state, so these need no rolls or physics — set the
    /// board, set the final dice, ask.</summary>
    public class OmaLateGameTests
    {
        /// <summary>A Deciding state for Oma with the given boxes already filled and these dice on
        /// the table. Any category not in <paramref name="filled"/> is open.</summary>
        private static GameEngine StateWith(int[] dice, params (Category cat, int score)[] filled)
        {
            var state = new GameState
            {
                CurrentPlayer = PlayerId.Oma,
                Phase = GamePhase.Deciding,
                Round = 13,
            };
            state.Dice.Values = dice;
            state.Dice.RollsUsed = 3; // rolls spent — she must score now
            foreach (var (cat, score) in filled)
                state.OmaCard.SetScore(cat, score);
            return GameEngine.FromState(state);
        }

        private static Category Decide(GameEngine engine) => new OmaAI().DecideCategory(engine);

        /// <summary>The owner's scenario, and the sound answer: Sixes still open, a six-heavy roll.
        /// Banking it in Sixes is correct — it is the most points AND drives the upper bonus. The
        /// worry that she "just went with 6" was the right call.</summary>
        [Test]
        public void SixHeavyRoll_WithSixesOpen_TakesSixes()
        {
            // Four 6s. Filled: everything upper except Fives and Sixes, so the bonus is live.
            var engine = StateWith(new[] { 6, 6, 6, 6, 2 },
                (Category.Aces, 3), (Category.Twos, 6), (Category.Threes, 9), (Category.Fours, 12),
                (Category.FullHouse, 25), (Category.SmallStraight, 30));
            Assert.AreEqual(Category.Sixes, Decide(engine),
                "a four-six roll with Sixes open should go to Sixes — most points and bonus pace");
        }

        /// <summary>Variant: Sixes already filled, Fives still needed. She can't make Fives from a
        /// six roll, so she must bank the 6s elsewhere — and must NOT zero a valuable box to do it.
        /// The sound move is the best available real score (3 of a Kind / Chance = 26), keeping
        /// Fives open for a future five attempt.</summary>
        [Test]
        public void SixRoll_WithSixesFilled_BanksElsewhere_DoesNotWasteFives()
        {
            var engine = StateWith(new[] { 6, 6, 6, 6, 2 },
                (Category.Sixes, 24), (Category.Aces, 3), (Category.Twos, 6), (Category.Threes, 9),
                (Category.Fours, 12), (Category.SmallStraight, 30));
            var choice = Decide(engine);
            Assert.AreNotEqual(Category.Fives, choice, "should not throw away the Fives box on a six roll");
            Assert.That(choice, Is.EqualTo(Category.FourOfAKind).Or.EqualTo(Category.ThreeOfAKind).Or.EqualTo(Category.Chance),
                $"should bank the 26 in a scoring box; chose {choice}");
        }

        /// <summary>A mediocre roll with the Five-of-a-Kind (Yahtzee) box and a cheap box both open.
        /// She must dump the bad roll in the cheap box, not burn the 50-point box on a zero.</summary>
        [Test]
        public void MediocreRoll_DoesNotBurnTheFiveOfAKindBox()
        {
            var engine = StateWith(new[] { 1, 2, 4, 6, 3 }, // no pair, no straight-5
                (Category.Twos, 4), (Category.Threes, 6), (Category.Fours, 8), (Category.Fives, 10),
                (Category.Sixes, 12), (Category.ThreeOfAKind, 0), (Category.FourOfAKind, 0),
                (Category.FullHouse, 0), (Category.SmallStraight, 0), (Category.LargeStraight, 0));
            // Open: Aces (=1) and Yahtzee/Five-of-a-Kind (=0). Take the point in Aces.
            Assert.AreEqual(Category.Aces, Decide(engine),
                "a junk roll should go in the cheap Aces box, never zero the Five-of-a-Kind box");
        }

        /// <summary>A made big hand is always taken.</summary>
        [Test]
        public void LargeStraight_IsTaken_WhenRolled()
        {
            var engine = StateWith(new[] { 2, 3, 4, 5, 6 },
                (Category.Aces, 1), (Category.Twos, 2), (Category.Threes, 3));
            Assert.AreEqual(Category.LargeStraight, Decide(engine));
        }

        /// <summary>A roll that would secure the +35 bonus should be taken there, over a nominally
        /// similar-value lower box — the bonus is worth far more than the raw points.</summary>
        [Test]
        public void ARollThatSecuresTheBonus_TakesIt()
        {
            // Upper subtotal 52 (needs 11 more for 63). Three 5s = 15 in Fives clears it.
            var engine = StateWith(new[] { 5, 5, 5, 2, 1 },
                (Category.Aces, 3), (Category.Twos, 8), (Category.Threes, 12), (Category.Fours, 12),
                (Category.Sixes, 17), (Category.Chance, 0));
            // Open: Fives (=15, secures bonus) and 3/4-of-a-kind (=18). Bonus must win.
            Assert.AreEqual(Category.Fives, Decide(engine),
                "scoring Fives here secures the +35 bonus and should outweigh a slightly bigger lower box");
        }

        /// <summary>The last open box is simply taken, whatever it scores.</summary>
        [Test]
        public void LastBox_IsForced()
        {
            var filled = new (Category, int)[]
            {
                (Category.Aces, 3), (Category.Twos, 6), (Category.Threes, 9), (Category.Fours, 12),
                (Category.Fives, 15), (Category.Sixes, 18), (Category.ThreeOfAKind, 20),
                (Category.FourOfAKind, 0), (Category.FullHouse, 25), (Category.SmallStraight, 30),
                (Category.LargeStraight, 40), (Category.Yahtzee, 0),
            };
            var engine = StateWith(new[] { 6, 6, 3, 2, 1 }, filled); // only Chance open
            Assert.AreEqual(Category.Chance, Decide(engine));
        }
    }
}

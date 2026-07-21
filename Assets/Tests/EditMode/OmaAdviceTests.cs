using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    /// <summary>"Ask Oma" advice. It runs the same evaluator she plays by, so the contract that
    /// matters is that asking is *free*: a pure query that consumes no RNG and mutates nothing,
    /// however many times it is used.</summary>
    public class OmaAdviceTests
    {
        /// <summary>Puts a specific hand on the table: the scripted RNG hands out exactly these
        /// faces on the first roll.</summary>
        private static GameEngine EngineWith(params int[] faces)
        {
            var engine = new GameEngine(new GameState(), new ScriptedRandomSource(faces));
            engine.Roll();
            return engine;
        }

        [Test]
        public void KeepsThePair_AndNamesWhatItIsChasing()
        {
            var engine = EngineWith(5, 5, 2, 3, 6);
            var advice = new OmaAI().Advise(engine);

            Assert.IsFalse(advice.ScoreNow, "two rolls left and only a pair — she would reroll");
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(engine.State.Dice.Values[i] == 5, advice.Keep[i], $"die {i}");
            Assert.AreEqual(3, advice.RerollCount);
            Assert.AreEqual(Category.ThreeOfAKind, advice.Target);
        }

        /// <summary>The owner's actual question: two 5s and two 4s, chasing three of a kind. Is it
        /// better to hold both pairs and roll one, or hold the 5s and roll three? She must give a
        /// definite answer, and the reroll count has to match the dice she kept.</summary>
        [Test]
        public void TwoPair_GivesACoherentAnswer()
        {
            var engine = EngineWith(5, 5, 4, 4, 2);
            var advice = new OmaAI().Advise(engine);

            Assert.IsFalse(advice.ScoreNow);
            int kept = 0;
            for (int i = 0; i < 5; i++)
                if (advice.Keep[i])
                {
                    kept++;
                    Assert.Contains(engine.State.Dice.Values[i], new[] { 4, 5 },
                        "she should not hold the odd die");
                }
            Assert.AreEqual(5 - kept, advice.RerollCount);
            // Full House is open here, so two pair is a legitimate goal; three of a kind is the
            // other. Either is coherent — an upper box or Chance would not be.
            Assert.That(advice.Target,
                Is.EqualTo(Category.FullHouse).Or.EqualTo(Category.ThreeOfAKind)
                  .Or.EqualTo(Category.Fives).Or.EqualTo(Category.Fours));
        }

        [Test]
        public void StandsPat_OnAMadeFullHouse()
        {
            var engine = EngineWith(3, 3, 3, 6, 6);
            var advice = new OmaAI().Advise(engine);

            Assert.IsTrue(advice.ScoreNow, "a made full house is worth banking");
            Assert.AreEqual(0, advice.RerollCount);
            Assert.Greater(advice.TargetPoints, 0);
        }

        [Test]
        public void OutOfRolls_RecommendsABoxWorthTaking()
        {
            // Three full rolls: nothing is kept, so all five dice are redrawn each time.
            var engine = new GameEngine(new GameState(), new ScriptedRandomSource(
                1, 2, 3, 4, 5,
                2, 2, 6, 1, 3,
                6, 6, 2, 4, 1));
            engine.Roll();
            engine.Roll();
            engine.Roll();

            var advice = new OmaAI().Advise(engine);
            Assert.IsTrue(advice.ScoreNow);
            Assert.IsTrue(engine.CurrentCard.IsOpen(advice.Target), "she cannot point at a filled box");
            Assert.IsTrue(engine.GetPotentialScores().ContainsKey(advice.Target),
                "and it must be legal to score right now (Joker rules included)");
        }

        /// <summary>Asking must never cost anything. If it drew from the RNG or touched state,
        /// a player who asked twice would get different dice from one who never asked.</summary>
        [Test]
        public void AskingIsFree_NoRngDrawsNoMutation()
        {
            var engine = EngineWith(5, 5, 2, 3, 6);
            string before = SnapshotOf(engine);
            int drawsBefore = engine.State.RngDraws;

            var oma = new OmaAI();
            for (int i = 0; i < 25; i++)
                oma.Advise(engine);

            Assert.AreEqual(drawsBefore, engine.State.RngDraws, "advice consumed RNG draws");
            Assert.AreEqual(before, SnapshotOf(engine), "advice mutated game state");
        }

        [Test]
        public void RepeatedAdvice_IsDeterministic()
        {
            var engine = EngineWith(2, 2, 2, 5, 1);
            var oma = new OmaAI();
            var first = oma.Advise(engine);
            var second = oma.Advise(engine);

            Assert.AreEqual(first.ScoreNow, second.ScoreNow);
            Assert.AreEqual(first.Target, second.Target);
            CollectionAssert.AreEqual(first.Keep, second.Keep);
        }

        [Test]
        public void ThrowsOutsideDeciding()
        {
            var engine = new GameEngine(new GameState(), new ScriptedRandomSource(1, 1, 1, 1, 1));
            Assert.Throws<System.InvalidOperationException>(() => new OmaAI().Advise(engine));
        }

        private static string SnapshotOf(GameEngine engine)
        {
            var state = engine.State;
            var sb = new System.Text.StringBuilder();
            sb.Append(state.Round).Append('|').Append(state.CurrentPlayer).Append('|').Append(state.Phase);
            foreach (int value in state.Dice.Values) sb.Append('|').Append(value);
            foreach (bool kept in state.Dice.Kept) sb.Append('|').Append(kept);
            for (int i = 0; i < CategoryExtensions.Count; i++)
                sb.Append('|').Append(state.PlayerCard.GetScore((Category)i)?.ToString() ?? "-");
            return sb.ToString();
        }
    }
}

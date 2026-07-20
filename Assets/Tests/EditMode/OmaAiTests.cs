using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    public class OmaAiTests
    {
        /// <summary>Drives one full turn the way GameController does in M3.</summary>
        public static void PlayAiTurn(GameEngine engine, OmaAI ai)
        {
            engine.Roll();
            while (engine.RollsRemaining > 0)
            {
                var keep = ai.DecideKeepers(engine);
                bool all = true;
                for (int i = 0; i < DiceState.DieCount; i++)
                {
                    engine.SetKeep(i, keep[i]);
                    all &= keep[i];
                }
                if (all)
                    break; // stands pat — score now
                engine.Roll();
            }
            engine.ScoreCategory(ai.DecideCategory(engine));
        }

        // ---- Determinism (skip must never change outcomes) ------------------

        [Test]
        public void Decisions_AreDeterministic_GivenSameState()
        {
            var ai = new OmaAI();
            for (int seed = 0; seed < 25; seed++)
            {
                var engineA = GameEngine.NewGame(seed);
                var engineB = GameEngine.NewGame(seed);
                for (int turn = 0; turn < 6; turn++)
                {
                    PlayAiTurn(engineA, ai);
                    PlayAiTurn(engineB, ai);
                }
                Assert.AreEqual(engineA.State.PlayerCard.Total, engineB.State.PlayerCard.Total, $"seed {seed}");
                CollectionAssert.AreEqual(engineA.State.PlayerCard.Slots, engineB.State.PlayerCard.Slots, $"seed {seed}");
                CollectionAssert.AreEqual(engineA.State.OmaCard.Slots, engineB.State.OmaCard.Slots, $"seed {seed}");
            }
        }

        [Test]
        public void AiQueries_DoNotMutateState()
        {
            var engine = GameEngine.NewGame(7);
            engine.Roll();
            string before = Services.SaveService.ToJson(engine.State);
            var ai = new OmaAI();
            ai.DecideKeepers(engine);
            ai.DecideCategory(engine);
            Assert.AreEqual(before, Services.SaveService.ToJson(engine.State));
        }

        // ---- Sensible play in forced situations ------------------------------

        [Test]
        public void KeepsAllFive_OnMadeYahtzee_AndLargeStraight()
        {
            var ai = new OmaAI();

            var yahtzeeState = new GameState();
            var engine = new GameEngine(yahtzeeState, new ScriptedRandomSource(4, 4, 4, 4, 4));
            engine.Roll();
            Assert.IsTrue(ai.DecideKeepers(engine).All(k => k));
            Assert.AreEqual(Category.Yahtzee, ai.DecideCategory(engine));

            var straightState = new GameState();
            var engine2 = new GameEngine(straightState, new ScriptedRandomSource(2, 3, 4, 5, 6));
            engine2.Roll();
            Assert.IsTrue(ai.DecideKeepers(engine2).All(k => k));
            Assert.AreEqual(Category.LargeStraight, ai.DecideCategory(engine2));
        }

        [Test]
        public void JokerTurn_PicksOnlyFromLegalSet()
        {
            var state = new GameState();
            state.OmaCard.SetScore(Category.Yahtzee, 50); // Oma's own card mid-game
            state.CurrentPlayer = PlayerId.Oma;
            var engine = new GameEngine(state, new ScriptedRandomSource(4, 4, 4, 4, 4));
            engine.Roll();

            var ai = new OmaAI();
            var choice = ai.DecideCategory(engine);
            CollectionAssert.Contains(engine.GetLegalCategories().ToArray(), choice);
            Assert.AreEqual(Category.Fours, choice, "forced matching upper box");
        }

        [Test]
        public void PrefersNotBurning_YahtzeeBox_ForAJunkRoll()
        {
            // Junk roll, everything open: she must dump into something cheap
            // (Aces/Twos-tier), never zero the Yahtzee box or a straight.
            var engine = new GameEngine(new GameState(), new ScriptedRandomSource(
                1, 2, 2, 4, 6, // roll 1: junk
                1, 3, 5,       // enough spare values for any rerolls
                2, 6, 1, 3, 2, 4, 1));
            engine.Roll();
            var ai = new OmaAI();
            // Whatever she rerolls, her final category for a weak hand must not be
            // a premium box at 0. Drive the turn to completion:
            PlayAiTurn2(engine, ai, out var chosen, out int points);
            if (points == 0)
            {
                Assert.AreNotEqual(Category.Yahtzee, chosen);
                Assert.AreNotEqual(Category.LargeStraight, chosen);
                Assert.AreNotEqual(Category.SmallStraight, chosen);
            }
        }

        private static void PlayAiTurn2(GameEngine engine, OmaAI ai, out Category chosen, out int points)
        {
            while (engine.RollsRemaining > 0)
            {
                var keep = ai.DecideKeepers(engine);
                bool all = true;
                for (int i = 0; i < DiceState.DieCount; i++)
                {
                    engine.SetKeep(i, keep[i]);
                    all &= keep[i];
                }
                if (all)
                    break;
                engine.Roll();
            }
            chosen = ai.DecideCategory(engine);
            points = engine.GetPotentialScores()[chosen];
            engine.ScoreCategory(chosen);
        }

        // ---- Strength & legality over many self-play games -------------------

        [Test]
        public void SelfPlay_1000Games_LegalAndInTargetBand()
        {
            var ai = new OmaAI();
            var totals = new List<int>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var engine = GameEngine.NewGame(seed);
                int guard = 0;
                while (engine.State.Phase != GamePhase.GameOver)
                {
                    PlayAiTurn(engine, ai); // any illegal action throws and fails the test
                    Assert.Less(++guard, 27, $"seed {seed}");
                }
                totals.Add(engine.State.PlayerCard.Total);
                totals.Add(engine.State.OmaCard.Total);
            }
            double mean = totals.Average();
            // Design target 200-230 (spec §4); fixed seeds → deterministic, small margin
            // so future weight nudges don't need test edits unless strength really moves.
            Assert.That(mean, Is.InRange(200.0, 235.0), $"Oma strength off target: mean {mean:F1}");
        }
    }
}

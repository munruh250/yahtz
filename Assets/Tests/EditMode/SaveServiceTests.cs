using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>M1 save-format decision tests: GameState (with its -1 open-slot sentinel)
    /// must round-trip losslessly through JsonUtility, including mid-turn states, and a
    /// resumed game must continue on the identical die stream.</summary>
    public class SaveServiceTests
    {
        [SetUp]
        public void SetUp() => SaveService.Delete();

        [TearDown]
        public void TearDown() => SaveService.Delete();

        private static GameState BuildMidGameState()
        {
            var engine = GameEngine.NewGame(seed: 424242);
            for (int i = 0; i < 7; i++)
                TestPolicies.PlayGreedyTurn(engine);
            // Leave the state mid-turn: one roll made, two keepers set.
            engine.Roll();
            engine.SetKeep(0, true);
            engine.SetKeep(3, true);
            return engine.State;
        }

        [Test]
        public void MidTurnState_RoundTripsLosslessly()
        {
            var original = BuildMidGameState();
            string json = SaveService.ToJson(original);
            var loaded = SaveService.FromJson(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(original.SaveVersion, loaded.SaveVersion);
            Assert.AreEqual(original.Seed, loaded.Seed);
            Assert.AreEqual(original.RngDraws, loaded.RngDraws);
            Assert.AreEqual(original.Round, loaded.Round);
            Assert.AreEqual(original.CurrentPlayer, loaded.CurrentPlayer);
            Assert.AreEqual(original.Phase, loaded.Phase);
            CollectionAssert.AreEqual(original.Dice.Values, loaded.Dice.Values);
            CollectionAssert.AreEqual(original.Dice.Kept, loaded.Dice.Kept);
            Assert.AreEqual(original.Dice.RollsUsed, loaded.Dice.RollsUsed);
            CollectionAssert.AreEqual(original.PlayerCard.Slots, loaded.PlayerCard.Slots);
            CollectionAssert.AreEqual(original.OmaCard.Slots, loaded.OmaCard.Slots);
            Assert.AreEqual(original.PlayerCard.YahtzeeBonusCount, loaded.PlayerCard.YahtzeeBonusCount);
            Assert.AreEqual(original.OmaCard.YahtzeeBonusCount, loaded.OmaCard.YahtzeeBonusCount);

            // Open (-1) and zero-scored slots must be distinguishable after the trip.
            Assert.AreEqual(json, SaveService.ToJson(loaded));
        }

        [Test]
        public void FreshState_OpenSlots_SurviveRoundTrip()
        {
            var loaded = SaveService.FromJson(SaveService.ToJson(new GameState { Seed = 7 }));
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.PlayerCard.OpenCategories().Count() == 13);
            Assert.IsNull(loaded.PlayerCard.GetScore(Category.Chance));
            Assert.AreEqual(GamePhase.TurnStart, loaded.Phase);
        }

        [Test]
        public void ResumedGame_ContinuesOnIdenticalDiceStream()
        {
            // Engine A plays straight through; engine B is saved and resumed mid-game,
            // then both finish with the same deterministic policy.
            var engineA = GameEngine.NewGame(seed: 987654);
            for (int i = 0; i < 9; i++)
                TestPolicies.PlayGreedyTurn(engineA);

            var engineB = GameEngine.FromState(
                SaveService.FromJson(SaveService.ToJson(engineA.State)));

            var rollsA = new List<int[]>();
            var rollsB = new List<int[]>();
            engineA.EventRaised += e => { if (e is DiceRolled r) rollsA.Add(r.Values); };
            engineB.EventRaised += e => { if (e is DiceRolled r) rollsB.Add(r.Values); };

            while (engineA.State.Phase != GamePhase.GameOver)
                TestPolicies.PlayGreedyTurn(engineA);
            while (engineB.State.Phase != GamePhase.GameOver)
                TestPolicies.PlayGreedyTurn(engineB);

            Assert.AreEqual(rollsA.Count, rollsB.Count);
            for (int i = 0; i < rollsA.Count; i++)
                CollectionAssert.AreEqual(rollsA[i], rollsB[i], $"roll {i} diverged after resume");
            Assert.AreEqual(SaveService.ToJson(engineA.State), SaveService.ToJson(engineB.State));
            Assert.AreEqual(engineA.State.PlayerCard.Total, engineB.State.PlayerCard.Total);
        }

        [Test]
        public void FromJson_RejectsGarbageAndWrongVersion()
        {
            Assert.IsNull(SaveService.FromJson(null));
            Assert.IsNull(SaveService.FromJson(""));
            Assert.IsNull(SaveService.FromJson("not json at all {"));

            string wrongVersion = SaveService.ToJson(new GameState())
                .Replace("\"SaveVersion\":1", "\"SaveVersion\":999");
            StringAssert.Contains("999", wrongVersion); // guard the replace actually hit
            Assert.IsNull(SaveService.FromJson(wrongVersion));
        }

        [Test]
        public void FileRoundTrip_SaveLoadDelete_And_ResumableCheck()
        {
            Assert.IsNull(SaveService.TryLoad());
            Assert.IsFalse(SaveService.HasResumableSave());

            var state = BuildMidGameState();
            SaveService.Save(state);
            var loaded = SaveService.TryLoad();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveService.ToJson(state), SaveService.ToJson(loaded));
            Assert.IsTrue(SaveService.HasResumableSave());

            // A finished game is not resumable.
            state.Phase = GamePhase.GameOver;
            SaveService.Save(state);
            Assert.IsFalse(SaveService.HasResumableSave());

            SaveService.Delete();
            Assert.IsNull(SaveService.TryLoad());
        }
    }
}

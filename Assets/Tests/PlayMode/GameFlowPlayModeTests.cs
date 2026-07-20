using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yahtzee.Core;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>M2 exit checks, driven through GameController's public input surface (the
    /// same methods the UI buttons call) with animations disabled. Verifies the full loop is
    /// playable and save/continue works across a scene reload.</summary>
    public class GameFlowPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.AnimationsEnabled = false;
            SaveService.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            GameController.AnimationsEnabled = true;
            SaveService.Delete();
        }

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null; // one frame for Awake/Start
        }

        private static GameController Controller()
        {
            var controller = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found — was the scene bootstrapped?");
            Assert.IsNotNull(controller.Engine, "Engine not initialized");
            return controller;
        }

        private static void PlayOneTurn(GameController controller)
        {
            controller.OnRollTapped();
            Assert.IsFalse(controller.InputLocked, "animations disabled — input should unlock synchronously");
            var best = BestCategory(controller.Engine.GetPotentialScores());
            controller.OnCellTapped(best); // select
            controller.OnCellTapped(best); // confirm
        }

        private static Category BestCategory(IReadOnlyDictionary<Category, int> potentials)
        {
            Category best = default;
            int bestScore = -1;
            foreach (var pair in potentials)
                if (pair.Value > bestScore)
                {
                    best = pair.Key;
                    bestScore = pair.Value;
                }
            return best;
        }

        [UnityTest]
        public IEnumerator FullGame_PlayableThroughController()
        {
            yield return LoadGameScene();
            var controller = Controller();

            int guard = 0;
            while (controller.Engine.State.Phase != GamePhase.GameOver)
            {
                PlayOneTurn(controller);
                Assert.Less(++guard, 27, "game did not finish in 26 turns");
                if (guard % 6 == 0)
                    yield return null;
            }

            Assert.IsTrue(controller.Engine.State.PlayerCard.IsComplete);
            Assert.IsTrue(controller.Engine.State.OmaCard.IsComplete);
            // The finished game was saved but must not offer Continue.
            Assert.IsFalse(SaveService.HasResumableSave());
        }

        [UnityTest]
        public IEnumerator SaveAndReload_ContinuesSameGame()
        {
            yield return LoadGameScene();
            var controller = Controller();

            for (int i = 0; i < 5; i++)
                PlayOneTurn(controller); // saves on every TurnChanged

            Assert.IsTrue(SaveService.HasResumableSave());
            string savedJson = SaveService.ToJson(SaveService.TryLoad());

            yield return LoadGameScene();
            var resumed = Controller();

            Assert.AreEqual(savedJson, SaveService.ToJson(resumed.Engine.State),
                "reloaded scene did not resume the saved game state");
            Assert.AreEqual(3, resumed.Engine.State.Round);
            Assert.AreEqual(PlayerId.Oma, resumed.Engine.State.CurrentPlayer);

            // And the resumed game must still be playable to completion.
            int guard = 0;
            while (resumed.Engine.State.Phase != GamePhase.GameOver)
            {
                PlayOneTurn(resumed);
                Assert.Less(++guard, 27);
                if (guard % 6 == 0)
                    yield return null;
            }
        }

        [UnityTest]
        public IEnumerator IllegalTaps_AreIgnoredNotThrown()
        {
            yield return LoadGameScene();
            var controller = Controller();

            // Before any roll: die taps and cell taps must be no-ops.
            controller.OnDieTapped(0);
            controller.OnCellTapped(Category.Chance);
            Assert.AreEqual(GamePhase.TurnStart, controller.Engine.State.Phase);

            controller.OnRollTapped();
            controller.OnRollTapped();
            controller.OnRollTapped();
            Assert.AreEqual(0, controller.Engine.RollsRemaining);
            controller.OnRollTapped(); // fourth roll tap: ignored
            controller.OnDieTapped(0); // keeps are moot after roll 3: ignored
            Assert.AreEqual(0, controller.Engine.RollsRemaining);

            // Selecting one cell then confirming a different one must not score the first.
            controller.OnCellTapped(Category.Chance);
            controller.OnCellTapped(Category.Aces);   // switches selection
            Assert.AreEqual(GamePhase.Deciding, controller.Engine.State.Phase);
            controller.OnCellTapped(Category.Aces);   // confirm
            Assert.IsFalse(controller.Engine.State.PlayerCard.IsOpen(Category.Aces));
            Assert.IsTrue(controller.Engine.State.PlayerCard.IsOpen(Category.Chance));
            yield return null;
        }
    }
}

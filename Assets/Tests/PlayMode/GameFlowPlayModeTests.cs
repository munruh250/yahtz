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
    /// <summary>M2/M3 exit checks through GameController's public input surface (the same
    /// methods the UI wires up). M3: Oma plays her own turns automatically; the driver only
    /// plays the human side and waits for her.</summary>
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
            GameController.Use3dDice = true;
            SaveService.Delete();
        }

        [UnityTest]
        public IEnumerator LegacyFlatLayer_StillPlaysBehindDebugFlag()
        {
            // TECH_PLAN §7: the 2D prototype layer stays available for fast rules testing.
            GameController.Use3dDice = false;
            yield return LoadGameScene();
            var controller = Controller();
            for (int i = 0; i < 2; i++)
            {
                PlayPlayerTurn(controller);
                yield return WaitForOma(controller);
            }
            Assert.AreEqual(3, controller.Engine.State.Round);
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

        private static void PlayPlayerTurn(GameController controller)
        {
            Assert.AreEqual(PlayerId.Player, controller.Engine.State.CurrentPlayer);
            controller.OnRollTapped();
            Assert.IsFalse(controller.InputLocked, "animations disabled — input should unlock synchronously");
            var best = BestCategory(controller.Engine.GetPotentialScores());
            controller.OnCellTapped(best); // select
            controller.OnCellTapped(best); // confirm → Oma's turn starts automatically
        }

        private static IEnumerator WaitForOma(GameController controller, int maxFrames = 900)
        {
            int frames = 0;
            while (controller.IsOmaTurn)
            {
                Assert.Less(++frames, maxFrames, "Oma's turn did not finish in time");
                yield return null;
            }
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

        private static int FilledCount(Scorecard card)
        {
            int filled = 0;
            for (int i = 0; i < CategoryExtensions.Count; i++)
                if (!card.IsOpen((Category)i))
                    filled++;
            return filled;
        }

        // ---- Full loop -------------------------------------------------------

        [UnityTest]
        public IEnumerator FullGame_PlayerVsAutoOma()
        {
            yield return LoadGameScene();
            var controller = Controller();

            int playerTurns = 0;
            while (controller.Engine.State.Phase != GamePhase.GameOver)
            {
                PlayPlayerTurn(controller);
                playerTurns++;
                yield return WaitForOma(controller);
                Assert.Less(playerTurns, 14, "game did not finish in 13 rounds");
            }

            Assert.AreEqual(13, playerTurns);
            Assert.IsTrue(controller.Engine.State.PlayerCard.IsComplete);
            Assert.IsTrue(controller.Engine.State.OmaCard.IsComplete, "Oma must have finished her card");
            Assert.IsFalse(SaveService.HasResumableSave());
        }

        [UnityTest]
        public IEnumerator OmaTurn_RunsAutomatically_FillsExactlyOneBox()
        {
            yield return LoadGameScene();
            var controller = Controller();
            int before = FilledCount(controller.Engine.State.OmaCard);

            PlayPlayerTurn(controller);
            yield return WaitForOma(controller);

            Assert.AreEqual(before + 1, FilledCount(controller.Engine.State.OmaCard));
            Assert.AreEqual(PlayerId.Player, controller.Engine.State.CurrentPlayer);
            Assert.AreEqual(2, controller.Engine.State.Round);
        }

        [UnityTest]
        public IEnumerator Skip_FastForwardsOmasPacedTurn()
        {
            yield return LoadGameScene();
            var controller = Controller();

            // Real pacing on (a full Oma turn takes 6-12 s unskipped)...
            GameController.AnimationsEnabled = true;
            controller.OnRollTapped();
            int settleFrames = 0;
            while (controller.InputLocked)
            {
                Assert.Less(++settleFrames, 300, "player roll animation never settled");
                yield return null;
            }
            var best = BestCategory(controller.Engine.GetPotentialScores());
            controller.OnCellTapped(best);
            controller.OnCellTapped(best);
            Assert.IsTrue(controller.IsOmaTurn);

            // ...but spamming Skip must finish her turn in well under a second.
            int frames = 0;
            while (controller.IsOmaTurn)
            {
                controller.OnSkipTapped();
                Assert.Less(++frames, 120, "skip did not fast-forward Oma's turn");
                yield return null;
            }
            Assert.AreEqual(1, FilledCount(controller.Engine.State.OmaCard));
        }

        // ---- Save / continue -------------------------------------------------

        [UnityTest]
        public IEnumerator SaveAndReload_ContinuesSameGame()
        {
            yield return LoadGameScene();
            var controller = Controller();

            for (int i = 0; i < 4; i++)
            {
                PlayPlayerTurn(controller);
                yield return WaitForOma(controller);
            }

            Assert.IsTrue(SaveService.HasResumableSave());
            string savedJson = SaveService.ToJson(SaveService.TryLoad());

            yield return LoadGameScene();
            var resumed = Controller();

            Assert.AreEqual(savedJson, SaveService.ToJson(resumed.Engine.State),
                "reloaded scene did not resume the saved game state");
            Assert.AreEqual(5, resumed.Engine.State.Round);
            Assert.AreEqual(PlayerId.Player, resumed.Engine.State.CurrentPlayer);

            while (resumed.Engine.State.Phase != GamePhase.GameOver)
            {
                PlayPlayerTurn(resumed);
                yield return WaitForOma(resumed);
            }
        }

        // ---- Input legality ----------------------------------------------------

        [UnityTest]
        public IEnumerator IllegalTaps_AreIgnoredNotThrown()
        {
            yield return LoadGameScene();
            var controller = Controller();

            // Before any roll: die taps and cell taps must be no-ops.
            controller.OnDieTapped(0);
            controller.OnCellTapped(Category.Chance);
            controller.OnSkipTapped(); // not Oma's turn: no-op
            Assert.AreEqual(GamePhase.TurnStart, controller.Engine.State.Phase);

            controller.OnRollTapped();
            controller.OnRollTapped();
            controller.OnRollTapped();
            Assert.AreEqual(0, controller.Engine.RollsRemaining);
            controller.OnRollTapped(); // fourth roll tap: ignored
            controller.OnDieTapped(0); // keeps are moot after roll 3: ignored
            Assert.AreEqual(0, controller.Engine.RollsRemaining);

            // Peeking at Oma's card disables scoring taps entirely.
            controller.OnPeekTapped();
            controller.OnCellTapped(Category.Chance);
            controller.OnCellTapped(Category.Chance);
            Assert.AreEqual(GamePhase.Deciding, controller.Engine.State.Phase);
            Assert.IsTrue(controller.Engine.State.PlayerCard.IsOpen(Category.Chance));
            controller.OnPeekTapped(); // back to own card

            // Selecting one cell then confirming a different one must not score the first.
            controller.OnCellTapped(Category.Chance);
            controller.OnCellTapped(Category.Aces);   // switches selection
            Assert.AreEqual(GamePhase.Deciding, controller.Engine.State.Phase);
            controller.OnCellTapped(Category.Aces);   // confirm
            Assert.IsFalse(controller.Engine.State.PlayerCard.IsOpen(Category.Aces));
            Assert.IsTrue(controller.Engine.State.PlayerCard.IsOpen(Category.Chance));

            yield return WaitForOma(controller);
        }
    }
}

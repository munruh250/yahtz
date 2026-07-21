using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yahtzee.Core;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>The "Ask Oma" hint button and her speech bubble.</summary>
    public class AskOmaTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.AnimationsEnabled = false;
            GameController.Use3dDice = true;
            SaveService.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            GameController.AnimationsEnabled = true;
            SaveService.Delete();
        }

        [UnityTest]
        public IEnumerator AskingAfterARoll_ShowsAdvice()
        {
            yield return LoadGameScene();
            var controller = Object.FindAnyObjectByType<GameController>();
            var bubble = Object.FindAnyObjectByType<SpeechBubbleView>();
            Assert.IsNotNull(bubble);
            // Oma greets the player at game start (design §2), so clear that first: this test is
            // about the hint, not about the bubble being empty.
            bubble.Hide();

            controller.OnRollTapped();
            controller.OnAskOmaTapped();
            yield return null;

            Assert.IsTrue(bubble.IsShowing, "asking should put a bubble on screen");
            Assert.IsNotEmpty(bubble.Message);
            Assert.IsTrue(bubble.Message.Contains("Keep") || bubble.Message.Contains("take")
                          || bubble.Message.Contains("zero"),
                $"advice should actually advise something; got: {bubble.Message}");
            TestContext.WriteLine($"Oma says: {bubble.Message}");
        }

        /// <summary>Design §5.2: the bubble never blocks input. A tap meant for a die or a score
        /// box must pass straight through it.</summary>
        [UnityTest]
        public IEnumerator Bubble_NeverSwallowsTaps()
        {
            yield return LoadGameScene();
            var controller = Object.FindAnyObjectByType<GameController>();
            var bubble = Object.FindAnyObjectByType<SpeechBubbleView>();

            controller.OnRollTapped();
            controller.OnAskOmaTapped();
            yield return null;
            Assert.IsTrue(bubble.IsShowing);

            foreach (var graphic in bubble.GetComponentsInChildren<Graphic>(includeInactive: true))
                Assert.IsFalse(graphic.raycastTarget,
                    $"{graphic.name} in the speech bubble is a raycast target and would eat taps");
        }

        /// <summary>Asking is a query, so it must not disturb the game whatever the player does
        /// with it — the whole feature would be unshippable if hints changed the dice.</summary>
        [UnityTest]
        public IEnumerator AskingRepeatedly_ChangesNothing()
        {
            yield return LoadGameScene();
            var controller = Object.FindAnyObjectByType<GameController>();

            controller.OnRollTapped();
            var before = (int[])controller.Engine.State.Dice.Values.Clone();
            int draws = controller.Engine.State.RngDraws;

            for (int i = 0; i < 10; i++)
                controller.OnAskOmaTapped();
            yield return null;

            CollectionAssert.AreEqual(before, controller.Engine.State.Dice.Values, "dice changed");
            Assert.AreEqual(draws, controller.Engine.State.RngDraws, "advice consumed RNG draws");
            Assert.AreEqual(GamePhase.Deciding, controller.Engine.State.Phase);
        }

        [UnityTest]
        public IEnumerator AskingWhenThereIsNothingToAdviseOn_IsANoOp()
        {
            yield return LoadGameScene();
            var controller = Object.FindAnyObjectByType<GameController>();
            var bubble = Object.FindAnyObjectByType<SpeechBubbleView>();

            // Before the first roll there are no dice on the table. Clear her greeting first.
            bubble.Hide();
            controller.OnAskOmaTapped();
            Assert.IsFalse(bubble.IsShowing, "nothing to advise on, so nothing should be said");
            Assert.AreEqual(GamePhase.TurnStart, controller.Engine.State.Phase);

            // And during Oma's turn it is not the player's decision to make.
            controller.OnRollTapped();
            var best = BestCategory(controller.Engine.GetPotentialScores());
            controller.OnCellTapped(best);
            controller.OnCellTapped(best);
            Assert.IsTrue(controller.IsOmaTurn);
            bubble.Hide(); // she may have reacted to the score just committed
            controller.OnAskOmaTapped();
            Assert.IsFalse(bubble.IsShowing, "no hints while Oma is playing");

            int frames = 0;
            while (controller.IsOmaTurn)
            {
                Assert.Less(++frames, 900);
                yield return null;
            }
        }

        private static Category BestCategory(System.Collections.Generic.IReadOnlyDictionary<Category, int> potentials)
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

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;
        }
    }
}

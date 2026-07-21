using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yahtzee.Core;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Peeking at Oma's scores. The card on the table is yours; hers is private unless
    /// you reach over and tap her card, which is a physical object rather than a HUD button.</summary>
    public class PeekTests
    {
        private const string PlayerCard = "YOUR CARD";
        private const string OmaCard = "OMA'S CARD";

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

        /// <summary>The card used to follow the *current player*, so Oma's scores were on display
        /// for the whole of her turn without anyone asking to see them.</summary>
        [UnityTest]
        public IEnumerator OmasScores_StayHiddenThroughHerEntireTurn()
        {
            yield return LoadGameScene();
            var controller = Object.FindAnyObjectByType<GameController>();
            var scorecard = Object.FindAnyObjectByType<ScorecardView>();
            Assert.AreEqual(PlayerCard, scorecard.Owner);

            controller.OnRollTapped();
            var best = BestCategory(controller.Engine.GetPotentialScores());
            controller.OnCellTapped(best);
            controller.OnCellTapped(best);
            Assert.IsTrue(controller.IsOmaTurn);

            int frames = 0;
            while (controller.IsOmaTurn)
            {
                Assert.AreEqual(PlayerCard, scorecard.Owner,
                    "Oma's card became visible during her turn without the player peeking");
                Assert.Less(++frames, 900);
                yield return null;
            }
            Assert.AreEqual(PlayerCard, scorecard.Owner);
        }

        [UnityTest]
        public IEnumerator TappingOmasCard_TogglesThePeek()
        {
            yield return LoadGameScene();
            var scorecard = Object.FindAnyObjectByType<ScorecardView>();
            var prop = FindOmaCardProp();

            prop.Tap();
            yield return null;
            Assert.AreEqual(OmaCard, scorecard.Owner, "tapping her card should reveal her scores");

            prop.Tap();
            yield return null;
            Assert.AreEqual(PlayerCard, scorecard.Owner, "tapping again should put it back");
        }

        /// <summary>Her card has to be reachable by an actual tap — the invisible fence sits
        /// between the camera and the table, and that is exactly what silently broke the dice.</summary>
        [UnityTest]
        public IEnumerator OmasCard_IsPickableFromTheDefaultFraming()
        {
            yield return LoadGameScene();
            var camera = Camera.main;
            camera.aspect = 1080f / 2340f;
            Object.FindAnyObjectByType<CameraDirector>().Set(CameraDirector.Framing.Default, instant: true);
            yield return null;

            var prop = FindOmaCardProp();
            var input = Object.FindAnyObjectByType<WorldTapInput>();
            Assert.IsNotNull(input, "nothing is routing taps to props");

            var screenPoint = camera.WorldToScreenPoint(prop.transform.position);
            Assert.AreSame(prop, input.PropAtScreenPoint(screenPoint),
                "a tap aimed at Oma's card did not reach it");

            // And it must be on screen at all, or there is nothing to aim at.
            var viewport = camera.WorldToViewportPoint(prop.transform.position);
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f));
        }

        private static TappableProp FindOmaCardProp()
        {
            foreach (var prop in Object.FindObjectsByType<TappableProp>(FindObjectsSortMode.None))
                if (prop.name == "OmaScorecardProp")
                    return prop;
            Assert.Fail("Oma's scorecard prop is not tappable");
            return null;
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

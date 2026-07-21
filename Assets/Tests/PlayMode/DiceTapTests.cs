using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Tapping a die is the only way to keep it (design §5.2), and it is the one input
    /// path the other PlayMode tests miss — they call GameController.OnDieTapped directly and so
    /// never exercise the pick. It was broken from the day 3D dice landed: the invisible fence
    /// stands between the camera and the table, and a first-hit raycast returns the wall.</summary>
    public class DiceTapTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.AnimationsEnabled = false; // dice placed instantly on their slots
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
        public IEnumerator TappingADie_PicksThatDie_FromEveryFraming()
        {
            yield return LoadGameScene();
            var camera = Camera.main;
            var controller = Object.FindAnyObjectByType<GameController>();
            var director = Object.FindAnyObjectByType<CameraDirector>();
            var view = Object.FindAnyObjectByType<DiceView3D>();

            controller.OnRollTapped();
            controller.OnDieTapped(0); // one die in the keep row, four in the roll zone
            yield return null;

            ReportWhatBlocksTheRay(camera, view);

            foreach (var framing in new[]
                     {
                         CameraDirector.Framing.Default,
                         CameraDirector.Framing.DiceFocus,
                         CameraDirector.Framing.ScorecardFocus,
                     })
            {
                director.Set(framing, instant: true);
                yield return null;

                foreach (var die in view.Dice)
                {
                    Assert.IsTrue(die.gameObject.activeSelf, "all five dice should be on the table after a roll");
                    var picked = view.DieAtScreenPoint(camera.WorldToScreenPoint(TopFace(die)));
                    Assert.AreSame(die, picked,
                        $"{framing}: tapping die {die.Index} picked {(picked == null ? "nothing" : picked.name)}");
                }
            }
        }

        /// <summary>Keeping and releasing round-trips through the real pick path.</summary>
        [UnityTest]
        public IEnumerator TappingADie_TogglesItsKeptState()
        {
            yield return LoadGameScene();
            var camera = Camera.main;
            var controller = Object.FindAnyObjectByType<GameController>();
            var view = Object.FindAnyObjectByType<DiceView3D>();

            controller.OnRollTapped();
            yield return null;

            var die = view.Dice[2];
            Assert.IsFalse(controller.Engine.State.Dice.Kept[2]);

            controller.OnDieTapped(view.DieAtScreenPoint(camera.WorldToScreenPoint(TopFace(die))).Index);
            yield return null;
            Assert.IsTrue(controller.Engine.State.Dice.Kept[2], "tapping an unkept die should keep it");

            // The die has moved to the keep row — pick it again where it now is, not where it was.
            controller.OnDieTapped(view.DieAtScreenPoint(camera.WorldToScreenPoint(TopFace(die))).Index);
            yield return null;
            Assert.IsFalse(controller.Engine.State.Dice.Kept[2], "tapping a kept die should release it");
        }

        /// <summary>Names whatever a naive first-hit raycast reports, so the reason this test
        /// exists stays visible in the run output instead of living only in a commit message.</summary>
        private static void ReportWhatBlocksTheRay(Camera camera, DiceView3D view)
        {
            foreach (var die in view.Dice)
            {
                var ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(TopFace(die)));
                string first = Physics.Raycast(ray, out var hit, 8f) ? hit.collider.name : "nothing";
                TestContext.WriteLine($"die {die.Index}: first collider along the tap ray is {first}");
            }
        }

        private static Vector3 TopFace(Die3D die) => die.transform.position + Vector3.up * 0.044f;

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null; // one frame for Awake/Start
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>There must be exactly one EventSystem, always.
    ///
    /// This regressed invisibly for most of the project: the EventSystem was tagged
    /// `HideFlags.DontSave`, which also means "survives scene load", so each reload added another
    /// and the test log filled with "There are N event systems in the scene". Unity disables all
    /// but one, and a disabled EventSystem means no UI input at all — so the failure mode is the
    /// whole interface going dead, in the exact scenario M6 introduces (Title ⇄ Game navigation).
    ///
    /// Only repeated scene loads catch it, which is why nothing did.</summary>
    public class EventSystemTests
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
        public IEnumerator ReloadingTheScene_NeverAccumulatesEventSystems()
        {
            for (int load = 1; load <= 4; load++)
            {
                var loading = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
                while (!loading.isDone)
                    yield return null;
                yield return null;

                var all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Assert.AreEqual(1, all.Length,
                    $"after {load} scene load(s) there are {all.Length} EventSystems — UI input goes dead when Unity disables the extras");

                Assert.IsTrue(all[0].isActiveAndEnabled,
                    "the surviving EventSystem is disabled, so nothing in the UI can be tapped");
            }
        }
    }
}

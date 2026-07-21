using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Guards the diegetic scorecard (design §5.2): it must stay a tappable world-space
    /// object that sits on the table, keeps clear of the dice, and stays legible in the portrait
    /// framings. These are the properties that quietly break when scene or camera numbers are
    /// re-tuned — the renders from FramingCaptureTests are the human half of the check.</summary>
    public class WorldScorecardTests
    {
        /// <summary>Portrait reference from the canvas scaler; asserting in viewport fractions
        /// scaled to this keeps the checks independent of the test runner's window size.</summary>
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 2340f;

        /// <summary>Table surface (y = 0) and the near dice fence (z = -0.38) from KitchenBuilder,
        /// plus the deepened table's near edge.</summary>
        private const float FenceZ = -0.38f;
        private const float TableNearZ = -0.85f;

        /// <summary>Top of the screen-space action bar (UiBuilder). The card is drawn by the
        /// camera and the bar overlays it, so anything below this line is simply not readable.</summary>
        private const float ActionBarTop = 0.105f;

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
            if (Camera.main != null)
                Camera.main.ResetAspect();
            SaveService.Delete();
        }

        [UnityTest]
        public IEnumerator Card_IsWorldSpaceAndTappable()
        {
            yield return LoadGameScene();

            var canvas = FindCardCanvas();
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode, "the card must be a world object, not a screen panel");
            Assert.IsNotNull(canvas.worldCamera, "world-space raycasting needs an event camera");
            Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), "cells would not receive taps");

            // All 13 boxes plus the controller wiring survived the move into world space.
            var cells = canvas.GetComponentsInChildren<ScoreCellView>();
            Assert.AreEqual(13, cells.Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<ScorecardView>(), "controller needs a view to render into");
        }

        [UnityTest]
        public IEnumerator Card_RestsOnTheTableClearOfTheDice()
        {
            yield return LoadGameScene();

            var corners = new Vector3[4];
            ((RectTransform)FindCardCanvas().transform).GetWorldCorners(corners);

            float minY = float.MaxValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var corner in corners)
            {
                minY = Mathf.Min(minY, corner.y);
                minZ = Mathf.Min(minZ, corner.z);
                maxZ = Mathf.Max(maxZ, corner.z);
            }

            Assert.GreaterOrEqual(minY, 0f, "the card must not sink through the table surface");
            Assert.LessOrEqual(maxZ, FenceZ, "the card must stay in front of the fence, out of the dice landing area");
            Assert.GreaterOrEqual(minZ, TableNearZ, "the card must not hang off the near edge of the table");
        }

        /// <summary>The contract a physical card creates: the player may tap a box from any
        /// framing the camera rests in during Deciding — after a roll (Default) and once the
        /// rolls are spent (ScorecardFocus) — so both must show all 13 boxes, clear of the
        /// action bar, at a readable size. DiceFocus is exempt: it is the roll's push-in and
        /// GameController eases off it the moment the dice settle.</summary>
        [UnityTest]
        public IEnumerator Card_IsFullyVisibleAndLegibleInEveryScoringFraming()
        {
            yield return LoadGameScene();
            var camera = Camera.main;
            camera.aspect = ReferenceWidth / ReferenceHeight; // judge against the target device, not the runner window
            var director = Object.FindAnyObjectByType<CameraDirector>();
            var card = (RectTransform)FindCardCanvas().transform;
            var cell = (RectTransform)FindCardCanvas().GetComponentInChildren<ScoreCellView>().transform;
            var corners = new Vector3[4];

            foreach (var framing in new[] { CameraDirector.Framing.Default, CameraDirector.Framing.ScorecardFocus })
            {
                director.Set(framing, instant: true);
                yield return null;

                card.GetWorldCorners(corners);
                ReportSpan(framing, camera, corners);
                foreach (var corner in corners)
                {
                    var viewport = camera.WorldToViewportPoint(corner);
                    Assert.Greater(viewport.z, 0f, $"{framing}: card corner fell behind the camera");
                    Assert.That(viewport.x, Is.InRange(0f, 1f), $"{framing}: card corner off screen horizontally at {viewport}");
                    Assert.That(viewport.y, Is.InRange(ActionBarTop, 1f), $"{framing}: card corner off screen or under the action bar at {viewport}");
                }

                // Design §5.5: 64 px minimum touch target at the 1080-wide reference. A box that
                // clears this is also comfortably readable at arm's length.
                cell.GetWorldCorners(corners);
                float bottom = camera.WorldToViewportPoint(corners[0]).y;
                float top = camera.WorldToViewportPoint(corners[1]).y;
                float cellPixels = Mathf.Abs(top - bottom) * ReferenceHeight;
                Assert.Greater(cellPixels, 64f, $"{framing}: score boxes render {cellPixels:F0} px tall — below the minimum touch target");
            }
        }

        /// <summary>The card is propped toward the player, so it occludes the table behind it —
        /// which silently hid the keep row when the card first landed. Rolled and kept dice alike
        /// must stay visible over its top edge from every framing the player reads dice in.</summary>
        [UnityTest]
        public IEnumerator Dice_AreNeverHiddenBehindTheCard()
        {
            yield return LoadGameScene();
            var camera = Camera.main;
            camera.aspect = ReferenceWidth / ReferenceHeight;
            var controller = Object.FindAnyObjectByType<GameController>();
            var director = Object.FindAnyObjectByType<CameraDirector>();

            // Roll, then keep two dice so both the roll zone and the keep row are populated.
            controller.OnRollTapped();
            controller.OnDieTapped(0);
            controller.OnDieTapped(1);
            yield return null;

            var dice = Object.FindObjectsByType<Die3D>(FindObjectsSortMode.None);
            Assert.AreEqual(5, dice.Length);

            var quad = new Vector2[4];
            var corners = new Vector3[4];
            foreach (var framing in new[]
                     {
                         CameraDirector.Framing.Default,
                         CameraDirector.Framing.ScorecardFocus,
                         CameraDirector.Framing.DiceFocus,
                     })
            {
                director.Set(framing, instant: true);
                yield return null;

                ((RectTransform)FindCardCanvas().transform).GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                    quad[i] = camera.WorldToViewportPoint(corners[i]);

                foreach (var die in dice)
                {
                    // The near-bottom edge is the first thing the card's top edge eats. The
                    // offset also covers the gold keep pad, which is wider than the die.
                    var p = die.transform.position;
                    var nearEdge = new Vector3(p.x, 0f, p.z - KeepPadRadius);
                    var viewport = camera.WorldToViewportPoint(nearEdge);
                    Assert.IsFalse(InsideQuad(viewport, quad),
                        $"{framing}: {die.name} at {p} is hidden behind the scorecard");
                }
            }
        }

        /// <summary>Radius of the gold pad under a kept die (KitchenBuilder.BuildKeepMarker),
        /// the widest thing sitting at a dice slot.</summary>
        private const float KeepPadRadius = 0.085f;

        /// <summary>Point-in-convex-quad. GetWorldCorners returns them in ring order, so a
        /// consistent cross-product sign against every edge means the point is inside. The card
        /// is planar and nearer the camera than the dice, so "inside" is exactly "occluded".</summary>
        private static bool InsideQuad(Vector2 point, Vector2[] quad)
        {
            bool anyPositive = false, anyNegative = false;
            for (int i = 0; i < quad.Length; i++)
            {
                var a = quad[i];
                var b = quad[(i + 1) % quad.Length];
                float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (cross > 0f) anyPositive = true;
                else if (cross < 0f) anyNegative = true;
            }
            return !(anyPositive && anyNegative);
        }

        /// <summary>Prints where the card actually lands, so retuning the framings is a matter of
        /// reading numbers off a test run rather than guessing at camera geometry.</summary>
        private static void ReportSpan(CameraDirector.Framing framing, Camera camera, Vector3[] corners)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var corner in corners)
            {
                var viewport = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, viewport.x);
                maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxY = Mathf.Max(maxY, viewport.y);
            }
            TestContext.WriteLine($"{framing}: card x {minX:F3}..{maxX:F3}, y {minY:F3}..{maxY:F3} " +
                $"({(maxY - minY) * ReferenceHeight:F0} px tall)");
        }

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null; // one frame for Awake/Start
        }

        private static Canvas FindCardCanvas()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.GetComponentInChildren<ScoreCellView>() != null)
                    return canvas;
            Assert.Fail("no canvas holding the scorecard was found in the scene");
            return null;
        }
    }
}

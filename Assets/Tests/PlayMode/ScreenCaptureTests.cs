using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Development aid: renders the WHOLE screen — kitchen plus the screen-space UI — to
    /// PNGs, so HUD and front-end work can be reviewed without a device build.
    ///
    /// `FramingCaptureTests` only renders the camera, which by definition cannot see a
    /// ScreenSpaceOverlay canvas. Every HUD change therefore needed an APK, an install and a
    /// screenshot pulled off the phone. This flips the canvas to ScreenSpaceCamera for the
    /// duration of the render and puts it back, which costs nothing and catches layout mistakes
    /// in seconds instead of minutes.
    ///
    /// Asserts only that rendering succeeded — judging the layout is human work.</summary>
    public class ScreenCaptureTests
    {
        private static string OutDir => Path.Combine(Application.persistentDataPath, "screens");

        [SetUp]
        public void SetUp()
        {
            GameController.AnimationsEnabled = false;
            GameController.Use3dDice = true;
            SaveService.Delete();
            GameSettings.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            GameController.AnimationsEnabled = true;
            SaveService.Delete();
            GameSettings.ResetAll();
        }

        [UnityTest]
        public IEnumerator CaptureEveryScreen()
        {
            Directory.CreateDirectory(OutDir);
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameController>();
            var screens = Object.FindAnyObjectByType<ScreensView>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(screens);

            foreach (var screen in new[]
                     {
                         ScreensView.Screen.Title,
                         ScreensView.Screen.Home,
                         ScreensView.Screen.Settings,
                         ScreensView.Screen.Store,
                     })
            {
                screens.Show(screen);
                yield return null;
                Capture($"screen-{screen}".ToLowerInvariant());
            }

            // And the game itself, mid-turn, with the HUD over it.
            screens.Close();
            controller.OnRollTapped();
            controller.OnDieTapped(0);
            yield return null;
            Capture("screen-ingame");

            // Game over is the one panel no other capture reaches, and it is styled from the
            // same palette as everything else — so it is exactly where a rename goes unnoticed.
            var hud = Object.FindAnyObjectByType<HudView>();
            hud.ShowGameOver(new Yahtzee.Core.GameEnded
            {
                PlayerTotal = 214,
                OmaTotal = 198,
                Result = Yahtzee.Core.GameResult.PlayerWins,
            });
            yield return null;
            Capture("screen-gameover");

            Assert.IsTrue(File.Exists(Path.Combine(OutDir, "screen-ingame.png")));
        }

        private static void Capture(string name)
        {
            var camera = Camera.main;
            var canvas = FindOverlayCanvas();

            // ScreenSpaceOverlay is drawn straight to the backbuffer and never appears in a
            // RenderTexture. Borrow the canvas onto the camera for one frame instead.
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(540, 1170, 24);
            var previousTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previousTarget;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;

            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            Object.Destroy(rt);

            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
        }

        private static Canvas FindOverlayCanvas()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.GetComponent<GraphicRaycaster>() != null && canvas.GetComponent<CanvasScaler>() != null)
                    return canvas;
            Assert.Fail("no screen-space UI canvas found");
            return null;
        }
    }
}

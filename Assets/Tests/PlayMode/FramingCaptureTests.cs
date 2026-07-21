using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yahtzee.Core;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Development aid: renders each camera framing of the live 3D scene to PNGs
    /// (persistentDataPath/framings) so composition can be reviewed headless against the
    /// concept mockup. Asserts only that rendering succeeds — layout review is human work.</summary>
    public class FramingCaptureTests
    {
        private static string OutDir => Path.Combine(Application.persistentDataPath, "framings");

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
        public IEnumerator CaptureAllFramings()
        {
            Directory.CreateDirectory(OutDir);
            var load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<GameController>();
            var director = Object.FindAnyObjectByType<CameraDirector>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(director);

            // Turn start: empty table.
            Capture("0-turnstart-default");

            // After roll 1 the camera eases back off the roll's push-in, because the player may
            // score from here and the diegetic card has to be readable in full.
            controller.OnRollTapped();
            yield return null;
            Capture("1-roll1-settled");

            // Burn the remaining rolls → ScorecardFocus + best-option hints active.
            controller.OnRollTapped();
            controller.OnRollTapped();
            yield return null;
            Capture("2-roll3-scorecardfocus");

            // Explicit framings for review regardless of game flow.
            director.Set(CameraDirector.Framing.Default, instant: true);
            yield return null;
            Capture("3-default");
            director.Set(CameraDirector.Framing.DiceFocus, instant: true);
            yield return null;
            Capture("4-dicefocus");
            director.Set(CameraDirector.Framing.OmaFocus, instant: true);
            yield return null;
            Capture("5-omafocus");

            Assert.IsTrue(File.Exists(Path.Combine(OutDir, "3-default.png")));
        }

        private static void Capture(string name)
        {
            var camera = Camera.main;
            var rt = new RenderTexture(540, 1170, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previous;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;

            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}

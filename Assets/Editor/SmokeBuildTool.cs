using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yahtzee.EditorTools
{
    /// <summary>Packages the game so build-only failures surface before a manual device test.
    ///
    /// Why this exists: EditMode and PlayMode both run inside the editor, where nothing is
    /// stripped and every shader and component type is available. A whole class of bug is
    /// therefore invisible to the suite — the one that cost a device round trip was
    /// `CreatePrimitive(Cylinder)` returning no collider on Android, because IL2CPP strips
    /// collider types the game never names. `KitchenBuilder.Build` threw in `Awake` and the
    /// entire 3D scene was missing, while the editor was perfectly happy.
    ///
    /// **The desktop build is not a substitute for the Android one.** Stripping is
    /// per-platform: a Windows player kept shaders and components that Android dropped, and it
    /// booted clean on the exact code that crashed on device. Use Desktop as a fast smoke test
    /// for logic that throws everywhere; use Android before believing anything about the phone.
    ///
    /// Batch: -executeMethod Yahtzee.EditorTools.SmokeBuildTool.BuildAndroidBatch
    /// Driven by Tools\device-smoke.ps1 (build, install, launch, scan logcat).</summary>
    public static class SmokeBuildTool
    {
        private const string OutputDir = "Build/Smoke";

        [MenuItem("Yahtzee/Smoke Build (Android APK)")]
        public static void BuildAndroid() => RunBuild(BuildTarget.Android);

        [MenuItem("Yahtzee/Smoke Build (desktop player)")]
        public static void BuildDesktop() => RunBuild(BuildTarget.StandaloneWindows64);

        public static void BuildAndroidBatch() => EditorApplication.Exit(RunBuild(BuildTarget.Android) ? 0 : 1);

        public static void BuildDesktopBatch() =>
            EditorApplication.Exit(RunBuild(BuildTarget.StandaloneWindows64) ? 0 : 1);

        private static bool RunBuild(BuildTarget target)
        {
            Directory.CreateDirectory(OutputDir);
            bool android = target == BuildTarget.Android;
            string output = Path.Combine(OutputDir, android ? "yahtzee-smoke.apk" : "YahtzeeSmoke.exe");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = output,
                target = target,
                targetGroup = android ? BuildTargetGroup.Android : BuildTargetGroup.Standalone,
                // Development keeps managed stack traces readable in logcat. It does NOT disable
                // stripping, so the failure mode above still reproduces.
                options = BuildOptions.Development,
            });

            var summary = report.summary;
            Debug.Log($"SMOKEBUILD target={target} result={summary.result} errors={summary.totalErrors} output={summary.outputPath}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}

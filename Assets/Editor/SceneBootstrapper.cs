using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yahtzee.Presentation;

namespace Yahtzee.EditorTools
{
    /// <summary>One-shot project setup for the M2 prototype. Idempotent — safe to re-run.
    /// Batch: Unity.exe -batchmode -executeMethod Yahtzee.EditorTools.SceneBootstrapper.SetupProject -quit</summary>
    public static class SceneBootstrapper
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Yahtzee/Setup Project (scene, build settings, TMP)")]
        public static void SetupProject()
        {
            ImportTmpEssentialsIfMissing();
            FontTool.Build();
            BuildGameScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Yahtzee project setup complete.");
        }

        private static void ImportTmpEssentialsIfMissing()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                return;
            AssetDatabase.ImportPackage(
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
                interactive: false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "GameRoot")
                    Object.DestroyImmediate(root);

            var gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<GameController>();

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiPalette.Backdrop;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // CLAUDE.md: IL2CPP/ARM64. Unity's Android defaults are Mono + ARMv7, which builds an
            // APK no modern phone will install ("trying to install ARMv7 APK to ARM64 device") and
            // that Google Play rejects outright. ARM64 is only offered under IL2CPP, so the
            // backend has to be set first or the architecture assignment is clamped back.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }
    }
}

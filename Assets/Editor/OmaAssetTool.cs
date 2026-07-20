using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Yahtzee.EditorTools
{
    /// <summary>One-shot import setup for the Oma character FBXs (Mixamo sitting set) in
    /// Assets/Resources/Oma: humanoid rig, looping idles, locked root, and a generated
    /// AnimatorController whose state names match OmaView's CrossFade targets.
    /// Idempotent. Batch: -executeMethod Yahtzee.EditorTools.OmaAssetTool.Setup</summary>
    public static class OmaAssetTool
    {
        private const string Dir = "Assets/Resources/Oma";
        private const string ControllerPath = Dir + "/OmaAnimator.controller";

        private static readonly string[] LoopingClips = { "Sitting Idle", "Sitting", "Sitting Talking" };

        [MenuItem("Yahtzee/Setup Oma Assets")]
        public static void Setup()
        {
            var fbxPaths = Directory.GetFiles(Dir, "*.fbx").Select(p => p.Replace('\\', '/')).ToArray();
            if (fbxPaths.Length == 0)
            {
                Debug.LogError($"No FBX files found in {Dir}");
                return;
            }

            foreach (var path in fbxPaths)
                ConfigureImport(path);
            AssetDatabase.Refresh();

            BuildController(fbxPaths);
            AssetDatabase.SaveAssets();
            Debug.Log($"Oma assets configured: {fbxPaths.Length} FBXs, controller at {ControllerPath}");
        }

        private static void ConfigureImport(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            string clipName = Path.GetFileNameWithoutExtension(path);
            bool loop = LoopingClips.Contains(clipName);

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;

            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = loop;
                // She stays planted in her chair: kill root drift from the clips.
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;

            try
            {
                importer.ExtractTextures(Dir); // no-op if nothing embedded
            }
            catch
            {
                // Some Mixamo exports have no extractable textures — gray is fine for now.
            }
            importer.SaveAndReimport();
        }

        private static void BuildController(string[] fbxPaths)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;

            foreach (var path in fbxPaths)
            {
                string clipName = Path.GetFileNameWithoutExtension(path);
                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null)
                {
                    Debug.LogWarning($"No animation clip found in {path}");
                    continue;
                }
                var state = stateMachine.AddState(clipName);
                state.motion = clip;
                if (clipName == "Sitting Idle")
                    stateMachine.defaultState = state;
            }
        }
    }
}

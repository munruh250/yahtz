using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Yahtzee.EditorTools
{
    /// <summary>Turns a dropped-in .ttf/.otf into the TMP font asset the game uses.
    ///
    /// Drop the file in `Assets/Fonts/` and run this (or `Yahtzee/Setup Project`, which calls it).
    /// It builds a dynamic SDF font asset into `Assets/Resources/Fonts/` where `UiBuilder` finds
    /// it at runtime. Dynamic atlas population means glyphs are rasterised on demand, so no
    /// character set has to be chosen up front.
    ///
    /// The generated asset falls back to TMP's default font. A handwriting face will not have
    /// every glyph the game needs — German umlauts in the wall samplers, the hamburger bar — and
    /// without a fallback those render as tofu boxes. (That is not hypothetical: a missing "✓"
    /// shipped as a tofu box in the roll counter.)</summary>
    public static class FontTool
    {
        private const string SourceFolder = "Assets/Fonts";
        private const string OutputFolder = "Assets/Resources/Fonts";

        /// <summary>Name UiBuilder loads from Resources. Kept stable so the game picks up whatever
        /// face is currently installed without a code change.</summary>
        public const string AssetName = "GameFont";

        [MenuItem("Yahtzee/Build Font Asset")]
        public static void Build()
        {
            if (!Directory.Exists(SourceFolder))
            {
                Directory.CreateDirectory(SourceFolder);
                AssetDatabase.Refresh();
            }

            string source = Directory.GetFiles(SourceFolder)
                .FirstOrDefault(path => path.EndsWith(".ttf") || path.EndsWith(".otf"));
            if (source == null)
            {
                Debug.LogWarning($"FontTool: no .ttf/.otf in {SourceFolder} — the game keeps TMP's default font.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(source);
            if (font == null)
            {
                Debug.LogError($"FontTool: could not load {source} as a Font.");
                return;
            }

            // 90pt sampling with an 8px atlas pad: enough weight for the big title without the
            // small scorecard hints turning to mush.
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = AssetName;

            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
                asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { defaultFont };

            Directory.CreateDirectory(OutputFolder);
            string output = $"{OutputFolder}/{AssetName}.asset";
            AssetDatabase.DeleteAsset(output);
            AssetDatabase.CreateAsset(asset, output);
            // The atlas texture and material are sub-assets, or the .asset alone renders nothing.
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"FontTool: built {output} from {Path.GetFileName(source)}.");
        }
    }
}

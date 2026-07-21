using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>The game's colours. Cozy and cartoony rather than heavy: soft periwinkle blues and
    /// lilacs against warm cream paper, with ink a deep indigo instead of black. Nothing here is
    /// fully saturated and nothing is pure white or black — that softness is what reads as
    /// storybook rather than spreadsheet.
    ///
    /// Named by role, not by hue, so a re-tint never leaves a field called `Gold` holding a blue.</summary>
    public static class UiPalette
    {
        // ---- Surfaces --------------------------------------------------------

        /// <summary>Behind the front-end screens: deep, soft indigo — night outside the kitchen.</summary>
        public static readonly Color Backdrop = Hex("2A2742");

        /// <summary>Chrome: top bar, menu rows, inactive chips.</summary>
        public static readonly Color Chrome = Hex("4E4A7C");
        public static readonly Color ChromeLight = Hex("6E69A4");

        /// <summary>Warm cream paper — the scorecard, speech bubbles, tiles.</summary>
        public static readonly Color Paper = Hex("FCF5E6");
        /// <summary>A filled or unavailable box: paper cooled toward lilac.</summary>
        public static readonly Color PaperShade = Hex("E6DEF3");
        public static readonly Color PaperRule = Hex("BAB2D6");
        /// <summary>Section bands on the scorecard.</summary>
        public static readonly Color PaperBand = Hex("D7CFEC");

        // ---- Type ------------------------------------------------------------

        /// <summary>Light type on chrome.</summary>
        public static readonly Color Cream = Hex("FBF5E9");
        public static readonly Color CreamDim = Hex("CFC7E6");
        /// <summary>Dark type on paper. Deep indigo, never black.</summary>
        public static readonly Color Ink = Hex("3C3768");
        public static readonly Color InkDark = Hex("2B2751");
        /// <summary>Ghosted potential scores.</summary>
        public static readonly Color InkGhost = Hex("A69ECA");

        // ---- Accent ----------------------------------------------------------

        /// <summary>Primary action and current selection: soft periwinkle.</summary>
        public static readonly Color Accent = Hex("93ABEA");
        /// <summary>Suggestions and secondary highlights.</summary>
        public static readonly Color AccentSoft = Hex("C6D4F7");
        /// <summary>Deeper periwinkle: unfilled roll boxes, pressed states.</summary>
        public static readonly Color AccentDeep = Hex("6E80C6");

        // ---- Dice ------------------------------------------------------------

        public static readonly Color DieFace = Hex("FCF7EE");
        public static readonly Color DiePip = Hex("4A4478");
        /// <summary>The pad a kept die sits on.</summary>
        public static readonly Color KeptOutline = Hex("93ABEA");

        private static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }
}

using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>Prototype palette — warm, cozy, kitchen-table (design spec §1). The 2D layer
    /// is scaffolding for M2/M3, so colors live in code, not assets.</summary>
    public static class UiPalette
    {
        public static readonly Color Background = Hex("2B1E16");
        public static readonly Color Panel = Hex("3A2A1E");
        public static readonly Color Cream = Hex("F3E7C9");
        public static readonly Color CreamDim = Hex("C9BFA8");
        public static readonly Color Ink = Hex("3B2C1A");
        public static readonly Color InkGhost = Hex("9C8F73");
        public static readonly Color Gold = Hex("E4B54A");
        public static readonly Color GoldSoft = Hex("EDD9A8");
        public static readonly Color GoldDark = Hex("B8892E");
        public static readonly Color DieFace = Hex("FAF6EA");
        public static readonly Color DiePip = Hex("33261A");
        public static readonly Color KeptOutline = Hex("E4B54A");

        // Top bar / menu chrome.
        public static readonly Color BarDeep = Hex("2A1D14");
        public static readonly Color BarLight = Hex("4A3728");

        // Paper scorecard (design reference: the real Yahtzee pad — white stock, black ink,
        // grey section bands).
        public static readonly Color Paper = Hex("F7F5EE");
        public static readonly Color PaperShade = Hex("DAD6CA");
        public static readonly Color PaperRule = Hex("9A948A");
        public static readonly Color PaperBand = Hex("BFBAAE");
        public static readonly Color InkDark = Hex("1E1A15");

        private static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }
}

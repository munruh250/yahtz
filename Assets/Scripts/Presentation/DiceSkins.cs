using UnityEngine;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>Look of the physical dice. Purely cosmetic — the engine decides values and the
    /// physics is theatre, so a skin can never touch gameplay.</summary>
    public static class DiceSkins
    {
        public readonly struct Skin
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly Color Face;
            public readonly Color Pip;

            public Skin(string id, string displayName, Color face, Color pip)
            {
                Id = id;
                DisplayName = displayName;
                Face = face;
                Pip = pip;
            }
        }

        public static readonly Skin[] All =
        {
            new Skin(GameSettings.ClassicDice, "Classic", UiPalette.DieFace, UiPalette.DiePip),
            new Skin(GameSettings.RubyDice, "Ruby", new Color(0.82f, 0.40f, 0.44f), new Color(0.99f, 0.97f, 0.96f)),
        };

        public static Skin Selected => Find(GameSettings.SelectedDice);

        public static Skin Find(string id)
        {
            foreach (var skin in All)
                if (skin.Id == id)
                    return skin;
            return All[0];
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>Builds the front-end screens. Placeholder shells for M6's real Title/Results/Pause
    /// work, but the settings and store inside them do function: difficulty and dice skins persist
    /// through <see cref="GameSettings"/> and take effect immediately.</summary>
    public static class ScreensBuilder
    {
        public static ScreensView Build(RectTransform parent, GameController controller)
        {
            var root = UiBuilder.Rect(parent, "Screens", Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<ScreensView>();

            var refreshers = new List<System.Action>();
            var panels = new Dictionary<ScreensView.Screen, GameObject>
            {
                { ScreensView.Screen.Title, BuildTitle(root, view) },
                { ScreensView.Screen.Home, BuildHome(root, view, controller, refreshers) },
                { ScreensView.Screen.Settings, BuildSettings(root, view, controller, refreshers) },
                { ScreensView.Screen.Store, BuildStore(root, view, controller, refreshers) },
            };

            view.Init(panels, () =>
            {
                foreach (var refresh in refreshers)
                    refresh();
            });
            return view;
        }

        private static GameObject BuildTitle(RectTransform parent, ScreensView view)
        {
            var panel = Backdrop(parent, "TitleScreen");

            var banner = UiBuilder.Image(panel, "Banner", new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.74f),
                UiPalette.Accent, cornerRadius: 34);
            var title = UiBuilder.Text(banner.rectTransform, "Title", "Dice with Oma", 84f, UiPalette.Cream,
                TextAlignmentOptions.Center, new Vector2(0f, 0.06f), new Vector2(1f, 1f));
            title.fontStyle = FontStyles.Bold;
            UiBuilder.Text(banner.rectTransform, "Subtitle", "an evening at Oma's kitchen table", 28f, UiPalette.Ink,
                TextAlignmentOptions.Center, new Vector2(0.04f, 0f), new Vector2(0.96f, 0.24f));

            var prompt = UiBuilder.Image(panel, "Prompt", new Vector2(0.28f, 0.34f), new Vector2(0.72f, 0.43f),
                UiPalette.Paper, cornerRadius: 26);
            UiBuilder.Text(prompt.rectTransform, "Label", "tap to begin", 36f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;

            // The whole screen is the button - nothing to aim at.
            var hit = panel.gameObject.AddComponent<Button>();
            hit.targetGraphic = panel.GetComponent<Image>();
            hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => view.Show(ScreensView.Screen.Home));
            return panel.gameObject;
        }

        private static GameObject BuildHome(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var backdrop = Backdrop(parent, "HomeScreen");
            var card = Card(backdrop, "DICE WITH OMA");

            var resume = MenuButton(card, "Resume", 0, UiPalette.AccentSoft, () => view.Close());
            MenuButton(card, "New Game", 1, UiPalette.Accent, () =>
            {
                view.Close();
                controller.OnNewGameTapped();
            });
            MenuButton(card, "Settings", 2, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Settings));
            MenuButton(card, "Store", 3, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Store));

            // Nothing to resume before the first game starts.
            refreshers.Add(() => resume.gameObject.SetActive(controller.Engine != null));
            return backdrop.gameObject;
        }

        private static GameObject BuildSettings(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var backdrop = Backdrop(parent, "SettingsScreen");
            var card = Card(backdrop, "SETTINGS");

            SectionLabel(card, "How sharp is Oma?", 0.90f);
            var difficulties = new[] { GameSettings.Difficulty.Gentle, GameSettings.Difficulty.Normal, GameSettings.Difficulty.Sharp };
            var difficultyButtons = new Image[difficulties.Length];
            for (int i = 0; i < difficulties.Length; i++)
            {
                var choice = difficulties[i];
                difficultyButtons[i] = Chip(card, choice.ToString(), i, difficulties.Length, 0.80f,
                    () => GameSettings.SelectedDifficulty = choice);
            }

            SectionLabel(card, "Your dice", 0.62f);
            var diceButtons = new Image[DiceSkins.All.Length];
            for (int i = 0; i < DiceSkins.All.Length; i++)
            {
                var skin = DiceSkins.All[i];
                diceButtons[i] = Chip(card, skin.DisplayName, i, DiceSkins.All.Length, 0.52f,
                    () => { if (GameSettings.Owns(skin.Id)) GameSettings.SelectedDice = skin.Id; });
            }

            MenuButton(card, "Back", 3, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Home));

            refreshers.Add(() =>
            {
                for (int i = 0; i < difficulties.Length; i++)
                    difficultyButtons[i].color = difficulties[i] == GameSettings.SelectedDifficulty
                        ? UiPalette.Accent : UiPalette.PaperShade;
                for (int i = 0; i < DiceSkins.All.Length; i++)
                {
                    var skin = DiceSkins.All[i];
                    diceButtons[i].color = !GameSettings.Owns(skin.Id) ? UiPalette.PaperRule
                        : skin.Id == GameSettings.SelectedDice ? UiPalette.Accent : UiPalette.PaperShade;
                }
            });
            return backdrop.gameObject;
        }

        private static GameObject BuildStore(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var backdrop = Backdrop(parent, "StoreScreen");
            var card = Card(backdrop, "STORE");
            SectionLabel(card, "Dice", 0.90f);

            var rows = new List<(DiceSkins.Skin skin, Image button, TextMeshProUGUI buttonLabel)>();
            for (int i = 0; i < DiceSkins.All.Length; i++)
            {
                var skin = DiceSkins.All[i];
                float top = 0.80f - i * 0.155f;
                var row = UiBuilder.Image(card, "Row" + i, new Vector2(0.05f, top - 0.135f), new Vector2(0.95f, top),
                    UiPalette.PaperShade, cornerRadius: 26);

                // A tile in the actual die colours, so the shelf shows what you are getting.
                var tile = UiBuilder.Image(row.rectTransform, "Swatch", new Vector2(0.04f, 0.14f),
                    new Vector2(0.24f, 0.86f), skin.Face, cornerRadius: 18);
                UiBuilder.Image(tile.rectTransform, "Pip", new Vector2(0.34f, 0.34f), new Vector2(0.66f, 0.66f),
                    skin.Pip, cornerRadius: 12);

                UiBuilder.Text(row.rectTransform, "Name", skin.DisplayName, 36f, UiPalette.Ink,
                    TextAlignmentOptions.MidlineLeft, new Vector2(0.30f, 0f), new Vector2(0.62f, 1f))
                    .fontStyle = FontStyles.Bold;

                var buyBg = UiBuilder.Image(row.rectTransform, "Action", new Vector2(0.64f, 0.20f),
                    new Vector2(0.95f, 0.80f), UiPalette.Accent, cornerRadius: 20);
                var buy = buyBg.gameObject.AddComponent<Button>();
                buy.targetGraphic = buyBg;
                var buyLabel = UiBuilder.Text(buyBg.rectTransform, "Label", "Get", 32f, UiPalette.Cream,
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
                buyLabel.fontStyle = FontStyles.Bold;

                var captured = skin;
                buy.onClick.AddListener(() =>
                {
                    if (!GameSettings.Owns(captured.Id))
                        GameSettings.Grant(captured.Id);
                    GameSettings.SelectedDice = captured.Id;
                    view.Show(ScreensView.Screen.Store); // refresh the shelf in place
                });
                rows.Add((skin, buyBg, buyLabel));
            }

            MenuButton(card, "Back", 3, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Home));

            refreshers.Add(() =>
            {
                foreach (var row in rows)
                {
                    bool owned = GameSettings.Owns(row.skin.Id);
                    bool selected = row.skin.Id == GameSettings.SelectedDice;
                    row.buttonLabel.text = selected ? "In play" : owned ? "Use" : "Get";
                    row.buttonLabel.color = selected ? UiPalette.Ink : UiPalette.Cream;
                    row.button.color = selected ? UiPalette.AccentSoft : UiPalette.Accent;
                }
            });
            return backdrop.gameObject;
        }

        // ---- Primitives ------------------------------------------------------

        /// <summary>Full-screen and opaque: while a screen is open nothing behind it is tappable.</summary>
        private static RectTransform Backdrop(RectTransform parent, string name) =>
            UiBuilder.Fill(parent, name, Vector2.zero, Vector2.one, UiPalette.Backdrop).rectTransform;

        /// <summary>A cream card holding the screen's contents, with a banner sitting across its
        /// top edge — the layout the owner's reference uses, and it stops every screen reading as
        /// a list of buttons floating in the dark.</summary>
        private static RectTransform Card(RectTransform parent, string title, float bottom = 0.16f)
        {
            var card = UiBuilder.Image(parent, "Card", new Vector2(0.06f, bottom), new Vector2(0.94f, 0.86f),
                UiPalette.Paper, cornerRadius: 34);

            var banner = UiBuilder.Image(parent, "Banner", new Vector2(0.12f, 0.845f), new Vector2(0.88f, 0.925f),
                UiPalette.Accent, cornerRadius: 26);
            var label = UiBuilder.Text(banner.rectTransform, "Title", title, 52f, UiPalette.Cream,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            label.fontStyle = FontStyles.Bold;
            return card.rectTransform;
        }

        /// <summary>A section strip inside a card, like the reference's "1. BASIC" ribbon.</summary>
        private static void SectionLabel(RectTransform card, string text, float top)
        {
            var strip = UiBuilder.Image(card, text, new Vector2(0.05f, top - 0.052f), new Vector2(0.62f, top),
                UiPalette.PaperBand, cornerRadius: 16);
            UiBuilder.Text(strip.rectTransform, "Label", text, 30f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
        }

        private static Image MenuButton(RectTransform parent, string label, int index, Color colour,
            UnityEngine.Events.UnityAction onClick)
        {
            float top = 0.70f - index * 0.145f;
            var bg = UiBuilder.Image(parent, label, new Vector2(0.10f, top - 0.115f), new Vector2(0.90f, top),
                colour, cornerRadius: 26);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            var text = UiBuilder.Text(bg.rectTransform, "Label", label, 40f,
                colour == UiPalette.ChromeLight ? UiPalette.Cream : UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            text.fontStyle = FontStyles.Bold;
            return bg;
        }

        private static Image Chip(RectTransform parent, string label, int index, int count, float top,
            UnityEngine.Events.UnityAction onClick)
        {
            float width = 0.90f / count;
            float x0 = 0.05f + index * width;
            var bg = UiBuilder.Image(parent, label, new Vector2(x0 + 0.012f, top - 0.085f),
                new Vector2(x0 + width - 0.012f, top), UiPalette.PaperShade, cornerRadius: 22);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            UiBuilder.Text(bg.rectTransform, "Label", label, 32f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
            return bg;
        }
    }
}

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

            var title = UiBuilder.Text(panel, "Title", "Dice with Oma", 96f, UiPalette.Gold,
                TextAlignmentOptions.Center, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.76f));
            title.fontStyle = FontStyles.Bold;
            UiBuilder.Text(panel, "Subtitle", "an evening at Oma's kitchen table", 34f, UiPalette.CreamDim,
                TextAlignmentOptions.Center, new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.60f));
            UiBuilder.Text(panel, "Prompt", "tap to begin", 38f, UiPalette.Cream,
                TextAlignmentOptions.Center, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.38f));

            // The whole screen is the button — nothing to aim at.
            var hit = panel.gameObject.AddComponent<Button>();
            hit.targetGraphic = panel.GetComponent<Image>();
            hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => view.Show(ScreensView.Screen.Home));
            return panel.gameObject;
        }

        private static GameObject BuildHome(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var panel = Backdrop(parent, "HomeScreen");
            Heading(panel, "Dice with Oma");

            var resume = MenuButton(panel, "Resume", 0, UiPalette.GoldSoft, () => view.Close());
            MenuButton(panel, "New Game", 1, UiPalette.Gold, () =>
            {
                view.Close();
                controller.OnNewGameTapped();
            });
            MenuButton(panel, "Settings", 2, UiPalette.BarLight, () => view.Show(ScreensView.Screen.Settings));
            MenuButton(panel, "Store", 3, UiPalette.BarLight, () => view.Show(ScreensView.Screen.Store));

            // Nothing to resume before the first game starts.
            refreshers.Add(() => resume.gameObject.SetActive(controller.Engine != null));
            return panel.gameObject;
        }

        private static GameObject BuildSettings(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var panel = Backdrop(parent, "SettingsScreen");
            Heading(panel, "Settings");

            UiBuilder.Text(panel, "DifficultyLabel", "How sharp is Oma?", 34f, UiPalette.CreamDim,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.75f));
            var difficulties = new[] { GameSettings.Difficulty.Gentle, GameSettings.Difficulty.Normal, GameSettings.Difficulty.Sharp };
            var difficultyButtons = new Image[difficulties.Length];
            for (int i = 0; i < difficulties.Length; i++)
            {
                var choice = difficulties[i];
                difficultyButtons[i] = Chip(panel, choice.ToString(), i, difficulties.Length, 0.62f,
                    () => GameSettings.SelectedDifficulty = choice);
            }

            UiBuilder.Text(panel, "DiceLabel", "Your dice", 34f, UiPalette.CreamDim,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.55f));
            var diceButtons = new Image[DiceSkins.All.Length];
            for (int i = 0; i < DiceSkins.All.Length; i++)
            {
                var skin = DiceSkins.All[i];
                diceButtons[i] = Chip(panel, skin.DisplayName, i, DiceSkins.All.Length, 0.42f,
                    () => { if (GameSettings.Owns(skin.Id)) GameSettings.SelectedDice = skin.Id; });
            }

            MenuButton(panel, "Back", 3, UiPalette.BarLight, () => view.Show(ScreensView.Screen.Home));

            refreshers.Add(() =>
            {
                for (int i = 0; i < difficulties.Length; i++)
                    difficultyButtons[i].color = difficulties[i] == GameSettings.SelectedDifficulty
                        ? UiPalette.Gold : UiPalette.BarLight;
                for (int i = 0; i < DiceSkins.All.Length; i++)
                {
                    var skin = DiceSkins.All[i];
                    diceButtons[i].color = !GameSettings.Owns(skin.Id) ? UiPalette.Panel
                        : skin.Id == GameSettings.SelectedDice ? UiPalette.Gold : UiPalette.BarLight;
                }
            });
            return panel.gameObject;
        }

        private static GameObject BuildStore(RectTransform parent, ScreensView view, GameController controller,
            List<System.Action> refreshers)
        {
            var panel = Backdrop(parent, "StoreScreen");
            Heading(panel, "Store");
            UiBuilder.Text(panel, "Note", "Free while the shelves are still being stocked.", 30f, UiPalette.CreamDim,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.75f));

            var rows = new List<(DiceSkins.Skin skin, Image swatch, TextMeshProUGUI label, Image button, TextMeshProUGUI buttonLabel)>();
            for (int i = 0; i < DiceSkins.All.Length; i++)
            {
                var skin = DiceSkins.All[i];
                float top = 0.66f - i * 0.13f;
                var row = UiBuilder.Image(panel, $"Row{i}", new Vector2(0.08f, top - 0.11f), new Vector2(0.92f, top), UiPalette.BarLight);

                // A swatch in the actual die colours, so the shelf shows what you are getting.
                var swatch = UiBuilder.Image(row.rectTransform, "Swatch", new Vector2(0.03f, 0.15f), new Vector2(0.18f, 0.85f), skin.Face);
                UiBuilder.Image(swatch.rectTransform, "Pip", new Vector2(0.32f, 0.32f), new Vector2(0.68f, 0.68f), skin.Pip);

                var label = UiBuilder.Text(row.rectTransform, "Name", skin.DisplayName, 34f, UiPalette.Cream,
                    TextAlignmentOptions.MidlineLeft, new Vector2(0.23f, 0f), new Vector2(0.62f, 1f));

                var buyBg = UiBuilder.Image(row.rectTransform, "Action", new Vector2(0.64f, 0.18f), new Vector2(0.96f, 0.82f), UiPalette.Gold);
                var buy = buyBg.gameObject.AddComponent<Button>();
                buy.targetGraphic = buyBg;
                var buyLabel = UiBuilder.Text(buyBg.rectTransform, "Label", "Get", 30f, UiPalette.Ink,
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
                rows.Add((skin, swatch, label, buyBg, buyLabel));
            }

            MenuButton(panel, "Back", 3, UiPalette.BarLight, () => view.Show(ScreensView.Screen.Home));

            refreshers.Add(() =>
            {
                foreach (var row in rows)
                {
                    bool owned = GameSettings.Owns(row.skin.Id);
                    bool selected = row.skin.Id == GameSettings.SelectedDice;
                    row.buttonLabel.text = selected ? "In play" : owned ? "Use" : "Get";
                    row.button.color = selected ? UiPalette.GoldSoft : UiPalette.Gold;
                }
            });
            return panel.gameObject;
        }

        // ---- Primitives ------------------------------------------------------

        /// <summary>Full-screen and opaque: while a screen is open nothing behind it is tappable.</summary>
        private static RectTransform Backdrop(RectTransform parent, string name)
        {
            var image = UiBuilder.Image(parent, name, Vector2.zero, Vector2.one, UiPalette.Background);
            return image.rectTransform;
        }

        private static void Heading(RectTransform parent, string text)
        {
            var heading = UiBuilder.Text(parent, "Heading", text, 64f, UiPalette.Gold,
                TextAlignmentOptions.Center, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.90f));
            heading.fontStyle = FontStyles.Bold;
        }

        private static Image MenuButton(RectTransform parent, string label, int index, Color colour,
            UnityEngine.Events.UnityAction onClick)
        {
            float top = 0.34f - index * 0.075f;
            var bg = UiBuilder.Image(parent, label, new Vector2(0.18f, top - 0.06f), new Vector2(0.82f, top), colour);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            var text = UiBuilder.Text(bg.rectTransform, "Label", label, 38f,
                colour == UiPalette.BarLight ? UiPalette.Cream : UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            text.fontStyle = FontStyles.Bold;
            return bg;
        }

        private static Image Chip(RectTransform parent, string label, int index, int count, float top,
            UnityEngine.Events.UnityAction onClick)
        {
            float width = 0.84f / count;
            float x0 = 0.08f + index * width;
            var bg = UiBuilder.Image(parent, label, new Vector2(x0 + 0.01f, top - 0.06f),
                new Vector2(x0 + width - 0.01f, top), UiPalette.BarLight);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            UiBuilder.Text(bg.rectTransform, "Label", label, 30f, UiPalette.Cream,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
            return bg;
        }
    }
}

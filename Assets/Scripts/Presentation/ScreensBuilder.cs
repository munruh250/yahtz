using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yahtzee.Core;
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
                { ScreensView.Screen.HowToPlay, BuildHowToPlay(root, view) },
                { ScreensView.Screen.Results, BuildResults(root, view, controller, out var results) },
            };

            view.Init(panels, results, () =>
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

            // Home doubles as the pause overlay when opened mid-game from the hamburger, so it
            // carries the full design §5.1 set: resume, restart, rules, settings, store, and a
            // corner "Title" to quit out.
            var resume = MenuButton(card, "Resume", 0, UiPalette.AccentSoft, () => view.Close());
            MenuButton(card, "New Game", 1, UiPalette.Accent, () =>
            {
                view.Close();
                controller.OnNewGameTapped();
            });
            MenuButton(card, "How to Play", 2, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.HowToPlay));
            MenuButton(card, "Settings", 3, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Settings));
            MenuButton(card, "Store", 4, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Store));
            CornerButton(card, "Title", () => view.Show(ScreensView.Screen.Title));

            // Nothing to resume before the first game starts.
            refreshers.Add(() => resume.gameObject.SetActive(controller.Engine != null));
            return backdrop.gameObject;
        }

        /// <summary>The static rules card (design §5.1). Enough to play, not a manual — the game
        /// teaches the rest through the ghosted potentials and the Ask-Oma hint.</summary>
        private static GameObject BuildHowToPlay(RectTransform parent, ScreensView view)
        {
            var backdrop = Backdrop(parent, "HowToPlayScreen");
            var card = Card(backdrop, "HOW TO PLAY");

            const string rules =
                "Thirteen rounds. Each turn:\n\n" +
                "•  Tap ROLL to throw all five dice.\n" +
                "•  Tap any die to keep it, then roll again — up to three rolls.\n" +
                "•  Tap a box on your card to score there, then tap it again to confirm.\n\n" +
                "Each box is used once. Fill your upper section (Aces–Sixes) to 63 for a +35 bonus.\n\n" +
                "Stuck? Tap Oma's mug to ask what she would do. Tap her card to peek at her score.\n\n" +
                "Most points after thirteen rounds wins. Viel Glück, Schatz!";

            var body = UiBuilder.Text(card, "Rules", rules, 30f, UiPalette.Ink, TextAlignmentOptions.TopLeft,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.80f));
            body.enableWordWrapping = true;

            MenuButton(card, "Back", 4, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Home));
            return backdrop.gameObject;
        }

        private static GameObject BuildResults(RectTransform parent, ScreensView view, GameController controller,
            out ResultsView results)
        {
            var backdrop = Backdrop(parent, "ResultsScreen");
            var card = UiBuilder.Image(backdrop, "Card", new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.83f),
                UiPalette.Paper, cornerRadius: 34);
            results = card.gameObject.AddComponent<ResultsView>();

            // Outcome banner sits across the card's top edge, its text set per game.
            var banner = UiBuilder.Image(backdrop, "Banner", new Vector2(0.14f, 0.80f), new Vector2(0.86f, 0.90f),
                UiPalette.Accent, cornerRadius: 28);
            var bannerText = UiBuilder.Text(banner.rectTransform, "Text", "", 56f, UiPalette.Cream,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            bannerText.fontStyle = FontStyles.Bold;

            var omaLine = UiBuilder.Text(card.rectTransform, "OmaLine", "", 27f, UiPalette.Ink,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.94f));
            omaLine.enableWordWrapping = true;
            omaLine.fontStyle = FontStyles.Italic;

            // Column headers.
            UiBuilder.Text(card.rectTransform, "YouHead", "You", 26f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.50f, 0.80f), new Vector2(0.72f, 0.845f)).fontStyle = FontStyles.Bold;
            UiBuilder.Text(card.rectTransform, "OmaHead", "Oma", 26f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.74f, 0.80f), new Vector2(0.96f, 0.845f)).fontStyle = FontStyles.Bold;

            var order = new[]
            {
                Category.Aces, Category.Twos, Category.Threes, Category.Fours, Category.Fives, Category.Sixes,
                Category.ThreeOfAKind, Category.FourOfAKind, Category.FullHouse, Category.SmallStraight,
                Category.LargeStraight, Category.Yahtzee, Category.Chance,
            };
            const float top = 0.79f, pitch = 0.036f;
            for (int i = 0; i < order.Length; i++)
                ComparisonRow(card.rectTransform, order[i], top - i * pitch, pitch, results);

            // Total, bold, under a rule.
            float totalTop = top - order.Length * pitch - 0.004f;
            var totalRule = UiBuilder.Fill(card.rectTransform, "Rule", new Vector2(0.06f, totalTop),
                new Vector2(0.96f, totalTop), UiPalette.PaperRule);
            totalRule.rectTransform.offsetMax = new Vector2(0f, 2f);
            UiBuilder.Text(card.rectTransform, "TotalLabel", "Total", 30f, UiPalette.Ink, TextAlignmentOptions.MidlineLeft,
                new Vector2(0.08f, totalTop - pitch * 1.3f), new Vector2(0.50f, totalTop)).fontStyle = FontStyles.Bold;
            var playerTotal = UiBuilder.Text(card.rectTransform, "PlayerTotal", "", 32f, UiPalette.Ink,
                TextAlignmentOptions.Center, new Vector2(0.50f, totalTop - pitch * 1.3f), new Vector2(0.72f, totalTop));
            playerTotal.fontStyle = FontStyles.Bold;
            var omaTotal = UiBuilder.Text(card.rectTransform, "OmaTotal", "", 32f, UiPalette.Ink,
                TextAlignmentOptions.Center, new Vector2(0.74f, totalTop - pitch * 1.3f), new Vector2(0.96f, totalTop));
            omaTotal.fontStyle = FontStyles.Bold;

            results.Init(bannerText, omaLine, playerTotal, omaTotal);

            // Play Again / Home, side by side under the card.
            var again = UiBuilder.Image(backdrop, "PlayAgain", new Vector2(0.08f, 0.025f), new Vector2(0.50f, 0.085f),
                UiPalette.Accent, cornerRadius: 24);
            again.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                view.Close();
                controller.OnNewGameTapped();
            });
            UiBuilder.Text(again.rectTransform, "Label", "Play again", 32f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;

            var home = UiBuilder.Image(backdrop, "HomeBtn", new Vector2(0.52f, 0.025f), new Vector2(0.92f, 0.085f),
                UiPalette.ChromeLight, cornerRadius: 24);
            home.gameObject.AddComponent<Button>().onClick.AddListener(() => view.Show(ScreensView.Screen.Home));
            UiBuilder.Text(home.rectTransform, "Label", "Home", 32f, UiPalette.Cream,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;

            return backdrop.gameObject;
        }

        /// <summary>One category's You/Oma comparison line on the results card.</summary>
        private static void ComparisonRow(RectTransform card, Category category, float top, float pitch, ResultsView results)
        {
            UiBuilder.Text(card, "N_" + category, UiBuilder.DisplayName(category), 22f, UiPalette.Ink,
                TextAlignmentOptions.MidlineLeft, new Vector2(0.08f, top - pitch), new Vector2(0.50f, top));
            var player = UiBuilder.Text(card, "P_" + category, "", 24f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.50f, top - pitch), new Vector2(0.72f, top));
            var oma = UiBuilder.Text(card, "O_" + category, "", 24f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.74f, top - pitch), new Vector2(0.96f, top));
            results.AddRow(category, player, oma);
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
                difficultyButtons[i] = Chip(card, choice.ToString(), i, difficulties.Length, 0.83f,
                    () => GameSettings.SelectedDifficulty = choice);
            }

            SectionLabel(card, "Your dice", 0.69f);
            var diceButtons = new Image[DiceSkins.All.Length];
            for (int i = 0; i < DiceSkins.All.Length; i++)
            {
                var skin = DiceSkins.All[i];
                diceButtons[i] = Chip(card, skin.DisplayName, i, DiceSkins.All.Length, 0.62f,
                    () => { if (GameSettings.Owns(skin.Id)) GameSettings.SelectedDice = skin.Id; });
            }

            SectionLabel(card, "Sound & feel", 0.50f);
            var (soundBg, soundLabel) = ToggleChip(card, 0, 2, 0.43f, "Sound",
                () => GameSettings.SoundEnabled = !GameSettings.SoundEnabled);
            var (hapticsBg, hapticsLabel) = ToggleChip(card, 1, 2, 0.43f, "Haptics",
                () => GameSettings.HapticsEnabled = !GameSettings.HapticsEnabled);

            MenuButton(card, "Back", 4, UiPalette.ChromeLight, () => view.Show(ScreensView.Screen.Home));

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
                soundLabel.text = GameSettings.SoundEnabled ? "Sound: On" : "Sound: Off";
                soundBg.color = GameSettings.SoundEnabled ? UiPalette.Accent : UiPalette.PaperShade;
                hapticsLabel.text = GameSettings.HapticsEnabled ? "Haptics: On" : "Haptics: Off";
                hapticsBg.color = GameSettings.HapticsEnabled ? UiPalette.Accent : UiPalette.PaperShade;
            });
            return backdrop.gameObject;
        }

        /// <summary>A chip whose label and colour reflect an on/off setting, flipped on tap. The
        /// caller's refresher sets the text and colour so it always matches the stored value.</summary>
        private static (Image bg, TMPro.TextMeshProUGUI label) ToggleChip(RectTransform card, int index, int count,
            float top, string name, UnityEngine.Events.UnityAction onClick)
        {
            float width = 0.90f / count;
            float x0 = 0.05f + index * width;
            var bg = UiBuilder.Image(card, name, new Vector2(x0 + 0.012f, top - 0.085f),
                new Vector2(x0 + width - 0.012f, top), UiPalette.PaperShade, cornerRadius: 22);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            var label = UiBuilder.Text(bg.rectTransform, "Label", name, 28f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            label.fontStyle = FontStyles.Bold;
            return (bg, label);
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

        /// <summary>A stacked menu row. Spacing fits up to five rows in a card, so Home can carry
        /// the full pause menu; index 3 is also where the Settings/Store "Back" lands, clear of
        /// their content above it.</summary>
        private static Image MenuButton(RectTransform parent, string label, int index, Color colour,
            UnityEngine.Events.UnityAction onClick)
        {
            float top = 0.75f - index * 0.12f;
            var bg = UiBuilder.Image(parent, label, new Vector2(0.10f, top - 0.10f), new Vector2(0.90f, top),
                colour, cornerRadius: 26);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            var text = UiBuilder.Text(bg.rectTransform, "Label", label, 38f,
                colour == UiPalette.ChromeLight ? UiPalette.Cream : UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            text.fontStyle = FontStyles.Bold;
            return bg;
        }

        /// <summary>Small corner button, e.g. Home's "Title" (quit to the title screen).</summary>
        private static void CornerButton(RectTransform card, string label, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UiBuilder.Image(card, "Corner", new Vector2(0.06f, 0.80f), new Vector2(0.30f, 0.85f),
                UiPalette.PaperShade, cornerRadius: 16);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);
            UiBuilder.Text(bg.rectTransform, "Label", label, 26f, UiPalette.Ink,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
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

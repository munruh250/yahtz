using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Builds the entire M2 2D prototype screen in code at startup — no prefabs to
    /// maintain for throwaway scaffolding, and the whole layout is reviewable here. The four
    /// portrait zones follow design spec §5.2: header/Oma (top), dice (middle), scorecard,
    /// action bar (bottom).</summary>
    public static class UiBuilder
    {
        public sealed class Refs
        {
            public DiceView2D Dice;
            public ScorecardView Scorecard;
            public HudView Hud;
        }

        public static string DisplayName(Category category) => Names[(int)category];

        private static readonly string[] Names =
        {
            "Aces", "Twos", "Threes", "Fours", "Fives", "Sixes",
            "3 of a Kind", "4 of a Kind", "Full House", "Sm Straight", "Lg Straight",
            "Yahtzee", "Chance",
        };

        public static Refs Build(Transform root, GameController controller)
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas2D", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;

            Image(canvasGo.transform, "Background", Vector2.zero, Vector2.one, UiPalette.Background);

            var safe = Rect(canvasGo.transform, "SafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var refs = new Refs();
            BuildHeaderAndStatus(safe, out var header, out var status);
            refs.Dice = BuildDiceRow(safe, controller);
            refs.Scorecard = BuildScorecard(safe, controller);
            BuildActionBar(safe, controller, out var rollButton, out var rollLabel);

            // Sibling order = raycast/draw order: skip overlay above the play surface,
            // peek toggle above the overlay, game-over scrim above everything.
            var skipOverlay = BuildSkipOverlay(safe, controller);
            BuildPeekButton(safe, controller, out var peekButton, out var peekLabel);
            BuildGameOver(safe, controller, out var gameOverPanel, out var gameOverText);

            refs.Hud = safe.gameObject.AddComponent<HudView>();
            refs.Hud.Init(rollButton, rollLabel, status, header, gameOverPanel, gameOverText,
                skipOverlay, peekButton, peekLabel);
            return refs;
        }

        // ---- Zones ---------------------------------------------------------

        private static void BuildHeaderAndStatus(RectTransform parent, out TextMeshProUGUI header, out TextMeshProUGUI status)
        {
            header = Text(parent, "Header", "", 44f, UiPalette.Cream, TextAlignmentOptions.MidlineLeft,
                new Vector2(0.02f, 0.945f), new Vector2(0.74f, 0.995f));
            header.fontStyle = FontStyles.Bold;
            status = Text(parent, "Status", "", 38f, UiPalette.Gold, TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.90f), new Vector2(0.98f, 0.945f));
        }

        private static DiceView2D BuildDiceRow(RectTransform parent, GameController controller)
        {
            var row = Rect(parent, "DiceRow", new Vector2(0.02f, 0.775f), new Vector2(0.98f, 0.90f));
            var view = row.gameObject.AddComponent<DiceView2D>();
            var dice = new DieView2D[DiceState.DieCount];
            for (int i = 0; i < dice.Length; i++)
            {
                int index = i;
                float x0 = i / 5f, x1 = (i + 1) / 5f;
                var slot = Rect(row, $"Die{i}", new Vector2(x0 + 0.012f, 0.08f), new Vector2(x1 - 0.012f, 0.86f));
                var tapTarget = slot.gameObject.AddComponent<Image>();
                tapTarget.color = Color.clear; // invisible but raycastable tap area (incl. lift)
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = tapTarget;

                var visual = Rect(slot, "Visual", Vector2.zero, Vector2.one);
                var outline = Image(visual, "KeptOutline", Vector2.zero, Vector2.one, UiPalette.KeptOutline);
                outline.rectTransform.offsetMin = new Vector2(-10f, -10f);
                outline.rectTransform.offsetMax = new Vector2(10f, 10f);
                var face = Image(visual, "Face", Vector2.zero, Vector2.one, UiPalette.DieFace);
                var label = Text(face.rectTransform, "Value", "", 96f, UiPalette.DiePip, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one);
                label.fontStyle = FontStyles.Bold;

                var die = slot.gameObject.AddComponent<DieView2D>();
                die.Init(visual, outline, face, label, button, () => controller.OnDieTapped(index));
                die.SetKept(false);
                dice[i] = die;
            }
            view.Init(dice);
            return view;
        }

        private static ScorecardView BuildScorecard(RectTransform parent, GameController controller)
        {
            var outer = Image(parent, "Scorecard", new Vector2(0.02f, 0.115f), new Vector2(0.98f, 0.77f), UiPalette.Panel).rectTransform;
            var view = outer.gameObject.AddComponent<ScorecardView>();
            var title = Text(outer, "Owner", "YOUR CARD", 28f, UiPalette.CreamDim, TextAlignmentOptions.Center,
                new Vector2(0f, 0.945f), new Vector2(1f, 1f));
            var panel = Rect(outer, "Grid", Vector2.zero, new Vector2(1f, 0.945f));
            var cells = new Dictionary<Category, ScoreCellView>();

            // Left column: upper section + bonus progress row. Right column: lower section.
            var left = new[] { Category.Aces, Category.Twos, Category.Threes, Category.Fours, Category.Fives, Category.Sixes };
            var right = new[]
            {
                Category.ThreeOfAKind, Category.FourOfAKind, Category.FullHouse, Category.SmallStraight,
                Category.LargeStraight, Category.Yahtzee, Category.Chance,
            };
            const float rows = 7f;
            for (int r = 0; r < left.Length; r++)
                cells[left[r]] = BuildCell(panel, left[r], controller,
                    new Vector2(0.015f, 1f - (r + 1) / rows), new Vector2(0.495f, 1f - r / rows));
            for (int r = 0; r < right.Length; r++)
                cells[right[r]] = BuildCell(panel, right[r], controller,
                    new Vector2(0.505f, 1f - (r + 1) / rows), new Vector2(0.985f, 1f - r / rows));

            // Bonus progress row fills the left column's last slot.
            var bonusRow = Image(panel, "BonusRow", new Vector2(0.015f, 1f - 7f / rows), new Vector2(0.495f, 1f - 6f / rows), UiPalette.CreamDim).rectTransform;
            Pad(bonusRow, 4f);
            // Progress rendered by anchoring the fill's right edge (no sprite needed).
            var fill = Image(bonusRow, "Fill", Vector2.zero, new Vector2(0f, 1f), UiPalette.GoldDark);
            var bonusLabel = Text(bonusRow, "Label", "", 30f, UiPalette.Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);

            view.Init(cells, bonusLabel, fill, title);
            return view;
        }

        private static GameObject BuildSkipOverlay(RectTransform parent, GameController controller)
        {
            var overlay = Image(parent, "SkipOverlay", Vector2.zero, Vector2.one, Color.clear);
            var button = overlay.gameObject.AddComponent<Button>();
            button.targetGraphic = overlay;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(controller.OnSkipTapped);
            return overlay.gameObject;
        }

        private static void BuildPeekButton(RectTransform parent, GameController controller, out GameObject button, out TextMeshProUGUI label)
        {
            var bg = Image(parent, "PeekButton", new Vector2(0.76f, 0.948f), new Vector2(0.98f, 0.992f), UiPalette.Panel);
            var peek = bg.gameObject.AddComponent<Button>();
            peek.targetGraphic = bg;
            peek.onClick.AddListener(controller.OnPeekTapped);
            label = Text(bg.rectTransform, "Label", "Peek: Oma", 30f, UiPalette.Cream, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            button = bg.gameObject;
        }

        private static ScoreCellView BuildCell(RectTransform parent, Category category, GameController controller, Vector2 aMin, Vector2 aMax)
        {
            var bg = Image(parent, Names[(int)category], aMin, aMax, UiPalette.Cream);
            Pad(bg.rectTransform, 4f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var name = Text(bg.rectTransform, "Name", Names[(int)category], 30f, UiPalette.Ink, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f), new Vector2(0.68f, 1f));
            name.rectTransform.offsetMin = new Vector2(16f, 0f);
            var value = Text(bg.rectTransform, "Value", "", 36f, UiPalette.InkGhost, TextAlignmentOptions.MidlineRight,
                new Vector2(0.68f, 0f), new Vector2(1f, 1f));
            value.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var cell = bg.gameObject.AddComponent<ScoreCellView>();
            cell.Init(bg, name, value, button, () => controller.OnCellTapped(category));
            return cell;
        }

        private static void BuildActionBar(RectTransform parent, GameController controller, out Button rollButton, out TextMeshProUGUI rollLabel)
        {
            var bar = Rect(parent, "ActionBar", new Vector2(0.02f, 0.005f), new Vector2(0.98f, 0.105f));

            var rollBg = Image(bar, "RollButton", new Vector2(0f, 0.08f), new Vector2(0.66f, 0.95f), UiPalette.Gold);
            rollButton = rollBg.gameObject.AddComponent<Button>();
            rollButton.targetGraphic = rollBg;
            rollButton.onClick.AddListener(controller.OnRollTapped);
            rollLabel = Text(rollBg.rectTransform, "Label", "Roll", 52f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            rollLabel.fontStyle = FontStyles.Bold;

            var newBg = Image(bar, "NewGameButton", new Vector2(0.70f, 0.08f), new Vector2(1f, 0.95f), UiPalette.Panel);
            var newButton = newBg.gameObject.AddComponent<Button>();
            newButton.targetGraphic = newBg;
            newButton.onClick.AddListener(controller.OnNewGameTapped);
            Text(newBg.rectTransform, "Label", "New Game", 34f, UiPalette.Cream, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
        }

        private static void BuildGameOver(RectTransform parent, GameController controller, out GameObject panel, out TextMeshProUGUI text)
        {
            var scrim = Image(parent, "GameOver", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.78f));
            panel = scrim.gameObject;
            var box = Image(scrim.rectTransform, "Box", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f), UiPalette.Cream).rectTransform;
            text = Text(box, "Text", "", 60f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            text.fontStyle = FontStyles.Bold;

            var againBg = Image(box, "PlayAgain", new Vector2(0.22f, 0.06f), new Vector2(0.78f, 0.32f), UiPalette.Gold);
            var againButton = againBg.gameObject.AddComponent<Button>();
            againButton.targetGraphic = againBg;
            againButton.onClick.AddListener(controller.OnNewGameTapped);
            Text(againBg.rectTransform, "Label", "Play again", 42f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
        }

        // ---- Primitive helpers ---------------------------------------------

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = aMin;
            rect.anchorMax = aMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Image(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
        {
            var image = Rect(parent, name, aMin, aMax).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions align, Vector2 aMin, Vector2 aMax)
        {
            var text = Rect(parent, name, aMin, aMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Pad(RectTransform rect, float pixels)
        {
            rect.offsetMin = new Vector2(rect.offsetMin.x + pixels, rect.offsetMin.y + pixels);
            rect.offsetMax = new Vector2(rect.offsetMax.x - pixels, rect.offsetMax.y - pixels);
        }
    }
}

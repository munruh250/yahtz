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
            /// <summary>Null when built for world-space (3D) dice.</summary>
            public DiceView2D Dice;
            /// <summary>Null in 3D mode — the card is diegetic there, built by KitchenBuilder.</summary>
            public ScorecardView Scorecard;
            public HudView Hud;
            public SpeechBubbleView SpeechBubble;
        }

        public static string DisplayName(Category category) => Names[(int)category];

        private static readonly string[] Names =
        {
            "Aces", "Twos", "Threes", "Fours", "Fives", "Sixes",
            "3 of a Kind", "4 of a Kind", "Full House", "Sm Straight", "Lg Straight",
            "Yahtzee", "Chance",
        };

        /// <summary>Builds the screen-space UI. With <paramref name="worldDice"/> the canvas keeps
        /// only the non-diegetic strip design §5.2 allows — header, status, action bar, peek and
        /// the overlays. Background, dice row and scorecard all drop away: the camera shows the
        /// kitchen, and the card is a physical object on the table.</summary>
        public static Refs Build(Transform root, GameController controller, bool worldDice = false)
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

            if (!worldDice)
                Image(canvasGo.transform, "Background", Vector2.zero, Vector2.one, UiPalette.Background);

            var safe = Rect(canvasGo.transform, "SafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var refs = new Refs();
            BuildHeaderAndStatus(safe, out var header, out var status);
            if (!worldDice)
            {
                refs.Dice = BuildDiceRow(safe, controller);
                refs.Scorecard = BuildScorecard(safe, controller);
            }
            BuildActionBar(safe, controller, out var rollButton, out var rollLabel, out var askButton);
            refs.SpeechBubble = BuildSpeechBubble(safe);

            // Sibling order = raycast/draw order: skip overlay above the play surface,
            // peek toggle above the overlay, game-over scrim above everything.
            var skipOverlay = BuildSkipOverlay(safe, controller);
            BuildPeekButton(safe, controller, out var peekButton, out var peekLabel);
            BuildGameOver(safe, controller, out var gameOverPanel, out var gameOverText);

            refs.Hud = safe.gameObject.AddComponent<HudView>();
            refs.Hud.Init(rollButton, rollLabel, status, header, gameOverPanel, gameOverText,
                skipOverlay, peekButton, peekLabel, askButton);
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
            return ScorecardBuilder.BuildInto(outer, controller);
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

        private static void BuildActionBar(RectTransform parent, GameController controller,
            out Button rollButton, out TextMeshProUGUI rollLabel, out Button askButton)
        {
            var bar = Rect(parent, "ActionBar", new Vector2(0.02f, 0.005f), new Vector2(0.98f, 0.105f));

            // Roll stays the widest — it is the primary action. Ask Oma sits beside it; New Game
            // is rare and destructive, so it gets the smallest, furthest target.
            var rollBg = Image(bar, "RollButton", new Vector2(0f, 0.08f), new Vector2(0.45f, 0.95f), UiPalette.Gold);
            rollButton = rollBg.gameObject.AddComponent<Button>();
            rollButton.targetGraphic = rollBg;
            rollButton.onClick.AddListener(controller.OnRollTapped);
            rollLabel = Text(rollBg.rectTransform, "Label", "Roll", 52f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            rollLabel.fontStyle = FontStyles.Bold;

            var askBg = Image(bar, "AskOmaButton", new Vector2(0.48f, 0.08f), new Vector2(0.79f, 0.95f), UiPalette.GoldSoft);
            askButton = askBg.gameObject.AddComponent<Button>();
            askButton.targetGraphic = askBg;
            askButton.onClick.AddListener(controller.OnAskOmaTapped);
            Text(askBg.rectTransform, "Label", "Ask Oma", 38f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;

            var newBg = Image(bar, "NewGameButton", new Vector2(0.82f, 0.08f), new Vector2(1f, 0.95f), UiPalette.Panel);
            var newButton = newBg.gameObject.AddComponent<Button>();
            newButton.targetGraphic = newBg;
            newButton.onClick.AddListener(controller.OnNewGameTapped);
            Text(newBg.rectTransform, "Label", "New\nGame", 26f, UiPalette.Cream, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
        }

        /// <summary>Oma's speech bubble, sitting under her in the top zone (design §5.2). Nothing
        /// in it is a raycast target: it must never eat a tap meant for the dice or the card.</summary>
        private static SpeechBubbleView BuildSpeechBubble(RectTransform parent)
        {
            // The view lives on a holder that stays active, not on the panel it switches off —
            // otherwise its Update never runs and nothing can find it.
            var holder = Rect(parent, "SpeechBubble", new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.755f));
            var view = holder.gameObject.AddComponent<SpeechBubbleView>();

            var panel = Image(holder, "Bubble", Vector2.zero, Vector2.one, UiPalette.Cream);
            panel.raycastTarget = false;
            var text = Text(panel.rectTransform, "Text", "", 32f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;

            view.Init(panel.gameObject, text);
            return view;
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

        // ---- Primitive helpers (shared with ScorecardBuilder) ----------------

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.DontSave;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
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

        internal static Image Image(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
        {
            var image = Rect(parent, name, aMin, aMax).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
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

        internal static void Pad(RectTransform rect, float pixels)
        {
            rect.offsetMin = new Vector2(rect.offsetMin.x + pixels, rect.offsetMin.y + pixels);
            rect.offsetMax = new Vector2(rect.offsetMax.x - pixels, rect.offsetMax.y - pixels);
        }
    }
}

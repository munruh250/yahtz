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
            public ScreensView Screens;
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
                Fill(canvasGo.transform, "Background", Vector2.zero, Vector2.one, UiPalette.Backdrop);

            var safe = Rect(canvasGo.transform, "SafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var refs = new Refs();
            BuildHeaderAndStatus(safe, controller, out var header, out var status, out var menuButton, out var menuPanel);
            if (!worldDice)
            {
                refs.Dice = BuildDiceRow(safe, controller);
                refs.Scorecard = BuildScorecard(safe, controller);
            }
            BuildRollBar(safe, controller, out var rollButton, out var rollLabel, out var rollPips);
            refs.SpeechBubble = BuildSpeechBubble(safe);

            // Sibling order = raycast/draw order: skip overlay above the play surface,
            // game-over scrim above everything.
            var skipOverlay = BuildSkipOverlay(safe, controller);
            BuildMenuPanel(menuPanel, controller);
            BuildGameOver(safe, controller, out var gameOverPanel, out var gameOverText);

            refs.Screens = ScreensBuilder.Build(safe, controller);

            refs.Hud = safe.gameObject.AddComponent<HudView>();
            refs.Hud.Init(rollButton, rollLabel, rollPips, status, header, gameOverPanel, gameOverText,
                skipOverlay, menuButton, menuPanel);
            return refs;
        }

        // ---- Zones ---------------------------------------------------------

        /// <summary>Top strip: round and running totals on a solid bar, with everything that is
        /// not part of playing a turn tucked behind a hamburger.</summary>
        private static void BuildHeaderAndStatus(RectTransform parent, GameController controller, out TextMeshProUGUI header,
            out TextMeshProUGUI status, out Button menuButton, out GameObject menuPanel)
        {
            // A solid bar, not a scrim: the kitchen wall behind Oma is pale, and cream-on-plaster
            // washed the round/score line out completely once the room went in.
            var bar = Rect(parent, "TopBar", new Vector2(0f, 0.876f), new Vector2(1f, 1f));

            // Backing overflows the bar upward so it also covers the notch strip between the
            // safe area and the true top of the screen — otherwise a band of kitchen shows
            // above it, and the bar's own contents (which sit inside the safe area) spill below
            // a backing anchored to the screen. Overflowing one rect solves both ends.
            var backing = Fill(bar, "Backing", Vector2.zero, Vector2.one, UiPalette.Chrome);
            backing.raycastTarget = false;
            backing.rectTransform.offsetMax = new Vector2(0f, 260f);
            backing.transform.SetAsFirstSibling();

            // Scores span the full width up to the hamburger — cramped in the middle they were
            // being clipped by the camera cutout.
            header = Text(bar, "Header", "", 44f, UiPalette.Cream, TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.40f), new Vector2(0.84f, 1f));
            header.fontStyle = FontStyles.Bold;

            var menuBg = Image(bar, "MenuButton", new Vector2(0.855f, 0.42f), new Vector2(0.985f, 0.98f), UiPalette.ChromeLight);
            menuButton = menuBg.gameObject.AddComponent<Button>();
            menuButton.targetGraphic = menuBg;
            menuButton.onClick.AddListener(controller.OnMenuTapped);
            Text(menuBg.rectTransform, "Glyph", "≡", 54f, UiPalette.Cream, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;

            // Second line is the round. The old per-roll chatter ("Roll 2 of 3 done - tap dice to
            // keep...") is gone: the pips already show the rolls, and the taps are on the table.
            // It still yields to anything genuinely important — Joker rules, Oma's turn, toasts.
            status = Text(bar, "Status", "", 32f, UiPalette.Accent, TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.40f));

            // The hamburger opens the full Home screen now; this panel is retired.
            menuPanel = Rect(parent, "MenuPanel", new Vector2(0.52f, 0.64f), new Vector2(0.985f, 0.872f)).gameObject;
            menuPanel.SetActive(false);
        }

        /// <summary>Everything that is not "play this turn": restart, and the M6 placeholders.</summary>
        private static void BuildMenuPanel(GameObject panel, GameController controller)
        {
            var rect = (RectTransform)panel.transform;
            Image(rect, "Backing", Vector2.zero, Vector2.one, UiPalette.Chrome);

            AddMenuRow(rect, "New Game", 0, UiPalette.Accent, UiPalette.Ink, controller.OnNewGameTapped);
            AddMenuRow(rect, "Settings", 1, UiPalette.ChromeLight, UiPalette.CreamDim, null);
            AddMenuRow(rect, "Store", 2, UiPalette.ChromeLight, UiPalette.CreamDim, null);
        }

        private static void AddMenuRow(RectTransform parent, string label, int index, Color background,
            Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            const float rows = 3f;
            float top = 1f - (index + 0.12f) / rows;
            float bottom = 1f - (index + 0.92f) / rows;
            var bg = Image(parent, label, new Vector2(0.06f, bottom), new Vector2(0.94f, top), background);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            else
                button.interactable = false; // wired up in M6
            Text(bg.rectTransform, "Label", label, 34f, textColor, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
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
            var outer = Image(parent, "Scorecard", new Vector2(0.02f, 0.115f), new Vector2(0.98f, 0.77f), UiPalette.Paper).rectTransform;
            return ScorecardBuilder.BuildInto(outer, controller);
        }

        private static GameObject BuildSkipOverlay(RectTransform parent, GameController controller)
        {
            var overlay = Fill(parent, "SkipOverlay", Vector2.zero, Vector2.one, Color.clear);
            var button = overlay.gameObject.AddComponent<Button>();
            button.targetGraphic = overlay;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(controller.OnSkipTapped);
            return overlay.gameObject;
        }

        /// <summary>One primary action across the whole bottom edge. Rolling is the only thing
        /// you do from the HUD now — keeping dice, scoring and asking Oma are all taps on the
        /// table itself, so the bar carries a single unmissable target.</summary>
        private static void BuildRollBar(RectTransform parent, GameController controller,
            out Button rollButton, out TextMeshProUGUI rollLabel, out Image[] pips)
        {
            var rollBg = Image(parent, "RollButton", new Vector2(0.02f, 0.012f), new Vector2(0.98f, 0.108f), UiPalette.Accent);
            rollButton = rollBg.gameObject.AddComponent<Button>();
            rollButton.targetGraphic = rollBg;
            rollButton.onClick.AddListener(controller.OnRollTapped);

            rollLabel = Text(rollBg.rectTransform, "Label", "ROLL", 58f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0f), new Vector2(0.62f, 1f));
            rollLabel.fontStyle = FontStyles.Bold;

            // Three boxes that tick off left to right as the rolls are spent, like crossing them
            // off on paper. Drawn boxes rather than glyphs in the label, so no font can be
            // missing the character; the tick itself is a child label toggled by HudView.
            pips = new Image[3];
            for (int i = 0; i < pips.Length; i++)
            {
                float x0 = 0.66f + i * 0.10f;
                pips[i] = Image(rollBg.rectTransform, $"Pip{i}", new Vector2(x0, 0.28f), new Vector2(x0 + 0.078f, 0.72f),
                    UiPalette.Chrome, cornerRadius: 10);
                pips[i].raycastTarget = false;
                AddTick(pips[i].rectTransform);
            }
        }

        /// <summary>A tick drawn from two rotated bars. The obvious "✓" is not an option: the
        /// default TMP font has no glyph for it and it rendered as a tofu box on device.</summary>
        private static void AddTick(RectTransform pip)
        {
            var tick = Rect(pip, "Tick", Vector2.zero, Vector2.one);
            var shortArm = Fill(tick, "Short", new Vector2(0.12f, 0.34f), new Vector2(0.52f, 0.52f), UiPalette.Ink);
            shortArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            shortArm.raycastTarget = false;
            var longArm = Fill(tick, "Long", new Vector2(0.30f, 0.30f), new Vector2(0.90f, 0.48f), UiPalette.Ink);
            longArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            longArm.raycastTarget = false;
            tick.gameObject.SetActive(false);
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
            var scrim = Fill(parent, "GameOver", Vector2.zero, Vector2.one, new Color(0.16f, 0.14f, 0.26f, 0.82f));
            panel = scrim.gameObject;
            var box = Image(scrim.rectTransform, "Box", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f), UiPalette.Cream).rectTransform;
            text = Text(box, "Text", "", 60f, UiPalette.Ink, TextAlignmentOptions.Center,
                new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            text.fontStyle = FontStyles.Bold;

            var againBg = Image(box, "PlayAgain", new Vector2(0.22f, 0.06f), new Vector2(0.78f, 0.32f), UiPalette.Accent);
            var againButton = againBg.gameObject.AddComponent<Button>();
            againButton.targetGraphic = againBg;
            againButton.onClick.AddListener(controller.OnNewGameTapped);
            Text(againBg.rectTransform, "Label", "Play again", 42f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
        }

        // ---- Primitive helpers (shared with ScorecardBuilder) ----------------

        private static TMP_FontAsset _gameFont;
        private static bool _gameFontLooked;

        /// <summary>The game's typeface, built from Assets/Fonts by the FontTool. Null until one is
        /// dropped in, in which case TMP's default face is used — a missing font is a downgrade,
        /// not a break.</summary>
        private static TMP_FontAsset GameFont
        {
            get
            {
                if (_gameFontLooked)
                    return _gameFont;
                _gameFontLooked = true;
                _gameFont = Resources.Load<TMP_FontAsset>("Fonts/GameFont");
                return _gameFont;
            }
        }

        /// <summary>Exactly one EventSystem, and one that dies with its scene.
        ///
        /// **No hideFlags.** It used to be tagged `HideFlags.DontSave`, which means both "don't
        /// serialise into the scene" AND "survive scene loads" — so every reload left the previous
        /// one alive and added another. Unity disables all but one and logs "There are N event
        /// systems in the scene", and a disabled EventSystem means no UI input at all. Harmless
        /// while the game loaded its one scene once; Title ⇄ Game navigation is exactly the case
        /// that would have broken.
        ///
        /// The flags were never needed: this object is created in Awake, so it is not part of the
        /// scene asset and there is nothing to keep out of it. Without them it lives and dies with
        /// its scene, which is the whole requirement.
        ///
        /// The sweep clears any strays a previous build left behind; the inactive search matters
        /// because FindAnyObjectByType skips inactive objects, so a disabled leftover would go
        /// unnoticed and could re-enable itself later.</summary>
        private static void EnsureEventSystem()
        {
            var existing = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                for (int i = 1; i < existing.Length; i++)
                    Object.DestroyImmediate(existing[i].gameObject);
                existing[0].gameObject.SetActive(true);
                existing[0].enabled = true;
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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

        /// <summary>A soft-cornered panel. Everything in the UI is rounded — hard rectangles are
        /// most of what made the old chrome read as a spreadsheet rather than a storybook.</summary>
        internal static Image Image(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color,
            int cornerRadius = 20)
        {
            var image = Rect(parent, name, aMin, aMax).gameObject.AddComponent<Image>();
            image.color = color;
            if (cornerRadius > 0)
            {
                image.sprite = UiSprites.Rounded(cornerRadius);
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        /// <summary>A square-cornered fill, for the few places a rounded edge would be wrong —
        /// full-bleed backdrops and the bonus progress bar.</summary>
        internal static Image Fill(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color) =>
            Image(parent, name, aMin, aMax, color, cornerRadius: 0);

        internal static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions align, Vector2 aMin, Vector2 aMax)
        {
            var text = Rect(parent, name, aMin, aMax).gameObject.AddComponent<TextMeshProUGUI>();
            var face = GameFont;
            if (face != null)
                text.font = face;
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

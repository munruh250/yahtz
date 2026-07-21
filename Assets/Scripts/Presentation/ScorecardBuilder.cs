using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Builds the interactive scorecard. One grid serves both layers: the screen-space
    /// panel of the 2D prototype and the diegetic card lying on the kitchen table (design
    /// §5.2/§5.3). Only the surface differs — <see cref="ScoreCellView"/> states, the two-tap
    /// confirm and the controller wiring are identical either way.</summary>
    public static class ScorecardBuilder
    {
        /// <summary>Card-local "pixel" space. The canvas is scaled down to <see cref="CardWidth"/>
        /// metres, so cell code keeps working in familiar UI units instead of fractions of a metre.</summary>
        private const float CardPixelWidth = 620f;
        private const float CardPixelHeight = 540f;

        /// <summary>Table-top footprint, in metres — a die is 0.09, so this reads as a real pad.
        /// Width is the binding constraint: the portrait frustum is only ~25 degrees across, so a
        /// wider card would spill off the sides long before it filled the bottom third.</summary>
        private const float CardWidth = 0.52f;
        /// <summary>Propped toward the camera. Flat on the table (0) is badly foreshortened from
        /// the seated framings; tilting also buys apparent height without needing more table
        /// depth, which is what makes the boxes legible at the ScorecardFocus framing.</summary>
        private const float TiltDegrees = 24f;
        /// <summary>Player's side of the table. Placed so the card's far edge stops at the dice
        /// fence (z = -0.38) and its near edge stays on the table (z = -0.85).</summary>
        private const float CardCenterZ = -0.59f;
        private const float TableClearance = 0.004f;
        private const float BoardThickness = 0.008f;
        private const float BoardMargin = 0.03f;

        private static float CardHeight => CardWidth * CardPixelHeight / CardPixelWidth;

        /// <summary>The paper card as an in-world object: a world-space canvas laid on the table
        /// and angled at the player, on a thin backing board. Taps ray-cast through the canvas's
        /// own GraphicRaycaster against <paramref name="camera"/>.</summary>
        public static ScorecardView BuildWorld(Transform parent, GameController controller, Camera camera)
        {
            var canvasGo = new GameObject("ScorecardWorld", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera; // the raycaster's event camera in world space

            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(CardPixelWidth, CardPixelHeight);
            rect.localScale = Vector3.one * (CardWidth / CardPixelWidth);
            // Euler(90) lies a canvas face-up and readable from the player's seat; backing off by
            // the tilt lifts the far edge toward the camera. Local +X stays world +X, so the
            // text never mirrors.
            rect.localRotation = Quaternion.Euler(90f - TiltDegrees, 0f, 0f);
            // Raise the card so its near edge — the corner that dips lowest once tilted —
            // still clears the table surface.
            float lift = CardHeight / 2f * Mathf.Sin(TiltDegrees * Mathf.Deg2Rad);
            rect.localPosition = new Vector3(0f, lift + TableClearance, CardCenterZ);

            AddBackingBoard(parent, rect);

            var paper = UiBuilder.Image(rect, "Paper", Vector2.zero, Vector2.one, UiPalette.Panel).rectTransform;
            return BuildInto(paper, controller);
        }

        /// <summary>Backing board under the canvas so the card reads as an object resting on the
        /// table rather than a decal floating over it (and hides any z-fighting with the wood).</summary>
        private static void AddBackingBoard(Transform parent, RectTransform card)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "ScorecardBoard";
            board.transform.SetParent(parent, false);
            board.transform.localRotation = card.localRotation;
            // The canvas's readable face points at the player, so +forward is behind the paper.
            var behind = card.localRotation * Vector3.forward;
            board.transform.localPosition = card.localPosition + behind * (BoardThickness / 2f + 0.001f);
            board.transform.localScale = new Vector3(CardWidth + BoardMargin, CardHeight + BoardMargin, BoardThickness);
            board.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.30f, 0.20f, 0.13f),
            };
            Object.Destroy(board.GetComponent<Collider>()); // decoration; dice never reach it
        }

        /// <summary>Fills <paramref name="outer"/> with the 13 boxes, the owner title and the
        /// upper-bonus progress line, and returns the wired view.</summary>
        public static ScorecardView BuildInto(RectTransform outer, GameController controller)
        {
            var view = outer.gameObject.AddComponent<ScorecardView>();
            var title = UiBuilder.Text(outer, "Owner", "YOUR CARD", 28f, UiPalette.CreamDim, TextAlignmentOptions.Center,
                new Vector2(0f, 0.945f), new Vector2(1f, 1f));
            var panel = UiBuilder.Rect(outer, "Grid", Vector2.zero, new Vector2(1f, 0.945f));
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
            var bonusRow = UiBuilder.Image(panel, "BonusRow", new Vector2(0.015f, 1f - 7f / rows),
                new Vector2(0.495f, 1f - 6f / rows), UiPalette.CreamDim).rectTransform;
            UiBuilder.Pad(bonusRow, 4f);
            // Progress rendered by anchoring the fill's right edge (no sprite needed).
            var fill = UiBuilder.Image(bonusRow, "Fill", Vector2.zero, new Vector2(0f, 1f), UiPalette.GoldDark);
            var bonusLabel = UiBuilder.Text(bonusRow, "Label", "", 30f, UiPalette.Ink, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);

            view.Init(cells, bonusLabel, fill, title);
            return view;
        }

        private static ScoreCellView BuildCell(RectTransform parent, Category category, GameController controller,
            Vector2 aMin, Vector2 aMax)
        {
            var bg = UiBuilder.Image(parent, UiBuilder.DisplayName(category), aMin, aMax, UiPalette.Cream);
            UiBuilder.Pad(bg.rectTransform, 4f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var name = UiBuilder.Text(bg.rectTransform, "Name", UiBuilder.DisplayName(category), 30f, UiPalette.Ink,
                TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(0.68f, 1f));
            name.rectTransform.offsetMin = new Vector2(16f, 0f);
            var value = UiBuilder.Text(bg.rectTransform, "Value", "", 36f, UiPalette.InkGhost,
                TextAlignmentOptions.MidlineRight, new Vector2(0.68f, 0f), new Vector2(1f, 1f));
            value.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var cell = bg.gameObject.AddComponent<ScoreCellView>();
            cell.Init(bg, name, value, button, () => controller.OnCellTapped(category));
            return cell;
        }
    }
}

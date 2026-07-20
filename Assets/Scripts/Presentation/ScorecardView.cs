using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>The interactive scorecard: 13 cells, upper-bonus progress line, and ghost
    /// potentials. Pure rendering — all rules come from the controller's engine queries.</summary>
    public sealed class ScorecardView : MonoBehaviour
    {
        private Dictionary<Category, ScoreCellView> _cells;
        private TextMeshProUGUI _bonusLabel;
        private Image _bonusFill;
        private TextMeshProUGUI _title;

        public void Init(Dictionary<Category, ScoreCellView> cells, TextMeshProUGUI bonusLabel, Image bonusFill, TextMeshProUGUI title)
        {
            _cells = cells;
            _bonusLabel = bonusLabel;
            _bonusFill = bonusFill;
            _title = title;
        }

        /// <summary>Whose card is on screen ("YOUR CARD" / "OMA'S CARD") — matters for peeking.</summary>
        public void SetOwner(string label) => _title.text = label;

        /// <summary>Redraw every cell from the card + current potentials. <paramref name="potentials"/>
        /// is null outside the deciding phase (no ghosts, nothing selectable);
        /// <paramref name="selected"/> is the cell awaiting its confirm tap, if any.</summary>
        public void Render(Scorecard card, IReadOnlyDictionary<Category, int> potentials, Category? selected)
        {
            foreach (var pair in _cells)
            {
                var category = pair.Key;
                var cell = pair.Value;
                int? locked = card.GetScore(category);
                if (locked.HasValue)
                {
                    cell.ShowLocked(locked.Value);
                }
                else if (potentials != null && potentials.TryGetValue(category, out int ghost))
                {
                    if (selected == category)
                        cell.ShowSelected(ghost);
                    else
                        cell.ShowOpen(ghost, selectable: true);
                }
                else
                {
                    // Open but not currently scorable (before first roll, or Joker-illegal).
                    cell.ShowOpen(null, selectable: false);
                }
            }

            _cells[Category.Yahtzee].SetSuffix(card.YahtzeeBonusCount > 0 ? $"x{card.YahtzeeBonusCount}" : null);

            int upper = card.UpperSubtotal;
            float progress;
            if (card.HasUpperBonus)
            {
                _bonusLabel.text = $"Bonus secured  +{Scorecard.UpperBonusScore}";
                _bonusLabel.color = UiPalette.Ink;
                _bonusFill.color = UiPalette.Gold;
                progress = 1f;
            }
            else
            {
                _bonusLabel.text = $"Bonus  {upper} / {Scorecard.UpperBonusThreshold}";
                _bonusLabel.color = UiPalette.Ink;
                _bonusFill.color = UiPalette.GoldDark;
                progress = Mathf.Clamp01(upper / (float)Scorecard.UpperBonusThreshold);
            }
            _bonusFill.rectTransform.anchorMax = new Vector2(progress, 1f);
        }
    }
}

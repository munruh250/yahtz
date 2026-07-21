using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yahtzee.Presentation
{
    /// <summary>One scorecard box. Visual states per design spec §5.3: locked scores in solid
    /// ink, ghost potentials muted, selected cell gold awaiting the confirm tap, Joker-illegal
    /// cells dimmed and unselectable.</summary>
    public sealed class ScoreCellView : MonoBehaviour
    {
        private Image _background;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _value;
        private Button _button;
        private string _baseName;

        public void Init(Image background, TextMeshProUGUI nameLabel, TextMeshProUGUI valueLabel, Button button, Action onTapped)
        {
            _background = background;
            _name = nameLabel;
            _value = valueLabel;
            _button = button;
            _baseName = nameLabel.text;
            _button.onClick.AddListener(() => onTapped());
        }

        public void ShowLocked(int score)
        {
            _background.color = UiPalette.Paper;
            _value.text = score.ToString();
            _value.color = UiPalette.InkDark;
            _value.fontStyle = FontStyles.Bold;
            _button.interactable = false;
        }

        public void ShowOpen(int? ghostScore, bool selectable)
        {
            _background.color = selectable ? UiPalette.Paper : UiPalette.PaperShade;
            _value.text = ghostScore?.ToString() ?? "";
            _value.color = UiPalette.InkGhost;
            _value.fontStyle = FontStyles.Normal;
            _button.interactable = selectable;
        }

        /// <summary>Post-final-roll hint: one of the best boxes to take (soft gold).</summary>
        public void ShowSuggested(int ghostScore)
        {
            _background.color = UiPalette.AccentSoft;
            _value.text = ghostScore.ToString();
            _value.color = UiPalette.InkDark;
            _value.fontStyle = FontStyles.Bold;
            _button.interactable = true;
        }

        public void ShowSelected(int ghostScore)
        {
            _background.color = UiPalette.Accent;
            _value.text = ghostScore.ToString();
            _value.color = UiPalette.InkDark;
            _value.fontStyle = FontStyles.Bold;
            _button.interactable = true;
        }

        public void SetSuffix(string suffix)
        {
            // Extra marker after the name, e.g. Yahtzee bonus chips "x2".
            _name.text = string.IsNullOrEmpty(suffix)
                ? _baseName
                : $"{_baseName}<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.AccentDeep)}> {suffix}</color>";
        }
    }
}

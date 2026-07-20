using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yahtzee.Presentation
{
    /// <summary>One tappable 2D die: white square, big numeral, gold outline + lift when
    /// kept (position + outline, never color-only). Prototype-only, replaced in M4.</summary>
    public sealed class DieView2D : MonoBehaviour
    {
        private RectTransform _visual;
        private Image _outline;
        private Image _face;
        private TextMeshProUGUI _label;
        private Button _button;
        private bool _kept;

        public void Init(RectTransform visual, Image outline, Image face, TextMeshProUGUI label, Button button, Action onTapped)
        {
            _visual = visual;
            _outline = outline;
            _face = face;
            _label = label;
            _button = button;
            _button.onClick.AddListener(() => onTapped());
        }

        public void SetValue(int value)
        {
            _label.text = value >= 1 && value <= 6 ? value.ToString() : "";
        }

        public void SetKept(bool kept)
        {
            _kept = kept;
            _outline.enabled = kept;
            _visual.anchoredPosition = new Vector2(0f, kept ? 26f : 0f);
        }

        public void SetInteractable(bool interactable)
        {
            _button.interactable = interactable;
            var tint = interactable || _kept ? 1f : 0.85f;
            _face.color = new Color(UiPalette.DieFace.r, UiPalette.DieFace.g, UiPalette.DieFace.b, tint);
        }
    }
}

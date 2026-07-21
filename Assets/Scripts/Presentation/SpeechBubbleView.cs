using TMPro;
using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>Oma's speech bubble. Design §5.2: auto-dismisses, replaces rather than queues,
    /// and **never blocks input** — nothing in it is a raycast target, so a tap meant for a die
    /// or a score box still lands even with the bubble open.
    ///
    /// Interim: M5 replaces the caller with a DialogueService driven by GameEvents. This view's
    /// API is what that will drive, so it should survive unchanged.</summary>
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        private GameObject _panel;
        private TextMeshProUGUI _text;
        private float _hideAt;

        public void Init(GameObject panel, TextMeshProUGUI text)
        {
            _panel = panel;
            _text = text;
            _panel.SetActive(false);
        }

        public bool IsShowing => _panel != null && _panel.activeSelf;

        /// <summary>What she is currently saying (empty when hidden). For tests and for the M5
        /// dialogue service to check before replacing a line.</summary>
        public string Message => _text == null ? "" : _text.text;

        /// <summary>Show <paramref name="message"/>, replacing anything already up.</summary>
        public void Show(string message, float seconds)
        {
            _text.text = message;
            _panel.SetActive(true);
            // Unscaled: a paused or time-scaled game must not strand a bubble on screen.
            _hideAt = Time.unscaledTime + seconds;
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void Update()
        {
            if (IsShowing && Time.unscaledTime >= _hideAt)
                Hide();
        }
    }
}

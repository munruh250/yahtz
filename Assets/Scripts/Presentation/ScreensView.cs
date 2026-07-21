using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>The front-end screens layered over the kitchen: title, home, settings and store.
    ///
    /// Placeholder shells (design §5.1 sketches Title / Results / Pause properly in M6), but the
    /// settings and store they contain are real — difficulty and dice skins persist and take
    /// effect. Full-screen and opaque, so while one is open nothing behind it can be tapped.</summary>
    public sealed class ScreensView : MonoBehaviour
    {
        public enum Screen
        {
            None,
            Title,
            Home,
            Settings,
            Store,
        }

        private readonly Dictionary<Screen, GameObject> _panels = new Dictionary<Screen, GameObject>();
        private Action _onShown;

        public Screen Current { get; private set; } = Screen.None;

        /// <summary>True while any screen covers the game — the controller uses this to keep the
        /// board from being played behind them.</summary>
        public bool IsOpen => Current != Screen.None;

        public void Init(Dictionary<Screen, GameObject> panels, Action onShown)
        {
            foreach (var pair in panels)
            {
                _panels[pair.Key] = pair.Value;
                pair.Value.SetActive(false);
            }
            _onShown = onShown;
        }

        public void Show(Screen screen)
        {
            Current = screen;
            foreach (var pair in _panels)
                pair.Value.SetActive(pair.Key == screen);
            // Screens read live state (what you own, what is selected), so refresh on the way in
            // rather than trying to keep every label in sync as things change.
            _onShown?.Invoke();
        }

        public void Close() => Show(Screen.None);
    }
}

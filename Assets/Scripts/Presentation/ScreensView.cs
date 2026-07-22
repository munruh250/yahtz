using System;
using System.Collections.Generic;
using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>The front-end screens layered over the kitchen.
    ///
    /// Title / Home / Settings / Store are the hub flow; Results is the end-of-game screen
    /// (design §5.1); How to Play is the static rules card. The hamburger opens Home mid-game,
    /// which doubles as the pause overlay (Resume / Restart / How to Play / Settings / Title).
    /// All full-screen and opaque, so while one is open nothing behind it can be tapped —
    /// which is also how a paused game freezes.</summary>
    public sealed class ScreensView : MonoBehaviour
    {
        public enum Screen
        {
            None,
            Title,
            Home,
            Settings,
            Store,
            HowToPlay,
            Results,
        }

        private readonly Dictionary<Screen, GameObject> _panels = new Dictionary<Screen, GameObject>();
        private Action _onShown;
        private ResultsView _results;

        public Screen Current { get; private set; } = Screen.None;

        /// <summary>True while any screen covers the game — the controller uses this to keep the
        /// board from being played behind them.</summary>
        public bool IsOpen => Current != Screen.None;

        public void Init(Dictionary<Screen, GameObject> panels, ResultsView results, Action onShown)
        {
            foreach (var pair in panels)
            {
                _panels[pair.Key] = pair.Value;
                pair.Value.SetActive(false);
            }
            _results = results;
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

        /// <summary>Fill and raise the end-of-game screen. <paramref name="closingLine"/> is Oma's
        /// closing reaction, so it shows on the card rather than in a bubble hidden behind it.</summary>
        public void ShowResults(GameState state, GameResult result, string closingLine)
        {
            _results.Populate(state, result, closingLine);
            Show(Screen.Results);
        }

        public void Close() => Show(Screen.None);
    }
}

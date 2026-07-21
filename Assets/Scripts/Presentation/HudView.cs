using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Non-diegetic strip: roll button with remaining-roll pips, status line, header
    /// with totals/round, and the game-over panel.</summary>
    public sealed class HudView : MonoBehaviour
    {
        private Button _rollButton;
        private TextMeshProUGUI _rollLabel;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _header;
        private GameObject _gameOverPanel;
        private TextMeshProUGUI _gameOverText;
        private GameObject _skipOverlay;
        private Image[] _rollPips;
        private GameObject _menuPanel;

        public void Init(Button rollButton, TextMeshProUGUI rollLabel, Image[] rollPips, TextMeshProUGUI status,
            TextMeshProUGUI header, GameObject gameOverPanel, TextMeshProUGUI gameOverText,
            GameObject skipOverlay, Button menuButton, GameObject menuPanel)
        {
            _rollPips = rollPips;
            _menuPanel = menuPanel;

            _rollButton = rollButton;
            _rollLabel = rollLabel;
            _status = status;
            _header = header;
            _gameOverPanel = gameOverPanel;
            _gameOverText = gameOverText;
            _skipOverlay = skipOverlay;
            _gameOverPanel.SetActive(false);
            _skipOverlay.SetActive(false);
        }

        /// <summary>During Oma's turn a full-screen transparent overlay turns any tap into
        /// Skip (design §4); the peek toggle sits above it and stays tappable.</summary>
        public void SetOmaTurn(bool omaTurn) => _skipOverlay.SetActive(omaTurn);

        /// <summary>Boxes tick off left to right as the rolls are used up.</summary>
        public void SetRoll(bool enabled, int rollsUsed)
        {
            _rollButton.interactable = enabled;
            _rollLabel.text = "ROLL";
            for (int i = 0; i < _rollPips.Length; i++)
            {
                bool used = i < rollsUsed;
                // Cream when ticked, deep chrome when still to come: periwinkle-on-periwinkle
                // was nearly invisible against the button itself.
                _rollPips[i].color = used ? UiPalette.Paper : UiPalette.Chrome;
                _rollPips[i].transform.GetChild(0).gameObject.SetActive(used);
            }
        }

        public void CloseMenu() => _menuPanel.SetActive(false);

        public void SetStatus(string text) => _status.text = text;

        public void SetHeader(GameState state)
        {
            // Round moved to the line below; this one is the scoreline, spread wide so the
            // camera cutout cannot clip it.
            _header.text = $"You  {state.PlayerCard.Total}      Oma  {state.OmaCard.Total}";
        }

        public void ShowGameOver(GameEnded ended)
        {
            _gameOverPanel.SetActive(true);
            string headline = ended.Result switch
            {
                GameResult.PlayerWins => "You win!",
                GameResult.OmaWins => "Oma wins!",
                _ => "It's a tie!",
            };
            _gameOverText.text = $"{headline}\nYou {ended.PlayerTotal}  -  Oma {ended.OmaTotal}";
        }

        public void HideGameOver() => _gameOverPanel.SetActive(false);
    }
}

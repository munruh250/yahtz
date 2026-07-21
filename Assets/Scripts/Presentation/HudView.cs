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
        private Button _askButton;

        public void Init(Button rollButton, TextMeshProUGUI rollLabel, TextMeshProUGUI status,
            TextMeshProUGUI header, GameObject gameOverPanel, TextMeshProUGUI gameOverText,
            GameObject skipOverlay, Button askButton)
        {
            _askButton = askButton;
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

        public void SetRoll(bool enabled, int rollsRemaining)
        {
            _rollButton.interactable = enabled;
            // Middle-dot pips: filled = rolls left this turn.
            _rollLabel.text = rollsRemaining > 0 ? $"Roll  {new string('·', rollsRemaining)}" : "Roll";
        }

        /// <summary>Only offer a hint when there are dice on the table to advise about.</summary>
        public void SetAskOma(bool enabled) => _askButton.interactable = enabled;

        public void SetStatus(string text) => _status.text = text;

        public void SetHeader(GameState state)
        {
            _header.text = $"Round {state.Round}/{GameState.TotalRounds}    You {state.PlayerCard.Total}  -  Oma {state.OmaCard.Total}";
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

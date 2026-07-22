using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>The end-of-game results (design §5.1): who won, Oma's closing line, and both
    /// scorecards side by side so the player can see where the game was decided. Built once with
    /// a fixed row per category; <see cref="Populate"/> fills in the numbers for a finished game.</summary>
    public sealed class ResultsView : MonoBehaviour
    {
        private TextMeshProUGUI _banner;
        private TextMeshProUGUI _omaLine;
        private TextMeshProUGUI _playerTotal;
        private TextMeshProUGUI _omaTotal;
        private readonly Dictionary<Category, (TextMeshProUGUI player, TextMeshProUGUI oma)> _cells =
            new Dictionary<Category, (TextMeshProUGUI, TextMeshProUGUI)>();

        public void Init(TextMeshProUGUI banner, TextMeshProUGUI omaLine,
            TextMeshProUGUI playerTotal, TextMeshProUGUI omaTotal)
        {
            _banner = banner;
            _omaLine = omaLine;
            _playerTotal = playerTotal;
            _omaTotal = omaTotal;
        }

        public void AddRow(Category category, TextMeshProUGUI playerValue, TextMeshProUGUI omaValue) =>
            _cells[category] = (playerValue, omaValue);

        /// <summary>Fill the card for a finished game. <paramref name="closingLine"/> is Oma's
        /// end-of-game reaction, from the same dialogue set as everything else she says.</summary>
        public void Populate(GameState state, GameResult result, string closingLine)
        {
            _banner.text = result switch
            {
                GameResult.PlayerWins => "You win!",
                GameResult.OmaWins => "Oma wins!",
                _ => "A tie!",
            };
            _omaLine.text = closingLine ?? "";

            foreach (var pair in _cells)
            {
                var category = pair.Key;
                pair.Value.player.text = CellText(state.PlayerCard, category);
                pair.Value.oma.text = CellText(state.OmaCard, category);
            }

            _playerTotal.text = state.PlayerCard.Total.ToString();
            _omaTotal.text = state.OmaCard.Total.ToString();
            // The winner's total is emphasised so the eye lands on it first.
            bool playerWon = result == GameResult.PlayerWins;
            bool omaWon = result == GameResult.OmaWins;
            _playerTotal.color = playerWon ? UiPalette.Ink : UiPalette.InkGhost;
            _omaTotal.color = omaWon ? UiPalette.Ink : UiPalette.InkGhost;
        }

        /// <summary>Every category is filled at game end, but guard anyway — an empty box reads
        /// as a dash rather than "0", which would look like a scored zero.</summary>
        private static string CellText(Scorecard card, Category category)
        {
            int? score = card.GetScore(category);
            return score.HasValue ? score.Value.ToString() : "-";
        }
    }
}

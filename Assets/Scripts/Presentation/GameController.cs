using System;
using UnityEngine;
using Yahtzee.Core;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>Owns the GameEngine and sequences all presentation. The ONLY caller of engine
    /// mutators (CLAUDE.md); views render state and forward input here. In M2 the player plays
    /// both hands manually — Oma's AI arrives in M3.</summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Tests set this false to make every animation instant.</summary>
        public static bool AnimationsEnabled = true;

        public GameEngine Engine { get; private set; }

        /// <summary>True while an animation owns the screen and taps are ignored.</summary>
        public bool InputLocked { get; private set; }

        private IDiceView _dice;
        private ScorecardView _scorecard;
        private HudView _hud;

        private Category? _selected;
        private string _toast; // one-shot status flourish (bonus secured, yahtzee, joker)

        private void Awake()
        {
            Application.targetFrameRate = 60;
            var refs = UiBuilder.Build(transform, this);
            _dice = refs.Dice;
            _scorecard = refs.Scorecard;
            _hud = refs.Hud;
        }

        private void Start()
        {
            var saved = SaveService.HasResumableSave() ? SaveService.TryLoad() : null;
            AttachEngine(saved != null ? GameEngine.FromState(saved) : GameEngine.NewGame(NewSeed()));
        }

        private void AttachEngine(GameEngine engine)
        {
            Engine = engine;
            Engine.EventRaised += OnGameEvent;
            _selected = null;
            _toast = null;
            InputLocked = false;
            _hud.HideGameOver();
            RefreshAll();
        }

        private static int NewSeed() => Environment.TickCount ^ Guid.NewGuid().GetHashCode();

        // ---- Input entry points (wired by UiBuilder) -----------------------

        public void OnRollTapped()
        {
            if (InputLocked || Engine.State.Phase == GamePhase.GameOver || Engine.RollsRemaining == 0)
                return;
            _selected = null;
            InputLocked = true;
            Engine.Roll(); // raises DiceRolled → animation → unlock in OnDiceSettled
        }

        public void OnDieTapped(int index)
        {
            if (InputLocked || Engine.State.Phase != GamePhase.Deciding || Engine.RollsRemaining == 0)
                return;
            Engine.SetKeep(index, !Engine.State.Dice.Kept[index]);
            _dice.SetDice(Engine.State.Dice.Values, Engine.State.Dice.Kept);
        }

        public void OnCellTapped(Category category)
        {
            if (InputLocked || Engine.State.Phase != GamePhase.Deciding)
                return;
            var potentials = Engine.GetPotentialScores();
            if (!potentials.ContainsKey(category))
                return; // filled or Joker-illegal — cell is greyed anyway

            if (_selected == category)
            {
                // Two-tap confirm (design §5.3): second tap locks it in. No undo.
                _selected = null;
                Engine.ScoreCategory(category);
            }
            else
            {
                _selected = category;
                RefreshAll();
            }
        }

        public void OnNewGameTapped()
        {
            if (Engine != null)
                Engine.EventRaised -= OnGameEvent;
            SaveService.Delete();
            AttachEngine(GameEngine.NewGame(NewSeed()));
        }

        // ---- Engine events -------------------------------------------------

        private void OnGameEvent(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case DiceRolled rolled:
                    _dice.PlayRoll(rolled.Values, Engine.State.Dice.Kept, OnDiceSettled);
                    break;
                case JokerActivated joker:
                    _toast = joker.BonusApplies ? "Joker rules! +100 bonus - " : "Joker rules! ";
                    break;
                case ScoreCommitted committed:
                    if (committed.YahtzeeBonusAwarded)
                        _toast = "Yahtzee bonus +100! ";
                    break;
                case UpperBonusSecured bonus:
                    _toast = $"{(bonus.Player == PlayerId.Player ? "Your" : "Oma's")} upper bonus +35! ";
                    break;
                case TurnChanged _:
                    SaveService.Save(Engine.State);
                    RefreshAll();
                    break;
                case GameEnded ended:
                    SaveService.Save(Engine.State);
                    RefreshAll();
                    _hud.ShowGameOver(ended);
                    break;
            }
        }

        private void OnDiceSettled()
        {
            InputLocked = false;
            RefreshAll();
        }

        // ---- Rendering -----------------------------------------------------

        private void RefreshAll()
        {
            var state = Engine.State;
            bool deciding = state.Phase == GamePhase.Deciding;
            var potentials = deciding && !InputLocked ? Engine.GetPotentialScores() : null;

            _hud.SetHeader(state);
            _hud.SetRoll(!InputLocked && state.Phase != GamePhase.GameOver && Engine.RollsRemaining > 0,
                Engine.RollsRemaining);
            _dice.SetDice(state.Dice.Values, state.Dice.Kept);
            _dice.SetInteractable(!InputLocked && deciding && Engine.RollsRemaining > 0);
            _scorecard.Render(Engine.CurrentCard, potentials, _selected);
            _hud.SetStatus(BuildStatus(deciding, potentials));
            _toast = null;
        }

        private string BuildStatus(bool deciding, System.Collections.Generic.IReadOnlyDictionary<Category, int> potentials)
        {
            var state = Engine.State;
            string toast = _toast ?? "";
            if (state.Phase == GamePhase.GameOver)
                return toast + "Game over.";

            string who = state.CurrentPlayer == PlayerId.Player ? "Your turn" : "Oma's turn (you play her hand)";
            if (!deciding)
                return $"{toast}{who} - tap Roll";
            if (InputLocked)
                return "Rolling...";
            if (_selected.HasValue && potentials != null)
                return $"{toast}Tap again to confirm: {UiBuilder.DisplayName(_selected.Value)} for {potentials[_selected.Value]}";
            if (Engine.IsJokerTurn)
                return JokerRules.BonusApplies(Engine.CurrentCard)
                    ? "Joker rules! +100 bonus - you must use a highlighted box"
                    : "Joker rules! You must use a highlighted box";
            return Engine.RollsRemaining > 0
                ? $"{toast}{who} - roll {state.Dice.RollsUsed} of 3 done, keep dice or score"
                : $"{toast}{who} - choose a score";
        }

        // ---- Lifecycle safety ----------------------------------------------

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveIfInProgress();
        }

        private void OnApplicationQuit() => SaveIfInProgress();

        private void SaveIfInProgress()
        {
            if (Engine != null && Engine.State.Phase != GamePhase.GameOver)
                SaveService.Save(Engine.State);
        }
    }
}

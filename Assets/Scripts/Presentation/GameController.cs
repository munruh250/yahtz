using System;
using System.Collections;
using UnityEngine;
using Yahtzee.Core;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>Owns the GameEngine and sequences all presentation. The ONLY caller of engine
    /// mutators (CLAUDE.md); views render state and forward input here.
    ///
    /// M3: Oma plays her own turns — staged OmaAI decisions with visible think beats
    /// (design §4: 6-12 s per turn), tap-anywhere Skip that fast-forwards without changing
    /// outcomes (decisions are deterministic given state), and scorecard peeking.</summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Tests set this false to make every animation and think-beat instant.</summary>
        public static bool AnimationsEnabled = true;

        public GameEngine Engine { get; private set; }

        /// <summary>True while a roll animation owns the screen.</summary>
        public bool InputLocked { get; private set; }

        public bool IsOmaTurn =>
            Engine != null
            && Engine.State.CurrentPlayer == PlayerId.Oma
            && Engine.State.Phase != GamePhase.GameOver;

        private readonly OmaAI _oma = new OmaAI();

        private IDiceView _dice;
        private ScorecardView _scorecard;
        private HudView _hud;

        private Category? _selected;   // player's pending confirm, or Oma's flash
        private bool _peekOther;       // scorecard shows the non-current player's card
        private bool _fastForward;     // Skip pressed during Oma's turn
        private string _toast;         // one-shot status flourish
        private Coroutine _omaRoutine;

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
            if (_omaRoutine != null)
            {
                StopCoroutine(_omaRoutine);
                _omaRoutine = null;
            }
            if (Engine != null)
                Engine.EventRaised -= OnGameEvent;

            Engine = engine;
            Engine.EventRaised += OnGameEvent;
            _selected = null;
            _peekOther = false;
            _fastForward = false;
            _toast = null;
            InputLocked = false;
            _hud.HideGameOver();
            RefreshAll();

            // A loaded save may resume mid-Oma-turn.
            if (IsOmaTurn)
                _omaRoutine = StartCoroutine(OmaTurnRoutine());
        }

        private static int NewSeed() => Environment.TickCount ^ Guid.NewGuid().GetHashCode();

        // ---- Input entry points (wired by UiBuilder) -----------------------

        public void OnRollTapped()
        {
            if (InputLocked || IsOmaTurn || Engine.State.Phase == GamePhase.GameOver || Engine.RollsRemaining == 0)
                return;
            _selected = null;
            InputLocked = true;
            Engine.Roll(); // raises DiceRolled → animation → unlock in OnDiceSettled
        }

        public void OnDieTapped(int index)
        {
            if (InputLocked || IsOmaTurn || Engine.State.Phase != GamePhase.Deciding || Engine.RollsRemaining == 0)
                return;
            Engine.SetKeep(index, !Engine.State.Dice.Kept[index]);
            _dice.SetDice(Engine.State.Dice.Values, Engine.State.Dice.Kept);
        }

        public void OnCellTapped(Category category)
        {
            if (InputLocked || IsOmaTurn || _peekOther || Engine.State.Phase != GamePhase.Deciding)
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

        /// <summary>Tap anywhere during Oma's turn: fast-forward to her result. Her decisions
        /// are pure functions of state, so skipping cannot change them.</summary>
        public void OnSkipTapped()
        {
            if (!IsOmaTurn)
                return;
            _fastForward = true;
            _dice.SkipAnimation();
        }

        public void OnPeekTapped()
        {
            if (Engine.State.Phase == GamePhase.GameOver)
                return;
            _peekOther = !_peekOther;
            _selected = null;
            RefreshAll();
        }

        public void OnNewGameTapped()
        {
            SaveService.Delete();
            AttachEngine(GameEngine.NewGame(NewSeed()));
        }

        // ---- Oma's turn ------------------------------------------------------

        private IEnumerator OmaTurnRoutine()
        {
            _fastForward = false;

            if (Engine.State.Phase == GamePhase.TurnStart)
            {
                if (ShouldPace) yield return ThinkBeat(0.9f);
                yield return RollAndSettle();
            }

            while (Engine.State.Phase == GamePhase.Deciding && Engine.RollsRemaining > 0)
            {
                if (ShouldPace) yield return ThinkBeat(0.8f);
                var keep = _oma.DecideKeepers(Engine);

                bool all = true;
                foreach (bool k in keep)
                    all &= k;
                if (all)
                    break; // she stands pat and scores

                // Keepers highlight one by one so her plan is readable.
                for (int i = 0; i < DiceState.DieCount; i++)
                {
                    if (Engine.State.Dice.Kept[i] == keep[i])
                        continue;
                    Engine.SetKeep(i, keep[i]);
                    _dice.SetDice(Engine.State.Dice.Values, Engine.State.Dice.Kept);
                    if (ShouldPace) yield return ThinkBeat(0.18f);
                }
                if (ShouldPace) yield return ThinkBeat(0.55f);
                yield return RollAndSettle();
            }

            if (ShouldPace) yield return ThinkBeat(1.0f);
            var category = _oma.DecideCategory(Engine);

            // Flash her chosen cell before it fills in.
            _selected = category;
            _peekOther = false;
            RefreshAll();
            if (ShouldPace) yield return ThinkBeat(0.6f);

            _selected = null;
            _omaRoutine = null;
            Engine.ScoreCategory(category); // advances turn; events refresh the screen
        }

        private bool ShouldPace => AnimationsEnabled && !_fastForward;

        private IEnumerator ThinkBeat(float seconds)
        {
            float t = 0f;
            while (t < seconds && !_fastForward)
            {
                yield return null;
                t += Time.deltaTime;
            }
        }

        private IEnumerator RollAndSettle()
        {
            InputLocked = true;
            Engine.Roll(); // instant settle when animations are off
            while (InputLocked)
            {
                if (_fastForward)
                    _dice.SkipAnimation();
                yield return null;
            }
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
                    _peekOther = false;
                    RefreshAll();
                    if (IsOmaTurn && _omaRoutine == null)
                        _omaRoutine = StartCoroutine(OmaTurnRoutine());
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

            var displayedCard = _peekOther
                ? (state.CurrentPlayer == PlayerId.Player ? state.OmaCard : state.PlayerCard)
                : Engine.CurrentCard;
            bool showPotentials = !_peekOther && deciding && !InputLocked;
            var potentials = showPotentials ? Engine.GetPotentialScores() : null;

            _hud.SetHeader(state);
            _hud.SetRoll(!InputLocked && !IsOmaTurn && state.Phase != GamePhase.GameOver && Engine.RollsRemaining > 0,
                Engine.RollsRemaining);
            _hud.SetOmaTurn(IsOmaTurn);
            _hud.SetPeek(state.Phase != GamePhase.GameOver, PeekLabel(state));
            _dice.SetDice(state.Dice.Values, state.Dice.Kept);
            _dice.SetInteractable(!InputLocked && !IsOmaTurn && deciding && Engine.RollsRemaining > 0);
            _scorecard.Render(displayedCard, potentials, _selected);
            _scorecard.SetOwner(OwnerLabel(state, displayedCard));
            _hud.SetStatus(BuildStatus(deciding, potentials));
            _toast = null;
        }

        private string PeekLabel(GameState state)
        {
            if (_peekOther)
                return "Back";
            bool otherIsOma = state.CurrentPlayer == PlayerId.Player;
            return otherIsOma ? "Peek: Oma" : "Peek: You";
        }

        private static string OwnerLabel(GameState state, Scorecard displayed) =>
            displayed == state.PlayerCard ? "YOUR CARD" : "OMA'S CARD";

        private string BuildStatus(bool deciding, System.Collections.Generic.IReadOnlyDictionary<Category, int> potentials)
        {
            var state = Engine.State;
            string toast = _toast ?? "";
            if (state.Phase == GamePhase.GameOver)
                return toast + "Game over.";
            if (IsOmaTurn)
                return InputLocked || deciding || state.Phase == GamePhase.TurnStart
                    ? $"{toast}Oma's turn - tap anywhere to skip"
                    : $"{toast}Oma's turn";
            if (!deciding)
                return $"{toast}Your turn - tap Roll";
            if (InputLocked)
                return "Rolling...";
            if (_peekOther)
                return "Peeking at Oma's card";
            if (_selected.HasValue && potentials != null)
                return $"{toast}Tap again to confirm: {UiBuilder.DisplayName(_selected.Value)} for {potentials[_selected.Value]}";
            if (Engine.IsJokerTurn)
                return JokerRules.BonusApplies(Engine.CurrentCard)
                    ? "Joker rules! +100 bonus - you must use a highlighted box"
                    : "Joker rules! You must use a highlighted box";
            return Engine.RollsRemaining > 0
                ? $"{toast}Roll {state.Dice.RollsUsed} of 3 done - keep dice, roll again, or score"
                : $"{toast}Choose a score";
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

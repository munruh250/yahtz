using System;
using System.Collections.Generic;

namespace Yahtzee.Core
{
    /// <summary>The only mutator of <see cref="GameState"/>. Enforces all legality (roll
    /// limits, filled boxes, Joker restrictions) and throws on illegal calls so bugs surface
    /// loudly in tests; the UI merely greys out illegal actions. Raises <see cref="GameEvent"/>s
    /// as the only channel to presentation.</summary>
    public sealed class GameEngine
    {
        public GameState State { get; }

        private readonly IRandomSource _rng;

        public event Action<GameEvent> EventRaised;

        public GameEngine(GameState state, IRandomSource rng)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>Fresh game from a seed (seed is stored in the state for save/replay).</summary>
        public static GameEngine NewGame(int seed) =>
            new GameEngine(new GameState { Seed = seed }, new SeededRandomSource(seed));

        /// <summary>Resume a loaded game; reconstructs the die stream at the saved position.</summary>
        public static GameEngine FromState(GameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            return new GameEngine(state, new SeededRandomSource(state.Seed, state.RngDraws));
        }

        public Scorecard CurrentCard => State.CardOf(State.CurrentPlayer);

        public int RollsRemaining => State.Dice.RollsRemaining;

        /// <summary>True when the current dice put this turn under Joker rules.</summary>
        public bool IsJokerTurn =>
            State.Phase == GamePhase.Deciding
            && JokerRules.IsJokerSituation(State.Dice.Values, CurrentCard);

        /// <summary>Roll all non-kept dice. Legal from TurnStart (all five) or Deciding with
        /// rolls remaining.</summary>
        public void Roll()
        {
            RequireNotOver();
            if (State.Dice.RollsUsed >= DiceState.MaxRolls)
                throw new InvalidOperationException("No rolls remaining this turn.");

            var dice = State.Dice;
            for (int i = 0; i < DiceState.DieCount; i++)
            {
                if (dice.Kept[i])
                    continue;
                dice.Values[i] = _rng.NextDie();
                State.RngDraws++;
            }
            dice.RollsUsed++;
            State.Phase = GamePhase.Deciding;

            Raise(new DiceRolled
            {
                Player = State.CurrentPlayer,
                Values = (int[])dice.Values.Clone(),
                RollNumber = dice.RollsUsed,
            });

            if (IsJokerTurn)
            {
                Raise(new JokerActivated
                {
                    Player = State.CurrentPlayer,
                    BonusApplies = JokerRules.BonusApplies(CurrentCard),
                    LegalCategories = JokerRules.LegalCategories(dice.Values, CurrentCard),
                });
            }
        }

        /// <summary>Mark/unmark a die as a keeper. Legal only between rolls (a keeper set
        /// after any roll may be released again before the next — DESIGN_SPEC §3 step 2).</summary>
        public void SetKeep(int dieIndex, bool keep)
        {
            RequireNotOver();
            if (dieIndex < 0 || dieIndex >= DiceState.DieCount)
                throw new ArgumentOutOfRangeException(nameof(dieIndex));
            if (State.Phase != GamePhase.Deciding)
                throw new InvalidOperationException("Cannot set keepers before the first roll.");
            if (State.Dice.RollsUsed >= DiceState.MaxRolls)
                throw new InvalidOperationException("No rolls remaining — keepers are moot.");
            State.Dice.Kept[dieIndex] = keep;
        }

        /// <summary>Open boxes, or the Joker-restricted set when Joker rules are active.</summary>
        public IReadOnlyList<Category> GetLegalCategories()
        {
            RequireNotOver();
            if (State.Phase != GamePhase.Deciding)
                throw new InvalidOperationException("Nothing rolled yet — no scoring available.");
            if (IsJokerTurn)
                return JokerRules.LegalCategories(State.Dice.Values, CurrentCard);
            return new List<Category>(CurrentCard.OpenCategories());
        }

        /// <summary>Potential score per legal box for the current dice (Joker-aware), for
        /// scorecard ghosting.</summary>
        public IReadOnlyDictionary<Category, int> GetPotentialScores()
        {
            bool joker = IsJokerTurn;
            var scores = new Dictionary<Category, int>();
            foreach (var category in GetLegalCategories())
            {
                scores[category] = joker
                    ? JokerRules.Score(category, State.Dice.Values)
                    : ScoreCalculator.Score(category, State.Dice.Values);
            }
            return scores;
        }

        /// <summary>Lock the current dice into <paramref name="category"/>: applies the score
        /// and any Yahtzee bonus, then advances the turn/round or ends the game.</summary>
        public void ScoreCategory(Category category)
        {
            bool joker = IsJokerTurn; // evaluated before any mutation
            var legal = GetLegalCategories();
            if (!Contains(legal, category))
                throw new InvalidOperationException($"{category} is not a legal box for this roll.");

            var card = CurrentCard;
            var player = State.CurrentPlayer;
            int points = joker
                ? JokerRules.Score(category, State.Dice.Values)
                : ScoreCalculator.Score(category, State.Dice.Values);

            bool bonusAwarded = joker && JokerRules.BonusApplies(card);
            bool hadUpperBonus = card.HasUpperBonus;

            card.SetScore(category, points);
            if (bonusAwarded)
                card.YahtzeeBonusCount++;

            Raise(new ScoreCommitted
            {
                Player = player,
                Category = category,
                Points = points,
                YahtzeeBonusAwarded = bonusAwarded,
                NewTotal = card.Total,
            });
            if (!hadUpperBonus && card.HasUpperBonus)
                Raise(new UpperBonusSecured { Player = player });

            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            State.Dice.ResetForNewTurn();

            if (State.CurrentPlayer == PlayerId.Player)
            {
                State.CurrentPlayer = PlayerId.Oma;
                State.Phase = GamePhase.TurnStart;
                Raise(new TurnChanged { Player = PlayerId.Oma, Round = State.Round });
                return;
            }

            if (State.Round >= GameState.TotalRounds)
            {
                State.Phase = GamePhase.GameOver;
                int playerTotal = State.PlayerCard.Total;
                int omaTotal = State.OmaCard.Total;
                Raise(new GameEnded
                {
                    PlayerTotal = playerTotal,
                    OmaTotal = omaTotal,
                    Result = playerTotal > omaTotal ? GameResult.PlayerWins
                        : omaTotal > playerTotal ? GameResult.OmaWins
                        : GameResult.Tie,
                });
                return;
            }

            State.Round++;
            State.CurrentPlayer = PlayerId.Player;
            State.Phase = GamePhase.TurnStart;
            Raise(new TurnChanged { Player = PlayerId.Player, Round = State.Round });
        }

        private void RequireNotOver()
        {
            if (State.Phase == GamePhase.GameOver)
                throw new InvalidOperationException("The game is over.");
        }

        private static bool Contains(IReadOnlyList<Category> list, Category category)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == category)
                    return true;
            return false;
        }

        private void Raise(GameEvent gameEvent) => EventRaised?.Invoke(gameEvent);
    }
}

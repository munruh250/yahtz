using System.Collections.Generic;

namespace Yahtzee.Core
{
    public enum GameResult
    {
        PlayerWins = 0,
        OmaWins = 1,
        Tie = 2,
    }

    /// <summary>Events are the ONLY channel from core to presentation (CLAUDE.md). The engine
    /// raises them synchronously, in order, from its mutators.</summary>
    public abstract class GameEvent
    {
    }

    public sealed class DiceRolled : GameEvent
    {
        public PlayerId Player;
        /// <summary>Copy of the five values after this roll.</summary>
        public int[] Values;
        /// <summary>1–3.</summary>
        public int RollNumber;
    }

    /// <summary>Raised right after a roll that puts the turn under Joker rules, so the UI can
    /// restrict cells and show the explainer.</summary>
    public sealed class JokerActivated : GameEvent
    {
        public PlayerId Player;
        /// <summary>True when scoring this roll will also earn the +100 bonus.</summary>
        public bool BonusApplies;
        public IReadOnlyList<Category> LegalCategories;
    }

    public sealed class ScoreCommitted : GameEvent
    {
        public PlayerId Player;
        public Category Category;
        public int Points;
        public bool YahtzeeBonusAwarded;
        /// <summary>Card total after this commit (and any bonus), for running-total UI.</summary>
        public int NewTotal;
    }

    /// <summary>Upper section crossed 63 with this commit — +35 secured.</summary>
    public sealed class UpperBonusSecured : GameEvent
    {
        public PlayerId Player;
    }

    public sealed class TurnChanged : GameEvent
    {
        /// <summary>Whose turn is starting.</summary>
        public PlayerId Player;
        /// <summary>1–13.</summary>
        public int Round;
    }

    public sealed class GameEnded : GameEvent
    {
        public int PlayerTotal;
        public int OmaTotal;
        public GameResult Result;
    }
}

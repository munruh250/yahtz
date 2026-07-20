using System;

namespace Yahtzee.Core
{
    public enum PlayerId
    {
        Player = 0,
        Oma = 1,
    }

    public enum GamePhase
    {
        /// <summary>Turn begun, nothing rolled yet — only Roll() is legal.</summary>
        TurnStart = 0,
        /// <summary>At least one roll made — keep/re-roll/score are legal per rules.</summary>
        Deciding = 1,
        /// <summary>Round 13 complete, both cards full. No further mutation.</summary>
        GameOver = 2,
    }

    /// <summary>The complete, serializable game: both scorecards, turn/round position, dice,
    /// and RNG position. This one object IS the save file (TECH_PLAN §2). The RNG is stored
    /// as seed + draws-consumed so a load resumes the identical die stream.</summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentSaveVersion = 1;
        public const int TotalRounds = 13;

        public int SaveVersion = CurrentSaveVersion;
        public int Seed;
        public int RngDraws;
        public int Round = 1;
        public PlayerId CurrentPlayer = PlayerId.Player;
        public GamePhase Phase = GamePhase.TurnStart;
        public DiceState Dice = new DiceState();
        public Scorecard PlayerCard = new Scorecard();
        public Scorecard OmaCard = new Scorecard();

        public Scorecard CardOf(PlayerId player) =>
            player == PlayerId.Player ? PlayerCard : OmaCard;
    }
}

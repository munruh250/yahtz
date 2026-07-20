using System;

namespace Yahtzee.Core
{
    /// <summary>Five die values (1–6; 0 = not yet rolled this turn), keep flags, and
    /// rolls used this turn (0–3). Serializable as part of <see cref="GameState"/>.</summary>
    [Serializable]
    public class DiceState
    {
        public const int DieCount = 5;
        public const int MaxRolls = 3;

        public int[] Values = new int[DieCount];
        public bool[] Kept = new bool[DieCount];
        public int RollsUsed;

        public int RollsRemaining => MaxRolls - RollsUsed;

        public void ResetForNewTurn()
        {
            for (int i = 0; i < DieCount; i++)
            {
                Values[i] = 0;
                Kept[i] = false;
            }
            RollsUsed = 0;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Yahtzee.Core
{
    /// <summary>One player's 13 boxes plus Yahtzee-bonus count. Slots use a -1 sentinel for
    /// "open" so the whole card is a plain int array — JsonUtility-friendly by design
    /// (TECH_PLAN §4). Once set, a slot can never change.</summary>
    [Serializable]
    public class Scorecard
    {
        public const int OpenSlot = -1;
        public const int UpperBonusThreshold = 63;
        public const int UpperBonusScore = 35;
        public const int YahtzeeBonusScore = 100;

        public int[] Slots;
        public int YahtzeeBonusCount;

        public Scorecard()
        {
            Slots = new int[CategoryExtensions.Count];
            for (int i = 0; i < Slots.Length; i++)
                Slots[i] = OpenSlot;
        }

        public bool IsOpen(Category category) => Slots[(int)category] == OpenSlot;

        /// <summary>Locked score for a box, or null while it is open.</summary>
        public int? GetScore(Category category) =>
            IsOpen(category) ? (int?)null : Slots[(int)category];

        public void SetScore(Category category, int score)
        {
            if (!IsOpen(category))
                throw new InvalidOperationException($"{category} is already scored.");
            if (score < 0)
                throw new ArgumentOutOfRangeException(nameof(score));
            Slots[(int)category] = score;
        }

        public IEnumerable<Category> OpenCategories()
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] == OpenSlot)
                    yield return (Category)i;
        }

        public bool IsComplete
        {
            get
            {
                foreach (int slot in Slots)
                    if (slot == OpenSlot)
                        return false;
                return true;
            }
        }

        public int UpperSubtotal => SumRange(Category.Aces, Category.Sixes);

        public bool HasUpperBonus => UpperSubtotal >= UpperBonusThreshold;

        public int LowerSubtotal => SumRange(Category.ThreeOfAKind, Category.Chance);

        public int Total =>
            UpperSubtotal
            + (HasUpperBonus ? UpperBonusScore : 0)
            + LowerSubtotal
            + YahtzeeBonusCount * YahtzeeBonusScore;

        private int SumRange(Category first, Category last)
        {
            int sum = 0;
            for (int i = (int)first; i <= (int)last; i++)
                if (Slots[i] != OpenSlot)
                    sum += Slots[i];
            return sum;
        }
    }
}

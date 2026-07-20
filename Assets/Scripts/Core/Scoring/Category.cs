namespace Yahtzee.Core
{
    /// <summary>The 13 scorecard boxes. Order is the canonical scorecard order; the
    /// int value doubles as the slot index in <see cref="Scorecard"/>.</summary>
    public enum Category
    {
        Aces = 0,
        Twos = 1,
        Threes = 2,
        Fours = 3,
        Fives = 4,
        Sixes = 5,
        ThreeOfAKind = 6,
        FourOfAKind = 7,
        FullHouse = 8,
        SmallStraight = 9,
        LargeStraight = 10,
        Yahtzee = 11,
        Chance = 12,
    }

    public static class CategoryExtensions
    {
        public const int Count = 13;

        public static bool IsUpper(this Category category) => (int)category <= (int)Category.Sixes;

        public static bool IsLower(this Category category) => !category.IsUpper();

        /// <summary>The die face (1–6) an upper category scores, e.g. Fours → 4.</summary>
        public static int UpperFace(this Category category)
        {
            if (!category.IsUpper())
                throw new System.ArgumentException($"{category} is not an upper category.");
            return (int)category + 1;
        }

        /// <summary>The upper category matching a die face (1–6), e.g. 4 → Fours.</summary>
        public static Category UpperCategoryForFace(int face)
        {
            if (face < 1 || face > 6)
                throw new System.ArgumentOutOfRangeException(nameof(face), face, "Face must be 1–6.");
            return (Category)(face - 1);
        }
    }
}

using System;

namespace Yahtzee.Core
{
    /// <summary>Deterministic die stream from a seed. <see cref="GameState"/> stores the seed
    /// plus the number of draws made, so a loaded game reconstructs the identical stream by
    /// replaying (and discarding) the draws already consumed.</summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed, int drawsToSkip = 0)
        {
            if (drawsToSkip < 0)
                throw new ArgumentOutOfRangeException(nameof(drawsToSkip));
            _random = new Random(seed);
            for (int i = 0; i < drawsToSkip; i++)
                _random.Next(1, 7);
        }

        public int NextDie() => _random.Next(1, 7);
    }
}

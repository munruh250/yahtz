using System;
using System.Collections.Generic;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    /// <summary>Deterministic die sequence for tests. Throws when exhausted so an
    /// unexpected extra draw fails the test loudly.</summary>
    public sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values)
        {
            foreach (int v in values)
                if (v < 1 || v > 6)
                    throw new ArgumentException($"Scripted die value {v} out of range 1–6.");
            _values = new Queue<int>(values);
        }

        public int Remaining => _values.Count;

        public int NextDie()
        {
            if (_values.Count == 0)
                throw new InvalidOperationException("Scripted RNG exhausted — engine drew more dice than the test scripted.");
            return _values.Dequeue();
        }
    }
}

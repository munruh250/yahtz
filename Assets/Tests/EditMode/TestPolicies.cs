using System.Collections.Generic;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    /// <summary>A deterministic "keep the most common face, score the highest box" policy so
    /// headless games are replayable. Not Oma's AI (that's M3) — just a legal driver.</summary>
    public static class TestPolicies
    {
        public static void PlayGreedyTurn(GameEngine engine)
        {
            engine.Roll();
            while (engine.RollsRemaining > 0)
            {
                int[] values = engine.State.Dice.Values;
                if (ScoreCalculator.IsYahtzee(values))
                    break;

                int bestFace = MostCommonFace(values);
                for (int i = 0; i < DiceState.DieCount; i++)
                    engine.SetKeep(i, values[i] == bestFace);
                engine.Roll();
            }

            engine.ScoreCategory(BestCategory(engine.GetPotentialScores()));
        }

        private static int MostCommonFace(int[] values)
        {
            var counts = ScoreCalculator.Histogram(values);
            int best = 1;
            for (int face = 2; face <= 6; face++)
                if (counts[face] >= counts[best])
                    best = face; // ties → higher face
            return best;
        }

        private static Category BestCategory(IReadOnlyDictionary<Category, int> potentials)
        {
            Category best = default;
            int bestScore = -1;
            for (int i = 0; i < CategoryExtensions.Count; i++)
            {
                var category = (Category)i;
                if (potentials.TryGetValue(category, out int score) && score > bestScore)
                {
                    best = category;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}

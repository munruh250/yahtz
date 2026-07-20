using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.EditorTools
{
    /// <summary>TECH_PLAN §6.5: N-game OmaAI self-play harness. Reports the score
    /// distribution used to tune her toward the 200-230 design target.
    /// Also runnable headless: -executeMethod Yahtzee.EditorTools.OmaSimulationTool.Run10k</summary>
    public static class OmaSimulationTool
    {
        [MenuItem("Yahtzee/Run Oma Simulation (10k games)")]
        public static void Run10k()
        {
            const int games = 10000;
            var ai = new OmaAI();
            var totals = new List<int>();
            int upperBonuses = 0, yahtzeeBonuses = 0;

            for (int seed = 0; seed < games; seed++)
            {
                var engine = GameEngine.NewGame(seed);
                engine.EventRaised += e =>
                {
                    if (e is UpperBonusSecured) upperBonuses++;
                    if (e is ScoreCommitted c && c.YahtzeeBonusAwarded) yahtzeeBonuses++;
                };
                while (engine.State.Phase != GamePhase.GameOver)
                    PlayAiTurn(engine, ai);
                totals.Add(engine.State.PlayerCard.Total);
                totals.Add(engine.State.OmaCard.Total);
            }

            totals.Sort();
            Debug.Log(
                $"Oma self-play: {games} games ({totals.Count} cards). " +
                $"mean {totals.Average():F1}, p10 {Pct(totals, 0.10)}, median {Pct(totals, 0.50)}, " +
                $"p90 {Pct(totals, 0.90)}, min {totals[0]}, max {totals[totals.Count - 1]} | " +
                $"upper bonus {upperBonuses / (double)totals.Count:P1}/card, " +
                $"yahtzee bonuses {yahtzeeBonuses / (double)totals.Count:F3}/card | target mean 200-230");
        }

        private static int Pct(List<int> sorted, double p) => sorted[(int)(sorted.Count * p)];

        private static void PlayAiTurn(GameEngine engine, OmaAI ai)
        {
            engine.Roll();
            while (engine.RollsRemaining > 0)
            {
                var keep = ai.DecideKeepers(engine);
                bool all = true;
                for (int i = 0; i < DiceState.DieCount; i++)
                {
                    engine.SetKeep(i, keep[i]);
                    all &= keep[i];
                }
                if (all)
                    break;
                engine.Roll();
            }
            engine.ScoreCategory(ai.DecideCategory(engine));
        }
    }
}

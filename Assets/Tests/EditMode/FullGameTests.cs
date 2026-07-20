using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    /// <summary>M1 exit criterion: complete 13-round games run headless with correct totals,
    /// across many seeds, with every event accounted for and no illegal action possible.</summary>
    public class FullGameTests
    {
        [Test]
        public void SeededGame_RunsThirteenRounds_TotalsConsistent()
        {
            RunAndVerifyFullGame(seed: 20260719);
        }

        [Test]
        public void ManySeeds_AllGamesCompleteLegally()
        {
            for (int seed = 0; seed < 200; seed++)
                RunAndVerifyFullGame(seed);
        }

        private static void RunAndVerifyFullGame(int seed)
        {
            var engine = GameEngine.NewGame(seed);
            var events = new List<GameEvent>();
            engine.EventRaised += events.Add;

            int guard = 0;
            while (engine.State.Phase != GamePhase.GameOver)
            {
                TestPolicies.PlayGreedyTurn(engine);
                Assert.Less(++guard, 27, $"seed {seed}: game did not end after 26 turns");
            }

            var state = engine.State;
            Assert.AreEqual(GameState.TotalRounds, state.Round, $"seed {seed}");
            Assert.IsTrue(state.PlayerCard.IsComplete, $"seed {seed}");
            Assert.IsTrue(state.OmaCard.IsComplete, $"seed {seed}");

            // Event accounting: 26 commits, 25 turn changes, exactly one game end.
            Assert.AreEqual(26, events.OfType<ScoreCommitted>().Count(), $"seed {seed}");
            Assert.AreEqual(25, events.OfType<TurnChanged>().Count(), $"seed {seed}");
            var ended = events.OfType<GameEnded>().Single();
            Assert.AreEqual(state.PlayerCard.Total, ended.PlayerTotal, $"seed {seed}");
            Assert.AreEqual(state.OmaCard.Total, ended.OmaTotal, $"seed {seed}");
            Assert.AreEqual(
                ended.PlayerTotal > ended.OmaTotal ? GameResult.PlayerWins
                : ended.OmaTotal > ended.PlayerTotal ? GameResult.OmaWins
                : GameResult.Tie,
                ended.Result, $"seed {seed}");

            // Totals rebuilt purely from events must match the cards.
            foreach (var player in new[] { PlayerId.Player, PlayerId.Oma })
            {
                var commits = events.OfType<ScoreCommitted>().Where(c => c.Player == player).ToList();
                Assert.AreEqual(13, commits.Count, $"seed {seed}");
                int rebuilt = commits.Sum(c => c.Points)
                    + Scorecard.YahtzeeBonusScore * commits.Count(c => c.YahtzeeBonusAwarded)
                    + Scorecard.UpperBonusScore * events.OfType<UpperBonusSecured>().Count(b => b.Player == player);
                Assert.AreEqual(state.CardOf(player).Total, rebuilt, $"seed {seed}, {player}");
            }

            // Joker activations always accompany a five-of-a-kind roll.
            foreach (var joker in events.OfType<JokerActivated>())
                Assert.IsNotEmpty(joker.LegalCategories, $"seed {seed}");

            // The finished game rejects everything.
            Assert.Throws<InvalidOperationException>(() => engine.Roll());
            Assert.Throws<InvalidOperationException>(() => engine.SetKeep(0, true));
            Assert.Throws<InvalidOperationException>(() => engine.GetLegalCategories());
            Assert.Throws<InvalidOperationException>(() => engine.ScoreCategory(Category.Chance));
        }
    }
}

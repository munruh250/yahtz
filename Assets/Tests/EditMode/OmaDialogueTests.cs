using System;
using System.Collections.Generic;
using NUnit.Framework;
using Yahtzee.Core;
using Yahtzee.Presentation;

namespace Yahtzee.Tests
{
    /// <summary>Oma's dialogue: the house rules from design §2, and the event-to-trigger map.
    ///
    /// The writing rules are worth enforcing mechanically because they are exactly what erodes as
    /// lines get added — a fourth variant that runs to two lines, or a trigger left with one.</summary>
    public class OmaDialogueTests
    {
        /// <summary>Design §2: "max ~90 characters, so they read in a glance".</summary>
        private const int MaxLineLength = 90;

        [Test]
        public void EveryTrigger_HasAtLeastThreeVariants()
        {
            foreach (OmaTrigger trigger in Enum.GetValues(typeof(OmaTrigger)))
            {
                Assert.IsTrue(OmaDialogueLines.Default.ContainsKey(trigger), $"{trigger} has no lines at all");
                Assert.GreaterOrEqual(OmaDialogueLines.Default[trigger].Length, 3,
                    $"{trigger} needs 3+ variants (design §2) or she repeats herself within a game");
            }
        }

        [Test]
        public void EveryLine_ReadsInAGlance()
        {
            foreach (var pair in OmaDialogueLines.Default)
            foreach (var line in pair.Value)
            {
                Assert.IsNotEmpty(line, $"{pair.Key} has an empty variant");
                Assert.LessOrEqual(line.Length, MaxLineLength,
                    $"{pair.Key}: \"{line}\" is {line.Length} chars, over the {MaxLineLength} the bubble can show");
            }
        }

        /// <summary>Every variant is used before any repeats, so a game does not hear the same
        /// line twice while others go unheard.</summary>
        [Test]
        public void Picker_UsesEveryVariantBeforeRepeating()
        {
            var lines = new Dictionary<OmaTrigger, string[]>
            {
                [OmaTrigger.GameStart] = new[] { "a", "b", "c" },
            };
            var picker = new OmaDialoguePicker(lines, new Random(1));

            var firstPass = new HashSet<string> { picker.Next(OmaTrigger.GameStart), picker.Next(OmaTrigger.GameStart), picker.Next(OmaTrigger.GameStart) };
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, firstPass);

            // Pool exhausted: it refills rather than returning null.
            Assert.IsNotNull(picker.Next(OmaTrigger.GameStart));
        }

        [Test]
        public void Picker_ForgetsHistoryOnANewGame()
        {
            var lines = new Dictionary<OmaTrigger, string[]> { [OmaTrigger.GameStart] = new[] { "only" } };
            var picker = new OmaDialoguePicker(lines, new Random(1));
            Assert.AreEqual("only", picker.Next(OmaTrigger.GameStart));
            picker.Reset();
            Assert.AreEqual("only", picker.Next(OmaTrigger.GameStart));
        }

        [Test]
        public void UnknownTrigger_IsSilentRatherThanThrowing()
        {
            var picker = new OmaDialoguePicker(new Dictionary<OmaTrigger, string[]>(), new Random(1));
            Assert.IsNull(picker.Next(OmaTrigger.Tie));
        }

        // ---- Trigger mapping -------------------------------------------------

        [Test]
        public void ZeroAndBigHands_AreRemarkedOn_OrdinaryScoresAreNot()
        {
            var state = new GameState();
            Assert.IsNotNull(Service().React(Score(PlayerId.Player, 0), state), "a zero deserves sympathy");
            Assert.IsNotNull(Service().React(Score(PlayerId.Oma, 30), state), "a big hand deserves a crow");
            Assert.IsNull(Service().React(Score(PlayerId.Player, 12), state),
                "an ordinary score should pass without comment, or she talks over every turn");
        }

        /// <summary>She should not announce a "lead" over an empty scorecard.</summary>
        [Test]
        public void LeadChange_IsSilentUntilSomeoneIsActuallyAhead()
        {
            var service = new DialogueService();
            var state = new GameState();
            Assert.IsNull(service.React(Score(PlayerId.Player, 12), state), "0-0 is nobody's lead");

            state.OmaCard.SetScore(Category.Chance, 20);
            Assert.IsNotNull(service.React(Score(PlayerId.Oma, 20), state), "Oma going ahead is worth saying");
            // Still ahead: no second announcement.
            Assert.IsNull(service.React(Score(PlayerId.Oma, 3), state), "she should not re-announce the same lead");
        }

        [Test]
        public void FinalRound_IsAnnouncedOnceAsItOpens()
        {
            var service = new DialogueService();
            var state = new GameState();
            Assert.IsNull(service.React(new TurnChanged { Player = PlayerId.Player, Round = 5 }, state));
            Assert.IsNotNull(service.React(
                new TurnChanged { Player = PlayerId.Player, Round = GameState.TotalRounds }, state));
        }

        [Test]
        public void EveryEnding_HasSomethingToSay()
        {
            var state = new GameState();
            foreach (GameResult result in Enum.GetValues(typeof(GameResult)))
                Assert.IsNotNull(Service().React(new GameEnded { Result = result }, state), $"silent on {result}");
        }

        [Test]
        public void StartingAGame_Greets()
        {
            Assert.IsNotNull(new DialogueService().StartGame());
        }

        private static DialogueService Service() => new DialogueService();

        private static ScoreCommitted Score(PlayerId player, int points) =>
            new ScoreCommitted { Player = player, Category = Category.Chance, Points = points };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yahtzee.Core;

namespace Yahtzee.Tests
{
    public class GameEngineTests
    {
        private List<GameEvent> _events;

        private GameEngine Engine(GameState state, params int[] scriptedDice)
        {
            var engine = new GameEngine(state, new ScriptedRandomSource(scriptedDice));
            _events = new List<GameEvent>();
            engine.EventRaised += _events.Add;
            return engine;
        }

        private T Single<T>() where T : GameEvent => _events.OfType<T>().Single();

        // ---- Rolling -------------------------------------------------------

        [Test]
        public void FirstRoll_FillsAllFiveDice_RaisesDiceRolled()
        {
            var engine = Engine(new GameState(), 1, 2, 3, 4, 5);
            engine.Roll();

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, engine.State.Dice.Values);
            Assert.AreEqual(1, engine.State.Dice.RollsUsed);
            Assert.AreEqual(GamePhase.Deciding, engine.State.Phase);
            Assert.AreEqual(5, engine.State.RngDraws);

            var rolled = Single<DiceRolled>();
            Assert.AreEqual(PlayerId.Player, rolled.Player);
            Assert.AreEqual(1, rolled.RollNumber);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rolled.Values);
        }

        [Test]
        public void Reroll_OnlyTouchesUnkeptDice()
        {
            var engine = Engine(new GameState(), 3, 3, 4, 5, 6, /* reroll of dice 2..4: */ 1, 1, 2);
            engine.Roll();
            engine.SetKeep(0, true);
            engine.SetKeep(1, true);
            engine.Roll();

            CollectionAssert.AreEqual(new[] { 3, 3, 1, 1, 2 }, engine.State.Dice.Values);
            Assert.AreEqual(2, engine.State.Dice.RollsUsed);
            Assert.AreEqual(8, engine.State.RngDraws);
        }

        [Test]
        public void Keeper_CanBeReleased_BeforeRollThree()
        {
            var engine = Engine(new GameState(), 6, 6, 6, 6, 6, /* roll 2 keeps all but 0: */ 2, /* roll 3, dice 0 and 1 free: */ 3, 4);
            engine.Roll();
            for (int i = 1; i < 5; i++)
                engine.SetKeep(i, true);
            engine.Roll(); // die 0 → 2
            CollectionAssert.AreEqual(new[] { 2, 6, 6, 6, 6 }, engine.State.Dice.Values);

            engine.SetKeep(1, false); // release a die kept since roll 1
            engine.SetKeep(0, true);
            engine.SetKeep(0, false); // toggling back and forth is fine
            engine.Roll(); // dice 0,1 → 3,4
            CollectionAssert.AreEqual(new[] { 3, 4, 6, 6, 6 }, engine.State.Dice.Values);
        }

        [Test]
        public void FourthRoll_Throws()
        {
            var engine = Engine(new GameState(), 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5);
            engine.Roll();
            engine.Roll();
            engine.Roll();
            Assert.Throws<InvalidOperationException>(() => engine.Roll());
        }

        // ---- Keep legality -------------------------------------------------

        [Test]
        public void SetKeep_IllegalBeforeFirstRoll_AfterThirdRoll_BadIndex()
        {
            var engine = Engine(new GameState(), 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5);
            Assert.Throws<InvalidOperationException>(() => engine.SetKeep(0, true));

            engine.Roll();
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetKeep(-1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetKeep(5, true));

            engine.Roll();
            engine.Roll();
            Assert.Throws<InvalidOperationException>(() => engine.SetKeep(0, true));
        }

        // ---- Scoring legality ---------------------------------------------

        [Test]
        public void Scoring_IllegalBeforeFirstRoll()
        {
            var engine = Engine(new GameState(), 1, 2, 3, 4, 5);
            Assert.Throws<InvalidOperationException>(() => engine.GetLegalCategories());
            Assert.Throws<InvalidOperationException>(() => engine.ScoreCategory(Category.Chance));
        }

        [Test]
        public void Scoring_FilledBox_Throws()
        {
            var state = new GameState();
            state.PlayerCard.SetScore(Category.Chance, 20);
            var engine = Engine(state, 1, 2, 3, 4, 5);
            engine.Roll();
            Assert.Throws<InvalidOperationException>(() => engine.ScoreCategory(Category.Chance));
        }

        [Test]
        public void ScoringEarly_AfterRollOne_IsLegal()
        {
            var engine = Engine(new GameState(), 6, 6, 6, 2, 2);
            engine.Roll();
            engine.ScoreCategory(Category.FullHouse);
            Assert.AreEqual(25, engine.State.PlayerCard.GetScore(Category.FullHouse));
        }

        [Test]
        public void PotentialScores_MatchCalculator_ForAllOpenBoxes()
        {
            var engine = Engine(new GameState(), 2, 3, 4, 5, 6);
            engine.Roll();
            var potentials = engine.GetPotentialScores();
            Assert.AreEqual(13, potentials.Count);
            foreach (var pair in potentials)
                Assert.AreEqual(ScoreCalculator.Score(pair.Key, new[] { 2, 3, 4, 5, 6 }), pair.Value);
            Assert.AreEqual(40, potentials[Category.LargeStraight]);
            Assert.AreEqual(20, potentials[Category.Chance]);
        }

        // ---- Joker enforcement ---------------------------------------------

        [Test]
        public void JokerRoll_RestrictsToMatchingUpper_AwardsBonus()
        {
            var state = new GameState();
            state.PlayerCard.SetScore(Category.Yahtzee, 50);
            var engine = Engine(state, 4, 4, 4, 4, 4);
            engine.Roll();

            Assert.IsTrue(engine.IsJokerTurn);
            var joker = Single<JokerActivated>();
            Assert.IsTrue(joker.BonusApplies);
            CollectionAssert.AreEqual(new[] { Category.Fours }, joker.LegalCategories.ToArray());
            CollectionAssert.AreEqual(new[] { Category.Fours }, engine.GetLegalCategories().ToArray());

            Assert.Throws<InvalidOperationException>(() => engine.ScoreCategory(Category.Chance));
            Assert.Throws<InvalidOperationException>(() => engine.ScoreCategory(Category.FullHouse));

            engine.ScoreCategory(Category.Fours);
            Assert.AreEqual(20, state.PlayerCard.GetScore(Category.Fours));
            Assert.AreEqual(1, state.PlayerCard.YahtzeeBonusCount);

            var commit = Single<ScoreCommitted>();
            Assert.IsTrue(commit.YahtzeeBonusAwarded);
            Assert.AreEqual(20, commit.Points);
            Assert.AreEqual(50 + 20 + 100, commit.NewTotal);
        }

        [Test]
        public void JokerRoll_ZeroYahtzeeBox_NoBonus_LowerWildcardValues()
        {
            var state = new GameState();
            state.PlayerCard.SetScore(Category.Yahtzee, 0);
            state.PlayerCard.SetScore(Category.Fours, 12);
            var engine = Engine(state, 4, 4, 4, 4, 4);
            engine.Roll();

            var joker = Single<JokerActivated>();
            Assert.IsFalse(joker.BonusApplies);

            var potentials = engine.GetPotentialScores();
            Assert.AreEqual(25, potentials[Category.FullHouse]);
            Assert.AreEqual(30, potentials[Category.SmallStraight]);
            Assert.AreEqual(40, potentials[Category.LargeStraight]);
            Assert.AreEqual(20, potentials[Category.ThreeOfAKind]);

            engine.ScoreCategory(Category.FullHouse);
            Assert.AreEqual(25, state.PlayerCard.GetScore(Category.FullHouse));
            Assert.AreEqual(0, state.PlayerCard.YahtzeeBonusCount);
            Assert.IsFalse(Single<ScoreCommitted>().YahtzeeBonusAwarded);
        }

        [Test]
        public void JokerRoll_ForcedUpperZero_StillAwardsBonus()
        {
            var state = new GameState();
            var card = state.PlayerCard;
            card.SetScore(Category.Yahtzee, 50);
            card.SetScore(Category.Fours, 12);
            card.SetScore(Category.ThreeOfAKind, 20);
            card.SetScore(Category.FourOfAKind, 22);
            card.SetScore(Category.FullHouse, 25);
            card.SetScore(Category.SmallStraight, 30);
            card.SetScore(Category.LargeStraight, 40);
            card.SetScore(Category.Chance, 21);
            var engine = Engine(state, 4, 4, 4, 4, 4);
            engine.Roll();

            CollectionAssert.AreEquivalent(
                new[] { Category.Aces, Category.Twos, Category.Threes, Category.Fives, Category.Sixes },
                engine.GetLegalCategories().ToArray());

            engine.ScoreCategory(Category.Sixes);
            Assert.AreEqual(0, card.GetScore(Category.Sixes));
            Assert.AreEqual(1, card.YahtzeeBonusCount);
        }

        [Test]
        public void NaturalYahtzee_IntoOpenYahtzeeBox_NoJokerNoBonus()
        {
            var engine = Engine(new GameState(), 4, 4, 4, 4, 4);
            engine.Roll();

            Assert.IsFalse(engine.IsJokerTurn);
            Assert.IsEmpty(_events.OfType<JokerActivated>());
            Assert.AreEqual(13, engine.GetLegalCategories().Count);

            engine.ScoreCategory(Category.Yahtzee);
            Assert.AreEqual(50, engine.State.PlayerCard.GetScore(Category.Yahtzee));
            Assert.AreEqual(0, engine.State.PlayerCard.YahtzeeBonusCount);
        }

        // ---- Upper bonus event ---------------------------------------------

        [Test]
        public void UpperBonus_EventFires_ExactlyOnCrossing()
        {
            var state = new GameState();
            var card = state.PlayerCard;
            card.SetScore(Category.Aces, 2);
            card.SetScore(Category.Twos, 10);
            card.SetScore(Category.Threes, 12);
            card.SetScore(Category.Fours, 16);
            card.SetScore(Category.Fives, 20); // upper = 60

            var engine = Engine(state, 6, 6, 6, 1, 2);
            engine.Roll();
            engine.ScoreCategory(Category.Sixes); // +18 → 78, crosses 63

            Assert.AreEqual(PlayerId.Player, Single<UpperBonusSecured>().Player);
            Assert.IsTrue(card.HasUpperBonus);
            Assert.AreEqual(78 + 35, card.Total);
        }

        [Test]
        public void UpperBonus_NoEvent_WhenNotCrossed_OrAlreadySecured()
        {
            var state = new GameState();
            state.PlayerCard.SetScore(Category.Aces, 2); // far from 63
            var engine = Engine(state, 2, 2, 3, 4, 5);
            engine.Roll();
            engine.ScoreCategory(Category.Twos); // upper = 6
            Assert.IsEmpty(_events.OfType<UpperBonusSecured>());

            // Already-secured card: another upper commit must not re-fire the event.
            var state2 = new GameState();
            var card2 = state2.PlayerCard;
            card2.SetScore(Category.Threes, 15);
            card2.SetScore(Category.Fours, 16);
            card2.SetScore(Category.Fives, 20);
            card2.SetScore(Category.Sixes, 24); // upper = 75, bonus already secured
            var engine2 = Engine(state2, 1, 1, 1, 2, 3);
            engine2.Roll();
            engine2.ScoreCategory(Category.Aces);
            Assert.IsEmpty(_events.OfType<UpperBonusSecured>());
        }

        // ---- Turn / round advance ------------------------------------------

        [Test]
        public void Commit_AdvancesPlayerThenRound_AndResetsDice()
        {
            var engine = Engine(new GameState(),
                1, 2, 3, 4, 5,  // player turn
                6, 6, 1, 1, 2); // oma turn
            engine.Roll();
            engine.ScoreCategory(Category.Chance);

            Assert.AreEqual(PlayerId.Oma, engine.State.CurrentPlayer);
            Assert.AreEqual(1, engine.State.Round);
            Assert.AreEqual(GamePhase.TurnStart, engine.State.Phase);
            Assert.AreEqual(0, engine.State.Dice.RollsUsed);
            Assert.IsTrue(engine.State.Dice.Kept.All(k => !k));
            var turn = Single<TurnChanged>();
            Assert.AreEqual(PlayerId.Oma, turn.Player);
            Assert.AreEqual(1, turn.Round);

            _events.Clear();
            engine.Roll();
            engine.ScoreCategory(Category.Sixes);
            Assert.AreEqual(12, engine.State.OmaCard.GetScore(Category.Sixes));
            Assert.AreEqual(PlayerId.Player, engine.State.CurrentPlayer);
            Assert.AreEqual(2, engine.State.Round);
        }
    }
}

using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Turns engine events into things Oma says (design §2).
    ///
    /// Reads the same `GameEvent` stream everything else does — no separate hooks into the rules,
    /// so she can never react to something that did not actually happen. Deliberately a plain C#
    /// class rather than a MonoBehaviour: it decides *what* she says, and the caller decides how
    /// to show it, which makes the whole trigger map testable without a scene.</summary>
    public sealed class DialogueService
    {
        /// <summary>Design §2: bubbles auto-dismiss after about this long.</summary>
        public const float BubbleSeconds = 3.5f;

        /// <summary>What counts as a hand worth remarking on. Below this she stays quiet, or she
        /// would be talking over every ordinary turn.</summary>
        private const int BigHandPoints = 25;

        private readonly OmaDialoguePicker _picker;
        private bool _omaWasAhead;
        private bool _leadKnown;

        public DialogueService(OmaDialoguePicker picker = null) => _picker = picker ?? new OmaDialoguePicker();

        /// <summary>New game: she may reuse lines, and the lead is unknown again.</summary>
        public string StartGame()
        {
            _picker.Reset();
            _leadKnown = false;
            return _picker.Next(OmaTrigger.GameStart);
        }

        /// <summary>The line for this event, or null when she has nothing to say.
        ///
        /// One line per event at most. The engine raises ScoreCommitted, then UpperBonusSecured,
        /// then TurnChanged, so when a score also secures the bonus the bigger news naturally
        /// replaces the smaller — which is exactly the "replace, don't queue" rule.</summary>
        public string React(GameEvent gameEvent, GameState state)
        {
            switch (gameEvent)
            {
                case ScoreCommitted committed:
                    return ReactToScore(committed, state);

                case UpperBonusSecured bonus:
                    return _picker.Next(bonus.Player == PlayerId.Player
                        ? OmaTrigger.UpperBonusPlayer
                        : OmaTrigger.UpperBonusOma);

                case TurnChanged turn:
                    // Only worth saying once, as the last round opens.
                    return turn.Round == GameState.TotalRounds && turn.Player == PlayerId.Player
                        ? _picker.Next(OmaTrigger.FinalRound)
                        : null;

                case GameEnded ended:
                    return _picker.Next(ended.Result switch
                    {
                        GameResult.PlayerWins => OmaTrigger.PlayerWins,
                        GameResult.OmaWins => OmaTrigger.OmaWins,
                        _ => OmaTrigger.Tie,
                    });

                default:
                    return null;
            }
        }

        private string ReactToScore(ScoreCommitted committed, GameState state)
        {
            // A lead change is the more interesting story, so it wins over a merely good score.
            string leadLine = LeadChangeLine(state);
            if (leadLine != null)
                return leadLine;

            bool player = committed.Player == PlayerId.Player;
            if (committed.Points == 0)
                return _picker.Next(player ? OmaTrigger.PlayerZero : OmaTrigger.OmaZero);
            if (committed.Points >= BigHandPoints || committed.YahtzeeBonusAwarded)
                return _picker.Next(player ? OmaTrigger.PlayerBigHand : OmaTrigger.OmaBigHand);
            return null;
        }

        /// <summary>Speaks only when the lead actually changes hands. The first score of a game
        /// establishes the baseline silently — otherwise she announces a "lead" over 0-0.</summary>
        private string LeadChangeLine(GameState state)
        {
            bool omaAhead = state.OmaCard.Total > state.PlayerCard.Total;
            bool tied = state.OmaCard.Total == state.PlayerCard.Total;

            if (!_leadKnown)
            {
                if (tied)
                    return null; // still nothing to be ahead of
                _leadKnown = true;
                _omaWasAhead = omaAhead;
                return _picker.Next(omaAhead ? OmaTrigger.OmaTakesLead : OmaTrigger.OmaLosesLead);
            }

            if (tied || omaAhead == _omaWasAhead)
                return null;

            _omaWasAhead = omaAhead;
            return _picker.Next(omaAhead ? OmaTrigger.OmaTakesLead : OmaTrigger.OmaLosesLead);
        }
    }
}

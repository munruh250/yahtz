namespace Yahtzee.Core
{
    /// <summary>What Oma would do with the player's dice, for the "Ask Oma" hint. Structured
    /// rather than pre-worded: the decision is Core's business, the wording is presentation's
    /// (and moves into the M5 dialogue set later).</summary>
    public sealed class KeepAdvice
    {
        /// <summary>Per-die keep flags she recommends. All true when <see cref="ScoreNow"/>.</summary>
        public bool[] Keep { get; }

        /// <summary>True when the hand is done — take <see cref="Target"/> rather than reroll,
        /// either because the rolls are spent or because nothing is worth chasing.</summary>
        public bool ScoreNow { get; }

        /// <summary>The box she is aiming at, or the one to take when <see cref="ScoreNow"/>.</summary>
        public Category Target { get; }

        /// <summary>What <see cref="Target"/> would score right now. Only meaningful when
        /// <see cref="ScoreNow"/> — mid-hand the target is a goal, not a settled score.</summary>
        public int TargetPoints { get; }

        /// <summary>How many dice she would throw back.</summary>
        public int RerollCount { get; }

        public KeepAdvice(bool[] keep, bool scoreNow, Category target, int targetPoints)
        {
            Keep = keep;
            ScoreNow = scoreNow;
            Target = target;
            TargetPoints = targetPoints;
            int rerolling = 0;
            foreach (bool k in keep)
                if (!k)
                    rerolling++;
            RerollCount = rerolling;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Yahtzee.Presentation
{
    /// <summary>The moments Oma has something to say (design §2, v1 trigger set).</summary>
    public enum OmaTrigger
    {
        GameStart,
        PlayerBigHand,
        PlayerZero,
        OmaBigHand,
        OmaZero,
        OmaTakesLead,
        OmaLosesLead,
        UpperBonusPlayer,
        UpperBonusOma,
        FinalRound,
        PlayerWins,
        OmaWins,
        Tie,
    }

    /// <summary>Oma's v1 lines.
    ///
    /// House rules from design §2, all enforced by `OmaDialogueTests`: at least three variants per
    /// trigger, nothing over 90 characters so it reads in a glance, and German only as flavour —
    /// endearments and exclamations whose meaning is obvious from tone. Nothing here is ever
    /// load-bearing for understanding the game, because the game is English-only.
    ///
    /// She teases but never sneers: she is losing to her grandchild half the time and delighted
    /// about it. Tiny Bubbles Sunshine is her bichon frisé.</summary>
    public static class OmaDialogueLines
    {
        public static readonly IReadOnlyDictionary<OmaTrigger, string[]> Default =
            new Dictionary<OmaTrigger, string[]>
            {
                [OmaTrigger.GameStart] = new[]
                {
                    "Sit, Schatz. I have the good dice and a fresh Streuselkuchen.",
                    "Ah, you came! Tiny Bubbles Sunshine has watched the door all evening.",
                    "Thirteen rounds. Try not to let an old woman embarrass you, ja?",
                    "Sit down, Liebling. The kettle is on and the dice are warm.",
                },
                [OmaTrigger.PlayerBigHand] = new[]
                {
                    "Ach du lieber! Where did you learn to roll like that?",
                    "Wunderbar, Schatz. Do that again and I check those dice.",
                    "Well! That is the sort of roll I usually keep for myself.",
                },
                [OmaTrigger.PlayerZero] = new[]
                {
                    "Ach, Schatz. Even my Apfelstrudel collapses sometimes.",
                    "A zero. Na gut - the dice owe you one now.",
                    "Oh, Liebling. Come, have a biscuit and forget it.",
                },
                [OmaTrigger.OmaBigHand] = new[]
                {
                    "Ha! Did you see that? Sixty years of practice, Schatz.",
                    "Wunderbar! Tiny Bubbles Sunshine, did you see what Oma did?",
                    "Ach, lucky old woman. Lucky, lucky old woman.",
                },
                [OmaTrigger.OmaZero] = new[]
                {
                    "Ach du lieber. Do not look at me like that.",
                    "A zero. At my age! Fetch the schnapps, Schatz.",
                    "Na gut. Even Oma burns the Lebkuchen now and then.",
                },
                [OmaTrigger.OmaTakesLead] = new[]
                {
                    "I am ahead now, Schatz. Only a little. Only a lot.",
                    "Ah - the lead. It suits me, ja?",
                    "Do not worry, Liebling. I shall be gentle when I win.",
                },
                [OmaTrigger.OmaLosesLead] = new[]
                {
                    "You are ahead? Hmph. Enjoy it while it lasts, Schatz.",
                    "Ach! Fine. The dice like you today.",
                    "So. The little one has teeth. Wunderbar.",
                },
                [OmaTrigger.UpperBonusPlayer] = new[]
                {
                    "The bonus! Thirty-five, just like that. Clever, Schatz.",
                    "Ha! You got the sixty-three. I taught you too well.",
                    "Wunderbar, Liebling. That bonus was mine to lose.",
                },
                [OmaTrigger.UpperBonusOma] = new[]
                {
                    "Sixty-three! The bonus is mine, Liebling.",
                    "Ah, the thirty-five. I kept room for it all evening.",
                    "The bonus, Schatz. An old woman plans ahead.",
                },
                [OmaTrigger.FinalRound] = new[]
                {
                    "Last round, Schatz. Make it count.",
                    "One more. Then Kaffee und Kuchen, win or lose.",
                    "The final box, Liebling. No pressure. Some pressure.",
                },
                [OmaTrigger.PlayerWins] = new[]
                {
                    "You beat me! Ach, wunderbar. Come here, let me look at you.",
                    "The winner! I shall tell everyone. Even Tiny Bubbles Sunshine.",
                    "Beaten by my own grandchild. I could not be happier, Schatz.",
                },
                [OmaTrigger.OmaWins] = new[]
                {
                    "I win, Schatz. Do not sulk - there is cake.",
                    "Ha! Sixty years of practice. Again tomorrow, ja?",
                    "Oma wins. Somebody has to, Liebling.",
                },
                [OmaTrigger.Tie] = new[]
                {
                    "A tie! Nobody washes up. Those are the rules.",
                    "Even, Schatz. The dice could not choose between us.",
                    "A draw. Wunderbar - now we must play again.",
                },
            };
    }

    /// <summary>Picks a line for a trigger, avoiding repeats within a game (design §2).
    ///
    /// Uses its own RNG, never the game's: the engine RNG is seeded and its draw count is saved,
    /// so drawing from it here would change the dice a player sees depending on what Oma happened
    /// to say.</summary>
    public sealed class OmaDialoguePicker
    {
        private readonly IReadOnlyDictionary<OmaTrigger, string[]> _lines;
        private readonly Random _rng;
        private readonly Dictionary<OmaTrigger, List<int>> _unused = new Dictionary<OmaTrigger, List<int>>();

        public OmaDialoguePicker(IReadOnlyDictionary<OmaTrigger, string[]> lines = null, Random rng = null)
        {
            _lines = lines ?? OmaDialogueLines.Default;
            _rng = rng ?? new Random();
        }

        /// <summary>A fresh game means she may repeat lines she used in the last one.</summary>
        public void Reset() => _unused.Clear();

        /// <summary>A line for <paramref name="trigger"/>, or null if it has none. Every variant is
        /// used once before any repeats.</summary>
        public string Next(OmaTrigger trigger)
        {
            if (!_lines.TryGetValue(trigger, out var variants) || variants.Length == 0)
                return null;

            if (!_unused.TryGetValue(trigger, out var pool) || pool.Count == 0)
            {
                pool = new List<int>(variants.Length);
                for (int i = 0; i < variants.Length; i++)
                    pool.Add(i);
                _unused[trigger] = pool;
            }

            int slot = _rng.Next(pool.Count);
            int index = pool[slot];
            pool.RemoveAt(slot);
            return variants[index];
        }
    }
}

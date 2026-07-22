using System.Text;
using Yahtzee.Core;

namespace Yahtzee.Presentation
{
    /// <summary>Turns Oma's structured <see cref="KeepAdvice"/> into something she would actually
    /// say. The advice itself is always plain English — design spec: German is flavour and is
    /// never rules-critical — with a warm sign-off appended.
    ///
    /// Interim home: M5 moves the line pool into an OmaDialogueSet ScriptableObject with proper
    /// per-game no-repeat selection. The rotation here is the same idea, in code.</summary>
    public static class OmaHints
    {
        /// <summary>Longer than the ~3.5 s of a reaction bubble (design §5.2) because this one is
        /// a sentence the player asked for and has to read.</summary>
        public const float BubbleSeconds = 7f;

        /// <summary>Flavour only — never carries information the player needs.</summary>
        private static readonly string[] Flavour =
        {
            "Tiny Bubbles Sunshine agrees, and she is never wrong about dice.",
            "Guter Teig braucht Zeit, Schatz. So do good dice.",
            "Ach du lieber, this is more nerve-wracking than my Streuselkuchen.",
            "I once let an Apfelstrudel burn waiting for all five to match. Worth it.",
            "Tiny Bubbles Sunshine is asleep on my slipper. She believes in you.",
            "Patience, Liebling. Even the Hefeteig needs time to rise.",
            "My mother played it this way, and she never once apologised for it.",
            "Trust the dice, Schatz. And always trust a second helping of Lebkuchen.",
            "Tiny Bubbles Sunshine wants a walk. Roll quickly and we both win.",
            "Na ja. Not every batch of Pfannkuchen comes out perfect either.",
        };

        private static readonly System.Random Rng = new System.Random();
        private static int _lastFlavour = -1;

        /// <summary>Oma's answer to "what would you do?" — the advice, then a flavour line.
        /// <paramref name="diceValues"/> is the current roll, in the same order as
        /// <see cref="KeepAdvice.Keep"/>.</summary>
        public static string Compose(KeepAdvice advice, int[] diceValues)
        {
            var sb = new StringBuilder();
            if (advice.ScoreNow)
            {
                sb.Append(advice.TargetPoints > 0
                    ? $"I would take {UiBuilder.DisplayName(advice.Target)} for {advice.TargetPoints}, Schatz."
                    : $"Nothing is falling your way, Schatz. Put the zero in {UiBuilder.DisplayName(advice.Target)}.");
            }
            else
            {
                sb.Append($"Keep {Describe(advice.Keep, diceValues)}, then roll the other ");
                sb.Append(advice.RerollCount == 1 ? "one." : $"{Number(advice.RerollCount)}.");
                sb.Append($" That is your best shot at {UiBuilder.DisplayName(advice.Target)}.");
            }
            sb.Append("\n\n");
            sb.Append(NextFlavour());
            return sb.ToString();
        }

        /// <summary>The kept dice in words: "two 5s and a 4". Faces, not positions — dice showing
        /// the same value are interchangeable, so naming the value is what the player needs.</summary>
        private static string Describe(bool[] keep, int[] diceValues)
        {
            var counts = new int[7];
            for (int i = 0; i < keep.Length; i++)
                if (keep[i])
                    counts[diceValues[i]]++;

            var parts = new System.Collections.Generic.List<string>();
            for (int face = 6; face >= 1; face--)
            {
                if (counts[face] == 0)
                    continue;
                parts.Add(counts[face] == 1 ? $"the {face}" : $"{Number(counts[face])} {face}s");
            }

            if (parts.Count == 0)
                return "nothing";
            if (parts.Count == 1)
                return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        private static string Number(int n) => n switch
        {
            1 => "one",
            2 => "two",
            3 => "three",
            4 => "four",
            5 => "five",
            _ => n.ToString(),
        };

        /// <summary>Never the same line twice running — the M5 dialogue set does this properly
        /// across a whole game.</summary>
        private static string NextFlavour()
        {
            if (Flavour.Length == 1)
                return Flavour[0];
            int index;
            do
            {
                index = Rng.Next(Flavour.Length);
            } while (index == _lastFlavour);
            _lastFlavour = index;
            return Flavour[index];
        }
    }
}

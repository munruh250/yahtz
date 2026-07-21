using System;
using UnityEngine;

namespace Yahtzee.Services
{
    /// <summary>Player preferences and unlocks. PlayerPrefs rather than the save file: these
    /// outlive any single game, and wiping a game must not wipe what you own.
    ///
    /// Deliberately not in Core — none of it is a rule. Core stays a pure rules engine; the
    /// difficulty setting is applied by the controller when it constructs Oma.</summary>
    public static class GameSettings
    {
        /// <summary>Maps onto OmaAI's sloppiness knob: how often she takes her second-best box
        /// instead of her best. She never cheats in the other direction — "Sharp" is simply her
        /// playing her own heuristic straight.</summary>
        public enum Difficulty
        {
            Gentle = 0,
            Normal = 1,
            Sharp = 2,
        }

        public const string ClassicDice = "classic";
        public const string RubyDice = "ruby";

        private const string DifficultyKey = "yz.difficulty";
        private const string DiceKey = "yz.dice";
        private const string OwnedPrefix = "yz.owns.";

        public static event Action Changed;

        public static Difficulty SelectedDifficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
            set
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>How often Oma takes her second choice. Gentle gives the player room; Sharp is
        /// her undiluted heuristic (and stays perfectly deterministic).</summary>
        public static double SloppinessFor(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Gentle => 0.30,
            Difficulty.Normal => 0.10,
            _ => 0.0,
        };

        public static string SelectedDice
        {
            get
            {
                string id = PlayerPrefs.GetString(DiceKey, ClassicDice);
                return Owns(id) ? id : ClassicDice; // never leave the player with dice they lost
            }
            set
            {
                PlayerPrefs.SetString(DiceKey, value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The classic set is always yours; everything else has to be unlocked.</summary>
        public static bool Owns(string diceId) =>
            diceId == ClassicDice || PlayerPrefs.GetInt(OwnedPrefix + diceId, 0) == 1;

        public static void Grant(string diceId)
        {
            PlayerPrefs.SetInt(OwnedPrefix + diceId, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Tests only — leaves no preferences behind for the next fixture.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(DifficultyKey);
            PlayerPrefs.DeleteKey(DiceKey);
            PlayerPrefs.DeleteKey(OwnedPrefix + RubyDice);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

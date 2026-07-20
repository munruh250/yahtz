using System;
using System.IO;
using UnityEngine;
using Yahtzee.Core;

namespace Yahtzee.Services
{
    /// <summary>Persists the single <see cref="GameState"/> object as JSON. M1 decision:
    /// JsonUtility (not Newtonsoft) — GameState was designed for it (int-array slots with a
    /// -1 open sentinel, no nullables/dictionaries), proven by the round-trip tests.
    /// Presentation calls Save on ScoreCommitted/TurnChanged and OnApplicationPause.</summary>
    public static class SaveService
    {
        public const string FileName = "save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static string ToJson(GameState state) => JsonUtility.ToJson(state);

        /// <summary>Deserialize, or null if the JSON is unusable or from another save version.</summary>
        public static GameState FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                var state = JsonUtility.FromJson<GameState>(json);
                if (state == null || state.SaveVersion != GameState.CurrentSaveVersion)
                    return null;
                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(GameState state) => File.WriteAllText(SavePath, ToJson(state));

        /// <summary>The saved game, or null if none exists or it is invalid.</summary>
        public static GameState TryLoad() =>
            File.Exists(SavePath) ? FromJson(File.ReadAllText(SavePath)) : null;

        /// <summary>True if a valid, unfinished game is on disk (title-screen Continue).</summary>
        public static bool HasResumableSave()
        {
            var state = TryLoad();
            return state != null && state.Phase != GamePhase.GameOver;
        }

        public static void Delete()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
    }
}

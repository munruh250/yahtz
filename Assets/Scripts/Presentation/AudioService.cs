using System.Collections.Generic;
using UnityEngine;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>All the game's sound (design §5.4). Clips are loaded by name from
    /// <c>Resources/Audio/</c>, so the game ships silent and comes alive the moment the owner
    /// drops the files in (same drop-in-by-convention pattern as the font) — no prefab wiring, no
    /// inspector references to lose. A missing clip is simply not played.
    ///
    /// Everything is gated by <see cref="GameSettings.SoundEnabled"/>, the global mute.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        /// <summary>Every sound the game asks for. The enum name is the file name it loads
        /// (Resources/Audio/&lt;name&gt;.wav|ogg|mp3) — see Docs/AUDIO_ASSETS.md.</summary>
        public enum Sfx
        {
            DiceRoll,        // dice leave the cup / tumble
            Keep,            // a die is tapped to keep or release
            Score,          // a box is locked in
            FanfareFiveKind, // five of a kind — the big one
            FanfareStraight, // large straight / full house — medium
            FanfareBonus,    // upper bonus secured — medium
            WinSting,        // game won
            LoseSting,       // game lost
        }

        private const string Music = "Music"; // Resources/Audio/Music.* — the background loop

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource _sfxSource;
        private AudioSource _musicSource;

        public void Init()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.volume = 0.5f; // background: sits under the SFX

            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Resources.Load<AudioClip>($"Audio/{sfx}");
                if (clip != null)
                    _clips[sfx] = clip;
            }

            GameSettings.Changed += ApplyMute;
            ApplyMute();
        }

        private void OnDestroy() => GameSettings.Changed -= ApplyMute;

        /// <summary>Play a one-shot. No-op when muted or when that clip has not been supplied.</summary>
        public void Play(Sfx sfx)
        {
            if (!GameSettings.SoundEnabled || _sfxSource == null)
                return;
            if (_clips.TryGetValue(sfx, out var clip))
                _sfxSource.PlayOneShot(clip);
        }

        /// <summary>Start the background loop if a Music clip was supplied and sound is on.</summary>
        public void StartMusic()
        {
            if (_musicSource == null)
                return;
            if (_musicSource.clip == null)
                _musicSource.clip = Resources.Load<AudioClip>($"Audio/{Music}");
            if (_musicSource.clip != null && GameSettings.SoundEnabled && !_musicSource.isPlaying)
                _musicSource.Play();
        }

        private void ApplyMute()
        {
            bool on = GameSettings.SoundEnabled;
            if (_musicSource != null)
            {
                if (!on) _musicSource.Pause();
                else if (_musicSource.clip != null && !_musicSource.isPlaying) _musicSource.Play();
            }
        }
    }
}

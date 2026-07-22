using NUnit.Framework;
using UnityEngine;
using Yahtzee.Presentation;
using Yahtzee.Services;

namespace Yahtzee.Tests
{
    /// <summary>Sound/haptics settings and the ship-silent contract: with no clips supplied, the
    /// audio service must be a safe no-op, never throwing mid-turn. The clips are dropped in later
    /// (Docs/AUDIO_ASSETS.md); until then the game plays fine, just silently.</summary>
    public class AudioHapticsTests
    {
        [SetUp]
        public void SetUp() => GameSettings.ResetAll();

        [TearDown]
        public void TearDown() => GameSettings.ResetAll();

        [Test]
        public void SoundAndHaptics_DefaultOn_AndPersist()
        {
            Assert.IsTrue(GameSettings.SoundEnabled, "sound should default on");
            Assert.IsTrue(GameSettings.HapticsEnabled, "haptics should default on");

            GameSettings.SoundEnabled = false;
            GameSettings.HapticsEnabled = false;
            Assert.IsFalse(GameSettings.SoundEnabled);
            Assert.IsFalse(GameSettings.HapticsEnabled);
        }

        [Test]
        public void AudioService_WithNoClips_IsASafeNoOp()
        {
            var go = new GameObject("audio");
            try
            {
                var audio = go.AddComponent<AudioService>();
                audio.Init(); // no Resources/Audio clips in a test project

                foreach (AudioService.Sfx sfx in System.Enum.GetValues(typeof(AudioService.Sfx)))
                    Assert.DoesNotThrow(() => audio.Play(sfx), $"playing {sfx} silently must not throw");

                Assert.DoesNotThrow(audio.StartMusic);

                GameSettings.SoundEnabled = false;
                Assert.DoesNotThrow(() => audio.Play(AudioService.Sfx.DiceRoll), "muted play must not throw");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Haptics_RespectTheToggle_AndNeverThrow()
        {
            GameSettings.HapticsEnabled = false;
            Assert.DoesNotThrow(HapticsService.Light, "disabled haptics must be a silent no-op");
            Assert.DoesNotThrow(HapticsService.Success);

            GameSettings.HapticsEnabled = true;
            // In-editor this falls through to the fallback path; it must be wrapped so a device
            // with no vibrator can't crash a turn.
            Assert.DoesNotThrow(HapticsService.Light);
            Assert.DoesNotThrow(HapticsService.Success);
        }
    }
}

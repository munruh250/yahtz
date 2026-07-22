# Audio & haptics — assets to plug in

The audio and haptics systems are built and wired; the game currently ships **silent** because no
sound files exist yet. Drop the clips below into **`Assets/Resources/Audio/`** with the exact file
names and they load automatically at startup — no prefab or inspector work (same drop-in pattern as
the font). A missing clip is simply skipped, so you can add them one at a time.

Both are toggleable in **Settings → Sound & feel** ("Sound" mutes all audio, "Haptics" the
vibration), stored in PlayerPrefs, on by default.

## Sound clips

Format: `.wav` preferred (also `.ogg` / `.mp3`). Keep SFX short and punchy. Import SFX as
*Decompress on Load*; import `Music` as *Streaming* with **Loop** ticked.

| File (in `Resources/Audio/`) | Plays when | Feel | Length |
|---|---|---|---|
| `DiceRoll.wav` | dice leave the cup and tumble | woody rattle + a couple of table taps | ~0.6–1.0 s |
| `Keep.wav` | a die is tapped to keep / release | soft, dry click | ~0.1 s |
| `Score.wav` | a box is locked in | gentle pencil-scratch / stamp | ~0.2 s |
| `FanfareFiveKind.wav` | **player** rolls five of a kind | the big celebratory sting (bells / "ta-da!") | ~1.2–1.8 s |
| `FanfareStraight.wav` | **player** scores large straight or full house | medium happy flourish | ~0.8–1.2 s |
| `FanfareBonus.wav` | **player** secures the upper +35 bonus | medium chime / "nice!" | ~0.8 s |
| `WinSting.wav` | game won (also a tie) | warm, triumphant | ~1.5 s |
| `LoseSting.wav` | game lost | soft, good-natured "aw" — Oma is kind | ~1.2 s |
| `Music.wav` | background, whole session | cozy, low-key kitchen loop; **must loop seamlessly** | 30–90 s loop |

Notes:
- Fanfares and the win/lose stings fire for the **player's** achievements only — a stinger when
  Oma scores would feel like the game cheering against you (she gets her clap animation instead).
- The background loop sits at half volume under the SFX; master it a touch quiet.

## Haptics (no files — built in)

Vibration is generated on-device (Android Vibrator API, with a coarse fallback elsewhere), gated by
the Haptics toggle and the phone's own vibration setting. Two patterns in v1:

| Trigger | Pattern |
|---|---|
| Tap a die to keep / release | a light ~18 ms tick |
| **Player** rolls five of a kind | a celebratory double-buzz (40 ms, gap, 120 ms) |

A `Medium()` pattern exists in `HapticsService` (reserved for score lock-in) if you want more feel
later — say the word and I'll wire it.

## Where it's wired

- `AudioService` (`Assets/Scripts/Presentation/AudioService.cs`) — loads and plays the clips.
- `HapticsService` (`.../HapticsService.cs`) — the vibration patterns.
- `GameController` calls both off the same `GameEvent`s the rest of the UI reacts to, plus the dice
  tap. `GameSettings.SoundEnabled` / `HapticsEnabled` are the toggles.

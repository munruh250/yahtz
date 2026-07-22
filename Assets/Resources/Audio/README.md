Drop the game's sound files here and they wire up automatically — no prefab or inspector work.

AudioService loads each clip by name at startup: `Resources/Audio/<Name>.wav` (or .ogg / .mp3).
A missing clip is simply not played, so the game ships and plays fine silent until these exist.

Required file names (see Docs/AUDIO_ASSETS.md for what each is and length guidance):

  DiceRoll.wav          dice leave the cup and tumble
  Keep.wav              a die is tapped to keep / release
  Score.wav             a box is locked in
  FanfareFiveKind.wav   five of a kind — the big celebratory sting
  FanfareStraight.wav   large straight / full house — medium
  FanfareBonus.wav      upper +35 bonus secured — medium
  WinSting.wav          game won (also plays on a tie)
  LoseSting.wav         game lost
  Music.wav             background loop (set the import to Loop; keep it soft)

Import tips: short SFX as "Decompress on Load", the music loop as "Streaming".

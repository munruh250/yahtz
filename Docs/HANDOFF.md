# Handoff — Yahtzee with Oma

**Last updated:** 2026-07-21
**Status:** M1–M3 complete, **M4 ~90%** (only the real art pass is left), **M5 dialogue done**, **M6 screens done** (Title/Home/Settings/Store/HowToPlay/Results + pause menu) and **Play build hardened**. The one thing between here and a store submission is the **real art pass** and the **Play Console/legal work** in [`LAUNCH.md`](LAUNCH.md).
**Test baseline (must stay green):** EditMode **111**, PlayMode **24**. Run them with `Tools\run-tests.ps1` — no need to close the editor.
**Next task:** the art pass (M4/M5), then audio/haptics (M6), then work [`LAUNCH.md`](LAUNCH.md).

---

## 1. Read first

`CLAUDE.md` (auto-loaded) has the non-negotiable architecture rules. `Docs/DESIGN_SPEC.md` and `Docs/TECH_PLAN.md` are authoritative for rules/UX and architecture/milestones respectively. This file is the current-state snapshot: what exists, what's decided, what's left.

**The one rule that matters most:** `Assets/Scripts/Core` has `noEngineReferences: true`. All rules/scoring/AI live there as pure C#; MonoBehaviours only render state and forward input; `GameController` is the sole caller of engine mutators; core→presentation is `GameEvent`s only.

---

## 2. Environment (this machine)

**The primary Unity install is corrupt.** `C:\Program Files\Unity\Hub\Editor\2022.3.62f3` is missing `PackageManager\Server` (batch mode exits 1 immediately). Use the intact sibling install — same version — for everything:

```
C:\Program Files\Unity\Hub\Editor\2022.3.62f3-x86_64\Editor\Unity.exe
```

Worth repairing via Unity Hub eventually; not blocking.

**Android device builds.** Build Support + SDK/NDK/OpenJDK are installed under the `-x86_64` editor; `adb` lives at `Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` and is the fastest way to triage a device that Unity won't list. Target device is a **Pixel 10 Pro** (ARM64). Two traps already hit:

- *"No Android devices connected"* with the phone plugged in — check `adb devices` against `Get-PnpDevice`. Windows showing the phone as class **WPD** while adb lists **nothing** means USB debugging is off (Developer options, *not* the USB-preferences menu — USB tethering is unrelated and shares mobile data instead). Serial listed as `unauthorized` instead means the on-phone RSA prompt was dismissed.
- *"trying to install ARMv7 APK to ARM64 device"* — Unity's Android defaults are Mono + ARMv7. `SceneBootstrapper.ConfigurePlayerSettings` now forces IL2CPP + ARM64 per CLAUDE.md (ARM64 is only offered under IL2CPP, so the backend must be set first). Re-run `Yahtzee/Setup Project` if the settings ever drift back.

### Build-only bugs: the suite cannot see them

EditMode and PlayMode run **inside the editor**, where nothing is stripped and every shader and component type is loaded. A green suite says nothing about a device build. This has already shipped one total failure: `GameController.Awake` threw, so the entire 3D scene, dice and scorecard were absent while the screen-space UI still drew.

**The trap that caused it:** with `stripEngineCode: 1` (the Android default), IL2CPP removes engine types the managed code never *names*. Nothing referenced `CapsuleCollider` — the collider Unity puts on a `Cylinder` primitive — so on device `CreatePrimitive(PrimitiveType.Cylinder)` came back with **no collider at all**, and `GetComponent<Collider>().material` threw. Null-check any component you did not add yourself, and remember that naming a type only in a comment does not keep it.

`Assets/link.xml` now preserves **`CapsuleCollider`** for exactly this reason: `CreatePrimitive(Cylinder)` attaches one to the cup, mug, both pencils and the five keep markers, so with it stripped every launch logged *"Can't add component because class 'CapsuleCollider' doesn't exist!"* five times over. Add a `<type>` entry there for any engine class only Unity itself references.

**A desktop player is not a proxy.** Stripping is per-platform: a Windows build booted perfectly clean on the exact code that crashed on the phone. Believe nothing about the device that was not run on the device. `device-smoke.ps1` fails on **any** `E/Unity` line — a clean launch produces none, and an earlier laxer filter that only looked for "Exception" sailed straight past the CapsuleCollider spam.

```powershell
Tools\device-smoke.ps1 -ListDevices   # is a device attached and authorised?
Tools\device-smoke.ps1                # build APK, install, launch, fail on any logcat exception
Tools\device-smoke.ps1 -SkipBuild     # re-launch and re-scan what is already installed
```

Manual triage with `adb` (path in the traps above):

```powershell
adb logcat -c; adb shell am force-stop com.DefaultCompany.yahtzee
adb shell monkey -p com.DefaultCompany.yahtzee -c android.intent.category.LAUNCHER 1
adb logcat -d -v brief > log.txt      # then grep E/Unity
adb shell screencap -p /sdcard/s.png; adb pull /sdcard/s.png shot.png
```

Two gotchas that cost time: Unity stack traces in logcat print **innermost frame first**, so the exception type and the frame that actually threw are at the *top* — a paste that starts mid-stack hides the answer. And never redirect `adb exec-out screencap -p` with PowerShell `>`; it adds a BOM and corrupts the PNG. Capture on device and `pull`.

### Running tests without closing the editor

**Unity allows one instance per project path**, so a headless run against the working copy dies with `HandleProjectAlreadyOpenInAnotherInstance` whenever the editor is open. Don't ask the owner to close it — use the testbed:

```powershell
Tools\run-tests.ps1                                     # PlayMode (~100s, dice soak dominates)
Tools\run-tests.ps1 -Platform EditMode                  # EditMode
Tools\run-tests.ps1 -Filter Yahtzee.Tests.DiceTapTests  # one fixture
```

It mirrors `Assets`/`Packages`/`ProjectSettings` to **`C:\yz-test`** and runs there, printing the pass/fail tally, any failure messages and each test's own output. `Library` is not mirrored (it regenerates; copying a live one risks a torn state), so the first run after a wipe is slow and later ones are normal speed. Framing PNGs land under the testbed's persistent data path.

**Mirror direction is one-way**, so anything Unity *generates* stays in the testbed. In practice that means new scripts get their `.meta` files written at `C:\yz-test\...`, not in the repo — copy them back before committing, or the next person to open the editor gets fresh GUIDs for files that were already tested.

### Headless commands (direct — needs the editor closed)

```powershell
$u = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3-x86_64\Editor\Unity.exe"
$p = "C:\Users\marku\Desktop\yahtzee\yahtzee"

# Tests (EditMode ~1s, PlayMode ~100s — the dice soak dominates)
& $u -batchmode -projectPath $p -runTests -testPlatform EditMode -testResults out.xml -logFile log.txt
& $u -batchmode -projectPath $p -runTests -testPlatform PlayMode -testResults out.xml -logFile log.txt
# add -testFilter "Yahtzee.Tests.SomeFixture" to narrow

# Editor tools (also available as Yahtzee/* menu items)
& $u -batchmode -projectPath $p -quit -logFile log.txt -executeMethod Yahtzee.EditorTools.SceneBootstrapper.SetupProject
& $u -batchmode -projectPath $p -quit -logFile log.txt -executeMethod Yahtzee.EditorTools.OmaAssetTool.Setup
& $u -batchmode -projectPath $p -quit -logFile log.txt -executeMethod Yahtzee.EditorTools.OmaSimulationTool.Run10k
```

Results XML parses with `[xml]$r = Get-Content out.xml; $r."test-run"` → `total/passed/failed`.

**Fast core iteration without Unity:** the Core asmdef is plain C#, so it compiles standalone with Unity's bundled Roslyn (`Editor\Data\DotNetSdkRoslyn\csc.dll` via `Editor\Data\NetCoreRuntime\dotnet.exe`, referencing `NetCoreRuntime\shared\Microsoft.NETCore.App\6.0.21\*.dll`, `-langversion:9.0 -nostdlib`). This is how the 10k-game AI tuning ran in seconds instead of minutes — worth rebuilding if you touch `OmaAI` weights.

---

## 3. What exists

### Core (`Assets/Scripts/Core`, asmdef `Yahtzee.Core`, no UnityEngine)

| File | Contents |
|---|---|
| `Scoring/Category.cs` | 13-box enum + `IsUpper`/`UpperFace`/`UpperCategoryForFace` |
| `Scoring/ScoreCalculator.cs` | Stateless scoring; histogram + straight bitmask |
| `Scoring/Scorecard.cs` | 13 int slots (`-1` = open), Yahtzee-bonus count, subtotals/bonus/total |
| `Scoring/JokerRules.cs` | Situation detection, +100 eligibility, forced-priority legal set, wildcard values |
| `Dice/DiceState.cs`, `Dice/IRandomSource.cs`, `Dice/SeededRandomSource.cs` | 5 values + keep flags + rollsUsed; injected RNG (seed + draws-to-skip) |
| `Game/GameState.cs` | The save file: both cards, round, turn, phase, dice, seed, RngDraws, SaveVersion |
| `Game/GameEvent.cs` | `DiceRolled`, `JokerActivated`, `ScoreCommitted`, `UpperBonusSecured`, `TurnChanged`, `GameEnded` |
| `Game/GameEngine.cs` | Sole mutator. Throws on illegal actions. `Roll`/`SetKeep`/`GetLegalCategories`/`GetPotentialScores`/`ScoreCategory`, `NewGame(seed)`/`FromState(state)` |
| `AI/OmaAI.cs` | `DecideKeepers` (32 subsets), `DecideCategory`, `RankForHint` (player hints, same valuation), `Advise` (**Ask Oma** — same evaluator, returns a `KeepAdvice`) |
| `AI/KeepAdvice.cs` | Structured hint: keep flags, reroll count, whether to score now, the box she is aiming at. Wording lives in presentation |

### Services (`Assets/Scripts/Services`, asmdef `Yahtzee.Services`)

`SaveService` — JsonUtility → `persistentDataPath/save.json`, version int, `TryLoad`/`HasResumableSave`/`Delete`.

`GameSettings` — PlayerPrefs, not the save file: difficulty, chosen dice skin and what you own all outlive any single game, and wiping a game must not wipe what you bought. Raises `Changed` so the table re-tints live.

**Difficulty maps to `OmaAI`'s sloppiness knob** (Gentle 0.30 / Normal 0.10 / Sharp 0.0 — how often she takes her second-best box). ⚠️ **Pre-ship caveat:** the ε draws are *not* persisted the way `RngDraws` is, so resuming a saved game mid-way can diverge from an uninterrupted one at any difficulty above Sharp. Skip is unaffected — ε is only consumed in `DecideCategory`, once per turn. Fix before release by persisting the ε draw count alongside `RngDraws`.

### Presentation (`Assets/Scripts/Presentation`, asmdef `Yahtzee.Presentation`)

- **`GameController`** — owns engine, subscribes to events, sequences everything. Input entry points: `OnRollTapped`, `OnDieTapped`, `OnCellTapped`, `OnSkipTapped`, `OnPeekTapped`, `OnNewGameTapped`. Two static flags: **`AnimationsEnabled`** (tests set false → everything instant) and **`Use3dDice`** (false → the 2D layer, kept per TECH_PLAN §7).
- **`IDiceView`** — `SetDice` / `PlayRoll` / `SetInteractable` / `SkipAnimation`. Two implementations:
  - `DiceCupView` — the cup lifts, tips over the play area and spills the dice from its mouth (design §5.4). Pours from *above* the fence, so the walls had to grow to contain it.
  - `DiceView3D` + `Die3D` — physics dice, **engine value decided first**, guided settle, watchdog snap, raycast tap-to-keep. Kept dice move to the keep row *and* light a gold pad (design §5.5 forbids colour-only signalling); rolled dice are penned into the roll zone so values stay trackable.
  - `DiceView2D` + `DieView2D` — sprite fallback behind the debug flag.
- **`KitchenBuilder`** — builds the whole gray-box scene in code (table, fence, lamp + fill light, props, dice, the diegetic scorecard, Oma, camera framings). Real art replaces primitives here.
- **`OmaView`** — idle rotation (idle/shift/talking, 5–12 s) + `PlayReaction(Clap|Disbelief)`.
- **`ScorecardBuilder`** — the one grid builder for both layers, styled after the real Yahtzee pad: white stock, black ink, grey UPPER/LOWER section bands, hairline rules, and the printed hints (`=1`…`=6`, and 25/30/40/50 on the fixed boxes). No repeat game columns — this card only ever tracks one game. `BuildInto(rect, …)` fills any RectTransform with the 13 boxes + title + bonus row; `BuildWorld(…)` wraps that in a **world-space canvas** lying on the table, propped 24° at the player, on a backing board. Card geometry (size, tilt, z) is a block of named constants at the top of the file — tune there, then re-run the framing renders.
- **`SpeechBubbleView`** — Oma's bubble: replace-don't-queue, auto-dismiss, and **nothing in it is a raycast target** so it can never eat a tap (design §5.2, asserted by `AskOmaTests`). The component sits on an always-active holder, not on the panel it hides, or its `Update` would never run. M5's `DialogueService` should drive this API unchanged.
- **`ScreensView` / `ScreensBuilder` / `ResultsView`** — the front-end: Title ("Dice with Oma") → Home → Settings / Store / How to Play, plus the end-of-game **Results** screen (winner banner, Oma's closing line, a You/Oma comparison table for all 13 boxes + total, Play Again / Home). Full-screen and opaque, so nothing behind them is tappable — which is also how a paused game freezes. The **hamburger opens Home mid-game**, which carries the full pause menu (Resume/Restart/How to Play/Settings/Store + a corner Title). Settings and Store *function* (difficulty and dice skins persist, take effect live); the old HudView game-over panel is **gone** — Results replaced it.
- **`UiPalette`** — the whole game's colour, **named by role, not hue** (`Accent`, `Chrome`, `Paper`, `Ink`), so a re-tint never leaves a field called `Gold` holding a blue. Cozy/cartoony direction: soft periwinkle and lilac against warm cream, ink a deep indigo. Nothing fully saturated, nothing pure black or white.
- **`UiSprites`** — rounded-rect sprites generated at runtime and 9-sliced, so every panel, button and tile has soft corners at any size. `UiBuilder.Image` applies one by default; `UiBuilder.Fill` is the square-cornered escape hatch for full-bleed backdrops, hairline rules and progress fills. Hard rectangles were most of what made the old chrome read as a spreadsheet.
- **`DiceSkins`** — cosmetic dice colours (Classic, Ruby). The engine decides values and physics is theatre, so a skin can never touch gameplay. `DiceView3D.ApplySkin` re-tints the two shared materials, so a change repaints all five dice at once.
- **`DialogueService` / `OmaDialogue`** — Oma's reactions. Reads the same `GameEvent` stream everything else does, so she can never react to something that did not happen. `DialogueService` is a plain C# class deciding *what* she says (the caller shows it), which makes the whole trigger map testable without a scene. `OmaDialoguePicker` uses every variant before repeating and resets each game. **Its RNG is its own, never the engine's** — drawing from the seeded stream would change the dice a player sees depending on what Oma happened to say.
- **`OmaHints`** — wording for `KeepAdvice`: advice always in plain English, one German flavour line appended (baking, or her bichon frisé *Tiny Bubbles Sunshine*), never repeating twice running. M5 moves the pool into the `OmaDialogueSet` ScriptableObject.
- **`UiBuilder`** — screen-space uGUI built in code. With `worldDice: true` it keeps only the non-diegetic strip (header, status, action bar, peek, overlays); background, 2D dice row and scorecard all drop away. **`ScorecardView`/`ScoreCellView`** (ghosts, two-tap confirm, Joker dimming, gold hint highlights, bonus bar) are shared verbatim by both layers, **`HudView`** (roll pips, status, totals, skip overlay, peek button, game-over panel), `SafeAreaFitter`, `UiPalette`.

**Two constraints the diegetic card creates.** Both are asserted by `WorldScorecardTests`, which also prints measured spans so retuning is reading numbers off a test run rather than guessing.

1. **Every framing the camera *rests* in during Deciding must show all 13 boxes**, clear of the action bar, at ≥64 px per box (design §5.5) — the player can score from any of them. Portrait is tight: the frustum is only ~25° wide, so card *width* is the binding limit and *tilt* is what buys legibility.
2. **The card is propped, so it occludes the table behind it.** Its raised top edge hides table level behind roughly z = −0.19 at the tightest framing. Anything the player must see — dice above all — has to live in front of that line. This is why `KitchenBuilder` has explicit `RollZoneMinZ/MaxZ/HalfX` and `KeepRowZ` constants instead of scattered magic numbers: the fence, the dice slots and the keep row are all derived from them. It is also a bug that already shipped once — the keep row sat at z = −0.30, so kept dice (yours and Oma's) were invisible.

### Editor tools (`Assets/Editor`) — all idempotent, all under the `Yahtzee/` menu

`SceneBootstrapper` (scene + TMP + build settings + portrait lock) · `OmaAssetTool` (FBX humanoid import + generated AnimatorController) · `OmaSimulationTool` (10k-game AI distribution report).

### Tests

- **EditMode (94)** — exhaustive 7,776-combination scoring sweep vs. a naive oracle, scorecard bonus edges (62/63/64), every Joker branch, engine legality/turn-flow/events with scripted RNG, 200-seed headless games with event-rebuilt totals, save round-trip + resume determinism, 6 AI tests (determinism, query-purity, made-hand keeps, Joker legality, box protection, 1000-game strength band).
- **EditMode also covers** `OmaAdviceTests` — Ask Oma keeps the right dice, names a coherent goal, stands pat on a made hand, and above all is **free**: 25 consecutive asks change neither `RngDraws` nor any state.
- **PlayMode (19)** — `GameFlowPlayModeTests` (full game vs. auto-Oma, one-box-per-turn, skip under real pacing, save/reload resume, illegal-tap no-ops, 2D-layer regression) · `DiceSoakTests` (**1,000 rolls × 5 dice all rest on engine values, inside the roll zone, and never overlapping each other**, mid-tumble skip) · `FramingCaptureTests` (renders each framing to PNG) · `ScreenCaptureTests` (renders every screen, the HUD **and the game-over panel** — the one panel nothing else reaches) · `EventSystemTests` (exactly one EventSystem across four scene loads) · `WorldScorecardTests` (card is world-space + raycastable, rests on the table clear of the dice, fully visible and ≥64 px per box in every scoring framing, and **no die is ever hidden behind it**) · `DiceTapTests` (tapping a die picks *that* die from every framing, and keep/release round-trips) · `AskOmaTests` (bubble shows advice, swallows no taps, changes nothing, no-ops when there is nothing to advise on).

**Mind the gap between "the controller works" and "the player can do it."** Every other PlayMode test drives `GameController.OnDieTapped` directly, so for the whole of M4 the suite was green while tap-to-keep was impossible to actually perform — the fence collider sat between the camera and the dice and swallowed the pick. `DiceTapTests` closes that specific gap; the same blind spot still exists for **scorecard cell taps**, which no test drives through the GraphicRaycaster.

**`ScreenCaptureTests` renders the WHOLE screen** — kitchen *plus* the screen-space UI — to `AppData\LocalLow\DefaultCompany\Dice with Oma\screens\*.png` (the folder follows `productName`, which is now "Dice with Oma" — earlier renders are under the old `yahtzee\` path), covering Title/Home/Settings/Store and the in-game HUD. Use it for any UI work. `FramingCaptureTests` only renders the camera, which by definition cannot see a ScreenSpaceOverlay canvas, so before this every HUD change cost an APK, an install and a screenshot pulled off the phone. The trick is flipping the canvas to `ScreenSpaceCamera` for the duration of the render and putting it back.

**`FramingCaptureTests` is a reusable design tool**, not just a test: it writes `AppData\LocalLow\DefaultCompany\yahtzee\framings\*.png` headless. That's how the camera was matched to the owner's concept mockup — re-run it after any scene/camera/art change and actually look at the output. `WorldScorecardTests` is the numeric half of the same loop: it catches "the card drifted off screen / got too small", which a render alone makes easy to eyeball past.

---

## 4. Decisions log (do not relitigate)

| Decision | Rationale |
|---|---|
| Pure-C# core separate from Unity presentation | Joker rules + AI are bug-prone; enables headless tests, cheap AI simulation, trivial JSON save |
| Diegetic 3D kitchen, first-person at the table | Owner's concept art; portrait composition maps naturally (Oma top / dice mid / card bottom) |
| Engine-first dice values; physics guided to match | Prevents the worst bug class (die face ≠ scorecard). Proven by the 1,000-roll soak |
| Heuristic Oma AI, ~200–230 avg | Optimal solver would be too strong and overkill; ε sloppiness knob reserved for future difficulty |
| **JsonUtility for saves** (M1) | `GameState` designed for it (int slots, `-1` sentinel, no nullables/dictionaries); avoids the Newtonsoft dependency |
| **RNG persisted as seed + draws-consumed** (M1) | `System.Random` isn't serializable; replaying the stream on load keeps resumed games on identical dice |
| ~~Custom camera rig~~ → **one fixed camera** (M4) | Owner call: the per-phase framings and their blends were fighting the transitions. `CameraDirector` is gone; `KitchenBuilder.PlaceCamera` sets one seated pose that holds Oma, the dice and the whole card at once. This also retires the "every scoring framing must show all 13 boxes" juggling — there is only one framing now |
| **HUD is one Roll button** (M4) | Owner call. Rolling is the only thing done from the HUD; keeping dice, scoring, peeking and asking Oma are all taps on the table. Round/score sit in a top bar, and New Game / Settings / Store live behind a hamburger. Settings and Store are **disabled placeholders** wired up in M6 |
| **Ask Oma is her coffee mug** (M4) | Owner call: a "?" on the mug replaces the button, matching the diegetic peek. Same discoverability caveat as peeking — it needs the M6 how-to-play |
| **Scorecard is a world-space canvas, not a mesh/texture** (M4) | Keeps `ScoreCellView` and the two-tap confirm byte-identical between the 2D and 3D layers; taps ray-cast via the canvas's own GraphicRaycaster. Cost: UI shaders are unlit, so the card ignores the lamp — revisit during the art pass |
| **Table deepened toward the player** (M4) | The old near edge (z −0.65) left only 0.27 m in front of the dice fence, too shallow for a legible card. Far edge unchanged, so Oma's side and all dice physics are untouched |
| **DiceFocus is transient — camera eases back on settle** (M4) | *Owner approved 2026-07-20; supersedes the literal reading of design §5.2 ("push in over the dice **when they settle**").* A physical card means any framing the player can score from must show all 13 boxes, and DiceFocus crops it badly (verified in the renders). The push-in now plays during the tumble and eases to Default once dice rest, where dice stay clearly legible. Rejected alternative: hold the push-in ~0.8 s then ease back — more literal to the spec, but boxes are unreachable during the hold, so a tap can land on nothing |
| **UI built in code, not prefabs** (M2) | The 2D layer is throwaway scaffolding; code keeps the whole layout reviewable and diffable |
| German flavor phrases in Oma's dialogue | Owner approved; flavor only, never rules-critical; game stays English-only |
| **Peeking is diegetic — tap Oma's card, no button** (M4) | Owner request. The card on the table is always *yours*; it used to follow the current player, so her scores were on show for her whole turn. **Discoverability risk:** nothing on screen says her card is tappable — worth a line in the M6 how-to-play |
| **Oma is astonished at you, and claps for herself** (M4) | Owner request: a big score from the player triggers *disbelief*, not applause — she is playfully competitive. Her own good hands get the clap, played **before** the score commits so the dice are still on the table and the animation has room to run |
| **"Ask Oma" hint is free and unlimited** | Owner-requested. She runs `OmaAI.Advise` — the *same* subset evaluator she plays by, so she gives away her own reasoning rather than peeking at anything hidden. It is a pure query: no RNG draws, no mutation (asserted in `OmaAdviceTests` / `AskOmaTests`), so a player who asks every turn gets identical dice to one who never asks. No cost/limit, matching the cozy no-meta decision |
| Restart = no consequence; no stats/meta/monetization in v1 | Owner decision; cozy game |
| Trademark flag | "Yahtzee" is Hasbro's; possible rename pre-release; no Hasbro assets ever |

## 5. Rules subtleties already specced (don't re-derive)

- Joker: forced priority = matching upper box → any open lower (FH 25 / SS 30 / LS 40 at fixed value) → forced 0 in an open upper. +100 only if the Yahtzee box holds 50; multiple bonuses possible.
- Five-of-a-kind is NOT a natural Full House (Joker only). Five sequential dice DO count for Small Straight. Zero-scoring any category is always legal.
- Keepers freely revisable between rolls (a die kept after roll 1 may be released on roll 3).
- Two-tap confirm on score cells, no undo. Ghost potentials on open cells after each roll. Upper-bonus live progress.
- Oma's decisions are pure functions of state, computed stage-by-stage — that's *why* Skip can't change outcomes. Keep it that way.

---

## 6. What's left

### M4 finish (current milestone)

1. **Real art pass** — swap gray-box primitives for the low-poly kitchen; **Oma is a purple-tinted placeholder mannequin** right now. Note: her FBX import extracted real textures (`Assets/Resources/Oma/Ch36_*.png`) that are currently unused — wiring those up is a quick interim improvement over the purple tint. The scorecard also still reads as a dark UI panel rather than paper (unlit UI shader, `UiPalette.Panel` border) — worth a pass here. Re-run `FramingCaptureTests` after any art change.
2. **Android device build** — **the game builds, installs and runs on a Pixel 10 Pro** (IL2CPP/ARM64), verified by a screenshot pulled off the device: 3D scene, dice, gold keep pads and the diegetic card all render correctly in portrait. That clears the long-outstanding **M2 exit criterion**. Still to do: **60 fps check**, safe-area on a real notch, and a **touch input pass**.

   **Real touch is still unverified.** Every automated test drives `GameController` or `DiceView3D.DieAtScreenPoint` directly, so nothing exercises input through the EventSystem — in particular scorecard cell taps via the world-space `GraphicRaycaster`, and whether `IsPointerOverUi` correctly stops a tap on the card from also reaching the dice underneath. That is the same blind spot that hid the dice-tap bug for all of M4, so sit with the device and tap every control.

### M5 — Oma lives (not started)

- `DialogueService` subscribing to the same `GameEvent`s, mapping to trigger types, picking unused variants from an `OmaDialogueSet` ScriptableObject (per-game no-repeat set).
- ~~`DialogueService`~~, ~~`SpeechBubbleController`~~, ~~trigger/line set~~ — **done.** All 13 design §2 triggers with 3+ variants each, enforced by `OmaDialogueTests` (3+ variants, ≤90 chars per line — the rules that erode as lines get added).
- Full v1 trigger/line set per design §2 (3+ variants each, German flavor phrases).
- Real Oma model integration; expression tech (blend shapes vs. material/UV swap) still undecided — decide with the artist. `OmaView`'s API stays the same either way.

### M6 — Polish & ship-ready (partly done)

**Done:** Title / Home / Settings / Store / **How to Play** / **Results** screens (`ScreensView` / `ScreensBuilder` / `ResultsView`); the hamburger→Home doubles as the **pause overlay** (Resume/Restart/How to Play/Settings/Store/Title). IL2CPP+ARM64 and the rest of the Play build config (`SceneBootstrapper.ConfigureForPlay`; release `.aab` build proven). "Yahtzee" → "Five of a Kind" in the UI.

**Audio, haptics, fanfares — built, wired, ship-silent.** `AudioService` loads clips by name from `Resources/Audio/` (drop-in by convention, like the font); `HapticsService` drives the Android Vibrator with a fallback. Dice-tap tick, five-of-a-kind success buzz + big fanfare, straight/full-house/bonus fanfares (all **player-only** — a stinger for Oma's plays would cheer against you), win/lose stings, background loop. Toggles in **Settings → Sound & feel** (PlayerPrefs). **No sound files yet** — the game plays fine silent until the owner supplies clips per [`AUDIO_ASSETS.md`](AUDIO_ASSETS.md).

**Still to do:** **real app icon + splash** (needs art) · a manual on-device test checklist. Camera-comfort pass is moot — the camera is a single fixed pose now.

**Google Play submission** — the console/legal/art work lives in [`LAUNCH.md`](LAUNCH.md). Headline gotcha: a **new personal dev account must run a 14-day / 20-tester closed test before production**, so "submit" is ~2 weeks out, not same-day. And **run `Yahtzee > Setup Project` once against the real project** so the target-SDK/product-name settings land in `ProjectSettings.asset` (they are enforced in code but the editor was holding the project during dev).

### Loose ends

- **Replacing the typeface:** drop a `.ttf`/`.otf` into `Assets/Fonts/` and run **Yahtzee > Build Font Asset**. The current face is `DearGrandma.otf`, built to `Assets/Resources/Fonts/GameFont.asset`, which `UiBuilder` applies to every text in the game. Note it is *wider* than a plain sans — swapping faces can overflow the scorecard's name column into the printed hints, which is what the tight column splits in `ScorecardBuilder.BuildCell` are for. Check the screen renders after any change.

- Strip unused template modules (terrain, vehicles, XR) — cleanup, not urgent. **Adaptive Performance is already gone**: it was never in `manifest.json` directly but came via `com.unity.feature.mobile`, which is why removing it in Package Manager never stuck — the feature set re-added it on every resolve. Removing the feature set took Adaptive Performance, mobile-notifications and android-logcat with it.
- **Trademark: resolved in-app, pending in the listing.** The UI says "Five of a Kind", not "Yahtzee" (display strings only — `Category.Yahtzee` and all internal names stay). The launcher label is "Dice with Oma". The **package id is still `com.DefaultCompany.yahtzee`** — owner is leaving it for now, but it is permanent once published and still holds the trademark word ([`LAUNCH.md`](LAUNCH.md) §6). Listing text must avoid "Yahtzee" too.

---

## 7. Working agreements with the owner

- Milestone order M1→M6; update this file when a milestone or major decision lands.
- Owner reviews playable builds in the editor and gives direction from concept art — **render screenshots and check them yourself** before claiming a visual change works.
- Commits are per-milestone with a summary body; push to `origin/main` (https://github.com/munruh250/yahtz). Owner has asked to commit+push explicitly each time so far.
- Tests run headless and must be green before committing.

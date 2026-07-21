# Handoff — Yahtzee with Oma

**Last updated:** 2026-07-20
**Status:** M1–M3 complete, **M4 ~75%**. The game is fully playable end-to-end in the 3D kitchen scene vs. an auto-playing Oma, and the scorecard is now a physical object on the table.
**Test baseline (must stay green):** EditMode **94**, PlayMode **12**.
**Next task:** M4 finish — see [What's left](#whats-left), item 1 (cup pour).

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

**Unity allows one instance per project.** Headless runs fail with `HandleProjectAlreadyOpenInAnotherInstance` while the editor is open. Check with `Get-Process Unity` before batch runs; if the owner has the editor open, either ask them to close it or hand them the menu-item equivalent.

### Headless commands

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
| `AI/OmaAI.cs` | `DecideKeepers` (32 subsets), `DecideCategory`, `RankForHint` (player hints, same valuation) |

### Services (`Assets/Scripts/Services`, asmdef `Yahtzee.Services`)

`SaveService` — JsonUtility → `persistentDataPath/save.json`, version int, `TryLoad`/`HasResumableSave`/`Delete`.

### Presentation (`Assets/Scripts/Presentation`, asmdef `Yahtzee.Presentation`)

- **`GameController`** — owns engine, subscribes to events, sequences everything. Input entry points: `OnRollTapped`, `OnDieTapped`, `OnCellTapped`, `OnSkipTapped`, `OnPeekTapped`, `OnNewGameTapped`. Two static flags: **`AnimationsEnabled`** (tests set false → everything instant) and **`Use3dDice`** (false → the 2D layer, kept per TECH_PLAN §7).
- **`IDiceView`** — `SetDice` / `PlayRoll` / `SetInteractable` / `SkipAnimation`. Two implementations:
  - `DiceView3D` + `Die3D` — physics dice, **engine value decided first**, guided settle, watchdog snap, raycast tap-to-keep.
  - `DiceView2D` + `DieView2D` — sprite fallback behind the debug flag.
- **`KitchenBuilder`** — builds the whole gray-box scene in code (table, fence, lamp + fill light, props, dice, the diegetic scorecard, Oma, camera framings). Real art replaces primitives here.
- **`CameraDirector`** — 4 framings (Default / DiceFocus / ScorecardFocus / OmaFocus), 0.5 s eased blends.
- **`OmaView`** — idle rotation (idle/shift/talking, 5–12 s) + `PlayReaction(Clap|Disbelief)`.
- **`ScorecardBuilder`** — the one grid builder for both layers. `BuildInto(rect, …)` fills any RectTransform with the 13 boxes + title + bonus row; `BuildWorld(…)` wraps that in a **world-space canvas** lying on the table, propped 24° at the player, on a backing board. Card geometry (size, tilt, z) is a block of named constants at the top of the file — tune there, then re-run the framing renders.
- **`UiBuilder`** — screen-space uGUI built in code. With `worldDice: true` it keeps only the non-diegetic strip (header, status, action bar, peek, overlays); background, 2D dice row and scorecard all drop away. **`ScorecardView`/`ScoreCellView`** (ghosts, two-tap confirm, Joker dimming, gold hint highlights, bonus bar) are shared verbatim by both layers, **`HudView`** (roll pips, status, totals, skip overlay, peek button, game-over panel), `SafeAreaFitter`, `UiPalette`.

**The constraint the diegetic card creates:** the player may score from any framing the camera *rests* in, so those framings must show all 13 boxes, clear of the action bar, at ≥64 px per box (design §5.5). Portrait is tight — the frustum is only ~25° wide, so card width is the binding limit and tilt is what buys legibility. `WorldScorecardTests` asserts this and prints the measured spans, so retuning is reading numbers off a test run rather than guessing.

### Editor tools (`Assets/Editor`) — all idempotent, all under the `Yahtzee/` menu

`SceneBootstrapper` (scene + TMP + build settings + portrait lock) · `OmaAssetTool` (FBX humanoid import + generated AnimatorController) · `OmaSimulationTool` (10k-game AI distribution report).

### Tests

- **EditMode (94)** — exhaustive 7,776-combination scoring sweep vs. a naive oracle, scorecard bonus edges (62/63/64), every Joker branch, engine legality/turn-flow/events with scripted RNG, 200-seed headless games with event-rebuilt totals, save round-trip + resume determinism, 6 AI tests (determinism, query-purity, made-hand keeps, Joker legality, box protection, 1000-game strength band).
- **PlayMode (12)** — `GameFlowPlayModeTests` (full game vs. auto-Oma, one-box-per-turn, skip under real pacing, save/reload resume, illegal-tap no-ops, 2D-layer regression) · `DiceSoakTests` (**1,000 rolls × 5 dice all rest on engine values**, mid-tumble skip) · `FramingCaptureTests` (renders each framing to PNG) · `WorldScorecardTests` (card is world-space + raycastable, rests on the table clear of the dice, fully visible and ≥64 px per box in every scoring framing).

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
| **Custom camera rig, not Cinemachine** (M4) | 4 fixed framings + one blend coroutine didn't justify the package. Revisit only if framing needs grow |
| **Scorecard is a world-space canvas, not a mesh/texture** (M4) | Keeps `ScoreCellView` and the two-tap confirm byte-identical between the 2D and 3D layers; taps ray-cast via the canvas's own GraphicRaycaster. Cost: UI shaders are unlit, so the card ignores the lamp — revisit during the art pass |
| **Table deepened toward the player** (M4) | The old near edge (z −0.65) left only 0.27 m in front of the dice fence, too shallow for a legible card. Far edge unchanged, so Oma's side and all dice physics are untouched |
| **DiceFocus is transient — camera eases back on settle** (M4) | *Owner approved 2026-07-20; supersedes the literal reading of design §5.2 ("push in over the dice **when they settle**").* A physical card means any framing the player can score from must show all 13 boxes, and DiceFocus crops it badly (verified in the renders). The push-in now plays during the tumble and eases to Default once dice rest, where dice stay clearly legible. Rejected alternative: hold the push-in ~0.8 s then ease back — more literal to the spec, but boxes are unreachable during the hold, so a tap can land on nothing |
| **UI built in code, not prefabs** (M2) | The 2D layer is throwaway scaffolding; code keeps the whole layout reviewable and diffable |
| German flavor phrases in Oma's dialogue | Owner approved; flavor only, never rules-critical; game stays English-only |
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

1. **Cup pour** — dice currently spawn beside the cup. They should launch from inside it with a tip/pour animation (`KitchenBuilder.CupPosition`, `DiceView3D.PlayRoll`).
2. **Real art pass** — swap gray-box primitives for the low-poly kitchen; **Oma is a purple-tinted placeholder mannequin** right now. Note: her FBX import extracted real textures (`Assets/Resources/Oma/Ch36_*.png`) that are currently unused — wiring those up is a quick interim improvement over the purple tint. The scorecard also still reads as a dark UI panel rather than paper (unlit UI shader, `UiPalette.Panel` border) — worth a pass here. Re-run `FramingCaptureTests` after any art change.
3. **Android device build** — 60 fps check, touch input pass, safe-area on a real notch. This is also the outstanding **M2 exit criterion** ("playable on device build" was only ever verified in-editor). Don't let it slip past M4. Note the world-space card's tap targets have only ever been exercised through `GameController` in tests — **actually tapping boxes on a device is unverified** and is the first thing to check.

### M5 — Oma lives (not started)

- `DialogueService` subscribing to the same `GameEvent`s, mapping to trigger types, picking unused variants from an `OmaDialogueSet` ScriptableObject (per-game no-repeat set).
- `SpeechBubbleController` — auto-dismiss ~3.5 s, replace-don't-queue, **never blocks input**.
- Full v1 trigger/line set per design §2 (3+ variants each, German flavor phrases).
- Real Oma model integration; expression tech (blend shapes vs. material/UV swap) still undecided — decide with the artist. `OmaView`'s API stays the same either way.

### M6 — Polish & ship-ready (not started)

Title / Results / Pause screens (only a game-over panel exists) · audio (`AudioService` + library, mute in PlayerPrefs) · haptics · special-hand fanfares · camera-motion comfort pass · icon/splash · IL2CPP+ARM64 build check · manual test checklist.

### Loose ends

- Strip unused template modules (terrain, vehicles, XR) — cleanup, not urgent.
- **EventSystem leak across scene loads** (pre-existing, surfaced by the new tests). `UiBuilder.EnsureEventSystem` tags the object `HideFlags.DontSave`, which also means "survives scene load", so each reload adds another and Unity logs *"There are N event systems in the scene"*. Harmless today (the game loads its scene once) but it will bite in M6 when Title ⇄ Game navigation lands, and multiple EventSystems break input.
- Final game name / trademark review — before store submission.

---

## 7. Working agreements with the owner

- Milestone order M1→M6; update this file when a milestone or major decision lands.
- Owner reviews playable builds in the editor and gives direction from concept art — **render screenshots and check them yourself** before claiming a visual change works.
- Commits are per-milestone with a summary body; push to `origin/main` (https://github.com/munruh250/yahtz). Owner has asked to commit+push explicitly each time so far.
- Tests run headless and must be green before committing.

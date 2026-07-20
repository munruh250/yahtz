# Handoff — Yahtzee with Oma

**Last updated:** 2026-07-20 (overnight session)
**Status:** **M3 complete — Oma plays her own turns.** EditMode 94 + PlayMode 5 all green; 10k-game sim: mean 219.0 (target 200-230), zero illegal actions. Next step: **M4 — the kitchen (3D scene, physics dice, camera rig).**

## What exists

- Unity 2022.3.62f3 LTS project (Mobile 3D template), cleaned: `TutorialInfo`/`Readme` removed, scene renamed to `Game.unity` (GUID preserved).
- **`Assets/Scripts/Core`** (`Yahtzee.Core` asmdef, `noEngineReferences: true`): `Category` (+extensions), `DiceState`, `IRandomSource`/`SeededRandomSource` (seed + draws-consumed, replay-skip on resume), `ScoreCalculator` (histogram + straight bitmask), `Scorecard` (int slots, -1 = open), `JokerRules`, `GameState` (the save file), `GameEngine` (sole mutator, throws on illegal actions, emits `GameEvent`s: DiceRolled, JokerActivated, ScoreCommitted, UpperBonusSecured, TurnChanged, GameEnded).
- **`Assets/Scripts/Services`** (`Yahtzee.Services` asmdef): `SaveService` — JsonUtility, `persistentDataPath/save.json`, save-version int, `HasResumableSave()`.
- **`Assets/Scripts/Presentation`** (`Yahtzee.Presentation` asmdef): full M2 2D prototype. `GameController` (sole engine-mutator caller; input lock during animations; save on TurnChanged/GameEnded/pause/quit; auto-continues a resumable save on launch, else new seeded game). `UiBuilder` constructs the whole uGUI screen in code (throwaway scaffolding — no prefabs): four portrait zones per design §5.2, 1080×2340 scaler, `SafeAreaFitter`. `IDiceView` interface (M4 swaps in 3D behind it) ← `DiceView2D`/`DieView2D` (tap-to-keep with gold outline + lift, face-flicker roll animation, `GameController.AnimationsEnabled=false` makes everything instant for tests). `ScorecardView`/`ScoreCellView` (ghost potentials, two-tap confirm, Joker-illegal cells dimmed, bonus progress bar, Yahtzee-bonus "xN" chips). `HudView` (roll pips, status line incl. Joker explainer, header totals, game-over panel with Play Again). Player plays both hands in M2.
- **`Assets/Editor/SceneBootstrapper.cs`**: idempotent setup — menu "Yahtzee/Setup Project" or `-executeMethod Yahtzee.EditorTools.SceneBootstrapper.SetupProject`. Adds GameRoot to `Game.unity`, imports TMP essentials, sets EditorBuildSettings + portrait lock.
- **`Assets/Tests/PlayMode`** (`Yahtzee.Tests.PlayMode` asmdef): 3 tests driving `GameController`'s public input surface with animations off — full game to completion, save/reload resume equality, illegal-tap no-ops.
- **`Assets/Tests/EditMode`** (`Yahtzee.Tests.EditMode` asmdef): 88 tests — exhaustive 7,776-combo scoring sweep with naive-oracle straight check, scorecard bonus edges (62/63/64), every Joker branch, engine legality/turn-flow/event tests with scripted RNG, 200-seed full headless games with event-rebuilt totals, save round-trip + resume-determinism tests.
- `Docs/DESIGN_SPEC.md`, `Docs/TECH_PLAN.md`, `CLAUDE.md` as before.
- Git repo initialized with Unity `.gitignore`; remote: https://github.com/munruh250/yahtz (branch `main`).

## Environment note (this machine)

The primary `C:\Program Files\Unity\Hub\Editor\2022.3.62f3` install is **corrupt** (missing `PackageManager\Server` — Unity exits with code 1 in batch mode). Use the intact sibling install `2022.3.62f3-x86_64` (same version). Headless test run:

```
"C:\Program Files\Unity\Hub\Editor\2022.3.62f3-x86_64\Editor\Unity.exe" -batchmode -projectPath <proj> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
```

## Decisions log (with rationale)

| Decision | Rationale |
|---|---|
| Pure-C# core (`Yahtzee.Core` asmdef) separate from Unity presentation | Joker rules + AI are bug-prone; enables headless EditMode tests, cheap AI simulation, trivial JSON save (one `GameState` object) |
| Diegetic 3D kitchen scene, first-person at the table | Owner's concept art (low-poly night kitchen, lamp, Oma across the table, scorecard/dice/cup as physical props). Portrait composition maps naturally: Oma top / dice mid / scorecard bottom |
| 2D dice prototype (M2–M3) → 3D physics dice (M4) | Prove loop fast; 3D behind same `IDiceView` interface. Owner explicitly wants 3D throws + camera treatments for readability long-term |
| Engine-first dice values; physics guided to match | Prevents the worst bug class (die face ≠ scorecard). Watchdog snap + 1,000-roll soak test |
| Heuristic Oma AI, ~200–230 avg | Optimal solver would be too strong and overkill; single "sloppiness" ε reserved for future difficulty levels (0 in v1) |
| German flavor phrases in Oma's dialogue | Owner approved; flavor only, never rules-critical; game stays English-only |
| Restart = no consequence | Owner decision; cozy game, no stats in v1 |
| One scene, screens as canvases; JSON save at persistentDataPath; save-format version int from day one | Right-sized for scope |
| **JsonUtility for saves (decided in M1)** | `GameState` designed for it: int-array slots with -1 open sentinel, no nullables/dictionaries; mid-turn round-trip + resume-determinism proven by tests; avoids adding the Newtonsoft package |
| RNG persisted as seed + draws-consumed | `System.Random` state isn't serializable; `SeededRandomSource(seed, drawsToSkip)` replays the stream on load — resumed games continue on the identical dice (tested) |
| Trademark flag | "Yahtzee" is Hasbro's; possible rename ("Dice with Oma" style) pre-release; no Hasbro assets |

## Rules subtleties already specced (don't re-derive)

- Joker rules: forced priority = matching upper box → any open lower (FH 25/SS 30/LS 40 at fixed value) → forced 0 in an open upper. +100 bonus only if Yahtzee box holds 50; multiple bonuses possible; UI restricts cells + shows explainer.
- Five-of-a-kind is NOT a natural Full House (only via Joker). Five sequential dice DO count for Small Straight. Zero-scoring a category is always legal.
- Keepers freely revisable between rolls (may release a kept die on roll 3).
- Two-tap confirm on score cells; no undo after confirm. Ghost potential scores on open cells after each roll. Upper-bonus live progress ("41 / 63").
- Oma's turns: decisions computed stage-by-stage (`DecideKeepers` per roll, `DecideCategory` at end), watchable 6–12 s pacing, Skip fast-forwards without changing outcomes (deterministic given state).

## M3 (done this session)

- **`Assets/Scripts/Core/AI/OmaAI.cs`**: staged heuristic per TECH_PLAN §5.6 — `DecideKeepers` (32 keep-subsets: of-a-kind line with upper-bonus bias + Yahtzee chase, straight-draw line, full-house line, chance floor; made-hand stand-pat shortcuts; all-five-kept = stop rolling) and `DecideCategory` (points − per-box opportunity-cost EV table + upper-bonus pace weight + secure-the-63 pull). Pure functions of engine state (deterministic ⇒ skip-safe), query-only, Joker-compliant via `GetLegalCategories()`. ε sloppiness knob in the ctor, 0 in v1. **Strength (untuned first cut): 10k games mean 219.0, median 210, p10 166, p90 271, upper bonus 33.5%/card — in band, no tuning needed.**
- Editor menu **"Yahtzee/Run Oma Simulation (10k games)"** (`OmaSimulationTool`) re-reports the distribution any time weights change.
- `GameController`: Oma turn coroutine (think beats 0.18-1.0 s, keepers highlight one-by-one, chosen cell flashes, turn lands in the 6-12 s design window), tap-anywhere **skip** via full-screen overlay (`OnSkipTapped` → fast-forward + `IDiceView.SkipAnimation()`), **peek** toggle (scorecard owner title "YOUR CARD"/"OMA'S CARD"), resume works mid-Oma-turn from a pause-save.
- Tests: 6 new EditMode (determinism, query-purity, made-hand keeps, Joker legality, junk-roll box protection, 1000-game strength band 200-235) → 94 total; PlayMode reworked for auto-Oma (full game vs her, one-box-per-turn, skip under real pacing, save/reload, illegal taps) → 5 total.

## Next steps (M4 — the kitchen, 3D)

1. **3D physics dice first (top risk)**: `DiceView3D`/`Die3D` behind the existing `IDiceView` — engine-first values, randomized launch impulses from the cup, guided settle in the last ~0.3 s to the engine face, ~2.5 s watchdog hard-snap, kept dice kinematic in the keep row. PlayMode **soak test: 1,000 rolls, rest face must equal engine value every time** (TECH_PLAN §5.4, risk table).
2. Camera rig with phase framings Default/DiceFocus/ScorecardFocus/OmaFocus, blends ≤0.6 s, input snaps to interactive framing. Open decision at M4 start: Cinemachine (tech-plan recommendation) vs small custom rig.
3. Gray-box kitchen (table, lamp light, collider fence) until art lands; diegetic world-space scorecard reusing the same ScoreCellView logic.
4. Keep the 2D layer working behind a debug flag (fast rules testing per TECH_PLAN §7).

## M2 leftovers (not blocking M3/M4)

- Device build untested (M2 exit criterion "playable on device build" verified in editor only — do a real Android build by M4 at the latest).
- 2D layer stays as debug-flag fallback after M4 per TECH_PLAN §7.

## Open items (not blocking M1)

- Cinemachine vs. custom camera rig — decide at M4 start (recommendation: Cinemachine).
- Oma expression tech (blend shapes vs. material/UV face swap) — decide with artist at M5.
- Final game name (trademark) — before store submission.

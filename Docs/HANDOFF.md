# Handoff — Yahtzee with Oma

**Last updated:** 2026-07-20
**Status:** **M1 complete — core rules engine done, all 88 EditMode tests green.** Next step: **M2 — playable 2D prototype loop.**

## What exists

- Unity 2022.3.62f3 LTS project (Mobile 3D template), cleaned: `TutorialInfo`/`Readme` removed, scene renamed to `Game.unity` (GUID preserved).
- **`Assets/Scripts/Core`** (`Yahtzee.Core` asmdef, `noEngineReferences: true`): `Category` (+extensions), `DiceState`, `IRandomSource`/`SeededRandomSource` (seed + draws-consumed, replay-skip on resume), `ScoreCalculator` (histogram + straight bitmask), `Scorecard` (int slots, -1 = open), `JokerRules`, `GameState` (the save file), `GameEngine` (sole mutator, throws on illegal actions, emits `GameEvent`s: DiceRolled, JokerActivated, ScoreCommitted, UpperBonusSecured, TurnChanged, GameEnded).
- **`Assets/Scripts/Services`** (`Yahtzee.Services` asmdef): `SaveService` — JsonUtility, `persistentDataPath/save.json`, save-version int, `HasResumableSave()`.
- **`Assets/Scripts/Presentation`** (`Yahtzee.Presentation` asmdef): placeholder only; M2 fills it.
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

## Next steps (M2 — playable prototype loop, 2D)

1. `GameController` MonoBehaviour in Presentation: owns `GameEngine`, subscribes to events, sequences with coroutines; the only caller of engine mutators.
2. Flat 2D game screen in `Game.unity` (uGUI canvas, 1080×2340 reference, safe-area root): sprite dice + keep toggles behind `IDiceView` (2D impl), scorecard with ghost potentials + two-tap confirm, Roll button with pips, HUD status text.
3. Wire `SaveService` (save on ScoreCommitted/TurnChanged + `OnApplicationPause`); player plays *both* sides manually.
4. Add `Game.unity` to EditorBuildSettings (scene list is currently empty); portrait lock + `targetFrameRate = 60`.
5. Exit: full game playable on a device build; save/continue works.

## Open items (not blocking M1)

- Cinemachine vs. custom camera rig — decide at M4 start (recommendation: Cinemachine).
- Oma expression tech (blend shapes vs. material/UV face swap) — decide with artist at M5.
- Final game name (trademark) — before store submission.

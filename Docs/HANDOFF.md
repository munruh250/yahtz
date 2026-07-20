# Handoff — Yahtzee with Oma

**Last updated:** 2026-07-19
**Status:** Documentation phase complete. **No code written yet.** Next step: **M1 — core rules engine.**

## What exists

- Fresh Unity 2022.3.62f3 LTS project (Mobile 3D template). Only template content in `Assets/` (`SampleScene`, `TutorialInfo` — both to be removed/renamed in M1).
- `Docs/DESIGN_SPEC.md` — full design spec (rules, Oma character, screens, art direction).
- `Docs/TECH_PLAN.md` — full tech plan (architecture, domain model, dice/camera approach, AI, testing, milestones M1–M6, risks).
- `CLAUDE.md` — condensed project context auto-loaded each session.
- No git repo yet — consider `git init` + Unity `.gitignore` before M1.

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
| One scene, screens as canvases; JSON save at persistentDataPath; save-format version int from day one | Right-sized for scope; `JsonUtility` vs. Newtonsoft decided in M1 (nullable-slot round-trip test) |
| Trademark flag | "Yahtzee" is Hasbro's; possible rename ("Dice with Oma" style) pre-release; no Hasbro assets |

## Rules subtleties already specced (don't re-derive)

- Joker rules: forced priority = matching upper box → any open lower (FH 25/SS 30/LS 40 at fixed value) → forced 0 in an open upper. +100 bonus only if Yahtzee box holds 50; multiple bonuses possible; UI restricts cells + shows explainer.
- Five-of-a-kind is NOT a natural Full House (only via Joker). Five sequential dice DO count for Small Straight. Zero-scoring a category is always legal.
- Keepers freely revisable between rolls (may release a kept die on roll 3).
- Two-tap confirm on score cells; no undo after confirm. Ghost potential scores on open cells after each roll. Upper-bonus live progress ("41 / 63").
- Oma's turns: decisions computed stage-by-stage (`DecideKeepers` per roll, `DecideCategory` at end), watchable 6–12 s pacing, Skip fast-forwards without changing outcomes (deterministic given state).

## Next steps (M1, in order)

1. `git init` + Unity `.gitignore`; delete `Assets/TutorialInfo`, rename `SampleScene` → `Game`.
2. Create asmdefs: `Yahtzee.Core`, `Yahtzee.Presentation`, `Yahtzee.Tests.EditMode`.
3. Implement Core: `Category`, `DiceState`, `IRandomSource`, `ScoreCalculator` (histogram + straight bitmask), `Scorecard`, `JokerRules`, `GameState`, `GameEngine` + `GameEvent`s.
4. SaveService (headless JSON round-trip; decide JsonUtility vs. Newtonsoft here).
5. EditMode test suite per TECH_PLAN §6.1–6.4. Exit: all green + scripted headless 13-round game with correct totals.

## Open items (not blocking M1)

- Cinemachine vs. custom camera rig — decide at M4 start (recommendation: Cinemachine).
- Oma expression tech (blend shapes vs. material/UV face swap) — decide with artist at M5.
- Final game name (trademark) — before store submission.

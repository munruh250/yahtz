# Yahtzee with Oma

Cozy mobile Yahtzee (iOS/Android, portrait) vs. one AI opponent: **Oma**, the player's warm, playfully competitive grandmother. Single-player only in v1.

## Project

- Unity **2022.3.62f3 LTS**, Mobile 3D template. uGUI + TextMeshPro. Portrait-only, 60 fps target, IL2CPP/ARM64.
- Docs are authoritative — read before implementing:
  - `Docs/DESIGN_SPEC.md` — rules (incl. Joker/Yahtzee-bonus), Oma character, screens/UX, art direction
  - `Docs/TECH_PLAN.md` — architecture, domain model, AI, milestones (M1–M6), risks
  - `Docs/HANDOFF.md` — session context, decisions log, current status / next step

## Architecture (non-negotiable)

- **Pure C# core, no UnityEngine:** `Assets/Scripts/Core` (asmdef `Yahtzee.Core`) holds all rules, scoring, `GameEngine`, `OmaAI`. MonoBehaviours in `Presentation/` only render state and forward input; `GameController` is the sole caller of engine mutators.
- Core → presentation communication is via **GameEvents only**.
- **Engine decides dice values first; physics/animation is theater.** Never read values off settled rigidbodies — 3D dice are guided to land on engine-chosen faces.
- Injected `IRandomSource`, one seeded RNG per game (stored in `GameState`, which is also the save file). Oma uses the same rules API and RNG — she never cheats.
- EditMode tests (Unity Test Framework) for all core logic; Joker rules and scoring get exhaustive coverage **before** any UI work.

## Key decisions (do not relitigate)

1. Diegetic 3D low-poly kitchen scene — first-person seat at Oma's table; scorecard/dice/cup are physical objects. Camera treatments per phase (≤0.6 s, subtle).
2. Dice: **2D sprite prototype first** (M2–M3), then 3D physics dice behind the same `IDiceView` interface (M4). Keep 2D layer behind a debug flag.
3. Oma AI: heuristic (32 keep-subset eval + weighted category choice), target **~200–230 avg**, tuned via 10k-game simulation harness. Not an optimal solver.
4. Oma's dialogue includes German flavor phrases ("Schatz", "Ach du lieber!") — never rules-critical. English-only game.
5. Mid-game restart has no consequence. No stats/meta/monetization/multiplayer in v1.
6. Trademark risk: "Yahtzee" is Hasbro's — name may change pre-release (e.g., "Dice with Oma"); no Hasbro assets ever.

## Conventions

- Milestone order M1→M6 per `Docs/TECH_PLAN.md` §7; don't start UI before M1 tests are green.
- Update `Docs/HANDOFF.md` status when a milestone or major decision lands.

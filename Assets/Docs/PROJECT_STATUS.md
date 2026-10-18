# PROJECT_STATUS.md — Blacktop

Last updated: 2026-10-17

Status snapshot of what works, what's incomplete, and what's blocking. Keep this current. If it disagrees with the code, the code wins.

Items marked **(verify)** were recorded from planning notes and should be confirmed against the repo before anyone relies on them.

## Summary

Blacktop is an arcade 7v7 street football game (Unity 6.5, URP). The core loop is playable end-to-end in rough form: formation, play call, movement, passing, tackling, possession change, touchdown, win condition. The current phase is **validating match correctness and filling in presentation/content gaps** (the core downs, OOB, and loose-ball paths exist), not polish.

## What works

### Movement and moves
- Transform-based player movement (no Rigidbody).
- Juke, Hurdle, and StiffArm, attribute-driven, implemented through the `IPlayerMove` pattern and orchestrated by `PlayerStateMachine`.

### Ball and possession
- Passing and pitching with a parabolic arc; `BallState` enum.
- Possession transfer.
- `PossessionController` with `ActivelyControlled` as the user-control authority.
- `PlayState.PossessionTeamId` owns drive direction; live carrier ownership drives return control/direction before the whistle.
- QB starts with the ball in hand (no snap).

### Offense and defense
- `BlockingCoordinator` / `AllyBlocker` for blocking. **Marked "untested" in observed play.**
- 7-defender formation via `FormationData` asset.
- Defender coordination: closest defender engages, others contain, with an LOS-break override.
- Tackle / play-state system and touchdown detection.
- Route/block rule: on Pass plays receivers run their route to completion or until a ball carrier crosses the LOS; on Run plays WRs block. Trick plays are deferred.

### Game flow
- Drives start at -10 / +10 on the receiving team's own side. Scorer/receiving-drive regression tests pass for offensive and defensive touchdowns.
- Safety only when the play *ends* in the team's own end zone, not merely when the ball touches it. **(verify: same pass.)**
- Win condition: first to 21 via ScoreBoard/GameRules; a winning defensive-return test passes with a runtime test target of six. Full manual match/rematch remains required.

### UI and feedback
- `ReceiverSelectionUI`, play-call UI, unified `GameHUD` scorebug and winner/rematch overlay (scene-wired).
- `CarrierHighlight`.
- `FieldLines.cs`: procedural LOS, first-down, and goal lines.

### Attributes
- `PlayerAttributes` 12-stat system with `AttributeCurves` and `FieldModifiers`. **(verify how much of the 12-stat list below is wired versus planned.)**
- `GamebreakerController` exists. **(verify: how closely it matches the spec below.)**

## Known issues

| Issue | Notes |
|---|---|
| OOB / goal-line rulings need Editor validation | Automatic OOB polling exists. Held-ball goal-line and sideline checks now share live ruling position and contact precedence; test both directions and corners. |
| Downs and first-down marker need match regression | `DownsTracker` is scene-wired; field lines consume its real distance. |
| `carryOffset` untuned | On `BallController`. Ball position relative to the carrier. |
| Blocking untested | `BlockingCoordinator` / `AllyBlocker` have not been properly play-tested. |
| Loose-ball movement ownership | Nearest recovery with tie-break exists; DefenderAI and AllyBlocker can both move some players during a scrum. Deferred outside the four resolution fixes. |
| Gamebreaker wiring/lifetime | Player controller is absent from inspected scene/prefab stacks; active lifetime differs from documented intent. Deferred. |
| Score / clock | ScoreBoard and play-clock HUD exist; no match clock. |
| Ball is a sphere primitive | Placeholder until the football mesh is imported. |

## Incomplete (next up, in order)

1. **Validate the four play-resolution fixes** — held-ball-only scoring, correct return scorer/receiving drive, live turnover control/direction, and move cancellation. All 20 automated scene regression cases pass in Unity 6000.5.6f1; manual controller/full-match scenarios remain in `TESTING.md`.
2. **Loose-ball movement ownership** — pursuit/recovery exist, but some players can receive both DefenderAI and AllyBlocker movement.
3. **Handoff visuals + football mesh** — proximity transfer exists; visible exchange animation remains missing.
4. **Main menu** — no MainMenu scene; only SampleScene is enabled for builds. Winner/rematch UI exists.
5. **Customization/environment** — Kings roster is wired; Vipers roster asset is missing (meta remains). Field modifier/condition data has no active application pipeline.

## Design backlog (hard constraints when reached)

### Player attributes (12)
Passing, Speed, Blocking, Agility, Catching, Run Power, Carrying, Tackling, Coverage, D-Moves, Route Running, Swagger.
- Single-owner rule: every gameplay effect maps to exactly one stat.
- Route Running is deferred until the passing system is mature.
- Swagger drives Gamebreaker meter fill rate (name is final).

### Per-slot characters
- Each roster slot (e.g. `T1Player02`, `T2Player07`) takes on a character's attributes and likeness, NFL Street style.
- Player identity via GUID/UUID generated at character creation.
- No trades or free agency. "Create a Team" is a feature.

### Gamebreaker
- Merge NFL Street 1's flat all-stat boost for the whole possession with NFL Street 3's player-triggered activation (no cutscene).
- Applied as a temporary multiplier on top of `PlayerAttributes` / `DefenderAttributes`; cleared on `PlayState.OnPlayEnded`.
- **Open question:** defensive equivalent. Auto-fumble-on-contact versus a contain-break boost.

### Walls and fences
- Hazards only. No wall-running or traversal.
- Detect with `OverlapBox` or a short raycast in the movement direction; trigger a `StumbleMove` (`IPlayerMove`) that zeroes velocity and applies knockback/stumble. No evasion window.

### Field modifiers
- Per-field ScriptableObject (e.g. beach = speed nerf, parking lot = agility buff).
- Additive percentage modifiers with upper and lower bounds. Not multiplicative.

### Play count
- Scale offense toward ~36 plays total (NFL Street 1 benchmark: ~12 per category).

### Character appearance
- 5 body archetypes (lean / athletic / thick / heavy / massive) as 5 unique meshes on a shared skeleton.
- 4 to 5 height bands as uniform scale multipliers.
- 30 faces, 20 hairstyles, 8 facial hair styles.
- NFL Street 1 aesthetic: grounded proportions, not cartoon-exaggerated.
- Shared bone hierarchy is non-negotiable.

## Current blockers

- Complete an Editor match regression after the four resolution/control fixes. Compilation alone does not establish gameplay correctness.
- Wrong-team return scoring, unheld-ball touchdowns, competing carrier/defender claims, and interrupted-hurdle immunity have code fixes; runtime validation is recorded separately in `TESTING.md`.
- Match progression already includes downs, possession changes, first-to-21 scoring, winner UI, and scene-reload rematch. No conversions or main menu yet.

## Tooling

- Unity 6.5, URP, Input System package, `Blacktop.Scripts.asmdef`.
- Source of truth: `github.com/ludwigsen/Blacktop`.
- Developed on Windows and macOS, synced via GitHub.
- Asset sources under consideration: Mixamo (rigging), Kenney.nl (CC0 low-poly), Synty Polygon packs, Tripo AI (mesh generation), Reallusion Character Creator (face morphs).
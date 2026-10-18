# PROJECT_STATUS.md — Blacktop

Last updated: 2026-10-17

Status snapshot of what works, what's incomplete, and what's blocking. Keep this current. If it disagrees with the code, the code wins.

Items marked **(verify)** were recorded from planning notes and should be confirmed against the repo before anyone relies on them.

## Summary

Blacktop is an arcade 7v7 street football game (Unity 6.5, URP). The core loop is playable end-to-end in rough form: formation, play call, movement, passing, tackling, possession change, touchdown, win condition. The current phase is **bug-fixing the loop and filling in the missing game-flow pieces** (loose ball, out of bounds, downs), not polish.

## What works

### Movement and moves
- Transform-based player movement (no Rigidbody).
- Juke, Hurdle, and StiffArm, attribute-driven, implemented through the `IPlayerMove` pattern and orchestrated by `PlayerStateMachine`.

### Ball and possession
- Passing and pitching with a parabolic arc; `BallState` enum.
- Possession transfer.
- `PossessionController` with `ActivelyControlled` as the user-control authority.
- `PlayState.PossessionTeamId` and possession-relative field direction. **(verify completeness: the four-item "possession foundation" was in progress as of 2026-09-20.)**
- QB starts with the ball in hand (no snap).

### Offense and defense
- `BlockingCoordinator` / `AllyBlocker` for blocking. **Marked "untested" in observed play.**
- 7-defender formation via `FormationData` asset.
- Defender coordination: closest defender engages, others contain, with an LOS-break override.
- Tackle / play-state system and touchdown detection.
- Route/block rule: on Pass plays receivers run their route to completion or until a ball carrier crosses the LOS; on Run plays WRs block. Trick plays are deferred.

### Game flow
- Drives start at -10 / +10 (own side, per team direction). **(verify: fixed in the 2026-10-05 pass.)**
- Safety only when the play *ends* in the team's own end zone, not merely when the ball touches it. **(verify: same pass.)**
- Win condition: first to 21 (first team to reach the target wins; no win-by-2). **(verify implementation state.)**

### UI and feedback
- `ReceiverSelectionUI`, play-call UI, `GamebreakerHUD`.
- `CarrierHighlight`.
- `FieldLines.cs`: procedural LOS, first-down, and goal lines.

### Attributes
- `PlayerAttributes` 12-stat system with `AttributeCurves` and `FieldModifiers`. **(verify how much of the 12-stat list below is wired versus planned.)**
- `GamebreakerController` exists. **(verify: how closely it matches the spec below.)**

## Known issues

| Issue | Notes |
|---|---|
| **Out of bounds not working** | Still broken after the first fix pass on 2026-10-05. Highest-priority bug. |
| First-down line is a placeholder | `LOS + 10f` in `FieldLines.Update()`. One-line change once downs tracking exists. |
| `carryOffset` untuned | On `BallController`. Ball position relative to the carrier. |
| Blocking untested | `BlockingCoordinator` / `AllyBlocker` have not been properly play-tested. |
| Fumble recovery picks first collider | Uses the first collider in the hits array; no distance tie-breaking. |
| Defenders freeze on loose ball | Acceptable for now; needs real loose-ball behavior. |
| Score / clock | Score exists via the win condition **(verify)**; no game clock. |
| Ball is a sphere primitive | Placeholder until the football mesh is imported. |

## Incomplete (next up, in order)

1. **Out of bounds** — fix; it's a blocker for realistic drives and sideline play.
2. **Loose-ball behavior** — defenders and offense should react to a loose ball; fix first-collider recovery.
3. **`TouchdownZone` vs incomplete-pass race** — a pass that is incomplete must not score or resolve ambiguously when it lands in or near the end zone.
4. **Handoff visuals + football mesh** — a visible handoff is required. Direct snap-to-RB was explicitly rejected. Replace the sphere in `BallController` with the football `.obj`.
5. **Downs / yardage tracking** — unlocks the real first-down line.
6. **Main menu** — separate `MainMenu` scene with a Start New Game button that loads the game scene. Requested 2026-10-06. **(verify whether built.)**

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

- **Out of bounds** is the only item actively blocking realistic play-testing.
- Downs tracking blocks first-down line correctness but not core play-testing.
- No other hard blockers recorded. Everything else is sequencing.

## Tooling

- Unity 6.5, URP, Input System package, `Blacktop.Scripts.asmdef`.
- Source of truth: `github.com/ludwigsen/Blacktop`.
- Developed on Windows and macOS, synced via GitHub.
- Asset sources under consideration: Mixamo (rigging), Kenney.nl (CC0 low-poly), Synty Polygon packs, Tripo AI (mesh generation), Reallusion Character Creator (face morphs).
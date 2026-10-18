# ARCHITECTURE.md — Blacktop

How the pieces fit together. Class names below come from the project's working notes. Anything marked **(verify)** should be confirmed against the repo; if this doc and the code disagree, the code wins and this doc gets fixed.

## Design constraints that shape everything

- **Transform-based movement.** No Rigidbody, no physics sim.
- **Polled contact detection.** `OverlapBox` / `OverlapSphere` in code. No trigger callbacks.
- **Resolve live, don't cache.** Ball carrier is read from `BallController.Instance.Carrier` at every point of use.
- **Both-ways roster.** 7 players, assigned independently to offensive and defensive slots.
- **No snap.** The QB starts with the ball in hand.

See `AGENTS.md` for the full rule table.

## Layered overview

```
┌──────────────────────────────────────────────────────────┐
│ UI                                                       │
│  Play-call UI · ReceiverSelectionUI · GamebreakerHUD     │
│  CarrierHighlight · (MainMenu scene)                     │
└───────────────▲──────────────────────────────────────────┘
                │ reads state / events
┌───────────────┴──────────────────────────────────────────┐
│ Game flow                                                │
│  PlayState (PossessionTeamId, OnPlayEnded, play phases)  │
│  Drive start · safety · touchdown · win condition (21)   │
│  FieldLines (LOS, first-down, goal lines)                │
└───────────────▲──────────────────────────────────────────┘
                │
┌───────────────┴──────────────────────────────────────────┐
│ Control and coordination                                 │
│  PossessionController (ActivelyControlled)               │
│  BlockingCoordinator / AllyBlocker                       │
│  Defender coordination (engage / contain / LOS override) │
│  GamebreakerController                                   │
└───────────────▲──────────────────────────────────────────┘
                │
┌───────────────┴──────────────────────────────────────────┐
│ Player layer                                             │
│  PlayerStateMachine → IPlayerMove                        │
│    Juke · Hurdle · StiffArm · (StumbleMove, planned)     │
│  PlayerAttributes / DefenderAttributes                   │
│  AttributeCurves · FieldModifiers                        │
└───────────────▲──────────────────────────────────────────┘
                │
┌───────────────┴──────────────────────────────────────────┐
│ Data and world                                           │
│  FieldConstants (single source of field geometry)        │
│  FormationData assets · play definitions                 │
│  BallController (BallState, passing/pitching arc)        │
└──────────────────────────────────────────────────────────┘
```

Rough rule: higher layers read from lower layers. Lower layers don't know UI exists.

## Gameplay states

### Play lifecycle

1. **Pre-play / huddle.** Possession team is read from `PlayState.PossessionTeamId`. Formation and play selection operate against that team. Drives start at -10 / +10 relative to the possessing team's direction.
2. **Play reset.** `BallController.HandlePlayReset()` places the ball in the QB's hand (Pistol or Shotgun). No snap.
3. **Live play.**
   - **Pass play:** receivers run routes to completion or until a ball carrier crosses the LOS. User selects a receiver via `ReceiverSelectionUI`.
   - **Run play:** WRs block. A visible handoff is required (handoff visuals are not built yet).
   - Defense engages: the closest defender engages the carrier, others contain, with an override once the carrier breaks the LOS.
4. **Resolution.** Play ends by tackle, touchdown, incomplete pass, out of bounds, or safety. Out of bounds is **currently broken**.
5. **Play ended.** `PlayState.OnPlayEnded` fires. Anything possession-scoped (Gamebreaker boost) clears here.
6. **Next play or possession change.** Drive/score/win check runs; first team to 21 wins.

### Ball states

`BallState` (top-level enum) describes the ball's condition: in carrier's hands, in flight (pass or pitch, parabolic arc), loose. Possession transfer and fumble recovery key off it. Fumble recovery currently takes the first collider in the hits array with no distance tie-breaking. **(verify enum members.)**

### Player move states

`PlayerStateMachine` owns the active move and delegates to an `IPlayerMove`. Moves are attribute-driven. Adding a move means implementing `IPlayerMove` and registering it with the state machine, not touching existing moves.

## Managers and key components

| Component | Responsibility | Notes |
|---|---|---|
| `PlayState` | Phase of the play, `PossessionTeamId`, `OnPlayEnded` event | Central game-flow hub. Possession-relative direction depends on it. |
| `BallController` | Ball state, carrier, passing/pitching arc, play reset | `Instance` singleton. `Carrier` is the only source of truth for who has the ball. Ball is currently a sphere primitive. |
| `PossessionController` | Enables/disables per-player control behavior; exposes `ActivelyControlled` | Canonical for "which player does the user control." Because it toggles per-object behavior, those behaviors can't be global singletons. |
| `PlayerStateMachine` | Runs the current `IPlayerMove` per player | One per player. |
| `IPlayerMove` | Interface for moves (Juke, Hurdle, StiffArm, future StumbleMove) | |
| `BlockingCoordinator` / `AllyBlocker` | Assigns and runs blocking for offensive players | Untested in observed play. |
| Defender coordination | Closest engages, others contain, LOS-break override | Defenders freeze on a loose ball. |
| `GamebreakerController` / `GamebreakerHUD` | Gamebreaker meter, activation, HUD | Spec in `PROJECT_STATUS.md`. Boost clears on `PlayState.OnPlayEnded`. |
| `ReceiverSelectionUI` | Lets the user pick a receiver on pass plays | |
| Play-call UI | Formation and play selection | Operates against the current possession team. |
| `FieldLines` | Procedural LOS, first-down, and goal lines | First-down line is a placeholder (`LOS + 10f`) in `Update()`. |
| `CarrierHighlight` | Visual highlight on the ball carrier | Resolve the carrier live; don't cache. Use `MaterialPropertyBlock`. |
| `FieldConstants` | Field geometry | The only place field dimensions live. All fields share identical dimensions. |
| `FormationData` | ScriptableObject describing a formation (e.g. the 7-defender set) | Data-driven. |
| `TouchdownZone` | End zone detection | Has a known race with incomplete-pass resolution. |

## Attributes and modifiers

- `PlayerAttributes`: 12 stats (Passing, Speed, Blocking, Agility, Catching, Run Power, Carrying, Tackling, Coverage, D-Moves, Route Running, Swagger). Defense uses `DefenderAttributes`.
- `AttributeCurves`: maps stat values to gameplay effect via `AnimationCurve`s so ranges feel meaningful rather than linear.
- `FieldModifiers`: per-field adjustments. **Additive** percentage modifiers with bounds.
- Gamebreaker: a temporary multiplier applied on top of attributes for the current possession.
- **Effect stacking order (intended):** base attribute → curve → additive field modifier (clamped) → Gamebreaker multiplier. Keep it in one place so stacking can't run away.
- **Single-owner rule:** every gameplay effect is driven by exactly one stat.

## Dependency map

Reads as "depends on."

```
Play-call UI ─────────► PlayState, FormationData
ReceiverSelectionUI ──► PossessionController, BallController
GamebreakerHUD ───────► GamebreakerController
CarrierHighlight ─────► BallController.Carrier (live)
FieldLines ───────────► FieldConstants, PlayState
PlayState ────────────► FieldConstants, BallController
BallController ───────► PossessionController (control), PlayState (reset)
BlockingCoordinator ──► PlayState, BallController.Carrier (live)
Defender coordination ► BallController.Carrier (live), FieldConstants
PlayerStateMachine ───► IPlayerMove implementations, PlayerAttributes
IPlayerMove impls ────► PlayerAttributes / DefenderAttributes, FieldModifiers
GamebreakerController ► PlayState.OnPlayEnded, PlayerAttributes
PossessionController ─► BallController
```

**(verify)** Some edges are inferred from the described behavior. Circular references between `PlayState`, `BallController`, and `PossessionController` are a likely trouble spot. If they exist, route through events rather than adding more direct references.

## Scenes

- **Game scene** — gameplay.
- **MainMenu scene** — requested 2026-10-06: a basic menu with a button that loads the game scene. **(verify built.)** Both scenes must be in Build Settings.

## Assemblies

- `Blacktop.Scripts.asmdef` covers gameplay scripts. New scripts must live under it. Input System package is a dependency.

## Conventions for extending

- **New move:** implement `IPlayerMove`; read its tuning from attributes via `AttributeCurves`; expose tunables in the Inspector.
- **New formation or play:** data-driven via `FormationData` / play definitions. Don't hardcode positions.
- **New field behavior:** add geometry to `FieldConstants` only; add stat changes via a `FieldModifiers` ScriptableObject.
- **New possession-scoped effect:** clear on `PlayState.OnPlayEnded`.
- **New contact check:** polled `Overlap*` or a short raycast. No triggers.
- **New enum:** top-level, not nested.

## Planned architecture (not built)

- **StumbleMove:** an `IPlayerMove` triggered by wall/fence contact; zero velocity, apply knockback, no evasion window.
- **Downs/yardage tracker:** will feed `FieldLines` with the real first-down line.
- **Roster/character assignment:** per-slot character binding (`T1Player02`, `T2Player07`, ...) with GUID identity; each slot carries its own attributes and likeness. Create-a-Team is in; trades and free agency are not.
- **Appearance system:** 5 body meshes on a shared skeleton, height bands as uniform scale, pooled faces / hair / facial hair.
- **Handoff animation and designed run plays.**
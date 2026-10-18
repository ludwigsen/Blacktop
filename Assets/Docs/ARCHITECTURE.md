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
4. **Resolution.** Play ends by tackle, touchdown, incomplete pass, out of bounds, or safety. Held/loose-ball OOB is polled automatically; airborne balls finish flight before a ruling. `PlayState` validates held-ball touchdowns inside the legal rectangle, with the same boundary checks used before contact/fumble rolls.
5. **Play ended.** `PlayState.OnPlayEnded` fires after score-team, next possession, and whistle spot are resolved. Player moves cancel through `IPlayerMove.Cancel`, independently of successful `Exit` (which releases passes/pitches). The existing three-second presentation then gathers the next huddle. Gamebreaker persists across these ordinary whistles; scores and changes of possession end both teams' active boosts.
6. **Next play or possession change.** Drive/score/win check runs; first team to 21 wins.

### Ball states

`BallState` describes the ball's condition: in carrier's hands, in flight (pass or pitch, parabolic arc), loose. Possession transfer and fumble recovery key off it. Recovery selects the nearest eligible player, with an entity-ID tie-break. Actual enum members are `BallController.BallState.Held`, `InFlight`, and `Loose`; the existing nested enum is unchanged.

### Player move states

`PlayerStateMachine` owns the active move and delegates to an `IPlayerMove`. Moves are attribute-driven. Adding a move means implementing `IPlayerMove` and registering it with the state machine, not touching existing moves.

## Managers and key components

| Component | Responsibility | Notes |
|---|---|---|
| `PlayState` | Phase of the play, `PossessionTeamId`, `OnPlayEnded` event | Central game-flow hub. Possession-relative direction depends on it. |
| `BallController` | Ball state, carrier, passing/pitching arc, play reset | `Instance` singleton. `Carrier` is the only source of truth for who has the ball. Ball is currently a sphere primitive. |
| `PossessionController` | Enables/disables per-player control behavior; exposes `ActivelyControlled` | Canonical for "which player does the user control." Because it toggles per-object behavior, those behaviors can't be global singletons. |
| `PlayerStateMachine` | Runs the current `IPlayerMove` per player | One per player. Whistle subscription occurs in `Start`; both whistle and component disable cancel temporary move effects. |
| `IPlayerMove` | Interface for moves (Juke, Hurdle, StiffArm, future StumbleMove) | |
| `BlockingCoordinator` / `AllyBlocker` | Assigns and runs blocking for offensive players | Untested in observed play. |
| Defender coordination | Closest engages, others contain, LOS-break override | Loose-ball defense uses DefenderAI; offense uses AllyBlocker; selected defender movement stays with human control. |
| `GamebreakerController` / `GamebreakerHUD` | Gamebreaker meter, activation, HUD | PlayState owns team meters and possession-long boosts; every Player prefab listens for activation through canonical control. GameHUD displays ready/active state. |
| `ReceiverSelectionUI` | Lets the user pick a receiver on pass plays | |
| Play-call UI | Formation and play selection | Operates against the current possession team. |
| `FieldLines` | Procedural LOS, first-down, and goal lines | Reads distance from the scene's `DownsTracker`. |
| `CarrierHighlight` | Visual highlight on the ball carrier | Resolve the carrier live; don't cache. Use `MaterialPropertyBlock`. |
| `FieldConstants` | Field geometry | The only place field dimensions live. All fields share identical dimensions. |
| `FormationData` | ScriptableObject describing a formation (e.g. the 7-defender set) | Data-driven. |
| `TouchdownZone` | End zone detection | Requests a validated ruling from `PlayState`; never scores airborne or loose balls. |

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
GamebreakerController ► PossessionController.ActivelyControlled, PlayState, PlayerAttributes
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
- **Downs/yardage tracker:** already implemented as `DownsTracker` and scene-wired; listed here previously as planned.
- **Roster/character assignment:** per-slot character binding (`T1Player02`, `T2Player07`, ...) with GUID identity; each slot carries its own attributes and likeness. Create-a-Team is in; trades and free agency are not.
- **Appearance system:** 5 body meshes on a shared skeleton, height bands as uniform scale, pooled faces / hair / facial hair.
- **Handoff animation and designed run plays.**

## Play-resolution and live-turnover ownership (2026-10-17)

- `BallController.RulingPosition` resolves a held ball from the current carrier transform and carry offset, avoiding the previous rendered position before `LateUpdate`.
- `PlayState.TryGetTouchdownTeam` requires a held ball, a carrier team, and a legal end-zone position. `EndPlay` rechecks this and promotes goal-line tackles to touchdowns. `IsBallOutOfBounds` is shared with `SidelineCheck` and checked before contact can fumble.
- `LastScoringTeamId` is the actual carrier team on a touchdown. The scorer's opponent receives at its possession-relative drive start; safeties retain the existing scoring/receiving rule.
- `PossessionTeamId` remains drive-owned until the whistle. During live play, `IsUserDefending` follows the held carrier or intended receiver, so the existing defender manager releases/selects control on live recoveries. `PossessionController.ActivelyControlled` remains the input authority; carrier/defender claims preserve each other's newer assignments regardless of Update order.
- `CPUCarrierAI` uses the carrier's team direction on returns. A recovering defensive QB runs as a returner instead of executing the old offensive play call.
- `IPlayerMove.Cancel(PlayerContext)` is cleanup only. All five moves implement it; hurdle restores its starting height and clears immunity, while pass/pitch discard their pending release. Future moves must implement both completion and cancellation.


## Loose-ball movement ownership (2026-10-17)

With no carrier, `PlayState.PossessionTeamId` still identifies the drive's offense. `AllyBlocker` owns that team's scramble movement and any blocker pushback already in progress. `DefenderAI` owns the other team's pursuit and stun/pushback; its offense gate runs before any displacement. Selected defenders retain `DefenderControl`, with both AI pursuit paths deferring. `ReceiverAI` already aborts loose-ball routes, carrier movement requires a held carrier, and CPU carrier AI requires a held ball. No new manager, execution-order dependency, or movement system is introduced; recovery remains owned by BallController.


## Standalone procedural rendering dependency

FieldLines and CarrierHighlight construct URP Unlit materials at runtime. `ProjectSettings/GraphicsSettings.asset` retains `Universal Render Pipeline/Unlit` in Always Included Shaders so shader stripping cannot remove the shader or its runtime-selected transparency variants. This is a build dependency; gameplay state and procedural geometry are unchanged.


## Player model hierarchy (2026-10-18)

`Player.prefab` keeps all gameplay components and its unchanged CapsuleCollider on the root. Its capsule MeshRenderer is disabled. Visible geometry lives under `PlayerVisuals/PlayerModel/PlayerBody`: PlayerModel.prefab is the reusable visual wrapper, and PlayerBody.fbx is the GUID-preserving rename of Biped_Humanoid.fbx. The bone hierarchy/avatar are unchanged. Only the visual child receives a uniform scale and offset to align the imported rest-pose body bounds with the existing two-unit gameplay capsule; the gameplay root/collider are not resized.

The humanoid uses the existing BlacktopCharacterController idle animation with Animator root motion disabled, preserving transform-based movement. PlayerVisualFeedback's existing Renderer reference now points to the primary SkinnedMeshRenderer; MaterialPropertyBlock tinting is unchanged. SampleScene has fourteen model-backed roster players and no unassigned character preview. The QB's old model-only position overrides were cleared so it inherits the same aligned visual prefab.

Issue #2 supersedes the earlier front/back preview: gameplay showed both teams facing backwards. PlayerModel's imported visual child is now rotated 180 degrees relative to that committed state (local identity), with its small X/Z fitting offsets mirrored to preserve centering. Gameplay roots, avatar, skeleton and root-motion settings are unchanged. Confirm facing while actually running both ways; an isolated idle preview was insufficient.

## Gamebreaker ownership and lifetime

`CharacterApplier` creates a private runtime `PlayerAttributes` for each player (including the rosterless Balanced fallback), binds its team ID, distributes it before other Awakes, and destroys it with the player. `PlayerAttributes.Effective` resolves that team's active multiplier live from `PlayState`; callers must not also pass the team multiplier as the optional explicit multiplier. Shared data assets never hold mutable team state.

`GamebreakerController` remains enabled per player but gates spending through `PossessionController.ActivelyControlled` and the user team. Styling additionally requires the held live ball. `CPUPlayCaller` explicitly activates the CPU team's full meter during live play. `PlayState.TryActivateGamebreaker` guards live/full/inactive state and publishes meter changes for READY/ACTIVE feedback. `GamebreakerBuffs` now stores one flat multiplier; the checked-in asset is migrated to 1.2. AI movement paths that bypass ratings apply only this live team boost, preserving neutral speed defaults.

`ApplyPossession`, scoring resolution and `EndGame` end active boosts. Neither a normal down ending nor a loose-ball drop ends the possession. Defensive effects use the existing capped attribute/probability checks; there is no pending guaranteed-turnover state. Unspent meter stays team-owned across possession changes; scene reload resets the match. The PlayMode test assembly references `Unity.InputSystem` to exercise real keyboard/gamepad action bindings.


## Scorebug and pre-game branding (2026-10-18)

`PlayState` already owns the two serialized `TeamIdentity` references; `GameHUD.Start` reads them after all Awakes. Each identity is a Unity ScriptableObject configuration asset (Create > Blacktop > Team Identity), containing the team name, explicitly matched logo sprite, primary accent color and secondary accent color. Configure these assets and assign them to PlayState before launching the match. Existing Kings/Vipers assets are wired; adding future teams requires no name-based lookup or roster changes. Names can change without losing the chosen logo. The full Art/Logos library imports as single UI sprites.

`TeamScorePanel` builds mirrored horizontal layouts. Neutral `scorebug_base.png` is tinted independently per panel; only background geometry is flipped. Primary color drives paint and segmented meter fill; secondary color drives the accent rule and meter frame. Empty, charging, READY and ACTIVE retain existing PlayState meter semantics. It owns/destroys a private Old London SDF outline material shared by its name and score, leaving the source font material unchanged. HUDTheme exposes soft-white text color, outline thickness, logo scale/outward tilt, outward name shift and name/score spacing. Names and scores use TMP bold weight. Default logos scale to 125% with mirrored 20-degree outward tilts; names move outward by 10% of the original name-slot width, and scores follow inward slot edges with reduced spacing. Supporting HUD and play-call copy use Outfit. SampleScene serializes the new layout/font sizes; changing C# defaults alone does not update serialized scene values.

Branding has no reference to roster or ratings. The intended roster/attribute relationship is the player GUID as the foreign key; the current TeamRoster still embeds ratings, tracked as deferred data-authoring work in PROJECT_STATUS.md.

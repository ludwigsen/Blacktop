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
- Loose-ball movement ownership: drive offense uses AllyBlocker, defense uses DefenderAI, selected defenders retain human input, and shove recovery advances under the same owner.
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
| Downs and first-down marker | Automated match cases verify first downs, incompletions, OOB gains and fourth-down possession changes; visual line placement still needs manual review. |
| `carryOffset` untuned | On `BallController`. Ball position relative to the carrier. |
| Blocking untested | `BlockingCoordinator` / `AllyBlocker` have not been properly play-tested. |
| Gamebreaker wiring/lifetime | Player controller is absent from inspected scene/prefab stacks; active lifetime differs from documented intent. Deferred. |
| Score / clock | ScoreBoard and play-clock HUD exist; no match clock. |
| Ball is a sphere primitive | Placeholder until the football mesh is imported. |

## Incomplete (next up, in order)

1. **Normal standalone gameplay pass** — owner verified the prior four-fix Editor gameplay. The expanded 28-case suite passes in both Editor and a macOS standalone player, including matches to 21 and rematches. Manual controller/graphics checks of the normal app remain.
2. **Loose-ball gameplay feel** — ownership is now exclusive; manually verify scrum readability and recovery/control transfer.
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

- No automated match-loop blocker remains in the 28-case Editor/macOS player suite. Finish a manual controller-played match/rematch in the normal app before treating game feel and visuals as verified.
- Wrong-team return scoring, unheld-ball touchdowns, competing carrier/defender claims, and interrupted-hurdle immunity have code fixes; runtime validation is recorded separately in `TESTING.md`.
- Match progression already includes downs, possession changes, first-to-21 scoring, winner UI, and scene-reload rematch. No conversions or main menu yet.

## Tooling

- Unity 6.5, URP, Input System package, `Blacktop.Scripts.asmdef`.
- Source of truth: `github.com/ludwigsen/Blacktop`.
- Developed on Windows and macOS, synced via GitHub.
- Asset sources under consideration: Mixamo (rigging), Kenney.nl (CC0 low-poly), Synty Polygon packs, Tripo AI (mesh generation), Reallusion Character Creator (face morphs).

## Match-loop validation follow-up (2026-10-17)

- Corrected SampleScene's T2Player02 team override to target its current Player prefab TeamMember. The old override pointed at a missing source component, leaving this player on Team 0 and Team 1 without OL2. A runtime roster test now requires one of every slot per team.
- Expanded PlayResolutionTests with offense/defense scramble ownership, shove recovery, selected-defender input, roster completeness, and two scripted matches to the actual default target of 21 plus the shipped GameHUD rematch path.
- Full-match tests accelerate runtime presentation only and arrange terminal situations deterministically. They verify rules/events/state progression, not human controller input or visual/game-feel quality. Editor/player results are recorded in TESTING.md after execution.
- Standalone validation exposed a stripped runtime-only URP Unlit shader used by FieldLines and CarrierHighlight. GraphicsSettings now retains that shader explicitly; no procedural-rendering rewrite or material changes were needed.
- Final follow-up results: 28/28 Editor Play Mode and 28/28 macOS player tests pass. Both match winners/rematches are covered in each environment; test reports are in ignored Builds/Validation. Windows and human controller play of the new changes are not claimed as tested.
- Normal macOS app is built locally at Builds/macOS/Blacktop.app (ignored by Git), with zero build errors; headless scene startup produced no runtime exceptions. Build warnings are primarily AI inference compute-shader variants. Manual normal-app graphics/controller validation and Windows builds remain outstanding.


## Humanoid visual replacement (2026-10-18)

- All fourteen players now display the imported humanoid instead of a capsule mesh; the invisible gameplay CapsuleCollider and player-root movement/contact ownership are unchanged.
- Renamed Biped_Humanoid.fbx to PlayerBody.fbx and BlacktopCharacter.prefab to PlayerModel.prefab, preserving their .meta GUIDs. Shared skeleton/bones remain unchanged. The hierarchy reads PlayerVisuals > PlayerModel > PlayerBody.
- Fixed visual offsets, including stale QB scene overrides; the body is uniformly fitted to the existing two-unit collider. Removed the extra unassigned scene preview.
- Existing idle controller is wired to the visual model with root motion disabled; state tint now targets the skinned body. Running/throwing/catching/tackling animation coverage remains unfinished; this task does not add animation clips.
- Unity asset validation passed for all fourteen players (bounds/alignment, colliders, valid material references, body feedback, humanoid avatar, idle controller and disabled root motion). All 28 existing Editor Play Mode regressions passed with the replacement assets. Visual preview/build checks are recorded in TESTING.md.

The imported body faces local -Z; PlayerModel rotates only its visual child 180 degrees to face the gameplay root's +Z. The animated front/back preview confirmed the corrected facing and existing idle pose.

The normal macOS app was rebuilt successfully with the final model assets (0 errors; 440 existing build warnings). Output: `Builds/macOS/Blacktop.app`. The rendered animated front/back preview is saved locally at `Builds/Validation/PlayerModel-preview.png`; Editor test results are at `Builds/Validation/character-playmode.xml`.

The refreshed app passed a headless scene-startup smoke check with no logged runtime exceptions, then was stopped intentionally. Manual controller play, running/football animation coverage, and Windows remain unverified for this visual change.

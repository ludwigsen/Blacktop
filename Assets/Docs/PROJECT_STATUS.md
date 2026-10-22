# PROJECT_STATUS.md — Blacktop

Last updated: 2026-10-18

Status snapshot of what works, what's incomplete, and what's blocking. Keep this current. If it disagrees with the code, the code wins.

Items marked **(verify)** were recorded from planning notes and should be confirmed against the repo before anyone relies on them.

## Summary

Blacktop is an arcade 7v7 street football game (Unity 6.5, URP). The core loop is playable end-to-end in rough form: formation, play call, movement, passing, tackling, possession change, touchdown, win condition. The current phase is **validating match correctness and filling in presentation/content gaps** (the core downs, OOB, and loose-ball paths exist), not polish.

## What works

### Movement and moves
- Transform-based player movement (no Rigidbody).
- Juke, Hurdle, and StiffArm, attribute-driven, implemented through the `IPlayerMove` pattern and orchestrated by `PlayerStateMachine`.
- All fourteen players inherit speed/direction-driven standard run, fast run and backward run through PlayerAnimationPresenter and the shared Animator Controller. Jump presents the existing hurdle, timed to its duration; no root motion or movement/attribute tuning changes.

### Ball and possession
- Passing and pitching with a parabolic arc; `BallState` enum.
- Possession transfer.
- `PossessionController` with `ActivelyControlled` as the user-control authority.
- `PlayState.PossessionTeamId` owns drive direction; live carrier ownership drives return control/direction before the whistle.
- QB starts with the ball in hand (no snap).
- Street-inspired football replaces the visible sphere: Blender source, Unity FBX, raised laces, leather maps, Blacktop stamp, and reusable FootballVisual prefab. Two FootballStyle presets (Street Classic / Backlot Leather) are selectable through FootballAppearance; a player-facing picker/persistence is deferred.

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
- Scorebug uses a horizontal crest/name/score/meter composition, bold Old London names/scores with soft-white faces, black SDF outlines and tighter spacing; crests are enlarged 25% and tilted 20 degrees outward, and Outfit supporting copy. Pre-game TeamIdentity assets supply names, matching Kings/Vipers crests, primary paint/meter colors and secondary rules/frame colors. All 16 logo imports are UI sprites. Visual validation is recorded in TESTING.md.
- `CarrierHighlight`.
- `FieldLines.cs`: procedural LOS, first-down, and goal lines.

### Attributes
- `PlayerAttributes` 12-stat system with `AttributeCurves` and `FieldModifiers`. **(verify how much of the 12-stat list below is wired versus planned.)**
- Gamebreaker input is on the shared Player prefab. Human offense/defense activate with G or gamepad LB/L1; CPU spends its own full meter at the next live opportunity.
- Both sides use the same flat all-attribute boost (Inspector-tunable 1.2 multiplier, existing attribute hard caps still apply), lasting until the possession ends. Team-owned runtime attributes cover AI and controlled players.
- Issue #2: PlayerModel visual body rotated 180 degrees from its committed backwards orientation; gameplay roots, skeleton and collider stay intact. Manual running-direction review remains required.

## Known issues

| Issue | Notes |
|---|---|
| OOB / goal-line rulings need Editor validation | Automatic OOB polling exists. Held-ball goal-line and sideline checks now share live ruling position and contact precedence; test both directions and corners. |
| Downs and first-down marker | Automated match cases verify first downs, incompletions, OOB gains and fourth-down possession changes; visual line placement still needs manual review. |
| Ball carry pose provisional | Scene carryOffset is fitted to the smaller football; hand-bone grip and a carry animation remain to align it precisely with the fingers. |
| Blocking untested | `BlockingCoordinator` / `AllyBlocker` have not been properly play-tested. |
| Gamebreaker balance/readability | Wiring and possession lifetime implemented; redesigned scorebug shows READY / ACTIVE. Manual balance/readability playtest remains. |
| Score / clock | ScoreBoard and play-clock HUD exist; no match clock. |
| Football style picker | Style assets and selection API exist; player-facing pre-game UI and saved selection are not built. |

## Incomplete (next up, in order)

1. **Normal standalone gameplay pass** — owner verified the prior four-fix Editor gameplay. The expanded 39-case suite passes in both Editor and a macOS standalone player, including matches to 21 and rematches. Manual controller/graphics checks of the normal app remain.
2. **Loose-ball gameplay feel** — ownership is now exclusive; manually verify scrum readability and recovery/control transfer.
3. **Visible handoff** — football mesh is integrated; proximity transfer still needs an observable QB-to-back exchange.
4. **Football animation readability** — running/backpedaling/fast-run and hurdle clips are wired. Carry grip, throwing, catching and tackle reactions remain unfinished; controller feel, foot sliding and transition tuning still need manual playtesting.
5. **Main menu** — no MainMenu scene; only SampleScene is enabled for builds. Winner/rematch UI exists.
6. **Roster data separation** — TeamIdentity branding is separate, but TeamRoster.PlayerRecord still embeds the 12 ratings. Move ratings into separate data keyed by the roster player GUID when implementing roster/attribute authoring; this HUD change does not migrate player data.
7. **Customization/environment** — Kings roster is wired; Vipers roster asset is missing (meta remains). Field modifier/condition data has no active application pipeline.

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
- Applied once in team-bound `PlayerAttributes.Effective`, after field addition and before hard caps. A possession spans multiple downs: clear both active boosts on a score, possession change (including fourth-down failure / opponent fumble recovery), or game over. Ordinary whistles and same-team fumble recoveries preserve it. Unspent meter remains with its team.
- Defensive decision: player-triggered flat team boost, same as offense; better speed, coverage and tackling improve existing outcomes. No automatic spending and no guaranteed turnover. Both teams may activate in the same possession. Active teams cannot refill until their boost ends.
- Swagger scales styling and actor-attributed move/sack/fumble/interception/touchdown earnings. CPU earns from existing gameplay awards; it does not simulate holding Style.
- Styling: hold T or gamepad X/Square while the controlled player holds the live ball. Activation: G or LB/L1 during live play, full meter only, including while controlling a defender. No cutscene.

### Walls and fences
- Hazards only. No wall-running or traversal.
- Detect with `OverlapBox` or a short raycast in the movement direction; trigger a `StumbleMove` (`IPlayerMove`) that zeroes velocity and applies knockback/stumble. No evasion window.

### Field modifiers
- Conditions and severity are selected independently of venue. FieldDefinition contains art/identity only; do not derive a speed/agility effect from asphalt, grass or any surface.
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

- No automated match-loop blocker remains in the 39-case Editor/macOS player suite. Finish a manual controller-played match/rematch in the normal app before treating game feel and visuals as verified.
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

Issue #2 supersedes the earlier preview: gameplay exposed backwards facing on both teams. The visual body's rotation is corrected by 180 degrees relative to that committed state, with fitting offsets mirrored. Automated checks verify all fourteen players inherit the correction; manual running-direction confirmation is still required.

The normal macOS app was rebuilt successfully with the final model assets (0 errors; 440 existing build warnings). Output: `Builds/macOS/Blacktop.app`. The rendered animated front/back preview is saved locally at `Builds/Validation/PlayerModel-preview.png`; Editor test results are at `Builds/Validation/character-playmode.xml`.

The refreshed app passed a headless scene-startup smoke check with no logged runtime exceptions, then was stopped intentionally. Manual controller play, running/football animation coverage, and Windows remain unverified for this visual change.

## 2026-10-18 — Gamebreaker and issue #2 validation

- Unity 6000.5.6f1 isolated-project Editor Play Mode: **39/39 passed**.
- macOS standalone test player: **39/39 passed**. Includes the prior 28 resolution/match/rematch cases plus 11 Gamebreaker/input/prefab cases.
- New coverage: real G/LB input on offense and defense, one controlled-team activation despite fourteen listeners, underfilled/dead/duplicate/game-over rejection, all twelve team-owned attribute multipliers and caps, defensive coverage/fumble odds without guaranteed outcomes, boosts across downs through fourth-down turnover, CPU activation on both sides, fourteen inherited model/input stacks.
- Synthetic input devices explicitly route to gameplay in the headless Editor; test settings are restored. This does not substitute for a physical-controller playtest.
- Full diff review and `git diff --check` passed. Final UI design, human gameplay balance/readability, visual running-direction review and Windows validation remain manual work.
- Normal macOS development app rebuilt successfully: zero build errors, 408 reported warnings (including obsolete Unity API warnings). Headless startup loaded the match scene without logged exceptions; stopped after startup. Physical controller, graphics and gameplay feel were not checked by that smoke run.


## Shared locomotion/hurdle animation validation (2026-10-18)

- Jump, Running Backward, Standard Run and Fast Run FBXs import as valid Humanoid clips; the original PlayerBody skeleton/avatar is retained across all fourteen players.
- Shared PlayerAnimationPresenter observes actual root motion from every movement owner. The shared controller blends backward/idle/standard/fast poses and fits Jump into the existing hurdle duration, with normal cancellation on whistle/control loss.
- Corrected the new clips' Original Y reference to match the fitted model/idle and extracted Jump's trajectory to avoid adding a second vertical arc.
- Final isolated Editor suite: **44/44 passed**, zero failures/skips, including all prior 40 match/Gamebreaker/football cases and four new animation cases. Rendered poses inspected. Physical controller/foot-sliding tuning, Windows and a refreshed standalone build remain manual work.
- Football lettering controls (LETTERING, LETTERING_WIDTH, LETTERING_FONT) are exposed near the top of SourceAssets/Football/build_football.py; SourceAssets/Football/README.md documents running the builder in Blender and exporting Unity's FBX.

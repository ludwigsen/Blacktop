# PROJECT_STATUS.md — Blacktop

Last updated: 2026-11-06

Status snapshot of what works, what's incomplete, and what's blocking. Keep this current. If it disagrees with the code, the code wins.

Items marked **(verify)** were recorded from planning notes and should be confirmed against the repo before anyone relies on them.

## Summary

Blacktop is an arcade 7v7 street football game (Unity 6.5, URP). The core loop is playable end-to-end in rough form: formation, play call, movement, passing, tackling, possession change, touchdown, win condition. The current phase is **validating match correctness and filling in presentation/content gaps** (the core downs, OOB, and loose-ball paths exist), not polish.

## What works

### Movement and moves
- Transform-based player movement (no Rigidbody).
- Juke, Hurdle, and StiffArm, attribute-driven, implemented through the `IPlayerMove` pattern and orchestrated by `PlayerStateMachine`.
- All fourteen players inherit speed/direction-driven standard run, fast run and backward run through PlayerAnimationPresenter and the shared Animator Controller. Jump presents the existing hurdle, timed to its duration; no root motion or movement/attribute tuning changes.
- Issue #2: current master's existing shared-prefab orientation is correct. Rendered Editor gameplay verifies model-to-root alignment on both human sides and in both attacking directions, including turns, routes, blocking, pursuit, huddles and backward locomotion. No additional rotation is needed. See T-72 in TESTING.md for the automated evidence and separate human/platform limits.

### Ball and possession
- Forward passes use constant-gravity ballistic motion and a velocity-aligned spiral,
  preserving caller-owned targets, Passing effects and arrival times. Street pitches
  retain their runner-tracking authored arc and guaranteed completion.
- Live fumbles pop from the current held position and immediately enter `Loose`:
  both teams can recover in the air or on the ground. Swept transform contacts handle
  players, solid environment colliders and ground; bounce/tumble/roll lose energy
  until settling. BallController remains the only attachment/recovery authority.
- Distinct ground glyphs track the live loose ball (ring) and committed pass target
  (diamond/cross), clearing on resolution/phase exit. No Rigidbody or trigger callback.
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
- MainMenu is the initial build scene: New Game (Head 2 Head / Pickup Game), Players, Quit. Both exhibition modes share field/condition/severity/difficulty settings and a concrete matchup review. Head 2 Head validates saved active lineups; Pickup drafts seven humans and seven unique CPU players from all saved rosters/reserves, with independent offense/defense overrides and match-only branding. Both bind the existing fourteen match actors. Escape / gamepad Start returns from gameplay; rematch retains the selected configuration.
- Players supports create/edit/duplicate/transfer/delete, seven active players plus unlimited reserves, and independent offense/defense assignment. A working-copy editor covers identity, twelve heights, five builds, derived weight, appearance/gear selections and all twelve existing 0–20 ratings, with visual-only live preview and save/cancel/reset.
- Runtime rosters use atomic versioned JSON with backup recovery. Canonical source commit is a separate confirmed Editor-only operation. See FRONT_END.md for setup, persistence and art replacement.
- `ReceiverSelectionUI`, play-call UI, unified `GameHUD` scorebug and winner/rematch overlay (scene-wired).
- Scorebug retains the horizontal crest/name/score/meter composition with clean angular geometry, Barlow Condensed ExtraBold names/scores and SemiBold supporting copy. TeamIdentity still supplies matching Kings/Vipers crests and independent accent/meter colors; both UI themes share this geometry. Live score, play clock, downs, field position, possession marker and Gamebreaker are bound to existing state. See UI_OVERHAUL.md and TESTING.md.
- `CarrierHighlight`.
- #12: GameplayLabelLayout coordinates four actionable receiver cards with controlled,
  held and inbound-player identity. Actual screen bounds are packed below the
  scorebug, above menu controls and away from the ball; displaced cards link to their
  actors. Duplicate receiver names and non-actionable live nameplates are suppressed.
- `FieldLines.cs`: procedural LOS, first-down, and goal lines.

### Attributes
- Exhibition conditions now affect both teams through one immutable match snapshot: percentage-point conversion once, bounded additive effects, existing hard caps, no duplicate Gamebreaker. Acceleration remains Agility-owned with a separate condition boundary.
- Match difficulty changes CPU decisions/execution rather than roster ratings; Normal retains baseline decision multipliers.
- All twelve ratings on the supplied 42 players reach live gameplay through private
  `PlayerAttributes` and the existing curves/modifiers. Blocking and D-Moves now
  affect distinct recovery timers; #14 tracks manual balance.
- Gamebreaker input is on the shared Player prefab. Human offense/defense activate with G or gamepad LB/L1; CPU spends its own full meter at the next live opportunity.
- Both sides use the same flat all-attribute boost (Inspector-tunable 1.2 multiplier, existing attribute hard caps still apply), lasting until the possession ends. Team-owned runtime attributes cover AI and controlled players.

### Character appearance foundation
- Editable Blender source is now supplied for all current geometry: five bodies with integrated faces, fifteen garment meshes and four eyes, with shared rest rig/UVs/weights. Open `SourceAssets/Characters/BlacktopCharacters.blend`; per-build and head-focused snapshots are also supplied. The Editor bridge returns artist geometry edits to existing Unity mesh IDs/bind poses. Future character mesh authoring must retain `.blend` source. See SourceAssets/Characters/README.md.
- Optional roster appearance references preserve GUIDs/ratings; rosterless players use the prefab default. PlayerVisualAssembler/PlayerVisualRig assemble below PlayerVisuals without resizing the gameplay root/collider.
- Stable preset/palette IDs, five body archetypes, twelve discrete heights/five derived bands, safe fallback, shared-bone attachment slots, authored skinned LOD support, property-block tinting and optional garment number labels.
- The original catalog remains compatible. The front-end catalog supplies five distinct temporary regional body meshes and fitted temporary top/shorts/footwear shells on the same skeleton. Heavy emphasizes waist mass; Massive emphasizes chest/shoulders/limbs. These are provisional silhouettes, not production anatomy/clothing. Three face IDs share the same integrated head; cropped hair/beard and additional gear are explicitly missing art.
- See `MODULAR_CHARACTERS.md` for the audit, canonical rig, art/import contract and workflow. Editor Play Mode suite: **62/62 passed**, including field checks and seven character tests.

## Known issues

| Issue | Notes |
|---|---|
| OOB / goal-line rulings need Editor validation | Automatic OOB polling exists. Held-ball goal-line and sideline checks now share live ruling position and contact precedence; test both directions and corners. |
| Downs and first-down marker | Automated match cases verify first downs, incompletions, OOB gains and fourth-down possession changes; visual line placement still needs manual review. |
| Ball carry pose provisional | Scene carryOffset is fitted to the smaller football; hand-bone grip and a carry animation remain to align it precisely with the fingers. |
| Blocking untested | `BlockingCoordinator` / `AllyBlocker` have not been properly play-tested. |
| Blocking / D-Moves feel (#14) | Previously unused ratings now own block-contact recovery cadence and recovery from block knockback respectively. Neutral timers and the existing power/resistance contest are retained; manual engagement/balance acceptance is still needed. |
| Gamepad action bindings (#13) | Master has no gamepad Pass/Pitch binding. The owner explicitly deferred input changes to #13 during the #12 fix. Existing 1–4 receiver keys remain unchanged; no fake controller-button glyphs are shown. |
| New-team branding | Ravens, Wolves, Outlaws and Reapers have installed identities/colors and seven supplied players each. Custom crests are not provided; the scorebug handles an absent crest. Production character art remains #15. |
| Gamebreaker balance/readability | Wiring and possession lifetime implemented; redesigned scorebug shows READY / ACTIVE. Manual balance/readability playtest remains. |
| Score / clock | ScoreBoard and play-clock HUD exist; no match clock. |
| Football style picker | Style assets and selection API exist; player-facing pre-game UI and saved selection are not built. |
| Modular character art | Five temporary bodies and fifteen temporary garment shells work. Integrated prototype head/eye skins remain; no distinct face geometry, hairstyle/beard art, authored production garments or LOD meshes. Additional gear slots are labelled missing. Garment numbers have no renderer; match nameplates show the saved number. Production anatomy, deformation, fit and prototype model provenance/licence require art review. |
| Exhibition tuning/manual follow-up | Complete controller-played Head 2 Head and Pickup matches, inspect 720p/gamepad focus and same-source mixed squad colors, balance condition feel and CPU tiers. CPU drafting is deferred until final review so it uses the resolved difficulty; review exposes both lineups before launch. No production art or platform validation is implied. |
| Front-end platform/performance follow-up | Editor UI and normal macOS startup are validated; physical controller feel and Windows remain manual. Temporary garments raise the selected fourteen-player sample from 70 to 112 skinned renderers. No frame-rate budget is claimed. |
| Direct SampleScene development entry | Direct entry now binds the first two authored TeamDefinitions and independent lineups before CharacterApplier runs, so retired prototype GUID overrides cannot shadow the CSV. Use MainMenu for saved/custom rosters and all six teams. |
| Ball-change follow-up across open issues | See ISSUE_PRIORITIES.md: #12 crowded labels/ground glyphs; #14 airborne scrum timing, initial release-volume clearance and control feel; #17 authored wall/fence colliders on gameplay roots; #11 hand-grip/release presentation; #13 physical-controller/Windows/macOS acceptance. These tickets remain open. |
| Standalone shutdown resource warning | The #3 build's bounded MainMenu startup passed, but intentional shutdown logged FrontEndBackdrop.OnDestroy releasing an active RenderTexture. That unchanged front-end cleanup path needs a separate exit/scene-teardown check during #13; no ball scene was entered in this smoke run. |

## Incomplete (next up, in order)

1. **Normal standalone gameplay pass** — owner verified the prior four-fix Editor gameplay. The expanded 39-case suite passes in both Editor and a macOS standalone player, including matches to 21 and rematches. Manual controller/graphics checks of the normal app remain.
2. **Loose-ball gameplay feel (#14)** — ownership is exclusive; #3 adds moving/airborne fumbles and ground glyphs. Recheck scrum readability, release-contact clearance and recovery/control transfer with physical input.
3. **Visible handoff** — football mesh is integrated; proximity transfer still needs an observable QB-to-back exchange.
4. **Football animation readability** — running/backpedaling/fast-run and hurdle clips are wired. Carry grip, throwing, catching and tackle reactions remain unfinished; controller feel, foot sliding and transition tuning still need manual playtesting.
5. **Front-end manual acceptance** — finish a physical-controller match with an edited roster, confirm all gameplay actions and restart persistence on Windows. MainMenu, roster editing and both exhibition modes are implemented; automated integration includes both human sides, rematch and return to menu.
6. **Roster authoring** — identity/ratings deliberately remain in the existing GUID-keyed PlayerRecord. Runtime appearance DTOs omit Unity asset references; explicit source commit creates per-GUID appearance assets. New-team creation is deferred.
7. **Exhibition balance/acceptance** — ConditionData now feeds bounded match-scoped effects through PlayerAttributes, with independent Accel handling. Four data-driven difficulties affect real CPU pursuit, coverage, offensive decisions and draft ranking. Conditions, difficulty, the six-team draft pool and physical controller play still need manual acceptance.

8. **Character vertical slice art** — author a headless Athletic body, three distinct faces, one cropped hairstyle and one beard on the documented rig. Measure height/profile anatomy and validate missing football-action clips before growing the catalog.

## Design backlog (hard constraints when reached)

### Player attributes (12)
Passing, Speed, Blocking, Agility, Catching, Run Power, Carrying, Tackling, Coverage, D-Moves, Route Running, Swagger.
- Single-owner rule: every gameplay effect maps to exactly one stat.
- Route Running owns the receiver's adjustment radius for an incoming throw.
- Swagger drives Gamebreaker meter fill rate (name is final).

### Per-slot characters
- Each roster slot (e.g. `T1Player02`, `T2Player07`) takes on a character's attributes and likeness, NFL Street style.
- Player identity via GUID/UUID generated at character creation.
- Development roster transfers are supported; no trading economy or free agency. "Create a Team" remains a future feature.

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
- Five derived height bands containing twelve explicit centimetre selections. Calibrated visual scaling around the feet; no arbitrary limb-bone scaling or rating changes.
- 30 faces, 20 hairstyles, 8 facial hair styles.
- NFL Street 1 aesthetic: grounded proportions, not cartoon-exaggerated.
- Shared bone hierarchy is non-negotiable.

## Current blockers

- No automated match-loop blocker remains in the 39-case Editor/macOS player suite. Finish a manual controller-played match/rematch in the normal app before treating game feel and visuals as verified.
- Wrong-team return scoring, unheld-ball touchdowns, competing carrier/defender claims, and interrupted-hurdle immunity have code fixes; runtime validation is recorded separately in `TESTING.md`.
- Match progression includes downs, possession changes, first-to-21 scoring, winner UI, scene-reload rematch and a return to MainMenu. Conversions remain deferred.

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

## Modular character foundation validation (2026-10-22)

- Started from verified current master `109633a`, including the merged field system. Scene, field assets, existing player GUIDs/ratings, gameplay scripts, controller and FBX imports remain unchanged.
- Unity 6000.5.6f1 isolated Editor Play Mode: **62 passed, 0 failed, 0 skipped**. Includes prior gameplay/animation, field checks and seven character tests covering sixty data combinations, deterministic IDs, roster spawn integration, attachment/LOD binding, team colors, invalid data and bind poses.
- This does not claim real physique/face variation, missing action-animation deformation, Windows, physical controller play or production frame-rate validation. Those require outstanding art/platform checks.


## Exhibition modes validation (2026-10-24)

- Head 2 Head and Pickup use the existing gameplay scene/bootstrap, detached GUID snapshots and independent lineup assignments. Shared settings, bounded conditions, acceleration ownership and four meaningful CPU profiles are implemented. No persistent pickup assets/save schema change.
- Graphics-enabled Unity 6000.5.6f1 Editor Play Mode: **97/97 passed**, including all prior regressions, real menu launch, both human sides, complete exhibition matches to 21, shipped winner/rematch and cross-mode condition reset. Native 1080p screens inspected, including full pickup scorebug names.
- Normal macOS development build: **0 errors / 438 warnings**; graphics-enabled Metal MainMenu startup passed and was intentionally stopped. No physical-controller, Windows, 720p readability or human-played standalone match acceptance is claimed. See TESTING.md and FRONT_END.md.


## Main menu / player lab prototype (2026-11-02)

Implemented: centered Blacktop Pro Duo branding; Barlow Condensed menu text; New Game, Edit Players, Settings and confirmed Quit; eleven owner-supplied 3840x2160 cinematic field views with continuous crossfades; submenu blur and directional slide/fade transitions. Settings exposes persisted SFX volume and Application.version. New Game uses the existing real match setup and gameplay scene.

Player Summary has a visual-only player on the left, all twelve square-block cold-to-hot ratings on the right, and a seven-card paged roster below. Separate Attributes and Fit & Body pages keep the lab free of scroll views. Fit & Body includes body, face, hair, fit, accessories and developer identity/lineup pages. The existing roster tools remain accessible to development users. Saves use the existing atomic roster transaction; source assets require the existing explicit commit action. Developer weight overrides are saved appearance metadata; builds still control the visible silhouette.

Known prototype limits: face IDs still share the prototype head; most hair/facial-hair/accessory options await art. Field images are concept backdrops, not screenshots of playable venues: campus is not registered as a playable venue. The eleven supplied cinematic PNGs are copied unchanged at native 3840x2160; texture imports preserve those dimensions. SFX currently controls the new menu clicks; gameplay audio remains unimplemented. This branch starts at master and intentionally does not include the separate parametric-body branch or its local sculpt work. Physical-controller feel and Windows acceptance remain manual checks.

Validation captured: 102/102 full Play Mode regression tests; 14/14 focused tests after final visual adjustments; inspected 1080p/720p native menu renders; successful macOS development build and standalone startup. After unlocking the Mac, visible mouse/keyboard testing caught and corrected a standalone overlay blur-pass issue and a stale derived-weight textbox. Actual lab navigation, rating edits/discard, appearance preview changes, SFX value changes, Head 2 Head launch/return, Pickup entry and Quit were exercised. See TESTING.md T-108 and ignored Builds/Validation for evidence.

Standalone follow-up outside menu scope: at 1280x720, gameplay nameplates/pass prompts overlap the upper scoreboard area and other players. Record this for the existing gameplay HUD readability pass; this menu change does not resize gameplay labels.

4K art replacement (2026-11-03): the previous four-image set is replaced by all eleven supplied cinematics, copied unchanged and explicitly ordered. Graphics-enabled focused menu tests passed 5/5; the updated macOS build and native sharp/crossfade/submenu-blur checks passed. Image provenance/order is recorded in Art/MainMenu/README.md; detailed evidence is in TESTING.md T-108.


## UI overhaul and themes (2026-11-04)

Working: Street Classic (default) / After Dark live switching and persistence through
Settings, shared semantic palette/type bindings, actual Barlow Regular/SemiBold/
ExtraBold/BoldItalic, existing Blacktop Pro Duo wordmark, angular buttons/cards,
preview swatches, large match setup cards, vertical customization accordions,
20-cell fixed-tier attribute bars, clean scorebug and read-only play schematics.
Secondary-page branding now has a tunable **54-reference-pixel top margin**; page
content is lowered to respect the header. All existing roster/save/GUID, preview,
Head 2 Head/Pickup, condition/difficulty/Random and gameplay integration remains.

Captured final regression: graphics-enabled Unity 6000.5.6f1 full Play Mode **117
passed / 0 failed / 0 skipped**, **140.779226 seconds**. Includes actual resolved
touchdown/first-down announcement priority in both themes. Real canvas renders cover
both themes at 720p/1080p/1440p/4K; glyph clearance, fixed action bounds, exact rating
fills and focus-revealed accordions are asserted. The earlier focused theme/menu
run passed 19/19. See TESTING.md and ignored Builds/Validation/ui-release.*.

Missing optional art: AsphaltGrain, DryBrush, SprayScatter, EdgeWear masks. Reusable
sprite slots and native geometry remain functional without them. Existing provisional
character/venue/play content, in-world label overlap, physical-controller feel,
Windows and human-played standalone matches remain acceptance gaps. No final texture
art or manual platform/gameplay acceptance is implied by automated tests.


Final macOS development build: **0 errors / 408 reported warnings**, 435,525,615
bytes reported (the earlier build reported 438 warnings). Actual standalone native
checks exercised keyboard and mouse navigation, immediate Street Classic/After Dark
switching, background cycling/blur, secondary-logo clearance, and After Dark retained
after a full app restart. Street Classic was restored and the app left on Main.
No runtime exception was found in the captured startup/settings Player log. No roster
edits were saved. This does not claim a physical-controller or human-played full match.
Build and native evidence: ignored Builds/Validation/ui-release-build.log and
ui-native-player.log; screenshots are in the chat computer-use record.


## Ball flight, live fumbles and issue ranking (2026-11-06)

- Issue #3 adds ballistic forward passes/spiral, swept live fumble contacts,
  airborne/ground recovery and distinct ground glyphs. The public Throw signature,
  caller-owned targets/Passing/time, pitch convention, catch/interception chances,
  canonical control and match rules remain. Old independent pass arc-height and
  preset fumble-duration controls are retired; gravity/pop height determine loft/time.
- Full graphics-enabled Unity 6000.5.6f1 Play Mode: **137/137 passed**, zero
  failures/skips, **142.827s**. Focused ball suite **20/20**. Staged 720p/1080p
  marker renders in both directions and close spiral poses inspected.
- Normal macOS development build succeeds: **0 errors / 438 warnings**. Bounded
  Metal MainMenu startup initializes both teams without a runtime exception. The
  separate shutdown resource warning is listed above. No Windows, physical-controller,
  human-played full-match or standalone ball-feel acceptance is claimed.
- ISSUE_PRIORITIES.md ranks all ten open issues and identifies immediate follow-up
  areas. #12 is next; #14 scrum feel, #17 hazard colliders, #11 grip/handoffs and #13
  platform acceptance must account for the ball change. No other issue is completed.
- App, complete-source packet and copied evidence: ignored Builds/BallFlight in
  the original checkout. Detailed scenarios/results/limits: TESTING.md T-112–T-115.

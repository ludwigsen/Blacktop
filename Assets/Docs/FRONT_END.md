# Main menu, exhibition modes and runtime player editor

## Pre-implementation audit (master ef8ca18)

- Unity 6000.5.6f1 / URP 17.5.0 / Input System 1.20.0 / uGUI + TMP. Existing HUDTheme uses Old London and Outfit; reuse those exact assets and existing Kings/Vipers crests/scorebug paint.
- Build entry is currently SampleScene, with fourteen placed Player roots (seven per side), not a roster-aware spawning manager. CharacterApplier resolves a roster GUID at execution order -100, then builds private team-bound PlayerAttributes before movement Awake. PlayState resolves TeamMember slots and owns human-side selection, directions, possession and huddles. Bind match records at an earlier execution order rather than replacing gameplay roots.
- TeamRoster.PlayerRecord already owns displayName, number, GUID, twelve 0–20 ratings and an optional appearance asset. Preserve that record and its rating order. Existing seven Kings GUIDs remain unchanged. Vipers has branding and seven balanced gameplay actors but no roster asset. Create an explicit development starter roster for those actors; never auto-fill a short roster at match start.
- The merged modular character foundation supplies stable part/palette IDs, twelve heights, five body slots, shared Avatar/controller, PlayerVisualRig and PlayerVisualAssembler. Its four non-Athletic builds and face/hair/beard slots mostly lack art. Reuse these contracts. Distinguish real options, visibly temporary geometry and missing-art presets.
- The body is PlayerBody.fbx (formerly Biped_Humanoid), under PlayerVisuals/PlayerModel; root CapsuleCollider/movement/AI/possession remain independent. Existing controller supports idle, run/backward/fast and Jump. Menus clone only PlayerModel, never the gameplay prefab.
- FieldRoot/FieldLoader swaps collider-free venue art. Pass a selected FieldDefinition in the match snapshot; keep FieldConstants, bounds, ruling algorithms and direction conventions unchanged.
- Existing winner UI reloads the current gameplay scene for a rematch. Keep the selected match snapshot through reload and provide a separate return-to-menu action.
- No runtime roster save, player editor, menu scene or active/reserve database exists. Source assets cannot serve as writable standalone saves.

## Ownership decisions

1. FrontEndSettings references actual team definitions, catalogs, field options, weight tuning and visual/font assets. TeamDefinition pairs branding/roster assets with explicit GUID-based offensive/defensive lineup assignments.
2. RosterService holds detached records plus portable appearance IDs, validates global GUID membership, jersey numbers, ratings and partial/full lineups, and commits clone/validate/write/swap transactions. No source ScriptableObject is edited during preview or ordinary runtime Save.
3. PlayerEditorSession owns a working copy. Existing GUIDs survive edits/transfers; new/duplicate GUIDs are assigned only when saving. Seven active assignments are separate from unlimited reserves and independent of roster list order.
4. A versioned JSON file under persistentDataPath is replaced atomically with backup recovery. Restore Defaults reimports canonical data explicitly. Editor source commit is a separate confirmed operation in the Editor assembly, with validation and Undo/rollback.
5. Head 2 Head creates an immutable detached snapshot of two explicitly chosen eligible lineups, human side and venue. MatchBootstrap binds existing player roots before CharacterApplier/PlayState initialize. Legacy direct SampleScene launch remains supported when no snapshot exists.
6. Navigation, reusable uGUI controls, roster views, editor categories and preview rendering have separate owners. InputSystemUIInputModule handles pointer/keyboard/gamepad focus. Preview uses a dedicated render target/camera/lights and no gameplay components.

## Launch and scene setup

Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. MainMenu is enabled build index 0; SampleScene is enabled index 1. Both scenes and all referenced data are already generated and committed. No manual assembly is required. `Blacktop > Front End > Generate and Wire Front End` is an explicit repair/authoring utility: it opens and saves both scenes and resets their wiring, so save unrelated scene work before using it. Existing data assets are retained rather than overwritten.

`Assets/_Project/Data/FrontEnd/FrontEndSettings.asset` references the team definitions, field choices, appearance catalog, default appearance, visual prefab, weight profile and existing HUD theme/paint. Kings/Vipers reuse their existing TeamIdentity assets and crests. Adding an installed team requires a unique TeamDefinition ID, branding, TeamRoster, valid GUID-based independent lineups, and a reference in settings. Changing the installed team set requires deliberately restoring defaults for version-1 local saves; it is not an automatic team-catalog migration.

MainMenu builds its reusable uGUI presentation at runtime. No new gameplay prefab or duplicated gameplay system was introduced. Preview clones `Assets/_Project/Prefabs/PlayerModel.prefab`, not Player.prefab. The existing shared Avatar/controller and PlayerVisualRig are retained.

## Navigation and controls

- Main menu: NEW GAME, PLAYERS, QUIT. Quit stops Play Mode through the Editor assembly or exits a standalone application.
- New Game: HEAD 2 HEAD, PICKUP GAME, BACK. Both use MATCH SETTINGS, then REVIEW MATCH, then START MATCH.
- Head 2 Head: select your team and a distinct CPU opponent (each offers Random), choose Random / HOME / FIRST POSSESSION / AWAY / DEFEND FIRST, then field, condition, severity and difficulty. Cards show actual player/active counts and readiness errors. A short lineup has a MANAGE ROSTER route; no permanent players are invented to make it eligible.
- Players: choose a team and any row. Actions include create, edit, duplicate, transfer, delete, assignment/replacement, reserve, save, restore defaults and Editor-only source commit. Both the roster and action lists scroll. The selected row has a shape marker; focused controls auto-scroll into view.
- Mouse: click, wheel-scroll lists; drag the model to rotate and wheel over it to zoom. Keyboard: arrows/Tab navigate, Enter submits, Escape goes back. Gamepad: stick/D-pad navigation, south button submits, east button goes back. Preview also exposes focusable rotate/zoom/full-body/head/reset buttons. Physique contains idle/run/backward/fast-run preview choices.
- Gameplay: Escape, gamepad Start, or MENU returns to MainMenu. Winner rematch reloads the selected match configuration. Returning to the menu clears that match snapshot and keeps the saved roster service.
- Dirty editor Back/Cancel asks before discarding; RESET CHANGES restores the draft to its opening values. Save selects the saved player in the roster. Dialogs disable the underlying screen and restore focus when dismissed.

## Roster identity, slots and transactions

`TeamRoster.PlayerRecord` still owns the name, number, GUID and twelve ratings. The runtime DTO wraps that same record and a portable appearance selection; it does not create a second authoritative rating array. The UI splits the existing displayName at its final space for first/last-name editing and combines it when edited. Existing names are unchanged merely by opening a session.

Existing Kings GUIDs/ratings and source appearance references are unchanged. The legacy scene QB had both an inherited empty CharacterApplier and a scene-added bound one; setup consolidates them into the inherited component using the existing Marcus Reed GUID. Vipers receives a new explicit seven-player balanced development roster and its generated metadata (`Vipers QB`, `Vipers RB`, etc.). These are source defaults for the seven existing actors, not match-start autofill. Existing rows without appearance references import the front-end default appearance.

Create and duplicate drafts have no GUID until a successful Save. Edits/transfers preserve the original GUID. Duplicate names are allowed. Jersey numbers must be non-negative whole numbers and unique within a team; there is no two-digit cap that would impose an artificial reserve limit. Transfers move the single record between teams, remove old active references and start in reserves unless explicitly assigned through the editor. Delete and transfer have confirmation dialogs.

Seven active players plus unlimited reserves are supported. Each active assignment stores player GUID, offense slot and defense slot separately. Assigning an occupied offense slot moves its occupant to reserves; an occupied defense slot swaps with the freed defense slot. There are no fixed offensive/defensive position pairings. Partial lineups may be saved; Head 2 Head requires all seven unique players, seven offensive slots and seven defensive slots. Roster array order has no gameplay position meaning.

Every operation clones the current state, validates all installed teams/memberships/IDs/ratings/selections/slots, writes the complete save and only then swaps in-memory state. Validation or I/O failure leaves the current state intact. The editor session is a detached working copy; preview and Cancel cannot write source assets or roster saves.

## Runtime persistence and recovery

The save is `Path.Combine(Application.persistentDataPath, "rosters.v1.json")`; Unity chooses the platform-specific folder. Do not hardcode a developer machine path. The JSON contains schema `version: 1`, team IDs, existing PlayerRecord fields without Unity appearance references, appearance IDs/heights/builds and GUID-based lineups. It contains no Unity instance IDs or packaged asset paths.

Writes go to a sibling `.tmp`, then `File.Replace` atomically replaces an existing file and retains `.bak`; the first write uses a same-directory move. Temporary files are cleaned up. Startup first validates the primary save, then a previous backup; otherwise it retains canonical defaults with a visible message. Unsupported versions/catalog mismatches are rejected rather than guessed at. Save after backup recovery to repair the primary. RESTORE DEFAULT ROSTERS deliberately replaces all local rosters with the current source defaults, including removing local-only players. Existing source records migrate through the default importer; no earlier runtime roster file format existed.

Runtime saving never modifies packaged/source ScriptableObjects. Match start takes a detached validated snapshot, so further menu changes cannot mutate an active match. Rematch retains that snapshot; a new Head 2 Head uses the current saved state.

## Commit canonical development rosters

In Editor Play Mode, save player changes first, then use COMMIT TO SOURCE (EDITOR) in the roster actions or `Blacktop > Front End > Commit Saved Rosters to Source`. Confirm the explicit source-write dialog. This writes **all saved teams**, not an unsaved editor draft: TeamRoster records, TeamDefinition active assignments, and per-GUID appearance assets under `Assets/_Project/Data/Characters/Players/`. Review these files in Git and commit them to ship the new defaults. Asset writes are validated and grouped with Undo; errors restore the recorded source objects and delete newly created files. Unreferenced older appearance assets are retained for deliberate cleanup.

Source commit does not erase/replace the separate local runtime save. Restore defaults deliberately when testing newly committed defaults. Ordinary Player Save, roster Save, preview and Cancel have no AssetDatabase dependency. `FrontEndSourceCommit` alone lives in Blacktop.Editor and installs runtime delegates; standalone code imports no UnityEditor namespace.

Head 2 Head is the supported entry for edited lineups. Direct SampleScene launch keeps the original fixed GUID development bindings when no snapshot exists. After deleting/transferring those original players in canonical source, those legacy scene bindings may no longer resolve; launch via MainMenu for custom rosters. This PR does not add a second scene spawner or silently replace those GUIDs.

## Physique, weight and ratings

The twelve existing height values remain centimetres (170, 175, 178, 180, 183, 185, 188, 191, 193, 196, 198, 201), displayed as the requested 5'7" through 6'7" selections. Bands derive from the selected profile. All five builds work at every height. Visual fitting/height scaling stays below PlayerVisuals; player roots and CapsuleColliders remain unchanged.

`BodyWeights.asset` stores pounds internally and rounds for display. Default weights at 183 cm are Lean 165, Athletic 190, Thick 220, Heavy 250, Massive 275 lb. Each entry exposes a height exponent (2.5) and limits (120–390 lb): `basePounds * (heightCm / 183)^exponent`, clamped to its limits. This is deterministic appearance data and never alters ratings or collision.

The editor uses the actual AttributeStat order: Passing, Speed, Blocking, Agility, Catching, Run Power, Carrying, Tackling, Coverage, D-Moves, Swagger, Route Running. All remain 0–20, stored on the existing record and distributed by CharacterApplier into private runtime PlayerAttributes. Physique/cosmetics do not tune any gameplay stat.

## Art states and replacing temporary assets

Editable Blender sources for all current bodies/integrated faces, clothing and eyes now live in `SourceAssets/Characters/BlacktopCharacters.blend`, with optional per-build/head snapshots. Use `SourceAssets/Characters/README.md` for editing and returning geometry to existing Unity mesh IDs/bind poses. Saving a `.blend` alone does not update Unity; export_meshes.py plus the explicit Editor mesh-apply command is the supported path. New character art must retain `.blend` sources. Missing production presets still require actual art and are not fabricated by format conversion.

The catalog supports three explicit states: available (`placeholder=false`, `temporaryVisual=false`), functional temporary visual (`temporaryVisual=true`), and defined missing art (`placeholder=true`). Empty shaved/clean-shaven choices are intentional available options, not missing meshes. The UI uses useful labels plus those state labels and reports fallback diagnostics. Do not treat the number of preset IDs as visual variety.

`Assets/_Project/Art/Characters/Temporary/` contains five distinct surface-deformed bodies and fifteen cropped top/shorts/footwear shell meshes. Regional edits preserve the existing bones, weights and bind poses: Lean narrows the torso, Thick increases torso/limb bulk, Heavy adds waist/depth mass, Massive emphasizes chest/shoulders/arms. This goes beyond uniform mesh scaling, but hands, garment edges and anatomy are visibly provisional. Temporary clothes are close-fitting colored mesh shells with rough cut edges, not authored jerseys/shoes. Body/garment presets live in Data/FrontEnd. Source meshes, imported model settings and original catalog remain intact.

Three face IDs currently retain the **same integrated prototype head**; no distinct geometry is claimed. Cropped hair and beard presets are missing and omitted, while shaved/clean-shaven work. Skin palettes visibly tint the body; a shared hair-color selection is stored and applies when hair/beard art is supplied. Gloves, wristbands, sleeves, headbands and accessories have functional selection slots with labelled missing presets and None. No jersey-number renderer exists on these shells; the match nameplate displays the saved name/number. Long-term 30/20/8 catalogs need art additions, not fixed-size arrays or an editor rewrite.

To add production art, follow the canonical skeleton/import/bind-pose/grounding/attachment contract in MODULAR_CHARACTERS.md. Create CharacterPartDefinition assets with unique stable IDs, useful displayName, correct slot/body compatibility, shared skeleton and renderer/bone/socket references. A compatible `skinnedMeshOverride` may reuse the source renderer bones/materials; validation checks its bind poses and submeshes. Use the same `variantFamilyId` for body-specific garment versions so build changes select their compatible counterpart. Set material tint roles for skin/team/hair; per-player colors use MaterialPropertyBlock. Add parts to FrontEndCatalog and configure default selections deliberately. Keep old IDs or migrate saves explicitly when retiring them. Face/hair compatibility and missing-art behavior come from the existing assembler, not the menu.

Preview owns one visual clone, camera/lights and 1024-square render target on an isolated layer, with shared idle/run controller. It destroys private appearance/texture/model resources on teardown. It never adds TeamMember, possession, AI, physics or a gameplay player root. Live selection updates trigger assembly on changes rather than polling every frame.

## Gameplay integration and validation

MatchBootstrap runs at -2000, ahead of CharacterApplier's -100 and all normal Awakes. It maps each validated offensive assignment to the corresponding existing actor on each side, configures runtime roster/appearance data, installs independent defensive slots, team branding, user team and selected FieldDefinition, then the existing lifecycle takes over. FieldConstants, boundaries, contact, movement, huddles, play calls, Gamebreaker and ball ruling code are preserved. No Rigidbody or trigger callbacks were introduced.

Final graphics-enabled Unity 6000.5.6f1 Editor Play Mode suite: **71 passed, 0 failed, 0 skipped**. The nine new tests cover transactional CRUD/IDs/ratings/lineups/reserves, file restart/backup/default recovery, invalid data and failed I/O, weights/body meshes, actual create/save/activate/Head 2 Head controls, runtime appearance/stats/nameplate and possession/control, both human sides/directions, field selection/rematch/menu return, draft cancel/preview cleanup, isolated canonical-source commit, and synthetic gamepad Submit/Back. The existing 62 gameplay/animation/field/appearance tests also pass. Source commit tests use temporary copied assets, not developer roster files.

Rendered main menu/roster/physique/face/gear/attributes/confirmation/match and five-body comparison were inspected; main/editor were checked at 1920x1080 and 1280x720. Final selected fourteen-player sample: **112 skinned renderers, 400,176 vertices, 12 unique shared meshes, 2 shared materials**. The direct legacy baseline was 70 renderers / 268,590 vertices; the extra garment shells account for the increase. One final full-roster SaveCurrent Editor sample took **27.147 ms** (14 default players; initial pre-optimization sample 339.035 ms). Shared catalog validation is done once per transaction rather than per player. These samples do not establish frame rate, skinning cost, allocations, load time or a production performance budget. Profile representative builds on both platforms before production art expansion.

The preview UI needs a graphics device; `-nographics` cannot create its render textures (the first headless run passed 67/71 and failed the four graphics-dependent cases). The final run used graphics. The copied Unity search cache emitted its existing internal QuickSearch indexing exception during visual startup; no product/gameplay exception failed the final suite. Physical-controller feel, Windows, prolonged standalone play, final garment/head fit/LOD and missing football-action animations remain manual/art-dependent checks. TESTING.md records standalone build/startup results and acceptance steps.

## Recommended next PR

Author the canonical headless Athletic body, three genuinely distinct heads, one cropped hairstyle, one beard and properly fitted top/shorts/shoes. Match the Street 1 direction, keep the shared rig and stable-ID contract, add low-detail meshes, and profile fourteen characters on macOS/Windows. Replace temporary presets through data/import configuration while retaining this editor and roster workflow.

## Exhibition match configuration (2026-10-24)

MainMenu and SampleScene remain the two committed scenes. Their existing controller/bootstrap references automatically build the new flows; do not rerun the scene generator to install this feature. No scene, source roster, GUID, skeleton, playbook, rule or field-boundary replacement is required. `QuickPlayView` and `QuickPlayMatch` retain their existing contract names for compatibility, but implement Head 2 Head and the shared exhibition snapshot; there is no separate Quick Play mode.

**Head 2 Head:** Main → New Game → Head 2 Head → human team → opponent → Match Settings → Review → Start Match. Only seven-player valid saved lineups can launch. Disabled invalid-team cards expose Manage Roster; settings also links to roster management. Saved offense/defense mappings, records and branding are copied unchanged. Random teams choose distinct eligible teams. Choosing an explicit invalid team never silently substitutes players.

**Pickup:** Main → New Game → Pickup Game → Draft → Human Lineup → Match Settings → Review → Start Match. The read-only pool contains active players and reserves from every installed saved roster. Search matches name or jersey number; team filtering and name/rating/role sorting help browsing. Details show all twelve ratings and the existing visual-only 3D preview. Seven slots expose remove/replace; an eighth or duplicate pick fails. Fewer than fourteen unique eligible saved players blocks the draft with a Manage Players route.

CPU selection happens when REVIEW MATCH freezes the final requested difficulty and settings. This deliberately defers CPU selection until the difficulty is known: the CPU cannot reserve or take a human pick. It fills complementary offensive needs, ranks the offensive vacancy 65% / the player's best independent defensive contribution 35%, and selects within the best 1–4 remaining candidates according to the profile's draft quality. Remaining players are removed after each pick. The display-only Overall is the equal-weight mean of the existing twelve ratings, times five; it is never serialized or used as a gameplay attribute. The per-slot draft ranking uses Passing for QB; Speed/Agility/Run Power/Carrying for backs; Blocking/Run Power/Tackling for linemen; Catching/Speed/Route Running/Agility for receivers, plus complementary defensive ratings. No temporary fake players or source writes occur.

Lineup suggestions preserve available source offense and defense assignments independently, resolve collisions in stable pool order and fill gaps by role rating. Human Lineup allows all fourteen human assignments to be changed; changing an occupied slot swaps its occupant. The final Review shows both completed squads and lets the user independently override all seven offensive and all seven defensive slots on either pickup side. Those changes create a new detached snapshot with the same resolved settings and draft. Head 2 Head review preserves the saved assignments.

Pickup has match-only IDs `pickup.home` / `pickup.away` and names PICKUP HOME / PICKUP AWAY. It uses configurable `pickupHomeStyle` / `pickupAwayStyle` references in settings, committed to the existing Kings amber and Vipers green styles/crests. Branding is copied as values into the snapshot; Bootstrap owns disposable TeamIdentity/roster/appearance objects. Scorebug team labels autosize within their existing slots so HOME/AWAY remains visible. Individual GUIDs, names, numbers, appearances and ratings survive mixed source teams. Source-team membership remains available for debugging and curve binding, while HUD, tints, possession and field markers use match-side identities.

`MatchSettingsView` is shared, with independent `HeadOptions` / `PickupOptions`. Fields offer Random plus only registered playable definitions with a valid collider-free surface and optional detail art. Incomplete definitions or `playable=false` cannot enter Random. Both shipped functional venues remain selectable; their visual art is still provisional. Conditions offer Random, Clear, Cold, Slick, Loose, Raw. Severity offers Clean ×0.5, Regular ×1, Hard ×1.8; Clear disables/ignores severity. Difficulty offers Random, Easy, Normal (default), Hard, Street Legend. Side selection also offers Random.

A local `IMatchRandom` / `SeededMatchRandom` resolves requests once at REVIEW MATCH immediately before constructing the snapshot. It never consumes Unity's gameplay random stream. Tests inject seeds. Requested field/condition/difficulty/team/side choices and their resolved values remain in the snapshot; Random difficulty is uniform over the four profiles. Review exposes the actual field, condition, severity, difficulty and teams. Back to settings and a fresh review explicitly creates a new match proposal; Start and Rematch never reroll an accepted proposal.

## Conditions and acceleration

`Assets/_Project/Data/FrontEnd/Conditions/` holds the five authoritative ConditionData assets. `GetEffectiveModifiers` scales authored percentage points by severity. `MatchModifiers` sums matching targets, converts points to fractions once and bounds each aggregate to [-0.30, +0.30]. Clear is always zero. Power maps to RunPower, Carrying to Carrying. MatchBootstrap binds the same immutable modifiers to all fourteen private runtime PlayerAttributes. Effective output is `(curve(rating) + field addition) × Gamebreaker`, clamped by the existing AttributeCurves hard caps. A bound match replaces the legacy optional FieldModifiers argument, preventing duplicate environmental addition. AI routes, blocking, CPU running/passing and designed-handoff movement now consume the same effective attribute path rather than separately applying Gamebreaker.

Acceleration remains owned by Agility's base rating/curve and hard caps. `PlayerAttributes.Acceleration` uses that curve plus only ConditionStat.Accel, then the one Agility Gamebreaker boost. Agility's separate condition penalty affects jukes/hurdles and is not charged again to acceleration. Both controlled-carrier and controlled-defender acceleration use this boundary. Existing constant-velocity AI has no acceleration integration, so an Accel penalty is not incorrectly converted into a second AI speed penalty. Huddle/formation presentation speed is intentionally independent of gameplay conditions. No thirteenth attribute was added.

## Difficulty authoring and actual consumers

`Assets/_Project/Data/FrontEnd/Difficulty/` holds four Inspector-editable DifficultyProfile assets; settings references them explicitly. Values are copied into immutable DifficultyTuning on review. Profiles affect only the CPU team's decision/execution paths; human input and human-team AI assistants retain Normal decision tuning. All players still use their own roster ratings and the shared selected condition.

| Consumer | Easy | Normal baseline | Hard | Street Legend |
|---|---|---|---|---|
| Initial defensive reaction | 0.28s | 0s | 0.02s | 0s |
| Pursuit decision interval / observed lead | 0.18s / 0s | every frame / 0s | every frame / 0.12s | every frame / 0.22s |
| Released-pass reaction multiplier | 1.8 | 1 | 0.75 | 0.55 |
| Coverage cushion / zone awareness | 1.3 / 0.75 | 1 / 1 | 0.9 / 1.15 | 0.8 / 1.3 |
| QB read-time multiplier | 1.4 | 1 | 0.85 | 0.7 |
| Runner avoidance / visible threats | 0.65 / 1 | 1 / 2 | 1.15 / 3 | 1.3 / 4 |
| Situational offense/defense weight | 0.5 | 1 | 1.3 | 1.6 |
| Additional standard run bias | +0.08 | 0 | 0 | 0 |
| Gamebreaker live activation delay | 0.7s | 0s | 0s | 0s |
| CPU draft quality | 0.30 | 0.65 | 0.85 | 1 |

DefenderAI uses only observed displacement for pursuit lead, keeps the existing intended-receiver/nearby-zone gate on released-pass reactions, and never gains a difficulty speed bonus. CPUCarrierAI uses read timing and avoidance; CPUPlayCaller adjusts situation-based run/pass probabilities, defensive playbook weights and the existing Gamebreaker activation policy. Normal's multipliers retain prior decision tuning. Moving AI onto the existing attribute path also fixes its previously bypassed roster/condition speed and passing effects; that correction applies at every difficulty.

Returning to MainMenu clears the match and destroys match-owned Unity objects; the saved RosterService remains. Rematch reloads SampleScene using the exact same snapshot, GUIDs, CPU draft, lineups, branding and resolved settings. Direct SampleScene without a snapshot retains legacy bindings, Clear/no match modifiers and Normal CPU tuning. Save format/version remains unchanged.

To author additional playable fields, register validated FieldDefinitions in settings and leave unfinished venues unplayable. Edit condition percentages only in the ConditionData assets. Edit CPU behavior only in the shared DifficultyProfile assets. All five named conditions and four concrete profiles are required for a valid catalog; missing catalogs block launch visibly. The existing explicit FrontEndSetup repair utility registers committed catalogs when missing, without replacing existing authored catalog choices. It still opens/saves scenes by design; it is unnecessary for ordinary installation.

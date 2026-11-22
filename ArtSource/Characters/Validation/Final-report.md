# Blacktop character migration

Historical migration checkpoint, 2026-11-21. The subsequent [six-body integration review](Integration.md) records the corrected Power underarm, the interactive lineup and updated mode coverage.

## Executive summary

**PASS.** Matches and previews use the six approved MPFB bodies through the existing roster and animation systems. The Athletic rig retains the approved sculpt. Source checksums, rig coverage, Humanoid imports, retargeting and the gameplay regression suite passed. The packaged macOS player initialized the six-team menu on Metal. The shipping scenes and player prefab resolve exclusively to the new body pipeline.

All 42 supplied players have no individual appearance asset and inherit the Athletic default. Their UUIDs, teams, ratings and lineups are intact. Saved body selections resolve to the corresponding MPFB bodies. The owner's outfit decision is applied in both matches and previews. The visible model is the body alone and its saved clothing IDs remain available for later fitting.

## Changes by phase

| Phase | Result | Checkpoint and evidence |
|---|---|---|
| 0. Audit | PASS | cb7e8b0. Traced character and animation dependencies, inspected the installed MPFB API, compared original topology and recorded local bytes. |
| 1. Creator retirement | PASS | 149abbb. Removed two orphaned generator bytecode files. Local Blender and MPFB opened, and the old operators were absent. The animation exporters and historical branch were preserved. |
| 2. Sources | PASS | 43f7d84. Six immutable sources and the Athletic sculpt match the seven external originals by SHA-256. |
| 3. Athletic rig | PASS | bde691c. Fitted MPFB game_engine, preserved the sculpt, checked weights, generated twelve diagnostic poses plus rest and inspected three views. |
| 4. Athletic import | PASS | 61f1626. Valid Humanoid, expected dimensions, one skinned mesh, 52 mapped human bones and 99 controller samples. |
| 5. Remaining bodies | PASS | 2e0da0c. Each body has its own fitted rest rig with the same hierarchy. The six-body table records the deformation limits. |
| 6. Runtime replacement | PASS | bb4b42b. Reopened six linked production prefabs in the review scene before connecting actor and preview. Tested 72 body/height choices and fourteen match actors. |
| 7. Cleanup | PASS | c5af0ba. Verified importer dependencies, archived reference tools, removed verified prototypes, checked shipping graphs and repeated the original football reference bake. |
| 8. Animation readiness | PASS | 0f56559. The per-action matrix covers installed and missing motion, gameplay usage, technical results and visual limitations. |
| 9. Verification | PASS within tested scope | Two working-scene Play Mode runs and an independent committed-scene run passed. Normal macOS build and menu startup passed. Manual and Windows acceptance remain open. |

## Files added, removed and preserved

The path-by-path manifest contains **115 additions, 47 modifications and 70 removals** relative to 39600e4. Moves appear as an old path and a new path. [Full changed-file manifest](Files.tsv)

- **Added** six original .blend files, the Athletic sculpt, six fitted .blend files, six Unity FBXs and importer records, six visual prefabs, the production wrapper, body/skeleton definitions, one shared skin material, and the review scene. Tools/CharacterPipeline holds the rig/export/validation sources. ArtSource/Characters/Validation holds the evidence.
- **Removed from active use** five generated bodies, fifteen generated garment shells, redundant prototype and temporary body records, an orphan prototype prefab, the temporary art builder, two generator caches and two byte-identical football texture copies. The former editor interchange code is archived under SourceAssets/Characters/Validation. [Removal decisions](Cleanup.tsv)
- **Preserved** all six source team rosters, corresponding Unity metadata, all 42 UUIDs and all twelve ratings per player. The production controller, its mask, all five animation FBXs and all six native football clips are byte-identical to the starting revision. The original PlayerModel, PlayerBody, eye meshes and old skeleton still serve the historical authoring reference. The original Blender sources, exported artist meshes and football authoring files remain available.

The immutable inputs live under Core to follow the existing ArtSource convention. All six share 19,158 original vertices and the same ordered 18,486 polygons. Athletic's separate sculpt changed 408 evaluated skin vertices by over one micrometre, up to 9.633 mm. The six source-to-rig rest-surface differences are below one micrometre. Git LFS was unavailable locally. These compressed inputs and derivatives are ordinary Git objects.

## Six-character rig validation

All rows have 13,380 visible vertices, 13,378 quads and 26,756 triangles. Each of the 53-bone hierarchies has 52 weighted bones, full vertex coverage, a maximum of four normalized influences and a valid local Humanoid Avatar. The dimensions are source-rest width × depth × height, before player height selection. Contacts count non-adjacent, intersecting triangle pairs. The third count is the synthetic Blender running pose.

| Body | Rest dimensions (m) | Rig and Avatar | Unity retarget | Rest source / rest rig / running contacts |
|---|---|---|---|---|
| Slim | 1.139 × 0.473 × 1.729 | PASS | PASS · 11/11 | 0 / 0 / 30 |
| Athletic | 1.155 × 0.482 × 1.729 | PASS | PASS · 11/11 | 0 / 0 / 77 |
| Muscular | 1.190 × 0.501 × 1.729 | PASS | PASS · 11/11 | 0 / 0 / 107 |
| Stocky | 1.183 × 0.513 × 1.729 | PASS | PASS · 11/11 | 14 / 14 / 206 |
| Power | 1.196 × 0.532 × 1.729 | PASS | PASS · 11/11 | 145 / 146 / 345 |
| Massive | 1.205 × 0.547 × 1.729 | PASS | PASS · 11/11 | 131 / 130 / 357 |

The checked Blender and Unity frames showed connected limbs and recognizable poses on all six bodies. Rest contacts on Stocky, Power and Massive already occur on the approved inputs. **FAIL · Power armpit deformation in the catching stress pose.** One 0.50 mm edge opened to 31.77 mm. That local defect remains visible in the diagnostic measurements. [Body comparison](Six-body-comparison.jpg) · [Per-body measurements](Six-rigs.csv) · [Source comparison](Source-comparison.json)

## Unity animation and game tests

| Test | Result |
|---|---|
| Idle, standard run, sprint, backpedal and hurdle | PASS on all six Avatars |
| Carry ready/tuck, throw windup/follow-through, handoff give/take | PASS for import and sampled playback on all six. Grip, throw weight transfer and exchange contact still need animation work. |
| Dedicated walk, jog, acceleration, braking, shuffle, cut, pivot, catch variants, secure possession, tackle, break tackle, stumble, fall, get-up and collision reactions | NOT TESTED as production clips. The production controller has no dedicated clips for these actions. |
| Roster browsing and selection, save/reload, Head 2 Head, Pickup, player spawning, game state, animation transitions, ball ownership and facing | PASS in Play Mode |
| Complete Play Mode suite | PASS · 180/180 before cleanup, 180/180 after cleanup and 180/180 with the committed match scene. Zero failures or skips in each run. |
| Shipping dependency graph and historical six-action reference bake | PASS · no legacy body in either build scene or Player.prefab. All reference bake data matched across 58 joints. |
| Standalone macOS | PASS · build completed with 0 errors and 438 warnings. Apple M4 Metal initialized the menu with six teams. The test stopped at the menu. |
| Windows, physical controller, human-played standalone match and match-speed contact across six bodies | NOT TESTED |

Unity evaluated nine times per installed clip per body, giving 594 baked samples. Blender pose sheets and Unity baked frames were inspected separately. The macOS build log includes deprecated Unity API warnings. The player logged a render-texture release warning during test shutdown. [Detailed animation readiness](Animation-readiness.md) · [Test results and log references](Unity-regression.json)

## Disk usage and cleanup

All sizes are logical file bytes. The local comparison includes ignored data, symlink lengths and the independent checkout under Builds. It was measured after the disposable validation project and redundant raw render copies were removed and before this final report, manifest and commit were written. The tracked Git tree comparison uses fixed revisions and excludes the final documentation checkpoint.

| Population | Before (bytes) | After (bytes) | Change |
|---|---:|---:|---:|
| Entire local directory, including main Git metadata | 8,720,153,937 | 8,417,568,926 | -302,585,011 (-3.47%) |
| Working tree with ignored files, excluding main .git | 8,481,077,440 | 8,167,348,794 | -313,728,646 (-3.70%) |
| Main .git metadata including stored history | 239,076,497 | 250,220,132 | +11,143,635 (+4.66%) |
| Unity Library | 3,528,981,781 | 3,214,042,562 | -314,939,219 (-8.92%) |
| Builds including the independent checkout | 4,708,650,134 | 4,715,349,355 | +6,699,221 (+0.14%) |
| Tracked Git file contents at 39600e4 / 0f56559 | 218,532,134 | 218,569,428 | +37,294 (+0.02%) |

Phase 7 retired original paths with a combined size of 30,113,669 bytes. Phase 1 retired 46,473 bytes of generator bytecode. These are gross original sizes, including code that was archived elsewhere. Two unused local caches contributed 314,939,219 bytes (Bee and ShaderCache). Unity regenerated both caches in the disposable project and built the player. The new source and fitted art offset the removed tracked content. The original working copies of tracked files measured 218,535,104 bytes, including local edits. New Git objects account for the growth of the main .git directory. Git history was preserved. [Tracked categories](Inventory.tsv) · [Cache manifest](Local-cleanup.tsv)

| Classification | Treatment and measured examples |
|---|---|
| A. Production | Retained all production textures, animations, models and scenes with live dependencies. The largest tracked asset is the 12,624,737-byte Suburbs menu backdrop. |
| B. Development/source | Retained originals, rigged derivatives, the 11,516,410-byte historical character seed, editor utilities and animation authoring sources. The main Git object store occupied 248,766,216 bytes at the local snapshot. |
| C. Regenerable | Removed the two idle main-project caches and the task's disposable Unity review project. Kept installed PackageCache (2,221,283,958 bytes) for offline builds and retained regression logs and XML. |
| D. Obsolete | Removed the enumerated legacy meshes, records, orphan prefab, bytecode and duplicate images after dependency checks. |
| E. Uncertain or independently owned | Preserved Builds/StandardFootball (3,914,682,972 bytes) with its own Git checkout, uncommitted controller/animation work and unique clips. Kept the unique alternate football FBX, third-party assets, prior evidence and uncommitted artist changes. |

## Remaining risks and next animation work

1. **Footwork.** Validate walk/jog candidates in the independent checkout, then establish acceleration, braking, lateral shuffle and planted cuts in both directions. Inspect foot sliding at game speed across height selections.
2. **Possession.** Author a chest catch into a secure tuck, match both hands to the handoff contact frame and refine the release and torso weight shift on throws. Use Slim, Athletic and Power for contact extremes. Preserve the existing movement and ownership clocks.
3. **Contact and recovery.** Add the first synchronized tackle/reaction, fall and get-up before producing alternate moves. Recheck shoulder, hip and inner-thigh skinning on the wider bodies in those poses.
4. **Fit and playtesting.** Height remains visual at 170–201 cm against the existing two-metre capsule. The crown ranges from 30 cm below to 1 cm above its top; extended arms can exceed its 0.5 m radius. Muscular uses Stocky's existing 220 lb at 183 cm for display. Run a physical-controller match to judge movement and hand/ball contact. Fitted clothing can later consume the preserved outfit IDs.

The source-to-runtime procedure and directions for assigning a variant to another player are in the [production character guide](../README.md).

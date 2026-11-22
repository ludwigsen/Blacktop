# Six-body integration review · 2026-11-22

## Interactive review

Open Assets/Scenes/MPFBCharacterReview.unity, enter Play Mode and use the Game view. Six production prefab instances stand at the same foot baseline and scale. Pick All six or an archetype, then a view and motion. The camera stays in front of the lineup and the subjects turn individually for side and three-quarter views, so they cannot hide each other. Pause holds the phase across view changes. The slider scrubs a paused motion and Play resumes normal clip timing. Idle, run, sprint, backpedal, hurdle, both carry poses, the two throw phases and both handoff roles use their actual controller states. Dedicated walk and catch clips remain missing. The scene is excluded from the player build.

## Runtime provenance

Head 2 Head resolves saved team lineups through QuickPlayMatch.TryHeadToHead. PickupDraft and TryPickup assemble the drafted players into a detached QuickPlayMatch snapshot. MatchBootstrap maps each snapshot record to an existing gameplay actor by offensive slot. It resolves the saved appearance and preserves the independent defensive slot. CharacterApplier binds the record and its ratings. PlayerVisualAssembler resolves the body through the catalog. PlayerVisualRig uses the fitted prefab and its skinned renderer, model-specific Humanoid Avatar and BlacktopCharacterController. The actor root retains the gameplay capsule and movement components. The forty-two stock records have no individual appearance assignment and use Athletic.

## Skinning and pose review

Power's near-coincident underarm edges (vertices 8304–8314 and 1632–1642) had a 19-percentage-point upper-arm weighting discontinuity across 0.497 mm. Both opened to 31.767 mm in the catch diagnostic. Averaging the original influence values on each pair changed four vertices. Both edges now measure 0.459 mm in the same pose. The mesh positions, topology, bones and rest silhouette stayed intact. The corrected FBX matched the approved source within 0.0004 mm after coordinate conversion.

| Power measurement | Before | After |
|---|---:|---:|
| Maximum catch edge stretch across the entire skin | 63.86× | 5.45× |
| Maximum throw edge stretch | 61.25× | 5.30× |
| Maximum shoulder abduction stretch | 36.32× | 3.02× |
| Intersecting triangle pairs at rest / running | 146 / 345 | 146 / 343 |
| Intersecting pairs in catch / throw | 46 / 73 | 46 / 74 |

The new maximum catch stretch occurs on a different 7.464 mm edge. The twelve-pose diagnostic, topology, UV and weight checks passed. The corrected Blender and Unity image sheets showed connected shoulders and limbs. The original broader bodies already have some underarm and inner-thigh contacts at rest. Correcting the unusually short edges does not remove every contact in every pose.

Athletic's hip maximum is a 3.426 mm edge opening to 22.16 mm at the pelvis/thigh fold. Its hip-flexion 99th-percentile stretch is 1.042×. The 77 running intersections are genuine non-adjacent triangle contacts around the elbows, shoulders, pelvis and knees. The inspected three-angle and Unity frames showed a connected silhouette under flexion. These measurements alone give insufficient reason to change Athletic's weights or skeleton. Throw samples show a readable raised arm but limited lower-body transfer. Handoff give/take samples still need a matched hand path. The static isolated diagnostic catch is independent of any production catch clip.

## Legacy inventory and retirement

| Class | Disposition |
|---|---|
| Active MPFB production | Keep the six sources, Athletic sculpt, fitted Blender derivatives and FBXs, corresponding Humanoid Avatars, prefab variants, wrapper, material and catalogs. |
| Shared infrastructure | Keep Player, MatchBootstrap, both presentation adapters, BlacktopCharacterController, its football mask and all eleven clips. Both exhibition modes consume these assets. |
| Historical animation authoring | Keep PlayerModel.prefab, PlayerBody.fbx and Avatar, CanonicalSkeleton, the four editable eye meshes and the Blender/Unity reference tools. The six-action reference bake uses the old rig. |
| Superseded meshes and prefabs | The five generated bodies, fifteen clothing shells and orphan prototype were already removed. The original per-path removal evidence is in Cleanup.tsv. No additional Unity asset removal was warranted. |
| Obsolete generator | The creator is on a historical Git branch. Its old bytecode was retired during migration. The active Python rig builder binds approved MPFB inputs. |
| Uncertain or independently owned | Retained UserBase.fbx, the distinct alternate football FBX, older artist sources, installed packages and the separate modified checkout under Builds/StandardFootball. No byte-identical FBX pair was found across the main Assets, ArtSource, GeneratedAssets and SourceAssets folders. |

The earlier Local-cleanup.tsv records prior cache removals. The current run's separate cleanup accounting appears below. Original MPFB hashes, roster assets, colliders, gameplay scripts and animation controller assets were preserved.

## Unity evidence

- **PASS · Interactive lineup.** A disposable Play Mode check opened the saved scene, verified the dominant clip on all six live Animators for all eleven choices, held a scrubbed phase, isolated one actor and restored the full set. It also verified that Play advances the phase. The camera capture shows the six rendered skins in the three-quarter run view. No physical mouse-input usability test was performed.
- **PASS · Runtime and dependency checks.** The full run passed 181/181 with no failures or skips, including 180 project tests and the additional lineup check. Four exhibition lifecycle cases verified the live skin, Avatar and controller of each actor against the saved selection and counted six distinct rigs in Head 2 Head and Pickup, home and away. A fresh Unity dependency traversal from both enabled scenes and Player.prefab found six production model/prefab pairs and zero old body, eyes, wrapper or skeleton. The separate model test sampled eleven clips at nine phases on each of six valid Humanoid Avatars (594 samples).
- **PASS · Power correction.** Only four vertices received new weights. Each side's catch-stress opening fell from 31.767 to 0.459 mm. The production importer GUID is preserved. Rest surface, bone count, hierarchy and weight budget passed.
- **FAIL · Handoff contact quality.** The staged gameplay exchange test passed ownership and timing checks. In the actual rendered frames at 20, 100 and 220 ms, the ball moves between players while the hands fail to form a receiving pocket. The ball center is 0.197 m from the receiving hand bone at 100 ms and 0.121 m at 220 ms. These are center-to-bone distances, not skin clearance. The frames support animation refinement, separate from rig weighting. See [the staged exchange](Handoff-contact.jpg).
- **NOT TESTED · Human-played match-speed visual acceptance** across the full six-body roster, physical-controller feel and Windows playback. The staged exchange holds player roots in the test fixture. Automated mode passes and isolated review poses cover different questions.

Local raw evidence is under Builds/Validation/MPFB/Integration, including playmode.xml, model-review.log, shipping-dependencies.txt, lineup.png and handoff.xml. The original migration report and Cleanup.tsv refer to the earlier retirement checkpoint.


## Cleanup measurement

**PASS.** The main checkout measured 8,420,406,231 logical file bytes immediately before cleanup and 8,420,383,688 immediately after, including Git, ignored files and the independent checkout. This isolated 22,543 bytes of recovery. At task start the checkout measured 8,417,719,995 bytes. Test evidence and new Git objects account for growth over the whole task. The disposable Unity project was outside these totals. No Unity asset or metadata file was deleted.

| Removed repository path | Bytes | Reason |
|---|---:|---|
| Tools/CharacterPipeline/__pycache__/check_poses.cpython-313.pyc | 12,317 | Ignored compiled Python cache. The source and the validated diagnostics are retained. |
| Tools/CharacterPipeline/__pycache__/rig_body.cpython-313.pyc | 10,226 | Ignored compiled Python cache for the prior rig builder. The current source contains the reproducible Power correction. |

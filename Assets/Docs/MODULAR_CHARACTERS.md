# Modular player characters — foundation

## Parametric body generator (2026-10-25)

The current authoring tool is `SourceAssets/Characters/BodyGenerator/CanonicalBody.blend`, with the **Blacktop Body Editor** sidebar source in `body_editor.py`. Read its README and AUDIT first. It adds three versioned families (Tapered: Lean/Athletic; Power: Built/Thick; Barrel: Heavy/Massive), six JSON presets, ten measured controls, calibrated inch/cm readouts, non-destructive preview, JSON editing and baked selected/all export. `canonical.json.gz` is the immutable 12,529-vertex / 19,575-triangle headless reference derived once from the unmodified Athletic snapshot. Its neck plane is Y=1.515m in Unity renderer space; 79 unique seam points, orientation and coordinates are recorded in `neck-interface.json`. The detached original head and unchanged eye skins are prototype fallbacks, not a finished head catalog.

New meshes/parts live under `Assets/_Project/{Art,Data}/Characters/Generated/v1/`. The existing FrontEndCatalog adds `body.<name>.parametric.v1`, matching gear IDs and `face.prototype.parametric.v1`. Built is enum value 5, preserving original 0–4 values. Existing temporary/prototype IDs, mesh GUIDs, sources, FBX, prefab, rosters and saved appearances remain available. Loading a save performs no silent migration; changing build chooses the generated family and matching shells. `compatibleBodyIds` restricts exact garment fit; `usePrototypeEyes` on the detached head preserves the existing eye skins. Neither field changes gameplay.

The v2 JSON bridge checks the complete canonical hierarchy/rest matrices, known target paths/GUIDs, topology arrays and normalized weights before any asset write. It retains target bind poses. Legacy v1 exports remain supported. Export uses baked temporary copies without shape keys or unsupported modifiers. Measurement tolerance is 1%; canonical percentage references and anatomical planes are documented in the generator README. Height calibration is separate from shape, and Unity's legacy uniform foot-pivot mapping is unchanged.

Matching garments are 4mm normal-offset shells from each solved body, preserving UVs and weights. Open seams, bent-joint fit and production topology/correctives need artist acceptance. The retained five installed animation clips are the coverage limit; no throw/catch/block/tackle coverage is claimed. See TESTING T-108 and the tracked generator Validation comparison/report. Roll back individual characters by choosing their original body, face and clothing IDs; keep generated IDs installed while saves still reference them. Revert the feature together for a full rollback.

## Legacy foundation and manual sources

## Current front-end extension

**Editable source:** all current body, integrated-face, clothing and eye geometry is available under `SourceAssets/Characters/` as `.blend` files with the shared rig/weights. Use the combined BlacktopCharacters.blend as the main authoring file, then export through export_meshes.py and apply with Blacktop > Characters > Blender > Apply Blender Mesh Export. Individual source files are independent snapshots targeting the same meshes, not synchronized libraries. All new character art must retain editable `.blend` source; see that folder's README for the source/export rules and the distinction between an integrated prototype face and future modular heads.

The foundation audit/results below describe the original prototype catalog. The implemented MainMenu/runtime creator now uses a separate FrontEndCatalog with five distinct temporary regional bodies and fifteen team-tinted garment shells on this same rig. FRONT_END.md documents the live preview, derived weights, versioned runtime saves, independent active lineups, explicit Editor source commit and replacement-art workflow. Production faces/hair/beards/garments and LODs remain outstanding. Vipers now has an explicit balanced development source roster; the current-master front-end audit found no roster asset or metadata, so both were authored once with stable new IDs. Kings identity/ratings remain intact.

## Audit and decisions

Audited current `origin/master` at `109633a` (including merged modular field surfaces), starting from a clean checkout. Implementation branch: `codex/modular-player-characters`. Unity is **6000.5.6f1**, URP **17.5.0**, Input System **1.20.0**, Test Framework **1.7.0**. No package or assembly-reference changes are required.

| Existing infrastructure | Assessment / integration |
|---|---|
| `Assets/_Project/Scripts/Player/TeamRoster.cs` | One roster asset embeds player GUIDs, display names, numbers and twelve ratings. Add one optional appearance reference. Do not migrate IDs or stats in this change. |
| `Assets/_Project/Scripts/Player/PlayerAttributes.cs` | Runtime factory copies ratings from a roster row; team-bound private copies supply Gamebreaker. Appearance has no rating dependency. This is usable gameplay infrastructure, not an appearance model. |
| `Assets/_Project/Scripts/Player/CharacterApplier.cs` | Runs at execution order -100, resolves the row, applies ratings before movement Awake and builds a nameplate. Preserve that order. The assembler reads its resolved row in Start, after PlayState branding is available. |
| `Assets/_Project/Data/Teams/Kings/Roster_Kings.asset` | Seven existing GUIDs and ratings remain unchanged. Optional per-row appearance can be selected in the roster Inspector. |
| Vipers | Rosterless in the original foundation. The front-end extension supplies a development source roster and independent GUID lineups; direct legacy scene players still use the explicit default without a Quick Play snapshot. |
| `Assets/_Project/Prefabs/Player.prefab` | Fourteen scene instances are already placed, rather than created by a dedicated spawner. Root owns CapsuleCollider (height 2, radius .5), movement, AI, tackles, possession and input. No Rigidbody. Instantiating this prefab also gets appearance assembly. |
| `Assets/_Project/Prefabs/PlayerModel.prefab` | Visual wrapper under `PlayerVisuals`; receives the replaceable PlayerVisualRig adapter. Existing imported body offsets/facing remain intact. |
| `Assets/_Project/Prefabs/PlayerBody.fbx` | Formerly `Biped_Humanoid.fbx`, renamed earlier with its GUID preserved. Valid Humanoid Avatar, 57 skinned body bones, 18,277 body vertices and four 227-vertex eye skins. Two shared URP Lit materials. Integrated face, no authored modular garments, no LODs. Useful stand-in, **not a production-ready modular body**. |
| `Assets/_Project/Art/Animations/BlacktopCharacterController.controller` | Shared idle (clip named mixamo.com), Standard Run, Fast Run, Running Backward and Jump. PlayerAnimationPresenter remains unchanged and root motion stays off. No walk, block, tackle, throw, catch or celebration animation assets exist in the current controller. |
| Field integration | `Assets/Scenes/SampleScene.unity`, FieldRoot/FieldLoader, FieldBounds, FieldLines and venue prefabs are untouched. Visual height never resizes gameplay colliders or changes ruling positions. |

**Must Have:** preserve the working football stack, explicit IDs, safe visual fallback, shared rig contract, height/build independence and authoring validation. **Should Have:** author the first genuine headless Athletic body, three distinct heads and one hair/beard attachment. **Later:** full catalog, cloth simulation, preview menus, mesh merging and draw-call optimization after profiling. **Kill:** 60 bespoke bodies, runtime random rerolls, per-build animation libraries, automatic rating changes from size.

## Runtime and saved data

`TeamRoster.PlayerRecord.appearance` optionally references a `PlayerAppearanceDefinition`. Existing rows need no migration. `PlayerVisualAssembler.Start` resolves that selection or its prefab default, reads current TeamMember/PlayState identity and the roster jersey number, then assembles under the child `PlayerVisualRig`. Invalid saved selections log diagnostics and try the default; a failed direct `TryApply` keeps the last valid visual intact. An invalid default leaves the original imported model visible.

Appearance stores stable part/palette IDs, body archetype, a discrete height profile, clothing/accessory IDs, head-size adjustment and optional seed metadata. IDs use ordinal, case-sensitive matching. The seed never overrides explicit choices. Reordering the catalog does not change reconstruction. Keep IDs after renaming assets; never reuse a retired ID for a different face. The front-end save layer persists these selections, height centimetres and the existing player GUID without Unity asset references; see FRONT_END.md.

ScriptableObjects are shared Unity assets: editing one changes every player referencing it. Duplicate a definition when a character needs different selections. Runtime assembly never writes those shared data assets. Team identity stays outside appearance. Call `ApplyTeam(identity, jerseyNumber)` after reassignment to refresh apparel property blocks/number labels without rebuilding attachments. The existing roster nameplate still supplies the visible number until numbered garments are authored; `PlayerVisualRig.jerseyLabels` is the optional TMP garment-label hook, empty on the prototype.

`CharacterPartDefinition` describes either a skinned renderer source bound to the contract, a rigid render-only prefab on a Humanoid socket, an intentional empty option, or an explicitly labelled missing-art slot. Skinned attachments reference the **same live bones**, not another Animator. Body replacement reuses the primary renderer so existing PlayerVisualFeedback references remain valid. Prototype eye skins are disabled when a headless replacement is selected. All selected assets are checked before changing the visual.

Per-material `CharacterTint` roles select Skin, Hair, TeamPrimary, TeamSecondary or None. URP `_BaseColor` is written through a MaterialPropertyBlock; shared materials are never cloned. Use one role per material slot. Slot overrides take priority over renderer-wide tint in Unity. The prototype has no isolated skin/apparel regions, so its tint array is deliberately empty and the existing debug state colors remain visible. Palette selection is functional for properly authored parts; it does not falsely recolor an absent hairstyle or garment.

Rigid prefabs allow Transform, MeshFilter, MeshRenderer and LODGroup only. They must not contain gameplay scripts, Animator, colliders or Rigidbody. Use separate part records for left/right wristbands or sleeves with different sockets. Skinned garments use the appropriate Top, Shorts or Footwear slot; other equipment uses Wristband, Gloves, ArmSleeve, Headband or Accessory. Author variant meshes with supported-body lists and a shared variantFamilyId as necessary. The front-end creator/preview uses these same contracts; temporaryVisual distinguishes functional stand-ins from missing-art placeholders.

## Heights and authored physiques

| Band | Height choices | Stored centimetres |
|---|---|---|
| Short | 5'7, 5'9, 5'10 | 170, 175, 178 |
| Average | 5'11, 6'0, 6'1 | 180, 183, 185 |
| Tall | 6'2, 6'3, 6'4 | 188, 191, 193 |
| Very Tall | 6'5, 6'6 | 196, 198 |
| Giant | 6'7 | 201 |

The five bands derive from the actual discrete value. Twelve heights × six generated presets = seventy-two combinations, with no position restriction. The original sixty prototype combinations remain covered as legacy fallbacks. Profiles expose leg, torso and arm lengths, shoulder height, head placement, camera and nameplate offsets. Anatomical measurements are **authoring/calibration metadata**, left at zero until measured; the foundation does not pretend these slots reshape limbs.

The current adapter uses its calibrated 183cm reference and foot pivot: scale = selected cm / reference cm. Position is adjusted around local (0,-1,0), preserving the prototype's grounded feet while its gameplay root/capsule stays unchanged. The legacy fitted six-foot prototype is **2 world units** tall; this is an explicit legacy visual calibration, not a claim of metre-accurate art. The parametric source retains the existing renderer space and records its measured 1.743155m stature with a separate 183cm measurement calibration. Preserve that space/rig until a separately validated production migration. Do not resize gameplay to compensate for art.

No independently scaled leg/torso/arm bones are applied. Non-uniform proportions require an authored skin calibration with the same rig and bind poses, named by `proportionCalibrationId` on the height profile and every non-empty real part. The prototype adapter rejects a nonempty calibration ID. A calibrated mesh may alter body surface proportions without altering the shared rig; changing joint rest positions requires a deliberate rig-version/adapter migration and a new deformation test pass. Camera offsets are exposed for future consumers; this change does not retarget the gameplay camera or ball grip. Nameplate offsets are applied after initial assembly. Tackle/contact/ball anchors stay gameplay-owned.

Head enlargement is adjustable from 1.00 to 1.05 on the shared head chain, with the prototype default 1.00 to preserve its fit. Evaluate 1.03–1.05 on authored heads at gameplay distance, including the neck seam and attached eyes/hair. No per-frame bone correction is added.

| Preset | Family / required silhouette |
|---|---|
| Lean | Tapered: narrow waist/hips, slender limbs and lower distributed mass. |
| Athletic | Tapered: shoulder-to-waist taper with more muscular fullness. |
| Built | Power: broad chest/back, strong V-taper and developed muscle contour. |
| Thick | Power: increased muscle volume/density while retaining the V-taper. |
| Heavy | Barrel: broad, substantially cylindrical trunk, hips and upper legs. |
| Massive | Barrel: more abdominal projection, trunk depth, hip/glute and thigh volume. |

The original five-skin plan is superseded by the six-preset parametric generator above: one shared headless topology, three normalized anatomical profiles and independent measured targets. Do not replace it with global sideways scaling. Production retopology and corrective shapes remain an artist task and require a new canonical version if vertex identity changes.

## Delivered slice and explicit missing art

`Assets/_Project/Data/Characters/PrototypeCatalog.asset` contains five body slots, three face slots, shaved/cropped hair slots, clean/beard facial-hair slots, two hair colors and three skin colors. Twelve height profiles and three saved example appearances (170/183/201cm) are supplied.

Only Athletic uses the existing monolithic prototype. Lean/Thick/Heavy/Massive are marked placeholders and retain that stand-in. All three face slots retain the **same** integrated face. Cropped hair and beard have no mesh and are omitted; shaved/clean are intentional empty choices. Skin/hair material separation and real numbered team apparel await authored assets. No new character mesh, texture, copyrighted source or misleading generated substitute art is included.

## Canonical rig and Unity imports

`Assets/_Project/Data/Characters/CanonicalSkeleton.asset` references the imported `PlayerBody.fbx` and its primary skin as the current **v1** reference. This is a replaceable contract asset, not a permanently hardcoded dependency in gameplay. Full paths are relative to the FBX's Animator; the exact hierarchy is listed below. Preserve fingers, twist bones, end bones, local axes, parentage and rest TRS. Matching leaf names or obtaining a green Humanoid check alone is insufficient.

Validation requires a valid Humanoid source Avatar, full bone-path mapping, reference local rest transforms, and inverse bind matrices within 0.001; rotations tolerate 0.1 degree. Every skin bone must belong to the reference. Material slots must match submeshes and contain valid materials. New attachments must use the same renderer coordinate space as the canonical primary skin (identity Models children); do not export arbitrary object transforms around an otherwise matching armature. The runtime adapter retains the canonical Avatar and animation controller.

See the [Unity rig import reference](https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/asset-types/models/importing/fbximporter-rig). Unity import baseline: Rig = Humanoid, Avatar Definition = Create From This Model for the reference; copy the canonical Avatar where using an identical hierarchy. Confirm valid human mapping in Configure. Optimize Game Objects stays off while modular sockets need exposed transforms. Mesh import must not add colliders. Use at most four normalized bone weights per vertex initially. Preserve authored normals/tangents and blend shapes where needed. Root motion is off in the visual Animator. Preserve existing FBX `.meta` files and GUIDs when reexporting; avoid changing root axes/scales or clip root-transform settings casually.

Run idle, standard/fast/backward run and Jump on every test height and mesh. Walk, cuts, throws, catches, blocks, tackles and celebrations need source clips before deformation can be signed off. Inspect shoulders, elbows, neck, groin/hips and bent knees in extreme poses; fix weight painting and topology in Blender. Shared animation does not eliminate skinning QA.

## Blender / face / attachment authoring

1. Import the canonical source rig for reference, keep an untouched collection and work in a separate mesh collection. Confirm the file's provenance/licence before shipping the prototype model; this change does not establish rights to the existing imported art.
2. Use metres, apply mesh object rotation/scale before binding, use one common armature and a consistent T/A rest pose matching the contract. Preserve bone roll/orientation and inverse binds. Export selected mesh + armature as FBX, disable added leaf bones and animation export for static parts, and use one documented axis conversion consistently. Reimport into Unity and validate instead of assuming FBX names prove compatibility.
3. Start at six feet/Athletic. Model body variations around the same skeleton. Use consistent neck seam ring topology, vertex positions, normals, UV borders and weights across all bodies/heads. Keep the cut ring fixed relative to the canonical Neck/Head transforms; record its exact mesh-space position in the production source file. The legacy monolithic model has no neck seam. The new parametric canonical defines its separate v1 interface in `SourceAssets/Characters/BodyGenerator/neck-interface.json`; use that interface for future compatible heads.
4. A separate head skin includes the neck transition and uses the canonical Head/Neck bones and matching bind poses. Its renderer is in canonical mesh space. Add authored lower-detail skins in Lower Detail Levels; supply descending screen-height thresholds and validate each skin against the same contract. Eyes, brows and mouth use fixed documented local landmarks in the production template. Eye height/spacing, pupil forward axis and the scalp envelope must be consistent enough for the selected compatibility family. Do not depend on live bone discovery by vague name.
5. Author three visibly different bald heads first: jaw width/angle, cheekbones, brow, nose bridge/tip/width, eye shape, age, asymmetry and silhouette. Faces must remain distinguishable without hair/beards. NFL Street **1** is the aesthetic benchmark: grounded athletic stylized realism, slightly emphasized features, varied skin tones and lived-in character. Avoid generic symmetry, glossy photoreal skin, anime eyes, giant heads or extreme caricature. Use geometry and purposeful albedo/roughness, not increasingly complex skin shaders. Do not copy or redistribute NFL assets.
6. Hair can be a rigid Head socket attachment or a skinned mesh for longer styles. Author fades, curls, braids, locs and shaved variants with deliberate silhouettes. Use shared colorable material slots rather than separate meshes/materials for every color. `scalpProfileId` on faces and `supportedScalpProfiles` on hair/headbands describe scalp-envelope compatibility; `compatibleFaceIds` can narrow unusual fits. An empty compatibility list means unrestricted, so constrain it intentionally when art needs fitting.
7. Beard/moustache skins follow the appropriate head/jaw envelope. Use explicit compatible-face IDs and skin/hair tint slots where required; clean-shaven needs no extra head mesh. Keep seams/alpha edges clean across skin colors. Optional facial blend shapes should use agreed names such as Blink_L/R and Smile_L/R across all heads; no facial-animation system is included.
8. Apparel requires appropriate body variant meshes where silhouettes differ. Mask/hide covered geometry in authored variants as needed; do not rely on deep intersection. Check sleeves/gloves/wristbands at elbow/wrist extremes, shorts at hip/knee extremes, footwear at ankles, and headbands through hair. Never solve clothing fit by editing gameplay colliders.
9. Use shared URP materials with consistent slots, UV layouts and atlas families; prefer geometric/readable surfaces at match distance over expensive skin effects. Create LOD meshes/rigged garment variants in the authoring source. No automatic mesh combining is performed.

## Adding content and previewing

1. Import the authored FBX, validate its Humanoid/rest/bind compatibility, then create **Blacktop > Characters > Part Preset**. Give it a permanent ID and slot. Assign the canonical skeleton contract and either the imported SkinnedMeshRenderer or a render-only attachment prefab/socket. Set material tint roles, compatibility lists and calibration ID. Remove `placeholder` only when usable art is assigned.
2. For a body, use the appropriate archetype, turn off `useExistingBody`, assign its skin and set `integratedHead` appropriately. A separate real face is rejected while the body still includes a head. Keep eye renderers in the head package after replacing the prototype head.
3. Add the preset to PrototypeCatalog (or a production catalog) and choose its ID in an appearance. No runtime C# change is necessary. Add a profile from the twelve discrete values for new calibrated height settings. Keep height/archetype independent from football position and ratings.
4. Open **Blacktop > Characters > Appearance Preview**. Select catalog, appearance, visual prefab, team and number; use ID/discrete-height dropdowns and a height-profile asset, then **Assemble / refresh preview**. Changes to the appearance edit the shared asset with normal Inspector Undo behavior. The isolated preview only creates the visual prefab, not fourteen gameplay stacks. Locomotion previews idle/backward/standard/fast states. A rejected selection shows diagnostics and a safe prototype view.
5. Assign the appearance to a roster row. Leave it blank to use the Player prefab default. Start SampleScene to verify the match-facing integration. Use `ApplyTeam` for explicit affiliation refresh; nothing polls team affiliation per frame.
6. **Validate All Appearance Assets** checks current data. **Generate Missing Foundation Assets** creates only absent assets and preserves GUIDs/Inspector edits. **Wire Existing Player Prefabs** explicitly saves the two existing prefabs; use for initial setup/recovery after saving work. The shipped prefabs are already wired.

## Performance and remaining validation

There is no character Update loop and no runtime mesh combining. The existing PlayerAnimationPresenter and debug tint polling remain unchanged. Initial assembly creates only requested attachments; repeated identical selections reuse them. Shared meshes, controllers and materials are retained. Skinning cost increases with each authored attachment, so budget renderer counts before growing the catalog. The current five-renderer prototype is a baseline, not a target to multiply by every equipment slot. Rigid prefabs can carry authored LODGroups. Skinned presets can supply Lower Detail Levels plus descending screen-height transitions; the assembler maps every LOD to the same bones and creates a LODGroup. No lower-detail character meshes ship yet, so the prototype retains its original cost. Profile production LOD assets before choosing thresholds.

Measure 14 players on each target device at the actual gameplay camera: draw calls/batches, triangles, Animator/skinning CPU/GPU time, managed allocation spikes, loaded material count and memory. Consider common eye/head consolidation and atlases when profiling justifies it. MaterialPropertyBlocks avoid material clones and disable SRP Batcher compatibility for affected renderers; measure instead of promising batching. See the [Unity batching documentation](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/reduce-draw-calls/draw-call-batching/properties). Menu previews are editor-only here. Do not treat Editor timings or the absence of new materials as a frame-rate guarantee.

See TESTING.md for actual run results and remaining manual checks. Windows/controller play, deformation in missing action clips, production seams/clothing, and real art distinctiveness remain unverified until their assets exist.

## Canonical hierarchy (relative to Animator)

The following was read from Unity's imported model. The contract asset, source FBX and `.meta` remain authoritative for numeric rest transforms and binds.

```text
Root
  Hips
    LeftUpperLeg
      LeftLowerLeg
        LeftFoot
          LeftToes
            LeftToesEnd
    RightUpperLeg
      RightLowerLeg
        RightFoot
          RightToes
            RightToesEnd
    Spine
      Chest
        UpperChest
          LeftShoulder
            LeftUpperArm
              LeftLowerArm
                LeftHand
                  LeftIndexProximal
                    LeftIndexIntermediate
                      LeftIndexDistal
                        LeftIndexDistalEnd
                  LeftMiddleProximal
                    LeftMiddleIntermediate
                      LeftMiddleDistal
                        LeftMiddleDistalEnd
                  LeftPinkyProximal
                    LeftPinkyIntermediate
                      LeftPinkyDistal
                        LeftPinkyDistalEnd
                  LeftRingProximal
                    LeftRingIntermediate
                      LeftRingDistal
                        LeftRingDistalEnd
                  LeftThumbProximal
                    LeftThumbIntermediate
                      LeftThumbDistal
                        LeftThumbDistalEnd
                LeftLowerArmTwist
              LeftUpperArmTwist
          Neck
            Head
              HeadEnd
            NeckTwist
          RightShoulder
            RightUpperArm
              RightLowerArm
                RightHand
                  RightIndexProximal
                    RightIndexIntermediate
                      RightIndexDistal
                        RightIndexDistalEnd
                  RightMiddleProximal
                    RightMiddleIntermediate
                      RightMiddleDistal
                        RightMiddleDistalEnd
                  RightPinkyProximal
                    RightPinkyIntermediate
                      RightPinkyDistal
                        RightPinkyDistalEnd
                  RightRingProximal
                    RightRingIntermediate
                      RightRingDistal
                        RightRingDistalEnd
                  RightThumbProximal
                    RightThumbIntermediate
                      RightThumbDistal
                        RightThumbDistalEnd
                RightLowerArmTwist
              RightUpperArmTwist
```

## Recorded measurements (2026-10-22)

| Measurement | Before | Foundation |
|---|---:|---:|
| Active player instances | 14 | 14 |
| Character skinned renderers | 70 | 70 |
| Character vertices across players | 268,590 | 268,590 |
| Distinct shared character materials | 2 | 2 |
| Distinct shared character mesh runtime memory | Same unchanged meshes | 4,286,668 bytes |

The initial unmodified-prefab Editor audit measured 3.350ms to instantiate fourteen objects in Edit Mode (no Start callbacks). This is a single sample, not a gameplay timing. The final graphics-enabled Play Mode preview measured 25.790ms for fourteen repeated validated TryApply calls; this includes preflight checks and is an event cost, not a frame cost. Those two operations are different and are not a speed comparison.

A 1600×900 native URP scene snapshot with fourteen players reported 287 draw calls and 1,739,869 triangles through UnityStats. It includes the field, shadows and scene rendering passes, so it is not a character-only count. No matching baseline draw-call capture was taken. ProfilerRecorder draw/batch/skinning counters were unavailable and the animation marker returned zero in batch mode, so no CPU/GPU skinning or frame-time claim is made. Editor-wide allocated memory in that capture was 419,522,658 bytes; this is not incremental character memory and is not comparable to the initial headless audit's total.

Visually inspected native Unity renders at 170/183/201cm in a common idle pose, plus fourteen players in running poses. The three-height image runs shortest to tallest from left to right. Grounding is visually consistent; foot bones sit above the sole as expected. Prototype integrated face/eyes remain attached. Cached Editor QuickSearch startup produced its pre-existing indexing exception in the isolated project; it did not fail the gameplay suite. Full target-platform profiling and all production art/action-pose checks remain outstanding.

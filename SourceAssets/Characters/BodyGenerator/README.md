# Blacktop Body Editor

Open **CanonicalBody.blend**. In Blender's Scripting workspace, select the **body_editor.py** text and click **Run Script** once per session. Return to Layout; open the right sidebar with **N** (or View → Sidebar), then click **Blacktop**. No numeric keypad or middle mouse button is needed. Python auto-run does not need to be enabled. Keep the entire BodyGenerator folder together.

1. Choose Lean, Athletic, Built, Thick, Heavy or Massive. Switch family to explore Tapered, Power or Barrel without reopening Blender.
2. Adjust the ten percentage sliders or click their numbers and type. Front / Side / Three-quarter buttons change the inspection camera. Preview shell clothing optionally.
3. Use **Duplicate preset**, edit **Rename**, and **Save JSON preset** to keep variations. Saving the .blend also keeps the per-preset edits. Sculpt/Edit Mode strokes are captured as per-preset offsets when switching, adjusting sliders, saving JSON or exporting; JSON includes those corrections. Keep Dyntopo/remeshing off and preserve the fixed neck collar. Reset restores a stock preset from its JSON; a duplicate resets to its initial snapshot. Loading a custom JSON establishes its reset snapshot.
4. Select the **Unity target** explicitly, then **Export selected**. Custom duplicates retain their own JSON IDs; this target control declares which of the six installed mesh targets their geometry updates. **Export all six** exports the current six named presets to `GeneratedBodies.json` (25 meshes). Selected export writes `GeneratedBody_<target>.json` (5 meshes) so it cannot replace your all-body export.
5. In Unity, use **Blacktop → Characters → Blender → Apply Blender Mesh Export** and choose `Builds/CharacterExports/GeneratedBodies.json` for all bodies, or the named `GeneratedBody_<target>.json` for a selected body. The initial 25 targets and catalog entries are already installed in this change. For a fresh migration use **Body Generator → 1 Prepare Export Targets**, export all six, apply, then **2 Register Validated Bodies**.
6. In Players → Physique, selecting a build chooses the generated body and corresponding shell variants. Existing saved appearances keep their original IDs until you change the build. The current detached prototype head and original eye skins are temporary visual fallbacks.

The new `.blend` is the editable workspace; **canonical.json.gz is the immutable authoritative topology**, with source provenance and the unchanged full rest rig. Sliders always evaluate from this reference. Never sculpt the canonical object, change its rig, or use `build_sources.py --overwrite`. Do not export old independent snapshots over generated geometry. Legacy files are untouched fallbacks. [AUDIT.md](AUDIT.md) documents exact paths, topology limits, migration and rollback.

## Measurements

Every percentage uses **athletic-183-v1**, the measured unmodified Athletic source, independent of the selected family. 110% chest means 1.10 × that canonical chest circumference. The Athletic *preset* itself can differ from this baseline. The panel shows Reference / Requested / Measured in inches or centimetres. Height changes those calibrated readouts only; Unity's twelve discrete uniform height profiles remain independent of body deformation. Blender geometry stays in canonical renderer space with fixed joint chains.

Canonical source stature is 1.743155m. `calibration = 1.83 / source_stature`; dimension at height H = mesh dimension × calibration × H/183. Inches divide metres by 0.0254; centimetres multiply by 100. The panel does **not** claim Unity's legacy two-unit footprint is metre-accurate. The canonical 183cm reference dimensions, approximately:

| Measurement | Reference inches | Landmark in Unity mesh space |
|---|---:|---|
| Shoulder width | 19.27 | Bi-deltoid surface landmarks sampled at canonical Y=1.405m; fixed barycentric vertex identity |
| Chest circumference | 40.88 | Closed torso tape at Y=1.285m, below the armpit junction |
| Waist circumference | 32.71 | Closed torso tape at Y=1.045m |
| Quad circumference | 21.93 | Bilateral mean; 40% hip→knee, plane normal to bone axis |
| Calf circumference | 14.86 | Bilateral mean; 42% knee→ankle |
| Bicep circumference | 13.63 | Bilateral mean; 48% shoulder→elbow |
| Forearm circumference | 10.16 | Bilateral mean; 42% elbow→wrist |
| Chest depth | 10.16 | Sagittal extent at chest plane |
| Abdominal depth | 8.98 | Sagittal extent at waist plane |
| Hip/glute depth | 9.18 | Sagittal extent at Y=0.910m |

Sections use triangle/plane intersections, welding coincident UV duplicates only for measurement at 0.01mm resolution. The closed component nearest the anatomical origin supplies the tape length; width and depth are independent extents. Bilateral sections are measured separately and reported together. The source is symmetric within numerical tolerance. No separate left/right sliders exist.

`profiles.json` contains three versioned normalized station distributions, anterior/posterior bias and limb contours. Circumferences are **not** achieved by assuming a uniform radius scale. A damped coupled numerical solve finds independent shoulder, torso width/depth and limb radial parameters, evaluating the resulting actual cross sections. Every evaluation starts from canonical data. **Surface softness** rounds the trunk envelope and relaxes the welded surface around the pelvis; Massive defaults to 1 for a fat silhouette, while the other five presets stay at 0. Sculpt offsets participate in the measured solve and remain separate from immutable geometry. Undo/redo restoration suspends regeneration so it cannot overwrite recovered strokes. Symmetric radial fields fade at joints; bones, hands, feet, lengths, bind poses, UVs and compatible skin weights stay fixed. The collar above Y=1.495m remains fixed. `neck-interface.json` defines the future head seam at Y=1.515m, shared forward/up orientation, mesh-space center and ring coordinates. Future heads must match positions, normals/UV border and skin weights.

The export tolerance is **1% relative error per requested measurement**. The solver normally exits below 0.15%. The panel reports unachievable combinations and blocks export outside tolerance; the 60–180% slider range does not promise every extreme combination is anatomically feasible. A percentage is always relative to the explicit baseline, never relative to the previously selected preset. Export sidecars record reference/requested/measured values and provenance. Neutral shell offset is 4mm; this is a preview fit, not production tailoring.

## Reproduction

Use the installed Blender executable (Windows: `blender.exe`; macOS: `/Applications/Blender.app/Contents/MacOS/Blender`). These repo-relative commands work from the repository root:

```sh
blender -b --python SourceAssets/Characters/BodyGenerator/validate.py -- --tapered
# Apply TaperedBodies.json / run GeneratedBodyValidation.ImportTapered in Unity.
blender -b --python SourceAssets/Characters/BodyGenerator/validate.py -- --render
# Apply GeneratedBodies.json, then register, or run GeneratedBodyValidation.ImportAll.
```

Blender validation covers all presets, twenty independent slider perturbations, measurement errors, bilateral symmetry, deterministic resets, canonical immutability, UV/weight/topology preservation, a fixed neck, panel switch/duplicate/rename/reset/JSON, actual baked export and posed-rig rejection. Evidence and eighteen rendered views are under ignored `Builds/Validation/BodyGenerator`. `GeneratedBodyTests` samples the five installed clips across all six bodies, twelve heights and three poses, checking finite body/garment skinning, animated neck continuity, hand anchors, gameplay invariants, old save IDs and cross-fit rejection. Numerical skinning checks do not certify artist acceptance of all joints or intersections.

`prepare_source.py` is a one-time conversion for a new version directory. It refuses to replace the canonical `.blend` or JSON. Ordinary editing/export never runs it. `validate_sources.py` remains restricted to the seven original legacy snapshots; the generator has its own validation.

Rollback: choose original body/face/garment IDs on affected saved players, then revert the feature together. Keep the new IDs installed while saved appearances reference them. Original sources, skeleton, FBX, prefabs and mesh IDs remain available.


## Export audit and refinement (2026-10-25)

The submitted GeneratedBodies.json was a selected Heavy export: five meshes, revised slider measurements, and no vertex difference from procedural generation. The live session contained additional Lean/Built/Thick/Heavy slider edits; these are now saved in Presets. The original exporter regenerated geometry and did not capture sculpt strokes. The saved and checked undo snapshots contained no surviving sculpt offsets. An untouched snapshot of the starting live session remains in SculptSessions/2026-10-25-UserEdits.blend, and the submitted JSON is backed up under ignored Builds/Validation/BodyRefinement. The corrected all-body export preserves the original Heavy body, clothes and head positions exactly.

Massive now uses a round soft trunk, a smoothed chest/abdomen envelope, a less exaggerated shoulder width and a smoother hip-to-thigh transition. Matching shells are regenerated from that solved surface. The shared topology, UVs, skin weights, neck, skeleton, asset GUIDs, gameplay and ratings remain unchanged. Validation now includes sculpt switching, JSON/export/reset, mode preservation, selected-export file isolation and undo-handler cleanup. See Validation/Massive-before-after.png and TESTING T-108.

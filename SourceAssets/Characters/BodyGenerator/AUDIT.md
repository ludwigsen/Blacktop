# Canonical body audit — 2026-10-25

Audited clean `master` `bd6686b2ca966dffbedd0d840a57c67c668dc198`, verified against GitHub master. Work is on `codex/parametric-character-bodies`.

Selected source: **SourceAssets/Characters/Bodies/Body_Athletic.blend**, SHA-256 `37db4941667bf1de7900e061ca84c008a79bf90a57c98126a2af378b732303cb`. This is an independent snapshot whose positions match the original Unity reference seed. The Athletic body in BlacktopCharacters.blend matches it. All five existing individual body files have 18,277 vertices / 28,796 triangles / one UV layout. None is overwritten. `build_sources.py --overwrite` was never used.

The canonical skin/Avatar is `Assets/_Project/Prefabs/PlayerBody.fbx`, defined by `Assets/_Project/Data/Characters/CanonicalSkeleton.asset` (`blacktop.humanoid.v1`). The Blender source has 58 hierarchy nodes, and body skins use 57 ordered bones, at most four normalized weights. Blender stores Z-up `(x,-z,y)` from Unity X-right/Y-up/Z-forward. Objects remain identity; renderer-local vertex coordinates and Unity bind poses remain authoritative. The imported rig has a valid Humanoid Avatar. `PlayerModel.prefab` remains the same wrapper, including its eye mesh overrides and visual calibration.

## Suitability and limitations

The Athletic source is suitable for a **procedural prototype foundation**, not a production topology endorsement. It is a triangulated, UV-seam-split imported skin with an integrated face and dense facial topology. It has no authored modular neck ring, no production muscle loops/correctives, no production clothing seams, and no supplied action clips for throws/catches/blocks/tackles. The rig's A-pose and joint positions constrain shoulder/limb reshaping. We expand surfaces around fixed joint chains; neither arm/leg length nor bone rest transforms change. Large body targets can still intersect bent limbs or garments and must pass visual acceptance.

The new canonical is a deterministic plane cut at Unity mesh Y=1.515m. It retains lower original vertex order and UV/skin data, appending interpolated edge intersections only. The clipped face becomes a **detached, explicitly prototype head fallback**. The four original eye meshes remain separate and unchanged. No finished heads are fabricated. Body and head seam positions, UV interpolation and weights match; collar positions and original normals are locked. `neck-interface.json` records the 79 unique ring positions, duplicate mesh indices, orientation and center. Future heads must match this interface or declare a new version; this ring is not a claim of artist-approved production retopology.

The original source stature is approximately 1.743155m. Readouts calibrate by `1.83/source_stature`; they describe the authoring reference at a chosen height. The existing Unity adapter still maps 183cm to its legacy two-world-unit visual footprint. These are explicitly different calibrations. We do not scale skeletons to force metre-accurate gameplay or change any existing height profile.

## Existing catalogs, saves and preview

- `Assets/_Project/Data/Characters/PrototypeCatalog.asset`: original five prototype body IDs and missing-art face/hair slots. Preserved.
- `Assets/_Project/Data/FrontEnd/FrontEndCatalog.asset`: five temporary bodies and fifteen garment shells, plus legacy prototype slots. Extended additively with six generated bodies, eighteen matching shells and one detached prototype head.
- Existing mesh assets remain under `Assets/_Project/Art/Characters/Temporary/`. Existing body/garment part IDs retain their meanings and GUIDs.
- New meshes: `Assets/_Project/Art/Characters/Generated/v1/{Body,Top,Shorts,Footwear}_{Lean,Athletic,Built,Thick,Heavy,Massive}.asset` and `Head_Prototype.asset`.
- New part records: matching names under `Assets/_Project/Data/Characters/Generated/v1/`.
- New IDs: `body.<name>.parametric.v1`, `gear.<slot>.<name>.parametric.v1`, `face.prototype.parametric.v1`. Built is appended as enum value **5**; existing 0–4 serialization remains unchanged.
- `AppearanceSelection` and `RosterSaveData` already save explicit IDs; no save schema or roster migration is required. Merely loading an old save retains old geometry. Only a deliberate build change selects generated geometry.
- `PlayerVisualRig.ApplyHeight` remains uniform around its foot pivot. All twelve 170–201cm profiles remain independent of body shape. `PlayerAppearancePreview` / `RuntimePlayerPreview` continue to use the existing assembler.

## Required migration and rollback

New export targets are prepared with canonical bind poses. v2 exports are restricted to the 25 declared new paths and exact target GUIDs, and include the full canonical hierarchy/rest matrices. Registration occurs only after headless-body/head checks pass. Legacy v1 export remains available for legacy sources. Never use a legacy combined/focused snapshot to overwrite generated targets, or vice versa.

Matching shells are generated from the solved body topology with a 4mm outward normal offset and identical inherited weights. They are working prototype fits; open boundaries, seams, elbow/hip penetration and production garment construction remain explicit art limitations. Exact-body compatibility rejects cross-fitting a legacy shell onto a generated body. No cloth simulation, collider, football rating or roster identity changes are made.

Rollback an individual character by selecting its old `body.<name>.temporary` (or original prototype) ID, original face ID, and corresponding original garment IDs. To roll back the whole feature, revert this branch's changes together, including the appended Built enum and catalog references. Do not remove new IDs while saved characters reference them; export those saves and switch their appearance first. Original `.blend`, FBX, meshes, GUIDs, prefabs, skeleton and authored roster rows remain intact throughout.

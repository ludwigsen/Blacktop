# Current parametric workflow

Use **BodyGenerator/CanonicalBody.blend** and its **Blacktop Body Editor** sidebar for the six-preset procedural system. Start with [BodyGenerator/README.md](BodyGenerator/README.md) and [AUDIT.md](BodyGenerator/AUDIT.md). The files below remain untouched legacy snapshots/fallbacks and use the existing v1 export route. They do not target the new generated meshes.

# Editable Blacktop character sources

**Open `BlacktopCharacters.blend` to edit all current character geometry in Blender.** The five body builds, integrated faces, fifteen clothing meshes and four eye meshes are real editable mesh objects with UVs, skin weights and the shared rest rig. Unity still consumes ordinary mesh assets; installing Blender is not required to open/play Blacktop.

## Files

| Source | Contents |
| --- | --- |
| `BlacktopCharacters.blend` | Recommended main authoring file: all 24 current meshes, grouped by build, plus the canonical rig. Athletic/Eyes are visible initially; reveal the other build collections in the Outliner. |
| `Bodies/Body_Lean.blend` | Lean body including its face, top, shorts, footwear, eyes and rig. |
| `Bodies/Body_Athletic.blend` | Athletic body including its face, top, shorts, footwear, eyes and rig. |
| `Bodies/Body_Thick.blend` | Thick body including its face, top, shorts, footwear, eyes and rig. |
| `Bodies/Body_Heavy.blend` | Heavy body including its face, top, shorts, footwear, eyes and rig. |
| `Bodies/Body_Massive.blend` | Massive body including its face, top, shorts, footwear, eyes and rig. |
| `Faces/PrototypeFace.blend` | Head-focused editing file with the full Athletic mesh (including face), four eyes and rig. The head is integrated, not a separate production head mesh. |
| `Reference/character_seed.json.gz` | Exact Unity geometry/weights/rest-rig reference used for the initial conversion, not the editable source of truth after artists make changes. |
| `export_meshes.py` | Export edited meshes from the currently open `.blend` to the Unity mesh bridge. It does not rebuild or save over the `.blend`. |
| `build_sources.py` | Initial seed conversion/recovery utility. Refuses to overwrite existing `.blend` files unless explicitly passed `--overwrite`. |
| `validate_sources.py` | Reopen sources, compare them with the initial seed, export a test edit and render previews. Seed fidelity checks apply to the initial conversion; deliberately sculpted sources will differ. |
| `Validation/CharacterBlenderRoundTripQA.cs` | Disposable-project Unity validation driver; never run its real mesh-write checks in the working art checkout. |

The combined and individual files are **independent editable snapshots**, not linked Blender libraries. Use the combined file as your main source. A per-build/head file is an alternative starting point, not an automatically synchronized copy. They target the same Unity mesh IDs; exporting an older copy later overwrites geometry from a newer copy. Choose one source for an asset and keep it authoritative. Rename/save copies deliberately when exploring variants.

## Edit and return to Unity

1. Open the main `.blend` or the individual source you intend to maintain. Select the mesh in the Outliner. Hide clothing to sculpt the underlying body.
2. Edit vertices in Edit/Sculpt Mode. Each body has an `EDIT_FACE_HEAD` vertex group for selecting/isolating its head/neck. This group is an authoring mask, not another deforming bone. Faces, clothes and eyes can all be edited directly.
3. Keep object transforms and the canonical bone hierarchy/rest pose unchanged. The source contains 58 bone nodes, including the ancestry node; body skins retain their original 57-bone mapping and eye skins retain their Head-only mapping. You can pose for inspection, then clear the pose before export. One Blender unit is one metre; coordinates are converted consistently to Unity's Y-up mesh space. Existing Unity height calibration remains responsible for the final actor height.
4. Save the `.blend` normally. Saving alone does not change Unity. In Blender's Scripting workspace, open/run the included `export_meshes.py` Text block, or run the external script below. Exports go to ignored `Builds/CharacterExports/<blend-name>.json` by default.
5. In Unity, use **Blacktop > Characters > Blender > Apply Blender Mesh Export** and select that JSON. This validates every mesh before writing any asset and updates the existing `.asset` contents with Undo support. Review the `.blend` and Unity mesh diffs together in Git.
6. Reopen the player preview/match and inspect silhouette, feet, shoulders, neck, clothing fit and idle/run/hurdle deformation at every affected height. Source changes do not modify ratings, roster GUIDs, root colliders, field geometry or animation controllers.

Example from the repository root (Blender on PATH, Windows/macOS):

```sh
blender --background --python-exit-code 1 SourceAssets/Characters/BlacktopCharacters.blend --python SourceAssets/Characters/export_meshes.py
```

To choose a destination:

```sh
blender --background --python-exit-code 1 SourceAssets/Characters/Bodies/Body_Heavy.blend --python SourceAssets/Characters/export_meshes.py -- --output Builds/CharacterExports/Heavy-edited.json
```

Export includes all Unity-targeted mesh objects in that file, including hidden collections. It exports base geometry in the rest pose, not the evaluated posed mesh. Apply modelling modifiers explicitly; active shape keys, object/rig transforms, renamed/rest-edited bones, unweighted vertices and more than four influences per vertex are rejected. UV/normal seams are split when needed, preserving their per-corner values. Keep a UV map; normalized weights are written with the existing bone-name order. Materials in the `.blend` are readable authoring previews. Team/skin/hair palette colors and shader settings remain in Unity's existing data assets and are not overwritten by the geometry bridge.

Existing Unity targets are the twenty body/garment assets under `Assets/_Project/Art/Characters/Temporary/` and four `Eye_*.asset` meshes in the same folder. PlayerModel.prefab now references those editable eye mesh copies with the original bone/material bindings; the original FBX/skeleton/Avatar remains intact. Asset GUIDs and bind poses are retained on import. The bridge rejects unknown targets, mismatched IDs/bones, non-finite geometry, malformed topology/weights and unsupported versions. Failed validation leaves earlier meshes unchanged.

## Existing versus missing artwork

This converts the **actual current artwork** into editable `.blend` source. It does not invent thirty finished face variants or missing hair/beard/gear meshes. The three face IDs still use the same integrated prototype head; edit the head geometry within a body or in PrototypeFace.blend. That head-focused file exports its Athletic body and shared eyes. Producing independent interchangeable faces still requires an authored headless body/head seam and compatible presets.

Hair, facial hair and additional gear with no installed geometry remain missing-art slots in Unity. Create their real models in Blender and retain their `.blend` sources when authoring them; follow `Assets/Docs/MODULAR_CHARACTERS.md` for compatible skinned/rigid attachments and preset IDs. The existing-target bridge intentionally does not create arbitrary new catalog parts or replace the skeleton. Height/weight/stat configuration stays in Unity data, not in a Blender file.

**Character-art source rule:** retain editable `.blend` source for every body, head, hairstyle, facial-hair mesh and garment added to Blacktop. Export/bridge the result into Unity; do not ship a newly generated mesh as its only editable source.

## Rebuilding the initial conversion

Do not run this to export manual art edits. It discards edits if deliberately forced.

1. In a disposable checkout or after saving your art elsewhere, use **Blacktop > Characters > Blender > Export Unity Reference Seed**. This records the current installed meshes/rest rig. If necessary it creates editable eye meshes once and wires PlayerModel's mesh references; it does not replace the FBX rig.
2. Run `blender --background --python-exit-code 1 --factory-startup --python SourceAssets/Characters/build_sources.py`. Existing sources are protected. Use `-- --overwrite` only when deliberately replacing all generated `.blend` files with the seed state.
3. Run `blender --background --python-exit-code 1 --factory-startup --python SourceAssets/Characters/validate_sources.py` for initial seed fidelity, transform rejection, test export and rendered previews. It opens/sculpts only in memory and never saves the test edit over a source.

Converted and validated with Blender 5.2.1 LTS and Unity 6000.5.6f1. All seven saved files reopen, 69 mesh objects across the source snapshots exactly match the seed positions/weights (maximum position error 0), and a 24-mesh export plus a real 0.0123m vertex edit round-trip through Unity preserving triangle geometry, asset GUIDs and bind poses. Object transform/posed-rig changes are rejected. Unity's disposable-project driver additionally checks invalid weights, invalid GUID/path/topology/bones and no partial write on failure. Current regression results are recorded in TESTING.md.

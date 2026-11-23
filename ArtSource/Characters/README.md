# Blacktop production characters

Each character follows the same production path.

**Approved MPFB source → optional sculpt → fitted Blender rig → FBX and local Unity Humanoid Avatar → visual prefab → roster player or preview.**

| Directory | Role |
|---|---|
| Core | Immutable approved MPFB files, with their original helpers and shape keys. |
| Sculpt | Independent working files. Athletic uses the approved edited skin in BT_Athletic_Sculpt_v1.blend. |
| Rigged | Derived Blender files with one skin and one MPFB game_engine armature. |
| Validation | Source checksums, rig and pose measurements, Unity results, image sheets, migration audit and animation readiness. |
| Tools/CharacterPipeline at repository root | Offline rig builder, FBX export, deformation checks and isolated Unity review drivers. |
| Assets/_Project/Art/Characters/Models | Unity FBXs and their retained importer metadata. |
| Assets/_Project/Prefabs/Characters | One visual-only prefab per stable body ID. |
| Assets/_Project/Data/Characters/MPFB | Fitted skeleton contracts, body presets and shared skin material. |

## Reproduce a body

Run from the repository root. Use the installed Blender 5.2.1 and enabled MPFB 2.0.18. Set the shell variable BLENDER to its executable and QA_SCRIPTS to the installed blender-asset-qa scripts directory. These commands need no download. The first command checks the exact source bytes. The rig builder uses a fresh destination so artist-edited derivatives are protected.

```sh
(cd ArtSource/Characters && shasum -a 256 -c Validation/sources.sha256)
"$BLENDER" -b --offline-mode --disable-autoexec --python-exit-code 1 \
  -P Tools/CharacterPipeline/rig_body.py -- Athletic --out Builds/MPFB-Rebuild/Rigged
"$BLENDER" -b --offline-mode --disable-autoexec Builds/MPFB-Rebuild/Rigged/BT_Athletic_Rigged.blend \
  --python-exit-code 1 -P Tools/CharacterPipeline/check_poses.py -- \
  --qa "$QA_SCRIPTS" --out Builds/MPFB-Rebuild/Athletic
"$BLENDER" -b --offline-mode --disable-autoexec Builds/MPFB-Rebuild/Rigged/BT_Athletic_Rigged.blend \
  --python-exit-code 1 -P Tools/CharacterPipeline/export_fbx.py -- Builds/MPFB-Rebuild/Models
```

Inspect the solid and wire sheets, pose intersections and edge distortion, particularly at shoulders, inner thighs and knees. MPFB fits its authored rig to each source. The skin carries normalized weights for at most four deform bones per vertex. Athletic uses the sculpt's verified visible-surface vertex order. A topology mismatch stops the transfer. The mesh rest position, UVs and source hashes provide independent preservation checks.

In an isolated copy of Blacktop, place the candidate FBX at its existing Models path and keep its .meta file. Copy MpfbModelReview.cs and MpfbPrefabReview.cs from Tools/CharacterPipeline into Assets/Editor in that copy. Open with Unity 6000.5.6f1 or run the editor in batch mode, passing the isolated project path and `-executeMethod MpfbModelReview.Run`, followed by a separate invocation of `-executeMethod MpfbPrefabReview.Run`. The first validates the Humanoid mapping and samples the shared controller. The second writes the prefab variants and reopens the six-body review scene. Inspect the captured animation samples under Builds/MPFBReview. Keep the Unity-generated FBX metadata, prefab links and data GUIDs with the accepted model. Neither driver belongs in a player build.

## Assign another player variant

Use an existing character appearance asset or copy it for the player. Set its body ID and matching enum and choose one of the existing height profiles. The production IDs are SLIM, ATHLETIC, MUSCULAR, STOCKY, POWER and MASSIVE. Assign that appearance to the existing player record. Its UUID, ratings, slots and team membership stay with the roster. This change reuses the installed body, Animator Controller and Avatar. It needs no rig or FBX rebuild.

Old numeric builds 0 through 4 retain the Slim, Athletic, Stocky, Power and Massive meanings; Muscular is 5. Old prototype and temporary body ID strings resolve at lookup. A player with no authored appearance uses the existing Athletic default. The selection contains outfit IDs for future fitted garments. Games and previews currently show the MPFB body alone.

The match prefab and the preview instantiate MPFBPlayerModel. Its adapter selects the fitted prefab and binds presentation to that Avatar's bones. The gameplay capsule remains at the actor root. Each fitted rig has the same named hierarchy and its own bind pose. Swapping the complete visual preserves that difference. The historical PlayerModel prefab, original Avatar and eye meshes support the previous animation-authoring reference.

## Interactive six-body review

Open Assets/Scenes/MPFBCharacterReview.unity in Unity, enter Play Mode and use the Game view. The top row isolates a body or restores the lineup. Choose Front, Side or Three-quarter. Each body turns in place to keep neighboring silhouettes separate. The motion buttons sample the production Animator Controller. Pause freezes the pose and dragging the slider pauses and scrubs the selected motion. Play loops it again. Throw and handoff buttons expose the individual action phases. Use an actual match to judge hand-to-ball contact and timing between two players. Walk and jog have dedicated clips. Catch still needs a clip. The Blacktop Animations menu also previews paired handoffs and both hard-cut directions.

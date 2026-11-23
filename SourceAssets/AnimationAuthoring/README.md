# Football animation authoring in Blender

Open **Football Actions.blend** in Blender 5.2.1 or later. This is an editable baseline of the installed football actions from Blacktop `39600e4`. Save a separate work copy before editing. The `.blend` owns the editable motion and the FBXs are review exports. Both live outside Unity's `Assets` directory. Blacktop still plays the original Unity clips.

## What was baked

| Blender scene | Action | Frames at 100 fps | Duration | Playback |
| --- | --- | --- | --- | --- |
| Throw | Throw Windup | 1–16 | 0.15 s | Once |
| Throw | Throw Follow Through | 1–32 | 0.31 s | Once |
| Carry | Carry Ready | 1–125 | 1.24 s | Loop |
| Carry | Carry Tuck | 1–65 | 0.64 s | Loop |
| Handoff | Handoff Give | 1–41 | 0.40 s | Once, QB |
| Handoff | Handoff Take | 1–41 | 0.40 s | Once, runner |

Each source under `Assets/_Project/Art/Animations/` is a Unity `.anim` with 73 Humanoid Animator muscle curves and no bone Transform curves, animation events, or root-motion tracks. Historical C# authoring produced those `.anim` assets. `PlayerAnimationPresenter` layers them over locomotion and adjusts the handoff reach at runtime. `FootballAppearance` positions the real ball separately. There were no imported FBXs for these six motions before this export.

Unity evaluated the original actions on the `PlayerModel` prefab and its `PlayerBody` Humanoid Avatar through the actual Football upper-body mask. A single Breathing Idle pose supplies the hips and legs in Blender. The bake captures both clip endpoints and samples the 58-node skeleton every 10 ms. The paired Handoff scene places both character rigs for side-by-side key editing. The approach, ball path, procedural reach, locomotion blend, and game-driven transitions require Unity to review. The animation file uses the same bone names, parents, and proportions as `PlayerBody`, with FBX handedness converted in the animation copy. The existing character modeling files retain their own coordinate convention.

## Edit and review in Blender

1. Open `Football Actions.blend`. Choose **File > Save As** and put a work copy under `Builds/AnimationEditing/` or another versioned folder. Keep this baseline as a reference. Use the scene selector in Blender's top bar to pick **Throw**, **Carry**, or **Handoff**.
2. In the Outliner select **Throw QB**, **Carrier**, **Handoff QB**, or **Handoff Runner**. Switch to **Pose Mode**. In a Dope Sheet switch the mode to **Action Editor**. Throw and Carry each have two Actions in the Action dropdown. The two handoff rigs already play their separate Actions on the same timeline. Enable **In Front** on the armature object if you prefer bones visible through the skin.
3. Scrub the Action's frame range in the table. Adjust joints in Pose Mode and insert rotation keys with the **Insert Keyframe** menu. Each input sample is a key. For a broad pose change, keep boundary keys, remove or decimate the intervening keys on the specific bones you are changing in the Graph Editor, then shape the arc between your keys. A change to a single dense key may only show for 10 ms. Save the work copy and compare to the baseline at the same frame.
4. Keep the scene names, rig names, Action names, frame ranges, and 100 fps. For Carry, make the last pose match the first. For Throw, check the Windup endpoint against the first Follow Through pose. For Handoff, scrub both actors together. Edit pose bones while keeping the hierarchy, rest transforms, and skin bind intact. The existing Unity Football layer masks out legs and root, so lower-body edits in Blender will be visible in this file and will require a separate runtime review before they can appear in the game.
5. In **Object Mode**, open the **Scripting** workspace, select the `export_actions.py` Text block, and choose **Run Script**. This writes six FBXs to a fresh `Builds/AnimationExports/<timestamp>/` directory. The same export can run headless from the repository root. The destination must be a new directory outside `Assets`.

```sh
blender --background "Builds/AnimationEditing/Football Actions Work.blend" --python-exit-code 1 --python SourceAssets/AnimationAuthoring/Tools/export_actions.py -- --output Builds/AnimationExports/Review-01
```

Each FBX is named for its Action and has one animated rig with the Athletic body as a bound reference. The skin supplies a real FBX BindPose. In the bone-only experiment the first animated pose became the rest pose in an importer. Do not apply object transforms, export leaf bones, or apply key reduction during the FBX export. The exporter removes each actor's staged Handoff placement from the individual FBXs.

## Import candidates into Unity

1. Use a disposable Blacktop copy or isolated review worktree. Create `Assets/FootballAnimationReview/` and copy in the six FBXs. For the first import, use the supplied `.fbx.meta` files from `Reference/FBX/` beside the corresponding FBXs to seed the tested import settings and candidate identities. On revisions, replace just the `.fbx` file in place and retain its Unity `.meta`, file name, and take name. Keep each candidate only once under `Assets`.
2. Copy `Tools/FootballClipRoundTrip.cs` to `Assets/Editor/` in the review project. In Unity select **Blacktop > Animations > Import Football Review FBXs**. It sets Humanoid with a local Avatar using the canonical mapping, scale factor 1, file units and axis conversion on, compression off, and all three root transforms baked into pose. It checks duration, loops and root speed. The utility imports authored FBX motion. It reads the original clips for their timing and never writes them.
3. Select an imported clip and scrub the Inspector preview. The command also creates or refreshes **Football Review.overrideController** in the review folder. It overrides the six original clip slots and inherits the existing mask, states, carry blend tree, and timing parameters. Assign it to the Animator in a disposable PlayerModel prefab or review scene. Test **Slants** and **Dive** using T-124 in `Assets/Docs/TESTING.md`. Check hands and ball contact from the game camera and when frame stepping. Switch the Animator's Controller field back to the original shared controller for an A/B comparison.
4. After visual approval, store the approved `.blend` as a versioned source under this authoring directory. Keep Blender edits and approved FBXs under version control. An approved override can be adopted later at the Animator while retaining the six original clip slots, the base controller, and their GUIDs. Reimport updated FBX bytes at the same asset paths, with the same `.meta` files and take names, to preserve the candidate references. This deliverable stops at review and leaves the live Animator on the original controller.

## Round-trip validation

Blender 5.2.1 reopened the editable file and all six FBXs. All 58 nodes and parent links, positive unit scale, and time spans matched. The Blender Action poses reproduced Unity's sampled bone positions to within **0.0013 mm**, and the FBXs reopened in Blender within **0.0026 mm**. Sampled Blender frames were visually inspected, including both characters at the exchange.

Unity 6000.5.6f1 recognized all six imported FBXs as Humanoid with **52 matching Avatar mappings** and all 58 model joints. Durations and loop flags matched. The existing mask held foot, toe, and root positions identical on the sampled idle and run base poses. Max relative bone scale difference was **0.00012%** and imported average root speed was zero. These comparisons isolate the pose layer and do not cover foot sliding during a live gait or gameplay contact.

Unity's Humanoid conversion does change some arm and finger poses. The table gives the maximum difference when playing each original and reimport on the actual Blacktop Avatar under the Football mask. The comparison used the original timing and checked between bake samples as well as at sample times.

| Clip | Max joint position drift | Max wrist position drift | Max joint rotation drift |
| --- | ---: | ---: | ---: |
| Throw Windup | 1.52 cm | 1.32 cm | 9.94° |
| Throw Follow Through | 0.40 cm | 0.20 cm | 9.85° |
| Carry Ready | 1.85 cm | 1.73 cm | 4.11° |
| Carry Tuck | 1.34 cm | 0.65 cm | 14.15° |
| Handoff Give | 0.42 cm | 0.16 cm | 12.79° |
| Handoff Take | 0.42 cm | 0.08 cm | 13.91° |

These imports exceed the fidelity check of 1 cm per joint and 2° per joint rotation. The FBXs are editable and usable for Unity review. Use the original and the imported candidate side by side when approving an edit, particularly the throw release, ready grip, and forearm twist. There is no claim of lossless Unity replacement.

`reference-stability.json` records a sacrificial edit to five Throw Windup wrist keys. The reimported pose changed in Unity while all six FBX GUIDs and clip sub-asset IDs, plus the override controller and its metadata, remained identical. The proof edit exists only in a disposable copy. Mesh QA found the same duplicate-vertex and hidden-face warnings on the existing Athletic reference and the authoring copy, with zero geometry errors in both.

`Reference/Validation/` contains the Blender and Unity comparison reports. `Tools/FootballClipRoundTrip.cs` also supplies two batch diagnostics in a disposable Unity project. Set `BLACKTOP_ANIMATION_OUTPUT` to an existing absolute output directory. Run Unity with `-batchmode -quit -projectPath <review-project> -executeMethod FootballClipRoundTrip.Bake` to write the initial evaluated sample JSON. Use `FootballClipRoundTrip.ImportAndCompare` with the unedited FBXs to reproduce the fidelity numbers. It writes the comparison before failing on tolerance. For edited work use `ImportReview` and visual approval, since deliberate pose changes should differ from the baseline.

`Tools/build_baked_source.py` is for recovering the initial snapshot from sampled Unity data. The original sample packet is archived in `Reference/unity-bake.json.gz`. Pass its decompressed JSON with `--bake` to the builder while opening `SourceAssets/Characters/Bodies/Body_Athletic.blend`. Also supply new output paths with `--blend` and `--fbx`. The builder refuses an existing `.blend` path. Normal animation edits use Blender and the exporter.

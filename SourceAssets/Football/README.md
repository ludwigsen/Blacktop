# Blacktop street football source

`StreetFootball.blend` is the editable Blender source. It includes packed, original procedural leather images, a studio camera and preview lights. Unity loads the exported FBX instead of the .blend file, so opening the game does not require Blender.

Rebuild from the repository root with Blender available on PATH:

```sh
blender --background --factory-startup --python SourceAssets/Football/build_football.py
```

The script exports `Assets/_Project/Art/Football/StreetFootball.fbx`, two leather maps, the editable source and an ignored `Builds/Validation/Football-hero.png` studio preview. Palette, model dimensions and laces are authored near the top of the script. The design is original Blacktop artwork inspired by the high-contrast street football aesthetic: black, off-white and orange panels, raised laces and a Blacktop stamp.

The body is 0.32 m long and 0.19 m across, plus the raised laces, for the project's roughly two-metre character model. Unity local Z is the football's long axis. The prefab turns the visual child so the stamped side faces outward beside the carrier.

Unity imports the six material groups in this verified order:

1. Leather
2. ContrastPanel
3. AccentPanel
4. Seams
5. Laces
6. Ink

Unity swaps the two side groups relative to the Blender material list. Keep the prefab's shared material order and `FootballStyle.ColorForSlot` consistent if reworking the FBX topology or material groups.

Create a style through **Create > Blacktop > Football Style**. Add the asset to `FootballAppearance`'s Available Styles array on `FootballVisual.prefab`, and assign Selected Style for the default. `SelectStyle(index)` is the future pre-game selection hook. The two shipped examples are Street Classic and Backlot Leather. Selection uses MaterialPropertyBlocks, leaving shared materials and all gameplay data intact. A selection screen and saved preferences are intentionally not included.

The carry offset is still a player-root offset. Running/backpedaling/fast-run and hurdle animations are wired on the shared Humanoid rig with root motion disabled. Carry grip, handoff, throwing, catching and tackle reactions remain follow-up work.


## Change the lettering in Blender

The exported stamp is mesh geometry, already joined into the ball. Open StreetFootball.blend, switch to the Scripting workspace, and open build_football.py in Blender's Text Editor. Near the top, change LETTERING = 'BLACKTOP' to your text. LETTERING_WIDTH controls its width in metres; LETTERING_FONT points to a font inside this repository. Save the Python file and click Run Script. Blender rebuilds the conforming stamp, saves the .blend and exports the FBX; refresh Unity to see it. Saving the .blend alone does not update Unity because the game imports the FBX.

The script rebuilds the ball from its parameters and replaces the saved Blender scene. Save a separate copy first if you also sculpt or otherwise hand-edit the mesh and want to retain those manual changes. For direct mesh edits, export only StreetFootball as an FBX over the existing export, with -Z Forward / Y Up and unit scale applied. Keep the six material groups, object name and existing Unity .meta files.

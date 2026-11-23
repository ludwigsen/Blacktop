"""Export the six existing Blender Actions to a new review directory."""
import argparse
from datetime import datetime
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix


# Stable scene, actor and Action names keep each FBX's imported clip identity predictable.
CLIPS = (
    ('Throw', 'Throw QB', 'Throw Windup', 16),
    ('Throw', 'Throw QB', 'Throw Follow Through', 32),
    ('Carry', 'Carrier', 'Carry Ready', 125),
    ('Carry', 'Carrier', 'Carry Tuck', 65),
    ('Handoff', 'Handoff QB', 'Handoff Give', 41),
    ('Handoff', 'Handoff Runner', 'Handoff Take', 41),
)
RATE = 100


def assign(rig, action):
    """Assign the single armature slot, including when switching Actions in Blender 5."""
    animation = rig.animation_data_create()
    animation.action = action
    animation.action_slot = action.slots[0]


def verify_rest(rig):
    reference = json.loads(rig.data['blacktop_rest'])
    if set(reference) != set(rig.data.bones.keys()):
        raise ValueError(f'{rig.name}: canonical bone names changed')
    for bone in rig.data.bones:
        saved = reference[bone.name]
        parent = bone.parent.name if bone.parent else ''
        error = max(abs(a - b) for row, current in zip(saved['matrix'], bone.matrix_local)
                    for a, b in zip(row, current))
        if parent != saved['parent'] or error > 0.00001:
            raise ValueError(f'{rig.name}: canonical rest or parent changed at {bone.name}')


def default_output():
    source = Path(bpy.data.filepath).resolve()
    for directory in source.parents:
        if (directory / 'ProjectSettings').exists():
            return directory / 'Builds' / 'AnimationExports' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    raise ValueError('Save an editing copy inside Blacktop, or pass an explicit output directory.')


def export(output=None):
    destination = Path(output).resolve() if output else default_output()
    if any(parent.name == 'Assets' for parent in (destination, *destination.parents)):
        raise ValueError('Export to a review directory outside Unity Assets.')
    if destination.exists():
        raise FileExistsError(f'Choose a new export directory: {destination}')

    # Check every actor and Action before creating any output files.
    jobs = []
    for scene_name, actor, name, last in CLIPS:
        scene = bpy.data.scenes[scene_name]
        rig = scene.objects[actor]
        verify_rest(rig)
        action = bpy.data.actions[name]
        if (scene.render.fps != RATE or scene.render.fps_base != 1 or len(action.slots) != 1
                or not action.use_frame_range or tuple(action.frame_range) != (1, last)):
            raise ValueError(f'{name}: retain the 100 fps clip and its 1–{last} Action range')
        jobs.append((scene, rig, action, last, destination / f'{name}.fbx'))
    destination.mkdir(parents=True)

    window = bpy.context.window
    original_scene = window.scene
    try:
        for scene, rig, action, last, path in jobs:
            window.scene = scene
            layer = bpy.context.view_layer
            animation = rig.animation_data
            prior = (rig.name, rig.matrix_world.copy(), animation.action,
                     scene.frame_start, scene.frame_end, scene.frame_current, layer.objects.active)
            selected = [obj for obj in layer.objects if obj.select_get()]
            tracks = [(track, track.mute) for track in animation.nla_tracks]
            export_track = None
            prior_nla = animation.use_nla
            prior_slot = animation.action_slot
            try:
                if bpy.context.object and bpy.context.object.mode != 'OBJECT':
                    bpy.ops.object.mode_set(mode='OBJECT')
                for obj in selected:
                    obj.select_set(False)
                rig.name = 'Blacktop_Rig'
                rig.matrix_world = Matrix.Identity(4)  # Remove the two-actor preview placement from the FBX.
                rig.select_set(True)
                # A skinned reference supplies the FBX BindPose. A bone-only FBX
                # treats the first animated pose as rest in these importers.
                body = next(obj for obj in rig.children if ' · Body_' in obj.name)
                body.select_set(True)
                layer.objects.active = rig
                # The model node defaults must be evaluated in the canonical rest
                # pose. The temporary NLA strip plays only during the bake range.
                animation.action = None
                for track, _ in tracks:
                    track.mute = True
                animation.use_nla = True
                export_track = animation.nla_tracks.new()
                strip = export_track.strips.new(action.name, 1, action)
                strip.action_slot = action.slots[0]
                strip.extrapolation = 'NOTHING'
                scene.frame_start, scene.frame_end = 1, last
                for bone in rig.pose.bones:
                    bone.matrix_basis = Matrix.Identity(4)
                scene.frame_set(0)
                result = bpy.ops.export_scene.fbx(
                    filepath=str(path), use_selection=True, object_types={'ARMATURE', 'MESH'},
                    global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                    add_leaf_bones=False, use_armature_deform_only=False,
                    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                    bake_anim_step=1, bake_anim_simplify_factor=0,
                )
                if result != {'FINISHED'} or not path.is_file() or not path.stat().st_size:
                    raise RuntimeError(f'FBX export did not finish: {path}')
            finally:
                rig.name, rig.matrix_world = prior[:2]
                if export_track is not None:
                    animation.nla_tracks.remove(export_track)
                for track, muted in tracks:
                    track.mute = muted
                animation.use_nla = prior_nla
                animation.action = prior[2]
                if prior_slot is not None:
                    animation.action_slot = prior_slot
                scene.frame_start, scene.frame_end = prior[3:5]
                scene.frame_set(prior[5])
                for obj in list(bpy.context.selected_objects):
                    obj.select_set(False)
                for obj in selected:
                    obj.select_set(True)
                layer.objects.active = prior[6]
    finally:
        window.scene = original_scene
    print('BLACKTOP_FBX_EXPORT', destination)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output')
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    export(parser.parse_args(args).output)

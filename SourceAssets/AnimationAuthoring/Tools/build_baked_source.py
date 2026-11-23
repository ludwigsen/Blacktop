"""Seed a new animation source from Unity's evaluated poses. Use export_actions.py for edits."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

# The character mesh bridge uses a proper rotation. FBX import into Unity also
# changes handedness, so this dedicated animation copy reflects Blender Y once.
REFLECTION = Matrix.Diagonal((1, -1, 1, 1))
C = REFLECTION @ Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_actions import CLIPS, RATE, assign, export


def converted(values):
    native = Matrix([[values[col * 4 + row] for col in range(4)] for row in range(4)])
    return C @ native @ C.transposed()


def set_scene(scene, length):
    scene.render.fps = RATE
    scene.render.fps_base = 1
    scene.frame_start, scene.frame_end = 1, length
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1


def clone_actor(scene, template, meshes, name, location=(0, 0, 0), yaw=0):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    rig = template.copy()
    rig.name = name
    collection.objects.link(rig)
    rig.location = location
    rig.rotation_euler.z = math.radians(yaw)
    rig.show_in_front = False
    for src in meshes:
        mesh = src.copy()
        mesh.name = f'{name} · {src.name}'
        collection.objects.link(mesh)
        mesh.parent = rig
        mesh.matrix_parent_inverse = Matrix.Identity(4)
        mesh.matrix_basis = src.matrix_world
        for modifier in mesh.modifiers:
            if modifier.type == 'ARMATURE':
                modifier.object = rig
    return rig


def convert_reference(template, meshes):
    # Reflect the rest bases and the skinned reference together in this copy.
    # All object and bone scales stay positive during authoring and export.
    bpy.context.view_layer.objects.active = template
    template.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = template.data.edit_bones
    matrices = {bone.name: bone.matrix.copy() for bone in edit_bones}
    for name, matrix in matrices.items():
        edit_bones[name].matrix = REFLECTION @ matrix @ REFLECTION
    bpy.ops.object.mode_set(mode='OBJECT')
    for mesh in meshes:
        mesh.data.transform(REFLECTION)
        mesh.data.flip_normals()
        mesh.matrix_world = REFLECTION @ mesh.matrix_world @ REFLECTION
    bpy.context.view_layer.update()


def bake_action(rig, joints, clip):
    action = bpy.data.actions.new(clip['name'])
    action.use_fake_user = True
    action.slots.new(id_type='OBJECT', name=rig.name)
    assign(rig, action)
    end = len(clip['frames'])
    action.use_frame_range = True
    action.frame_start, action.frame_end = 1, end
    action['unity_guid'] = clip['guid']
    action['unity_duration_seconds'] = clip['duration']
    action['unity_loop'] = clip['loop']
    index = {joint['name']: i for i, joint in enumerate(joints)}
    rest = [rig.data.bones[joint['name']].matrix_local.copy() for joint in joints]
    parents = [index.get(joint['parent']) for joint in joints]
    inverse_local_rest = [(rest[parent].inverted() @ rest[i]).inverted() if parent is not None else rest[i].inverted()
                          for i, parent in enumerate(parents)]
    previous = [None] * len(joints)
    for frame, sample in enumerate(clip['frames'], 1):
        raw = sample['matrices']
        poses = [converted(raw[i * 16:(i + 1) * 16]) for i in range(len(joints))]
        inverses = [matrix.inverted() for matrix in poses]
        for i, joint in enumerate(joints):
            bone = rig.pose.bones[joint['name']]
            parent = parents[i]
            # Solve the local bone delta in the saved Blender rest axes.
            local = inverses[parent] @ poses[i] if parent is not None else poses[i]
            location, rotation, scale = (inverse_local_rest[i] @ local).decompose()
            if previous[i] is not None:
                rotation.make_compatible(previous[i])
            previous[i] = rotation.copy()
            bone.rotation_mode = 'QUATERNION'
            bone.location, bone.rotation_quaternion, bone.scale = location, rotation, scale
            for prop in ('location', 'rotation_quaternion', 'scale'):
                bone.keyframe_insert(prop, frame=frame, group=bone.name)
    curves = action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    for curve in curves:
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
    return action


def build(args):
    destination = Path(args.blend).resolve()
    if destination.exists():
        raise FileExistsError(f'Use a new source path: {destination}')
    packet = json.loads(Path(args.bake).read_text())
    if packet['rate'] != RATE:
        raise ValueError('Unexpected sampling rate')
    joints = packet['joints']
    legacy = bpy.context.scene
    template = bpy.data.objects['Blacktop_Rig']
    meshes = [obj for obj in legacy.objects if obj.type == 'MESH' and obj.name.startswith(('Body_', 'Eye_'))]
    convert_reference(template, meshes)
    rest = {}
    for item in joints:
        bone = template.data.bones[item['name']]
        parent = bone.parent.name if bone.parent else ''
        expected = converted(item['rest'])
        error = max(abs(a - b) for row, current in zip(expected, bone.matrix_local) for a, b in zip(row, current))
        if parent != item['parent'] or error > 0.00001:
            raise ValueError(f'Unity and Blender rest rigs differ at {bone.name}')
        rest[bone.name] = {'parent': parent, 'matrix': [list(row) for row in bone.matrix_local]}
    if set(rest) != set(template.data.bones.keys()):
        raise ValueError('Unity and Blender have different canonical joint sets')
    template.data['blacktop_rest'] = json.dumps(rest)
    scenes = {name: bpy.data.scenes.new(name) for name in ('Throw', 'Carry', 'Handoff')}
    rigs = {
        'Throw QB': clone_actor(scenes['Throw'], template, meshes, 'Throw QB'),
        'Carrier': clone_actor(scenes['Carry'], template, meshes, 'Carrier'),
        'Handoff QB': clone_actor(scenes['Handoff'], template, meshes, 'Handoff QB', (-0.46, 0, 0)),
        'Handoff Runner': clone_actor(scenes['Handoff'], template, meshes, 'Handoff Runner', (0.46, -0.18, 0), 55),
    }
    motions = {clip['name']: clip for clip in packet['clips']}
    for scene_name, actor, name, last in CLIPS:
        if len(motions[name]['frames']) != last:
            raise ValueError(f'Unexpected sampled range for {name}')
        bpy.context.window.scene = scenes[scene_name]
        bake_action(rigs[actor], joints, motions[name])
    starts = {'Throw': ('Throw QB', 'Throw Windup', 16), 'Carry': ('Carrier', 'Carry Ready', 125),
              'Handoff': ('Handoff QB', 'Handoff Give', 41)}
    for name, scene in scenes.items():
        bpy.context.window.scene = scene
        actor, motion, end = starts[name]
        rig = rigs[actor]
        assign(rig, bpy.data.actions[motion])
        set_scene(scene, end)
        scene.frame_set(1)
        bpy.context.view_layer.objects.active = rig
        rig.select_set(True)
        scene.timeline_markers.new('Start', frame=1)
        scene.timeline_markers.new('End', frame=end)

    bpy.context.window.scene = scenes['Handoff']
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                view = area.spaces.active
                view.shading.color_type = 'MATERIAL'
                view.region_3d.view_location = (0, 0, 0.92)
                view.region_3d.view_distance = 3.8
                view.region_3d.view_perspective = 'ORTHO'
                view.region_3d.view_rotation = Vector((0.3, -1, -0.13)).to_track_quat('-Z', 'Y')
                view.overlay.show_extras = False
    bpy.data.scenes.remove(legacy)
    bpy.data.orphans_purge(do_recursive=True)
    guide = bpy.data.texts.new('START HERE')
    guide.write('See SourceAssets/AnimationAuthoring/README.md. Save a separate work copy.\n'
                'Choose Throw, Carry or Handoff using the Scene selector. For Throw and Carry use the Action Editor to pick either take.\n'
                'Use Pose Mode to edit the sampled bone curves at 100 fps. Handoff actor placement is preview-only.\n'
                'Run the export_actions.py Text block to create a separate set of review FBXs.\n')
    bpy.data.texts.load(str(Path(__file__).with_name('export_actions.py')))
    bpy.ops.wm.save_as_mainfile(filepath=str(destination), compress=True)
    export(args.fbx)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--bake', required=True)
    parser.add_argument('--blend', required=True)
    parser.add_argument('--fbx', required=True)
    build(parser.parse_args(sys.argv[sys.argv.index('--') + 1:]))

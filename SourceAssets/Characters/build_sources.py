"""One-time conversion of exact Unity meshes to editable Blender sources. Never run to export edits."""
from pathlib import Path
import argparse
import gzip
import json
import sys
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SourceAssets/Characters'
C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))

def unity_matrix(values):
    # Unity seed matrices are column-major; Blender matrices use row access.
    return Matrix([[values[c * 4 + r] for c in range(4)] for r in range(4)])

def build(rows, bones, destination, head=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene; scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
    rig_data = bpy.data.armatures.new('Canonical_Blacktop_Skeleton')
    rig = bpy.data.objects.new('Blacktop_Rig', rig_data); scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig; rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
    convert = C.to_4x4()
    for row in bones:
        matrix = convert @ unity_matrix(row['matrix']) @ convert.inverted()
        bone = rig_data.edit_bones.new(row['name']); bone.head = matrix.translation
        direction = matrix.to_3x3().col[1].normalized()
        bone.tail = bone.head + direction * 0.07
        bone.align_roll(matrix.to_3x3().col[2].normalized())
    for row in bones:
        if row.get('parent'):
            rig_data.edit_bones[row['name']].parent = rig_data.edit_bones[row['parent']]
    bpy.ops.object.mode_set(mode='OBJECT')
    rig['blacktop_rest'] = json.dumps({bone.name: [list(row) for row in bone.matrix_local] for bone in rig_data.bones})
    rig.show_in_front = True; rig_data.display_type = 'STICK'
    materials = {}
    for name, color in {'Skin': (.19, .075, .038, 1), 'Top': (.85, .22, .018, 1), 'Shorts': (.8, .8, .75, 1),
                        'Footwear': (.1, .1, .1, 1), 'Eyes': (.7, .7, .67, 1)}.items():
        material = bpy.data.materials.new(name); material.diffuse_color = color; material.use_nodes = True
        material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = color
        material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .7
        materials[name] = material
    collections = {}
    for row in rows:
        name = row['name']; build_name = 'Eyes' if name.startswith('Eye_') else name.split('_')[-1]
        if build_name not in collections:
            collection = bpy.data.collections.new(build_name); scene.collection.children.link(collection); collections[build_name] = collection
        mesh = bpy.data.meshes.new(name)
        vertices = [C @ Vector(row['positions'][i:i+3]) for i in range(0, len(row['positions']), 3)]
        triangles = [row['triangles'][i:i+3] for i in range(0, len(row['triangles']), 3)]  # C is a rotation, so winding stays unchanged.
        mesh.from_pydata(vertices, [], triangles); mesh.update()
        uv = mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:
            i = loop.vertex_index; uv.data[loop.index].uv = row['uv'][i*2:i*2+2]
        for polygon in mesh.polygons: polygon.use_smooth = True
        mesh.normals_split_custom_set_from_vertices([C @ Vector(row['normals'][i:i+3]) for i in range(0, len(row['normals']), 3)])
        obj = bpy.data.objects.new(name, mesh); collections[build_name].objects.link(obj)
        matrix = convert @ unity_matrix(row['matrix']) @ convert.inverted(); obj.matrix_world = matrix
        metadata = {key: row.get(key) for key in ('name', 'targetAsset', 'targetGuid', 'boneNames')}
        metadata['blenderMatrix'] = [list(line) for line in matrix]; obj['blacktop_surface'] = json.dumps(metadata)
        for bone in row['boneNames']: obj.vertex_groups.new(name=bone)
        for i in range(len(vertices)):
            for offset in range(4):
                weight = row['weights'][i*4+offset]
                if weight > 0: obj.vertex_groups[row['indices'][i*4+offset]].add([i], weight, 'REPLACE')
        if name.startswith('Body_'):
            face = obj.vertex_groups.new(name='EDIT_FACE_HEAD')
            selected = [i for i in range(len(vertices)) if sum(row['weights'][i*4+j] for j in range(4)
                         if any(word in row['boneNames'][row['indices'][i*4+j]] for word in ('Head', 'Neck'))) > .5]
            face.add(selected, 1, 'REPLACE')  # Non-deforming selection mask; exporter excludes this authoring group.
        modifier = obj.modifiers.new('Shared skeleton', 'ARMATURE'); modifier.object = rig
        role = 'Eyes' if name.startswith('Eye_') else name.split('_')[0]
        obj.data.materials.append(materials.get(role, materials['Skin']))
    # The combined library starts with Athletic visible; individual sources contain only their build.
    if len(collections) > 2:
        for name, collection in collections.items():
            collection.hide_viewport = collection.hide_render = name not in ('Athletic', 'Eyes')
    camera_data = bpy.data.cameras.new('Source preview'); camera = bpy.data.objects.new('Source preview', camera_data); scene.collection.objects.link(camera)
    target = Vector((0, 0, 1.55 if head else .88)); camera.location = (0, -3.6, target.z + .05)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'; camera_data.ortho_scale = .62 if head else 2.15; scene.camera = camera
    for name, location, energy, size in [('Key', (2, -3, 4), 400, 4), ('Fill', (-2, -1, 2), 180, 3)]:
        data = bpy.data.lights.new(name, 'AREA'); data.energy = energy; data.shape = 'DISK'; data.size = size
        light = bpy.data.objects.new(name, data); scene.collection.objects.link(light); light.location = location
        light.rotation_euler = (target - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.world = bpy.data.worlds.new('Neutral'); scene.world.color = (.08, .08, .08)
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 16
    scene.render.resolution_x = 800; scene.render.resolution_y = 900; scene.render.resolution_percentage = 100
    for obj in bpy.context.selected_objects: obj.select_set(False)
    body = next(o for o in bpy.data.objects if o.name.startswith('Body_') and ('Athletic' in o.name or len(collections) <= 2))
    body.select_set(True); bpy.context.view_layer.objects.active = body
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.color_type = 'MATERIAL'
    bpy.data.texts.load(str(SOURCE / 'export_meshes.py'))
    note = bpy.data.texts.new('READ ME FIRST')
    note.write('Editable Blacktop prototype source. Read SourceAssets/Characters/README.md.\n'
               'Edit mesh vertices in Edit/Sculpt Mode. Keep bone names/rest pose and object transforms unchanged.\n'
               'Body meshes include their editable face. EDIT_FACE_HEAD is a head/neck selection group.\n'
               'Body, face, clothing and eye edits export to existing Unity mesh assets. Missing hairstyles/beards are not invented.\n'
               'Run export_meshes.py to export edits; do NOT run build_sources.py, which rebuilds sources from the Unity seed.\n')
    destination.parent.mkdir(parents=True, exist_ok=True); bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(destination), compress=True)
    print('BLACKTOP_SOURCE_SAVED', destination)

def main():
    args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser(); parser.add_argument('--overwrite', action='store_true'); options = parser.parse_args(args)
    if list(SOURCE.rglob('*.blend')) and not options.overwrite:
        raise RuntimeError('Editable sources already exist. This seed conversion would overwrite manual work; use --overwrite only deliberately.')
    with gzip.open(SOURCE / 'Reference/character_seed.json.gz', 'rt') as file: packet = json.load(file)
    meshes = packet['meshes']; eyes = [row for row in meshes if row['name'].startswith('Eye_')]
    for name in ('Lean', 'Athletic', 'Thick', 'Heavy', 'Massive'):
        build([row for row in meshes if row['name'].endswith('_'+name)] + eyes, packet['bones'], SOURCE / 'Bodies' / ('Body_'+name+'.blend'))
    build([row for row in meshes if row['name']=='Body_Athletic'] + eyes, packet['bones'], SOURCE / 'Faces/PrototypeFace.blend', head=True)
    build(meshes, packet['bones'], SOURCE / 'BlacktopCharacters.blend')

if __name__ == '__main__': main()

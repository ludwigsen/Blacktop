"""Reopen sources, verify source fidelity, and exercise the edit/export path without saving changes."""
from pathlib import Path
import gzip
import importlib.util
import json
import sys
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True  # Validation leaves no generated Python cache in the source folder.
ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SourceAssets/Characters'
OUT = ROOT / 'Builds/Validation'
OUT.mkdir(parents=True, exist_ok=True)
spec = importlib.util.spec_from_file_location('blacktop_export', SOURCE / 'export_meshes.py')
bridge = importlib.util.module_from_spec(spec); spec.loader.exec_module(bridge)
with gzip.open(SOURCE / 'Reference/character_seed.json.gz', 'rt') as file:
    seed = json.load(file)
originals = {row['name']: row for row in seed['meshes']}
report = {'files': [], 'max_position_error': 0, 'mesh_objects_checked': 0}
for path in sorted(SOURCE.rglob('*.blend')):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig = bpy.data.objects['Blacktop_Rig']
    assert len(rig.data.bones) == len(seed['bones'])
    count = 0
    for obj in bpy.data.objects:
        if obj.type != 'MESH' or 'blacktop_surface' not in obj:
            continue
        row = originals[obj.name]; count += 1
        assert len(obj.data.vertices) == len(row['positions']) // 3
        assert len(obj.data.polygons) == len(row['triangles']) // 3
        for vertex in obj.data.vertices:
            i = vertex.index
            expected = bridge.C @ Vector(row['positions'][i*3:i*3+3])
            report['max_position_error'] = max(report['max_position_error'], (vertex.co - expected).length)
            actual = {obj.vertex_groups[g.group].name: g.weight for g in vertex.groups if obj.vertex_groups[g.group].name in row['boneNames']}
            wanted = {row['boneNames'][row['indices'][i*4+j]]: row['weights'][i*4+j] for j in range(4) if row['weights'][i*4+j] > 0}
            assert actual.keys() == wanted.keys()
            assert all(abs(actual[name] - value) < 0.000001 for name, value in wanted.items())
        if obj.name.startswith('Body_'):
            assert obj.vertex_groups.get('EDIT_FACE_HEAD')
    report['files'].append({'file': str(path.relative_to(ROOT)), 'mesh_objects': count, 'bones': len(rig.data.bones)})
    report['mesh_objects_checked'] += count
assert report['max_position_error'] < 0.000001
# Export every mesh from the combined library, including hidden build collections.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'BlacktopCharacters.blend'))
packet = bridge.export(OUT / 'character-roundtrip.json')
assert len(packet['meshes']) == 24
report['roundtrip_meshes'] = len(packet['meshes'])
# Apply an actual sculpt-like vertex move to the isolated Athletic source in memory only.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'Bodies/Body_Athletic.blend'))
body = bpy.data.objects['Body_Athletic']; body.data.vertices[0].co.x += .0123
body.data.update(); bridge.export(OUT / 'character-edited.json')
report['edited_vertex_delta_x'] = .0123
# Unsupported rig/object edits must be rejected instead of silently changing skinning contracts.
body.scale.x = 1.1
try:
    bridge.export(OUT / 'character-invalid.json')
    raise AssertionError('Changed mesh transform was accepted')
except RuntimeError:
    report['changed_object_transform_rejected'] = True
body.scale.x = 1
rig = bpy.data.objects['Blacktop_Rig']; rig.pose.bones[0].rotation_mode = 'XYZ'; rig.pose.bones[0].rotation_euler.x = .1
try:
    bridge.export(OUT / 'character-invalid.json')
    raise AssertionError('Posed skeleton was accepted')
except RuntimeError:
    report['posed_rig_rejected'] = True
# Render real saved sources, not a separate approximation.
for relative, image in [('Bodies/Body_Athletic.blend', 'Character-blender-body.png'), ('Faces/PrototypeFace.blend', 'Character-blender-face.png')]:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / relative))
    bpy.context.scene.render.filepath = str(OUT / image)
    bpy.ops.render.render(write_still=True)
(OUT / 'character-blender-validation.json').write_text(json.dumps(report, indent=2))
print('BLACKTOP_SOURCE_VALIDATION', json.dumps(report))

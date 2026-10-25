"""Create the new canonical once from the audited Athletic .blend; never overwrite legacy sources."""
from pathlib import Path
import gzip
import hashlib
import json
import sys
import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.dont_write_bytecode = True
from engine import BodyEngine, digest


def cut(row, height, lower):
    # Clip triangles at a shared neck plane. Original retained vertices preserve order; only edge cuts interpolate.
    positions = np.array(row['positions']).reshape(-1, 3)
    normals = np.array(row['normals']).reshape(-1, 3)
    uv = np.array(row['uv']).reshape(-1, 2)
    weights = np.array(row['weights']).reshape(-1, 4)
    indices = np.array(row['indices']).reshape(-1, 4)
    keep = positions[:, 1] <= height if lower else positions[:, 1] >= height
    original = [int(i) for i in np.where(keep)[0]]; mapping = {int(v): i for i, v in enumerate(original)}
    points = [list(positions[i]) for i in original]; tex = [list(uv[i]) for i in original]
    norm = [list(normals[i]) for i in original]
    skin = [[(int(b), float(w)) for b, w in zip(indices[i], weights[i]) if w > 0] for i in original]
    edges = {}; triangles = []
    def edge(a, b):
        key = tuple(sorted((a, b)))
        if key in edges:
            return edges[key]
        t = (height-positions[a, 1])/(positions[b, 1]-positions[a, 1])
        point = positions[a]+t*(positions[b]-positions[a]); point[1] = height
        normal = normals[a]+t*(normals[b]-normals[a]); normal /= np.linalg.norm(normal)
        combined = {}
        for source, ratio in ((a, 1-t), (b, t)):
            for bone, weight in zip(indices[source], weights[source]):
                combined[int(bone)] = combined.get(int(bone), 0)+float(weight*ratio)
        influence = sorted(((b, w) for b, w in combined.items() if w > 1e-8), key=lambda p: -p[1])[:4]
        total = sum(w for _, w in influence); influence = [(b, w/total) for b, w in influence]
        index = len(points); edges[key] = index
        points.append(list(point)); tex.append(list(uv[a]+t*(uv[b]-uv[a]))); norm.append(list(normal)); skin.append(influence)
        return index
    for triangle in np.array(row['triangles']).reshape(-1, 3):
        polygon = []
        for a, b in zip(triangle, np.roll(triangle, -1)):
            if keep[a]: polygon.append(mapping[int(a)])
            if keep[a] != keep[b]: polygon.append(edge(int(a), int(b)))
        for i in range(1, len(polygon)-1): triangles.extend((polygon[0], polygon[i], polygon[i+1]))
    padded = [influences+[(0, 0)]*(4-len(influences)) for influences in skin]
    return dict(positions=np.array(points).flatten().tolist(), normals=np.array(norm).flatten().tolist(),
                uv=np.array(tex).flatten().tolist(), triangles=triangles,
                indices=[b for influences in padded for b, _ in influences], weights=[w for influences in padded for _, w in influences],
                boneNames=row['boneNames'], source_vertex_ids=original+[-1]*len(edges))


def add_mesh(name, row, rig, collection):
    # Every generated mesh uses the same renderer space, bone map and untouched armature.
    p = np.array(row['positions']).reshape(-1, 3)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(x, -z, y) for x, y, z in p], [], np.array(row['triangles']).reshape(-1, 3).tolist())
    mesh.update(); layer = mesh.uv_layers.new(name='UVMap')
    uv = np.array(row['uv']).reshape(-1, 2)
    for loop in mesh.loops: layer.data[loop.index].uv = uv[loop.vertex_index]
    for face in mesh.polygons: face.use_smooth = True
    obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj)
    for name in row['boneNames']: obj.vertex_groups.new(name=name)
    for i in range(len(p)):
        for bone, weight in zip(row['indices'][i*4:i*4+4], row['weights'][i*4:i*4+4]):
            if weight > 0: obj.vertex_groups[bone].add([i], weight, 'REPLACE')
    mod = obj.modifiers.new('Canonical skin', 'ARMATURE'); mod.object = rig
    obj['blacktop_surface'] = json.dumps({'name': name, 'boneNames': row['boneNames'], 'blenderMatrix': [[int(i == j) for j in range(4)] for i in range(4)]})
    return obj


def main():
    if (HERE/'CanonicalBody.blend').exists() or (HERE/'canonical.json.gz').exists():
        raise RuntimeError('Canonical source already exists. A topology migration requires a new version directory.')
    source = HERE.parent/'Bodies/Body_Athletic.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    obj = bpy.data.objects['Body_Athletic']; mesh = obj.data
    metadata = json.loads(obj['blacktop_surface']); names = metadata['boneNames']
    uv = [None]*len(mesh.vertices)
    for loop in mesh.loops: uv[loop.vertex_index] = list(mesh.uv_layers.active.data[loop.index].uv)
    row = dict(positions=[v for p in mesh.vertices for v in (p.co.x, p.co.z, -p.co.y)],
               normals=[v for p in mesh.vertices for v in (p.normal.x, p.normal.z, -p.normal.y)], uv=[v for p in uv for v in p],
               triangles=[v for p in mesh.polygons for v in p.vertices], boneNames=names)
    weights = []; indices = []
    for vertex in mesh.vertices:
        values = [(names.index(obj.vertex_groups[g.group].name), g.weight) for g in vertex.groups if obj.vertex_groups[g.group].name in names and g.weight > 0]
        values += [(0, 0)]*(4-len(values)); indices.extend(b for b, _ in values); weights.extend(w for _, w in values)
    row.update(weights=weights, indices=indices)
    with gzip.open(HERE.parent/'Reference/character_seed.json.gz', 'rt') as stream: seed = json.load(stream)
    reference = next(r for r in seed['meshes'] if r['name'] == 'Body_Athletic')
    if not np.allclose(row['positions'], reference['positions'], atol=1e-7, rtol=0):
        raise RuntimeError('Audited Athletic snapshot changed; review its provenance before selecting a new canonical.')
    body = cut(row, 1.515, True); head = cut(row, 1.515, False)
    points = np.array(body['positions']).reshape(-1, 3)
    ring = np.where(np.abs(points[:, 1]-1.515) < 1e-8)[0]
    unique = sorted(set(tuple(np.round(points[i], 8)) for i in ring), key=lambda p: np.arctan2(p[2]+.0033, p[0]))
    canonical = {'version': 'blacktop.body.v1', 'reference': 'athletic-183-v1',
                 'source': str(source.relative_to(HERE.parents[2])), 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                 'source_stature_m': float(np.ptp(np.array(row['positions']).reshape(-1, 3)[:, 1])),
                 'bones': seed['bones'], 'body': body, 'head': head,
                 'neck': {'id': 'blacktop.neck.v1', 'plane_y_m': 1.515, 'up': [0, 1, 0], 'forward': [0, 0, 1],
                          'center_m': np.mean(unique, axis=0).tolist(), 'ring_vertex_indices': ring.tolist(), 'ordered_unique_positions_m': unique}}
    encoded = json.dumps(canonical, separators=(',', ':'))  # Serialize before creating the protected source.
    with gzip.open(HERE/'canonical.json.gz', 'wt') as stream: stream.write(encoded)
    (HERE/'neck-interface.json').write_text(json.dumps(canonical['neck'], indent=2)+'\n')
    # Preserve the original source rig, cameras, lights and eye skins; remove only objects in this NEW file.
    rig = bpy.data.objects['Blacktop_Rig']
    for old in list(bpy.data.objects):
        if old.type == 'MESH' and not old.name.startswith('Eye_'): bpy.data.objects.remove(old, do_unlink=True)
    collection = bpy.data.collections.new('Body Editor'); bpy.context.scene.collection.children.link(collection)
    body_obj = add_mesh('BT_Canonical_Immutable', body, rig, collection)
    head_obj = add_mesh('BT_Prototype_Head', head, rig, collection)
    material = bpy.data.materials.get('Skin'); body_obj.data.materials.append(material); head_obj.data.materials.append(material)
    body_obj.hide_render = True; body_obj.hide_set(True); body_obj.hide_select = True
    head_obj['blacktop_prototype'] = True
    engine = BodyEngine(canonical)
    preview = add_mesh('BT_Body_Preview', body, rig, collection); preview.data.materials.append(material)
    positions, report = engine.solve(json.loads((HERE/'Presets/Athletic.json').read_text()))
    preview.data.vertices.foreach_set('co', positions[:, [0, 2, 1]].__mul__([1, -1, 1]).flatten())
    preview.data.update()
    scene = bpy.context.scene; scene['blacktop_canonical_hash'] = digest(canonical)
    scene['blacktop_generator_root'] = '//'
    for old in list(bpy.data.texts): bpy.data.texts.remove(old)
    note = bpy.data.texts.new('START HERE')
    note.write('Blacktop Body Editor: run body_editor.py from the Scripting workspace once per session, then open the 3D View sidebar > Blacktop.\nCanonical reference is immutable. Presets live in Presets/*.json. Old .blend files are independent legacy fallbacks.\n')
    editor_text = bpy.data.texts.load(str(HERE/'body_editor.py'))
    editor_text.cursor_set(0)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'TEXT_EDITOR': area.spaces.active.text = editor_text
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.show_region_ui = True
                area.spaces.active.overlay.show_overlays = False
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
    bpy.context.view_layer.objects.active = preview; preview.select_set(True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CanonicalBody.blend'), compress=True)
    print('CANONICAL_CREATED', len(points), len(body['triangles'])//3, len(unique), engine.baseline.tolist())

if __name__ == '__main__': main()

"""Preview and baked-copy export; the canonical reference is never edited or exported implicitly."""
from pathlib import Path
import importlib.util
import json
from contextlib import contextmanager
import sys
import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.dont_write_bytecode = True
from engine import BodyEngine, read_json, VERSION
from prepare_source import add_mesh

_ENGINE = None


def engine():
    global _ENGINE
    if _ENGINE is None: _ENGINE = BodyEngine()
    return _ENGINE


def require_canonical():
    e = engine(); obj = bpy.data.objects.get('BT_Canonical_Immutable')
    if obj is None or bpy.context.scene.get('blacktop_canonical_hash') != e.reference_hash:
        raise ValueError('Open BodyGenerator/CanonicalBody.blend before using this tool.')
    positions = np.array([(v.co.x, v.co.z, -v.co.y) for v in obj.data.vertices])
    if positions.shape != e.base.shape or not np.allclose(positions, e.base, atol=1e-7, rtol=0):
        raise ValueError('Canonical reference was edited. Reopen the clean source; do not export.')
    if len(obj.data.polygons) != len(e.triangles):
        raise ValueError('Canonical topology changed; reopen the clean source.')
    return e


def normals(positions, triangles):
    # Recalculate smooth surface normals on a position-welded topology, preserving the UV seam vertices.
    p = np.array(positions).reshape(-1, 3); tri = np.array(triangles).reshape(-1, 3)
    face = np.cross(p[tri[:, 1]]-p[tri[:, 0]], p[tri[:, 2]]-p[tri[:, 0]])
    _, inverse = np.unique(np.round(p, 7), axis=0, return_inverse=True)
    summed = np.zeros((inverse.max()+1, 3))
    for i in range(3): np.add.at(summed, inverse[tri[:, i]], face)
    result = summed[inverse]; length = np.linalg.norm(result, axis=1)
    return result/np.maximum(length[:, None], 1e-12)


def body_row(positions):
    e = engine(); row = dict(e.data['body']); row['positions'] = positions.flatten().tolist()
    normal = normals(positions, row['triangles'])
    # Fixed collar normals meet the detached prototype head using the original continuous-surface normals.
    collar = positions[:, 1] >= 1.495
    normal[collar] = np.array(row['normals']).reshape(-1, 3)[collar]
    row['normals'] = normal.flatten().tolist()
    return row


def garment(row, slot):
    p = np.array(row['positions']).reshape(-1, 3); tri = np.array(row['triangles']).reshape(-1, 3)
    center = engine().base[tri].mean(axis=1)
    mask = ((center[:, 1] > .94) & (center[:, 1] < 1.49) & (np.abs(center[:, 0]) < .36)) if slot == 'Top' else \
           ((center[:, 1] > .47) & (center[:, 1] < .98)) if slot == 'Shorts' else center[:, 1] < .15
    selected = tri[mask]; used, inverse = np.unique(selected, return_inverse=True)
    normal = np.array(row['normals']).reshape(-1, 3)
    # Matching surface shells inherit the exact deformation/weights; a small normal offset avoids neutral-pose z fighting.
    positions = p[used]+normal[used]*.004
    result = {'positions': positions.flatten().tolist(), 'triangles': inverse.tolist(), 'boneNames': row['boneNames']}
    for key, size in (('uv', 2), ('indices', 4), ('weights', 4), ('normals', 3)):
        result[key] = np.array(row[key]).reshape(-1, size)[used].flatten().tolist()
    return result


@contextmanager
def editable_surface(obj):
    # Flush sculpt buffers while retaining the artist's mode after preview/save operations.
    mode = obj.mode
    if mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    try:
        yield
    finally:
        if mode != 'OBJECT' and bpy.context.view_layer.objects.active == obj: bpy.ops.object.mode_set(mode=mode)


def capture_sculpt(scene=None):
    scene = scene or bpy.context.scene
    obj = bpy.data.objects.get('BT_Body_Preview')
    if obj is None: return False
    with editable_surface(obj):
        e = require_canonical()
        actual = np.array([(v.co.x, v.co.z, -v.co.y) for v in obj.data.vertices])
        if actual.shape != e.base.shape or [tuple(p.vertices) for p in obj.data.polygons] != [tuple(t) for t in e.triangles]:
            raise ValueError('Sculpt topology changed. Undo remesh/Dyntopo; keep the shared vertex/UV/weight topology.')
        values = json.loads(scene.get('blacktop_presets', '{}'))
        key = obj.get('blacktop_preview_id') or json.loads(scene.get('blacktop_report', '{}')).get('preset')
        if key not in values: return False
        if 'blacktop_preview_positions' in obj:
            previous = np.array(obj['blacktop_preview_positions']).reshape(-1, 3)
        else:
            previous, _ = e.solve(values[key])  # Adopt an already-open v1 workspace without discarding its current sculpt.
        difference = actual-previous
        if np.max(np.abs(difference)) <= 2e-6: return False
        if np.max(np.abs(difference[e.base[:, 1] >= 1.495])) > 1e-7:
            raise ValueError('Sculpt changed the fixed neck collar. Undo that stroke before switching/exporting.')
        existing = np.array(values[key].get('sculpt_delta', np.zeros_like(actual).flatten().tolist())).reshape(-1, 3)
        delta = existing+difference; delta[np.abs(delta) < 1e-7] = 0
        values[key]['sculpt_delta'] = delta.flatten().tolist(); scene['blacktop_presets'] = json.dumps(values)
        obj['blacktop_preview_id'] = key; obj['blacktop_preview_positions'] = actual.flatten().tolist()
        return True


def discard_preview_capture():
    obj = bpy.data.objects.get('BT_Body_Preview')
    if obj:
        # Reset acknowledges the current surface so automatic capture cannot re-add the discarded stroke.
        obj['blacktop_preview_positions'] = [value for vertex in obj.data.vertices for value in (vertex.co.x, vertex.co.z, -vertex.co.y)]
        if 'blacktop_preview_id' not in obj:
            obj['blacktop_preview_id'] = json.loads(bpy.context.scene.get('blacktop_report', '{}')).get('preset', '')


def update_preview(preset, show_clothes=False):
    e = require_canonical(); positions, report = e.solve(preset)
    obj = bpy.data.objects['BT_Body_Preview']
    with editable_surface(obj):
        obj.data.vertices.foreach_set('co', (positions[:, [0, 2, 1]]*[1, -1, 1]).flatten()); obj.data.update()
        row = body_row(positions)
        obj.data.normals_split_custom_set_from_vertices([(x, -z, y) for x, y, z in np.array(row['normals']).reshape(-1, 3)])
        collection = obj.users_collection[0]; rig = bpy.data.objects['Blacktop_Rig']
        for slot in ('Top', 'Shorts', 'Footwear'):
            old = bpy.data.objects.get('BT_Preview_'+slot)
            if old:
                mesh = old.data; bpy.data.objects.remove(old, do_unlink=True); bpy.data.meshes.remove(mesh)
            if show_clothes:
                new = add_mesh('BT_Preview_'+slot, garment(row, slot), rig, collection)
                new.data.materials.append(bpy.data.materials.get(slot))
        obj['blacktop_preview_id'] = preset['id']; obj['blacktop_preview_positions'] = positions.flatten().tolist()
        bpy.context.scene['blacktop_report'] = json.dumps(report)
        return report


def export_presets(presets, output=None):
    e = require_canonical()
    targets = {row['name']: row for row in read_json(HERE/'unity-targets.json')['meshes']}
    spec = importlib.util.spec_from_file_location('blacktop_mesh_export', HERE.parent/'export_meshes.py')
    bridge = importlib.util.module_from_spec(spec); spec.loader.exec_module(bridge)
    rows = []; reports = []
    for preset in presets:
        positions, report = e.solve(preset)
        if not report['valid']: raise ValueError(f'{preset["name"]}: measurements exceed 1% tolerance; export blocked.')
        # Only the six declared IDs have Unity targets. Custom duplicates export to an explicitly assigned stock ID.
        name = next((n for n in ('Lean', 'Athletic', 'Built', 'Thick', 'Heavy', 'Massive') if preset['id'] == 'body.'+n.lower()+'.parametric.v1'), None)
        if name is None: raise ValueError('Custom preset has no Unity target. Save its JSON, then load its measurements into a named target preset before exporting.')
        body = body_row(positions); rows.append(('Body_'+name, body))
        rows.extend((slot+'_'+name, garment(body, slot)) for slot in ('Top', 'Shorts', 'Footwear'))
        reports.append(report)
    rows.append(('Head_Prototype', e.data['head']))
    collection = bpy.data.collections.new('Temporary baked export'); bpy.context.scene.collection.children.link(collection)
    objects = []
    try:
        for name, row in rows:
            obj = add_mesh(name, row, bpy.data.objects['Blacktop_Rig'], collection); objects.append(obj)
            meta = json.loads(obj['blacktop_surface']); meta.update(targets[name]); obj['blacktop_surface'] = json.dumps(meta)
            obj.data.normals_split_custom_set_from_vertices([(x, -z, y) for x, y, z in np.array(row['normals']).reshape(-1, 3)])
        destination = Path(output) if output else HERE.parents[2]/'Builds/CharacterExports/GeneratedBodies.json'
        packet = bridge.export(destination, objects=objects, packet_metadata={'version': 2, 'generator': VERSION,
                               'canonicalHash': e.reference_hash, 'bones': e.data['bones']})
        destination.with_suffix('.measurements.json').write_text(json.dumps({'reports': reports,
            'clothing_status': 'Prototype normal-offset shells; neutral fit follows body exactly. Seams and animation penetration require visual acceptance.'}, indent=2))
        return packet
    finally:
        for obj in objects:
            mesh = obj.data; bpy.data.objects.remove(obj, do_unlink=True); bpy.data.meshes.remove(mesh)
        bpy.data.collections.remove(collection)

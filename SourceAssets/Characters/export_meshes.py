"""Export edited rest-pose meshes; never rebuild or overwrite the artist's .blend."""
from pathlib import Path
import argparse
import json
import sys
import bpy
from mathutils import Matrix, Vector

# Unity mesh (X right, Y up) <-> Blender workspace (Z up), used in both directions.
C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))

def project_root():
    for folder in Path(bpy.data.filepath).resolve().parents:
        if (folder / 'Assets/_Project').is_dir():
            return folder
    raise RuntimeError('Save this source inside the Blacktop repository before exporting.')

def close(a, b):
    return all(abs(a[r][c] - b[r][c]) < 0.00001 for r in range(4) for c in range(4))

def export(output=None, objects=None, packet_metadata=None):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')  # Flush Edit/Sculpt mode geometry without saving over the source.
    bpy.context.view_layer.update()  # Refresh matrices after script-driven or just-completed edits.
    rig = bpy.data.objects.get('Blacktop_Rig')
    if rig is None or not close(rig.matrix_world, Matrix.Identity(4)):
        raise RuntimeError('Keep Blacktop_Rig and mesh object transforms unchanged; edit vertices in Edit/Sculpt Mode.')
    rest = json.loads(rig['blacktop_rest'])
    if set(rest) != set(rig.data.bones.keys()):
        raise RuntimeError('Do not add/remove/rename canonical bones.')
    for bone in rig.data.bones:
        if not close(bone.matrix_local, Matrix(rest[bone.name])) or not close(rig.pose.bones[bone.name].matrix_basis, Matrix.Identity(4)):
            raise RuntimeError('Export in the unchanged rest pose. Clear pose transforms; rig rest changes are unsupported.')
    packet = {'version': 1, 'meshes': []}
    if packet_metadata:
        packet.update(packet_metadata)  # Generated exports declare their canonical rig/version explicitly.
    for obj in sorted(bpy.data.objects if objects is None else objects, key=lambda o: o.name):
        if obj.type != 'MESH' or 'blacktop_surface' not in obj:
            continue
        row = json.loads(obj['blacktop_surface'])
        if not row.get('targetAsset'):
            continue  # Non-gameplay authoring references are never exported implicitly.
        if not close(obj.matrix_world, Matrix(row.pop('blenderMatrix'))):
            raise RuntimeError(f'{obj.name}: apply no object transforms; edit mesh geometry instead.')
        if any(mod.type != 'ARMATURE' for mod in obj.modifiers):
            raise RuntimeError(f'{obj.name}: apply mesh modifiers explicitly before exporting.')
        mesh = obj.data
        if mesh.shape_keys:
            raise RuntimeError(f'{obj.name}: apply the intended shape to the base mesh before exporting.')
        if not mesh.uv_layers.active:
            raise RuntimeError(f'{obj.name}: a UV map is required.')
        names = row['boneNames']
        bone_index = {name: i for i, name in enumerate(names)}
        memberships = []
        for vertex in mesh.vertices:
            influences = [(bone_index[obj.vertex_groups[g.group].name], g.weight) for g in vertex.groups
                          if obj.vertex_groups[g.group].name in bone_index and g.weight > 0.000001]
            if not influences or len(influences) > 4:
                raise RuntimeError(f'{obj.name}: vertex {vertex.index} needs 1–4 bone influences. Normalize/limit weights first.')
            total = sum(weight for _, weight in influences)
            memberships.append([(i, w / total) for i, w in influences] + [(0, 0)] * (4 - len(influences)))
        # Keep original vertex order, splitting only where UV seams or corner normals require it.
        mesh.calc_loop_triangles()
        slots = [[] for _ in mesh.vertices]
        vertices, normals, uv, influences = [], [], [], []
        for vertex in mesh.vertices:
            vertices.append(list(C.transposed() @ vertex.co))
            normals.append(list(C.transposed() @ vertex.normal))
            uv.append([0.0, 0.0])
            influences.append(memberships[vertex.index])
        triangles = []
        for triangle in mesh.loop_triangles:
            mapped = []
            for loop_index in triangle.loops:
                vertex_index = mesh.loops[loop_index].vertex_index
                tex = tuple(mesh.uv_layers.active.data[loop_index].uv)
                normal = mesh.corner_normals[loop_index].vector
                key = tuple(round(x, 7) for x in (*tex, *normal))
                found = next((index for candidate, index in slots[vertex_index] if candidate == key), None)
                if found is None:
                    found = vertex_index if not slots[vertex_index] else len(vertices)
                    if found == len(vertices):
                        vertices.append(vertices[vertex_index][:]); normals.append([0, 0, 0]); uv.append([0, 0]); influences.append(memberships[vertex_index])
                    uv[found] = list(tex); normals[found] = list(C.transposed() @ normal)
                    slots[vertex_index].append((key, found))
                mapped.append(found)
            triangles.extend(mapped)  # The workspace conversion is a rotation; preserve winding.
        row.update(positions=[v for p in vertices for v in p], normals=[v for n in normals for v in n],
                   uv=[v for tex in uv for v in tex], triangles=triangles,
                   indices=[i for weights in influences for i, _ in weights], weights=[w for weights in influences for _, w in weights])
        packet['meshes'].append(row)
    if not packet['meshes']:
        raise RuntimeError('No Unity-targeted character meshes are present.')
    destination = Path(output) if output else project_root() / 'Builds/CharacterExports' / (Path(bpy.data.filepath).stem + '.json')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(packet, separators=(',', ':')))
    print(f'BLACKTOP_EXPORTED {len(packet["meshes"])} meshes -> {destination}')
    return packet

if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser(); parser.add_argument('--output')
    export(parser.parse_args(args).output)

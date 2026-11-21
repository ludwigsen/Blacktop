"""Exercise the saved MPFB rig and write per-pose metrics and three-view renders."""
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

# Directions use Blender armature space. Left is +X, forward is -Y, up is +Z.
LIFT = {"upperarm_l": (1, 0, 0)}
FRONT = {"upperarm_l": (0.15, -1, 0)}
CATCH = {"upperarm_l": (.48, -.84, .24), "lowerarm_l": (-.30, -.70, .64),
         "upperarm_r": (-.48, -.84, .24), "lowerarm_r": (.30, -.70, .64)}
POSES = {
    "Rest": {},
    "Shoulder abduction": LIFT,
    "Shoulder forward": FRONT,
    "Shoulder backward": {"upperarm_l": (.33, .65, -.68)},
    "Elbow flexion": {"lowerarm_l": (.10, -.66, .75)},
    "Wrist rotation": {},
    "Torso twist": {},
    "Hip flexion": {"thigh_l": (.07, -.93, -.37)},
    "Knee flexion": {"calf_l": (.03, .98, .20)},
    "Neck rotation": {},
    "Running stride": {"thigh_l": (.1, -.8, -.6), "calf_l": (0, .48, -.88),
                       "thigh_r": (-.1, .55, -.83), "calf_r": (0, .96, .26),
                       "upperarm_l": (.3, .55, -.78), "lowerarm_l": (.08, -.72, -.68),
                       "upperarm_r": (-.3, -.76, -.58), "lowerarm_r": (0, -.72, .69)},
    "Throwing position": {"upperarm_r": (-.94, .23, .14), "lowerarm_r": (.05, .34, .94),
                          "upperarm_l": (.40, -.90, .12), "lowerarm_l": (.02, -.84, .54)},
    "Catching position": CATCH,
}


def aim(bone, rotation):
    # Convert a world-space rotation into the posed parent's local frame.
    matrix = bone.matrix.copy()
    bone.matrix = Matrix.LocRotScale(matrix.translation, rotation @ matrix.to_quaternion(), matrix.to_scale())
    bpy.context.view_layer.update()


def set_pose(rig, name, directions):
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for key, direction in directions.items():
        bone = rig.pose.bones[key]
        aim(bone, (bone.tail - bone.head).normalized().rotation_difference(Vector(direction).normalized()))
    if name == "Wrist rotation":
        bone = rig.pose.bones["hand_l"]
        aim(bone, Quaternion((bone.tail - bone.head).normalized(), math.radians(50)))
    if name in ("Torso twist", "Neck rotation"):
        bone = rig.pose.bones["spine_02" if name == "Torso twist" else "neck_01"]
        aim(bone, Quaternion((0, 0, 1), math.radians(28 if name == "Torso twist" else 45)))


def sample(body):
    mesh = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    co = np.empty(3 * len(mesh.vertices), dtype=np.float32)
    mesh.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def intersection_pairs(co, faces):
    bvh = BVHTree.FromPolygons(co.tolist(), faces, all_triangles=True)
    # Ignore adjacent triangles, which touch by construction.
    return {(a, b) for a, b in bvh.overlap(bvh) if a < b and not set(faces[a]).intersection(faces[b])}


def run(args):
    body, = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    rig = body.find_armature()
    mesh = body.data
    mesh.calc_loop_triangles()
    faces = [tuple(t.vertices) for t in mesh.loop_triangles]
    edges = np.array([e.vertices[:] for e in mesh.edges])
    weights = np.zeros((len(mesh.vertices), len(body.vertex_groups)), dtype=np.float32)
    for vertex in mesh.vertices:
        for g in vertex.groups:
            weights[vertex.index, g.group] = g.weight
    if not np.allclose(weights.sum(axis=1), 1, atol=1e-5) or (weights > 0).sum(axis=1).max() > 4:
        raise ValueError("Invalid skin weight coverage")
    group_names = [g.name for g in body.vertex_groups]
    if any(rig.data.bones.get(n) is None or not rig.data.bones[n].use_deform for n in group_names):
        raise ValueError("A skin group has no deform bone")
    dominant = weights.argmax(axis=1)
    out = args.out.resolve()
    out.mkdir(exist_ok=True, parents=True)
    sys.path.insert(0, str(args.qa.resolve()))
    import qa_check
    import review_shots
    report = {"asset": body.name, "hierarchy": {b.name: b.parent.name if b.parent else None for b in rig.data.bones},
              "dimensions_m": list(body.dimensions), "rest_mesh_qa": qa_check.run(objects=[body.name]),
              "influence_coverage": int((weights.sum(axis=0) > 0).sum()),
              "edge_weight_L1_p99": float(np.percentile(np.abs(weights[edges[:, 0]] - weights[edges[:, 1]]).sum(axis=1), 99)),
              "poses": {}}
    for name, directions in POSES.items():
        set_pose(rig, name, directions)
        co = sample(body)
        lengths = np.linalg.norm(co[edges[:, 0]] - co[edges[:, 1]], axis=1)
        hits = intersection_pairs(co, faces)
        if name == "Rest":
            baseline = hits
            rest_length = lengths
            rest = co
        ratio = lengths / rest_length
        if not np.isfinite(ratio).all() or (name != "Rest" and np.max(np.abs(rest - co)) < .001):
            raise ValueError(f"Mesh failed to deform correctly in {name}")
        # Keep the affected anatomy so an overlap can be located in the corresponding renders.
        contacts = Counter()
        for a, b in hits - baseline:
            names = [group_names[int(np.bincount(dominant[list(faces[i])]).argmax())] for i in (a, b)]
            contacts[" / ".join(sorted(names))] += 1
        report["poses"][name] = {"edge_ratio_min_max": [float(ratio.min()), float(ratio.max())],
                                 "edge_ratio_p01_p99": np.percentile(ratio,[1,99]).tolist(),
                                 "intersecting_triangle_pairs": len(hits), "new_contacts": dict(contacts)}
        review_shots.run(objects=[body.name], out=str(out / (name + ".png")),
                         views=["front", "right", "three_quarter"], mode="solid", res=args.res)
        if name == "Rest":
            review_shots.run(objects=[body.name], out=str(out / "Wire.png"),
                             views=["front", "right", "three_quarter"], mode="wire", res=args.res)
    (out / "poses.json").write_text(json.dumps(report, indent=2) + "\n")
    if not report["rest_mesh_qa"]["pass"]:
        raise ValueError(report["rest_mesh_qa"]["errors"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--qa", type=Path, required=True, help="Installed blender-asset-qa scripts directory")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--res", type=int, default=448)
    run(parser.parse_args(sys.argv[sys.argv.index("--") + 1:]))

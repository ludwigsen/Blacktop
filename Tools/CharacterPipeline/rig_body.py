"""Build a skinned derivative from a verified MPFB source in offline Blender."""
from pathlib import Path
import argparse
import hashlib
import sys

import bpy
import numpy as np
from bl_ext.blender_org.mpfb.services.humanservice import HumanService

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/Characters"
BODIES = ("Slim", "Athletic", "Muscular", "Stocky", "Power", "Massive")


def checked_source(relative):
    # MPFB's rig fitter addresses helper vertices by index. Reject a replaced input.
    manifest = (ART / "Validation/sources.sha256").read_text().splitlines()
    hashes = dict(line.split("  ", 1)[::-1] for line in manifest)
    path = ART / relative
    if hashlib.sha256(path.read_bytes()).hexdigest() != hashes[relative]:
        raise ValueError(f"Source checksum changed: {relative}")
    return path


def vertex_positions(mesh):
    values = np.empty(3 * len(mesh.vertices), dtype=np.float32)
    mesh.vertices.foreach_get("co", values)
    return values


def use_athletic_sculpt(mesh):
    path = checked_source("Sculpt/BT_Athletic_Sculpt_v1.blend")
    with bpy.data.libraries.load(str(path), link=False) as (available, loaded):
        loaded.objects = ["BT_Athletic_Sculpt"]
    sculpt = loaded.objects[0]
    # Verify correspondence on the masked skin, before assigning the sculpt positions.
    src = sculpt.data
    if (len(mesh.vertices) != len(src.vertices) or len(mesh.polygons) != len(src.polygons)
            or any(tuple(p.vertices) != tuple(q.vertices) for p, q in zip(mesh.polygons, src.polygons))):
        raise ValueError("Sculpt topology differs from the rigged visible surface")
    mesh.vertices.foreach_set("co", vertex_positions(src))
    mesh.update()
    bpy.data.objects.remove(sculpt, do_unlink=True)


def build(name, directory):
    target = directory / f"BT_{name}_Rigged.blend"
    # A new destination protects manual adjustments in a previous derivative.
    if target.exists():
        raise FileExistsError(target)
    source = checked_source(f"Core/BT_{name}.blend")
    bpy.ops.wm.open_mainfile(filepath=str(source), use_scripts=False)
    human = bpy.data.objects["Human"]
    rig = HumanService.add_builtin_rig(human, "game_engine", import_weights=True)
    dg = bpy.context.evaluated_depsgraph_get()
    skin = bpy.data.meshes.new_from_object(human.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    if name == "Athletic":
        use_athletic_sculpt(skin)

    # Keep the approved, evaluated skin with its UVs. Remove helper and control groups.
    body = bpy.data.objects.new(f"BT_{name}", skin)
    bpy.context.collection.objects.link(body)
    body.parent = rig
    skin.name = body.name
    bones = {b.name for b in rig.data.bones if b.use_deform}
    names = [v.name for v in human.vertex_groups]
    # Store weights before clearing the evaluated mesh's original group assignments.
    weights = []
    for vertex in skin.vertices:
        ranked = sorted(((g.weight, names[g.group]) for g in vertex.groups
                         if g.weight > 0 and names[g.group] in bones), reverse=True)[:4]
        if not ranked:
            raise ValueError(f"Vertex {vertex.index} has no MPFB deform weight")
        weights.append(ranked)
    # Discard the original index namespace along with the source object.
    body.vertex_groups.clear()
    groups = {n: body.vertex_groups.new(name=n) for n in names if n in bones}
    for index, ranked in enumerate(weights):
        total = sum(w for w, _ in ranked)
        for weight, bone in ranked:
            groups[bone].add([index], weight / total, 'REPLACE')
    skin.materials.clear()
    skin.materials.append(bpy.data.materials.new("Body"))
    modifier = body.modifiers.new("Skeleton", "ARMATURE")
    modifier.object = rig
    rig.name = rig.data.name = "BT_Skeleton"
    rig.data.bones["Root"].use_deform = False
    bpy.data.objects.remove(human, do_unlink=True)
    for text in list(bpy.data.texts):
        bpy.data.texts.remove(text)
    bpy.data.orphans_purge(do_recursive=True)
    # Store exactly one body and armature, at rest and with identity object transforms.
    bpy.context.view_layer.update()
    if len(bpy.context.scene.objects) != 2 or not np.allclose(vertex_positions(skin),
            vertex_positions(body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data), atol=1e-6):
        raise ValueError("Unexpected extra object or non-identity deformation at rest")
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    directory.mkdir(parents=True, exist_ok=True)
    if bpy.ops.wm.save_as_mainfile(filepath=str(target), compress=True) != {'FINISHED'}:
        raise RuntimeError(f"Could not save {target}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("body", choices=BODIES)
    parser.add_argument("--out", type=Path, default=ART / "Rigged")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    build(args.body, args.out.resolve())

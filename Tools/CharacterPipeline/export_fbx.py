"""Export only the bound skin and skeleton from the currently open production rig."""
from pathlib import Path
import sys
import bpy

# Keep the mesh and its bind pose. The existing Unity controller supplies animation.
body, = [o for o in bpy.context.scene.objects if o.type == 'MESH']
rig = body.find_armature()
if rig is None or body.parent != rig or len(bpy.context.scene.objects) != 2:
    raise ValueError("Expected exactly one bound skin and its parent rig")
if any(not b.matrix_basis.is_identity for b in rig.pose.bones):
    raise ValueError("Clear pose transforms before export")
target = Path(sys.argv[sys.argv.index("--") + 1]).resolve() / (body.name + ".fbx")
target.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for obj in (body, rig):
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
result = bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True,
    object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    use_armature_deform_only=True, add_leaf_bones=False, bake_anim=False,
    use_mesh_modifiers=False, use_custom_props=False)
if result != {'FINISHED'} or not target.stat().st_size:
    raise RuntimeError(f"FBX export failed: {target}")

"""Rebuild the editable Blender source, Unity FBX and procedural leather maps.
Run: blender --background --factory-startup --python SourceAssets/Football/build_football.py
All paths resolve from this script so the asset builds on Windows and macOS.
"""
from pathlib import Path
import math
import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/_Project/Art/Football'
OUT = ROOT / 'Builds/Validation'
ART.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
# Rebuilding also works after editing the mesh in Blender's Edit Mode.
if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
# Dimensions are authored into the mesh: 32 cm long, 19 cm wide before raised laces.
HALF_LENGTH, RADIUS = 0.16, 0.095
PALETTE = [(0.028, 0.031, 0.035, 1), (0.95, 0.245, 0.025, 1),
           (0.91, 0.87, 0.76, 1), (0.008, 0.008, 0.008, 1),
           (0.98, 0.94, 0.82, 1), (0.025, 0.025, 0.025, 1)]
NAMES = ['Leather', 'AccentPanel', 'ContrastPanel', 'Seams', 'Laces', 'Ink']
# Lettering controls: change these, save the script, then Run Script in Blender.
LETTERING = 'BLACKTOP'
LETTERING_WIDTH = 0.132
LETTERING_FONT = 'Assets/_Project/Scripts/UI/Fonts/Outfit-SemiBold.ttf'

# Tileable pebbled leather is authored procedurally, with no downloaded textures.
size = 512
v, u = np.mgrid[0:size, 0:size] / size
cells = 32
cx, cy = np.floor(u*cells).astype(int), np.floor(v*cells).astype(int)
rng = np.random.default_rng(71)
jx, jy = rng.uniform(-0.12, 0.12, (cells, cells)), rng.uniform(-0.12, 0.12, (cells, cells))
dx = (u*cells) % 1 - 0.5 - jx[cy, cx]
dy = (v*cells) % 1 - 0.5 - jy[cy, cx]
pebble = np.maximum(0, 1 - (dx*dx+dy*dy)/0.19)
shade = 0.86 + 0.11*pebble + rng.uniform(-0.025, 0.025, (size, size))
normal = np.stack([dx*pebble*0.45+0.5, dy*pebble*0.45+0.5, np.ones_like(dx), np.ones_like(dx)], axis=-1)
for filename, pixels in [('FootballLeather.png', np.stack([shade, shade, shade, np.ones_like(shade)], axis=-1)),
                         ('FootballLeatherNormal.png', normal)]:
    image = bpy.data.images.new(filename, width=size, height=size, alpha=True)
    if 'Normal' in filename:
        image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = str(ART / filename)
    image.file_format = 'PNG'
    image.save()
    image.pack()  # Keep the editable .blend portable across machines.

materials = []
for index, (name, color) in enumerate(zip(NAMES, PALETTE)):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get('Principled BSDF')
    principled.inputs['Base Color'].default_value = color
    principled.inputs['Roughness'].default_value = 0.8 if index < 4 else 0.6
    if index < 3:
        # Preview and Unity share the same exported UV leather maps.
        nodes, links = material.node_tree.nodes, material.node_tree.links
        albedo = nodes.new('ShaderNodeTexImage')
        albedo.image = bpy.data.images['FootballLeather.png']
        tint = nodes.new('ShaderNodeMixRGB')
        tint.blend_type = 'MULTIPLY'
        tint.inputs[0].default_value = 1
        tint.inputs[1].default_value = color
        links.new(albedo.outputs['Color'], tint.inputs[2])
        links.new(tint.outputs[0], principled.inputs['Base Color'])
        normal_texture = nodes.new('ShaderNodeTexImage')
        normal_texture.image = bpy.data.images['FootballLeatherNormal.png']
        normal = nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value = 0.7
        links.new(normal_texture.outputs['Color'], normal.inputs['Color'])
        links.new(normal.outputs['Normal'], principled.inputs['Normal'])
    materials.append(material)

# A pointed prolate profile, split into four broad, readable Street-inspired panels.
def radius(y):
    return RADIUS * max(0, 1 - (y/HALF_LENGTH)**2)**0.68

def mesh_object(name, vertices, faces, slots):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    for polygon, slot in zip(mesh.polygons, slots):
        polygon.material_index = slot
        polygon.use_smooth = True
    return obj

rings, segments = 40, 64
vertices, faces, slots = [], [], []
# Small tip caps avoid degenerate triangles while retaining a pointed silhouette.
ys = [-HALF_LENGTH+0.0003 + (2*HALF_LENGTH-0.0006)*i/rings for i in range(rings+1)]
for y in ys:
    for j in range(segments):
        phi = 2*math.pi*j/segments + math.pi/4
        vertices.append((radius(y)*math.cos(phi), y, radius(y)*math.sin(phi)))
for i in range(rings):
    for j in range(segments):
        faces.append((i*segments+j, i*segments+(j+1)%segments,
                      (i+1)*segments+(j+1)%segments, (i+1)*segments+j))
        phi = (2*math.pi*(j+0.5)/segments + math.pi/4) % (2*math.pi)
        panel = int(((phi + math.pi/4) % (2*math.pi)) / (math.pi/2))
        slot = [1, 0, 2, 0][panel]
        # Off-white end markings appear on the colored side panels, not across the laces.
        if slot == 1 and 0.104 < abs((ys[i]+ys[i+1])/2) < 0.119:
            slot = 2
        slots.append(slot)
faces.extend([tuple(reversed(range(segments))), tuple(rings*segments+j for j in range(segments))])
slots.extend([0, 0])
body = mesh_object('StreetFootball', vertices, faces, slots)
# UVs repeat around the ball; seam vertices get independent per-face UVs.
uvs = body.data.uv_layers.new(name='FootballUV')
for polygon in body.data.polygons:
    phis = []
    for vertex_id in polygon.vertices:
        point = body.data.vertices[vertex_id].co
        phis.append((math.atan2(point.z, point.x) % (2*math.pi))/(2*math.pi))
    wraps = max(phis)-min(phis) > 0.5
    for loop, p in zip(polygon.loop_indices, phis):
        point = body.data.vertices[body.data.loops[loop].vertex_index].co
        if wraps and p < 0.5:
            p += 1
        uvs.data[loop].uv = ((point.y/HALF_LENGTH+1)/2*4, p*2)

parts = [body]
def cord(name, points, thickness, material_index):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = thickness
    curve.bevel_resolution = 1
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for point, position in zip(spline.points, points):
        point.co = (*position, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(materials[material_index])
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    obj.select_set(False)
    parts.append(obj)

# Four narrow panel seams and an oversized, physically modeled lace assembly.
for k in range(4):
    phi = math.pi/4+k*math.pi/2
    cord(f'PanelSeam{k}', [(radius(y)*1.002*math.cos(phi), y, radius(y)*1.002*math.sin(phi)) for y in ys], 0.00065, 3)
cord('LaceSlit', [(0, y, radius(y)+0.0004) for y in np.linspace(-0.056, 0.056, 22)], 0.0035, 3)
for sign in [-1, 1]:
    cord('LaceRail', [(sign*0.012, y, math.sqrt(max(0, radius(y)**2-0.012**2))+0.003)
                      for y in np.linspace(-0.05, 0.05, 24)], 0.0015, 4)
for index, y in enumerate(np.linspace(-0.043, 0.043, 8)):
    cord(f'Lace{index}', [(x, y, math.sqrt(max(0, radius(y)**2-x*x))+0.004)
                         for x in np.linspace(-0.018, 0.018, 12)], 0.0021, 4)

# Convert a real Blacktop wordmark to mesh and conform it to the orange side panel.
text_curve = bpy.data.curves.new('BLACKTOP', 'FONT')
text_curve.body = LETTERING
text_curve.align_x = 'CENTER'
text_curve.align_y = 'CENTER'
font = ROOT / LETTERING_FONT
text_curve.font = bpy.data.fonts.load(str(font))
text_curve.size = 1
text_curve.shear = 0.12
text = bpy.data.objects.new('BlacktopStamp', text_curve)
bpy.context.collection.objects.link(text)
bpy.context.view_layer.objects.active = text
text.select_set(True)
bpy.ops.object.convert(target='MESH')
# Triangulate/subdivide so the stamp conforms without cutting through the curved ball.
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.quads_convert_to_tris()
bpy.ops.mesh.subdivide(number_cuts=2)
bpy.ops.object.mode_set(mode='OBJECT')
scale = LETTERING_WIDTH / text.dimensions.x
for vertex in text.data.vertices:
    y = vertex.co.x*scale
    z = vertex.co.y*scale
    x = math.sqrt(max(0, radius(y)**2-z*z))+0.00065
    vertex.co = Vector((x, y, z))
text.data.materials.append(materials[5])
parts.append(text)
# Join into one renderer, with the six stable material slots used by FootballAppearance.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
body.name = 'StreetFootball'
body['dimensions_metres'] = '0.32 length x 0.19 diameter, plus raised laces'
body['material_slots'] = ', '.join(NAMES)
bpy.ops.export_scene.fbx(filepath=str(ART/'StreetFootball.fbx'), use_selection=True,
                         object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                         apply_unit_scale=True, bake_anim=False, use_mesh_modifiers=True,
                         add_leaf_bones=False, path_mode='STRIP')

# A studio preview is kept outside Assets, so lights/floor never enter the Unity model.
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.098))
floor = bpy.context.object
floor.name = 'PreviewFloor'
floor_material = bpy.data.materials.new('PreviewAsphalt')
floor_material.use_nodes = True
floor_material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.025, 0.028, 0.032, 1)
floor_material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.85
floor.data.materials.append(floor_material)
for name, location, energy, size in [('Key', (0.5,-0.4,0.7),8,0.45),
                                     ('Fill', (-0.4,0.2,0.3),4,0.5),
                                     ('Rim', (0.1,0.5,0.5),8,0.3)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = size
    light.rotation_euler = (Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0.57, -0.40, 0.36))
camera = bpy.context.object
camera.rotation_euler = (Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 0.47
bpy.context.scene.camera = camera
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.world.color = (0.18,0.18,0.18)
scene.render.resolution_x, scene.render.resolution_y = 1200, 850
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.render.filepath = str(OUT/'Football-hero.png')
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceAssets/Football/StreetFootball.blend'))
bpy.ops.render.render(write_still=True)
print('FOOTBALL_EXPORTED', len(body.data.vertices), 'vertices', len(body.data.polygons), 'faces')

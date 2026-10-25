"""Run in Blender's Text Editor, or install this file as a legacy add-on beside its sibling modules."""
bl_info = {'name': 'Blacktop Body Editor', 'author': 'Blacktop', 'version': (1, 0, 0),
           'blender': (5, 2, 0), 'location': '3D View > Sidebar > Blacktop', 'category': 'Mesh'}
from pathlib import Path
import json
import sys
import uuid
import textwrap
import bpy
from bpy.props import EnumProperty, FloatProperty, StringProperty, BoolProperty, PointerProperty
from bpy_extras.io_utils import ImportHelper, ExportHelper
from mathutils import Vector

# Resolve sibling modules even when launched from a .blend Text block without an absolute __file__.
HERE = Path(bpy.data.filepath).resolve().parent
if not (HERE/'engine.py').exists(): HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.dont_write_bytecode = True
from engine import KEYS, LABELS, HEIGHTS, read_json, validate_preset
import pipeline

_BUSY = False
_ITEMS = []
_ITEM_CACHE = {}
ORDER = ('Lean', 'Athletic', 'Built', 'Thick', 'Heavy', 'Massive')


def saved(scene):
    if 'blacktop_presets' not in scene:
        defaults = [read_json(HERE/'Presets'/(name+'.json')) for name in ORDER]
        scene['blacktop_presets'] = json.dumps({p['id']: p for p in defaults})
    return json.loads(scene['blacktop_presets'])


def items(self, context):
    global _ITEMS
    values = saved(context.scene) if context else {}
    # Keep strings alive for Blender's dynamic EnumProperty callback.
    _ITEMS = [_ITEM_CACHE.setdefault((key, p['name'], p['family']), (key, p['name'], p['family'])) for key, p in values.items()]
    return _ITEMS


def current(settings):
    values = saved(bpy.context.scene)
    key = settings.preset or next(iter(values))
    return {'version': 'blacktop.body.v1', 'reference': 'athletic-183-v1', 'id': key,
            'name': settings.preset_name, 'family': settings.family,
            'percent': {key: getattr(settings, key) for key in KEYS}}


def refresh(self, context):
    if _BUSY: return
    try:
        preset = validate_preset(current(self)); values = saved(context.scene); values[preset['id']] = preset
        context.scene['blacktop_presets'] = json.dumps(values)
        report = pipeline.update_preview(preset, self.clothing)
        self.status = ('Ready: max measurement error %.2f%%' % (report['max_relative_error']*100)) if report['valid'] else 'Outside 1% tolerance: revise targets; export blocked.'
    except Exception as error:
        if 'blacktop_report' in context.scene: del context.scene['blacktop_report']
        self.status = 'Last valid preview retained: '+str(error)


def choose(self, context):
    global _BUSY
    values = saved(context.scene)
    if self.preset not in values: return
    _BUSY = True
    try:
        preset = values[self.preset]; self.family = preset['family']; self.preset_name = preset['name']
        for key in KEYS: setattr(self, key, preset['percent'][key])
        for name in ORDER:
            if preset['id'] == 'body.'+name.lower()+'.parametric.v1': self.export_target = name
    finally: _BUSY = False
    refresh(self, context)


class BT_Settings(bpy.types.PropertyGroup):
    preset: EnumProperty(name='Preset', items=items, update=choose)
    family: EnumProperty(name='Shape family', items=[(x, x, '') for x in ('Tapered', 'Power', 'Barrel')], update=refresh)
    preset_name: StringProperty(name='Name', default='Athletic', update=refresh)
    unit: EnumProperty(name='Readout units', items=[('IN', 'Inches', ''), ('CM', 'Centimeters', '')])
    height: EnumProperty(name='Calibration height', items=[(str(h), str(h)+' cm', '') for h in HEIGHTS], default='183')
    clothing: BoolProperty(name='Preview shell clothing', default=False, update=refresh)
    export_target: EnumProperty(name='Unity target', items=[(x, x, '') for x in ORDER], default='Athletic')
    status: StringProperty(default='Choose a preset to begin.')

# Sliders always mean a percentage of the same measured Athletic canonical, regardless of selected family.
for key, label in zip(KEYS, LABELS):
    BT_Settings.__annotations__[key] = FloatProperty(name=label+' %', default=100, min=60, max=180,
        precision=1, description='Percent of athletic-183-v1 '+label.lower()+'; 110 means 1.10 times that reference', update=refresh)


class BT_OT_Reset(bpy.types.Operator):
    bl_idname = 'blacktop.body_reset'; bl_label = 'Reset current preset'
    def execute(self, context):
        settings = context.scene.blacktop_body; values = saved(context.scene)
        default = next((read_json(HERE/'Presets'/(name+'.json')) for name in ORDER if settings.preset == 'body.'+name.lower()+'.parametric.v1'), None)
        if default is None: default = json.loads(context.scene.get('blacktop_custom_defaults', '{}')).get(settings.preset)
        if default: values[settings.preset] = default; context.scene['blacktop_presets'] = json.dumps(values); choose(settings, context)
        return {'FINISHED'}


class BT_OT_Duplicate(bpy.types.Operator):
    bl_idname = 'blacktop.body_duplicate'; bl_label = 'Duplicate preset'
    def execute(self, context):
        settings = context.scene.blacktop_body; preset = current(settings)
        preset['id'] = 'body.custom.'+uuid.uuid4().hex; preset['name'] += ' Copy'
        values = saved(context.scene); values[preset['id']] = preset; context.scene['blacktop_presets'] = json.dumps(values)
        defaults = json.loads(context.scene.get('blacktop_custom_defaults', '{}')); defaults[preset['id']] = preset
        context.scene['blacktop_custom_defaults'] = json.dumps(defaults); settings.preset = preset['id']
        return {'FINISHED'}


class BT_OT_Save(bpy.types.Operator, ExportHelper):
    bl_idname = 'blacktop.body_save'; bl_label = 'Save JSON preset'; filename_ext = '.json'
    filter_glob: StringProperty(default='*.json', options={'HIDDEN'})
    def execute(self, context):
        Path(self.filepath).write_text(json.dumps(validate_preset(current(context.scene.blacktop_body)), indent=2)+'\n')
        return {'FINISHED'}


class BT_OT_Load(bpy.types.Operator, ImportHelper):
    bl_idname = 'blacktop.body_load'; bl_label = 'Load JSON preset'
    filter_glob: StringProperty(default='*.json', options={'HIDDEN'})
    def execute(self, context):
        try:
            preset = validate_preset(read_json(self.filepath)); values = saved(context.scene); values[preset['id']] = preset
            context.scene['blacktop_presets'] = json.dumps(values)
            defaults = json.loads(context.scene.get('blacktop_custom_defaults', '{}')); defaults[preset['id']] = preset
            context.scene['blacktop_custom_defaults'] = json.dumps(defaults)
            context.scene.blacktop_body.preset = preset['id']; choose(context.scene.blacktop_body, context)
        except Exception as error: self.report({'ERROR'}, str(error)); return {'CANCELLED'}
        return {'FINISHED'}


class BT_OT_View(bpy.types.Operator):
    bl_idname = 'blacktop.body_view'; bl_label = 'Inspect silhouette'
    view: StringProperty(default='Front')
    def execute(self, context):
        # Camera buttons work on a trackpad with no numpad, orbit gesture or middle mouse button.
        camera = context.scene.camera; target = Vector((0, 0, .87))
        camera.location = {'Front': (0, -4, .87), 'Side': (4, 0, .87), 'Three-quarter': (3, -4, 1.05)}[self.view]
        camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.type = 'ORTHO'; camera.data.ortho_scale = 2.10
        for area in context.screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.overlay.show_overlays = False  # Keep bones/lights out of silhouette inspection.
        return {'FINISHED'}


class BT_OT_Export(bpy.types.Operator):
    bl_idname = 'blacktop.body_export'; bl_label = 'Export body meshes'
    all_bodies: BoolProperty(default=False)
    def execute(self, context):
        settings = context.scene.blacktop_body
        try:
            if self.all_bodies:
                values = saved(context.scene); presets = [values['body.'+name.lower()+'.parametric.v1'] for name in ORDER]
            else:
                preset = current(settings)
                # Explicit target selection allows a duplicated custom preset to update an existing Unity target safely.
                preset['id'] = 'body.'+settings.export_target.lower()+'.parametric.v1'; presets = [preset]
            packet = pipeline.export_presets(presets)
            settings.status = 'Exported %d baked meshes. Apply GeneratedBodies.json in Unity.' % len(packet['meshes'])
        except Exception as error: settings.status = str(error); self.report({'ERROR'}, str(error)); return {'CANCELLED'}
        return {'FINISHED'}


class BT_PT_Body(bpy.types.Panel):
    bl_label = 'Blacktop Body Editor'; bl_idname = 'BT_PT_body_editor'
    bl_space_type = 'VIEW_3D'; bl_region_type = 'UI'; bl_category = 'Blacktop'
    def draw(self, context):
        layout = self.layout; settings = context.scene.blacktop_body
        layout.prop(settings, 'preset'); layout.prop(settings, 'family', text='Family'); layout.prop(settings, 'preset_name', text='Rename')
        row = layout.row(align=True); row.operator('blacktop.body_reset', text='Reset'); row.operator('blacktop.body_duplicate', text='Duplicate')
        row = layout.row(align=True); row.operator('blacktop.body_load', text='Load JSON'); row.operator('blacktop.body_save', text='Save JSON')
        row = layout.row(align=True)
        for view in ('Front', 'Side', 'Three-quarter'): row.operator('blacktop.body_view', text='3/4' if view == 'Three-quarter' else view).view = view
        layout.prop(settings, 'clothing')
        layout.prop(settings, 'export_target')
        row = layout.row(align=True); row.operator('blacktop.body_export', text='Export selected'); row.operator('blacktop.body_export', text='Export all six').all_bodies = True
        layout.label(text='100% = Athletic canonical at 183 cm')
        layout.label(text='Reference ID: athletic-183-v1')
        for key in KEYS: layout.prop(settings, key, slider=True)
        layout.prop(settings, 'unit', text='Units'); layout.prop(settings, 'height', text='Height')
        layout.label(text='Height changes readouts; rig stays fixed.')
        report = json.loads(context.scene.get('blacktop_report', '{}'))
        if report:
            e = pipeline.engine(); factor = e.calibration*int(settings.height)/183*(1/.0254 if settings.unit == 'IN' else 100)
            box = layout.box(); box.label(text='Reference / Requested / Measured ('+('in' if settings.unit == 'IN' else 'cm')+')')
            for key, label in zip(KEYS, LABELS):
                m = report['measurements'][key]
                box.label(text=label)
                row = box.row(align=True)
                for field in ('reference_m', 'requested_m', 'measured_m'): row.label(text='%.2f' % (m[field]*factor))
        for line in textwrap.wrap(settings.status, max(24, int(context.region.width/7))): layout.label(text=line)
        layout.label(text='Prototype shells: seam/animation fit needs review.')


CLASSES = (BT_Settings, BT_OT_Reset, BT_OT_Duplicate, BT_OT_Save, BT_OT_Load, BT_OT_View, BT_OT_Export, BT_PT_Body)


def register():
    # Blender does not expose every registered PropertyGroup by Python class name.
    # Keep the actual class objects on Scene so rerunning the Text block and reopening files are both safe.
    previous = getattr(bpy.types.Scene, '_blacktop_body_classes', ())
    if hasattr(bpy.types.Scene, 'blacktop_body'): del bpy.types.Scene.blacktop_body
    for old in reversed(previous):
        if old.is_registered: bpy.utils.unregister_class(old)
    for cls in CLASSES: bpy.utils.register_class(cls)
    bpy.types.Scene._blacktop_body_classes = CLASSES
    bpy.types.Scene.blacktop_body = PointerProperty(type=BT_Settings)
    settings = bpy.context.scene.blacktop_body
    settings.preset = 'body.athletic.parametric.v1'; choose(settings, bpy.context)


def unregister():
    if hasattr(bpy.types.Scene, 'blacktop_body'): del bpy.types.Scene.blacktop_body
    for cls in reversed(getattr(bpy.types.Scene, '_blacktop_body_classes', CLASSES)):
        if cls.is_registered: bpy.utils.unregister_class(cls)
    bpy.types.Scene._blacktop_body_classes = ()

if __name__ == '__main__': register()

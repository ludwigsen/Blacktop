"""Blender integration/geometry QA. Run against the saved canonical; renders actual generated meshes."""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import time
import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE)); sys.dont_write_bytecode = True
import engine
import pipeline
import body_editor


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--tapered', action='store_true'); parser.add_argument('--render', action='store_true')
    options = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    root = HERE.parents[2]; out = root/'Builds/Validation/BodyGenerator'; out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'CanonicalBody.blend')); body_editor.register(); body_editor.register()
    e = pipeline.require_canonical(); untouched = e.base.copy(); checksum = e.reference_hash
    names = ('Lean', 'Athletic') if options.tapered else body_editor.ORDER
    reports = []; solved = {}; t = time.monotonic()
    for name in names:
        preset = engine.read_json(HERE/'Presets'/(name+'.json'))
        positions, report = e.solve(preset); assert report['valid'], report
        assert np.isfinite(positions).all()
        assert max(v['relative_difference'] for v in report['bilateral'].values()) < .001
        assert np.array_equal(e.base, untouched) and engine.digest(e.data) == checksum
        # A different intervening slider state cannot affect subsequent output.
        altered = json.loads(json.dumps(preset)); altered['percent']['chest'] += 3; e.solve(altered)
        restored, _ = e.solve(preset); assert np.array_equal(positions, restored)
        neck = e.data['neck']['ring_vertex_indices']; assert np.array_equal(positions[neck], e.base[neck])
        # Every generated surface keeps original UV/weight arrays and triangle indexing.
        row = pipeline.body_row(positions)
        for key in ('uv', 'indices', 'weights', 'triangles'): assert row[key] == e.data['body'][key]
        for slot in ('Top', 'Shorts', 'Footwear'):
            garment = pipeline.garment(row, slot)
            assert len(garment['triangles']) > 0
            assert np.allclose(np.array(garment['weights']).reshape(-1, 4).sum(axis=1), 1, atol=1e-6)
        solved[name] = positions; reports.append(report)
        print('BODY_VALIDATED', name, report['max_relative_error'], flush=True)
    # Validate slider independence beyond the preset matrix, including coupled chest circumference/depth requests.
    athletic = engine.read_json(HERE/'Presets/Athletic.json')
    slider_cases = []
    for key in engine.KEYS:
        for multiplier in (.95, 1.05):
            test = json.loads(json.dumps(athletic)); test['percent'][key] *= multiplier
            _, report = e.solve(test); assert report['valid'], (key, multiplier, report)
            slider_cases.append({'key': key, 'multiplier': multiplier, 'error': report['max_relative_error']})
    # Exercise the panel's saved state, preset switching, duplicate/rename/reset and JSON validation path.
    settings = bpy.context.scene.blacktop_body
    settings.preset = 'body.lean.parametric.v1'; settings.preset = 'body.massive.parametric.v1'
    assert settings.family == 'Barrel'
    bpy.ops.blacktop.body_duplicate(); custom = settings.preset; settings.preset_name = 'QA Custom'
    settings.waist += 2; bpy.ops.blacktop.body_reset()
    assert body_editor.saved(bpy.context.scene)[custom]['percent']['waist'] == engine.read_json(HERE/'Presets/Massive.json')['percent']['waist']
    saved = out/'custom-preset.json'; bpy.ops.blacktop.body_save(filepath=str(saved)); bpy.ops.blacktop.body_load(filepath=str(saved))
    assert settings.preset == custom
    invalid = json.loads(saved.read_text()); invalid['percent']['chest'] = float('nan')
    try: engine.validate_preset(invalid); raise AssertionError('NaN accepted')
    except ValueError: pass
    presets = [engine.read_json(HERE/'Presets'/(name+'.json')) for name in names]
    selected = pipeline.export_presets(presets[:1], out/'selected.json')
    assert len(selected['meshes']) == 5
    assert all(row['name'].endswith('_'+names[0]) or row['name'] == 'Head_Prototype' for row in selected['meshes'])
    packet = pipeline.export_presets(presets, root/'Builds/CharacterExports'/('TaperedBodies.json' if options.tapered else 'GeneratedBodies.json'))
    assert len(packet['meshes']) == len(names)*4+1
    # A posed rig is rejected by the original bridge even for an evaluated export copy.
    rig = bpy.data.objects['Blacktop_Rig']; rig.pose.bones[0].rotation_mode = 'XYZ'; rig.pose.bones[0].rotation_euler.x = .1
    try: pipeline.export_presets(presets, out/'invalid.json'); raise AssertionError('Posed rig accepted')
    except RuntimeError: pass
    rig.pose.bones[0].rotation_euler.x = 0
    if not options.tapered:
        widths = {name: {section: e.section(points, section)[1] for section in ('chest', 'waist', 'hip')} for name, points in solved.items()}
        assert widths['Massive']['waist']/widths['Massive']['chest'] > widths['Lean']['waist']/widths['Lean']['chest']+.15
        report_shapes = widths
    else: report_shapes = {}
    result = {'canonical_hash': checksum, 'presets': reports, 'slider_cases': slider_cases, 'silhouette_widths': report_shapes,
              'deterministic_reset': True, 'immutable_reference': True, 'fixed_neck': True, 'panel_roundtrip': True,
              'rig_pose_rejected': True, 'panel_reregister': True, 'selected_target_isolation': True, 'seconds': time.monotonic()-t, 'clothing': 'Matching offset shells; motion fit requires review.'}
    (out/('tapered-results.json' if options.tapered else 'results.json')).write_text(json.dumps(result, indent=2))
    if options.render:
        scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 16
        scene.render.resolution_x = 480; scene.render.resolution_y = 640; scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        for name in names:
            preset = engine.read_json(HERE/'Presets'/(name+'.json')); pipeline.update_preview(preset)
            for view, location in [('front', (0, -4, .87)), ('side', (4, 0, .87)), ('three-quarter', (3, -4, 1.05))]:
                camera = scene.camera; camera.location = location; target = Vector((0, 0, .87))
                camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler(); camera.data.ortho_scale = 2.1
                scene.render.filepath = str(out/(name+'-'+view+'.png')); bpy.ops.render.render(write_still=True)
    print('BODY_GENERATOR_VALIDATION_PASS', len(names), result['seconds'], flush=True)

if __name__ == '__main__': main()

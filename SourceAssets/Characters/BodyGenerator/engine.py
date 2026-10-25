"""Deterministic anatomical surface deformation in canonical Unity mesh coordinates (metres)."""
from pathlib import Path
import gzip
import hashlib
import json
import numpy as np

HERE = Path(__file__).resolve().parent
VERSION = 'blacktop.body.v1'
KEYS = ('shoulder', 'chest', 'waist', 'quad', 'calf', 'bicep', 'forearm',
        'chest_depth', 'abdomen_depth', 'hip_depth')
LABELS = ('Shoulder width', 'Chest circumference', 'Waist circumference', 'Quad circumference',
          'Calf circumference', 'Bicep circumference', 'Forearm circumference', 'Chest depth',
          'Abdominal depth', 'Hip/glute depth')
HEIGHTS = (170, 175, 178, 180, 183, 185, 188, 191, 193, 196, 198, 201)


def digest(data):
    # Hash the immutable interchange values, rather than platform-specific compressed bytes.
    return hashlib.sha256(json.dumps(data, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text())


def validate_preset(preset):
    if preset.get('version') != VERSION or preset.get('reference') != 'athletic-183-v1':
        raise ValueError('Preset version/reference does not match the canonical body.')
    if preset.get('family') not in ('Tapered', 'Power', 'Barrel'):
        raise ValueError('Unknown anatomical family.')
    if not isinstance(preset.get('id'), str) or not preset['id'].strip() or not str(preset.get('name', '')).strip():
        raise ValueError('A preset needs a stable ID and display name.')
    values = preset.get('percent', {})
    if set(values) != set(KEYS) or any(not isinstance(v, (int, float)) or not np.isfinite(v) or not 60 <= v <= 180 for v in values.values()):
        raise ValueError('All ten measurements must be finite percentages between 60 and 180.')
    return preset


def smooth(a, b, x):
    t = np.clip((x-a)/(b-a), 0, 1)
    return t*t*(3-2*t)


class BodyEngine:
    def __init__(self, canonical=None):
        if canonical is None:
            with gzip.open(HERE/'canonical.json.gz', 'rt') as stream:
                canonical = json.load(stream)
        self.data = canonical
        self.reference_hash = digest(canonical)
        self.base = np.array(canonical['body']['positions'], dtype=float).reshape(-1, 3)
        self.triangles = np.array(canonical['body']['triangles'], dtype=int).reshape(-1, 3)
        self.bones = {b['name']: np.array(b['matrix'][12:15]) for b in canonical['bones']}
        self.profiles = read_json(HERE/'profiles.json')
        self.weights = np.array(canonical['body']['weights']).reshape(-1, 4)
        self.indices = np.array(canonical['body']['indices']).reshape(-1, 4)
        self.names = canonical['body']['boneNames']
        self.fields = self.make_fields()
        self.sections = self.make_sections()
        self.shoulder_landmarks = self.make_shoulder_landmarks()
        self.baseline = self.measure(self.base)
        # Mesh stature includes the detached prototype head; height remains an independent uniform calibration.
        self.calibration = 1.83 / canonical['source_stature_m']

    def influence(self, names):
        return np.sum(self.weights * np.isin(self.indices, [self.names.index(n) for n in names if n in self.names]), axis=1)

    def make_fields(self):
        p = self.base
        torso = self.influence(('Hips', 'Spine', 'Chest', 'UpperChest', 'LeftShoulder', 'RightShoulder'))
        # Lock the shared collar; no family or measurement can move the neck attachment ring.
        torso *= 1-smooth(1.33, 1.45, p[:, 1])
        fields = {'torso': torso, 'shoulder': []}
        for side in ('Left', 'Right'):
            joint = self.bones[side+'UpperArm']; end = self.bones[side+'LowerArm']
            axis = (end-joint)/np.linalg.norm(end-joint)
            delta = p-joint; along = delta@axis
            radial = delta-along[:, None]*axis
            radial[:, 1] = 0  # Deltoid breadth/depth does not move the chest tape or change vertical joint landmarks.
            # Rounded deltoid caps expand around the fixed humerus, rather than stretching a torso shelf over it.
            mask = np.exp(-(np.linalg.norm(delta, axis=1)/.115)**4)
            mask *= smooth(1.285, 1.34, p[:, 1])*(1-smooth(1.43, 1.495, p[:, 1]))
            fields['shoulder'].append((mask, radial))
        for name, start, end in [('quad', 'UpperLeg', 'LowerLeg'), ('calf', 'LowerLeg', 'Foot'),
                                 ('bicep', 'UpperArm', 'LowerArm'), ('forearm', 'LowerArm', 'Hand')]:
            fields[name] = []
            for side in ('Left', 'Right'):
                a, b = self.bones[side+start], self.bones[side+end]
                axis = b-a; length = np.linalg.norm(axis); axis /= length
                along = (p-a)@axis / length
                center = a + np.clip(along, 0, 1)[:, None]*(b-a)
                radial = p-center
                radial -= (radial@axis)[:, None]*axis
                mask = self.influence((side+start, side+start+'Twist'))
                # Cross-sectional swelling fades to zero at joints; hands/feet and grip positions stay fixed.
                mask *= smooth(.02, .23, along)*(1-smooth(.80, .99, along))
                fields[name].append((mask, radial, along))
        return fields

    def make_sections(self):
        # Planes are fixed to the rest skeleton, with bilateral limb sections normal to the bone axis.
        specs = {}
        for name, height in [('shoulder', 1.405), ('chest', 1.285), ('waist', 1.045), ('hip', .91)]:
            specs[name] = (np.array((0., height, -.0126)), np.array((0., 1., 0.)), np.array((1., 0., 0.)))
        for name, start, end, fraction in [('quad', 'UpperLeg', 'LowerLeg', .40), ('calf', 'LowerLeg', 'Foot', .42),
                                          ('bicep', 'UpperArm', 'LowerArm', .48), ('forearm', 'LowerArm', 'Hand', .42)]:
            for side in ('Left', 'Right'):
                a, b = self.bones[side+start], self.bones[side+end]
                normal = (b-a)/np.linalg.norm(b-a)
                u = np.cross(normal, (0, 0, 1)); u /= np.linalg.norm(u)
                specs[name+side] = (a+(b-a)*fraction, normal, u)
        result = {}
        for key, (origin, normal, u) in specs.items():
            # Candidate filtering uses canonical anatomy only, so changing volume cannot change region identity.
            centers = self.base[self.triangles].mean(axis=1)
            if key in ('shoulder', 'chest', 'waist', 'hip'):
                valid = np.ones(len(centers), dtype=bool)
            else:
                valid = np.linalg.norm(centers-origin, axis=1) < .40
                valid &= centers[:, 0]*origin[0] > 0
            result[key] = (origin, normal, u, np.cross(normal, u), self.triangles[valid])
        return result

    def make_shoulder_landmarks(self):
        # Track canonical bi-deltoid surface points through deformation; the landmarks do not jump to another loop.
        points = []
        for a, b in ((0, 1), (1, 2), (2, 0)):
            ia, ib = self.triangles[:, a], self.triangles[:, b]
            da, db = self.base[ia, 1]-1.405, self.base[ib, 1]-1.405
            crossing = da*db < 0
            for i, j, t in zip(ia[crossing], ib[crossing], da[crossing]/(da[crossing]-db[crossing])):
                points.append((self.base[i, 0]*(1-t)+self.base[j, 0]*t, int(i), int(j), float(t)))
        return (min(points), max(points))

    def section(self, positions, key):
        origin, normal, u, v, triangles = self.sections[key]
        points = positions[triangles]
        distances = (points-origin)@normal
        crossing = (distances.min(axis=1) < -1e-10) & (distances.max(axis=1) > 1e-10)
        points, distances = points[crossing], distances[crossing]
        segments = []
        for i, j in ((0, 1), (1, 2), (2, 0)):
            ok = distances[:, i]*distances[:, j] < 0
            t = distances[ok, i]/(distances[ok, i]-distances[ok, j])
            xyz = points[ok, i]+t[:, None]*(points[ok, j]-points[ok, i])-origin
            segments.append((np.where(ok)[0], np.column_stack((xyz@u, xyz@v))))
        edges = [[] for _ in points]
        for rows, xy in segments:
            for row, point in zip(rows, xy):
                edges[row].append(tuple(np.round(point, 5)))
        # Weld UV duplicates for measurement only. Mesh vertices, UV seams and weights remain untouched.
        graph = {}
        for edge in edges:
            if len(edge) != 2 or edge[0] == edge[1]:
                continue
            a, b = edge
            graph.setdefault(a, set()).add(b); graph.setdefault(b, set()).add(a)
        components = []
        unseen = set(graph)
        while unseen:
            todo = [unseen.pop()]; component = set(todo)
            while todo:
                for other in graph[todo.pop()]:
                    if other in unseen:
                        unseen.remove(other); component.add(other); todo.append(other)
            # A valid measuring tape follows a closed section, not a partial arc or another limb.
            if len(component) >= 6 and all(len(graph[p]) == 2 for p in component):
                components.append(component)
        if not components:
            raise ValueError(f'{key}: no closed anatomical section; target or topology is unsupported.')
        component = min(components, key=lambda c: np.linalg.norm(np.mean(list(c), axis=0)))
        perimeter = sum(np.linalg.norm(np.array(a)-b) for a in component for b in graph[a])/2
        xy = np.array(list(component))
        return perimeter, np.ptp(xy[:, 0]), np.ptp(xy[:, 1])

    def measure(self, positions):
        sections = {name: self.section(positions, name) for name in self.sections if name != 'shoulder'}
        shoulder = [positions[i, 0]*(1-t)+positions[j, 0]*t for _, i, j, t in self.shoulder_landmarks]
        result = [shoulder[1]-shoulder[0], sections['chest'][0], sections['waist'][0]]
        for key in ('quad', 'calf', 'bicep', 'forearm'):
            result.append((sections[key+'Left'][0]+sections[key+'Right'][0])/2)
        return np.array(result+[sections['chest'][2], sections['waist'][2], sections['hip'][2]])

    def deform(self, parameters, family):
        profile = self.profiles['families'][family]
        p = self.base; y = p[:, 1]; z = p[:, 2]+.0126; torso = self.fields['torso']
        stations = np.array(profile['stations'])
        width = np.interp(y/1.83, stations[:, 0], stations[:, 1])
        depth = np.interp(y/1.83, stations[:, 0], stations[:, 2])
        shoulder = np.exp(-((y-1.405)/.075)**2)
        chest = np.exp(-((y-1.285)/.105)**2)
        waist = np.exp(-((y-1.045)/.125)**2)
        hip = np.exp(-((y-.91)/.095)**2)
        # Width/depth are independent unknowns; circumference is measured after their coupled effects.
        width *= np.exp(parameters[1]*chest+parameters[3]*waist)
        depth *= np.exp(parameters[2]*chest+parameters[4]*waist+parameters[5]*hip)
        result = p.copy()
        result[:, 0] += torso*p[:, 0]*(width-1)
        # Family anterior/posterior distribution changes contour without translating skeleton joints.
        front = smooth(-.015, .025, z)
        bias = profile['anterior_bias']
        depth = 1+(depth-1)*(1+bias*(front*2-1))
        result[:, 2] += torso*z*(depth-1)
        for mask, radial in self.fields['shoulder']:
            result += (mask*np.expm1(parameters[0]))[:, None]*radial
        for i, name in enumerate(('quad', 'calf', 'bicep', 'forearm')):
            for mask, radial, along in self.fields[name]:
                # A normalized local contour distinguishes muscular peak from distributed barrel mass.
                contour = 1+profile['muscle_peak']*np.exp(-((along-.43)/.18)**2)
                scale = np.exp(parameters[6+i])*profile['limb_fullness'][i]*contour
                result += (mask*(scale-1))[:, None]*radial
        return result

    def solve(self, preset):
        validate_preset(preset)
        target = self.baseline*np.array([preset['percent'][key]/100 for key in KEYS])
        params = np.zeros(10)
        best = None
        # Damped finite-difference Newton: solve all measurements from canonical on every evaluation.
        for iteration in range(16):
            positions = self.deform(params, preset['family'])
            measured = self.measure(positions); residual = np.log(measured/target)
            error = np.max(np.abs(measured/target-1))
            if best is None or error < best[0]:
                best = (error, positions, measured, params.copy(), iteration)
            if error < .0015:
                break
            jacobian = np.zeros((10, 10))
            for j in range(10):
                probe = params.copy(); probe[j] += .003
                jacobian[:, j] = (np.log(self.measure(self.deform(probe, preset['family']))/target)-residual)/.003
            step = np.linalg.lstsq(jacobian, -residual, rcond=1e-5)[0]
            step = np.clip(step, -.20, .20)
            accepted = False
            for damping in (1., .5, .25, .125):
                trial = np.clip(params+damping*step, -1.1, 1.4)
                try:
                    trial_error = np.max(np.abs(self.measure(self.deform(trial, preset['family']))/target-1))
                except ValueError:
                    continue
                if trial_error < error:
                    params = trial; accepted = True; break
            if not accepted:
                break
        error, positions, measured, params, iterations = best
        bilateral = {}
        for key in ('quad', 'calf', 'bicep', 'forearm'):
            left = self.section(positions, key+'Left')[0]; right = self.section(positions, key+'Right')[0]
            bilateral[key] = {'left_m': left, 'right_m': right, 'relative_difference': abs(left-right)/max(left, right)}
        report = {'preset': preset['id'], 'family': preset['family'], 'version': VERSION,
                  'canonical_hash': self.reference_hash, 'max_relative_error': float(error),
                  'tolerance': .01, 'valid': bool(error <= .01), 'iterations': iterations+1,
                  'parameters': params.tolist(), 'bilateral': bilateral, 'measurements': {key: {'reference_m': float(base), 'requested_m': float(want),
                    'measured_m': float(got), 'calibrated_inches': float(got*self.calibration/.0254)}
                    for key, base, want, got in zip(KEYS, self.baseline, target, measured)}}
        return positions, report

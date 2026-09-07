# Field bounds setup

Every Blacktop venue uses the same transform-relative field: a 30-unit local-X playable width, a 60-unit local-Z playable length, and a 2-unit non-playable safety band. Do not resize these values for a venue; vary only the surface, safety-band presentation, lighting, props, and scenery beyond the outer boundary.

Recommended scene hierarchy:

```text
FieldRoot
├── FieldBounds                 (FieldBounds component)
├── PlaySurface
├── SafetyBandVisuals
├── Environment
└── OutOfBoundsMonitors
    ├── BallMonitor             (OutOfBoundsMonitor)
    └── PlayerMonitor           (OutOfBoundsMonitor, as needed)
```

1. Create `FieldRoot` at the centre of the field. Rotate or move this root freely; do not rotate individual bounds children to compensate.
2. Add `FieldBounds` to `FieldRoot`, leaving its defaults at width **30**, length **60**, and safety band **2**. Its scene Gizmos show green legal play, amber safety space, the red outer OOB edge, a white centre line, and cyan end lines.
3. Put play surface and visual-only safety-band meshes under the root. The outer safety edge is local X ±17 and Z ±32; visual treatment can vary by location, but the gameplay boundary must not.
4. Add `OutOfBoundsMonitor` to an object beneath `OutOfBoundsMonitors`, assign `FieldRoot`'s `FieldBounds`, then assign the ball or player transform to track. It emits `EnteredSafetyBand`, `ExitedSafetyBand`, and `CrossedOuterBoundary` once per transition.
5. Subscribe in the owning gameplay system. `FieldBoundsExample` demonstrates resetting a tracked object with `GetClosestPlayablePoint`; production code can instead end a play, respawn, or update UI.

`SidelineCheck` and `TouchdownZone` are older world-axis scene logic. Do not add them to new field scenes; use `OutOfBoundsMonitor` subscribers so field placement and rotation remain correct.

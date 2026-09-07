# Field bounds setup

Every Blacktop venue uses the same transform-relative field: a 30-unit local-X playable width, a 60-unit local-Z playable length, two 10-unit end zones, and a 2-unit non-playable safety band. Do not resize these values for a venue; vary only the surface, safety-band presentation, lighting, props, and scenery beyond the outer boundary.

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
2. Add `FieldBounds` to `FieldRoot`, leaving its defaults at width **30**, length **60**, end-zone depth **10**, and safety band **2**. End zones occupy local Z **-30 to -20** and **+20 to +30**; cyan goal lines are at Z **-20** and **+20**. Gizmos show green legal play, blue end zones, amber safety space, the red outer OOB edge, and a white centre line.
3. Put play surface and visual-only safety-band meshes under the root. The outer safety edge is local X ±17 and Z ±32; visual treatment can vary by location, but the gameplay boundary must not.
4. Add `OutOfBoundsMonitor` to an object beneath `OutOfBoundsMonitors`, assign `FieldRoot`'s `FieldBounds`, then assign the ball or player transform to track. It emits `EnteredSafetyBand`, `ExitedSafetyBand`, and `CrossedOuterBoundary` once per transition.
5. Subscribe in the owning gameplay system. `FieldBoundsExample` demonstrates resetting a tracked object with `GetClosestPlayablePoint`; production code can instead end a play, respawn, or update UI.

Assign `FieldRoot`'s `FieldBounds` to the existing `TouchdownZone` component. It then scores when the ball enters either blue end zone and remains correct after moving or rotating the field. `SidelineCheck` is older world-axis scene logic; do not add it to new field scenes—use `OutOfBoundsMonitor` subscribers instead.

# Shared field geometry and venue art

`FieldConstants` owns the 30×60 legal rectangle, two internal 10-unit end zones and two-unit safety band. `FieldBounds` exposes its local-space queries and boundary Gizmos. All venues use those same dimensions.

SampleScene has one persistent identity `FieldRoot` with `FieldBounds` and `FieldLoader`; `FieldLines`, `TouchdownZone` and the existing `SidelineCheck` use those bounds. Keep the root at world origin with identity rotation/scale: football LOS/spawn/scoring still assume world X/Z. Mathematical transform-relative bounds queries do not make the whole match rotation-safe.

The original Plane's collider is retained, while FieldLoader swaps collider-free surface/detail prefabs. Add future physical wall objects under the persistent bounds root so `FieldBoundaryWall` registers there. `OutOfBoundsMonitor` only reports state transitions; the existing SidelineCheck/PlayState own match OOB rulings, so do not add duplicate end-play subscribers. No trigger callbacks or Rigidbody are required.

Select TheLot or Downtown on FieldLoader and press Apply Selected Field before playing (or between plays). For asset paths, material/tint/tiling/decorative setup, generation utility, reserved boundary/environment references and condition integration gaps, see [FIELD_SURFACES.md](../../../Docs/FIELD_SURFACES.md).

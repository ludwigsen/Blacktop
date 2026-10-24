# Modular field surfaces — Phase 1

## Audit and ownership

Baseline: master `566af27b3b8c930007f3def0d6acfd98d10198c5`. The feature is isolated from the owner's uncommitted animation/football changes.

- `Assets/Scenes/SampleScene.unity` originally had one origin-centred built-in Plane (MeshFilter/MeshCollider mesh fileID 10209), transform scale (3,1,6), identity rotation, Y=0. Its serialized object/component IDs and collider remain. Only the MeshRenderer is disabled after a venue successfully loads.
- `Assets/_Project/Scripts/Field/FieldConstants.cs` owns width 30, length 60, end-zone depth 10 and safety band 2. FieldBounds formerly duplicated editable dimensions; its queries now derive from those constants, so venue geometry cannot drift through Inspector overrides.
- `FieldBounds` queries local X/Z. Legal X is [-15,15], Z [-30,30]; near/far end zones are [-30,-20] / [20,30]. Safety limits are X ±17 / Z ±32. The safety band is not legal scoring space.
- The baseline scene had no FieldBounds, so PlayState/SidelineCheck used their immediate-OOB world-axis fallback. Wiring one origin/identity FieldBounds activates their existing safety-band path. Held/loose balls die beyond the outer band unless a registered wall contains them. No ruling algorithms changed. This is the intentional scene behavior change.
- `PlayState` still owns validated scoring, possession direction, LOS, whistle spots, downs and reset. `TouchdownZone` requests its held-ball ruling. All football systems still use their existing world-coordinate assumptions. Phase 1 explicitly rejects moved/rotated/scaled FieldLoader roots.
- `FieldBoundaryWall` still registers with ancestor bounds and uses collider containment; `OutOfBoundsMonitor` remains an optional transition reporter. SidelineCheck remains the actual match-ruling poller. Do not add another OOB subscriber that ends plays.
- Existing `FieldLines` builds independent dynamic LOS/first-down and static goal-line quads at Y=0.02. It now has an explicit serialized reference to the single scene bounds. Paint is Y=0.008, parking/wear Y=0.004, repairs Y=0.003, surface Y=0. Decorative transparent materials queue before gameplay overlays. No decal-renderer feature, Shader Graph or package dependency is needed.
- URP PC renderer uses deferred rendering and SSAO. Existing retained URP Unlit supports runtime gameplay lines. Venue ground/paint use serialized URP Lit assets; normal-map support uses the standard material slots.

## Assets and workflow

The scene is already wired; no setup action is required after pulling the feature.

- Definitions: `Assets/_Project/Data/Fields/TheLot.asset`, `Downtown.asset`.
- Shared surface: `Assets/_Project/Prefabs/Fields/SharedSurface.prefab` (four upward-facing vertices, UV 0–1, 30×60, zero colliders).
- Decoration: `Assets/_Project/Prefabs/Fields/LotDetails.prefab`, `DowntownDetails.prefab`.
- Placeholder materials, color/normal textures and mesh assets: `Assets/_Project/Art/Fields/Generated/`. These are deliberately generated placeholders, separate from future production art.
- Runtime components: `Assets/_Project/Scripts/Field/FieldDefinition.cs`, `FieldSurface.cs`, `FieldLoader.cs`.
- Editor utility: `Assets/_Project/Scripts/Editor/FieldSurfaceSetup.cs`, inside the existing Blacktop.Editor assembly. No new assembly dependency.

1. Open SampleScene, select FieldRoot, choose **Selected Field** (TheLot or Downtown) and press **Apply Selected Field**. Save the scene to retain the development choice. A blank selection falls back to The Lot. Available Fields is the small development catalog for future UI; it does not apply conditions.
2. Enter Play Mode, call plays and play football. Between plays the same Inspector apply button or `SelectField(definition)` can swap the visuals without restarting Unity. Live play, post-play presentation and game-over swaps return false. Play Mode changes are temporary, following normal Unity serialization behavior.
3. To add a venue, use Create > Blacktop > Field Definition. Supply a stable ID/name, assign SharedSurface and a URP Lit **Base Material**. No geometry or football logic belongs in a definition. Only two functional definitions ship; the other nine venues have no placeholder environments.
4. Edit a material's Base Map, Normal Map, smoothness and texture slots in the material Inspector. Enable **Use Tint** in the definition for an optional color multiplier. **Texture Tiling** multiplies the base material's scale, including its normal-map scale. Defaults repeat a two-unit tile (15×30 repetitions). Renderer overrides use MaterialPropertyBlock; they neither clone nor modify the shared material. Unity may retain the prefab material when an optional base material is absent.
5. For local decorative marks, create a prefab of MeshFilters/MeshRenderers only; assign it as **Detail Prefab**. Duplicate/edit the provided detail prefabs for a starting point. Place meshes under Y=0.02 and above Y=0. Use transparent Lit materials with ZWrite off and render queue 2990. Assign optional wear masks in the detail material's Base Map/alpha slot. Never add colliders, Rigidbody, or gameplay components to decorative prefabs; the loader rejects them.
6. Boundary and environment prefab fields are reserved references only in Phase 1; they are not instantiated. Later physical boundaries belong under the persistent FieldBounds root, with FieldBoundaryWall registering there. Scenery can be introduced separately without copying gameplay managers. Do not put FieldBounds inside a surface prefab.
7. Blacktop > Fields > **Generate Missing Phase 1 Assets** creates missing placeholders through Unity APIs. Existing assets and their Inspector edits are preserved. **Wire SampleScene (Save Scene)** explicitly opens/saves the shipped scene, chooses The Lot, finds its existing ground by footprint/collider rather than name and wires references. Use this only for initial wiring/recovery, after saving other work. It refuses ambiguous ground or pre-existing unmanaged bounds. It is not an automatic scene replacement.

Missing optional detail/material references are supported. Missing required surface prefab or invalid art leaves the current venue intact and returns false. If neither selected nor default definition exists, the legacy Plane remains the initial fallback. The loader rejects duplicate active bounds; it does not silently delete another system's root.

## Conditions: independent match integration

FieldDefinition remains venue art/identity only. Head 2 Head and Pickup independently select conditions through FrontEndSettings' committed ConditionData catalog. See FRONT_END.md for the complete calculation, acceleration ownership, profiles and lifecycle.

| Condition | Regular severity |
|---|---|
| Clear | No modifiers |
| Cold | Agility -10%, Run Power +10% |
| Slick | Agility -10%, Acceleration -10%, Speed +10% |
| Loose | Speed -10%, Acceleration -10%, Carrying +10% |
| Raw | Carrying -10%, Run Power +10% |

Clean ×0.5, Regular ×1, Hard ×1.8. At match review, MatchModifiers converts ConditionData percentage points to fractions once, sums like targets, bounds environmental additions to [-0.30,+0.30], and freezes the result. CharacterApplier binds it to both sides' runtime PlayerAttributes. Effective applies `(base curve + condition) × Gamebreaker`, then existing hard caps; no additional consumer multiplier or FieldLoader effect exists. Clear always supplies zero additions and ignores severity. Legacy standalone FieldModifiers remains a compatibility authoring type; a bound match takes precedence rather than stacking it twice.

Power maps to RunPower and Carrying to Carrying. Acceleration keeps the Agility-owned base curve but uses only the independent Accel condition addition at PlayerAttributes.Acceleration. Agility's condition effect is reserved for its own moves, so Slick never charges both penalties to one effect. Controlled carrier and defender movement consume acceleration. Constant-velocity AI uses its effective Speed once and does not invent an acceleration stat or double its speed penalty. AI routes, blockers, CPU carriers/passing and handoff movement now use the existing PlayerAttributes path, including one Gamebreaker boost.

Field selection filters invalid/missing art and definitions explicitly marked unplayable. Both shipped functional venues remain playable despite provisional visual art. Random cannot choose unimplemented venues. Match rematch retains resolved choices; return to menu discards match-scoped modifiers. Direct scene entry retains the zero-condition baseline.

## Validation

See TESTING.md for the recorded automated result and remaining manual checks. Placeholder visual quality, physical-controller feel and Windows validation are separate from bounds/material/overlay assertions. Detailed scenery and saved menu preferences remain deferred. Exhibition venue/condition selection and runtime condition integration are now implemented.

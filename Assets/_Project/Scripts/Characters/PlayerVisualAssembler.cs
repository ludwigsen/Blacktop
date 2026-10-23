using System;
using System.Collections.Generic;
using UnityEngine;

// Presentation only. Start sees CharacterApplier's GUID-resolved row and PlayState's branding.
[DisallowMultipleComponent]
public class PlayerVisualAssembler : MonoBehaviour
{
    public PlayerAppearanceCatalog catalog;
    public PlayerAppearanceDefinition defaultAppearance;
    public PlayerVisualRig rig;
    public PlayerAppearanceDefinition AppliedAppearance { get; private set; }
    public string LastDiagnostics { get; private set; } = "Not assembled.";
    public bool HasPlaceholders { get; private set; }
    public Vector3 NameplateOffset => AppliedAppearance != null ? AppliedAppearance.heightProfile.nameplateOffset : Vector3.zero;
    public Vector3 CameraOffset => AppliedAppearance != null ? AppliedAppearance.heightProfile.cameraOffset : Vector3.zero;

    readonly List<(Renderer renderer, CharacterPartDefinition part)> tinted = new();
    string signature;
    Transform nameplate;
    Vector3 nameplateBasePosition;
    MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        var record = GetComponent<CharacterApplier>()?.Record;
        var appearance = record.HasValue && record.Value.appearance != null ? record.Value.appearance : defaultAppearance;
        var member = GetComponent<TeamMember>();
        var team = member != null && PlayState.Instance != null ? PlayState.Instance.IdentityFor(member.teamId) : null;
        if (!TryApply(appearance, team, record?.jerseyNumber ?? 0))
        {
            Debug.LogWarning($"[PlayerVisualAssembler] {name}: {LastDiagnostics}", this);
            // An invalid saved selection may fall back; an invalid default retains the original model.
            if (appearance != defaultAppearance && defaultAppearance != null) TryApply(defaultAppearance, team, record?.jerseyNumber ?? 0);
        }
    }

    public bool TryApply(PlayerAppearanceDefinition appearance, TeamIdentity team = null, int jerseyNumber = 0, bool forceRebuild = false)
    {
        if (catalog == null || rig == null || rig.transform == transform || !rig.transform.IsChildOf(transform))
        { LastDiagnostics = "Assign a catalog and a separate child PlayerVisualRig."; return false; }
        if (!rig.ValidateRig(out string rigError)) { LastDiagnostics = rigError; return false; }
        var errors = catalog.Validate(appearance, out var parts);
        if (errors.Count != 0) { LastDiagnostics = string.Join("\n", errors); return false; }
        rig.Initialize();
        foreach (var part in parts)
        {
            if (part.placeholder || part.emptyOption) continue;
            if (part.skeleton != rig.skeleton)
            { LastDiagnostics = $"{part.id}: skeleton contract differs from the adapter."; return false; }
            if (part.useExistingBody) continue;
            if (part.skinnedSource != null)
            {
                var sources = new List<SkinnedMeshRenderer> { part.skinnedSource };
                foreach (var lod in part.lowerDetailLevels) sources.Add(lod.source);
                foreach (var source in sources)
                foreach (var bone in rig.Remap(source))
                    if (bone == null) { LastDiagnostics = $"{part.id}: target skeleton is missing a bone."; return false; }
            }
            else if (rig.animator.GetBoneTransform(part.socket) == null)
            { LastDiagnostics = $"{part.id}: target skeleton is missing socket {part.socket}."; return false; }
        }

        var body = parts[0];
        if (body.useExistingBody && !string.IsNullOrEmpty(appearance.heightProfile.proportionCalibrationId))
        { LastDiagnostics = "The existing prototype has no authored limb-proportion calibration."; return false; }
        string nextSignature = appearance.GetEntityId() + ":" + appearance.heightProfile.GetEntityId() + ":" +
            appearance.heightProfile.Centimeters + ":" + appearance.headScale + ":" + string.Join("|", Array.ConvertAll(parts, p => p.id));
        if (signature != nextSignature || forceRebuild)
        {
            // Build only after preflight; old visuals remain intact for missing/incompatible art.
            ClearParts();
            rig.RestoreBody();
            tinted.Clear();
            if (!body.placeholder && !body.useExistingBody) CopySkin(body.skinnedSource, rig.bodyRenderer);
            tinted.Add((rig.bodyRenderer, body));
            if (!body.placeholder && !body.useExistingBody) ConfigureLods(body, rig.bodyRenderer);
            foreach (var renderer in rig.integratedHeadRenderers) if (renderer != null) renderer.enabled = body.integratedHead;
            for (int i = 1; i < parts.Length; i++) AddPart(parts[i]);
            signature = nextSignature;
        }
        rig.ApplyHeight(appearance.heightProfile, appearance.headScale);
        AppliedAppearance = appearance;
        if (nameplate == null)
        {
            nameplate = transform.Find("Nameplate");
            if (nameplate != null) nameplateBasePosition = nameplate.localPosition;
        }
        if (nameplate != null) nameplate.localPosition = nameplateBasePosition + NameplateOffset;
        HasPlaceholders = Array.Exists(parts, p => p.placeholder);
        LastDiagnostics = HasPlaceholders ? "PLACEHOLDER selections: prototype geometry retained / unavailable attachments omitted." : "Appearance assembled.";
        ApplyTeam(team, jerseyNumber);
        return true;
    }

    void AddPart(CharacterPartDefinition part)
    {
        if (part.placeholder || part.emptyOption) return;
        if (part.skinnedSource != null)
        {
            // Modular skins share the live skeleton; no duplicate Animator or bone hierarchy.
            var go = new GameObject(part.id, typeof(SkinnedMeshRenderer));
            go.transform.SetParent(rig.bodyRenderer.transform.parent, false);
            go.transform.SetLocalPositionAndRotation(rig.bodyRenderer.transform.localPosition, rig.bodyRenderer.transform.localRotation);
            go.transform.localScale = rig.bodyRenderer.transform.localScale;
            var skin = go.GetComponent<SkinnedMeshRenderer>();
            CopySkin(part.skinnedSource, skin);
            generatedSkins.Add(go);
            tinted.Add((skin, part));
            ConfigureLods(part, skin);
        }
        else
        {
            var go = Instantiate(part.attachmentPrefab, rig.animator.GetBoneTransform(part.socket), false);
            go.name = part.id;
            go.transform.SetLocalPositionAndRotation(part.localPosition, Quaternion.Euler(part.localEulerAngles));
            go.transform.localScale = part.localScale;
            generatedSkins.Add(go);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) tinted.Add((renderer, part));
        }
    }

    void CopySkin(SkinnedMeshRenderer source, SkinnedMeshRenderer destination)
    {
        destination.sharedMesh = source.sharedMesh;
        destination.sharedMaterials = source.sharedMaterials;
        destination.bones = rig.Remap(source);
        var sourceRoot = source.GetComponentInParent<Animator>(true).transform;
        destination.rootBone = source.rootBone != null ? rig.Bone(CharacterSkeletonDefinition.Path(source.rootBone, sourceRoot)) : rig.animator.transform;
        destination.localBounds = source.localBounds;
        destination.quality = source.quality;
        destination.shadowCastingMode = source.shadowCastingMode;
        destination.receiveShadows = source.receiveShadows;
    }

    LODGroup bodyLodGroup;
    void ConfigureLods(CharacterPartDefinition part, SkinnedMeshRenderer primary)
    {
        if (part.lowerDetailLevels.Length == 0) return;
        // Artists supply the LOD skins; assembly only maps their bones and switch thresholds.
        var group = primary == rig.bodyRenderer && bodyLodGroup != null ? bodyLodGroup : primary.gameObject.AddComponent<LODGroup>();
        group.enabled = true;
        if (primary == rig.bodyRenderer) bodyLodGroup = group;
        var levels = new LOD[part.lowerDetailLevels.Length + 1];
        levels[0] = new LOD(part.lod0ScreenHeight, new Renderer[] { primary });
        for (int i = 0; i < part.lowerDetailLevels.Length; i++)
        {
            var source = part.lowerDetailLevels[i];
            var go = new GameObject(part.id + ".LOD" + (i + 1), typeof(SkinnedMeshRenderer));
            go.transform.SetParent(primary.transform, false);
            var skin = go.GetComponent<SkinnedMeshRenderer>();
            CopySkin(source.source, skin);
            tinted.Add((skin, part));
            generatedSkins.Add(go);
            levels[i + 1] = new LOD(source.screenHeight, new Renderer[] { skin });
        }
        group.SetLODs(levels);
        group.RecalculateBounds();
    }

    public void ApplyTeam(TeamIdentity team, int jerseyNumber)
    {
        if (AppliedAppearance == null) return;
        block ??= new MaterialPropertyBlock();
        catalog.TryColor(catalog.skinTones, AppliedAppearance.skinToneId, out var skin);
        catalog.TryColor(catalog.hairColors, AppliedAppearance.hairColorId, out var hair);
        foreach (var entry in tinted)
        {
            for (int slot = 0; slot < entry.part.materialTints.Length; slot++)
            {
                var role = entry.part.materialTints[slot];
                if (role == CharacterTint.None) continue;
                entry.renderer.GetPropertyBlock(block, slot);
                Color color = role switch { CharacterTint.Skin => skin, CharacterTint.Hair => hair,
                    CharacterTint.TeamPrimary => team != null ? team.accentColor : Color.white,
                    _ => team != null ? team.secondaryAccentColor : Color.white };
                block.SetColor(BaseColor, color);
                entry.renderer.SetPropertyBlock(block, slot);
            }
        }
        foreach (var label in rig.jerseyLabels) if (label != null) label.text = jerseyNumber.ToString();
        // Reassignment refreshes colors/number without rebuilding mesh or material assets.
    }

    readonly List<GameObject> generatedSkins = new();
    void ClearParts()
    {
        if (bodyLodGroup != null)
        {
            bodyLodGroup.enabled = false;
            bodyLodGroup.SetLODs(Array.Empty<LOD>());
            rig.bodyRenderer.enabled = true;
        }
        foreach (var go in generatedSkins) Dispose(go);
        generatedSkins.Clear();
    }

    static void Dispose(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false); // Destroy is deferred in Play Mode; avoid one frame of overlapping art.
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    void OnDestroy() => ClearParts();
}

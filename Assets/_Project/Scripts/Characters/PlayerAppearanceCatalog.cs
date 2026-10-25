using System;
using System.Collections.Generic;
using UnityEngine;

// One small shared catalog, referenced explicitly by the player prefab (no Resources scan).
[CreateAssetMenu(menuName = "Blacktop/Characters/Appearance Catalog")]
public class PlayerAppearanceCatalog : ScriptableObject
{
    public CharacterPartDefinition[] parts = Array.Empty<CharacterPartDefinition>();
    public PlayerHeightProfile[] heightProfiles = Array.Empty<PlayerHeightProfile>();
    public PlayerAppearanceDefinition[] sampleAppearances = Array.Empty<PlayerAppearanceDefinition>();
    public AppearanceColor[] skinTones = Array.Empty<AppearanceColor>();
    public AppearanceColor[] hairColors = Array.Empty<AppearanceColor>();

    public CharacterPartDefinition Find(string id) => Array.Find(parts, p => p != null && p.id == id);

    public bool TryColor(AppearanceColor[] palette, string id, out Color color)
    {
        foreach (var entry in palette)
            if (entry.id == id) { color = entry.color; return true; }
        color = Color.white;
        return false;
    }

    public List<string> ValidateCatalog()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (part == null) { errors.Add("Catalog contains a missing part reference."); continue; }
            if (string.IsNullOrWhiteSpace(part.id) || !ids.Add(part.id)) errors.Add($"Missing/duplicate part ID: {part.id}.");
            if (!Enum.IsDefined(typeof(CharacterPartSlot), part.slot)) errors.Add($"{part.id}: unknown part slot.");
            foreach (var tint in part.materialTints)
                if (!Enum.IsDefined(typeof(CharacterTint), tint)) errors.Add($"{part.id}: unknown tint role.");
            ValidateArt(part, errors);
        }
        foreach (var palette in new[] { skinTones, hairColors })
        {
            ids.Clear();
            foreach (var entry in palette)
                if (string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id)) errors.Add($"Missing/duplicate palette ID: {entry.id}.");
        }
        ids.Clear();
        foreach (var profile in heightProfiles)
            if (profile == null || string.IsNullOrWhiteSpace(profile.id) || !ids.Add(profile.id) ||
                !Enum.IsDefined(typeof(PlayerHeight), profile.height)) errors.Add("Missing, duplicate or unsupported height profile.");
        ids.Clear();
        foreach (var appearance in sampleAppearances)
            if (appearance == null || string.IsNullOrWhiteSpace(appearance.id) || !ids.Add(appearance.id)) errors.Add("Missing/duplicate sample appearance ID.");
        return errors;
    }

    public List<string> Validate(PlayerAppearanceDefinition appearance, out CharacterPartDefinition[] selected, bool validateCatalog = true)
    {
        var errors = validateCatalog ? ValidateCatalog() : new List<string>();
        var resolved = new List<CharacterPartDefinition>();
        selected = Array.Empty<CharacterPartDefinition>();
        if (appearance == null) { errors.Add("Appearance is missing."); return errors; }
        if (string.IsNullOrWhiteSpace(appearance.id)) errors.Add("Appearance ID is missing.");
        if (appearance.heightProfile == null || string.IsNullOrWhiteSpace(appearance.heightProfile.id) ||
            !Enum.IsDefined(typeof(PlayerHeight), appearance.heightProfile.height)) errors.Add("Select a supported discrete height profile.");
        if (!Enum.IsDefined(typeof(BodyArchetype), appearance.bodyArchetype)) errors.Add("Unknown body archetype.");
        if (appearance.headScale < 1f || appearance.headScale > 1.05f || float.IsNaN(appearance.headScale)) errors.Add("Head scale must be between 1 and 1.05.");
        if (!TryColor(skinTones, appearance.skinToneId, out _)) errors.Add($"Unknown skin tone {appearance.skinToneId}.");
        if (!TryColor(hairColors, appearance.hairColorId, out _)) errors.Add($"Unknown hair color {appearance.hairColorId}.");

        Add(appearance.bodyId, CharacterPartSlot.Body);
        Add(appearance.faceId, CharacterPartSlot.Face);
        Add(appearance.hairId, CharacterPartSlot.Hair);
        Add(appearance.facialHairId, CharacterPartSlot.FacialHair);
        foreach (var id in appearance.clothingIds) Add(id, null, true);
        foreach (var id in appearance.accessoryIds) Add(id, null, false);
        var body = Find(appearance.bodyId);
        var face = Find(appearance.faceId);
        if (body != null && body.bodyArchetype != appearance.bodyArchetype) errors.Add("Body ID and body archetype disagree.");
        foreach (var part in resolved)
        {
            if (part.supportedBodies.Length > 0 && Array.IndexOf(part.supportedBodies, appearance.bodyArchetype) < 0)
                errors.Add($"{part.id} does not support {appearance.bodyArchetype}.");
            if (part.compatibleBodyIds.Length > 0 && Array.IndexOf(part.compatibleBodyIds, appearance.bodyId) < 0)
                errors.Add($"{part.id} does not fit body {appearance.bodyId}.");
            // Legacy shells cannot be assumed to fit generated volume distributions.
            if (body != null && body.variantFamilyId == "body.parametric.v1" && part.slot >= CharacterPartSlot.Top &&
                part.slot <= CharacterPartSlot.Footwear && !part.placeholder && Array.IndexOf(part.compatibleBodyIds, appearance.bodyId) < 0)
                errors.Add($"{part.id} needs a generated garment variant for {appearance.bodyId}.");
            if (part.compatibleFaceIds.Length > 0 && Array.IndexOf(part.compatibleFaceIds, appearance.faceId) < 0)
                errors.Add($"{part.id} does not fit {appearance.faceId}.");
            if (part.supportedScalpProfiles.Length > 0 && (face == null || Array.IndexOf(part.supportedScalpProfiles, face.scalpProfileId) < 0))
                errors.Add($"{part.id} requires a compatible scalp profile.");
            if (part.slot == CharacterPartSlot.Face && !part.placeholder && !part.emptyOption && body != null && body.integratedHead)
                errors.Add("Separate heads require a headless body; the prototype has an integrated head.");
            string calibration = appearance.heightProfile != null ? appearance.heightProfile.proportionCalibrationId : "";
            if (!part.placeholder && !part.emptyOption && (part.proportionCalibrationId ?? "") != (calibration ?? ""))
                errors.Add($"{part.id} does not match height proportion calibration '{calibration}'.");
        }
        selected = resolved.ToArray();
        return errors;

        void Add(string id, CharacterPartSlot? slot, bool clothing = false)
        {
            var part = Find(id);
            if (part == null) { errors.Add($"Unknown part ID '{id}'."); return; }
            bool isClothing = part.slot >= CharacterPartSlot.Top && part.slot <= CharacterPartSlot.Footwear;
            if (slot.HasValue ? part.slot != slot.Value : clothing ? !isClothing : part.slot < CharacterPartSlot.Wristband)
                errors.Add($"{id} is in the wrong appearance slot.");
            if (resolved.Contains(part)) errors.Add($"Duplicate selection {id}.");
            if (isClothing && resolved.Exists(p => p.slot == part.slot)) errors.Add($"Multiple garments in {part.slot}.");
            resolved.Add(part);
        }
    }

    static void ValidateArt(CharacterPartDefinition part, List<string> errors)
    {
        // Explicit placeholders are valid data slots, never evidence of completed art.
        if (part.placeholder || part.emptyOption) return;
        if (part.useExistingBody)
        {
            if (part.slot != CharacterPartSlot.Body) errors.Add($"{part.id}: existing-body mode is only valid for bodies.");
            if (part.lowerDetailLevels.Length > 0) errors.Add($"{part.id}: authored LODs require an authored body skin.");
            if (part.skeleton == null) errors.Add($"{part.id}: missing prototype skeleton contract.");
            else if (!part.skeleton.ValidateSkin(part.skeleton.referenceSkin, out var error)) errors.Add($"{part.id}: {error}");
            return;
        }
        if ((part.skinnedSource != null) == (part.attachmentPrefab != null))
        { errors.Add($"{part.id}: choose exactly one skin or rigid attachment."); return; }
        if (part.skinnedSource != null)
        {
            if (part.skeleton == null) errors.Add($"{part.id}: missing skeleton contract.");
            else if (!part.skeleton.ValidateSkin(part.skinnedSource, out string error, part.skinnedMeshOverride)) errors.Add($"{part.id}: {error}");
            if (part.materialTints.Length > part.skinnedSource.sharedMaterials.Length) errors.Add($"{part.id}: tint slot exceeds material slots.");
            float previous = part.lod0ScreenHeight;
            foreach (var lod in part.lowerDetailLevels)
            {
                if (lod.screenHeight <= 0f || lod.screenHeight >= previous) errors.Add($"{part.id}: LOD transitions must descend above zero.");
                previous = lod.screenHeight;
                if (part.skeleton != null && !part.skeleton.ValidateSkin(lod.source, out var error)) errors.Add($"{part.id} LOD: {error}");
                if (lod.source != null && part.materialTints.Length > lod.source.sharedMaterials.Length) errors.Add($"{part.id}: LOD tint slots exceed materials.");
            }
        }
        else
        {
            if (part.slot == CharacterPartSlot.Body) errors.Add($"{part.id}: body must be skinned.");
            if (!Enum.IsDefined(typeof(HumanBodyBones), part.socket) || part.socket == HumanBodyBones.LastBone) errors.Add($"{part.id}: invalid socket.");
            if (part.attachmentPrefab.GetComponentsInChildren<Renderer>(true).Length == 0) errors.Add($"{part.id}: rigid attachment has no renderers.");
            foreach (var component in part.attachmentPrefab.GetComponentsInChildren<Component>(true))
                if (!(component is Transform || component is MeshFilter || component is MeshRenderer || component is LODGroup))
                    errors.Add($"{part.id}: attachment contains unsupported component {component?.GetType().Name ?? "Missing Script"}.");
            foreach (var renderer in part.attachmentPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<MeshFilter>()?.sharedMesh == null) errors.Add($"{part.id}: rigid renderer has no mesh.");
                if (part.materialTints.Length > renderer.sharedMaterials.Length) errors.Add($"{part.id}: rigid tint slots exceed materials.");
                foreach (var material in renderer.sharedMaterials)
                    if (material == null) errors.Add($"{part.id}: rigid renderer has a missing material.");
            }
        }
    }
}

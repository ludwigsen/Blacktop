using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Additive migration: legacy mesh assets, catalog IDs and saved appearances remain valid fallbacks.
public static class GeneratedBodySetup
{
    public const string MeshFolder = "Assets/_Project/Art/Characters/Generated/v1";
    public const string DataFolder = "Assets/_Project/Data/Characters/Generated/v1";
    public const string Family = "body.parametric.v1";
    public static readonly string[] Names = { "Lean", "Athletic", "Built", "Thick", "Heavy", "Massive" };
    [Serializable] public class Targets { public int version = 1; public Target[] meshes; }
    [Serializable] public class Target { public string name, targetAsset, targetGuid; }

    public static bool IsTarget(string path) => Names.SelectMany(n => new[] { "Body_", "Top_", "Shorts_", "Footwear_" }.Select(p => MeshFolder + "/" + p + n + ".asset"))
        .Append(MeshFolder + "/Head_Prototype.asset").Contains(path, StringComparer.Ordinal);

    [MenuItem("Blacktop/Characters/Body Generator/1 Prepare Export Targets")]
    public static void Prepare()
    {
        Directory.CreateDirectory(MeshFolder); Directory.CreateDirectory(DataFolder); AssetDatabase.Refresh();
        var contract = AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>(CharacterAppearanceSetup.Data + "/CanonicalSkeleton.asset");
        var names = Names.SelectMany(n => new[] { "Body_", "Top_", "Shorts_", "Footwear_" }.Select(p => p + n)).Append("Head_Prototype");
        var targets = new List<Target>();
        foreach (string name in names)
        {
            string path = MeshFolder + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) == null)
            {
                // Seed only new targets with canonical bind poses; Blender subsequently replaces surface data.
                var mesh = FrontEndTemporaryArt.ReadableCopy(contract.referenceSkin.sharedMesh);
                mesh.name = name; AssetDatabase.CreateAsset(mesh, path);
            }
            targets.Add(new Target { name = name, targetAsset = path, targetGuid = AssetDatabase.AssetPathToGUID(path) });
        }
        File.WriteAllText("SourceAssets/Characters/BodyGenerator/unity-targets.json", JsonUtility.ToJson(new Targets { meshes = targets.ToArray() }, true));
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Blacktop/Characters/Body Generator/2 Register Validated Bodies")]
    public static void Register()
    {
        var contract = AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>(CharacterAppearanceSetup.Data + "/CanonicalSkeleton.asset");
        var meshes = Names.SelectMany(n => new[] { "Body_", "Top_", "Shorts_", "Footwear_" }.Select(p => p + n)).Append("Head_Prototype")
            .ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + n + ".asset"));
        foreach (var pair in meshes)
        {
            if (pair.Value == null || pair.Value.vertexCount == 0 || !pair.Value.bindposes.SequenceEqual(contract.referenceSkin.sharedMesh.bindposes))
                throw new InvalidDataException("Import all generated meshes before registering: " + pair.Key);
            if (pair.Key.StartsWith("Body_", StringComparison.Ordinal) && pair.Value.bounds.max.y > 1.5151f ||
                pair.Key == "Head_Prototype" && pair.Value.bounds.min.y < 1.5149f)
                throw new InvalidDataException("Headless body / neck interface validation failed: " + pair.Key);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerAppearanceCatalog>(FrontEndSetup.Data + "/FrontEndCatalog.asset");
        var parts = catalog.parts.ToList();
        var face = Part("Head_Prototype", "face.prototype.parametric.v1", CharacterPartSlot.Face, BodyArchetype.Athletic);
        face.usePrototypeEyes = true; face.scalpProfileId = "prototype";
        foreach (string name in Names)
        {
            var build = (BodyArchetype)Enum.Parse(typeof(BodyArchetype), name);
            var body = Part("Body_" + name, "body." + name.ToLowerInvariant() + ".parametric.v1", CharacterPartSlot.Body, build);
            body.compatibleFaceIds = new[] { face.id };
            foreach (var slot in new[] { CharacterPartSlot.Top, CharacterPartSlot.Shorts, CharacterPartSlot.Footwear })
            {
                var part = Part(slot + "_" + name, "gear." + slot.ToString().ToLowerInvariant() + "." + name.ToLowerInvariant() + ".parametric.v1", slot, build);
                part.supportedBodies = new[] { build }; part.compatibleBodyIds = new[] { body.id };
                part.variantFamilyId = "parametric." + slot;
                part.materialTints = new[] { slot == CharacterPartSlot.Shorts ? CharacterTint.TeamSecondary : CharacterTint.TeamPrimary };
                EditorUtility.SetDirty(part);
            }
            EditorUtility.SetDirty(body);
        }
        catalog.parts = parts.ToArray(); EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(face);
        var weights = AssetDatabase.LoadAssetAtPath<BodyWeightProfile>(FrontEndSetup.Data + "/BodyWeights.asset");
        if (!weights.entries.Any(e => e.build == BodyArchetype.Built))
        {
            // Display-only estimate; no rating, collider, roster or existing weight entry changes.
            weights.entries = weights.entries.Append(new BodyWeightEntry { build = BodyArchetype.Built, poundsAt183cm = 205f,
                heightExponent = 2.5f, poundLimits = new Vector2(120f, 390f) }).ToArray();
            EditorUtility.SetDirty(weights);
        }
        var errors = catalog.ValidateCatalog();
        if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        AssetDatabase.SaveAssets();

        CharacterPartDefinition Part(string meshName, string id, CharacterPartSlot slot, BodyArchetype build)
        {
            string path = DataFolder + "/" + meshName + ".asset";
            var part = AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>(path);
            if (part == null) { part = ScriptableObject.CreateInstance<CharacterPartDefinition>(); AssetDatabase.CreateAsset(part, path); }
            part.id = id; part.displayName = meshName.Replace('_', ' ') + " (parametric prototype)";
            part.slot = slot; part.bodyArchetype = build; part.skeleton = contract; part.skinnedSource = contract.referenceSkin;
            part.skinnedMeshOverride = meshes[meshName]; part.integratedHead = false; part.temporaryVisual = true;
            part.variantFamilyId = slot == CharacterPartSlot.Body ? Family : part.variantFamilyId;
            part.materialTints = new[] { CharacterTint.Skin };
            part.authoringNote = "Generated v1 prototype; canonical rig/UVs retained. Shell clothing and unavailable action clips need artist acceptance.";
            if (!parts.Contains(part)) parts.Add(part);
            return part;
        }
    }
}

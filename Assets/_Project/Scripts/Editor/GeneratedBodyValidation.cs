using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Reproducible real importer checks; invalid packets must fail before touching any target mesh.
public static class GeneratedBodyValidation
{
    public static void ImportTapered() => Import("Builds/CharacterExports/TaperedBodies.json", false);
    public static void ImportAll() => Import("Builds/CharacterExports/GeneratedBodies.json", true);
    static void Import(string path, bool register)
    {
        var packet = JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(path));
        var before = packet.meshes.ToDictionary(r => r.targetAsset, r => AssetDatabase.LoadAssetAtPath<Mesh>(r.targetAsset).bindposes);
        CharacterBlenderBridge.Apply(path);
        foreach (var row in packet.meshes)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(row.targetAsset);
            Require(AssetDatabase.AssetPathToGUID(row.targetAsset) == row.targetGuid, "GUID changed.");
            Require(mesh.bindposes.SequenceEqual(before[row.targetAsset]), "Bind poses changed.");
            Require(mesh.triangles.Length == row.triangles.Length, "Triangle count changed.");
            // Compare every exported triangle corner, including any UV/normal seam vertex splits.
            var vertices = mesh.vertices; var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i++)
            {
                int k = row.triangles[i] * 3;
                Require(Vector3.Distance(vertices[indices[i]], new Vector3(row.positions[k], row.positions[k + 1], row.positions[k + 2])) < .000001f, "Imported geometry differs.");
            }
        }
        var snapshots = packet.meshes.ToDictionary(r => r.targetAsset, r => EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<Mesh>(r.targetAsset)));
        string invalid = "Builds/Validation/BodyGenerator/invalid-unity.json";
        foreach (string kind in new[] { "weights", "guid", "path", "bones", "rest", "parent", "version" })
        {
            var bad = JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(path));
            // Corrupt a later row after a valid earlier row would have been staged.
            bad.meshes[0].positions[0] += .01f;
            switch (kind)
            {
                case "weights": bad.meshes.Last().weights[0] = -1; break;
                case "guid": bad.meshes.Last().targetGuid = "bad"; break;
                case "path": bad.meshes.Last().targetAsset = "Assets/../outside.asset"; break;
                case "bones": bad.meshes.Last().boneNames[0] = "bad"; break;
                case "rest": bad.bones[0].matrix[12] += .01f; break;
                case "parent": bad.bones.Last().parent = "Root"; break;
                case "version": bad.generator = "blacktop.body.unknown"; break;
            }
            File.WriteAllText(invalid, JsonUtility.ToJson(bad)); bool rejected = false;
            try { CharacterBlenderBridge.Apply(invalid); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Invalid packet accepted: " + kind);
            foreach (var snapshot in snapshots)
                Require(EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<Mesh>(snapshot.Key)) == snapshot.Value, "Partial write after invalid " + kind);
        }
        if (register) GeneratedBodySetup.Register();
        Directory.CreateDirectory("Builds/Validation/BodyGenerator");
        File.WriteAllText("Builds/Validation/BodyGenerator/" + (register ? "unity-all.txt" : "unity-tapered.txt"),
            $"PASS: {packet.meshes.Length} real meshes imported; triangle geometry, GUIDs and bind poses retained; seven malformed packets rejected without partial writes.\n");
        Debug.Log("GENERATED_BODY_IMPORT_PASS " + packet.meshes.Length);
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}

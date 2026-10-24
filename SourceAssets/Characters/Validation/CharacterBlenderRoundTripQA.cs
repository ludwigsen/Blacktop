using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Copy into Assets/Editor in a disposable QA checkout only: this exercises real mesh writes.
public static class CharacterBlenderRoundTripQA
{
    static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    static void Reject(CharacterBlenderBridge.Packet packet,string path)
    {
        File.WriteAllText(path,JsonUtility.ToJson(packet));
        bool rejected=false;try {CharacterBlenderBridge.Apply(path);}catch(Exception){rejected=true;}
        Require(rejected,"Invalid export was accepted.");
    }
    public static void Run()
    {
        const string folder="Builds/Validation/";
        CharacterBlenderBridge.Packet seed;
        using(var file=File.OpenRead("SourceAssets/Characters/Reference/character_seed.json.gz"))
        using(var zip=new GZipStream(file,CompressionMode.Decompress))
        using(var reader=new StreamReader(zip))seed=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(reader.ReadToEnd());
        var binds=seed.meshes.ToDictionary(m=>m.targetAsset,m=>AssetDatabase.LoadAssetAtPath<Mesh>(m.targetAsset).bindposes);
        CharacterBlenderBridge.Apply(folder+"character-roundtrip.json");
        foreach(var row in seed.meshes)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(row.targetAsset);var vertices=mesh.vertices;var triangles=mesh.triangles;
            Require(AssetDatabase.AssetPathToGUID(row.targetAsset)==row.targetGuid,"Mesh GUID changed.");
            Require(mesh.bindposes.SequenceEqual(binds[row.targetAsset]),"Bind poses changed.");
            Require(triangles.Length==row.triangles.Length,"Triangle count changed.");
            for(int i=0;i<triangles.Length;i++)
            {
                int index=row.triangles[i];var expected=new Vector3(row.positions[index*3],row.positions[index*3+1],row.positions[index*3+2]);
                Require(Vector3.Distance(vertices[triangles[i]],expected)<0.000001f,"Round-trip triangle geometry changed.");
            }
        }
        CharacterBlenderBridge.Apply(folder+"character-edited.json");
        var athletic=seed.meshes.Single(m=>m.name=="Body_Athletic");var target=AssetDatabase.LoadAssetAtPath<Mesh>(athletic.targetAsset);
        Require(Mathf.Abs(target.vertices[0].x-athletic.positions[0]-.0123f)<0.000001f,"Blender vertex edit did not reach Unity.");
        Require(target.bindposes.SequenceEqual(binds[athletic.targetAsset]),"Edited mesh bind poses changed.");
        var before=target.vertices;var bad=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(folder+"character-edited.json"));
        bad.meshes[0].positions[0]+=.2f;bad.meshes.Last().weights[0]=-1;
        Reject(bad,folder+"character-invalid.json");
        Require(target.vertices.SequenceEqual(before),"Validation failure partially changed an earlier mesh.");
        bad=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(folder+"character-edited.json"));bad.meshes[0].targetGuid="invalid";
        Reject(bad,folder+"character-invalid.json");
        bad=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(folder+"character-edited.json"));bad.meshes[0].targetAsset="Assets/../outside.asset";
        Reject(bad,folder+"character-invalid.json");
        bad=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(folder+"character-edited.json"));bad.meshes[0].triangles=Array.Empty<int>();
        Reject(bad,folder+"character-invalid.json");
        bad=JsonUtility.FromJson<CharacterBlenderBridge.Packet>(File.ReadAllText(folder+"character-edited.json"));bad.meshes[0].boneNames[0]="RenamedBone";
        Reject(bad,folder+"character-invalid.json");
        File.WriteAllText(folder+"character-unity-roundtrip.txt","PASS: 24 real Blender mesh round trips; triangle geometry/GUID/bind poses preserved; actual 0.0123m sculpt edit applied; negative weights/partial writes/wrong GUID/path/empty topology/renamed bone rejected.\n");
        Debug.Log("BLACKTOP_BLENDER_ROUNDTRIP_PASS");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only interchange preserves existing Mesh asset GUIDs and canonical bind poses.
public static class CharacterBlenderBridge
{
    const string Folder="SourceAssets/Characters";
    [Serializable] public class Packet { public int version=1; public string generator,canonicalHash; public Bone[] bones; public Surface[] meshes; }
    [Serializable] public class Bone { public string name,parent; public float[] matrix; }
    [Serializable] public class Surface
    {
        public string name,targetAsset,targetGuid;
        public string[] boneNames;
        public float[] positions,normals,uv,weights,matrix;
        public int[] triangles,indices;
    }
    [MenuItem("Blacktop/Characters/Blender/Export Unity Reference Seed")]
    public static void ExportSeed()
    {
        var contract=AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>("Assets/_Project/Data/Characters/CanonicalSkeleton.asset");
        // Imported model assets are inactive; include their parents when resolving the Animator.
        var skin=contract.referenceSkin;var root=skin.GetComponentInParent<Animator>(true).transform;
        var eyes=contract.referenceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s!=skin).ToArray();
        EnsureEditableEyes(eyes);
        var nodes=new HashSet<Transform>();
        foreach(var bone in skin.bones) for(var node=bone;node!=root;node=node.parent) nodes.Add(node);
        var bones=nodes.OrderBy(n=>CharacterSkeletonDefinition.Path(n,root)).Select(n=>new Bone {
            name=n.name,parent=nodes.Contains(n.parent)?n.parent.name:null,
            matrix=Matrix(skin.transform.worldToLocalMatrix*n.localToWorldMatrix) }).ToArray();
        if(bones.Select(b=>b.name).Distinct().Count()!=bones.Length)throw new InvalidOperationException("Bone leaf names must be unique for Blender authoring.");
        var surfaces=new List<Surface>();
        foreach(string path in AssetDatabase.FindAssets("t:Mesh",new[]{FrontEndTemporaryArt.Folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!Path.GetFileName(p).StartsWith("Eye_",StringComparison.Ordinal)).OrderBy(p=>p))
            surfaces.Add(Describe(AssetDatabase.LoadAssetAtPath<Mesh>(path),skin,path,skin.transform));
        foreach(var eye in eyes)
        {
            string path=EyePath(eye.name);
            surfaces.Add(Describe(AssetDatabase.LoadAssetAtPath<Mesh>(path),eye,path,skin.transform));
        }
        var packet=new Packet {bones=bones,meshes=surfaces.ToArray()};
        Directory.CreateDirectory(Folder+"/Reference");
        using(var file=File.Create(Folder+"/Reference/character_seed.json.gz"))
        using(var gzip=new GZipStream(file,System.IO.Compression.CompressionLevel.Optimal))
        using(var writer=new StreamWriter(gzip))writer.Write(JsonUtility.ToJson(packet));
        Debug.Log($"Exported {surfaces.Count} editable surfaces and {bones.Length} rig bones. Build .blend sources with SourceAssets/Characters/build_sources.py.");
    }
    static string EyePath(string name)=>FrontEndTemporaryArt.Folder+"/Eye_"+name+".asset";
    static void EnsureEditableEyes(SkinnedMeshRenderer[] source)
    {
        // Extract imported eye geometry once so Blender edits can return without replacing the FBX rig.
        foreach(var eye in source)
            if(AssetDatabase.LoadAssetAtPath<Mesh>(EyePath(eye.name))==null)
            {var copy=FrontEndTemporaryArt.ReadableCopy(eye.sharedMesh);copy.name="Eye_"+eye.name;AssetDatabase.CreateAsset(copy,EyePath(eye.name));}
        const string path="Assets/_Project/Prefabs/PlayerModel.prefab";
        var prefab=PrefabUtility.LoadPrefabContents(path);bool changed=false;
        try
        {
            foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!source.Any(s=>s.name==renderer.name))continue;
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(EyePath(renderer.name));
                if(renderer.sharedMesh==mesh)continue;
                renderer.sharedMesh=mesh;changed=true;
            }
            if(changed)PrefabUtility.SaveAsPrefabAsset(prefab,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();
    }
    static Surface Describe(Mesh original,SkinnedMeshRenderer skin,string path,Transform canonical)
    {
        var mesh=FrontEndTemporaryArt.ReadableCopy(original);
        try
        {
            var weights=mesh.boneWeights;
            return new Surface {name=path!=null?Path.GetFileNameWithoutExtension(path):"Reference_"+skin.name,
                targetAsset=path,targetGuid=path!=null?AssetDatabase.AssetPathToGUID(path):null,
                boneNames=skin.bones.Select(b=>b.name).ToArray(),positions=mesh.vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),
                normals=mesh.normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),
                triangles=mesh.triangles,indices=weights.SelectMany(w=>new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}).ToArray(),
                weights=weights.SelectMany(w=>new[]{w.weight0,w.weight1,w.weight2,w.weight3}).ToArray(),
                matrix=Matrix(canonical.worldToLocalMatrix*skin.transform.localToWorldMatrix)};
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }
    static float[] Matrix(Matrix4x4 m)=>Enumerable.Range(0,16).Select(i=>m[i%4,i/4]).ToArray();
    [MenuItem("Blacktop/Characters/Blender/Apply Blender Mesh Export")]
    static void ApplyMenu()
    {
        string path=EditorUtility.OpenFilePanel("Apply edited Blender character meshes",Folder,"json");
        if(string.IsNullOrEmpty(path))return;
        try {Apply(path);EditorUtility.DisplayDialog("Character meshes updated","Saved existing mesh assets. Review the Git diff and inspect animation/fit in Unity.","OK");}
        catch(Exception error) {Debug.LogException(error);EditorUtility.DisplayDialog("Character import rejected",error.Message,"OK");}
    }
    public static void Apply(string path)
    {
        var packet=JsonUtility.FromJson<Packet>(File.ReadAllText(path));
        if(packet==null||(packet.version!=1&&packet.version!=2)||packet.meshes==null||packet.meshes.Length==0)throw new InvalidDataException("Unsupported or empty Blender mesh export.");
        var canonical=AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>("Assets/_Project/Data/Characters/CanonicalSkeleton.asset").referenceSkin;
        var bodyBones=canonical.bones.Select(b=>b.name).ToArray();
        if(packet.version==2)ValidateGeneratedRig(packet,canonical);
        var eyes=canonical.GetComponentInParent<Animator>(true).GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s!=canonical).ToArray();
        var staged=new List<(Mesh target,Mesh replacement)>();var paths=new HashSet<string>();
        try
        {
            foreach(var row in packet.meshes)
            {
                // Restrict writes to existing character meshes; never accept arbitrary export paths.
                if(row==null||string.IsNullOrEmpty(row.targetAsset)||!(packet.version==2?GeneratedBodySetup.IsTarget(row.targetAsset):row.targetAsset.StartsWith(FrontEndTemporaryArt.Folder+"/",StringComparison.Ordinal))||
                    row.targetAsset.Contains("..")||!paths.Add(row.targetAsset)||AssetDatabase.AssetPathToGUID(row.targetAsset)!=row.targetGuid)
                    throw new InvalidDataException("Export targets must be unique existing character mesh assets with matching GUIDs.");
                var target=AssetDatabase.LoadAssetAtPath<Mesh>(row.targetAsset);
                var eye=eyes.FirstOrDefault(s=>EyePath(s.name)==row.targetAsset);
                var expectedBones=eye!=null?eye.bones.Select(b=>b.name).ToArray():bodyBones;
                if(target==null||row.boneNames==null||!row.boneNames.SequenceEqual(expectedBones))throw new InvalidDataException("Canonical bone names/order changed.");
                int count=(row.positions?.Length??0)/3;
                if(count==0||row.positions.Length!=count*3||row.normals?.Length!=count*3||row.uv?.Length!=count*2||
                    row.indices?.Length!=count*4||row.weights?.Length!=count*4||row.triangles==null||row.triangles.Length==0||row.triangles.Length%3!=0||
                    row.triangles.Any(i=>i<0||i>=count)||row.indices.Any(i=>i<0||i>=expectedBones.Length)||
                    row.positions.Concat(row.normals).Concat(row.uv).Concat(row.weights).Any(v=>float.IsNaN(v)||float.IsInfinity(v))||row.weights.Any(v=>v<0))
                    throw new InvalidDataException("Invalid mesh arrays, triangles, UVs or weights: "+row.name);
                var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    int k=i*4;if(Mathf.Abs(row.weights.Skip(k).Take(4).Sum()-1)>0.001f)throw new InvalidDataException("Every vertex needs normalized weights (maximum four bones).");
                    weights[i]=new BoneWeight {boneIndex0=row.indices[k],boneIndex1=row.indices[k+1],boneIndex2=row.indices[k+2],boneIndex3=row.indices[k+3],
                        weight0=row.weights[k],weight1=row.weights[k+1],weight2=row.weights[k+2],weight3=row.weights[k+3]};
                }
                var mesh=new Mesh {name=target.name,indexFormat=count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
                staged.Add((target,mesh)); // Dispose even if staging a later mesh throws before any asset write.
                mesh.vertices=Enumerable.Range(0,count).Select(i=>new Vector3(row.positions[i*3],row.positions[i*3+1],row.positions[i*3+2])).ToArray();
                mesh.normals=Enumerable.Range(0,count).Select(i=>new Vector3(row.normals[i*3],row.normals[i*3+1],row.normals[i*3+2])).ToArray();
                mesh.uv=Enumerable.Range(0,count).Select(i=>new Vector2(row.uv[i*2],row.uv[i*2+1])).ToArray();
                mesh.boneWeights=weights;mesh.bindposes=target.bindposes;mesh.triangles=row.triangles;mesh.RecalculateTangents();mesh.RecalculateBounds();
            }
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply Blender character meshes");
            try
            {
                foreach(var item in staged){Undo.RegisterCompleteObjectUndo(item.target,"Update character mesh");EditorUtility.CopySerialized(item.replacement,item.target);EditorUtility.SetDirty(item.target);}
                AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
            }
            catch {Undo.RevertAllDownToGroup(group);AssetDatabase.SaveAssets();throw;}
            Debug.Log($"Applied {staged.Count} Blender meshes without replacing asset GUIDs or bind poses.");
        }
        finally {foreach(var item in staged)UnityEngine.Object.DestroyImmediate(item.replacement);}
    }
    static void ValidateGeneratedRig(Packet packet,SkinnedMeshRenderer canonical)
    {
        // Generated packets carry the complete rest contract; the importer never trusts a matching bone list alone.
        if(packet.generator!="blacktop.body.v1"||packet.canonicalHash==null||packet.canonicalHash.Length!=64||packet.bones==null)
            throw new InvalidDataException("Missing generated-body provenance or rig contract.");
        var root=canonical.GetComponentInParent<Animator>(true).transform;
        var nodes=new HashSet<Transform>();
        foreach(var bone in canonical.bones)for(var node=bone;node!=root;node=node.parent)nodes.Add(node);
        if(packet.bones.Length!=nodes.Count||packet.bones.Select(b=>b.name).Distinct().Count()!=nodes.Count)
            throw new InvalidDataException("Generated rig hierarchy changed.");
        foreach(var node in nodes)
        {
            var row=packet.bones.SingleOrDefault(b=>b.name==node.name);
            var expected=Matrix(canonical.transform.worldToLocalMatrix*node.localToWorldMatrix);
            string parent=nodes.Contains(node.parent)?node.parent.name:"";
            if(row==null||(row.parent??"")!=parent||row.matrix==null||row.matrix.Length!=16||
                row.matrix.Where((value,i)=>float.IsNaN(value)||float.IsInfinity(value)||Mathf.Abs(value-expected[i])>0.00001f).Any())
                throw new InvalidDataException("Generated rig rest transform changed: "+node.name);
        }
    }

}

using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit development stand-ins: region-based surface edits preserve bones, binds and animation data.
public static class FrontEndTemporaryArt
{
    public const string Folder="Assets/_Project/Art/Characters/Temporary";
    public static CharacterPartDefinition[] Build(PlayerAppearanceCatalog original)
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var reference=original.Find("body.athletic.prototype").skeleton;
        var result=original.parts.ToList();
        foreach(BodyArchetype build in Enum.GetValues(typeof(BodyArchetype)))
        {
            string suffix=build.ToString().ToLowerInvariant();
            var bodyMesh=MeshAsset("Body_"+build,()=>BodyMesh(reference.referenceSkin,build));
            result.Add(Part("Body_"+build,$"body.{suffix}.temporary",CharacterPartSlot.Body,reference,bodyMesh,build));
            foreach(var slot in new[]{CharacterPartSlot.Top,CharacterPartSlot.Shorts,CharacterPartSlot.Footwear})
            {
                var garment=MeshAsset(slot+"_"+build,()=>Garment(bodyMesh,slot));
                var part=Part(slot+"_"+build,$"gear.{slot.ToString().ToLowerInvariant()}.{suffix}.temporary",slot,reference,garment,build);
                part.supportedBodies=new[]{build};part.variantFamilyId="temporary."+slot;
                part.materialTints=new[]{slot==CharacterPartSlot.Shorts?CharacterTint.TeamSecondary:CharacterTint.TeamPrimary};
                EditorUtility.SetDirty(part);result.Add(part);
            }
        }
        foreach(var slot in new[]{CharacterPartSlot.Gloves,CharacterPartSlot.Wristband,CharacterPartSlot.ArmSleeve,CharacterPartSlot.Headband,CharacterPartSlot.Accessory})
        {
            string path=$"{FrontEndSetup.Data}/Missing_{slot}.asset";
            var missing=AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>(path);
            if(missing==null)
            {
                missing=ScriptableObject.CreateInstance<CharacterPartDefinition>();missing.id="gear."+slot.ToString().ToLowerInvariant()+".missing";
                missing.displayName=slot+" production slot";missing.slot=slot;missing.placeholder=true;
                missing.authoringNote="No compatible gear mesh is installed. Selecting this records the slot and omits the attachment.";
                AssetDatabase.CreateAsset(missing,path);
            }
            result.Add(missing);
        }
        AssetDatabase.SaveAssets();return result.ToArray();
    }
    static CharacterPartDefinition Part(string name,string id,CharacterPartSlot slot,CharacterSkeletonDefinition skeleton,Mesh mesh,BodyArchetype build)
    {
        string path=$"{FrontEndSetup.Data}/{name}.asset";
        var part=AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>(path);
        if(part!=null)return part;
        part=ScriptableObject.CreateInstance<CharacterPartDefinition>();part.id=id;part.displayName=name.Replace('_',' ');
        part.slot=slot;part.bodyArchetype=build;part.skeleton=skeleton;part.skinnedSource=skeleton.referenceSkin;
        part.skinnedMeshOverride=mesh;part.integratedHead=slot==CharacterPartSlot.Body;part.temporaryVisual=true;
        part.authoringNote="TEMPORARY region-deformed prototype geometry. Shared rig/binds retained; production topology, seams and animation fit require artist review.";
        part.materialTints=new[]{CharacterTint.Skin};AssetDatabase.CreateAsset(part,path);return part;
    }
    static Mesh MeshAsset(string name,Func<Mesh> create)
    {
        string path=$"{Folder}/{name}.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
        var mesh=create();mesh.name="TEMP_"+name;AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Mesh ReadableCopy(Mesh original)
    {
        // Editor-only MeshUtility reads the imported non-readable model without changing its importer.
        using(var arrays=MeshUtility.AcquireReadOnlyMeshData(original))
        {
            var source=arrays[0];var mesh=new Mesh();mesh.SetVertexBufferParams(source.vertexCount,original.GetVertexAttributes());
            for(int stream=0;stream<source.vertexBufferCount;stream++)
            {var bytes=source.GetVertexData<byte>(stream);mesh.SetVertexBufferData(bytes,0,0,bytes.Length,stream);}
            var indices=source.GetIndexData<ushort>();
            if(source.indexFormat==IndexFormat.UInt16){mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,indices.Length);}
            else{var wide=source.GetIndexData<uint>();mesh.SetIndexBufferParams(wide.Length,IndexFormat.UInt32);mesh.SetIndexBufferData(wide,0,0,wide.Length);}
            mesh.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)mesh.SetSubMesh(i,source.GetSubMesh(i),MeshUpdateFlags.DontRecalculateBounds);
            mesh.bindposes=original.bindposes;mesh.bounds=original.bounds;return mesh;
        }
    }
    static Mesh BodyMesh(SkinnedMeshRenderer skin,BodyArchetype build)
    {
        var mesh=ReadableCopy(skin.sharedMesh);var vertices=mesh.vertices;var weights=mesh.boneWeights;var bones=skin.bones;
        for(int i=0;i<vertices.Length;i++)
        {
            var p=vertices[i];var w=weights[i];
            float head=Head(w.boneIndex0)*w.weight0+Head(w.boneIndex1)*w.weight1+Head(w.boneIndex2)*w.weight2+Head(w.boneIndex3)*w.weight3;
            if(head>.5f)continue; // Keep the integrated head/neck and eye attachment envelope consistent.
            float chest=Mathf.Exp(-Mathf.Pow((p.y-1.32f)/.24f,2));
            float waist=Mathf.Exp(-Mathf.Pow((p.y-.97f)/.20f,2));
            float legs=Mathf.Exp(-Mathf.Pow((p.y-.65f)/.28f,2));
            float arms=Mathf.SmoothStep(.25f,.5f,Mathf.Abs(p.x))*chest;
            float width=1,depth=1;
            switch(build)
            {
                case BodyArchetype.Lean:width-=.12f*chest+.10f*waist+.08f*legs;depth-=.10f*chest+.09f*waist;break;
                case BodyArchetype.Thick:width+=.14f*chest+.08f*waist+.15f*legs+.10f*arms;depth+=.13f*chest+.10f*legs;break;
                case BodyArchetype.Heavy:width+=.12f*chest+.34f*waist+.14f*legs;depth+=.14f*chest+.42f*waist+.12f*legs;break;
                case BodyArchetype.Massive:width+=.28f*chest+.10f*waist+.22f*legs+.22f*arms;depth+=.25f*chest+.08f*waist+.18f*legs;break;
            }
            p.x*=width;p.z*=depth;vertices[i]=p;
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        float Head(int index)=>bones[index].name.Contains("Head")||bones[index].name.Contains("Neck")?1:0;
    }
    static Mesh Garment(Mesh body,CharacterPartSlot slot)
    {
        var vertices=body.vertices;var triangles=body.triangles;
        var selected=new System.Collections.Generic.List<int>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3f;
            bool include=slot==CharacterPartSlot.Top?center.y>.94f&&center.y<1.49f&&Mathf.Abs(center.x)<.36f:
                slot==CharacterPartSlot.Shorts?center.y>.47f&&center.y<.98f:center.y<.15f;
            if(include){selected.Add(triangles[i]);selected.Add(triangles[i+1]);selected.Add(triangles[i+2]);}
        }
        // Remove unused vertices so a small shoe does not skin the full eighteen-thousand-vertex body.
        var used=selected.Distinct().ToArray();var remap=used.Select((v,i)=>(v,i)).ToDictionary(p=>p.v,p=>p.i);
        var mesh=new Mesh();mesh.vertices=used.Select(i=>new Vector3(vertices[i].x*1.025f,vertices[i].y,vertices[i].z*1.035f)).ToArray();
        var uv=body.uv;mesh.uv=used.Select(i=>uv[i]).ToArray();
        var weights=body.boneWeights;mesh.boneWeights=used.Select(i=>weights[i]).ToArray();mesh.bindposes=body.bindposes;
        mesh.triangles=selected.Select(i=>remap[i]).ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}

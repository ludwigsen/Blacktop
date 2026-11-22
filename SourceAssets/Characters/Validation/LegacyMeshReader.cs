using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The archived JSON mesh bridge reads legacy artist edits in a compatible review checkout.
internal static class LegacyMeshReader
{
    public const string Folder = "Assets/_Project/Art/Characters/Temporary";
    internal static Mesh ReadableCopy(Mesh original)
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
}

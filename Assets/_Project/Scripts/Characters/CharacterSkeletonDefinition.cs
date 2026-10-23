using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Blacktop/Characters/Skeleton Contract")]
public class CharacterSkeletonDefinition : ScriptableObject
{
    public string id = "blacktop.humanoid.v1";
    public GameObject referenceModel;
    public SkinnedMeshRenderer referenceSkin;
    [Min(0.00001f)] public float bindTolerance = 0.001f;

    public static string Path(Transform bone, Transform root)
    {
        // Full paths distinguish identical leaf names on different branches.
        if (bone == root) return "";
        string path = bone.name;
        while (bone.parent != null && bone.parent != root)
        {
            bone = bone.parent;
            path = bone.name + "/" + path;
        }
        return bone.parent == root ? path : null;
    }

    public bool ValidateSkin(SkinnedMeshRenderer source, out string error)
    {
        error = null;
        var canonical = referenceModel != null ? referenceModel.GetComponentInChildren<Animator>(true) : null;
        var sourceAnimator = source != null ? source.GetComponentInParent<Animator>(true) : null;
        if (canonical == null || canonical.avatar == null || !canonical.avatar.isHuman || !canonical.avatar.isValid ||
            referenceSkin == null || referenceSkin.sharedMesh == null || sourceAnimator == null || sourceAnimator.avatar == null ||
            !sourceAnimator.avatar.isHuman || !sourceAnimator.avatar.isValid || source.sharedMesh == null)
        { error = "Missing valid Humanoid reference model, skin or source Avatar."; return false; }

        var referenceSpace = canonical.transform.worldToLocalMatrix * referenceSkin.transform.localToWorldMatrix;
        var sourceSpace = sourceAnimator.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        for (int element = 0; element < 16; element++)
            if (Mathf.Abs(referenceSpace[element] - sourceSpace[element]) > bindTolerance)
            { error = "Skin renderer must use the canonical mesh coordinate space."; return false; }

        // Bone names alone do not prove compatibility: compare rest TRS and inverse binds.
        var binds = new Dictionary<string, Matrix4x4>();
        var referenceBones = referenceSkin.bones;
        var referenceBinds = referenceSkin.sharedMesh.bindposes;
        if (referenceBones.Length != referenceBinds.Length)
        { error = "Canonical skin has inconsistent bone and bind-pose counts."; return false; }
        for (int i = 0; i < referenceBones.Length; i++)
        {
            string path = Path(referenceBones[i], canonical.transform);
            if (path == null || !binds.TryAdd(path, referenceBinds[i]))
            { error = "Canonical skin contains ambiguous bone paths."; return false; }
        }
        var bones = source.bones;
        var poses = source.sharedMesh.bindposes;
        if (bones.Length == 0 || bones.Length != poses.Length)
        { error = "Skin must contain one bind pose per bone."; return false; }
        for (int i = 0; i < bones.Length; i++)
        {
            string path = bones[i] != null ? Path(bones[i], sourceAnimator.transform) : null;
            if (path == null || !binds.TryGetValue(path, out var expected))
            { error = $"Unknown skin bone {path ?? "<null>"}."; return false; }
            for (int element = 0; element < 16; element++)
                if (Mathf.Abs(expected[element] - poses[i][element]) > bindTolerance)
                { error = $"Incompatible bind pose at {path}."; return false; }
            for (var bone = bones[i]; bone != sourceAnimator.transform; bone = bone.parent)
            {
                string bonePath = Path(bone, sourceAnimator.transform);
                var rest = canonical.transform.Find(bonePath);
                if (rest == null || Vector3.Distance(rest.localPosition, bone.localPosition) > bindTolerance ||
                    Quaternion.Angle(rest.localRotation, bone.localRotation) > 0.1f ||
                    Vector3.Distance(rest.localScale, bone.localScale) > bindTolerance)
                { error = $"Incompatible rest hierarchy at {bonePath}."; return false; }
            }
        }
        if (source.sharedMaterials.Length != source.sharedMesh.subMeshCount)
        { error = "Material slots must match mesh submeshes."; return false; }
        foreach (var material in source.sharedMaterials)
            if (material == null) { error = "Skin contains a missing material."; return false; }
        return true;
    }
}

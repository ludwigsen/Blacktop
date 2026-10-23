using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Replaceable adapter on PlayerModel. Gameplay never depends on imported mesh names.
[DisallowMultipleComponent]
public class PlayerVisualRig : MonoBehaviour
{
    public CharacterSkeletonDefinition skeleton;
    public Animator animator;
    public SkinnedMeshRenderer bodyRenderer;
    public Renderer[] integratedHeadRenderers = System.Array.Empty<Renderer>();
    public TMP_Text[] jerseyLabels = System.Array.Empty<TMP_Text>();
    [Min(1)] public int referenceHeightCm = 183;
    public Vector3 footPivot = new Vector3(0f, -1f, 0f);

    Dictionary<string, Transform> bones;
    Vector3 originalPosition, originalScale, headScale;
    Mesh originalMesh;
    Material[] originalMaterials;
    Transform[] originalBones;
    Transform originalRootBone;
    Bounds originalBounds;
    Transform head;

    public void Initialize()
    {
        if (bones != null) return;
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        originalMesh = bodyRenderer.sharedMesh;
        originalMaterials = bodyRenderer.sharedMaterials;
        originalBones = bodyRenderer.bones;
        originalRootBone = bodyRenderer.rootBone;
        originalBounds = bodyRenderer.localBounds;
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) headScale = head.localScale;
        bones = new Dictionary<string, Transform>();
        foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            bones.Add(CharacterSkeletonDefinition.Path(bone, animator.transform), bone);
    }

    public bool ValidateRig(out string error)
    {
        error = null;
        if (skeleton == null || animator == null || bodyRenderer == null || referenceHeightCm <= 0 ||
            animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman || animator.applyRootMotion ||
            !animator.transform.IsChildOf(transform) || !bodyRenderer.transform.IsChildOf(animator.transform) ||
            skeleton.referenceModel == null || skeleton.referenceSkin == null || skeleton.referenceSkin.sharedMesh == null ||
            animator.avatar != skeleton.referenceModel.GetComponentInChildren<Animator>(true)?.avatar)
        { error = "Adapter needs a child Humanoid Animator (root motion off), body skin and skeleton contract."; return false; }
        foreach (var bone in skeleton.referenceSkin.bones)
        {
            string path = bone != null ? CharacterSkeletonDefinition.Path(bone, skeleton.referenceModel.GetComponentInChildren<Animator>(true).transform) : null;
            if (path == null || animator.transform.Find(path) == null)
            { error = $"Adapter is missing canonical bone {bone?.name ?? "<null>"}."; return false; }
        }
        return true;
    }

    public Transform Bone(string path) => bones.TryGetValue(path, out var bone) ? bone : null;

    public void ApplyHeight(PlayerHeightProfile profile, float enlargement)
    {
        Initialize();
        float ratio = profile.Centimeters / (float)referenceHeightCm;
        transform.localScale = originalScale * ratio;
        transform.localPosition = footPivot + (originalPosition - footPivot) * ratio;
        if (head != null) head.localScale = headScale * enlargement;
        // Measurement/anchor metadata never changes colliders, root scale or ratings.
    }

    public void RestoreBody()
    {
        bodyRenderer.sharedMesh = originalMesh;
        bodyRenderer.sharedMaterials = originalMaterials;
        bodyRenderer.bones = originalBones;
        bodyRenderer.rootBone = originalRootBone;
        bodyRenderer.localBounds = originalBounds;
        bodyRenderer.SetPropertyBlock(null);
        for (int i = 0; i < originalMaterials.Length; i++) bodyRenderer.SetPropertyBlock(null, i);
    }

    public Transform[] Remap(SkinnedMeshRenderer source)
    {
        var sourceRoot = source.GetComponentInParent<Animator>(true).transform;
        var sourceBones = source.bones;
        var mapped = new Transform[sourceBones.Length];
        for (int i = 0; i < mapped.Length; i++) mapped[i] = Bone(CharacterSkeletonDefinition.Path(sourceBones[i], sourceRoot));
        return mapped;
    }
}

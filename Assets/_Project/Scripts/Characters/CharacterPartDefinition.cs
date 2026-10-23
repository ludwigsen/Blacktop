using UnityEngine;

[CreateAssetMenu(menuName = "Blacktop/Characters/Part Preset")]
public class CharacterPartDefinition : ScriptableObject
{
    public string id;
    public CharacterPartSlot slot;
    public BodyArchetype bodyArchetype = BodyArchetype.Athletic;
    public CharacterSkeletonDefinition skeleton;
    [Header("Art availability")]
    public bool placeholder;
    [TextArea] public string authoringNote;
    public bool emptyOption; // Shaved / clean-shaven are real choices without a renderer.
    public bool useExistingBody;
    public bool integratedHead;
    [Header("Choose a skinned source OR a rigid attachment")]
    public SkinnedMeshRenderer skinnedSource;
    [Range(0.001f, 1f)] public float lod0ScreenHeight = 0.6f;
    public CharacterSkinLod[] lowerDetailLevels = System.Array.Empty<CharacterSkinLod>();
    public GameObject attachmentPrefab;
    public HumanBodyBones socket = HumanBodyBones.Head;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    [Tooltip("One tint role per material slot; None preserves the shared material.")]
    public CharacterTint[] materialTints = System.Array.Empty<CharacterTint>();
    [Header("Compatibility (empty lists mean unrestricted)")]
    public BodyArchetype[] supportedBodies = System.Array.Empty<BodyArchetype>();
    public string[] compatibleFaceIds = System.Array.Empty<string>();
    public string proportionCalibrationId;
    public string scalpProfileId;
    public string[] supportedScalpProfiles = System.Array.Empty<string>();
}

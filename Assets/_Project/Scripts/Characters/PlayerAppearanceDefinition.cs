using UnityEngine;

[CreateAssetMenu(menuName = "Blacktop/Characters/Player Appearance")]
public class PlayerAppearanceDefinition : ScriptableObject
{
    public string id;
    public BodyArchetype bodyArchetype = BodyArchetype.Athletic;
    public string bodyId = "body.athletic.prototype";
    public string faceId = "face.prototype.a";
    public string hairId = "hair.shaved";
    public string facialHairId = "facial.clean";
    public string skinToneId = "skin.medium";
    public string hairColorId = "hair.black";
    public PlayerHeightProfile heightProfile;
    [Range(1f, 1.05f)] public float headScale = 1f;
    public string[] clothingIds = System.Array.Empty<string>();
    public string[] accessoryIds = System.Array.Empty<string>();
    [Tooltip("Reserved saved metadata; assembly never rerolls explicit preset IDs.")]
    public int appearanceSeed;
}

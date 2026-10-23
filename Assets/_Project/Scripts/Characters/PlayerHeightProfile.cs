using UnityEngine;

[CreateAssetMenu(menuName = "Blacktop/Characters/Height Profile")]
public class PlayerHeightProfile : ScriptableObject
{
    public string id;
    public PlayerHeight height = PlayerHeight.Ft6In0;
    [Header("Authoring measurements in metres at this height")]
    [Min(0f)] public float legLength;
    [Min(0f)] public float torsoLength;
    [Min(0f)] public float armLength;
    [Min(0f)] public float shoulderHeight;
    public Vector3 headPlacement;
    public Vector3 cameraOffset;
    public Vector3 nameplateOffset;
    [Tooltip("Optional authored mesh calibration. Empty uses the adapter's uniform foot-pivot scale.")]
    public string proportionCalibrationId;

    // Bands are derived, never independently serialized into a contradictory selection.
    public int Centimeters => (int)height;
    public PlayerHeightBand Band => Centimeters <= 178 ? PlayerHeightBand.Short :
        Centimeters <= 185 ? PlayerHeightBand.Average : Centimeters <= 193 ? PlayerHeightBand.Tall :
        Centimeters <= 198 ? PlayerHeightBand.VeryTall : PlayerHeightBand.Giant;
}

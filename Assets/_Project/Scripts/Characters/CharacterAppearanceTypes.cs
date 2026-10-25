using System;
using UnityEngine;

// Explicit values keep saved selections stable when the Inspector lists grow.
public enum BodyArchetype { Lean = 0, Athletic = 1, Thick = 2, Heavy = 3, Massive = 4, Built = 5 }
public enum PlayerHeightBand { Short, Average, Tall, VeryTall, Giant }
public enum PlayerHeight { Ft5In7 = 170, Ft5In9 = 175, Ft5In10 = 178, Ft5In11 = 180,
    Ft6In0 = 183, Ft6In1 = 185, Ft6In2 = 188, Ft6In3 = 191, Ft6In4 = 193,
    Ft6In5 = 196, Ft6In6 = 198, Ft6In7 = 201 }
public enum CharacterPartSlot { Body, Face, Hair, FacialHair, Top, Shorts, Footwear,
    Wristband, Gloves, ArmSleeve, Headband, Accessory }
public enum CharacterTint { None, Skin, Hair, TeamPrimary, TeamSecondary }

[Serializable]
public struct AppearanceColor
{
    public string id;
    public Color color; // Shared materials receive this through per-slot property blocks.
}

[Serializable]
public struct CharacterSkinLod
{
    public SkinnedMeshRenderer source;
    [Range(0.001f, 1f)] public float screenHeight; // Lower detail levels descend toward culling.
}

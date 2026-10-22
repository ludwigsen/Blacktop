using UnityEngine;

// Cosmetic data only: every style shares the same football mesh and gameplay rules.
[CreateAssetMenu(menuName = "Blacktop/Football Style")]
public class FootballStyle : ScriptableObject
{
    public string displayName = "Street Classic";
    public Color leatherColor = new Color(0.028f, 0.031f, 0.035f);
    public Color accentColor = new Color(0.95f, 0.245f, 0.025f);
    public Color contrastColor = new Color(0.91f, 0.87f, 0.76f);
    public Color seamColor = new Color(0.008f, 0.008f, 0.008f);
    public Color laceColor = new Color(0.98f, 0.94f, 0.82f);
    public Color inkColor = new Color(0.025f, 0.025f, 0.025f);
    [Range(0f, 1f)] public float leatherSmoothness = 0.18f;

    // Stable FBX material-slot contract: leather, contrast, accent, seams, laces, ink.
    public Color ColorForSlot(int slot) => slot switch
    {
        0 => leatherColor,
        1 => contrastColor,
        2 => accentColor,
        3 => seamColor,
        4 => laceColor,
        5 => inkColor,
        _ => Color.white
    };
}

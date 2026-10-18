using UnityEngine;

// Pre-game presentation configuration only. Roster membership and player attributes
// belong to their own data assets; changing branding never changes player identity.
[CreateAssetMenu(menuName = "Blacktop/Team Identity")]
public class TeamIdentity : ScriptableObject
{
    public string teamName = "Football Team";
    public Color accentColor = Color.white; // Primary: background paint and meter fill.
    public Color secondaryAccentColor = Color.white; // Secondary: meter frame and accent rules.
    public Sprite logo; // Explicit assignment keeps a renamed team paired with its chosen crest.
}
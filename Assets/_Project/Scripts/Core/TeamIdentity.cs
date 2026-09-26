using UnityEngine;

// Minimal per-team branding — name, accent color, and (once real art exists) a
// crest/logo sprite. Exists so GameHUD, and anything else that ever needs a team's
// name or color, reads ONE shared source instead of every UI script hardcoding its
// own placeholder strings and color fields.
[CreateAssetMenu(menuName = "Blacktop/Team Identity")]
public class TeamIdentity : ScriptableObject
{
    public string teamName = "TEAM";
    public Color accentColor = Color.white;
    public Sprite logo; // crown / anarchy-style crest — no art yet, wired for when it exists
}
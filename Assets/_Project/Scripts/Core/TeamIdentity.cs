using UnityEngine;

// Minimal per-team branding — name, accent color, and (once real art exists) a
// crest/logo sprite. Exists so GameHUD, and anything else that ever needs a team's
// name or color, reads ONE shared source instead of every UI script hardcoding its
// own placeholder strings and color fields.
[CreateAssetMenu(menuName = "Blacktop/Team Identity")]
public class TeamIdentity : ScriptableObject
{
    public string teamName = "Football Team";
    public Color accentColor = Color.white; // white default
    public Sprite logo; // crest
}
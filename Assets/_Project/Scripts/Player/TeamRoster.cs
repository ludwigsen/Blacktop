using System.Collections.Generic;
using UnityEngine;

// One team's full player pool as DATA. GUIDs are generated automatically the moment a
// row is added (OnValidate, editor-only) — you never type or see one. Uniqueness only
// has to hold WITHIN a roster (no trades/free agency in Blacktop — "Create a Team" is
// the only roster-authoring feature), so there's no cross-team ID registry to maintain.
[CreateAssetMenu(menuName = "Blacktop/Team Roster")]
public class TeamRoster : ScriptableObject
{
    [System.Serializable]
    public struct PlayerRecord
    {
        [Tooltip("Auto-generated GUID — do not edit by hand. Left blank on a freshly added row until OnValidate fills it.")]
        public string id;
        public string displayName;
        public int jerseyNumber;

        [Tooltip("Flavor only, no gameplay effect yet.")]
        public string archetypeLabel;
        public Color accentColor;

        [Header("Attributes (0-20, 10 = neutral)")]
        [Range(0, 20)] public int passing;
        [Range(0, 20)] public int speed;
        [Range(0, 20)] public int blocking;
        [Range(0, 20)] public int agility;
        [Range(0, 20)] public int catching;
        [Range(0, 20)] public int runPower;
        [Range(0, 20)] public int carrying;
        [Range(0, 20)] public int tackling;
        [Range(0, 20)] public int coverage;
        [Range(0, 20)] public int dMoves;
        [Range(0, 20)] public int swagger;
        [Range(0, 20)] public int routeRunning;
    }

    public string teamName;
    public AttributeCurves curves;
    public List<PlayerRecord> players = new();

    public PlayerRecord? GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var p in players)
            if (p.id == id) return p;
        return null;
    }

#if UNITY_EDITOR
    // Editor-only, runs on every Inspector edit — including "add element to list" via
    // the "+" button, which is how a designer actually creates a player: hit +, type a
    // name and stats, GUID fills itself in silently. Also catches the near-impossible
    // case of a manually pasted duplicate ID.
    void OnValidate()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];

            if (string.IsNullOrEmpty(p.id))
            {
                p.id = System.Guid.NewGuid().ToString();
                players[i] = p;
            }
            else if (!seen.Add(p.id))
            {
                Debug.LogWarning($"[TeamRoster] {name}: duplicate id on '{p.displayName}' — regenerating.");
                p.id = System.Guid.NewGuid().ToString();
                players[i] = p;
            }
        }
    }
#endif
}
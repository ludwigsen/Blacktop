using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns playerId from "paste a GUID you can't read" into "pick a name from a list."
// The GUID still exists underneath (TeamRoster.PlayerRecord.id) — this editor just
// resolves it to a display string and writes back the underlying id string when
// the dropdown selection changes.
[CustomEditor(typeof(CharacterApplier))]
public class CharacterApplierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var rosterProp = serializedObject.FindProperty("roster");
        var idProp = serializedObject.FindProperty("playerId");
        var showNameplateProp = serializedObject.FindProperty("showNameplate");
        var heightProp = serializedObject.FindProperty("nameplateHeight");
        var fontProp = serializedObject.FindProperty("nameplateFontSize");

        EditorGUILayout.PropertyField(rosterProp);

        var roster = rosterProp.objectReferenceValue as TeamRoster;
        if (roster != null && roster.players.Count > 0)
        {
            var options = roster.players.Select(p => $"#{p.jerseyNumber} {p.displayName}").ToArray();
            var ids = roster.players.Select(p => p.id).ToArray();

            int currentIndex = System.Array.IndexOf(ids, idProp.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            int selected = EditorGUILayout.Popup("Player", currentIndex, options);
            idProp.stringValue = ids[selected];
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a TeamRoster with at least one player to pick from a list.", MessageType.Info);
        }

        EditorGUILayout.PropertyField(showNameplateProp);
        EditorGUILayout.PropertyField(heightProp);
        EditorGUILayout.PropertyField(fontProp);

        serializedObject.ApplyModifiedProperties();
    }
}
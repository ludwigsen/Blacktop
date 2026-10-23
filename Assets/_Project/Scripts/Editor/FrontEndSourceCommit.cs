using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The only source-asset writer. Ordinary runtime Save/Cancel cannot call AssetDatabase.
[InitializeOnLoad]
public static class FrontEndSourceCommit
{
    static FrontEndSourceCommit()
    {
        FrontEndRuntime.SourceCommitRequested-=Commit;FrontEndRuntime.SourceCommitRequested+=Commit;
        FrontEndRuntime.EditorQuitRequested=()=>EditorApplication.isPlaying=false;
    }
    [MenuItem("Blacktop/Front End/Commit Saved Rosters to Source")]
    public static void CommitMenu()
    {
        var service=FrontEndRuntime.Rosters;
        if(service==null){Debug.LogWarning("Open MainMenu in Play Mode and save a roster first.");return;}
        if(EditorUtility.DisplayDialog("Commit canonical roster assets?","Write validated runtime rosters, appearances and active assignments to development assets?","Commit source assets","Cancel"))Commit(service);
    }
    static void Commit(RosterService service)
    {
        var snapshot=service.Snapshot;string error=service.Validate(snapshot);
        if(error!=null){FrontEndRuntime.SourceCommitMessage="Source commit rejected: "+error;return;}
        const string folder="Assets/_Project/Data/Characters/Players";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Commit Blacktop rosters");
        var created=new List<string>();
        try
        {
            foreach(var team in snapshot.teams)
            {
                var definition=service.Settings.Team(team.teamId);
                Undo.RecordObjects(new UnityEngine.Object[]{definition,definition.roster},"Commit roster");
                var records=new List<TeamRoster.PlayerRecord>();
                foreach(var player in team.players)
                {
                    string path=$"{folder}/{player.record.id}.asset";
                    var appearance=AssetDatabase.LoadAssetAtPath<PlayerAppearanceDefinition>(path);
                    if(appearance==null)
                    {
                        if(File.Exists(path))throw new InvalidOperationException("An unrelated asset occupies "+path);
                        appearance=ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();
                        AssetDatabase.CreateAsset(appearance,path);created.Add(path);
                    }
                    else Undo.RecordObject(appearance,"Commit player appearance");
                    player.appearance.Apply(appearance,service.Settings.appearances,player.record.id);
                    EditorUtility.SetDirty(appearance);
                    var record=player.record;record.appearance=appearance;records.Add(record);
                }
                definition.roster.players=records;
                definition.activeLineup=team.active.Select(a=>new LineupAssignment {playerId=a.playerId,offense=a.offense,defense=a.defense}).ToArray();
                EditorUtility.SetDirty(definition.roster);EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
            FrontEndRuntime.SourceCommitMessage="Canonical roster/appearance/lineup assets committed. Review the Git diff; runtime saves remain separate.";
        }
        catch(Exception failure)
        {
            Undo.RevertAllDownToGroup(group);
            foreach(string path in created)AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();FrontEndRuntime.SourceCommitMessage="Source commit rolled back: "+failure.Message;
        }
    }
}

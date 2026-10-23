using System;
using UnityEngine;

// A cancellable transaction, separate from the live roster and all source assets.
public sealed class PlayerEditorSession
{
    public RosterPlayer Draft { get; private set; }
    public string TeamId;
    public int Offense=-1, Defense;
    readonly string originalTeam, originalJson;
    readonly int originalOffense, originalDefense;
    public bool HasUncommittedInput; // Invalid field text still requires an explicit discard.
    public bool Dirty => HasUncommittedInput || TeamId!=originalTeam || Offense!=originalOffense || Defense!=originalDefense || JsonUtility.ToJson(Draft)!=originalJson;
    public PlayerEditorSession(RosterPlayer player,string team,int offense=-1,int defense=0,bool duplicate=false)
    {
        Draft=player.Copy(); TeamId=originalTeam=team;
        if(duplicate) { Draft.record.id=""; offense=-1; }
        Offense=originalOffense=offense; Defense=originalDefense=defense;
        originalJson=JsonUtility.ToJson(Draft);
    }
    public void Reset()
    {
        HasUncommittedInput=false;Draft=JsonUtility.FromJson<RosterPlayer>(originalJson); TeamId=originalTeam;
        Offense=originalOffense; Defense=originalDefense;
    }
    public bool Save(RosterService service,out string id,out string error) => service.SavePlayer(TeamId,Draft,Offense,Defense,out id,out error);
    public static RosterPlayer New(FrontEndSettings settings,int number)
    {
        var record=new TeamRoster.PlayerRecord {id="",displayName="New Player",jerseyNumber=number,accentColor=Color.white};
        for(int i=0;i<12;i++) RosterRatings.Set(ref record,i,10);
        return new RosterPlayer {record=record,appearance=AppearanceSelection.From(settings.defaultAppearance)};
    }
}

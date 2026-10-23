using System;
using System.Linq;

// Detached snapshot survives scene reload/rematch; beginning a match never edits source assets.
public sealed class QuickPlayMatch
{
    public FrontEndSettings Settings { get; }
    public string Team0Id { get; }
    public string Team1Id { get; }
    public int HumanSide { get; }
    public FieldDefinition Field { get; }
    readonly RosterSaveData snapshot;
    public RosterSaveData Snapshot => snapshot.Copy();
    QuickPlayMatch(RosterService service,string team0,string team1,int human,FieldDefinition field)
    { Settings=service.Settings;Team0Id=team0;Team1Id=team1;HumanSide=human;Field=field;snapshot=service.Snapshot; }
    public static bool TryCreate(RosterService service,string human,string opponent,int humanSide,FieldDefinition field,out QuickPlayMatch match,out string error)
    {
        match=null;
        error=human==opponent?"Choose two different teams.": service.Readiness(human) ?? service.Readiness(opponent);
        if(humanSide<0 || humanSide>1) error="Choose human side 0 or 1.";
        if(field==null || !service.Settings.fields.Contains(field)) error="Choose an available field.";
        if(error!=null)return false;
        match=new QuickPlayMatch(service,humanSide==0?human:opponent,humanSide==1?human:opponent,humanSide,field);
        return true;
    }
}

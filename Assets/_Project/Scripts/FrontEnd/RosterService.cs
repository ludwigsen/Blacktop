using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// All membership edits are clone -> validate -> atomic disk write -> swap, never partial mutations.
public sealed class RosterService
{
    public FrontEndSettings Settings { get; }
    public string LoadMessage { get; private set; } = "Using canonical default rosters.";
    readonly RosterSaveStore store;
    RosterSaveData data;
    public RosterSaveData Snapshot => data.Copy();
    public SavedTeamRoster Team(string id) => data.Copy().teams.Find(t=>t.teamId==id);

    public RosterService(FrontEndSettings settings, RosterSaveStore persistence)
    {
        Settings=settings; store=persistence; data=Defaults();
        for(int attempt=0;attempt<2;attempt++)
        {
            try
            {
                string json=store.Read(attempt==1);
                if(string.IsNullOrEmpty(json)) continue;
                var loaded=JsonUtility.FromJson<RosterSaveData>(json);
                string error=Validate(loaded);
                if(error!=null) { LoadMessage="Saved rosters unavailable: "+error; continue; }
                data=loaded;
                LoadMessage=attempt==0?"Loaded saved rosters.":"Recovered previous save backup. Save again to repair the primary file.";
                return;
            }
            catch(Exception error) { LoadMessage="Save could not be read; defaults retained. "+error.Message; }
        }
    }

    RosterSaveData Defaults()
    {
        var result=new RosterSaveData();
        foreach(var source in Settings.teams)
        {
            var team=new SavedTeamRoster { teamId=source.id };
            foreach(var row in source.roster.players)
            {
                var record=row;
                var appearance=AppearanceSelection.From(record.appearance!=null?record.appearance:Settings.defaultAppearance);
                record.appearance=null; // Unity asset references never enter portable save files.
                team.players.Add(new RosterPlayer { record=record,appearance=appearance });
            }
            team.active=source.activeLineup.Select(a=>new LineupAssignment {playerId=a.playerId,offense=a.offense,defense=a.defense}).ToList();
            result.teams.Add(team);
        }
        return result;
    }

    public string Validate(RosterSaveData candidate)
    {
        if(candidate==null || candidate.version!=1 || candidate.teams==null) return "Unsupported roster save version.";
        if(candidate.teams.Count!=Settings.teams.Length) return "Save team catalog differs from installed teams. Restore Defaults to migrate deliberately.";
        var catalogErrors=Settings.appearances.ValidateCatalog();
        if(catalogErrors.Count>0)return catalogErrors[0]; // Validate immutable art once per transaction, not per player.
        var teamIds=new HashSet<string>(); var players=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var team in candidate.teams)
        {
            if(team==null) return "Save contains an empty team.";
            if(Settings.Team(team.teamId)==null) return "Unknown saved team: "+team.teamId+"; catalog: "+string.Join(",",Settings.teams.Select(t=>t!=null?t.id:"<missing>"));
            if(!teamIds.Add(team.teamId)) return "Duplicate saved team: "+team.teamId;
            if(team.players==null || team.active==null) return "Team data is missing: "+team.teamId;
            var numbers=new HashSet<int>(); var membership=new HashSet<string>();
            foreach(var player in team.players)
            {
                if(player==null || !Guid.TryParse(player.record.id,out _) || !players.Add(player.record.id)) return "Player GUID is missing, invalid or belongs to multiple teams.";
                membership.Add(player.record.id);
                if(string.IsNullOrWhiteSpace(player.record.displayName)) return "Player name is required.";
                if(player.record.jerseyNumber<0 || !numbers.Add(player.record.jerseyNumber)) return $"{Settings.Team(team.teamId).identity.teamName}: jersey number {player.record.jerseyNumber} is already used or invalid.";
                if(RosterRatings.Get(player.record).Any(r=>r<0 || r>20)) return "All twelve ratings must be 0–20.";
                if(player.record.appearance!=null || player.appearance==null) return "Appearance must be a portable selection, not a Unity asset reference.";
                var visual=ScriptableObject.CreateInstance<PlayerAppearanceDefinition>();
                player.appearance.Apply(visual,Settings.appearances,player.record.id);
                var errors=Settings.appearances.Validate(visual,out _,false);
                UnityEngine.Object.DestroyImmediate(visual);
                if(errors.Count>0) return player.record.displayName+": "+errors[0];
            }
            var activeIds=new HashSet<string>(); var offense=new HashSet<TeamMember.RosterSlot>(); var defense=new HashSet<int>();
            foreach(var slot in team.active)
                if(slot==null || !membership.Contains(slot.playerId) || !activeIds.Add(slot.playerId) ||
                    !Enum.IsDefined(typeof(TeamMember.RosterSlot),slot.offense) || !offense.Add(slot.offense) ||
                    slot.defense<0 || slot.defense>6 || !defense.Add(slot.defense)) return "Lineup contains missing players or duplicate/invalid offense or defense assignments.";
            if(team.active.Count>7) return "Only seven players may be active; all other players are reserves.";
        }
        return null;
    }

    public string Readiness(string teamId)
    {
        string integrity=Validate(data); if(integrity!=null) return integrity;
        var team=data.teams.Find(t=>t.teamId==teamId);
        if(team==null) return "Choose a team.";
        return team.active.Count==7?null:$"{Settings.Team(teamId).identity.teamName} has {team.active.Count}/7 active players. Assign all seven offense and defense slots in Players.";
    }

    bool Commit(RosterSaveData next, out string error)
    {
        error=Validate(next); if(error!=null) return false;
        try { store.Write(next); data=next; return true; }
        catch(Exception failure) { error="Nothing changed: roster save failed. "+failure.Message; return false; }
    }

    public int NextNumber(string teamId)
    {
        var numbers=data.teams.Find(t=>t.teamId==teamId).players.Select(p=>p.record.jerseyNumber).ToHashSet();
        int value=0; while(numbers.Contains(value)) value++;
        return value; // No arbitrary roster capacity or two-digit reserve-number cap.
    }

    public bool SavePlayer(string teamId, RosterPlayer draft, int offense, int defense, out string savedId, out string error)
    {
        savedId=null; error=null;
        var next=data.Copy(); var target=next.teams.Find(t=>t.teamId==teamId);
        if(target==null || draft==null) { error="Choose a valid team and player."; return false; }
        var value=draft.Copy();
        var source=next.teams.Find(t=>t.players.Any(p=>p.record.id==value.record.id));
        if(!string.IsNullOrEmpty(value.record.id) && source==null) { error="Player no longer exists; reopen the roster."; return false; }
        if(string.IsNullOrEmpty(value.record.id)) value.record.id=Guid.NewGuid().ToString();
        if(source!=null) { source.players.RemoveAll(p=>p.record.id==value.record.id); if(source!=target) source.active.RemoveAll(a=>a.playerId==value.record.id); }
        target.players.Add(value);
        Assign(target,value.record.id,offense,defense);
        if(!Commit(next,out error)) return false;
        savedId=value.record.id; return true;
    }

    static void Assign(SavedTeamRoster team,string id,int offense,int defense)
    {
        var old=team.active.Find(a=>a.playerId==id);
        if(offense<0) { team.active.RemoveAll(a=>a.playerId==id); return; }
        var replaced=team.active.Find(a=>(int)a.offense==offense && a.playerId!=id);
        int freed=old?.defense ?? replaced?.defense ?? Enumerable.Range(0,7).FirstOrDefault(n=>!team.active.Any(a=>a.defense==n));
        team.active.RemoveAll(a=>a.playerId==id || a==replaced);
        var swap=team.active.Find(a=>a.defense==defense);
        if(swap!=null) swap.defense=freed; // Independent defense assignment, never a hardcoded position pairing.
        team.active.Add(new LineupAssignment {playerId=id,offense=(TeamMember.RosterSlot)offense,defense=defense});
    }

    public bool SetActive(string teamId,string playerId,int offense,int defense,out string error)
    {
        var next=data.Copy(); var team=next.teams.Find(t=>t.teamId==teamId);
        if(team==null || !team.players.Any(p=>p.record.id==playerId)) {error="Player not found on this team.";return false;}
        Assign(team,playerId,offense,defense); return Commit(next,out error);
    }
    public bool Transfer(string id,string destination,out string error)
    {
        var source=data.teams.Find(t=>t.players.Any(p=>p.record.id==id));
        if(source==null) {error="Player not found.";return false;}
        var player=source.players.Find(p=>p.record.id==id);
        return SavePlayer(destination,player,-1,0,out _,out error);
    }
    public bool Delete(string id,out string error)
    {
        var next=data.Copy(); var team=next.teams.Find(t=>t.players.Any(p=>p.record.id==id));
        if(team==null) {error="Player not found.";return false;}
        team.active.RemoveAll(a=>a.playerId==id); team.players.RemoveAll(p=>p.record.id==id);
        return Commit(next,out error);
    }
    public bool RestoreDefaults(out string error) => Commit(Defaults(),out error);
    public bool SaveCurrent(out string error) => Commit(data.Copy(),out error);
}

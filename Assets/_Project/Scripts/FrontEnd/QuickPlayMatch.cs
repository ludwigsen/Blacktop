using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Legacy contract name retained: both exhibition modes use this one detached snapshot.
public sealed class QuickPlayMatch
{
    public FrontEndSettings Settings { get; }
    public ExhibitionMode Mode { get; }
    public string Team0Id { get; }
    public string Team1Id { get; }
    public int HumanSide { get; }
    public int RequestedHumanSide { get; }
    public string RequestedHumanTeam { get; }
    public string RequestedOpponentTeam { get; }
    public MatchConfiguration Configuration { get; }
    public FieldDefinition Field => Configuration.Field;
    readonly RosterSaveData snapshot;
    readonly Dictionary<string,string> sources;
    readonly Dictionary<string,AttributeCurves> curves;
    readonly MatchBrand[] brands;
    public RosterSaveData Snapshot => snapshot.Copy();
    public string SourceTeamFor(string id) => sources[id];
    public AttributeCurves CurvesFor(string id) => curves[id];
    public MatchBrand Brand(int side) => brands[side];
    QuickPlayMatch(RosterService service,ExhibitionMode mode,RosterSaveData squads,int human,int requestedSide,
        string humanRequest,string opponentRequest,MatchConfiguration configuration,Dictionary<string,string> sourceTeams,MatchBrand[] identities,
        Dictionary<string,AttributeCurves> frozenCurves=null)
    {
        Settings=service.Settings;Mode=mode;snapshot=squads.Copy();Team0Id=snapshot.teams[0].teamId;Team1Id=snapshot.teams[1].teamId;
        HumanSide=human;RequestedHumanSide=requestedSide;RequestedHumanTeam=humanRequest;RequestedOpponentTeam=opponentRequest;
        Configuration=configuration;sources=new Dictionary<string,string>(sourceTeams);brands=(MatchBrand[])identities.Clone();
        curves=frozenCurves!=null?new Dictionary<string,AttributeCurves>(frozenCurves):sources.ToDictionary(p=>p.Key,p=>Settings.Team(p.Value).roster.curves);
    }
    // Old callers delegate to Head 2 Head with the Clear/Normal baseline.
    public static bool TryCreate(RosterService service,string human,string opponent,int humanSide,FieldDefinition field,out QuickPlayMatch match,out string error)
        => TryHeadToHead(service,human,opponent,humanSide,new MatchOptions {fieldId=field?.venueId},new SeededMatchRandom(Environment.TickCount),out match,out error);

    public static bool TryHeadToHead(RosterService service,string human,string opponent,int side,MatchOptions options,IMatchRandom random,
        out QuickPlayMatch match,out string error)
    {
        match=null;error=service.Validate(service.Snapshot);if(error!=null)return false;
        if(side< -1 || side>1){error="Choose home, away or Random.";return false;}
        var eligible=service.Settings.teams.Where(t=>service.Readiness(t.id)==null).Select(t=>t.id).ToArray();
        string humanRequest=human,opponentRequest=opponent;
        if(string.IsNullOrEmpty(human))
        {
            var choices=eligible.Where(id=>id!=opponent).ToArray();
            if(choices.Length==0){error="Two different teams with seven valid active players are required. Manage rosters in PLAYERS.";return false;}
            human=choices[random.Next(choices.Length)];
        }
        if(string.IsNullOrEmpty(opponent))
        {
            var choices=eligible.Where(id=>id!=human).ToArray();
            if(choices.Length==0){error="No other team has seven valid active players. Manage rosters in PLAYERS.";return false;}
            opponent=choices[random.Next(choices.Length)];
        }
        error=human==opponent?"Choose two different teams.":service.Readiness(human)??service.Readiness(opponent);
        if(error!=null)return false;
        if(!MatchConfiguration.TryResolve(service.Settings,options,random,out var configuration,out error))return false;
        int resolvedSide=side== -1?random.Next(2):side;
        var saved=service.Snapshot;var squads=new RosterSaveData();
        squads.teams.Add(saved.teams.Single(t=>t.teamId==(resolvedSide==0?human:opponent)));
        squads.teams.Add(saved.teams.Single(t=>t.teamId==(resolvedSide==1?human:opponent)));
        var sourceTeams=squads.teams.SelectMany(t=>t.players.Select(p=>(p.record.id,t.teamId))).ToDictionary(x=>x.id,x=>x.teamId);
        match=new QuickPlayMatch(service,ExhibitionMode.HeadToHead,squads,resolvedSide,side,humanRequest,opponentRequest,configuration,sourceTeams,
            squads.teams.Select(t=>new MatchBrand(service.Settings.Team(t.teamId).identity)).ToArray());return true;
    }

    public static bool TryPickup(RosterService service,IEnumerable<string> humanIds,List<LineupAssignment> humanLineup,int side,
        MatchOptions options,IMatchRandom random,out QuickPlayMatch match,out string error)
    {
        match=null;var draft=new PickupDraft(service);error=draft.PoolError;
        var ids=humanIds?.ToArray()??Array.Empty<string>();
        if(error==null&&(ids.Length!=7||ids.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=7||ids.Any(id=>!draft.Pool.Any(p=>p.Id==id))))
            error="Draft exactly seven distinct saved players.";
        if(side< -1||side>1)error="Choose home, away or Random.";
        if(error!=null)return false;
        var humans=draft.Pool.Where(p=>ids.Contains(p.Id)).ToList();var human=Squad("human",humans,humanLineup);
        error=MatchLineups.Validate(human);if(error!=null)return false;
        if(!MatchConfiguration.TryResolve(service.Settings,options,random,out var configuration,out error))return false;
        var cpuPlayers=PickupDraft.DraftCpu(draft.Pool,ids,configuration.Cpu,random);
        var cpu=Squad("cpu",cpuPlayers,MatchLineups.Suggest(cpuPlayers));
        error=MatchLineups.Validate(cpu);if(error!=null)return false;
        int resolvedSide=side== -1?random.Next(2):side;
        var squads=new RosterSaveData();squads.teams.Add(resolvedSide==0?human:cpu);squads.teams.Add(resolvedSide==1?human:cpu);
        squads.teams[0].teamId="pickup.home";squads.teams[1].teamId="pickup.away";
        var all=humans.Concat(cpuPlayers).ToArray();
        if(all.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=14){error="Opposing squads cannot share a player GUID.";return false;}
        var settings=service.Settings;
        match=new QuickPlayMatch(service,ExhibitionMode.Pickup,squads,resolvedSide,side,null,null,configuration,
            all.ToDictionary(p=>p.Id,p=>p.SourceTeamId),new[] {
                new MatchBrand(settings.pickupHomeStyle??settings.teams[0].identity,"PICKUP HOME"),
                new MatchBrand(settings.pickupAwayStyle??settings.teams[1].identity,"PICKUP AWAY") });return true;
    }
    static SavedTeamRoster Squad(string id,IEnumerable<PickupPlayer> players,List<LineupAssignment> lineup)=>new SavedTeamRoster {
        teamId=id,players=players.Select(p=>p.Player).ToList(),active=lineup?.Select(a=>new LineupAssignment {
            playerId=a.playerId,offense=a.offense,defense=a.defense}).ToList() };

    public bool TryWithLineups(List<LineupAssignment> side0,List<LineupAssignment> side1,RosterService service,out QuickPlayMatch result,out string error)
    {
        result=null;var data=Snapshot;data.teams[0].active=side0;data.teams[1].active=side1;
        error=MatchLineups.Validate(data.teams[0])??MatchLineups.Validate(data.teams[1]);if(error!=null)return false;
        result=new QuickPlayMatch(service,Mode,data,HumanSide,RequestedHumanSide,RequestedHumanTeam,RequestedOpponentTeam,Configuration,sources,brands,curves);return true;
    }
}

// Branding is frozen as values; Bootstrap owns disposable Unity presentation objects.
public sealed class MatchBrand
{
    public string Name {get;}
    public Color Primary {get;}
    public Color Secondary {get;}
    public Sprite Logo {get;}
    public MatchBrand(TeamIdentity source,string name=null){Name=name??source.teamName;Primary=source.accentColor;Secondary=source.secondaryAccentColor;Logo=source.logo;}
    public TeamIdentity CreateRuntime()
    {
        var value=ScriptableObject.CreateInstance<TeamIdentity>();value.teamName=Name;value.accentColor=Primary;
        value.secondaryAccentColor=Secondary;value.logo=Logo;return value;
    }
}

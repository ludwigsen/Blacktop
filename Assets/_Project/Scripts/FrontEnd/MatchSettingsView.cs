using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Both modes render this same component while keeping independent MatchOptions instances.
public static class MatchSettingsView
{
    public static readonly string[] DefenseNames={"EDGE-L","EDGE-R","LB-L","LB-R","DB-L","DB-R","S"};
    public static void Build(FrontEndController menu,RectTransform root)
    {
        Header(root,menu,menu.Mode==ExhibitionMode.Pickup?"PICKUP / MATCH SETTINGS":"HEAD 2 HEAD / MATCH SETTINGS");
        var area=FrontEndUI.Panel(root,"Settings",new Vector2(.1f,.13f),new Vector2(.9f,.87f),Color.clear);
        var list=FrontEndUI.Scroll(area);var options=menu.Options;
        string team(string id)=>string.IsNullOrEmpty(id)?"RANDOM":menu.settings.Team(id)?.identity.teamName;
        FrontEndUI.Text(list,menu.Mode==ExhibitionMode.Pickup?"YOUR SEVEN  vs  CPU DRAFT  /  FIRST TO 21":$"YOU: {team(menu.HumanTeam)}    vs    CPU: {team(menu.OpponentTeam)}",menu.settings,32,true,64);
        FrontEndUI.Picker(list,"Your side",new[]{"RANDOM","HOME / FIRST POSSESSION","AWAY / DEFEND FIRST"},menu.MatchSide+1,i=>menu.MatchSide=i-1,menu.settings);
        var fields=MatchConfiguration.PlayableFields(menu.settings);
        FrontEndUI.Picker(list,"Field",new[]{"RANDOM"}.Concat(fields.Select(f=>f.displayName)).ToArray(),
            Array.FindIndex(fields,f=>f.venueId==options.fieldId)+1,i=>options.fieldId=i==0?null:fields[i-1].venueId,menu.settings);
        var conditions=Enum.GetValues(typeof(MatchCondition)).Cast<MatchCondition>().ToArray();
        var severityRow=default(RectTransform);
        FrontEndUI.Picker(list,"Condition",conditions.Select(c=>c.ToString().ToUpperInvariant()).ToArray(),(int)options.condition,i=>{
            options.condition=(MatchCondition)i;UpdateSeverity();},menu.settings);
        severityRow=FrontEndUI.Vertical(list,"Severity",0);severityRow.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=62;
        FrontEndUI.Picker(severityRow,"Severity",new[]{"CLEAN / 0.5x","REGULAR / 1.0x","HARD / 1.8x"},(int)options.severity,i=>options.severity=(ConditionSeverity)i,menu.settings);
        FrontEndUI.Picker(list,"Difficulty",new[]{"RANDOM","EASY","NORMAL","HARD","STREET LEGEND"},(int)options.difficulty,i=>options.difficulty=(MatchDifficulty)i,menu.settings);
        UpdateSeverity();
        FrontEndUI.Text(list,"Random resolves on review. Clear ignores severity. Rematch keeps every resolved choice.",menu.settings,23,false,58);
        FrontEndUI.Text(list,menu.Message,menu.settings,24,false,60);
        var review=FrontEndUI.Button(root,"REVIEW MATCH",menu.PrepareMatch,menu.settings,82);
        FrontEndUI.Anchor(review.GetComponent<RectTransform>(),new Vector2(.65f,0),new Vector2(1,.1f));
        if(menu.Mode==ExhibitionMode.HeadToHead)
        {
            var manage=FrontEndUI.Button(root,"MANAGE ROSTERS",menu.ShowTeams,menu.settings);
            FrontEndUI.Anchor(manage.GetComponent<RectTransform>(),new Vector2(.25f,0),new Vector2(.6f,.1f));
        }
        Back(menu,root);
        void UpdateSeverity()
        {
            if(severityRow==null)return;
            foreach(var button in severityRow.GetComponentsInChildren<UnityEngine.UI.Button>())button.interactable=options.condition!=MatchCondition.Clear;
        }
    }
    public static void HumanLineup(FrontEndController menu,RectTransform root)
    {
        Header(root,menu,"PICKUP / ASSIGN YOUR LINEUP");
        var area=FrontEndUI.Panel(root,"Lineup",new Vector2(.08f,.15f),new Vector2(.92f,.87f),Color.clear);
        var players=menu.Draft.Pool.Where(p=>menu.Draft.Selected.Contains(p.Id)).Select(p=>p.Player).ToList();
        Lineup(menu,FrontEndUI.Scroll(area),new SavedTeamRoster {players=players,active=menu.PickupLineup},true,()=>{});
        var next=FrontEndUI.Button(root,"MATCH SETTINGS",menu.ShowSettings,menu.settings,70);
        FrontEndUI.Anchor(next.GetComponent<RectTransform>(),new Vector2(.65f,0),new Vector2(1,.1f));Back(menu,root);
    }
    public static void Review(FrontEndController menu,RectTransform root)
    {
        var match=menu.PreparedMatch;Header(root,menu,match.Mode==ExhibitionMode.Pickup?"PICKUP / READY TO RUN?":"HEAD 2 HEAD / READY TO RUN?");
        var cfg=match.Configuration;
        var summary=FrontEndUI.Text(root,$"{cfg.Field.displayName}  /  {cfg.Condition}  /  {(cfg.Condition==MatchCondition.Clear?"SEVERITY IGNORED":cfg.Severity.ToString())}  /  {DifficultyName(cfg.Difficulty)}",menu.settings,26);
        FrontEndUI.Anchor(summary.rectTransform,new Vector2(0,.81f),new Vector2(1,.88f));
        var data=match.Snapshot;
        for(int side=0;side<2;side++)
        {
            var team=data.teams[side];
            var panel=FrontEndUI.Panel(root,"Squad",new Vector2(side*.51f,.14f),new Vector2(side*.51f+.49f,.8f),Color.clear);
            var list=FrontEndUI.Scroll(panel);
            FrontEndUI.Text(list,$"{(side==0?"HOME":"AWAY")} / {(side==match.HumanSide?"YOU":"CPU")} / {match.Brand(side).Name}",menu.settings,30,true,70);
            Lineup(menu,list,team,match.Mode==ExhibitionMode.Pickup,()=>menu.UpdatePreparedLineups(data.teams[0].active,data.teams[1].active));
        }
        var start=FrontEndUI.Button(root,"START MATCH",menu.StartMatch,menu.settings,78);
        FrontEndUI.Anchor(start.GetComponent<RectTransform>(),new Vector2(.65f,0),new Vector2(1,.1f));Back(menu,root);
    }
    static string DifficultyName(MatchDifficulty value)=>value==MatchDifficulty.StreetLegend?"STREET LEGEND":value.ToString().ToUpperInvariant();
    static void Lineup(FrontEndController menu,Transform list,SavedTeamRoster team,bool editable,Action changed)
    {
        var setters=new Dictionary<string,(Action<int> offense,Action<int> defense)>();
        foreach(var assignment in team.active.OrderBy(a=>a.offense))
        {
            var player=team.players.Single(p=>p.record.id==assignment.playerId);
            FrontEndUI.Text(list,$"#{player.record.jerseyNumber} {player.record.displayName}",menu.settings,26,false,44);
            if(!editable)
            {FrontEndUI.Text(list,$"{assignment.offense}  /  {DefenseNames[assignment.defense]}",menu.settings,23,false,44);continue;}
            var offense=FrontEndUI.Picker(list,"Offense",Enum.GetNames(typeof(TeamMember.RosterSlot)),(int)assignment.offense,i=>Swap(assignment.playerId,i,false),menu.settings);
            var defense=FrontEndUI.Picker(list,"Defense",DefenseNames,assignment.defense,i=>Swap(assignment.playerId,i,true),menu.settings);
            setters[assignment.playerId]=(offense,defense);
        }
        void Swap(string id,int slot,bool defense)
        {
            MatchLineups.Swap(team.active,id,slot,defense);
            foreach(var a in team.active){setters[a.playerId].offense((int)a.offense);setters[a.playerId].defense(a.defense);}
            changed();
        }
    }
    static void Header(RectTransform root,FrontEndController menu,string title)
    {var text=FrontEndUI.Text(root,title,menu.settings,48,true);FrontEndUI.Anchor(text.rectTransform,new Vector2(0,.89f),Vector2.one);}
    static void Back(FrontEndController menu,RectTransform root)
    {var back=FrontEndUI.Button(root,"BACK",menu.Back,menu.settings);FrontEndUI.Anchor(back.GetComponent<RectTransform>(),Vector2.zero,new Vector2(.23f,.1f));}
}

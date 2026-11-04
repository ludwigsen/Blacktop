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
        Header(root,menu,menu.Mode==ExhibitionMode.Pickup?"PICKUP / BUILD YOUR MATCH":"HEAD 2 HEAD / BUILD YOUR MATCH");
        var options=menu.Options;
        string team(string id)=>string.IsNullOrEmpty(id)?"RANDOM":menu.settings.Team(id)?.identity.teamName;
        var summary=FrontEndUI.Text(root,menu.Mode==ExhibitionMode.Pickup?"YOUR SEVEN  vs  CPU DRAFT  /  FIRST TO 21":$"YOU: {team(menu.HumanTeam)}    vs    CPU: {team(menu.OpponentTeam)}",menu.settings,30,true);
        FrontEndUI.Anchor(summary.rectTransform,new Vector2(.02f,.80f),new Vector2(.60f,.88f));
        var side=FrontEndUI.Vertical(root,"Possession choice",0);FrontEndUI.Anchor(side,new Vector2(.60f,.80f),new Vector2(.98f,.88f));
        FrontEndUI.Picker(side,"Your side",new[]{"RANDOM","HOME / FIRST POSSESSION","AWAY / DEFEND FIRST"},menu.MatchSide+1,i=>menu.MatchSide=i-1,menu.settings);
        var field=Card("03 / FIELD",0,.33f);var modifier=Card("04 / MODIFIER",.345f,.675f);var difficulty=Card("05 / DIFFICULTY",.69f,1);
        var fields=MatchConfiguration.PlayableFields(menu.settings);
        var fieldButtons=new List<UnityEngine.UI.Button>();
        AddField("RANDOM",null);
        foreach(var venue in fields)AddField(venue.displayName.ToUpperInvariant(),venue.venueId);
        FrontEndUI.Text(field,"Choose the block. The modifier is selected independently.",menu.settings,24,false,80);
        var conditions=Enum.GetValues(typeof(MatchCondition)).Cast<MatchCondition>().ToArray();
        RectTransform severity=null;
        FrontEndUI.Picker(modifier,"Condition",conditions.Select(c=>c.ToString().ToUpperInvariant()).ToArray(),(int)options.condition,i=>{
            options.condition=(MatchCondition)i;UpdateSeverity();},menu.settings);
        severity=FrontEndUI.Vertical(modifier,"Severity",0);severity.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=72;
        FrontEndUI.Picker(severity,"Severity",new[]{"CLEAN / 0.5x","REGULAR / 1.0x","HARD / 1.8x"},(int)options.severity,i=>options.severity=(ConditionSeverity)i,menu.settings);
        FrontEndUI.Text(modifier,"Clear ignores severity. Conditions apply equally to both squads.",menu.settings,24,false,120);
        var difficultyButtons=new List<UnityEngine.UI.Button>();
        foreach(MatchDifficulty choice in Enum.GetValues(typeof(MatchDifficulty)))
        {
            var selected=choice;
            var button=FrontEndUI.Button(difficulty,DifficultyName(choice),()=>{
                options.difficulty=selected;
                for(int i=0;i<difficultyButtons.Count;i++)difficultyButtons[i].GetComponent<ThemeButton>().SetSelected((int)selected==i);
            },menu.settings,62);
            button.GetComponent<ThemeButton>().SetSelected(choice==options.difficulty);difficultyButtons.Add(button);
        }
        UpdateSeverity();
        var status=FrontEndUI.Text(root,"Random resolves on review. Rematch keeps every resolved choice.  "+menu.Message,menu.settings,22);
        FrontEndUI.Anchor(status.rectTransform,new Vector2(.02f,.115f),new Vector2(.98f,.20f));
        var review=FrontEndUI.Button(root,"REVIEW MATCH",menu.PrepareMatch,menu.settings,82);
        FrontEndUI.Anchor(review.GetComponent<RectTransform>(),new Vector2(.65f,0),new Vector2(1,.1f));
        if(menu.Mode==ExhibitionMode.HeadToHead)
        {
            var manage=FrontEndUI.Button(root,"MANAGE ROSTERS",menu.ShowTeams,menu.settings);
            FrontEndUI.Anchor(manage.GetComponent<RectTransform>(),new Vector2(.25f,0),new Vector2(.6f,.1f));
        }
        Back(menu,root);
        var progress=root.GetComponentInParent<FrontEndController>().GetComponentInChildren<SetupProgress>();
        // Spatial focus follows the same Field → Modifier → Difficulty order as the setup cards.
        for(int step=2;step<=4;step++)
        {
            var section=step==2?field:step==3?modifier:difficulty;
            foreach(var control in section.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {var focus=control.gameObject.AddComponent<SetupStepFocus>();focus.progress=progress;focus.step=step;}
        }
        RectTransform Card(string name,float left,float right)
        {
            var panel=FrontEndUI.Panel(root,name,new Vector2(left,.22f),new Vector2(right,.78f),UIColorRole.SurfacePrimary);
            var list=FrontEndUI.Vertical(panel,name,20);FrontEndUI.Anchor(list,Vector2.zero,Vector2.one);
            list.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=9;
            FrontEndUI.Text(list,name,menu.settings,36,true,66);return list;
        }
        void AddField(string name,string id)
        {
            var button=FrontEndUI.Button(field,name,()=>{
                options.fieldId=id;
                for(int i=0;i<fieldButtons.Count;i++)fieldButtons[i].GetComponent<ThemeButton>().SetSelected(i==0?id==null:fields[i-1].venueId==id);
            },menu.settings,82);
            button.GetComponent<ThemeButton>().SetSelected(options.fieldId==id);fieldButtons.Add(button);
        }
        void UpdateSeverity()
        {
            if(severity==null)return;
            foreach(var button in severity.GetComponentsInChildren<UnityEngine.UI.Button>())button.interactable=options.condition!=MatchCondition.Clear;
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

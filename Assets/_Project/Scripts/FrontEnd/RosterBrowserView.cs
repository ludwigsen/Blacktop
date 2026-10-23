using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Rows select GUIDs; roster ordering never determines gameplay slots or identity.
public static class RosterBrowserView
{
    public static void Teams(FrontEndController menu,RectTransform root)
    {
        var list=FrontEndUI.Vertical(root);FrontEndUI.Anchor(list,Vector2.zero,Vector2.one);
        FrontEndUI.Text(list,"PLAYERS / CHOOSE A TEAM",menu.settings,52,true,90);
        foreach(var team in menu.settings.teams)
        {
            var roster=menu.Service.Team(team.id);
            FrontEndUI.Button(list,$"{team.identity.teamName.ToUpperInvariant()}   /   {roster.players.Count} PLAYERS   /   {roster.active.Count}/7 ACTIVE",()=>menu.ShowRoster(team.id),menu.settings,100);
        }
        FrontEndUI.Button(list,"BACK TO MAIN MENU",menu.ShowMain,menu.settings);
        FrontEndUI.Text(list,menu.Message,menu.settings,23,false,120);
    }
    public static void Build(FrontEndController menu,RectTransform root,string teamId)
    {
        var settings=menu.settings;var roster=menu.Service.Team(teamId);string selected=roster.players.Any(p=>p.record.id==menu.SelectedPlayerId)?menu.SelectedPlayerId:roster.players.FirstOrDefault()?.record.id;
        var heading=FrontEndUI.Text(root,$"{settings.Team(teamId).identity.teamName.ToUpperInvariant()} / PLAYER LAB",settings,48,true);
        FrontEndUI.Anchor(heading.rectTransform,new Vector2(0,.9f),Vector2.one);
        var area=FrontEndUI.Panel(root,"Roster",new Vector2(0,.14f),new Vector2(.65f,.88f),Color.clear);
        var list=FrontEndUI.Scroll(area);
        var side=FrontEndUI.Panel(root,"Actions",new Vector2(.68f,.14f),new Vector2(1,.88f),new Color(.09f,.11f,.13f));
        var actions=FrontEndUI.Scroll(side,"Roster actions");
        var detail=FrontEndUI.Text(actions,"",settings,25,false,110);
        foreach(var player in roster.players)
        {
            var slot=roster.active.Find(a=>a.playerId==player.record.id);
            string dimensions=$"{HeightLabel(player.appearance.height)} / {settings.weights.Pounds(player.appearance.height,player.appearance.bodyArchetype)} LB / {player.appearance.bodyArchetype}";
            string role=slot==null?"RESERVE":$"ACTIVE {slot.offense} / DEF {DefenseNames[slot.defense]}";
            var entry=FrontEndUI.Button(list,$"#{player.record.jerseyNumber}  {player.record.displayName}\n{dimensions}   ·   {role}",()=>{selected=player.record.id;menu.SelectedPlayerId=selected;Detail();},settings,103);
            if(player.record.id==selected)menu.PreferredFocus=entry.gameObject;
        }
        if(roster.players.Count==0)FrontEndUI.Text(list,"NO PLAYERS YET. CREATE ONE TO BEGIN.",settings,28,false,140);
        FrontEndUI.Button(actions,"CREATE PLAYER",menu.CreatePlayer,settings);
        FrontEndUI.Button(actions,"EDIT SELECTED",()=>{var p=Chosen();if(p!=null)menu.Edit(p);},settings).interactable=selected!=null;
        FrontEndUI.Button(actions,"DUPLICATE",()=>{var p=Chosen();if(p!=null)menu.Edit(p,true);},settings).interactable=selected!=null;
        int offense=0,defense=0;
        FrontEndUI.Picker(actions,"Offense",Enum.GetNames(typeof(TeamMember.RosterSlot)),0,i=>offense=i,settings);
        FrontEndUI.Picker(actions,"Defense",DefenseNames,0,i=>defense=i,settings);
        FrontEndUI.Button(actions,"ASSIGN / REPLACE SLOT",()=>{
            var p=Chosen();if(p==null)return;
            menu.Confirm("SET ACTIVE ASSIGNMENT?",$"{p.record.displayName} → {((TeamMember.RosterSlot)offense)} / {DefenseNames[defense]}. The prior offensive-slot occupant becomes a reserve; defense swaps when needed.",()=>{
                menu.Service.SetActive(teamId,selected,offense,defense,out var error);Refresh(error??"Active lineup saved.");},"ASSIGN");},settings).interactable=selected!=null;
        FrontEndUI.Button(actions,"MOVE TO RESERVES",()=>{if(selected==null)return;menu.Service.SetActive(teamId,selected,-1,0,out var error);Refresh(error??"Player moved to reserves.");},settings).interactable=selected!=null;
        var destinations=settings.teams.Where(t=>t.id!=teamId).ToArray();int destination=0;
        FrontEndUI.Picker(actions,"Transfer to",destinations.Select(t=>t.identity.teamName).ToArray(),0,i=>destination=i,settings);
        FrontEndUI.Button(actions,"TRANSFER SELECTED",()=>{
            var p=Chosen();if(p==null||destinations.Length==0)return;
            menu.Confirm("TRANSFER PLAYER?",$"Move {p.record.displayName} to {destinations[destination].identity.teamName} reserves? Identity, ratings and appearance are preserved.",()=>{
                menu.Service.Transfer(selected,destinations[destination].id,out var error);Refresh(error??"Transfer saved.");},"TRANSFER");},settings).interactable=selected!=null&&destinations.Length>0;
        FrontEndUI.Button(actions,"DELETE SELECTED",()=>{
            var p=Chosen();if(p==null)return;
            menu.Confirm("DELETE PLAYER?",$"Delete {p.record.displayName} from the runtime roster and active lineup?",()=>{
                menu.Service.Delete(selected,out var error);Refresh(error??"Player deleted.");},"DELETE PLAYER");},settings).interactable=selected!=null;
        FrontEndUI.Button(actions,"SAVE RUNTIME ROSTERS",()=>{menu.Service.SaveCurrent(out var error);Refresh(error??"Runtime save written. Source assets unchanged.");},settings);
        if(Application.isEditor)FrontEndUI.Button(actions,"COMMIT TO SOURCE (EDITOR)",()=>menu.Confirm("WRITE CANONICAL ROSTERS?","Commit all validated saved teams, appearances and lineups to development assets. This is separate from the local runtime save.",()=>{
            FrontEndRuntime.SourceCommitMessage=null;FrontEndRuntime.SourceCommitRequested?.Invoke(menu.Service);
            Refresh(FrontEndRuntime.SourceCommitMessage??"Editor source-commit handler is not available. Use Blacktop > Front End > Commit Saved Rosters to Source.");},"COMMIT SOURCE ASSETS"),settings,84);
        FrontEndUI.Button(actions,"RESTORE DEFAULT ROSTERS",()=>menu.Confirm("RESTORE ALL DEFAULTS?","Replace local runtime rosters with the canonical source assets for every team. This removes local-only players.",()=>{
            menu.Service.RestoreDefaults(out var error);Refresh(error??"Default rosters restored.");},"RESTORE DEFAULTS"),settings,84);
        var back=FrontEndUI.Button(root,"BACK TO TEAMS",menu.Back,settings);FrontEndUI.Anchor(back.GetComponent<RectTransform>(),Vector2.zero,new Vector2(.25f,.1f));
        string summary=menu.Service.Readiness(teamId)??$"{roster.players.Count} PLAYERS / 7 ACTIVE / {roster.players.Count-7} RESERVES";
        var status=FrontEndUI.Text(root,summary+(string.IsNullOrEmpty(menu.Message)?"":"\n"+menu.Message),settings,22,false,88);
        FrontEndUI.Anchor(status.rectTransform,new Vector2(.28f,0),new Vector2(1,.1f));
        if(!string.IsNullOrEmpty(menu.Message))FrontEndUI.Text(actions,menu.Message,settings,22,false,150);
        Detail();
        RosterPlayer Chosen() => roster.players.Find(p=>p.record.id==selected);
        void Detail(){var p=Chosen();detail.text=p==null?"SELECT A PLAYER":$"#{p.record.jerseyNumber} {p.record.displayName}\n{settings.Team(teamId).identity.teamName}";}
        void Refresh(string message){menu.Message=message;menu.ShowRoster(teamId);}
    }
    public static readonly string[] DefenseNames={"EDGE-L","EDGE-R","LB-L","LB-R","DB-L","DB-R","S"};
    public static string HeightLabel(PlayerHeight value)
    {
        string name=value.ToString().Replace("Ft","").Replace("In","′");return name+"″";
    }
}

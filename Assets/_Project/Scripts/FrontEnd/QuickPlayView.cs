using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Team cards read saved roster state; match creation still revalidates the full snapshot.
public static class QuickPlayView
{
    public static void Build(FrontEndController menu,RectTransform root,FrontEndScreen stage)
    {
        if(stage==FrontEndScreen.Confirmation){MatchSettingsView.Build(menu,root);return;}
        var heading=FrontEndUI.Text(root,stage==FrontEndScreen.HumanTeam?"SELECT YOUR TEAM":stage==FrontEndScreen.Opponent?"SELECT OPPONENT":"READY TO RUN?",menu.settings,52,true);
        FrontEndUI.Anchor(heading.rectTransform,new Vector2(0,.88f),Vector2.one);
        var area=FrontEndUI.Panel(root,"Choices",new Vector2(0,.13f),new Vector2(1,.86f),Color.clear);
        if(stage!=FrontEndScreen.Confirmation)
        {
            var list=FrontEndUI.Scroll(area);
            FrontEndUI.Button(list,"RANDOM TEAM",()=>{
                if(stage==FrontEndScreen.HumanTeam){menu.HumanTeam=null;menu.ShowQuickPlay(FrontEndScreen.Opponent);}
                else {menu.OpponentTeam=null;menu.ShowSettings();}},menu.settings,80);
            foreach(var definition in menu.settings.teams)
            {
                var team=menu.Service.Team(definition.id);var card=FrontEndUI.Row(list,154);
                var logo=FrontEndUI.Panel(card,"Crest",Vector2.zero,Vector2.one,definition.identity.accentColor);
                logo.gameObject.AddComponent<LayoutElement>().preferredWidth=140;logo.GetComponent<Image>().sprite=definition.identity.logo;
                logo.GetComponent<Image>().preserveAspect=true;
                string readiness=menu.Service.Readiness(definition.id);
                string label=$"{definition.identity.teamName.ToUpperInvariant()}  /  {team.players.Count} PLAYERS  /  {team.active.Count}/7 ACTIVE\n"+(readiness??"READY TO PLAY");
                var choose=FrontEndUI.Button(card,label,()=>{
                    if(stage==FrontEndScreen.HumanTeam){menu.HumanTeam=definition.id;menu.ShowQuickPlay(FrontEndScreen.Opponent);}
                    else {menu.OpponentTeam=definition.id;menu.Message="";menu.ShowSettings();}},menu.settings,154);
                choose.interactable=readiness==null&&!(stage==FrontEndScreen.Opponent&&definition.id==menu.HumanTeam);
                if(readiness!=null)FrontEndUI.Button(card,"MANAGE ROSTER",()=>menu.ShowRoster(definition.id),menu.settings,154);
            }
        }
        var back=FrontEndUI.Button(root,"BACK",menu.Back,menu.settings);FrontEndUI.Anchor(back.GetComponent<RectTransform>(),Vector2.zero,new Vector2(.23f,.09f));
    }
}

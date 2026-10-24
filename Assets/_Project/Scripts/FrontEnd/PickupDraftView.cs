using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Preview, pool filtering and draft slots share one live screen; no saved roster mutation.
public sealed class PickupDraftView : MonoBehaviour
{
    FrontEndController menu;
    RectTransform poolList,slots;
    RuntimePlayerPreview preview;
    TextMeshProUGUI details,remaining;
    Button draftButton,next;
    PickupPlayer selected;
    string search,teamFilter;
    PickupSort sort;
    public void Initialize(FrontEndController controller,RectTransform root)
    {
        menu=controller;
        var title=FrontEndUI.Text(root,"PICKUP / DRAFT YOUR SEVEN",menu.settings,48,true);
        FrontEndUI.Anchor(title.rectTransform,new Vector2(0,.89f),Vector2.one);
        var left=FrontEndUI.Panel(root,"Saved player pool",new Vector2(0,.15f),new Vector2(.42f,.87f),Color.clear);
        var filters=FrontEndUI.Vertical(left,"Filters",0);FrontEndUI.Anchor(filters,new Vector2(0,.67f),Vector2.one);
        FrontEndUI.Input(filters,"Search / #","",value=>{search=value;RefreshPool();},menu.settings);
        var teams=menu.settings.teams;
        FrontEndUI.Picker(filters,"Team",new[]{"ALL TEAMS"}.Concat(teams.Select(t=>t.identity.teamName)).ToArray(),0,i=>{teamFilter=i==0?null:teams[i-1].id;RefreshPool();},menu.settings);
        FrontEndUI.Picker(filters,"Sort",new[]{"NAME","RATING","ROLE"},0,i=>{sort=(PickupSort)i;RefreshPool();},menu.settings);
        var pool=FrontEndUI.Panel(left,"Pool",Vector2.zero,new Vector2(1,.65f),Color.clear);poolList=FrontEndUI.Scroll(pool);
        var middle=FrontEndUI.Panel(root,"Player details",new Vector2(.44f,.15f),new Vector2(.74f,.87f),Color.clear);
        var raw=FrontEndUI.PreviewSurface(middle,new Vector2(0,.5f),Vector2.one);
        preview=raw.gameObject.AddComponent<RuntimePlayerPreview>();preview.Initialize(menu.settings);
        details=FrontEndUI.Text(middle,"Select a saved player to inspect all twelve attributes.",menu.settings,21);
        FrontEndUI.Anchor(details.rectTransform,Vector2.zero,new Vector2(1,.4f));
        draftButton=FrontEndUI.Button(middle,"DRAFT SELECTED",ToggleSelected,menu.settings);
        FrontEndUI.Anchor(draftButton.GetComponent<RectTransform>(),new Vector2(0,.41f),new Vector2(1,.49f));
        var right=FrontEndUI.Panel(root,"Your seven",new Vector2(.76f,.15f),new Vector2(1,.87f),Color.clear);
        var heading=FrontEndUI.Text(right,"YOUR SQUAD",menu.settings,32,true);
        FrontEndUI.Anchor(heading.rectTransform,new Vector2(0,.91f),Vector2.one);
        var slotArea=FrontEndUI.Panel(right,"Drafted slots",new Vector2(0,.23f),new Vector2(1,.90f),Color.clear);slots=FrontEndUI.Scroll(slotArea);
        var slotLayout=slots.GetComponent<VerticalLayoutGroup>();slotLayout.padding=new RectOffset(0,0,0,0);slotLayout.spacing=6;
        remaining=FrontEndUI.Text(right,"",menu.settings,21);FrontEndUI.Anchor(remaining.rectTransform,new Vector2(0,.13f),new Vector2(1,.22f));
        next=FrontEndUI.Button(right,"ASSIGN LINEUP",menu.ShowLineup,menu.settings);FrontEndUI.Anchor(next.GetComponent<RectTransform>(),Vector2.zero,new Vector2(1,.12f));
        var back=FrontEndUI.Button(root,"BACK",menu.Back,menu.settings);FrontEndUI.Anchor(back.GetComponent<RectTransform>(),Vector2.zero,new Vector2(.2f,.1f));
        var manage=FrontEndUI.Button(root,"MANAGE PLAYERS",menu.ShowTeams,menu.settings);FrontEndUI.Anchor(manage.GetComponent<RectTransform>(),new Vector2(.22f,0),new Vector2(.47f,.1f));
        var rotate=FrontEndUI.Button(root,"ROTATE PREVIEW",()=>preview.Rotate(45f),menu.settings);FrontEndUI.Anchor(rotate.GetComponent<RectTransform>(),new Vector2(.49f,0),new Vector2(.74f,.1f));
        var reset=FrontEndUI.Button(root,"RESET VIEW",preview.ResetCamera,menu.settings);FrontEndUI.Anchor(reset.GetComponent<RectTransform>(),new Vector2(.76f,0),new Vector2(1,.1f));
        var status=FrontEndUI.Text(root,menu.Draft.PoolError??"CPU drafts from the remaining saved players at match review.",menu.settings,22);
        FrontEndUI.Anchor(status.rectTransform,new Vector2(0,.105f),new Vector2(1,.15f));
        RefreshPool();RefreshSlots();
        if(menu.Draft.Pool.Count>0)Select(menu.Draft.Pool[0]);
    }
    void RefreshPool()
    {
        Clear(poolList);
        foreach(var player in menu.Draft.Browse(search,teamFilter,sort))
        {
            var record=player.Player.record;bool drafted=menu.Draft.Selected.Contains(player.Id);
            FrontEndUI.Button(poolList,$"{(drafted?"[IN] ":"")}#{record.jerseyNumber} {record.displayName}\n{player.SourceTeamName} / {player.Role} / OVR {player.Overall:0}",()=>Select(player),menu.settings,100);
        }
    }
    void Select(PickupPlayer player)
    {
        selected=player;var value=player.Player;preview.Apply(value,menu.settings.Team(player.SourceTeamId).identity);
        var ratings=RosterRatings.Get(value.record);
        details.text=$"#{value.record.jerseyNumber} {value.record.displayName}\n{player.SourceTeamName} / {player.Role}\nEST. OVERALL {player.Overall:0} / BROWSING ONLY\n"+
            string.Join("\n",Enumerable.Range(0,6).Select(i=>$"{RosterRatings.Names[i]} {ratings[i]}   |   {RosterRatings.Names[i+6]} {ratings[i+6]}"));
        RefreshAction();
    }
    void ToggleSelected()
    {
        if(selected==null)return;
        if(menu.Draft.Selected.Contains(selected.Id))menu.Draft.Remove(selected.Id);
        else if(!menu.Draft.Add(selected.Id,out var error)){remaining.text=error;return;}
        menu.DraftChanged();RefreshPool();RefreshSlots();
    }
    void RefreshSlots()
    {
        Clear(slots);
        for(int i=0;i<7;i++)
        {
            string id=i<menu.Draft.Selected.Count?menu.Draft.Selected[i]:null;
            var player=menu.Draft.Pool.FirstOrDefault(p=>p.Id==id);
            var button=FrontEndUI.Button(slots,player==null?$"{i+1} / EMPTY SLOT":$"{i+1} / {player.Player.record.displayName}",()=>{
                menu.Draft.Remove(id);menu.DraftChanged();RefreshPool();RefreshSlots();},menu.settings,52);
            if(player!=null)button.name+=" / REMOVE";
            button.GetComponentInChildren<TMP_Text>().fontSize=23;
            button.interactable=player!=null;
        }
        remaining.text=menu.Draft.PoolError!=null?"DRAFT UNAVAILABLE":$"{7-menu.Draft.Selected.Count} PICKS REMAIN\nSELECT SLOT TO REMOVE / REPLACE";
        next.interactable=menu.Draft.PoolError==null&&menu.Draft.Selected.Count==7;RefreshAction();
    }
    void RefreshAction()
    {
        bool drafted=selected!=null&&menu.Draft.Selected.Contains(selected.Id);
        draftButton.GetComponentInChildren<TMP_Text>().text=drafted?"REMOVE SELECTED":"DRAFT SELECTED";
        draftButton.interactable=menu.Draft.PoolError==null&&selected!=null&&(drafted||menu.Draft.Selected.Count<7);
    }
    static void Clear(Transform parent)
    {foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
}

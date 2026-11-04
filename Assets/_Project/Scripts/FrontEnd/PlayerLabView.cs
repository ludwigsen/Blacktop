using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PlayerLabPage { Summary, Attributes, Body, Face, Hair, Fit, Accessories, Identity }

// Every page shares one cancellable session. The saved roster changes only on Save.
public class PlayerLabView : MonoBehaviour
{
    FrontEndController menu;
    PlayerEditorSession session;
    RuntimePlayerPreview preview;
    TextMeshProUGUI dimensions, status;
    TMP_InputField weightInput;
    RectTransform form;
    FrontEndSettings Settings => menu.settings;
    AppearanceSelection Appearance => session.Draft.appearance;
    public void Initialize(FrontEndController owner, PlayerLabPage page)
    {
        menu=owner;session=menu.Session;
        var title=FrontEndUI.Text(transform,page==PlayerLabPage.Summary?"PLAYER SUMMARY":page==PlayerLabPage.Attributes?"EDIT ATTRIBUTES":"FIT & BODY",Settings,49,true);
        FrontEndUI.Anchor(title.rectTransform,new Vector2(0,.92f),new Vector2(.53f,1));
        var teams=FrontEndUI.Vertical(transform,"Team selector",0);
        FrontEndUI.Anchor(teams,new Vector2(.62f,.92f),new Vector2(1,1));
        FrontEndUI.Picker(teams,"TEAM",Settings.teams.Select(t=>t.identity.teamName.ToUpperInvariant()).ToArray(),
            Array.FindIndex(Settings.teams,t=>t.id==session.TeamId),i=>menu.SelectLabPlayer(Settings.teams[i].id,null),Settings);

        var image=FrontEndUI.PreviewSurface(transform,new Vector2(0,.30f),new Vector2(.42f,.88f));
        image.transform.parent.GetComponent<Image>().color=Color.clear;
        preview=image.gameObject.AddComponent<RuntimePlayerPreview>();preview.Initialize(Settings);
        var name=FrontEndUI.Text(transform,$"#{session.Draft.record.jerseyNumber:00}  {session.Draft.record.displayName.ToUpperInvariant()}",Settings,39);
        FrontEndUI.Anchor(name.rectTransform,new Vector2(.02f,.84f),new Vector2(.43f,.91f));
        dimensions=FrontEndUI.Text(transform,"",Settings,24);
        FrontEndUI.Anchor(dimensions.rectTransform,new Vector2(.02f,.29f),new Vector2(.44f,.34f));
        var orbit=FrontEndUI.Row(transform,44);FrontEndUI.Anchor(orbit,new Vector2(.02f,.24f),new Vector2(.42f,.29f));
        FrontEndUI.Button(orbit,"ROTATE LEFT",()=>preview.Rotate(-25),Settings,44);
        FrontEndUI.Button(orbit,"ROTATE RIGHT",()=>preview.Rotate(25),Settings,44);
        FrontEndUI.Button(orbit,"FULL / HEAD",()=>{head=!head;preview.SetHeadView(head);},Settings,44);

        bool ratings=page==PlayerLabPage.Summary||page==PlayerLabPage.Attributes;
        var heading=FrontEndUI.Text(transform,ratings?"THE NUMBERS  /  0—20":"MAKE IT YOURS",Settings,26);
        FrontEndUI.Anchor(heading.rectTransform,new Vector2(.47f,.87f),new Vector2(1,.92f));
        form=FrontEndUI.Vertical(transform,"Player fields",0);
        FrontEndUI.Anchor(form,new Vector2(.47f,.29f),new Vector2(1,.86f));
        form.GetComponent<VerticalLayoutGroup>().spacing=3;
        if(ratings)
        {
            var values=RosterRatings.Get(session.Draft.record);
            for(int index=0;index<values.Length;index++)
            {
                int stat=index;
                FrontEndUI.SquareRating(form,RosterRatings.Names[index].ToUpperInvariant(),values[index],
                    page==PlayerLabPage.Attributes&&menu.CanAuthorPlayers?v=>{
                        RosterRatings.Set(ref session.Draft.record,stat,v);status.text="UNSAVED CHANGES";
                    }:null,Settings);
            }
        }
        else
        {
            // Customization has one scrollable accordion, keeping the 3D preview and actions fixed.
            Destroy(form.gameObject);
            var area=FrontEndUI.Panel(transform,"Customization accordion",new Vector2(.47f,.30f),new Vector2(1,.86f),UIColorRole.SurfacePrimary);
            form=FrontEndUI.Scroll(area);
            foreach(var section in new[]{PlayerLabPage.Identity,PlayerLabPage.Body,PlayerLabPage.Face,PlayerLabPage.Hair,PlayerLabPage.Fit,PlayerLabPage.Accessories})
            {
                if(section==PlayerLabPage.Identity&&!menu.CanAuthorPlayers)continue;
                var choice=section;
                string label=section==PlayerLabPage.Body?"BODY BUILD / HEIGHT":section==PlayerLabPage.Hair?"HAIR / FACIAL HAIR":section.ToString().ToUpperInvariant();
                var header=FrontEndUI.Button(form,label,()=>menu.ShowLabPage(choice),Settings,48);
                header.GetComponent<ThemeButton>().SetSelected(section==page);
                if(section!=page)continue;
                var sectionFields=FrontEndUI.Vertical(form,"Expanded "+section,8);
                sectionFields.gameObject.AddComponent<LayoutElement>();
                form=sectionFields;
                BuildSection();
                form=(RectTransform)sectionFields.parent;
            }
            void BuildSection()
            {
            switch(page)
            {
                case PlayerLabPage.Body:Body();break;
                case PlayerLabPage.Face:
                    Preset("FACE",CharacterPartSlot.Face,Appearance.faceId,id=>Appearance.faceId=id);
                    Palette("SKIN TONE",Settings.appearances.skinTones,Appearance.skinToneId,id=>Appearance.skinToneId=id);
                    if(menu.CanAuthorPlayers)FrontEndUI.Picker(form,"HEAD SCALE",new[]{"100%","101%","102%","103%","104%","105%"},Mathf.RoundToInt((Appearance.headScale-1)*100),i=>{Appearance.headScale=1+i*.01f;Refresh();},Settings);
                    Note("Prototype face presets share one head. Distinct face art is still to come.");preview.SetHeadView(true);break;
                case PlayerLabPage.Hair:
                    Preset("HAIR",CharacterPartSlot.Hair,Appearance.hairId,id=>Appearance.hairId=id);
                    Preset("FACIAL HAIR",CharacterPartSlot.FacialHair,Appearance.facialHairId,id=>Appearance.facialHairId=id);
                    Palette("COLOR",Settings.appearances.hairColors,Appearance.hairColorId,id=>Appearance.hairColorId=id);
                    Note("Shaved and clean-shaven are available. Other styles await art.");preview.SetHeadView(true);break;
                case PlayerLabPage.Fit:
                    Gear(CharacterPartSlot.Top);Gear(CharacterPartSlot.Shorts);Gear(CharacterPartSlot.Footwear);
                    Note("Team colors follow your saved team. Prototype garments are provisional.");break;
                case PlayerLabPage.Accessories:
                    foreach(var slot in new[]{CharacterPartSlot.Gloves,CharacterPartSlot.Wristband,CharacterPartSlot.ArmSleeve,CharacterPartSlot.Headband,CharacterPartSlot.Accessory})Gear(slot);
                    Note("Unavailable pieces are marked MISSING ART.");break;
                case PlayerLabPage.Identity:Identity();break;
            }
        }
        }

        status=FrontEndUI.Text(transform,session.Dirty?"UNSAVED CHANGES":menu.CanAuthorPlayers?"DEV ACCESS  /  LOCAL ROSTER":"COSMETIC EDITING  /  RATINGS LOCKED",Settings,22);
        FrontEndUI.Anchor(status.rectTransform,new Vector2(.47f,.235f),new Vector2(1,.28f));
        var actions=FrontEndUI.Row(transform,58);FrontEndUI.Anchor(actions,new Vector2(0,.16f),new Vector2(1,.23f));
        if(page==PlayerLabPage.Summary)
        {
            FrontEndUI.Button(actions,"BACK",menu.Back,Settings);
            FrontEndUI.Button(actions,"EDIT ATTRIBUTES",()=>menu.ShowLabPage(PlayerLabPage.Attributes),Settings).interactable=menu.CanAuthorPlayers;
            FrontEndUI.Button(actions,"EDIT FIT & BODY",()=>menu.ShowLabPage(PlayerLabPage.Body),Settings);
            if(session.Dirty)FrontEndUI.Button(actions,"SAVE PLAYER",Save,Settings);
            if(menu.CanAuthorPlayers&&!session.Dirty)FrontEndUI.Button(actions,"DEV ROSTER TOOLS",()=>menu.GuardLabChange(()=>menu.ShowRoster(session.TeamId)),Settings);
        }
        else
        {
            FrontEndUI.Button(actions,"SUMMARY",()=>menu.ShowLabPage(PlayerLabPage.Summary),Settings);
            FrontEndUI.Button(actions,"SAVE PLAYER",Save,Settings);
            FrontEndUI.Button(actions,"DISCARD CHANGES",()=>menu.Confirm("DISCARD CHANGES?","Restore this player to the saved roster.",()=>menu.ReloadLabPlayer(),"DISCARD"),Settings);
        }
        RosterStrip();Refresh();
    }
    bool head;

    void Body()
    {
        if(!menu.CanAuthorPlayers){Note("Height, build and weight are set by the developer. Make your fit yours in the other tabs.");return;}
        var heights=(PlayerHeight[])Enum.GetValues(typeof(PlayerHeight));
        FrontEndUI.Picker(form,"HEIGHT",heights.Select(RosterBrowserView.HeightLabel).ToArray(),Array.IndexOf(heights,Appearance.height),i=>{Appearance.height=heights[i];Refresh();},Settings);
        FrontEndUI.Picker(form,"BUILD",Enum.GetNames(typeof(BodyArchetype)),(int)Appearance.bodyArchetype,i=>{
            Appearance.bodyArchetype=(BodyArchetype)i;
            var body=Settings.appearances.parts.First(p=>p.slot==CharacterPartSlot.Body&&p.bodyArchetype==(BodyArchetype)i&&p.temporaryVisual);
            Appearance.bodyId=body.id;
            Appearance.clothingIds=Appearance.clothingIds.Select(Compatible).ToArray();
            Appearance.accessoryIds=Appearance.accessoryIds.Select(Compatible).ToArray();Refresh();},Settings);
        weightInput=FrontEndUI.Input(form,"WEIGHT (LB)",Appearance.Pounds(Settings.weights).ToString(),text=>{
            bool valid=int.TryParse(text,out int pounds)&&pounds>=120&&pounds<=450;
            session.HasUncommittedInput=!valid;if(valid)Appearance.weightPounds=pounds;
            if(status!=null)status.text=valid?"UNSAVED CHANGES":"WEIGHT MUST BE 120–450 LB";
            if(valid)Refresh();},Settings,true);
        weightInput.characterLimit=3;
        FrontEndUI.Button(form,"USE BUILD-DERIVED WEIGHT",()=>{Appearance.weightPounds=0;session.HasUncommittedInput=false;menu.ShowLabPage(PlayerLabPage.Body);},Settings,48);
        FrontEndUI.Picker(form,"PREVIEW",new[]{"IDLE","RUN","BACKPEDAL","FAST RUN"},0,i=>preview.Animation(new[]{0f,1f,-1f,2f}[i]),Settings);
        Note("Build changes the silhouette. Weight is appearance metadata; it does not change ratings or collisions.");
    }

    string Compatible(string id)
    {
        var old=Settings.appearances.Find(id);
        if(old==null||string.IsNullOrEmpty(old.variantFamilyId))return id;
        return Settings.appearances.parts.FirstOrDefault(p=>p.slot==old.slot&&p.variantFamilyId==old.variantFamilyId&&
            (p.supportedBodies.Length==0||p.supportedBodies.Contains(Appearance.bodyArchetype)))?.id??id;
    }

    void Identity()
    {
        FrontEndUI.Input(form,"NAME",session.Draft.record.displayName,v=>{session.Draft.record.displayName=v;Refresh();},Settings).characterLimit=32;
        FrontEndUI.Input(form,"JERSEY",session.Draft.record.jerseyNumber.ToString(),v=>{
            bool valid=int.TryParse(v,out int number)&&number>=0;session.HasUncommittedInput=!valid;
            if(valid)session.Draft.record.jerseyNumber=number;
            status.text=valid?"UNSAVED CHANGES":"ENTER A NON-NEGATIVE JERSEY NUMBER";},Settings,true).characterLimit=3;
        var slots=new[]{"RESERVE"}.Concat(Enum.GetNames(typeof(TeamMember.RosterSlot))).ToArray();
        FrontEndUI.Picker(form,"OFFENSE",slots,session.Offense+1,i=>{session.Offense=i-1;Refresh();},Settings);
        FrontEndUI.Picker(form,"DEFENSE",RosterBrowserView.DefenseNames,session.Defense,i=>{session.Defense=i;Refresh();},Settings);
        Note("Offense and defense slots are independent. Existing occupants swap or move to reserves on Save.");
    }

    void Gear(CharacterPartSlot slot)
    {
        bool clothing=slot<=CharacterPartSlot.Footwear;
        string[] current=clothing?Appearance.clothingIds:Appearance.accessoryIds;
        Preset(slot.ToString().ToUpperInvariant(),slot,current.FirstOrDefault(id=>Settings.appearances.Find(id)?.slot==slot),id=>{
            var ids=(clothing?Appearance.clothingIds:Appearance.accessoryIds).Where(v=>Settings.appearances.Find(v)?.slot!=slot).ToList();
            if(!string.IsNullOrEmpty(id))ids.Add(id);
            if(clothing)Appearance.clothingIds=ids.ToArray();else Appearance.accessoryIds=ids.ToArray();
        },true);
    }

    void Preset(string label,CharacterPartSlot slot,string current,Action<string> set,bool optional=false)
    {
        var parts=Settings.appearances.parts.Where(p=>p.slot==slot&&(p.supportedBodies.Length==0||p.supportedBodies.Contains(Appearance.bodyArchetype))&&
            (p.compatibleFaceIds.Length==0||p.compatibleFaceIds.Contains(Appearance.faceId))).ToArray();
        var ids=parts.Select(p=>p.id).ToList();
        var labels=parts.Select(p=>(string.IsNullOrEmpty(p.displayName)?p.name.Replace('_',' '):p.displayName)+(p.placeholder?" / MISSING ART":"")).ToList();
        if(optional){ids.Insert(0,null);labels.Insert(0,"NONE");}
        FrontEndUI.Picker(form,label,labels,ids.IndexOf(current),i=>{set(ids[i]);Refresh();},Settings);
    }

    void Palette(string label,AppearanceColor[] colors,string current,Action<string> set)
    {
        FrontEndUI.Picker(form,label,colors.Select(c=>c.id.Split('.').Last().ToUpperInvariant()).ToArray(),Array.FindIndex(colors,c=>c.id==current),i=>{set(colors[i].id);Refresh();},Settings);
    }
    void Note(string text)=>FrontEndUI.Text(form,text,Settings,22,false,60);

    void Refresh()
    {
        preview.Apply(session.Draft,Settings.Team(session.TeamId).identity);
        dimensions.text=$"{RosterBrowserView.HeightLabel(Appearance.height)}   /   {Appearance.Pounds(Settings.weights)} LB   /   {Appearance.bodyArchetype.ToString().ToUpperInvariant()}";
        // Refresh derived weight after height/build changes without creating a manual override or erasing invalid text.
        if(weightInput!=null&&Appearance.weightPounds==0&&!session.HasUncommittedInput)
            weightInput.SetTextWithoutNotify(Appearance.Pounds(Settings.weights).ToString());
        if(status!=null&&session.Dirty&&!session.HasUncommittedInput)status.text="UNSAVED CHANGES";
    }

    void Save()
    {
        if(session.HasUncommittedInput){status.text="CORRECT THE INVALID INPUT BEFORE SAVING";return;}
        if(session.Save(menu.Service,out var id,out var error))menu.ReloadLabPlayer(id);
        else status.text=error;
    }

    void RosterStrip()
    {
        const int pageSize=7;
        var players=menu.Service.Team(session.TeamId).players;
        int selected=players.FindIndex(p=>p.record.id==session.Draft.record.id);
        int page=menu.RosterPage<0?Mathf.Max(0,selected)/pageSize:menu.RosterPage;
        int lastPage=Mathf.Max(0,(players.Count-1)/pageSize);page=Mathf.Clamp(page,0,lastPage);
        var strip=FrontEndUI.Row(transform,83);FrontEndUI.Anchor(strip,new Vector2(0,.045f),new Vector2(1,.145f));
        foreach(var player in players.Skip(page*pageSize).Take(pageSize))
        {
            var record=player.record;
            var button=FrontEndUI.Button(strip,$"#{record.jerseyNumber:00}\n{record.displayName.ToUpperInvariant()}",()=>menu.SelectLabPlayer(session.TeamId,record.id),Settings,83);
            var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=24;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=24;
            if(record.id==session.Draft.record.id)button.GetComponent<ThemeButton>().SetSelected(true);
        }
        var pager=FrontEndUI.Row(transform,32);FrontEndUI.Anchor(pager,new Vector2(0,0),new Vector2(1,.038f));
        int displayed=page;
        FrontEndUI.Button(pager,"PREVIOUS 7",()=>{menu.RosterPage=displayed-1;menu.ShowLabPage(menu.LabPage);},Settings,32).interactable=page>0;
        var count=FrontEndUI.Text(pager,$"ROSTER  /  {page+1} OF {lastPage+1}  /  {players.Count} PLAYERS",Settings,20,false,32);
        count.alignment=TextAlignmentOptions.Center;
        FrontEndUI.Button(pager,"NEXT 7",()=>{menu.RosterPage=displayed+1;menu.ShowLabPage(menu.LabPage);},Settings,32).interactable=page<lastPage;
    }
}

using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Editor categories edit only a session draft. Preview owns the model/camera; service owns commits.
public class PlayerEditorView : MonoBehaviour
{
    FrontEndController menu;
    PlayerEditorSession session;
    RuntimePlayerPreview preview;
    RectTransform formArea,form;
    TextMeshProUGUI status,dimensions;
    string first,last;
    bool invalidNumber;
    Button[] categories;
    FrontEndSettings Settings=>menu.settings;
    public void Initialize(FrontEndController owner,PlayerEditorSession editing)
    {
        menu=owner;session=editing;
        var title=FrontEndUI.Text(transform,string.IsNullOrEmpty(session.Draft.record.id)?"CREATE PLAYER":"EDIT PLAYER",Settings,49,true);
        FrontEndUI.Anchor(title.rectTransform,new Vector2(0,.91f),Vector2.one);
        var image=FrontEndUI.PreviewSurface(transform,new Vector2(0,.29f),new Vector2(.43f,.9f));
        preview=image.gameObject.AddComponent<RuntimePlayerPreview>();preview.Initialize(Settings);
        var controls=FrontEndUI.Vertical(transform,"Preview controls",0);FrontEndUI.Anchor(controls,new Vector2(0,.12f),new Vector2(.43f,.29f));
        var orbit=FrontEndUI.Row(controls,50);
        FrontEndUI.Button(orbit,"ROTATE ‹",()=>preview.Rotate(-25),Settings,50);
        FrontEndUI.Button(orbit,"ROTATE ›",()=>preview.Rotate(25),Settings,50);
        FrontEndUI.Button(orbit,"ZOOM +",()=>preview.Zoom(-.35f),Settings,50);
        FrontEndUI.Button(orbit,"ZOOM −",()=>preview.Zoom(.35f),Settings,50);
        var framing=FrontEndUI.Row(controls,50);
        FrontEndUI.Button(framing,"FULL BODY",()=>preview.SetHeadView(false),Settings,50);
        FrontEndUI.Button(framing,"HEAD",()=>preview.SetHeadView(true),Settings,50);
        FrontEndUI.Button(framing,"RESET VIEW",preview.ResetCamera,Settings,50);
        dimensions=FrontEndUI.Text(controls,"",Settings,22,false,45);
        var tabs=FrontEndUI.Vertical(transform,"Categories",0);FrontEndUI.Anchor(tabs,new Vector2(.46f,.75f),new Vector2(1,.9f));
        string[] names={"IDENTITY","PHYSIQUE","FACE","HAIR","FACIAL HAIR","GEAR","ATTRIBUTES"};
        categories=new Button[names.Length];
        for(int row=0;row<2;row++)
        {
            var strip=FrontEndUI.Row(tabs,52);
            for(int i=row*4;i<Mathf.Min(row*4+4,names.Length);i++)
            {int category=i;categories[i]=FrontEndUI.Button(strip,names[i],()=>Category(category),Settings,52);}
        }
        formArea=FrontEndUI.Panel(transform,"Editor fields",new Vector2(.46f,.14f),new Vector2(1,.74f),new Color(.09f,.11f,.13f));
        var bottom=FrontEndUI.Row(transform,68);FrontEndUI.Anchor(bottom,new Vector2(0,0),new Vector2(1,.085f));
        FrontEndUI.Button(bottom,"SAVE PLAYER",Save,Settings);
        FrontEndUI.Button(bottom,"CANCEL",menu.Back,Settings);
        FrontEndUI.Button(bottom,"RESET CHANGES",()=>menu.Confirm("RESET THIS DRAFT?","Restore fields to their values when the editor opened. Saved roster data stays unchanged.",()=>{
            session.Reset();invalidNumber=false;Category(0);UpdatePreview();},"RESET DRAFT"),Settings);
        status=FrontEndUI.Text(transform,"",Settings,21,false,45);FrontEndUI.Anchor(status.rectTransform,new Vector2(0,.085f),new Vector2(1,.135f));
        Category(0);UpdatePreview();
    }
    void Category(int category)
    {
        for(int i=0;i<categories.Length;i++)categories[i].GetComponentInChildren<TextMeshProUGUI>().text=(i==category?"> ":"")+categories[i].name;
        if(form!=null){form.parent.parent.gameObject.SetActive(false);Destroy(form.parent.parent.gameObject);}
        form=FrontEndUI.Scroll(formArea,"Category scroll");
        switch(category)
        {
            case 0:Identity();break;
            case 1:Physique();break;
            case 2:
                Preset("Face",CharacterPartSlot.Face,session.Draft.appearance.faceId,id=>session.Draft.appearance.faceId=id);
                Palette("Skin tone",Settings.appearances.skinTones,session.Draft.appearance.skinToneId,id=>session.Draft.appearance.skinToneId=id);
                Note("Three defined face IDs currently share the same integrated prototype head. Distinct face art is missing; changing IDs does not manufacture a different face.");preview.SetHeadView(true);break;
            case 3:
                Preset("Hair",CharacterPartSlot.Hair,session.Draft.appearance.hairId,id=>session.Draft.appearance.hairId=id);
                Palette("Hair color",Settings.appearances.hairColors,session.Draft.appearance.hairColorId,id=>session.Draft.appearance.hairColorId=id);
                Note("Shaved is an intentional empty option. Missing styles retain a clearly labelled fallback.");preview.SetHeadView(true);break;
            case 4:
                Preset("Facial hair",CharacterPartSlot.FacialHair,session.Draft.appearance.facialHairId,id=>session.Draft.appearance.facialHairId=id);
                Palette("Shared hair color",Settings.appearances.hairColors,session.Draft.appearance.hairColorId,id=>session.Draft.appearance.hairColorId=id);
                Note("Clean-shaven needs no extra face. Missing beard art is omitted; face compatibility is validated on Save.");preview.SetHeadView(true);break;
            case 5:Gear();preview.SetHeadView(false);break;
            case 6:
                FrontEndUI.Text(form,"RATINGS 0–20 / INDEPENDENT OF APPEARANCE",Settings,24,false,60);
                var ratings=RosterRatings.Get(session.Draft.record);
                for(int i=0;i<12;i++){int index=i;FrontEndUI.Rating(form,RosterRatings.Names[i],ratings[i],v=>{
                    RosterRatings.Set(ref session.Draft.record,index,v);status.text="Unsaved rating changes.";},Settings);}
                break;
        }
    }
    void Identity()
    {
        string name=session.Draft.record.displayName;int split=name.LastIndexOf(' ');
        first=split<0?name:name.Substring(0,split);last=split<0?"":name.Substring(split+1);
        FrontEndUI.Input(form,"First name",first,v=>{first=v;NameChanged();},Settings);
        FrontEndUI.Input(form,"Last name",last,v=>{last=v;NameChanged();},Settings);
        FrontEndUI.Input(form,"Jersey number",session.Draft.record.jerseyNumber.ToString(),v=>{
            invalidNumber=!int.TryParse(v,out int number)||number<0;session.HasUncommittedInput=invalidNumber;
            if(!invalidNumber)session.Draft.record.jerseyNumber=number;
            UpdatePreview();},Settings,true);
        var teams=Settings.teams; Action<int> activeSelection=null;
        FrontEndUI.Picker(form,"Team",teams.Select(t=>t.identity.teamName).ToArray(),Array.FindIndex(teams,t=>t.id==session.TeamId),i=>{
            if(teams[i].id!=session.TeamId){session.TeamId=teams[i].id;session.Offense=-1;activeSelection?.Invoke(0);}UpdatePreview();},Settings);
        var slots=new[]{"RESERVE"}.Concat(Enum.GetNames(typeof(TeamMember.RosterSlot))).ToArray();
        activeSelection=FrontEndUI.Picker(form,"Active offense",slots,session.Offense+1,i=>{session.Offense=i-1;UpdatePreview();},Settings);
        FrontEndUI.Picker(form,"Defense slot",RosterBrowserView.DefenseNames,session.Defense,i=>{session.Defense=i;UpdatePreview();},Settings);
        Note("Assigning an occupied offensive slot replaces that player into reserves. Defensive slots are independent and swap when necessary. Transfers keep this player’s identity, ratings and appearance and begin as reserves unless you assign a slot.");
    }
    void NameChanged(){session.Draft.record.displayName=(first+" "+last).Trim();UpdatePreview();}
    void Physique()
    {
        var heights=(PlayerHeight[])Enum.GetValues(typeof(PlayerHeight));
        FrontEndUI.Picker(form,"Height",heights.Select(RosterBrowserView.HeightLabel).ToArray(),Array.IndexOf(heights,session.Draft.appearance.height),i=>{
            session.Draft.appearance.height=heights[i];UpdatePreview();},Settings);
        FrontEndUI.Picker(form,"Build",Enum.GetNames(typeof(BodyArchetype)),(int)session.Draft.appearance.bodyArchetype,i=>{
            var selection=session.Draft.appearance;selection.bodyArchetype=(BodyArchetype)i;
            // Prefer validated generated art for a new selection; loading an old save keeps its explicit IDs.
            var body=Settings.appearances.parts.Where(p=>p.slot==CharacterPartSlot.Body&&p.bodyArchetype==(BodyArchetype)i&&p.temporaryVisual)
                .OrderByDescending(p=>p.variantFamilyId=="body.parametric.v1").First();
            selection.bodyId=body.id;
            if(body.variantFamilyId=="body.parametric.v1")selection.faceId="face.prototype.parametric.v1";
            else if(selection.faceId=="face.prototype.parametric.v1")selection.faceId="face.prototype.a";
            selection.clothingIds=selection.clothingIds.Select(CompatibleVariant).ToArray();
            selection.accessoryIds=selection.accessoryIds.Select(CompatibleVariant).ToArray();UpdatePreview();},Settings);
        Note("Weight follows height and build. You control the ratings separately. Every height and build is available at every position.");
        Note("Body and garment variants are functional TEMPORARY silhouettes, not finished character art. Heavy emphasizes torso/belly mass; Massive increases belly, hip and thigh volume within the Barrel family.");
        FrontEndUI.Picker(form,"Preview animation",new[]{"IDLE","RUN","BACKWARD","FAST RUN"},0,i=>preview.Animation(new[]{0f,1f,-1f,2f}[i]),Settings);
        preview.SetHeadView(false);
    }
    string CompatibleVariant(string id)
    {
        var old=Settings.appearances.Find(id);if(old==null)return id;
        // Keep selected clothing slots, resolving their matching generated fit during a body change.
        if(session.Draft.appearance.bodyId.EndsWith(".parametric.v1",StringComparison.Ordinal)&&old.slot>=CharacterPartSlot.Top&&old.slot<=CharacterPartSlot.Footwear)
            return Settings.appearances.parts.First(p=>p.slot==old.slot&&p.compatibleBodyIds.Contains(session.Draft.appearance.bodyId)).id;
        if(string.IsNullOrEmpty(old.variantFamilyId))return id;
        var match=Settings.appearances.parts.FirstOrDefault(p=>p.slot==old.slot&&p.variantFamilyId==old.variantFamilyId&&
            (p.supportedBodies.Length==0||p.supportedBodies.Contains(session.Draft.appearance.bodyArchetype)));
        return match!=null?match.id:id;
    }
    void Gear()
    {
        foreach(var slot in new[]{CharacterPartSlot.Top,CharacterPartSlot.Shorts,CharacterPartSlot.Footwear,CharacterPartSlot.Gloves,
            CharacterPartSlot.Wristband,CharacterPartSlot.ArmSleeve,CharacterPartSlot.Headband,CharacterPartSlot.Accessory})
        {
            bool clothing=slot<=CharacterPartSlot.Footwear;
            var ids=clothing?session.Draft.appearance.clothingIds:session.Draft.appearance.accessoryIds;
            string current=ids.FirstOrDefault(id=>Settings.appearances.Find(id)?.slot==slot);
            Preset(slot.ToString(),slot,current,id=>{
                var existing=clothing?session.Draft.appearance.clothingIds:session.Draft.appearance.accessoryIds;
                var next=existing.Where(v=>Settings.appearances.Find(v)?.slot!=slot).ToList();if(!string.IsNullOrEmpty(id))next.Add(id);
                if(clothing)session.Draft.appearance.clothingIds=next.ToArray();else session.Draft.appearance.accessoryIds=next.ToArray();
            },true);
        }
        Note("Temporary garments use team colors. Missing gear is labelled. The prototype has no numbered garment renderer; roster nameplates show the saved number in the match.");
    }
    void Preset(string label,CharacterPartSlot slot,string current,Action<string> set,bool optional=false)
    {
        var options=Settings.appearances.parts.Where(p=>p.slot==slot&&(p.supportedBodies.Length==0||p.supportedBodies.Contains(session.Draft.appearance.bodyArchetype))&&
            (p.compatibleBodyIds.Length==0||p.compatibleBodyIds.Contains(session.Draft.appearance.bodyId))&&
            (!(slot>=CharacterPartSlot.Top&&slot<=CharacterPartSlot.Footwear)||!session.Draft.appearance.bodyId.EndsWith(".parametric.v1",StringComparison.Ordinal)||p.placeholder||p.compatibleBodyIds.Contains(session.Draft.appearance.bodyId))&&
            (slot!=CharacterPartSlot.Face||!session.Draft.appearance.bodyId.EndsWith(".parametric.v1",StringComparison.Ordinal)||p.id=="face.prototype.parametric.v1")).ToList();
        var labels=options.Select(p=>Label(p)).ToList();int selected=options.FindIndex(p=>p.id==current);
        if(optional){labels.Insert(0,"NONE");selected=string.IsNullOrEmpty(current)?0:selected+1;}
        FrontEndUI.Picker(form,label,labels,selected,i=>{set(optional&&i==0?null:options[optional?i-1:i].id);UpdatePreview();},Settings);
    }
    void Palette(string label,AppearanceColor[] options,string current,Action<string> set)
    {FrontEndUI.Picker(form,label,options.Select(c=>c.id.Split('.').Last().ToUpperInvariant()).ToArray(),Array.FindIndex(options,c=>c.id==current),i=>{set(options[i].id);UpdatePreview();},Settings);}
    public static string Label(CharacterPartDefinition p) => (string.IsNullOrEmpty(p.displayName)?p.name.Replace('_',' '):p.displayName)+
        (p.placeholder?" [MISSING ART]":p.temporaryVisual?" [TEMPORARY]":" [AVAILABLE]");
    void Note(string text)=>FrontEndUI.Text(form,text,Settings,22,false,116);
    void UpdatePreview()
    {
        if(preview==null)return;
        bool valid=preview.Apply(session.Draft,Settings.Team(session.TeamId).identity);
        var appearance=session.Draft.appearance;
        dimensions.text=$"{RosterBrowserView.HeightLabel(appearance.height)} / {Settings.weights.Pounds(appearance.height,appearance.bodyArchetype)} LB / {appearance.bodyArchetype}";
        status.text=invalidNumber?"Enter a non-negative whole jersey number.":valid?"DRAFT • "+preview.Diagnostics:preview.Diagnostics;
    }
    void Save()
    {
        if(invalidNumber){status.text="Enter a valid jersey number before saving.";return;}
        if(session.Save(menu.Service,out var id,out var error))
        {menu.Message="Player saved locally. Ready for lineup assignment.";menu.ShowRoster(session.TeamId,id);}
        else status.text=error;
    }
}

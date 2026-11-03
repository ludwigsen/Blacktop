using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The open field is the main menu's breathing room; the brand stays centered across aspect ratios.
public static class MainMenuView
{
    public static void Build(FrontEndController menu, RectTransform root)
    {
        var edition=FrontEndUI.Text(root,"7 ON 7  /  STREET FOOTBALL",menu.settings,24);
        FrontEndUI.Anchor(edition.rectTransform,new Vector2(.02f,.91f),new Vector2(.5f,.97f));
        var label=FrontEndUI.Text(root,"THE BLOCK IS YOURS.",menu.settings,23);
        FrontEndUI.Anchor(label.rectTransform,new Vector2(.5f,.91f),new Vector2(.98f,.97f));
        label.alignment=TextAlignmentOptions.MidlineRight;
        var title=FrontEndUI.Text(root,"Blacktop",menu.settings,210);
        title.font=menu.settings.menuTitleFont!=null?menu.settings.menuTitleFont:title.font;
        title.alignment=TextAlignmentOptions.Center;
        title.enableAutoSizing=true;title.fontSizeMin=80;title.fontSizeMax=210;
        title.textWrappingMode=TextWrappingModes.NoWrap;
        title.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(5,-6);
        FrontEndUI.Anchor(title.rectTransform,new Vector2(.12f,.48f),new Vector2(.88f,.80f));
        var rule=FrontEndUI.Panel(root,"Chalk line",new Vector2(.40f,.475f),new Vector2(.60f,.479f),FrontEndUI.Paper);
        rule.localRotation=Quaternion.Euler(0,0,-1.4f);rule.GetComponent<Image>().raycastTarget=false;
        var list=FrontEndUI.Vertical(root,"Main menu",0);
        FrontEndUI.Anchor(list,new Vector2(.36f,.115f),new Vector2(.64f,.445f));
        list.GetComponent<VerticalLayoutGroup>().spacing=7;
        Entry("NEW GAME",menu.ShowModes);
        Entry("EDIT PLAYERS",()=>menu.ShowPlayerLab());
        Entry("SETTINGS",menu.ShowPreferences);
        Entry("QUIT",()=>menu.Confirm("CALL IT A NIGHT?","Your saved roster will be here when you get back.",menu.Quit,"QUIT GAME"));
        var footer=FrontEndUI.Text(root,"NO STANDS. NO SPOTLIGHT. JUST YOUR BLOCK.",menu.settings,22);
        footer.alignment=TextAlignmentOptions.Center;
        FrontEndUI.Anchor(footer.rectTransform,new Vector2(.15f,.025f),new Vector2(.85f,.075f));
        void Entry(string text,System.Action action)
        {
            var button=FrontEndUI.Button(list,text,action,menu.settings,61);
            button.GetComponent<Image>().color=new Color(.035f,.045f,.04f,.68f);
            var caption=button.GetComponentInChildren<TextMeshProUGUI>();
            caption.fontSize=38;caption.alignment=TextAlignmentOptions.Center;caption.characterSpacing=3;
        }
    }

    public static void Modes(FrontEndController menu,RectTransform root)
    {
        var list=FrontEndUI.Vertical(root,"New game",24);FrontEndUI.Anchor(list,new Vector2(.2f,.12f),new Vector2(.8f,.96f));
        FrontEndUI.Text(list,"NEW GAME",menu.settings,60,true,110);
        FrontEndUI.Button(list,"HEAD 2 HEAD",menu.BeginHeadToHead,menu.settings,100);
        FrontEndUI.Text(list,"Two saved teams. Your lineup. Your block.",menu.settings,26,false,65);
        FrontEndUI.Button(list,"PICKUP GAME",menu.BeginPickup,menu.settings,100);
        FrontEndUI.Text(list,"Draft seven. Make some noise.",menu.settings,26,false,65);
        FrontEndUI.Button(list,"BACK",menu.Back,menu.settings,70);
    }

    public static void Preferences(FrontEndController menu,RectTransform root)
    {
        var list=FrontEndUI.Vertical(root,"Settings",24);FrontEndUI.Anchor(list,new Vector2(.23f,.23f),new Vector2(.77f,.84f));
        FrontEndUI.Text(list,"SETTINGS",menu.settings,64,true,110);
        FrontEndUI.Rating(list,"SFX",Mathf.RoundToInt(MenuSound.Volume*100),v=>{
            MenuSound.SetVolume(v/100f);menu.Sound.Play();},menu.settings,100,"%");
        FrontEndUI.Text(list,"VOLUME  /  0 MUTES MENU SOUNDS",menu.settings,22,false,60);
        FrontEndUI.Text(list,"VERSION  "+Application.version+"  /  MENU PROTOTYPE",menu.settings,28,false,80);
        FrontEndUI.Button(list,"BACK",menu.Back,menu.settings,70);
    }
}

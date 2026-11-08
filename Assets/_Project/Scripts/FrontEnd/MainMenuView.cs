using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The brand and menu form one left-hand stack so the field remains the hero on the right.
public static class MainMenuView
{
    public static void Build(FrontEndController menu, RectTransform root)
    {
        var art = UITheme.Library;
        StreetArt.Layer(root, "Menu ink scrim", art?.inkScrim, new Vector2(-.045f, 0),
            new Vector2(.77f, 1), UIColorRole.BackgroundSecondary, .92f);
        StreetArt.Layer(root, "Menu paint spray", art?.sprayScatter, new Vector2(-.02f, .47f),
            new Vector2(.38f, .92f), UIColorRole.ActionCancel, .65f).preserveAspect = true;
        StreetArt.Layer(root, "Menu crown", art?.crown, new Vector2(.04f, .77f),
            new Vector2(.15f, .91f), UIColorRole.AccentSpecial).preserveAspect = true;
        StreetArt.Layer(root, "Menu brand shadow", art?.wordmark, new Vector2(.05f, .562f),
            new Vector2(.44f, .81f), UIColorRole.ShadowPrimary).preserveAspect = true;
        StreetArt.Layer(root, "Blacktop wordmark", art?.wordmark, new Vector2(.045f, .57f),
            new Vector2(.435f, .818f), UIColorRole.TextPrimary).preserveAspect = true;
        StreetArt.Layer(root, "Menu brush underline", art?.dryBrush, new Vector2(.04f, .52f),
            new Vector2(.44f, .61f), UIColorRole.AccentSpecial);

        var edition = FrontEndUI.Text(root, "7 ON 7  /  STREET FOOTBALL", menu.settings, 24);
        FrontEndUI.Anchor(edition.rectTransform, new Vector2(.045f, .925f), new Vector2(.5f, .975f));
        var list = FrontEndUI.Vertical(root, "Main menu", 0);
        FrontEndUI.Anchor(list, new Vector2(.045f, .115f), new Vector2(.37f, .49f));
        list.GetComponent<VerticalLayoutGroup>().spacing = 9;
        Entry("NEW GAME", menu.ShowModes);
        Entry("EDIT PLAYERS", () => menu.ShowPlayerLab());
        Entry("SETTINGS", menu.ShowPreferences);
        Entry("QUIT", () => menu.Confirm("CALL IT A NIGHT?", "Your saved roster will be here when you get back.",
            menu.Quit, "QUIT GAME"));
        var footer = FrontEndUI.Text(root, "NO STANDS. NO SPOTLIGHT. JUST YOUR BLOCK.", menu.settings, 22);
        footer.alignment = TextAlignmentOptions.MidlineLeft;
        FrontEndUI.Anchor(footer.rectTransform, new Vector2(.045f, .045f), new Vector2(.55f, .095f));

        void Entry(string label, System.Action action)
        {
            var button = FrontEndUI.Button(list, label, action, menu.settings, 74, paintedSelection: true);
            var caption = button.GetComponentInChildren<TextMeshProUGUI>();
            caption.fontSize = 39;
            FrontEndUI.Anchor(caption.rectTransform, new Vector2(.15f, .10f), new Vector2(.90f, .90f));
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
        var list=FrontEndUI.Vertical(root,"Settings",24);FrontEndUI.Anchor(list,new Vector2(.23f,.10f),new Vector2(.77f,.96f));
        FrontEndUI.Text(list,"SETTINGS",menu.settings,64,true,110);
        FrontEndUI.Rating(list,"SFX",Mathf.RoundToInt(MenuSound.Volume*100),v=>{
            MenuSound.SetVolume(v/100f);menu.Sound.Play();},menu.settings,100,"%");
        FrontEndUI.Text(list,"VOLUME  /  0 MUTES MENU SOUNDS",menu.settings,22,false,60);
        FrontEndUI.Picker(list,"UI THEME",new[]{"Street Classic","After Dark"},(int)UITheme.Current.id,i=>UITheme.Select((UIThemeId)i),menu.settings);
        var preview=FrontEndUI.Row(list,72);
        foreach(var role in new[]{UIColorRole.SurfacePrimary,UIColorRole.SelectionHighlight,UIColorRole.ActionConfirm,UIColorRole.StateSuccess})
        {
            var swatch=FrontEndUI.Panel(preview,role.ToString(),Vector2.zero,Vector2.one,role);
            swatch.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        }
        FrontEndUI.Text(list,"APPLIES IMMEDIATELY / SAVED BETWEEN SESSIONS",menu.settings,22,false,44);
        FrontEndUI.Text(list,"VERSION  "+Application.version,menu.settings,28,false,60);
        FrontEndUI.Button(list,"BACK",menu.Back,menu.settings,70);
    }
}

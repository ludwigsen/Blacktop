using UnityEngine;
using UnityEngine.UI;

// Menu presentation reuses the visual-only preview, never a gameplay player instance.
public static class MainMenuView
{
    public static void Build(FrontEndController menu,RectTransform root)
    {
        var raw=FrontEndUI.PreviewSurface(root,new Vector2(0,.05f),new Vector2(.58f,.98f));
        var preview=raw.gameObject.AddComponent<RuntimePlayerPreview>();preview.Initialize(menu.settings);
        var player=PlayerEditorSession.New(menu.settings,0);preview.Apply(player,menu.settings.teams[0].identity);preview.Rotate(-12);
        var list=FrontEndUI.Vertical(root,"Menu",24);FrontEndUI.Anchor(list,new Vector2(.62f,.12f),new Vector2(1,.92f));
        FrontEndUI.Text(list,"RUN YOUR BLOCK",menu.settings,57,true,130);
        FrontEndUI.Text(list,"HEAD 2 HEAD / PICKUP / PLAYER LAB",menu.settings,23,false,70);
        FrontEndUI.Button(list,"NEW GAME",menu.ShowModes,menu.settings,86);
        FrontEndUI.Button(list,"PLAYERS",menu.ShowTeams,menu.settings,86);
        FrontEndUI.Button(list,"QUIT",menu.Quit,menu.settings,86);
        FrontEndUI.Text(list,menu.Message,menu.settings,21,false,100);
        FrontEndUI.Text(list,"TEMPORARY CHARACTER ART  •  REAL ROSTERS",menu.settings,20,false,54);
    }
    public static void Modes(FrontEndController menu,RectTransform root)
    {
        var list=FrontEndUI.Vertical(root,"New game",24);FrontEndUI.Anchor(list,new Vector2(.16f,.12f),new Vector2(.84f,.96f));
        FrontEndUI.Text(list,"NEW GAME",menu.settings,60,true,110);
        FrontEndUI.Button(list,"HEAD 2 HEAD",menu.BeginHeadToHead,menu.settings,100);
        FrontEndUI.Text(list,"Two saved teams. Your lineup. Your block.",menu.settings,26,false,65);
        FrontEndUI.Button(list,"PICKUP GAME",menu.BeginPickup,menu.settings,100);
        FrontEndUI.Text(list,"Draft any seven saved players. Face a CPU-drafted squad.",menu.settings,26,false,65);
        FrontEndUI.Button(list,"BACK",menu.ShowMain,menu.settings,70);
    }
}

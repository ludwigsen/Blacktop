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
        FrontEndUI.Text(list,"QUICK PLAY / ROSTERS / PLAYER LAB",menu.settings,23,false,70);
        FrontEndUI.Button(list,"NEW GAME",()=>menu.ShowQuickPlay(FrontEndScreen.HumanTeam),menu.settings,86);
        FrontEndUI.Button(list,"PLAYERS",menu.ShowTeams,menu.settings,86);
        FrontEndUI.Button(list,"QUIT",menu.Quit,menu.settings,86);
        FrontEndUI.Text(list,menu.Message,menu.settings,21,false,100);
        FrontEndUI.Text(list,"TEMPORARY CHARACTER ART  •  REAL ROSTERS",menu.settings,20,false,54);
    }
}

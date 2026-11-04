using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AnnouncementKind { Information, Touchdown, FirstDown, Turnover, Gamebreaker }

// Animation observes existing events; the post-play owner still decides exactly when to hide it.
public class GameAnnouncement : MonoBehaviour
{
    TMP_Text text;Image background,accent;CanvasGroup group;
    AnnouncementKind kind;float shown,expires;
    public void Initialize(TMP_Text label)
    {
        text=label;background=GetComponent<Image>();group=gameObject.AddComponent<CanvasGroup>();
        var rule=FrontEndUI.Panel(transform,"Directional accent",new Vector2(.02f,0),new Vector2(.98f,.065f),Color.white);
        accent=rule.GetComponent<Image>();accent.raycastTarget=false;
        ThemeableUI.Bind(text,UIColorRole.TextPrimary,UITypeRole.Announcement).tint=false;
    }
    void OnEnable(){UITheme.Changed+=Apply;Apply();}
    void OnDisable()=>UITheme.Changed-=Apply;
    public void Show(AnnouncementKind value,string message,float seconds=0)
    {
        kind=value;text.text=message;shown=Time.unscaledTime;expires=seconds>0?shown+seconds:0;
        gameObject.SetActive(true);Apply();
    }
    void Apply()
    {
        if(text==null)return;
        var primary=kind switch
        {
            AnnouncementKind.Touchdown=>UIColorRole.ActionCancel,AnnouncementKind.FirstDown=>UIColorRole.SelectionHighlight,
            AnnouncementKind.Turnover=>UIColorRole.StateSuccess,AnnouncementKind.Gamebreaker=>UIColorRole.AccentSpecial,
            _=>UIColorRole.SurfaceRaised
        };
        background.color=UITheme.Color(primary);
        // Large touchdown lettering uses the requested light face; smaller bright-surface copy uses dark ink.
        text.color=UITheme.Color(kind==AnnouncementKind.Information||kind==AnnouncementKind.Touchdown?UIColorRole.TextPrimary:UIColorRole.TextOnBright);
        accent.color=UITheme.Color(kind==AnnouncementKind.FirstDown?UIColorRole.SelectionPrimary:UIColorRole.ActionConfirm);
    }
    void Update()
    {
        float elapsed=Time.unscaledTime-shown;
        group.alpha=Mathf.Clamp01(elapsed/.12f);
        // Short overshoot settles in 220ms without changing the game's presentation duration.
        float t=Mathf.Clamp01(elapsed/.22f);float overshoot=1+2.5f*Mathf.Pow(t-1,3)+1.5f*Mathf.Pow(t-1,2);
        ((RectTransform)transform).anchoredPosition=new Vector2((1-overshoot)*100,0);
        if(expires>0&&Time.unscaledTime>=expires)gameObject.SetActive(false);
    }
}

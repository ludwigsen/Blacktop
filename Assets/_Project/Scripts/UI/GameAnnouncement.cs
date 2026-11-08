using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AnnouncementKind { Information, Touchdown, FirstDown, Turnover, Gamebreaker }

// Animation observes existing events; the post-play owner still decides exactly when to hide it.
public class GameAnnouncement : MonoBehaviour
{
    TMP_Text text;
    Image background, accent, crown;
    CanvasGroup group;
    AnnouncementKind kind;
    float shown, expires;

    public void Initialize(TMP_Text label)
    {
        text = label;
        // The root still owns the original layout and timing. Paint is stacked above its shadow.
        GetComponent<Image>().enabled = false;
        group = gameObject.AddComponent<CanvasGroup>();
        var art = UITheme.Library;
        var shadow = StreetArt.Layer(transform, "Paint shadow", art?.brushPlate,
            new Vector2(.007f, -.07f), new Vector2(1.007f, .91f), UIColorRole.ShadowPrimary, .8f);
        shadow.transform.SetAsFirstSibling();
        background = StreetArt.Layer(transform, "Announcement paint", art?.brushPlate,
            Vector2.zero, Vector2.one, UIColorRole.ActionCancel);
        background.GetComponent<ThemeableUI>().tint = false;
        accent = StreetArt.Layer(transform, "Paint underline", art?.dryBrush,
            new Vector2(.14f, -.095f), new Vector2(.93f, .18f), UIColorRole.AccentSpecial);
        accent.GetComponent<ThemeableUI>().tint = false;
        crown = StreetArt.Layer(transform, "Announcement crown", art?.crown,
            new Vector2(.023f, .63f), new Vector2(.18f, 1.24f), UIColorRole.AccentSpecial);
        crown.preserveAspect = true;
        // The artwork carries no baked lettering. All event copy stays in Barlow Bold Italic.
        ThemeableUI.Bind(text, UIColorRole.TextPrimary, UITypeRole.Announcement).tint = false;
        text.transform.SetAsLastSibling();
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
        accent.color = UITheme.Color(kind == AnnouncementKind.FirstDown
            ? UIColorRole.SelectionPrimary : UIColorRole.AccentSpecial);
        crown.enabled = crown.sprite != null &&
            kind is AnnouncementKind.Touchdown or AnnouncementKind.Gamebreaker;
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

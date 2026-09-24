using UnityEngine;
using UnityEngine.UI;

// Pure display — reads DownsTracker's already-resolved state and renders it. No downs
// math lives here; if this ever shows something wrong, the bug is in DownsTracker, not
// here. Same "builds its own Canvas, no scene wiring" pattern as GamebreakerHUD.
//
// SETUP: drop on any single GameObject — GameManager, alongside PlayState/DownsTracker,
// is the natural home.
public class DownsHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Vector2 anchoredPosition = new Vector2(30f, -30f); // top-left
    [SerializeField] int fontSize = 32;
    [SerializeField] Color textColor = Color.white;

    Text downText;

    void Awake() => BuildHUD();

    // Subscribed in Start(), not OnEnable() — same reasoning as BallController and
    // DownsTracker itself: Awake/OnEnable ordering isn't guaranteed BETWEEN separate
    // GameObjects, so OnEnable here could fire before DownsTracker.Awake() has set
    // Instance. Start() is guaranteed to run only after every object's Awake() has.
    void Start()
    {
        if (DownsTracker.Instance == null) return;
        DownsTracker.Instance.OnDownsChanged += Refresh;
        Refresh(); // prime immediately — don't wait for the next play to end
    }

    void OnDestroy()
    {
        if (DownsTracker.Instance != null)
            DownsTracker.Instance.OnDownsChanged -= Refresh;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("DownsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGO = new GameObject("DownText", typeof(Text));
        textGO.transform.SetParent(canvasGO.transform, false);

        downText = textGO.GetComponent<Text>();
        downText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        downText.fontSize = fontSize;
        downText.fontStyle = FontStyle.Bold;
        downText.alignment = TextAnchor.UpperLeft;
        downText.color = textColor;

        var rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(300f, 50f);
    }

    void Refresh()
    {
        var downs = DownsTracker.Instance;
        if (downs == null || downText == null) return;

        downText.text = downs.IsGoalToGo
            ? $"{Ordinal(downs.CurrentDown)} & Goal"
            : $"{Ordinal(downs.CurrentDown)} & {Mathf.CeilToInt(downs.YardsToGo)}";
    }

    static string Ordinal(int down) => down switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        4 => "4th",
        _ => $"{down}th" // shouldn't be reachable now that turnover-on-downs exists — stays readable if it ever is
    };
}
using UnityEngine;
using UnityEngine.UI;

// Builds its own Canvas hierarchy at runtime — no manual Canvas/Image/Text assembly
// needed in the Editor. Matches the project's "structurally sound first, art pass later"
// philosophy: this gets bars on screen immediately with zero scene setup risk. Swap in
// real sprites/fonts whenever the art pass happens; none of the event wiring below needs
// to change to support that — just point CreateBar/readyPrompt at real assets instead of
// solid-color Images.
//
// SETUP: drop this component on any single GameObject in the scene — GameManager is a
// natural home, alongside PlayState/DefenderCoordinator/BlockingCoordinator. Nothing else
// required. Colors/sizes/positions are exposed for quick tuning without touching code.
public class GamebreakerHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Vector2 barSize = new Vector2(300f, 24f);
    [SerializeField] Vector2 screenMargin = new Vector2(30f, 30f);
    [SerializeField] int promptFontSize = 28;

    [Header("Colors")]
    [SerializeField] Color offenseColor = new Color(1f, 0.6f, 0f);      // orange
    [SerializeField] Color defenseColor = new Color(0.2f, 0.6f, 1f);    // blue
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

    Image offenseFill;
    Image defenseFill;
    Text readyPrompt;

    void Awake() => BuildHUD();

    void OnEnable()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnOffenseMeterChanged += HandleOffenseMeter;
        PlayState.Instance.OnDefenseMeterChanged += HandleDefenseMeter;
        PlayState.Instance.OnOffenseGamebreakerActivated += HandleActivated;

        // Prime immediately — otherwise both bars sit empty until the next point-award
        // event fires, which could be well into the first play.
        HandleOffenseMeter(PlayState.Instance.OffenseMeter);
        HandleDefenseMeter(PlayState.Instance.DefenseMeter);
    }

    void OnDisable()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnOffenseMeterChanged -= HandleOffenseMeter;
        PlayState.Instance.OnDefenseMeterChanged -= HandleDefenseMeter;
        PlayState.Instance.OnOffenseGamebreakerActivated -= HandleActivated;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("GamebreakerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Offense — bottom-left. Defense — bottom-right. Mirrored margins so tuning
        // screenMargin moves both symmetrically without separate offsets to track.
        offenseFill = CreateBar(canvasGO.transform, "OffenseBar", offenseColor,
            anchor: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
            anchoredPos: screenMargin);

        defenseFill = CreateBar(canvasGO.transform, "DefenseBar", defenseColor,
            anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-screenMargin.x, screenMargin.y));

        // Ready prompt — top-center, hidden until the offense meter caps.
        var promptGO = new GameObject("ReadyPrompt", typeof(Text));
        promptGO.transform.SetParent(canvasGO.transform, false);

        readyPrompt = promptGO.GetComponent<Text>();
        readyPrompt.text = "GAMEBREAKER READY — PRESS G";
        readyPrompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        readyPrompt.fontSize = promptFontSize;
        readyPrompt.fontStyle = FontStyle.Bold;
        readyPrompt.alignment = TextAnchor.MiddleCenter;
        readyPrompt.color = offenseColor;

        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.5f, 1f);
        promptRT.anchorMax = new Vector2(0.5f, 1f);
        promptRT.pivot = new Vector2(0.5f, 1f);
        promptRT.anchoredPosition = new Vector2(0f, -30f);
        promptRT.sizeDelta = new Vector2(600f, 50f);

        promptGO.SetActive(false);
    }

    // Background + fill pair, anchored at a given screen corner. No sprite assigned on
    // either Image — an Image with sprite == null still renders as a solid quad using
    // its Color, so this needs zero placeholder art to look like a real meter.
    Image CreateBar(Transform parent, string name, Color fillColor, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
    {
        var bgGO = new GameObject(name + "_BG", typeof(Image));
        bgGO.transform.SetParent(parent, false);

        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = anchor;
        bgRT.anchorMax = anchor;
        bgRT.pivot = pivot;
        bgRT.sizeDelta = barSize;
        bgRT.anchoredPosition = anchoredPos;
        bgGO.GetComponent<Image>().color = backgroundColor;

        var fillGO = new GameObject(name + "_Fill", typeof(Image));
        fillGO.transform.SetParent(bgGO.transform, false);

        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var fillImg = fillGO.GetComponent<Image>();
        fillImg.color = fillColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;

        return fillImg;
    }

    void HandleOffenseMeter(float value)
    {
        if (offenseFill != null) offenseFill.fillAmount = value / 100f;

        if (readyPrompt != null)
        {
            bool ready = value >= 100f && PlayState.Instance != null && !PlayState.Instance.IsOffenseGamebreakerActive;
            readyPrompt.gameObject.SetActive(ready);
        }
    }

    void HandleDefenseMeter(float value)
    {
        if (defenseFill != null) defenseFill.fillAmount = value / 100f;
    }

    void HandleActivated()
    {
        if (readyPrompt != null) readyPrompt.gameObject.SetActive(false);
    }
}
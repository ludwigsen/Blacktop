using UnityEngine;
using UnityEngine.UI;

// One team's slice of the scorebug — logo, name, score, and a SEGMENTED Gamebreaker
// meter (discrete chunks, not a smooth fill) matching the concept board's three states:
// Empty (all dark), Charging (some chunks lit), Full/Ready (all chunks lit and visibly
// brighter — a cheap stand-in for "glow" until this project has a real emissive/bloom
// pipeline for UI, which Canvas Overlay doesn't get by default in URP). Self-building,
// same pattern as every other runtime HUD here.
//
// Build() lays out the panel with placeholder name/color and is safe to call from
// Awake() — before PlayState.Instance necessarily exists. ApplyIdentity() reads the
// real name/color/logo and is meant to be called from Start(), once it does.
public class TeamScorePanel : MonoBehaviour
{
    const int SegmentCount = 8;
    static readonly Color EmptyColor = new Color(0.18f, 0.18f, 0.18f);
    static readonly Color DefaultAccent = new Color(0.6f, 0.6f, 0.6f);

    Text teamNameText;
    Text scoreText;
    Image logoImage;
    Image[] segments;
    Color accent = DefaultAccent;
    Color readyColor = Color.white;
    bool reverseFill;
    float lastGamebreakerValue;

    public void Build(int nameFontSize, int scoreFontSize, bool reverseFill)
    {
        this.reverseFill = reverseFill;

        var logoGO = new GameObject("Logo", typeof(Image));
        logoGO.transform.SetParent(transform, false);
        logoImage = logoGO.GetComponent<Image>();
        logoImage.preserveAspect = true;
        logoImage.enabled = false; // hidden until ApplyIdentity finds a sprite
        AnchorStrip(logoGO.GetComponent<RectTransform>(),
            reverseFill ? new Vector2(0.82f, 0.6f) : new Vector2(0.02f, 0.6f),
            reverseFill ? new Vector2(0.98f, 0.98f) : new Vector2(0.18f, 0.98f));

        var nameGO = new GameObject("TeamName", typeof(Text));
        nameGO.transform.SetParent(transform, false);
        teamNameText = ConfigureText(nameGO, nameFontSize);
        teamNameText.text = "TEAM";
        AnchorStrip(nameGO.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 1f));

        var scoreGO = new GameObject("Score", typeof(Text));
        scoreGO.transform.SetParent(transform, false);
        scoreText = ConfigureText(scoreGO, scoreFontSize);
        AnchorStrip(scoreGO.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.55f));

        BuildGamebreakerBar(transform);
    }

    // Safe to call any time after Build(). Falls back to a neutral placeholder if
    // identity is null (no TeamIdentity asset assigned on PlayState yet).
    public void ApplyIdentity(TeamIdentity identity)
    {
        accent = identity != null ? identity.accentColor : DefaultAccent;
        readyColor = Color.Lerp(accent, Color.white, 0.6f);
        teamNameText.text = identity != null ? identity.teamName : "TEAM";

        if (identity != null && identity.logo != null)
        {
            logoImage.sprite = identity.logo;
            logoImage.color = Color.white;
            logoImage.enabled = true;
        }
        else
        {
            logoImage.enabled = false;
        }

        SetGamebreaker(lastGamebreakerValue); // repaint already-lit segments in the new color
    }

    public void SetScore(int score) => scoreText.text = score.ToString();

    // value: 0-100. Lights whole chunks only — "62%" reads as 5 of 8 chunks lit, not
    // one chunk sitting at a fractional fill. That's the "unified, one flow" chunky
    // read the concept board is going for.
    public void SetGamebreaker(float value)
    {
        lastGamebreakerValue = value;
        int lit = Mathf.FloorToInt(Mathf.Clamp01(value / 100f) * SegmentCount);
        Color litColor = value >= 100f ? readyColor : accent;

        for (int i = 0; i < SegmentCount; i++)
        {
            int slot = reverseFill ? SegmentCount - 1 - i : i;
            segments[slot].color = i < lit ? litColor : EmptyColor;
        }
    }

    void BuildGamebreakerBar(Transform parent)
    {
        var barGO = new GameObject("Gamebreaker", typeof(RectTransform));
        barGO.transform.SetParent(parent, false);
        AnchorStrip(barGO.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.22f));

        segments = new Image[SegmentCount];
        const float gap = 0.02f; // fraction of the bar's own width, between chunks
        float segmentWidth = (1f - gap * (SegmentCount - 1)) / SegmentCount;

        for (int i = 0; i < SegmentCount; i++)
        {
            var segGO = new GameObject($"Segment_{i}", typeof(Image));
            segGO.transform.SetParent(barGO.transform, false);

            float xMin = i * (segmentWidth + gap);
            var rt = segGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMin + segmentWidth, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = segGO.GetComponent<Image>();
            img.color = EmptyColor;
            segments[i] = img;
        }
    }

    static Text ConfigureText(GameObject go, int fontSize)
    {
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    static void AnchorStrip(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One team's slice of the scorebug — logo, name, score, GAMEBREAKER label, and a
// SEGMENTED Gamebreaker meter (discrete chunks, not a smooth fill) matching the concept
// board's three states: Empty (all dark), Charging (some chunks lit), Full/Ready (all
// chunks lit and visibly brighter — a brightness swap stands in for real glow until this
// project has an HDR/bloom pipeline for UI, which Canvas Overlay doesn't get by default).
// Self-building, same pattern as every other runtime HUD here.
//
// Text elements use the DISPLAY font (team name, score, GAMEBREAKER label) per the
// concept board's own typographic split — the legible font belongs to GameHUD's center
// panel instead, not to anything built by this component.
public class TeamScorePanel : MonoBehaviour
{
    const int SegmentCount = 8;
    static readonly Color EmptyColor = new Color(0.18f, 0.18f, 0.18f);
    static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.75f);
    static readonly Color DefaultAccent = new Color(0.6f, 0.6f, 0.6f);

    TextMeshProUGUI teamNameText;
    TextMeshProUGUI scoreText;
    TextMeshProUGUI gamebreakerLabel;
    Image logoImage;
    Image[] segments;
    Color accent = DefaultAccent;
    Color readyColor = Color.white;
    bool reverseFill;
    float lastGamebreakerValue;

    public void Build(TMP_FontAsset displayFont, int nameFontSize, int scoreFontSize, int labelFontSize, bool reverseFill)
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

        var nameGO = new GameObject("TeamName", typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(transform, false);
        teamNameText = ConfigureText(nameGO, displayFont, nameFontSize);
        teamNameText.text = "TEAM";
        AnchorStrip(nameGO.GetComponent<RectTransform>(), new Vector2(0f, 0.62f), new Vector2(1f, 1f));

        var scoreGO = new GameObject("Score", typeof(TextMeshProUGUI));
        scoreGO.transform.SetParent(transform, false);
        scoreText = ConfigureText(scoreGO, displayFont, scoreFontSize);
        AnchorStrip(scoreGO.GetComponent<RectTransform>(), new Vector2(0f, 0.34f), new Vector2(1f, 0.62f));

        var labelGO = new GameObject("GamebreakerLabel", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(transform, false);
        gamebreakerLabel = ConfigureText(labelGO, displayFont, labelFontSize);
        gamebreakerLabel.text = "GAMEBREAKER";
        gamebreakerLabel.color = LabelColor;
        AnchorStrip(labelGO.GetComponent<RectTransform>(), new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.34f));

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
        AnchorStrip(barGO.GetComponent<RectTransform>(), new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.19f));

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

    // font left null is fine — TMP falls back to TMP_Settings.defaultFontAsset, same as
    // ReceiverSelectionUI already relies on elsewhere in this project.
    static TextMeshProUGUI ConfigureText(GameObject go, TMP_FontAsset font, int fontSize)
    {
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
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
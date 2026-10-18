using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One horizontal slice: crest, wordmark, score, then meter toward the center.
// Only placement/background art mirror; lettering and crests always face normally.
public class TeamScorePanel : MonoBehaviour
{
    const int SegmentCount = 8;
    static readonly Color EmptyColor = new Color(0.12f, 0.13f, 0.14f);
    static readonly Color LabelColor = new Color(0.9f, 0.9f, 0.87f);
    static readonly Color DefaultAccent = new Color(0.6f, 0.6f, 0.6f);

    TextMeshProUGUI teamNameText;
    TextMeshProUGUI scoreText;
    TextMeshProUGUI gamebreakerLabel;
    Image logoImage;
    Image backgroundImage;
    Image accentRule;
    Outline meterFrame;
    Image[] segments;
    Material wordmarkMaterial;
    Color accent = DefaultAccent;
    Color readyColor = Color.white;
    bool reverseFill;
    bool lastActive;
    float lastGamebreakerValue;

    public void Build(TMP_FontAsset displayFont, TMP_FontAsset legibleFont, int nameFontSize, int scoreFontSize, int labelFontSize,
        bool reverseFill, Sprite backgroundSprite = null, bool flipBackground = false,
        Color? textColor = null, float outlineWidth = 0.22f,
        float logoScale = 1.25f, float logoOutwardTilt = 20f,
        float nameOutwardShift = 0.10f, float nameScoreGapReduction = 0.04f)
    {
        this.reverseFill = reverseFill;

        if (backgroundSprite != null)
        {
            backgroundImage = CreateImage("PanelBackground", transform, Vector2.zero, Vector2.one);
            backgroundImage.sprite = backgroundSprite;
            // Neutral grunge art takes the team's primary color without changing source pixels.
            if (flipBackground) backgroundImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        accentRule = CreateImage("AccentRule", transform, new Vector2(0.08f, 0.08f), new Vector2(0.95f, 0.095f));
        logoImage = CreateImage("Logo", transform, new Vector2(0f, 0.12f), new Vector2(0.175f, 0.90f));
        // Leave space outside the name for the larger crest; tilt its top toward the screen edge.
        logoImage.rectTransform.localScale = Vector3.one * logoScale;
        logoImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, reverseFill ? -logoOutwardTilt : logoOutwardTilt);
        logoImage.preserveAspect = true;
        logoImage.enabled = false; // No white square when a future team has no crest yet.

        // Interpret 10% as a fraction of the original name slot, mirrored for the other team.
        float nameShift = (0.475f - 0.205f) * nameOutwardShift;
        float scoreShift = nameShift + nameScoreGapReduction;
        teamNameText = CreateText("TeamName", displayFont, nameFontSize,
            new Vector2(0.205f - nameShift, 0.16f), new Vector2(0.475f - scoreShift, 0.90f));
        teamNameText.text = "Team";
        teamNameText.alignment = reverseFill ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        scoreText = CreateText("Score", displayFont, scoreFontSize,
            new Vector2(0.48f - scoreShift, 0.10f), new Vector2(0.635f - scoreShift, 0.94f));
        // Apply TMP's heavier SDF weight to identity only; Outfit meter copy keeps its weight.
        teamNameText.fontStyle = FontStyles.Bold;
        scoreText.fontStyle = FontStyles.Bold;

        // TMP's SDF outline stays crisp at different resolutions. Share one private
        // material between these two labels and release it with the panel.
        if (displayFont != null)
        {
            wordmarkMaterial = new Material(displayFont.material);
            wordmarkMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            wordmarkMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            teamNameText.fontSharedMaterial = wordmarkMaterial;
            scoreText.fontSharedMaterial = wordmarkMaterial;
            teamNameText.UpdateMeshPadding();
            scoreText.UpdateMeshPadding();
        }
        teamNameText.color = scoreText.color = textColor ?? new Color(0.94f, 0.93f, 0.89f);

        gamebreakerLabel = CreateText("GamebreakerLabel", legibleFont, labelFontSize,
            new Vector2(0.65f, 0.57f), new Vector2(0.96f, 0.82f));
        gamebreakerLabel.text = "GAMEBREAKER";
        gamebreakerLabel.color = LabelColor;
        BuildGamebreakerBar();
    }

    // Pre-game branding is independent of roster selection and player GUIDs.
    public void ApplyIdentity(TeamIdentity identity)
    {
        accent = identity != null ? identity.accentColor : DefaultAccent;
        Color secondary = identity != null ? identity.secondaryAccentColor : Color.white;
        readyColor = Color.Lerp(accent, Color.white, 0.6f);
        teamNameText.text = identity != null && !string.IsNullOrWhiteSpace(identity.teamName) ? identity.teamName : "Team";
        if (backgroundImage != null) backgroundImage.color = accent;
        accentRule.color = secondary;
        meterFrame.effectColor = secondary;
        logoImage.sprite = identity != null ? identity.logo : null;
        logoImage.enabled = logoImage.sprite != null;
        // Preserve READY/ACTIVE if branding is reapplied during a preview.
        SetGamebreaker(lastGamebreakerValue, lastActive);
    }

    public void SetScore(int score) => scoreText.text = score.ToString();

    public void SetGamebreaker(float value, bool active = false)
    {
        lastGamebreakerValue = value;
        lastActive = active;
        gamebreakerLabel.text = active ? "GAMEBREAKER ACTIVE" : value >= 100f ? "GAMEBREAKER READY" : "GAMEBREAKER";
        int lit = Mathf.FloorToInt(Mathf.Clamp01(active ? 1f : value / 100f) * SegmentCount);
        Color litColor = active || value >= 100f ? readyColor : accent;
        for (int i = 0; i < SegmentCount; i++)
        {
            int slot = reverseFill ? SegmentCount - 1 - i : i;
            segments[slot].color = i < lit ? litColor : EmptyColor;
        }
    }

    void BuildGamebreakerBar()
    {
        var track = CreateImage("Gamebreaker", transform, new Vector2(0.65f, 0.30f), new Vector2(0.96f, 0.49f));
        track.color = Color.black;
        meterFrame = track.gameObject.AddComponent<Outline>();
        meterFrame.effectDistance = new Vector2(1.5f, -1.5f);
        segments = new Image[SegmentCount];
        const float gap = 0.018f;
        const float inset = 0.025f;
        float segmentWidth = (1f - 2f * inset - gap * (SegmentCount - 1)) / SegmentCount;
        for (int i = 0; i < SegmentCount; i++)
        {
            float xMin = inset + i * (segmentWidth + gap);
            // The small cant gives the chunky meter the reference's forward lean.
            var segment = CreateImage($"Segment_{i}", track.transform,
                new Vector2(xMin, 0.16f), new Vector2(xMin + segmentWidth, 0.84f), mirror: false);
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            segment.color = EmptyColor;
            segments[i] = segment;
        }
    }

    Image CreateImage(string objectName, Transform parent, Vector2 min, Vector2 max, bool mirror = true)
    {
        var go = new GameObject(objectName, typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        Place(image.rectTransform, min, max, mirror);
        return image;
    }

    TextMeshProUGUI CreateText(string objectName, TMP_FontAsset font, int fontSize, Vector2 min, Vector2 max)
    {
        var go = new GameObject(objectName, typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal; // Supporting labels use their native font weight.
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.55f;
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        Place(text.rectTransform, min, max);
        return text;
    }

    void Place(RectTransform rt, Vector2 min, Vector2 max, bool mirror = true)
    {
        // Reflect anchors rather than scaling content, so crests and text stay readable.
        rt.anchorMin = reverseFill && mirror ? new Vector2(1f - max.x, min.y) : min;
        rt.anchorMax = reverseFill && mirror ? new Vector2(1f - min.x, max.y) : max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void OnDestroy()
    {
        // Never modify or destroy the font asset's shared material.
        if (wordmarkMaterial != null) Destroy(wordmarkMaterial);
    }
}

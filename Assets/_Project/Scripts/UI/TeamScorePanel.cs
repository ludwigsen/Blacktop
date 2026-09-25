using UnityEngine;
using UnityEngine.UI;

// One team's slice of the scorebug — name, score, and Gamebreaker fill. Self-building
// (Build() constructs its own Text/Image children) so GameHUD can create two of these
// side by side with no hand-authored prefab, matching how every other runtime HUD in
// this project builds itself. Pure display — GameHUD is the only thing that calls
// Set*() here; this never reads PlayState/ScoreBoard/DownsTracker directly.
public class TeamScorePanel : MonoBehaviour
{
    Text teamNameText;
    Text scoreText;
    Image gamebreakerFill;

    // reverseFill: the right-side panel fills right-to-left, so both bars visually
    // grow toward the center of the scorebug instead of both growing rightward.
    public void Build(Color accentColor, int nameFontSize, int scoreFontSize, bool reverseFill)
    {
        var nameGO = new GameObject("TeamName", typeof(Text));
        nameGO.transform.SetParent(transform, false);
        teamNameText = ConfigureText(nameGO, nameFontSize);
        AnchorStrip(nameGO.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 1f));

        var scoreGO = new GameObject("Score", typeof(Text));
        scoreGO.transform.SetParent(transform, false);
        scoreText = ConfigureText(scoreGO, scoreFontSize);
        AnchorStrip(scoreGO.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.55f));

        gamebreakerFill = BuildGamebreakerBar(transform, accentColor, reverseFill);
    }

    public void SetTeam(string name) => teamNameText.text = name;
    public void SetScore(int score) => scoreText.text = score.ToString();
    public void SetGamebreaker(float value) => gamebreakerFill.fillAmount = value / 100f;

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

    static Image BuildGamebreakerBar(Transform parent, Color fillColor, bool reverse)
    {
        var bgGO = new GameObject("Gamebreaker_BG", typeof(Image));
        bgGO.transform.SetParent(parent, false);
        AnchorStrip(bgGO.GetComponent<RectTransform>(), new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.2f));
        bgGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var fillGO = new GameObject("Gamebreaker_Fill", typeof(Image));
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var fill = fillGO.GetComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = reverse ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        return fill;
    }
}
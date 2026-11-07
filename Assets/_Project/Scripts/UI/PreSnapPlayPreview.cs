using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Projects the user's set assignments through the gameplay camera. It never
// installs a route, changes control, or advances the phase.
[DefaultExecutionOrder(1250)]
public sealed class PreSnapPlayPreview : MonoBehaviour
{
    [SerializeField] GameplayLabelLayout labels;
    [SerializeField] FrontEndSettings settings;
    [SerializeField, Min(1f)] float strokeWidth = 4f;
    [SerializeField, Min(0.01f)] float groundHeight = 0.05f;

    sealed class Stroke
    {
        public Image outline;
        public Image fill;
    }

    readonly List<Stroke> strokes = new();
    readonly List<Vector2> visibleRanges = new();
    InputSystem_Actions controls;
    PlayState play;
    Canvas canvas;
    RectTransform fieldArea;
    TextMeshProUGUI caption;
    Camera view;
    Rect safe;
    int usedStrokes;
    int visibleStrokeCount;

    void Awake()
    {
        play = GetComponent<PlayState>();
        controls = new InputSystem_Actions();
        canvas = FrontEndUI.CreateCanvas("Pre-snap play preview", 80);
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        var field = new GameObject("Assignments on field", typeof(RectTransform), typeof(RectMask2D));
        field.transform.SetParent(canvas.transform, false);
        fieldArea = field.GetComponent<RectTransform>();
        var panel = FrontEndUI.Panel(canvas.transform, "Pre-snap controls",
            new Vector2(.37f, .015f), new Vector2(.985f, .08f), UIColorRole.SurfacePrimary, .96f);
        panel.GetComponent<Image>().raycastTarget = false;
        caption = FrontEndUI.Text(panel, "", settings, 25);
        caption.alignment = TextAlignmentOptions.Center;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.richText = false;
        FrontEndUI.Anchor(caption.rectTransform, new Vector2(.015f, 0f), new Vector2(.985f, 1f));
        Hide();
    }

    void OnEnable()
    {
        controls.Player.PreviewPlay.Enable();
        play.OnPlayReset += Hide;
        play.OnHuddleStarted += Hide;
        play.OnGameOver += HideAfterPhaseChange;
        play.OnPossessionChanged += HideAfterPhaseChange;
    }

    void OnDisable()
    {
        controls.Player.PreviewPlay.Disable();
        play.OnPlayReset -= Hide;
        play.OnHuddleStarted -= Hide;
        play.OnGameOver -= HideAfterPhaseChange;
        play.OnPossessionChanged -= HideAfterPhaseChange;
        Hide();
    }

    void OnDestroy() => controls?.Dispose();
    void HideAfterPhaseChange(int _) => Hide();

    void Hide()
    {
        if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
        usedStrokes = 0;
        HideUnusedStrokes();
    }

    void LateUpdate()
    {
        if (!play.IsSetAtLOS)
        {
            Hide();
            return;
        }

        canvas.gameObject.SetActive(true);
        bool defense = play.IsUserDefending;
        usedStrokes = 0;
        string message = defense ? "HOLD V / LT / L2 FOR PLAY ART    |    Q / RB / R1 SWITCH" :
            "HOLD V / LT / L2 FOR PLAY ART    |    R START PLAY";
        if (controls.Player.PreviewPlay.IsPressed() && (view = Camera.main) != null)
        {
            // The label layout has already placed the identity cards this frame.
            safe = labels.SafeArea;
            Vector2 screen = new(Screen.width, Screen.height);
            FrontEndUI.Anchor(fieldArea, safe.min / screen, safe.max / screen);
            message = defense ? DrawDefense() : DrawOffense();
        }
        if (caption.text != message) caption.text = message;
        HideUnusedStrokes();
    }

    string DrawOffense()
    {
        var call = play.CurrentPlayCall;
        if (call == null) return "OFFENSE   /   NO AUTHORED ASSIGNMENTS";
        bool run = call.PlayType == PlayType.Run;
        for (int index = 0; index < play.OffensivePlayers.Count; index++)
        {
            var actor = play.OffensivePlayers[index];
            if (!VisibleActor(actor)) continue;
            if (!run && call.TryGetAuthoredRoute(index, out var route) && route != RoutePattern.None &&
                ReceiverTargeting.IsEligible(actor) && actor.TryGetComponent<ReceiverAI>(out var receiver))
            {
                Arrow("Route", actor.position, receiver.SetRouteEndpoint, UIColorRole.SelectionHighlight);
            }
            if (actor.TryGetComponent<AllyBlocker>(out var blocker) &&
                BlockingCoordinator.IsEligibleBlocker(blocker, call, carrierPastLOS: false))
            {
                // The call supplies who blocks. Actual targets are assigned live.
                Bar("Block", actor.position + play.AttackDirection.Forward * .55f, UIColorRole.TextPrimary);
            }
        }

        if (run && VisibleActor(play.Passer))
        {
            var back = HandoffCoordinator.FindReceiver(play.PossessionTeamId, call.HandoffReceiverSlot);
            if (VisibleActor(back))
                Arrow("Exchange", back.position, play.Passer.position, UIColorRole.StateWarning);
        }
        return call.DisplayName + (run ? "   /   GOLD EXCHANGE    WHITE BLOCK" : "   /   BLUE ROUTE    WHITE BLOCK");
    }

    string DrawDefense()
    {
        var call = play.CurrentDefensivePlay;
        if (call == null) return "DEFENSE   /   NO AUTHORED ASSIGNMENTS";
        var manTargets = DefenderCoordinator.GetManTargets(play, call);
        for (int index = 0; index < play.DefensePlayers.Count; index++)
        {
            var actor = play.DefensePlayers[index];
            if (!VisibleActor(actor) || !actor.TryGetComponent<DefenderAI>(out var ai) ||
                !call.TryGetAssignment(index, out var assignment)) continue;

            switch (assignment.job)
            {
                case DefenderJob.Zone:
                    Vector3 anchor = DefenderCoordinator.ZoneAnchor(play, assignment);
                    Line("Zone drop", actor.position, anchor, UIColorRole.SelectionHighlight);
                    // Adjacent zones overlap by design. Keep the drop lines legible
                    // and make the currently selected defender's zone easiest to read.
                    float opacity = actor == PossessionController.ActivelyControlled ? .85f : .48f;
                    Ring("Zone", anchor, ai.ZoneReadRadius, UIColorRole.SelectionHighlight, opacity);
                    break;
                case DefenderJob.Man:
                    if (manTargets.TryGetValue(ai, out var target))
                        Line("Man", actor.position, target.position, UIColorRole.TextPrimary);
                    else
                        Bar("Unmatched man", actor.position, UIColorRole.TextPrimary);
                    break;
                case DefenderJob.Rush:
                    if (VisibleActor(play.Passer))
                        Arrow("Blitz", actor.position, play.Passer.position, UIColorRole.StateWarning);
                    break;
                case DefenderJob.Spy:
                    Bar("Spy", actor.position, UIColorRole.TextPrimary);
                    break;
            }
        }
        return call.DisplayName + "   /   BLUE ZONE    WHITE MAN / HOLD    GOLD BLITZ";
    }

    static bool VisibleActor(Transform actor) => actor != null && actor.gameObject.activeInHierarchy;

    void Arrow(string name, Vector3 start, Vector3 end, UIColorRole color)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude < .01f) return;

        float head = Mathf.Min(.55f, direction.magnitude * .4f);
        direction.Normalize();
        Vector3 back = end - direction * head;
        Vector3 wing = Vector3.Cross(Vector3.up, direction) * head * .5f;
        Line(name, start, end, color);
        Line(name, back + wing, end, color);
        Line(name, back - wing, end, color);
    }

    void Ring(string name, Vector3 center, float radius, UIColorRole color, float opacity)
    {
        if (radius <= 0f) return;
        const int sides = 20;
        Vector3 last = center + Vector3.right * radius;
        for (int i = 1; i <= sides; i++)
        {
            float angle = i * (Mathf.PI * 2f / sides);
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Line(name, last, point, color, opacity);
            last = point;
        }
    }

    void Bar(string name, Vector3 center, UIColorRole color)
    {
        Vector3 halfWidth = Vector3.right * .35f;
        Line(name, center - halfWidth, center + halfWidth, color);
    }

    void Line(string name, Vector3 start, Vector3 end, UIColorRole color, float opacity = 1f)
    {
        start.y = end.y = groundHeight;
        Vector3 projectedStart = view.WorldToScreenPoint(start);
        Vector3 projectedEnd = view.WorldToScreenPoint(end);
        if (projectedStart.z <= view.nearClipPlane || projectedEnd.z <= view.nearClipPlane) return;

        Vector2 travel = (Vector2)(projectedEnd - projectedStart);
        if (travel.sqrMagnitude < 1f) return;
        float enter = 0f, leave = 1f;
        if (!Clip(projectedStart, travel, safe, ref enter, ref leave)) return;

        // Cut out the identity cards before painting the overlay. The field mask
        // also keeps thick strokes out of the scorebug and bottom control band.
        visibleRanges.Clear();
        visibleRanges.Add(new Vector2(enter, leave));
        float padding = (strokeWidth + 8f) * canvas.scaleFactor;
        foreach (var card in labels.PlacedLabels.Values)
        {
            Rect keepout = Rect.MinMaxRect(card.xMin - padding, card.yMin - padding,
                card.xMax + padding, card.yMax + padding);
            for (int i = visibleRanges.Count - 1; i >= 0; i--)
            {
                var range = visibleRanges[i];
                enter = range.x;
                leave = range.y;
                if (!Clip(projectedStart, travel, keepout, ref enter, ref leave)) continue;
                visibleRanges.RemoveAt(i);
                if (range.x < enter) visibleRanges.Add(new Vector2(range.x, enter));
                if (leave < range.y) visibleRanges.Add(new Vector2(leave, range.y));
            }
        }

        foreach (var range in visibleRanges)
            PaintStroke(name, (Vector2)projectedStart + travel * range.x, travel * (range.y - range.x), color, opacity);
    }

    static bool Clip(Vector2 start, Vector2 travel, Rect area, ref float enter, ref float leave)
        => GameplayLabelLayout.ClipAxis(start.x, travel.x, area.xMin, area.xMax, ref enter, ref leave) &&
           GameplayLabelLayout.ClipAxis(start.y, travel.y, area.yMin, area.yMax, ref enter, ref leave);

    void PaintStroke(string name, Vector2 start, Vector2 travel, UIColorRole color, float opacity)
    {
        if (travel.sqrMagnitude < 1f) return;
        if (usedStrokes == strokes.Count) strokes.Add(CreateStroke());
        Stroke stroke = strokes[usedStrokes++];
        stroke.outline.gameObject.name = name;
        stroke.outline.gameObject.SetActive(true);
        RectTransform rect = stroke.outline.rectTransform;
        rect.sizeDelta = new Vector2(travel.magnitude / canvas.scaleFactor + 1f, strokeWidth + 3f);
        rect.anchoredPosition = (start + travel * .5f - safe.center) / canvas.scaleFactor;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg);
        Color fill = UITheme.Color(color);
        fill.a *= opacity;
        Color outline = UITheme.Color(UIColorRole.ShadowPrimary);
        outline.a *= opacity;
        stroke.fill.color = fill;
        stroke.outline.color = outline;
    }

    Stroke CreateStroke()
    {
        var outline = NewImage(fieldArea);
        var fill = NewImage(outline.transform);
        FrontEndUI.Anchor(fill.rectTransform, Vector2.zero, Vector2.one);
        fill.rectTransform.offsetMin = new Vector2(.5f, 1.5f);
        fill.rectTransform.offsetMax = new Vector2(-.5f, -1.5f);
        return new Stroke { outline = outline, fill = fill };
    }

    static Image NewImage(Transform parent)
    {
        var item = new GameObject("Preview stroke", typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    void HideUnusedStrokes()
    {
        for (int i = usedStrokes; i < visibleStrokeCount; i++)
            strokes[i].outline.gameObject.SetActive(false);
        visibleStrokeCount = usedStrokes;
    }
}

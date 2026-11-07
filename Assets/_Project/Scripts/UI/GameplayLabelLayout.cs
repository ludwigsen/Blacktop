using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One presentation pass packs actionable receiver cards and player identity together.
// It reads canonical ownership; it never selects a player, target or gameplay action.
[DefaultExecutionOrder(1200)]
public sealed class GameplayLabelLayout : MonoBehaviour
{
    [SerializeField] float topSafeMargin = 190f;
    [SerializeField] float bottomSafeMargin = 85f;
    [SerializeField] float edgeMargin = 18f;
    [SerializeField] float gap = 8f;
    [SerializeField] float anchorLift = 12f;
    [SerializeField] float receiverAnchorHeight = 1.45f;
    [SerializeField] Vector2 identitySize = new(230f, 34f);
    [SerializeField] Vector2 promptSize = new(210f, 38f);
    [SerializeField] float identityFontSize = 26f;
    [SerializeField] float promptFontSize = 24f;

    sealed class Label
    {
        public Transform actor;
        public RectTransform rect, leader;
        public Canvas worldCanvas;
        public TMP_Text text;
        public Vector3 localAnchor;
        public Vector2 screenAnchor;
        public PlayerVisualAssembler visual;
        public Image accent;
        public bool prompt;
        public int priority, order;
    }

    readonly List<Label> names = new();
    readonly List<Label> prompts = new();
    readonly List<Label> visible = new();
    readonly List<Rect> occupied = new();
    readonly Dictionary<Transform, Rect> placed = new();
    Canvas canvas;
    PlayState play;
    bool offerReceivers;

    public IReadOnlyDictionary<Transform, Rect> PlacedLabels => placed;
    public Rect SafeArea { get; private set; }
    public Vector2 PromptSize => promptSize;

    public void Configure(Canvas target, PlayState state)
    {
        canvas = target; play = state;
    }

    void Start()
    {
        // Every CharacterApplier builds its existing Nameplate/Label in early Awake;
        // collect once after visual height offsets have been applied in Start.
        foreach (var applier in FindObjectsByType<CharacterApplier>())
        {
            var root = applier.transform.Find("Nameplate") as RectTransform;
            if (root == null) continue;
            var world = root.GetComponent<Canvas>();
            var text = root.GetComponentInChildren<TMP_Text>();
            if (world == null || text == null) continue;
            var visual = applier.GetComponent<PlayerVisualAssembler>();
            var label = new Label { actor = applier.transform, rect = root, worldCanvas = world, text = text,
                localAnchor = root.localPosition - (visual != null ? visual.NameplateOffset : Vector3.zero), visual = visual,
                order = names.Count };
            ThemeableUI.Bind(root.gameObject.AddComponent<AngularPanel>(), UIColorRole.SurfacePrimary, null, .95f);
            var typography = ThemeableUI.Bind(text, UIColorRole.TextPrimary, UITypeRole.Navigation);
            typography.tint = true; typography.Apply(); // replace legacy team-colored text with readable theme contrast
            text.fontSize = identityFontSize; text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(10f, 0f, 10f, 0f); text.raycastTarget = false;
            var accentObject = new GameObject("Controlled identity accent", typeof(RectTransform), typeof(Image));
            accentObject.transform.SetParent(root, false);
            label.accent = accentObject.GetComponent<Image>(); label.accent.raycastTarget = false;
            var accentRect = accentObject.GetComponent<RectTransform>();
            accentRect.anchorMin = Vector2.zero; accentRect.anchorMax = Vector2.up;
            accentRect.pivot = new Vector2(0f, .5f); accentRect.anchoredPosition = Vector2.zero; accentRect.sizeDelta = new Vector2(4f, 0f);
            ThemeableUI.Bind(label.accent, UIColorRole.SelectionHighlight);
            label.leader = MakeLeader(); names.Add(label);
        }
    }

    public void ClearPrompts()
    {
        foreach (var label in prompts)
            if (label.leader != null) { label.leader.gameObject.SetActive(false); Destroy(label.leader.gameObject); }
        prompts.Clear();
    }

    public void AddPrompt(Transform actor, RectTransform rect, TMP_Text text, int index)
    {
        prompts.Add(new Label { actor = actor, rect = rect, text = text, prompt = true,
            priority = 1, order = index, localAnchor = Vector3.up * receiverAnchorHeight,
            visual = actor.GetComponent<PlayerVisualAssembler>(), leader = MakeLeader() });
    }

    public void SetReceiverVisibility(bool visible) => offerReceivers = visible;

    RectTransform MakeLeader()
    {
        if (canvas == null) return null;
        var go = new GameObject("Label actor link", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var image = go.GetComponent<Image>(); image.raycastTarget = false;
        ThemeableUI.Bind(image, UIColorRole.TextSecondary, null, .65f);
        go.transform.SetAsFirstSibling();
        return go.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        placed.Clear(); visible.Clear(); occupied.Clear(); HideAll();
        var camera = Camera.main;
        if (canvas == null || play == null || camera == null || play.IsGameOver ||
            (!play.IsLive && !play.IsSetAtLOS)) return;
        float scale = Screen.height / 1080f;
        SafeArea = Rect.MinMaxRect(edgeMargin * scale, bottomSafeMargin * scale,
            Screen.width - edgeMargin * scale, Screen.height - topSafeMargin * scale);

        // Reserve the actual ball projection too; no card should hide a catch/pop.
        var ball = BallController.Instance;
        if (ball != null)
        {
            Vector3 point = camera.WorldToScreenPoint(ball.RulingPosition);
            if (point.z > 0f) occupied.Add(new Rect((Vector2)point - Vector2.one * 18f * scale, Vector2.one * 36f * scale));
        }
        foreach (var label in prompts)
            if (offerReceivers && Valid(label.actor)) visible.Add(label);
        foreach (var label in names)
        {
            if (!Valid(label.actor)) continue;
            bool controlled = PossessionController.ActivelyControlled == label.actor;
            label.accent.enabled = controlled;
            bool holder = ball != null && ball.Carrier == label.actor;
            bool inbound = ball != null && ball.IntendedReceiver == label.actor;
            if (!controlled && !holder && !inbound) continue;
            if (visible.Exists(p => p.actor == label.actor)) continue; // prompt already contains this identity
            label.priority = controlled ? 0 : 2;
            visible.Add(label);
        }
        visible.Sort((a, b) => a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order));
        foreach (var label in visible)
        {
            Vector3 offset = label.localAnchor + (label.visual != null ? label.visual.NameplateOffset : Vector3.zero);
            Vector3 anchor = camera.WorldToScreenPoint(label.actor.TransformPoint(offset));
            if (anchor.z <= 0f || anchor.x < 0f || anchor.x > Screen.width || anchor.y < 0f || anchor.y > Screen.height) continue;
            Vector2 size = (label.prompt ? promptSize : identitySize) * scale;
            Vector2 preferred = (Vector2)anchor + Vector2.up * anchorLift * scale;
            if (!TryPlace(preferred, size, scale, out Rect rect)) continue;
            occupied.Add(Inflate(rect, gap * scale * .5f)); placed[label.actor] = rect;
            Apply(label, rect, anchor.z, camera, scale);
            label.screenAnchor = anchor;
        }
        // Check links after all cards are placed so lines never cross lower-priority text.
        foreach (var label in visible)
            if (placed.TryGetValue(label.actor, out Rect rect))
                DrawLeader(label.leader, label.screenAnchor, rect, scale);
    }

    static bool Valid(Transform actor) => actor != null && actor.gameObject.activeInHierarchy &&
        actor.TryGetComponent<TeamMember>(out var member) && member.enabled;

    bool TryPlace(Vector2 preferred, Vector2 size, float scale, out Rect result)
    {
        // A small expanding grid stays near the actor, with deterministic slot order.
        // There are at most four receiver cards plus controlled/carrier identities.
        for (int ring = 0; ring <= 6; ring++)
        for (int y = ring; y >= -ring; y--)
        for (int x = -ring; x <= ring; x++)
        {
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring) continue;
            Vector2 center = preferred + new Vector2(x * (size.x + gap * scale), y * (size.y + gap * scale));
            center.x = Mathf.Clamp(center.x, SafeArea.xMin + size.x * .5f, SafeArea.xMax - size.x * .5f);
            center.y = Mathf.Clamp(center.y, SafeArea.yMin + size.y * .5f, SafeArea.yMax - size.y * .5f);
            var candidate = new Rect(center - size * .5f, size);
            bool blocked = occupied.Exists(other => other.Overlaps(candidate));
            if (!blocked) { result = candidate; return true; }
        }
        result = default; return false;
    }

    static Rect Inflate(Rect rect, float amount) => Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

    void Apply(Label label, Rect rect, float depth, Camera camera, float scale)
    {
        if (label.prompt)
        {
            label.rect.gameObject.SetActive(true);
            label.rect.sizeDelta = promptSize * (scale / canvas.rootCanvas.scaleFactor);
            label.text.fontSize = promptFontSize * scale / canvas.rootCanvas.scaleFactor;
            ScreenPosition(label.rect, rect.center);
        }
        else
        {
            label.worldCanvas.enabled = true; label.rect.sizeDelta = identitySize;
            float worldPerPixel = camera.orthographic ? 2f * camera.orthographicSize / camera.pixelHeight :
                2f * depth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) / camera.pixelHeight;
            label.rect.localScale = Vector3.one * (worldPerPixel * scale);
            label.rect.SetPositionAndRotation(camera.ScreenToWorldPoint(new Vector3(rect.center.x, rect.center.y, depth)), camera.transform.rotation);
        }
    }

    void ScreenPosition(RectTransform rect, Vector2 point)
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, point, uiCamera, out var world))
            rect.position = world;
    }

    void DrawLeader(RectTransform line, Vector2 anchor, Rect rect, float scale)
    {
        if (line == null) return;
        anchor = new Vector2(Mathf.Clamp(anchor.x, SafeArea.xMin, SafeArea.xMax), Mathf.Clamp(anchor.y, SafeArea.yMin, SafeArea.yMax));
        Vector2 start = new(Mathf.Clamp(anchor.x, rect.xMin, rect.xMax), Mathf.Clamp(anchor.y, rect.yMin, rect.yMax));
        Vector2 travel = anchor - start;
        float end = 1f;
        foreach (Rect obstacle in occupied)
        {
            if (obstacle.Overlaps(rect)) continue; // our card's own padding
            float enter = 0f, leave = end;
            if (ClipAxis(start.x, travel.x, obstacle.xMin, obstacle.xMax, ref enter, ref leave) &&
                ClipAxis(start.y, travel.y, obstacle.yMin, obstacle.yMax, ref enter, ref leave))
                end = Mathf.Max(0f, enter - 2f * scale / travel.magnitude);
        }
        travel *= end;
        line.gameObject.SetActive(travel.magnitude > 6f * scale);
        line.sizeDelta = new Vector2(travel.magnitude / canvas.rootCanvas.scaleFactor, 1.5f * scale / canvas.rootCanvas.scaleFactor);
        ScreenPosition(line, start + travel * .5f);
        line.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg);
    }

    // Restrict the line parameter to the portion inside one axis of an obstacle.
    static bool ClipAxis(float start, float travel, float min, float max, ref float enter, ref float leave)
    {
        if (Mathf.Approximately(travel, 0f)) return start >= min && start <= max;
        float a = (min - start) / travel, b = (max - start) / travel;
        enter = Mathf.Max(enter, Mathf.Min(a, b)); leave = Mathf.Min(leave, Mathf.Max(a, b));
        return enter <= leave;
    }

    void HideAll()
    {
        foreach (var label in names)
        {
            if (label.worldCanvas != null) label.worldCanvas.enabled = false;
            if (label.leader != null) label.leader.gameObject.SetActive(false);
        }
        foreach (var label in prompts)
        {
            if (label.rect != null) label.rect.gameObject.SetActive(false);
            if (label.leader != null) label.leader.gameObject.SetActive(false);
        }
    }

    void OnDisable() { HideAll(); placed.Clear(); }
    void OnDestroy()
    {
        foreach (var label in names) if (label.leader != null) Destroy(label.leader.gameObject);
        ClearPrompts();
    }
}

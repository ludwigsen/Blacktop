using UnityEngine;
using TMPro;

// Sits on every player capsule alongside TeamMember. Points at a TeamRoster asset + a
// playerId, looks up that player's row at boot, builds a runtime PlayerAttributes from
// it, and pushes it into every component that needs one. Also spawns a floating
// nameplate — the visible "likeness" marker until real character meshes/faces exist.
//
// One TeamRoster asset covers an entire team's player pool (32 teams total in the end
// state, not 32x7 individual assets). This component is the only per-instance wiring
// that has to happen by hand: which row, on which capsule.
//
// DefaultExecutionOrder(-100): MUST run before PlayerStateMachine.Awake(), which reads
// its own `attributes` field ONCE to build PlayerContext. Unity guarantees Awake()
// ordering across ALL objects by DefaultExecutionOrder, project-wide, regardless of
// GameObject hierarchy order.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(TeamMember))]
public class CharacterApplier : MonoBehaviour
{
    [SerializeField] TeamRoster roster;
    [SerializeField] int playerId;

    [Header("Nameplate")]
    [SerializeField] bool showNameplate = true;
    [SerializeField] float nameplateHeight = 2.6f;
    [SerializeField] int nameplateFontSize = 28;

    public TeamRoster.PlayerRecord? Record { get; private set; }

    void Awake()
    {
        if (roster == null)
        {
            Debug.LogWarning($"[CharacterApplier] {name} has no TeamRoster assigned — leaving whatever attributes are already wired per-component.", this);
            return;
        }

        var record = roster.GetById(playerId);
        if (!record.HasValue)
        {
            Debug.LogWarning($"[CharacterApplier] {name}: player id {playerId} not found in {roster.name}.", this);
            return;
        }

        Record = record;
        var attr = PlayerAttributes.CreateFromRecord(record.Value, roster.curves);
        ApplyAttributes(attr);

        if (showNameplate) BuildNameplate(record.Value);
    }

    void ApplyAttributes(PlayerAttributes attr)
    {
        if (TryGetComponent<PlayerMovement>(out var movement)) movement.SetAttributes(attr);
        if (TryGetComponent<PlayerStateMachine>(out var stateMachine)) stateMachine.SetAttributes(attr);
        if (TryGetComponent<DefenderAI>(out var defenderAI)) defenderAI.SetAttributes(attr);
        if (TryGetComponent<AllyBlocker>(out var blocker)) blocker.SetAttributes(attr);
        if (TryGetComponent<GamebreakerController>(out var gamebreaker)) gamebreaker.SetAttributes(attr);
    }

    void BuildNameplate(TeamRoster.PlayerRecord record)
    {
        var canvasGO = new GameObject("Nameplate", typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, nameplateHeight, 0f);
        canvasGO.transform.localScale = Vector3.one * 0.02f;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220f, 60f);

        var textGO = new GameObject("Label", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(canvasGO.transform, false);

        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.text = $"#{record.jerseyNumber} {record.displayName}";
        text.fontSize = nameplateFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = record.accentColor;

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        canvasGO.AddComponent<Billboard>();
    }
}
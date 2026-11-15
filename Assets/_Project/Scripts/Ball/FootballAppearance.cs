using UnityEngine;

// Moves only the rendered football, after gameplay and the animated hand poses update.
// BallController remains the sole owner of possession, flight, recovery and rulings.
[DefaultExecutionOrder(1100)]
[DisallowMultipleComponent]
public class FootballAppearance : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] FootballStyle selectedStyle;
    [SerializeField] FootballStyle[] availableStyles;
    [Header("Animated ball transfer")]
    [SerializeField, Min(0.01f)] float handoffBlendSeconds = 0.16f;
    [SerializeField, Min(0.01f)] float passBlendSeconds = 0.07f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
    MaterialPropertyBlock propertyBlock;
    BallController ball;
    Vector3 restPosition;
    Quaternion restRotation;
    PlayerAnimationPresenter exchangeGiver;
    Pose releasedGrip;
    Vector3 releaseOffset;
    float handoffElapsed;
    float passElapsed = float.PositiveInfinity;

    public FootballStyle SelectedStyle => selectedStyle;
    public int StyleCount => availableStyles != null ? availableStyles.Length : 0;
    public FootballStyle StyleAt(int index) => index >= 0 && index < StyleCount ? availableStyles[index] : null;

    void Awake()
    {
        ball = GetComponentInParent<BallController>();
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        ApplyStyle(selectedStyle);
    }

    void LateUpdate() => UpdateBallVisual(Time.deltaTime);

    void OnDisable()
    {
        ClearTransfer();
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
    }

    internal void ClearTransfer()
    {
        exchangeGiver = null;
        passElapsed = float.PositiveInfinity;
    }

    internal void BeginPass(PlayerAnimationPresenter thrower)
    {
        if (!thrower.TryGetBallGrip(out releasedGrip)) return;
        releaseOffset = releasedGrip.position - ball.RulingPosition;
        passElapsed = 0f;
    }

    internal void BeginHandoff(PlayerAnimationPresenter giver)
    {
        exchangeGiver = giver;
        handoffElapsed = 0f;
    }

    void UpdateBallVisual(float deltaTime)
    {
        if (ball == null) return; // The visual prefab can also be previewed by itself.
        var carrier = ball.Carrier;
        if (ball.IsHeld && carrier != null &&
            carrier.TryGetComponent<PlayerAnimationPresenter>(out var holder) &&
            holder.TryGetBallGrip(out var grip))
        {
            if (exchangeGiver != null)
            {
                handoffElapsed += deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, handoffElapsed / handoffBlendSeconds);
                if (exchangeGiver.TryGetBallGrip(out var from))
                    grip = new Pose(Vector3.Lerp(from.position, grip.position, t), Quaternion.Slerp(from.rotation, grip.rotation, t));
                if (t >= 1f) exchangeGiver = null;
            }

            transform.SetPositionAndRotation(grip.position, grip.rotation);
        }
        else if (ball.IsForwardPass && passElapsed < passBlendSeconds)
        {
            passElapsed += deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, passElapsed / passBlendSeconds);
            // Decay only the launch offset. The visual travels with the real ball
            // immediately, keeping fast passes readable at gameplay distance.
            transform.SetPositionAndRotation(ball.transform.TransformPoint(restPosition) + releaseOffset * (1f - t),
                Quaternion.Slerp(releasedGrip.rotation, ball.transform.rotation * restRotation, t));
        }
        else transform.SetLocalPositionAndRotation(restPosition, restRotation);
    }

    public bool SelectStyle(int index)
    {
        var style = StyleAt(index);
        if (style == null) return false;
        ApplyStyle(style);
        return true;
    }

    public void ApplyStyle(FootballStyle style)
    {
        if (style == null || targetRenderer == null) return;
        selectedStyle = style;
        propertyBlock ??= new MaterialPropertyBlock();
        int slots = Mathf.Min(6, targetRenderer.sharedMaterials.Length);
        for (int slot = 0; slot < slots; slot++)
        {
            // Per-slot overrides keep shared URP materials intact and avoid .material clones.
            propertyBlock.Clear();
            targetRenderer.GetPropertyBlock(propertyBlock, slot);
            propertyBlock.SetColor(BaseColor, style.ColorForSlot(slot));
            if (slot < 3) propertyBlock.SetFloat(Smoothness, style.leatherSmoothness);
            targetRenderer.SetPropertyBlock(propertyBlock, slot);
        }
    }

#if UNITY_EDITOR
    void OnValidate() => ApplyStyle(selectedStyle); // Preview a changed selection in the Inspector.
#endif
}

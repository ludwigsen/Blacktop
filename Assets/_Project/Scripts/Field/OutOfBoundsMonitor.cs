using System;
using UnityEngine;

/// <summary>
/// Polls one player, ball, or other tracked transform against a FieldBounds component.
/// It only reports state transitions; subscribers decide what an out-of-bounds ruling does.
/// </summary>
[DisallowMultipleComponent]
public sealed class OutOfBoundsMonitor : MonoBehaviour
{
    [SerializeField] FieldBounds fieldBounds;
    [SerializeField] Transform trackedTransform;

    public FieldBounds FieldBounds => fieldBounds;
    public Transform TrackedTransform => trackedTransform;
    public FieldBoundsState CurrentState { get; private set; } = FieldBoundsState.InPlay;

    public event Action<OutOfBoundsMonitor> EnteredSafetyBand;
    public event Action<OutOfBoundsMonitor> ExitedSafetyBand;
    public event Action<OutOfBoundsMonitor> CrossedOuterBoundary;

    bool hasInitialState;

    void Reset()
    {
        fieldBounds = GetComponentInParent<FieldBounds>();
        trackedTransform = transform;
    }

    void OnValidate()
    {
        if (fieldBounds == null) fieldBounds = GetComponentInParent<FieldBounds>();
        if (trackedTransform == null) trackedTransform = transform;
    }

    void OnEnable()
    {
        hasInitialState = false;
    }

    void Update()
    {
        Evaluate();
    }

    /// <summary>Evaluates immediately. Useful after teleporting a tracked object.</summary>
    public void Evaluate()
    {
        if (fieldBounds == null || trackedTransform == null) return;

        FieldBoundsState nextState = fieldBounds.GetState(trackedTransform.position);
        if (!hasInitialState)
        {
            CurrentState = nextState;
            hasInitialState = true;
            return;
        }

        if (nextState == CurrentState) return;

        FieldBoundsState previousState = CurrentState;
        CurrentState = nextState;

        if (previousState == FieldBoundsState.InSafetyBand && nextState != FieldBoundsState.InSafetyBand)
            ExitedSafetyBand?.Invoke(this);

        if (previousState != FieldBoundsState.InSafetyBand && nextState == FieldBoundsState.InSafetyBand)
            EnteredSafetyBand?.Invoke(this);

        if (previousState != FieldBoundsState.OutOfBounds && nextState == FieldBoundsState.OutOfBounds)
            CrossedOuterBoundary?.Invoke(this);
    }
}

using UnityEngine;

/// <summary>
/// Example subscriber. Assign an OutOfBoundsMonitor that tracks a ball or player.
/// Replace this component with the owning gameplay system in production; FieldBounds
/// intentionally has no dependency on PlayState, scoring, UI, or respawn rules.
/// </summary>
public sealed class FieldBoundsExample : MonoBehaviour
{
    [SerializeField] OutOfBoundsMonitor monitor;

    void OnEnable()
    {
        if (monitor != null) monitor.CrossedOuterBoundary += ResetTrackedObjectToPlayableBounds;
    }

    void OnDisable()
    {
        if (monitor != null) monitor.CrossedOuterBoundary -= ResetTrackedObjectToPlayableBounds;
    }

    void ResetTrackedObjectToPlayableBounds(OutOfBoundsMonitor source)
    {
        Transform tracked = source.TrackedTransform;
        if (tracked != null)
            tracked.position = source.FieldBounds.GetClosestPlayablePoint(tracked.position);
    }
}

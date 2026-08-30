using UnityEngine;

// Minimal offensive teammate — no route tree, just a fixed streak upfield from the snap.
// Exists to give PassMove/PitchMove something to target and to validate the pass/pitch/
// interception pipeline before investing in real route running. Deliberately dumb — same
// "structurally sound first, tune later" approach as everything else here.
//
// Position is no longer this script's responsibility — PlayState.ResetPlay() now
// repositions every offensive player centrally via OffensiveFormationData, same pattern
// as defenders. This class only owns route-running bookkeeping.
public class ReceiverAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float routeDepth = 12f; // distance upfield before holding position

    Vector3 snapPosition;
    bool routeComplete;

    void OnEnable()
    {
        snapPosition = transform.position;
        routeComplete = false;
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset += HandleReset;
    }

    void OnDisable()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset -= HandleReset;
    }

    // By the time this fires, PlayState.ResetPlay() has already moved this transform to
    // its formation slot — this just re-baselines the route against wherever that new
    // position is, it doesn't move anything itself.
    void HandleReset()
    {
        snapPosition = transform.position;
        routeComplete = false;
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;
        if (routeComplete) return;

        transform.position += transform.forward * moveSpeed * Time.deltaTime;

        // Holds here once it hits route depth — good enough to be a legible, catchable
        // target. Real route shapes (slants, curls, etc.) are a deliberate later pass.
        if (Vector3.Distance(snapPosition, transform.position) >= routeDepth)
            routeComplete = true;
    }
}
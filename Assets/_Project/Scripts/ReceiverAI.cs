using UnityEngine;

// Minimal offensive teammate — no route tree, just a fixed streak upfield from the snap.
// Exists to give PassMove/PitchMove something to target and to validate the pass/pitch/
// interception pipeline before investing in real route running. Deliberately dumb — same
// "structurally sound first, tune later" approach as everything else here.
public class ReceiverAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float routeDepth = 12f; // distance upfield before holding position

    // Where this receiver lines up relative to the line of scrimmage. Captured
    // automatically at Start() from wherever you hand-placed it in the scene — not
    // authored in the Inspector — so there's zero manual setup per receiver. Same
    // LOS-relative convention as FormationData.DefenderSlot.offsetFromLOS, which is
    // what lets HandleReset() below reuse PlayState's exact repositioning math.
    //
    // This is a stopgap: real offensive alignment should eventually be its own
    // FormationData asset (mirroring the defensive one) instead of "wherever it
    // happened to be placed in the editor." Fine for validating the pipeline with one
    // receiver; revisit before building a real formation.
    Vector3 offsetFromLOS;

    Vector3 snapPosition;
    bool routeComplete;

    void Start()
    {
        // Start(), not Awake() — guarantees PlayState.Instance exists by now (Awake
        // order between separate GameObjects isn't guaranteed; Start order relative to
        // all Awakes is). Same reasoning as BallController's subscribe timing.
        float losZ = PlayState.Instance != null ? PlayState.Instance.CurrentLineOfScrimmageZ : transform.position.z;
        offsetFromLOS = transform.position - new Vector3(0f, 1f, losZ);
    }

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

    // Actually moves back to the snap alignment spot now, instead of just relabeling
    // wherever the route happened to end as the new "start" — that was the bug. Uses
    // the exact same offset-from-LOS math PlayState.ResetPlay() already uses for
    // defenders, so if the LOS has moved since the last play, this receiver correctly
    // moves with it rather than staying at a fixed world position forever.
    void HandleReset()
    {
        float losZ = PlayState.Instance != null ? PlayState.Instance.CurrentLineOfScrimmageZ : transform.position.z;
        transform.position = new Vector3(0f, 1f, losZ) + offsetFromLOS;
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
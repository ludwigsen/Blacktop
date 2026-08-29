using UnityEngine;

// Minimal offensive teammate — no route tree, just a fixed streak upfield from the snap.
// Exists purely to give PassMove/PitchMove a real target and validate the throw/catch/
// interception pipeline before investing in actual route running or play calling.
// Deliberately dumb, same "structurally sound first, tune later" approach as the rest
// of the project. Needs the "Teammate" tag (add in TagManager — not something I can do
// for you from here).
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
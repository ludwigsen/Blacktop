using UnityEngine;

// Sits on GameManager, alongside PlayState. Every frame, finds all Defender-tagged
// objects, measures distance to the ball carrier, and marks the single closest one
// Engage — everyone else gets Contain. Deliberately simple (no role stickiness/hysteresis,
// no formation-aware zone assignment) — this is the smallest version that produces
// "one defender commits, others hold shape" instead of every defender beelining the
// runner. Refine later if role-flickering (closest defender changing rapidly, causing
// visible role-swapping jitter) becomes a problem once playtested.
public class DefenderCoordinator : MonoBehaviour
{
    [SerializeField] Transform ballCarrier;
    [SerializeField] string defenderTag = "Defender";

    void Awake()
    {
        if (ballCarrier == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) ballCarrier = player.transform;
        }
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;
        if (ballCarrier == null) return;

        GameObject[] defenderObjects = GameObject.FindGameObjectsWithTag(defenderTag);
        if (defenderObjects.Length == 0) return;

        // Once the ball carrier has advanced past the line of scrimmage, "holding a lane"
        // no longer makes sense — everyone converges. Assumes offense moves toward +Z,
        // consistent with the rest of the project (movement, TouchdownZone, etc.).
        bool pastLOS = PlayState.Instance != null && ballCarrier.position.z > PlayState.Instance.CurrentLineOfScrimmageZ;

        if (pastLOS)
        {
            foreach (var obj in defenderObjects)
            {
                var ai = obj.GetComponent<DefenderAI>();
                if (ai != null) ai.SetRole(DefenderAI.Role.Engage);
            }
            return; // skip the closest-only assignment below entirely once past LOS
        }

        DefenderAI closest = null;
        float closestDist = float.MaxValue;

        foreach (var obj in defenderObjects)
        {
            var ai = obj.GetComponent<DefenderAI>();
            if (ai == null) continue;

            float dist = Vector3.Distance(obj.transform.position, ballCarrier.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = ai;
            }
        }

        foreach (var obj in defenderObjects)
        {
            var ai = obj.GetComponent<DefenderAI>();
            if (ai == null) continue;
            ai.SetRole(ai == closest ? DefenderAI.Role.Engage : DefenderAI.Role.Contain);
        }
    }
}
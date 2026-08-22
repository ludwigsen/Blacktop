using UnityEngine;

// Sits on GameManager, alongside PlayState. Every frame, finds all Defender-tagged
// objects, measures distance to the ball carrier, and marks the single closest one
// Engage — everyone else gets Contain. Deliberately simple (no role stickiness/hysteresis,
// no formation-aware zone assignment) — smallest version that produces "one defender
// commits, others hold shape."
//
// Carrier is resolved live from BallController rather than cached — same reasoning as
// DefenderAI's Target property. A hardcoded Player reference would go stale the moment
// possession changes (fumble, eventual interception).
public class DefenderCoordinator : MonoBehaviour
{
    [SerializeField] string defenderTag = "Defender";

    Transform Carrier => BallController.Instance != null ? BallController.Instance.Carrier : null;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        var carrier = Carrier;
        if (carrier == null) return; // loose ball — no one to assign roles relative to yet

        GameObject[] defenderObjects = GameObject.FindGameObjectsWithTag(defenderTag);
        if (defenderObjects.Length == 0) return;

        // Once the carrier has advanced past the line of scrimmage, "holding a lane"
        // no longer makes sense — everyone converges. Assumes offense moves toward +Z,
        // consistent with the rest of the project.
        bool pastLOS = PlayState.Instance != null && carrier.position.z > PlayState.Instance.CurrentLineOfScrimmageZ;

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

            float dist = Vector3.Distance(obj.transform.position, carrier.position);
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
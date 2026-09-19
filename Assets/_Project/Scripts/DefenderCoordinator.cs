using System.Collections.Generic;
using UnityEngine;

// Sits on GameManager, alongside PlayState. Every frame, finds every player NOT on the
// ball carrier's team, measures distance to the carrier, and marks the closest one
// Engage — everyone else gets Contain. Resolves "who's on defense" from
// TeamMember.teamId rather than a hardcoded Defender tag, so this works regardless of
// which team currently has the ball — required for symmetric T1/T2 rosters where
// either side can snap the ball.
public class DefenderCoordinator : MonoBehaviour
{
    Transform Carrier => BallController.Instance != null ? BallController.Instance.Carrier : null;

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        var carrier = Carrier;
        if (carrier == null) return; // loose ball — no one to assign roles relative to yet
        if (!carrier.TryGetComponent<TeamMember>(out var carrierTeam)) return;

        var defenders = new List<DefenderAI>();
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId == carrierTeam.teamId) continue; // same team as carrier — not a defender
            if (member.TryGetComponent<DefenderAI>(out var ai)) defenders.Add(ai);
        }
        if (defenders.Count == 0) return;

        // Once the carrier has advanced past the line of scrimmage, "holding a lane"
        // no longer makes sense — everyone converges. Assumes offense moves toward +Z,
        // consistent with the rest of the project.
        bool pastLOS = PlayState.Instance != null && carrier.position.z > PlayState.Instance.CurrentLineOfScrimmageZ;

        if (pastLOS)
        {
            foreach (var ai in defenders) ai.SetRole(DefenderAI.Role.Engage);
            return;
        }

        DefenderAI closest = null;
        float closestDist = float.MaxValue;

        foreach (var ai in defenders)
        {
            float dist = Vector3.Distance(ai.transform.position, carrier.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = ai;
            }
        }

        foreach (var ai in defenders)
            ai.SetRole(ai == closest ? DefenderAI.Role.Engage : DefenderAI.Role.Contain);
    }
}
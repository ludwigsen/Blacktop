using System.Collections.Generic;
using UnityEngine;

// Shared "who's the best throw" heuristic — used by PassMove's human auto-select fallback
// AND by CPUCarrierAI's QB brain, which has no human making a read at all. One place to
// tune "openness" improves both.
public static class PassTargeting
{
    public static Transform FindBestReceiver(Transform passer, List<Transform> offensivePlayers, float maxRadius, float coneAngle)
    {
        var receivers = ReceiverTargeting.GetEligibleReceivers(offensivePlayers);

        Transform best = null;
        float bestScore = float.MinValue;

        foreach (var r in receivers)
        {
            if (r == null || !IsValidTarget(passer, r, maxRadius, coneAngle)) continue;

            float openness = NearestOpponentDistance(r.position, passer);
            float dist = Vector3.Distance(r.position, passer.position);
            float score = openness * 2f - dist * 0.1f;

            if (score > bestScore) { bestScore = score; best = r; }
        }

        return best;
    }

    static bool IsValidTarget(Transform passer, Transform receiver, float maxRadius, float coneAngle)
    {
        Vector3 toReceiver = receiver.position - passer.position;
        if (toReceiver.magnitude > maxRadius) return false;
        return Vector3.Angle(passer.forward, toReceiver) <= coneAngle;
    }

    static float NearestOpponentDistance(Vector3 pos, Transform passer)
    {
        float closest = float.MaxValue;
        foreach (var member in Object.FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!TeamMember.AreOpponents(passer, member)) continue;
            float dist = Vector3.Distance(pos, member.transform.position);
            if (dist < closest) closest = dist;
        }
        return closest == float.MaxValue ? 999f : closest;
    }
}
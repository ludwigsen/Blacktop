using System.Collections.Generic;
using UnityEngine;

// Assigns defensive roles. Pre-existing rule stays: everyone converges (Engage) once the
// carrier is past the LOS, regardless of play type — covers scrambles and busted plays
// uniformly. New: on a designed PASS play, man-coverage matchups are locked in ONCE at the
// snap (not recomputed every frame) — otherwise crossing routes would cause defenders to
// instantly swap assignments mid-route, which reads as "beat your man" being auto-corrected
// rather than an earned separation. Leftover defenders either rush the passer or fall back
// to the existing Contain (lane-hold) behavior as a crude zone/spy.
public class DefenderCoordinator : MonoBehaviour
{
    [Tooltip("How many leftover (non-covering) defenders commit to rushing the passer on a pass play. Everyone else spies/contains.")]
    [SerializeField] int passRushCount = 2;

    Transform Carrier => BallController.Instance != null ? BallController.Instance.Carrier : null;

    void OnEnable()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset += HandlePlayReset;
    }

    void OnDisable()
    {
        if (PlayState.Instance != null) PlayState.Instance.OnPlayReset -= HandlePlayReset;
    }

    void HandlePlayReset()
    {
        var carrier = Carrier;
        if (carrier == null || !carrier.TryGetComponent<TeamMember>(out var carrierTeam)) return;

        var defenders = LiveDefenders(carrierTeam.teamId);
        foreach (var ai in defenders) ai.ClearCoverTarget();
        if (defenders.Count == 0) return;

        var playCall = PlayState.Instance != null ? PlayState.Instance.CurrentPlayCall : null;
        if (playCall == null || playCall.PlayType != PlayType.Pass) return; // run/no-call: original closest-Engage/rest-Contain resolves fresh every frame below

        var receivers = ReceiverTargeting.GetEligibleReceivers(PlayState.Instance.OffensivePlayers);
        var available = new List<DefenderAI>(defenders);

        // Cascade-by-distance, same pattern BlockingCoordinator uses — each receiver claims
        // its nearest still-available defender, closest-to-the-ball receiver resolved first
        // so the most dangerous route gets first pick of coverage.
        receivers.Sort((a, b) =>
            Vector3.Distance(carrier.position, a.position).CompareTo(Vector3.Distance(carrier.position, b.position)));

        foreach (var receiver in receivers)
        {
            DefenderAI best = null;
            float bestDist = float.MaxValue;
            foreach (var ai in available)
            {
                float dist = Vector3.Distance(ai.transform.position, receiver.position);
                if (dist < bestDist) { bestDist = dist; best = ai; }
            }
            if (best == null) continue;

            best.SetRole(DefenderAI.Role.Cover);
            best.SetCoverTarget(receiver);
            available.Remove(best);
        }

        available.Sort((a, b) =>
            Vector3.Distance(carrier.position, a.transform.position).CompareTo(Vector3.Distance(carrier.position, b.transform.position)));

        for (int i = 0; i < available.Count; i++)
            available[i].SetRole(i < passRushCount ? DefenderAI.Role.Engage : DefenderAI.Role.Contain);
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;

        var carrier = Carrier;
        if (carrier == null) return; // loose ball, or mid-flight — no one to assign roles relative to
        if (!carrier.TryGetComponent<TeamMember>(out var carrierTeam)) return;

        var defenders = LiveDefenders(carrierTeam.teamId);
        if (defenders.Count == 0) return;

        bool pastLOS = PlayState.Instance != null && PlayState.Instance.IsCarrierPastLineOfScrimmage(carrier.position.z);
        if (pastLOS)
        {
            foreach (var ai in defenders) ai.SetRole(DefenderAI.Role.Engage);
            return;
        }

        var playCall = PlayState.Instance != null ? PlayState.Instance.CurrentPlayCall : null;
        if (playCall != null && playCall.PlayType == PlayType.Pass) return; // Cover/rush assignments from the snap stand as-is

        DefenderAI closest = null;
        float closestDist = float.MaxValue;
        foreach (var ai in defenders)
        {
            float dist = Vector3.Distance(ai.transform.position, carrier.position);
            if (dist < closestDist) { closestDist = dist; closest = ai; }
        }
        foreach (var ai in defenders)
            ai.SetRole(ai == closest ? DefenderAI.Role.Engage : DefenderAI.Role.Contain);
    }

    static List<DefenderAI> LiveDefenders(int carrierTeamId)
    {
        var defenders = new List<DefenderAI>();
        foreach (var member in FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (member.teamId == carrierTeamId) continue;
            if (member.TryGetComponent<DefenderAI>(out var ai) && !ai.IsUserControlled) defenders.Add(ai);
        }
        return defenders;
    }
}
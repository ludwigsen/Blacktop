using System.Collections.Generic;
using UnityEngine;

// The defensive counterpart to PlayCallData. Deliberately NOT a route tree: a defensive
// play is a formation plus one JOB per formation slot. Index N here = defenderSlots[N] in
// the formation = PlayState.DefensePlayers[N] — the same index alignment PlayCallData's
// receiverIndex already relies on. Same known risk: a mismatch is silently wrong, not an error.
//
// Leave Formation null to use PlayState's default defensive formation.
[CreateAssetMenu(menuName = "Blacktop/Defensive Play")]
public class DefensivePlayData : ScriptableObject
{
    [System.Serializable]
    public struct SlotAssignment
    {
        public DefenderJob job;

        [Tooltip("Zone only. Where this defender drops to, relative to the LOS, authored for a +Z attacker (x = lateral, z = depth downfield). Flipped automatically for the other team.")]
        public Vector3 zoneOffsetFromLOS;
    }

    [SerializeField] string displayName = "Man";
    [SerializeField] FormationData defensiveFormation;
    [SerializeField] List<SlotAssignment> assignments = new();

    public string DisplayName => displayName;
    public FormationData Formation => defensiveFormation;

    // Slots past the end of the list default to Spy — a short list degrades gracefully.
    public SlotAssignment GetAssignment(int slotIndex) =>
        slotIndex >= 0 && slotIndex < assignments.Count
            ? assignments[slotIndex]
            : new SlotAssignment { job = DefenderJob.Spy };

    // Used by CPUPlayCaller to pick situationally without hardcoding play names.
    public int RushCount => CountJobs(DefenderJob.Rush);
    public int ZoneCount => CountJobs(DefenderJob.Zone);
    public int ManCount => CountJobs(DefenderJob.Man);

    int CountJobs(DefenderJob job)
    {
        int n = 0;
        foreach (var a in assignments) if (a.job == job) n++;
        return n;
    }
}
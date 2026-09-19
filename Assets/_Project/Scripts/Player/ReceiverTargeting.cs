using System.Collections.Generic;
using UnityEngine;

// The four pass targets for the current prototype. Eligibility keys off each player's
// TeamMember.slot rather than GameObject name — this is what makes any player on the
// roster controllable without hardcoding "Ally__WR1"-style names into pass logic.
public static class ReceiverTargeting
{
    static readonly TeamMember.RosterSlot[] targetSlots =
    {
        TeamMember.RosterSlot.WR1,
        TeamMember.RosterSlot.WR2,
        TeamMember.RosterSlot.WR3,
        TeamMember.RosterSlot.RB
    };

    public static bool IsEligible(Transform player)
    {
        if (player == null) return false;
        if (!player.TryGetComponent<TeamMember>(out var member)) return false;

        foreach (var slot in targetSlots)
        {
            if (member.slot == slot) return true;
        }
        return false;
    }

    // Always return the targets in their button order: 1=WR1, 2=WR2, 3=WR3, 4=RB.
    public static List<Transform> GetEligibleReceivers(IList<Transform> players)
    {
        var receivers = new List<Transform>(targetSlots.Length);
        if (players == null) return receivers;

        foreach (var slot in targetSlots)
        {
            foreach (Transform player in players)
            {
                if (player != null && player.TryGetComponent<TeamMember>(out var member) && member.slot == slot)
                {
                    receivers.Add(player);
                    break;
                }
            }
        }

        return receivers;
    }
}
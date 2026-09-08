using System.Collections.Generic;
using UnityEngine;

// The four pass targets for the current prototype. Keeping the roster rule in one
// place prevents the UI and passing code from disagreeing about what button means what.
public static class ReceiverTargeting
{
    static readonly string[] targetNames =
    {
        "Ally__WR1",
        "Ally__WR2",
        "Ally__WR3",
        "Ally__RB"
    };

    public static bool IsEligible(Transform player)
    {
        if (player == null) return false;

        foreach (string targetName in targetNames)
        {
            if (player.name == targetName) return true;
        }

        return false;
    }

    // Always return the targets in their button order: 1=WR1, 2=WR2, 3=WR3, 4=RB.
    public static List<Transform> GetEligibleReceivers(IList<Transform> players)
    {
        var receivers = new List<Transform>(targetNames.Length);
        if (players == null) return receivers;

        foreach (string targetName in targetNames)
        {
            foreach (Transform player in players)
            {
                if (player != null && player.name == targetName)
                {
                    receivers.Add(player);
                    break;
                }
            }
        }

        return receivers;
    }
}

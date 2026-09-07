using System.Collections.Generic;
using UnityEngine;

// Bundles offensive formation + route assignments into a reusable play call.
// Assigned on PlayState at runtime or swapped per-play once play calling UI exists.
[CreateAssetMenu(menuName = "Blacktop/Play Call")]
public class PlayCallData : ScriptableObject
{
    [System.Serializable]
    public struct RouteAssignment
    {
        public int receiverIndex; // matches offensivePlayers[i] in PlayState
        public RoutePattern route;
    }

    [SerializeField] OffensiveFormationData offensiveFormation;
    [SerializeField] List<RouteAssignment> routes = new();

    public OffensiveFormationData OffensiveFormation => offensiveFormation;
    public List<RouteAssignment> Routes => routes;

    // Quick lookup: given a receiver index, return its assigned route (or None if no assignment).
    public RoutePattern GetRouteForReceiver(int receiverIndex)
    {
        foreach (var assignment in routes)
        {
            if (assignment.receiverIndex == receiverIndex)
                return assignment.route;
        }
        return RoutePattern.None;
    }
}
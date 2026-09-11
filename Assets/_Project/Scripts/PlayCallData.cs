using System.Collections.Generic;
using UnityEngine;

// Bundles offensive formation + route assignments into a reusable play call.
// v1 (uniformRoute = true, the default): every eligible receiver runs the SAME route —
// this is the "Play 1: Go / Play 2: Slants" style playbook PlayCallSelector cycles
// through pre-snap. Flip uniformRoute off and use the per-receiver routes list below
// once real route trees (actual differentiated "plays") are worth building —
// GetRouteForReceiver already supports both, so nothing calling into this needs to change.
[CreateAssetMenu(menuName = "Blacktop/Play Call")]
public class PlayCallData : ScriptableObject
{
    [System.Serializable]
    public struct RouteAssignment
    {
        public int receiverIndex; // matches offensivePlayers[i] in PlayState
        public RoutePattern route;
    }

    [Tooltip("Shown in the pre-snap play-selection HUD.")]
    [SerializeField] string displayName = "Go";

    [Tooltip("If true, every eligible receiver runs 'route' below, regardless of index. Turn off to use the per-receiver routes list instead.")]
    [SerializeField] bool uniformRoute = true;

    [Tooltip("Only used when Uniform Route is checked.")]
    [SerializeField] RoutePattern route = RoutePattern.Go;

    [SerializeField] OffensiveFormationData offensiveFormation;
    [SerializeField] List<RouteAssignment> routes = new();

    public string DisplayName => displayName;
    public OffensiveFormationData OffensiveFormation => offensiveFormation;
    public List<RouteAssignment> Routes => routes;

    // Quick lookup: given a receiver index, return its assigned route (or None if no
    // assignment). Uniform plays skip the list entirely and return the single authored
    // route for every index — this is what makes "same route for all 4 receivers" free.
    public RoutePattern GetRouteForReceiver(int receiverIndex)
    {
        if (uniformRoute) return route;

        foreach (var assignment in routes)
        {
            if (assignment.receiverIndex == receiverIndex)
                return assignment.route;
        }
        return RoutePattern.None;
    }
}
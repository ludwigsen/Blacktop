using UnityEngine;

public class ReceiverAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float routeDepth = 12f;
    [SerializeField] float slantDistance = 4f; // lateral distance for Slant
    [SerializeField] float hitchDepth = 4f;   // distance upfield for Hitch before settling

    Vector3 snapPosition;
    Vector3 targetPosition;
    RoutePattern assignedRoute = RoutePattern.None;
    bool routeComplete;

    public bool RouteComplete => routeComplete;

    void OnEnable()
    {
        snapPosition = transform.position;
        targetPosition = snapPosition;
        routeComplete = false;
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset += HandleReset;
    }

    void OnDisable()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayReset -= HandleReset;
    }

    void HandleReset()
    {
        snapPosition = transform.position;
        targetPosition = snapPosition;
        routeComplete = false;
        assignedRoute = RoutePattern.None;
    }

    // Called by PlayState when distributing routes
    public void SetRoute(RoutePattern route)
    {
        // SetRoute marks the beginning of a new rep. This also makes the opening
        // play work without requiring a prior reset event.
        snapPosition = transform.position;
        assignedRoute = route;
        routeComplete = false;
        CalculateTargetPosition();
    }

    void CalculateTargetPosition()
    {
        // Default to forward streak
        targetPosition = snapPosition + transform.forward * routeDepth;

        switch (assignedRoute)
        {
            case RoutePattern.Go:
                targetPosition = snapPosition + transform.forward * routeDepth;
                break;
            case RoutePattern.Slant:
                targetPosition = snapPosition + (transform.forward * routeDepth * 0.7f) + (transform.right * slantDistance);
                break;
            case RoutePattern.Hitch:
                targetPosition = snapPosition + transform.forward * hitchDepth;
                break;
            case RoutePattern.Wheel:
                // Curved path — for now, just go out to sideline then forward
                targetPosition = snapPosition + (transform.right * 5f) + (transform.forward * routeDepth * 0.5f);
                break;
            case RoutePattern.Comeback:
                // Go deep then come back toward snap spot
                targetPosition = snapPosition + transform.forward * routeDepth + (-transform.forward * routeDepth * 0.4f);
                break;
            case RoutePattern.None:
                targetPosition = snapPosition; // stay at snap point
                break;
        }
    }

    void Update()
    {
        if (PlayState.Instance != null && !PlayState.Instance.IsLive) return;
        if (routeComplete) return;

        // Move toward target
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);

        // Check if arrived at target
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
            routeComplete = true;
    }
}

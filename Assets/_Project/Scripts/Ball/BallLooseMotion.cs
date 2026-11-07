using System;
using System.Collections.Generic;
using UnityEngine;

// Explicit free-ball integration. BallController alone decides recovery and rulings;
// this serializable helper only moves its transform using polled collision queries.
[Serializable]
public sealed class BallLooseMotion
{
    [SerializeField, Min(0.1f)] float gravity = 9.81f;
    [SerializeField, Min(0.01f)] float collisionRadius = 0.1f;
    [SerializeField, Min(0.01f)] float halfLength = 0.16f;
    [SerializeField, Range(0f, 1f)] float restitution = 0.48f;
    [SerializeField, Range(0f, 1f)] float contactFriction = 0.24f;
    [SerializeField, Min(0f)] float rollingDeceleration = 3f;
    [SerializeField, Min(0f)] float settleSpeed = 0.12f;
    [SerializeField, Min(0f)] float minimumBounceSpeed = 0.7f;
    [SerializeField, Range(0f, 0.5f)] float tipDeflection = 0.2f;
    [SerializeField, Min(0.001f)] float maxStep = 1f / 120f;

    readonly RaycastHit[] sweepHits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    readonly HashSet<Collider> releaseContacts = new();
    Vector3 angularVelocity;
    bool grounded;
    public Vector3 Velocity { get; private set; }
    public float Gravity => Mathf.Max(0.1f, gravity);

    public void Launch(Transform ball, Vector3 velocity)
    {
        Velocity = velocity;
        angularVelocity = new Vector3(470f, 190f, 320f);
        grounded = false;
        releaseContacts.Clear();
        // A ball released inside a tackle must first leave that contact volume.
        // This is spatial clearance, not a timed recovery immunity window.
        int count = Physics.OverlapSphereNonAlloc(ball.position, collisionRadius, overlaps,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (overlaps[i].GetComponentInParent<TeamMember>() != null)
                releaseContacts.Add(overlaps[i]);
    }

    public void Stop()
    {
        Velocity = angularVelocity = Vector3.zero;
        releaseContacts.Clear();
        grounded = false;
    }

    public bool IsReleaseContact(Collider collider) => releaseContacts.Contains(collider);

    public void Simulate(Transform ball, float deltaTime, Func<Vector3, Vector3, bool> recover)
    {
        // Substeps bound curvature error; sphere sweeps still cover the entire travel
        // of a fast ball within each step, including thin walls at low frame rates.
        float remaining = Mathf.Max(0f, deltaTime);
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, Mathf.Max(0.001f, maxStep));
            remaining -= step;
            releaseContacts.RemoveWhere(c => c == null || !c.enabled ||
                (c.ClosestPoint(ball.position) - ball.position).sqrMagnitude > collisionRadius * collisionRadius);
            if (recover(ball.position, ball.position)) return;
            if (ResolveOverlaps(ball, recover)) return;
            ball.Rotate(angularVelocity * step, Space.World);
            if (grounded)
            {
                // Rolling loses energy continuously and settles without tiny rebounds.
                Velocity = Vector3.MoveTowards(Vector3.ProjectOnPlane(Velocity, Vector3.up),
                    Vector3.zero, rollingDeceleration * step);
                angularVelocity = Vector3.MoveTowards(angularVelocity, Vector3.zero, 800f * step);
                ball.position = new Vector3(ball.position.x, Support(ball, Vector3.up), ball.position.z);
                if (Velocity.magnitude < settleSpeed) Velocity = angularVelocity = Vector3.zero;
            }
            if (Advance(ball, step, recover)) return;
        }
    }

    float Support(Transform ball, Vector3 normal)
    {
        // An ellipsoid's support changes as its pointed ends tumble. The contact
        // response uses that orientation instead of injecting random bounce impulses.
        float alignment = Vector3.Dot(ball.forward, normal);
        return Mathf.Sqrt(collisionRadius * collisionRadius +
            Mathf.Max(0f, halfLength * halfLength - collisionRadius * collisionRadius) * alignment * alignment);
    }

    bool Ignore(Collider collider, Transform ball) => collider.transform.IsChildOf(ball) ||
        releaseContacts.Contains(collider);

    bool ResolveOverlaps(Transform ball, Func<Vector3, Vector3, bool> recover)
    {
        // Sweeps do not report a collider that already encloses their origin, which
        // can happen when a transform-driven player steps into the ball this frame.
        int count = Physics.OverlapSphereNonAlloc(ball.position, collisionRadius, overlaps,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlaps[i];
            if (Ignore(collider, ball)) continue;
            Vector3 point = collider.ClosestPoint(ball.position);
            Vector3 normal = ball.position - point;
            if (normal.sqrMagnitude < 0.000001f)
            {
                normal = ball.position - collider.bounds.center;
                normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
                float extent = collider.bounds.extents.magnitude + halfLength;
                if (collider.Raycast(new Ray(ball.position + normal * extent, -normal), out var hit, extent * 2f))
                { point = hit.point; normal = hit.normal; }
            }
            normal.Normalize();
            Vector3 corrected = point + normal * (Support(ball, normal) + 0.002f);
            if (recover(ball.position, corrected)) return true;
            ball.position = corrected;
            Respond(ball, normal);
        }
        return false;
    }

    bool Advance(Transform ball, float step, Func<Vector3, Vector3, bool> recover)
    {
        // Resolve a bounded number of contacts per substep. At a closed corner we
        // discard the blocked remainder rather than advancing through the obstacle.
        for (int contact = 0; contact < 4 && step > 0f; contact++)
        {
            Vector3 acceleration = grounded ? Vector3.zero : Vector3.down * gravity;
            Vector3 start = ball.position;
            Vector3 travel = Velocity * step + acceleration * (0.5f * step * step);
            float distance = travel.magnitude;
            float fraction = 1f;
            Vector3 normal = Vector3.zero;
            if (distance > 0.000001f)
            {
                int count = Physics.SphereCastNonAlloc(start, collisionRadius, travel / distance,
                    sweepHits, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = sweepHits[i];
                    if (Ignore(hit.collider, ball) || Vector3.Dot(travel, hit.normal) >= 0f) continue;
                    float candidate = Mathf.Clamp01(hit.distance / distance);
                    if (candidate < fraction) { fraction = candidate; normal = hit.normal; }
                }
            }
            float support = Support(ball, Vector3.up);
            // Every supported venue shares the ground plane at world Y=0; rendered
            // surfaces are collider-free. No field width/length is duplicated here.
            if (start.y + travel.y < support && travel.y < 0f)
            {
                float candidate = Mathf.Clamp01((support - start.y) / travel.y);
                if (candidate <= fraction) { fraction = candidate; normal = Vector3.up; }
            }
            Vector3 end = start + travel * fraction;
            if (recover(start, end)) return true;
            ball.position = end;
            Velocity += acceleration * (step * fraction);
            if (normal == Vector3.zero) return false;
            ball.position += normal * 0.002f;
            Respond(ball, normal);
            step *= 1f - fraction;
        }
        return false;
    }

    void Respond(Transform ball, Vector3 normal)
    {
        float incoming = -Vector3.Dot(Velocity, normal);
        if (incoming <= 0f) return;
        Vector3 tangent = Vector3.ProjectOnPlane(Velocity, normal) * (1f - contactFriction);
        Vector3 tip = Vector3.ProjectOnPlane(ball.forward, normal) *
            (Vector3.Dot(ball.forward, normal) * incoming * tipDeflection);
        Velocity = Vector3.ClampMagnitude(tangent + tip + normal * (incoming * restitution), Velocity.magnitude);
        angularVelocity *= 1f - contactFriction;
        grounded = normal.y > 0.7f && incoming * restitution < minimumBounceSpeed;
        if (grounded) Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);
    }
}

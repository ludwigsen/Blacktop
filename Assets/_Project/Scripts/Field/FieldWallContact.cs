using UnityEngine;

// A planar sweep uses the gameplay capsule's footprint at ground level. Even a
// hurdling player meets the same barrier, and a fast move cannot skip a thin fence.
internal static class FieldWallContact
{
    internal const float Skin = 0.025f;
    static readonly RaycastHit[] hits = new RaycastHit[16];

    static float Radius(CapsuleCollider body) =>
        body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));

    static Vector3 GroundCenter(FieldBounds field, CapsuleCollider body, Vector3 root)
    {
        Vector3 center = root + body.transform.TransformVector(body.center);
        center.y = field.transform.position.y + Radius(body);
        return center;
    }

    public static bool IsNear(FieldBounds field, CapsuleCollider body, FieldBoundaryWall wall, float clearance)
    {
        if (field == null || body == null || !wall.IsSolid) return false;
        Collider collider = wall.WallCollider;
        Vector3 center = GroundCenter(field, body, body.transform.position);
        float reach = Radius(body) + clearance;
        return (collider.ClosestPoint(center) - center).sqrMagnitude <= reach * reach;
    }

    public static bool Sweep(FieldBounds field, CapsuleCollider body, Vector3 destination,
        out Vector3 stop, out FieldBoundaryWall wall, out Vector3 normal)
    {
        stop = destination;
        wall = null;
        normal = Vector3.zero;
        if (field == null || body == null || field.BoundaryWalls.Count == 0) return false;

        Vector3 origin = body.transform.position;
        Vector3 center = GroundCenter(field, body, origin);
        Vector3 travel = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
        float distance = travel.magnitude;
        float radius = Radius(body);

        // Sphere casts omit colliders already overlapping the origin. Recover an
        // invalid starting pose (for example, a scene edit or a venue swap) first.
        Vector3 groundedRoot = origin;
        groundedRoot.y = field.transform.position.y +
            Mathf.Abs(body.transform.lossyScale.y) * (body.height * 0.5f - body.center.y);
        int layers = 0;
        foreach (var candidate in field.BoundaryWalls)
        {
            if (!candidate.IsSolid) continue;
            Collider collider = candidate.WallCollider;
            Bounds broadPhase = collider.bounds;
            broadPhase.Expand(radius * 2f);
            if (!broadPhase.Contains(center) &&
                (distance <= 0f || !broadPhase.IntersectRay(new Ray(center, travel / distance), out float entry) ||
                 entry > distance)) continue;
            layers |= 1 << collider.gameObject.layer;

            if (!broadPhase.Contains(center) ||
                !Physics.ComputePenetration(body, groundedRoot, body.transform.rotation,
                    collider, collider.transform.position, collider.transform.rotation, out Vector3 separation, out float depth))
                continue;

            // A low concrete barrier may report up as its shortest exit. Recover
            // on the field side instead of lifting a player on top of it.
            normal = candidate.InwardNormal;
            if (normal == Vector3.zero) normal = Vector3.ProjectOnPlane(separation, Vector3.up).normalized;
            if (normal == Vector3.zero) continue;
            float push = depth;
            if (collider is BoxCollider && candidate.InwardNormal != Vector3.zero)
            {
                Vector3 face = collider.bounds.ClosestPoint(center + normal * (collider.bounds.size.magnitude + radius));
                push = Mathf.Max(0f, Vector3.Dot(face - center, normal) + radius);
            }
            stop = origin + normal * (push + Skin);
            wall = candidate;
            return true;
        }
        if (layers == 0 || distance <= 0f) return false;

        Vector3 direction = travel / distance;
        int count = Physics.SphereCastNonAlloc(center, radius, direction, hits, distance, layers, QueryTriggerInteraction.Ignore);
        // An unrelated collider on the same layer can fill the buffer. Preserve
        // the closest-wall result even in a crowded scene.
        RaycastHit[] results = hits;
        if (count == hits.Length)
        {
            results = Physics.SphereCastAll(center, radius, direction, distance, layers, QueryTriggerInteraction.Ignore);
            count = results.Length;
        }
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = results[i];
            if (hit.distance >= closest || !hit.collider.TryGetComponent(out FieldBoundaryWall candidate) ||
                candidate.FieldBounds != field || !candidate.IsSolid) continue;
            Vector3 outward = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            if (Vector3.Dot(direction, outward) >= -0.0001f) continue;
            closest = hit.distance;
            wall = candidate;
            normal = outward;
        }
        if (wall == null) return false;

        float fraction = Mathf.Max(0f, closest - Skin) / distance;
        stop = Vector3.LerpUnclamped(origin, destination, fraction);
        return true;
    }
}

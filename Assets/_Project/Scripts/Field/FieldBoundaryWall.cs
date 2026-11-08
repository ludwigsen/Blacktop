using UnityEngine;

/// <summary>
/// Marks a physical wall as protecting part of a FieldBounds boundary.
///
/// The wall's Collider provides the shape for polled player and ball contact.
/// The registered side also identifies boundary crossings contained by this wall.
///
/// A wall can cover an entire boundary or only part of it. Partial walls
/// are supported naturally because the collider itself defines the covered
/// region.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class FieldBoundaryWall : MonoBehaviour
{
    [SerializeField] FieldBounds fieldBounds;

    [Header("Boundary")]
    [SerializeField] FieldBoundarySide boundarySide;

    [Header("OOB Grace")]
    [Tooltip(
        "Maximum distance from this wall at which crossing the field boundary " +
        "is still treated as contained by the wall."
    )]
    [SerializeField, Min(0f)] float containmentGrace = 0.2f;

    Collider wallCollider;

    public FieldBounds FieldBounds => fieldBounds;
    public FieldBoundarySide BoundarySide => boundarySide;
    public Collider WallCollider => wallCollider;
    public float ContainmentGrace => containmentGrace;
    public bool IsSolid => isActiveAndEnabled && wallCollider != null && wallCollider.enabled && !wallCollider.isTrigger;

    // Field-local sides remain fixed when the teams reverse their attack direction.
    internal Vector3 InwardNormal => boundarySide switch
    {
        FieldBoundarySide.LeftSideline => fieldBounds.transform.right,
        FieldBoundarySide.RightSideline => -fieldBounds.transform.right,
        FieldBoundarySide.NearEndLine => fieldBounds.transform.forward,
        FieldBoundarySide.FarEndLine => -fieldBounds.transform.forward,
        _ => Vector3.zero
    };

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        CacheReferences();

        if (fieldBounds == null)
            fieldBounds = GetComponentInParent<FieldBounds>();

        if (fieldBounds != null)
            fieldBounds.RegisterBoundaryWall(this);
    }

    void OnDisable()
    {
        if (fieldBounds != null)
            fieldBounds.UnregisterBoundaryWall(this);
    }

    void Reset()
    {
        CacheReferences();

        if (fieldBounds == null)
            fieldBounds = GetComponentInParent<FieldBounds>();
    }

    void OnValidate()
    {
        CacheReferences();

        if (fieldBounds == null)
            fieldBounds = GetComponentInParent<FieldBounds>();

        containmentGrace = Mathf.Max(0f, containmentGrace);
    }

    void CacheReferences()
    {
        if (wallCollider == null)
            wallCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// Returns true when the supplied world position is close enough to this
    /// wall's collider to treat the boundary crossing as physically contained.
    /// </summary>
    public bool ContainsBoundaryCrossing(Vector3 worldPosition, float extraGrace = 0f)
    {
        if (!IsSolid)
            return false;

        float grace = Mathf.Max(containmentGrace, extraGrace);

        Vector3 closestPoint = wallCollider.ClosestPoint(worldPosition);
        float distance = Vector3.Distance(worldPosition, closestPoint);

        return distance <= grace;
    }
}

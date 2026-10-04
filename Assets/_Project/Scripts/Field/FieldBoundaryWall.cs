using UnityEngine;

/// <summary>
/// Marks a physical wall as protecting part of a FieldBounds boundary.
///
/// The wall's Collider provides the actual physical collision/bounce.
/// This component only tells FieldBounds that this portion of the boundary
/// is enclosed and therefore should not immediately be ruled out of bounds.
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
        if (wallCollider == null || !wallCollider.enabled)
            return false;

        float grace = Mathf.Max(containmentGrace, extraGrace);

        Vector3 closestPoint = wallCollider.ClosestPoint(worldPosition);
        float distance = Vector3.Distance(worldPosition, closestPoint);

        return distance <= grace;
    }
}
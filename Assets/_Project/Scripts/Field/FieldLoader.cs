using System.Collections.Generic;
using UnityEngine;

// One persistent gameplay root; replace only collider-free visual children between plays.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(FieldBounds))]
public sealed class FieldLoader : MonoBehaviour
{
    [SerializeField] FieldBounds fieldBounds;
    [SerializeField] FieldDefinition selectedField;
    [SerializeField] FieldDefinition defaultField;
    [SerializeField] FieldDefinition[] availableFields = System.Array.Empty<FieldDefinition>();
    [SerializeField] Renderer legacyGroundRenderer;
    [SerializeField, HideInInspector] FieldSurface activeSurface;
    [SerializeField, HideInInspector] GameObject activeDetails;
    [SerializeField, HideInInspector] FieldDefinition loadedField;

    public FieldDefinition SelectedField => selectedField != null ? selectedField : defaultField;
    public IReadOnlyList<FieldDefinition> AvailableFields => availableFields;
    public FieldDefinition LoadedField => loadedField;
    public FieldBounds Bounds => fieldBounds;
    public FieldSurface ActiveSurface => activeSurface;

    void Awake()
    {
        // Scene wiring provides bounds before FieldLines/SidelineCheck run their Awakes.
        fieldBounds = GetComponent<FieldBounds>();
        LoadSelectedField(false);
    }

    public bool SelectField(FieldDefinition definition)
    {
        if (!CanSwitch()) return false;
        FieldDefinition previous = selectedField;
        selectedField = definition;
        if (ApplySelectedField()) return true;
        selectedField = previous; // Invalid art must leave the last working venue intact.
        return false;
    }

    public bool ApplySelectedField() => LoadSelectedField(true);

    bool LoadSelectedField(bool rebuild)
    {
        if (!CanSwitch()) return false;
        fieldBounds = GetComponent<FieldBounds>();
        // Football still uses world X/Z. Reject moved/scaled roots instead of misaligning art.
        if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity ||
            transform.lossyScale != Vector3.one ||
            FindObjectsByType<FieldBounds>().Length != 1)
        {
            Debug.LogError("FieldLoader requires one active FieldBounds at world identity.", this);
            return false;
        }

        FieldDefinition definition = SelectedField;
        if (definition == null || definition.surfacePrefab == null) return false;
        if (!IsVisualOnly(definition.surfacePrefab.gameObject) ||
            (definition.detailPrefab != null && !IsVisualOnly(definition.detailPrefab)))
        {
            Debug.LogError("Field art must contain only renderers/transforms and FieldSurface; no gameplay or colliders.", this);
            return false;
        }
        if (definition.surfacePrefab.SurfaceRenderer == null) return false;

        // Reuse a serialized editor preview on startup; no duplicated surfaces or materials.
        if (!rebuild && loadedField == definition && activeSurface != null)
        {
            activeSurface.Apply(definition);
            if (legacyGroundRenderer != null) legacyGroundRenderer.enabled = false;
            return true;
        }

        RemoveVisual(activeSurface != null ? activeSurface.gameObject : null);
        RemoveVisual(activeDetails);
        activeSurface = Instantiate(definition.surfacePrefab, transform, false);
        activeSurface.name = definition.displayName + " Surface";
        activeSurface.Apply(definition);
        activeDetails = definition.detailPrefab != null ? Instantiate(definition.detailPrefab, transform, false) : null;
        loadedField = definition;
        if (legacyGroundRenderer != null) legacyGroundRenderer.enabled = false;
        return true;
    }

    static bool IsVisualOnly(GameObject root)
    {
        if (root.GetComponentInChildren<Collider>(true) != null ||
            root.GetComponentInChildren<Rigidbody>(true) != null) return false;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour == null || behaviour is not FieldSurface) return false;
        return true;
    }

    static bool CanSwitch()
    {
        // Inspector selection is a development workflow, never a live-play action.
        var play = PlayState.Instance;
        return !Application.isPlaying || play == null || (!play.IsLive && !play.IsPlayEnding && !play.IsGameOver);
    }

    static void RemoveVisual(GameObject visual)
    {
        if (visual == null) return;
        visual.SetActive(false); // Deferred Destroy cannot leave a second active surface this frame.
        if (Application.isPlaying) Destroy(visual);
        else DestroyImmediate(visual);
    }
}

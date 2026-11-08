using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Generate editable prototype boundary prefabs from the shared football footprint.
public static class FieldBoundarySetup
{
    [MenuItem("Blacktop/Fields/Generate Missing Venue Boundaries")]
    public static void Generate()
    {
        FieldSurfaceSetup.GenerateAssets();
        Assign("TheLot", "LotBoundaries", false);
        Assign("Downtown", "DowntownBoundaries", true);
        AssetDatabase.SaveAssets();
    }

    static void Assign(string fieldName, string prefabName, bool park)
    {
        var field = AssetDatabase.LoadAssetAtPath<FieldDefinition>(FieldSurfaceSetup.Data + "/" + fieldName + ".asset");
        if (field.boundaryPrefab != null) return; // Preserve hand-edited definitions.
        string path = FieldSurfaceSetup.Prefabs + "/" + prefabName + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject(prefabName);
            try
            {
                AddApron(root.transform, field.baseMaterial);
                if (park) AddParkFence(root.transform);
                else AddLotBarriers(root.transform);
                BakeVisuals(root);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        field.boundaryPrefab = prefab;
        EditorUtility.SetDirty(field);
    }

    static void AddApron(Transform root, Material material)
    {
        // Carry the visible ground through the existing safety band. The playable
        // surface, scoring rectangle and existing ground collider stay the same.
        float margin = FieldConstants.OutOfBoundsMargin;
        float side = FieldConstants.HalfWidth + margin * 0.5f;
        float end = FieldConstants.PlayLength * 0.5f + margin * 0.5f;
        float width = 2f * (FieldConstants.HalfWidth + margin);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Visual(root, "Sideline apron", PrimitiveType.Cube, new Vector3(sign * side, -0.06f, 0f),
                new Vector3(margin, 0.1f, FieldConstants.PlayLength), material);
            Visual(root, "End line apron", PrimitiveType.Cube, new Vector3(0f, -0.06f, sign * end),
                new Vector3(width, 0.1f, margin), material);
        }
    }

    static void AddLotBarriers(Transform root)
    {
        var concrete = Material("BarrierConcrete", new Color(0.58f, 0.55f, 0.47f));
        var yellow = Material("BarrierYellow", new Color(0.83f, 0.51f, 0.12f));
        const float offset = 0.55f;
        const float thickness = 0.48f;
        const float height = 0.95f;
        float length = FieldConstants.PlayLength;
        int panels = Mathf.CeilToInt(length / 3f);
        float panelLength = length / panels;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            float face = sign * (FieldConstants.HalfWidth + offset);
            float x = face + sign * thickness * 0.5f;
            Transform barrier = Wall(root, "Concrete sideline barrier", x, 0f, length, height, thickness,
                sign < 0 ? FieldBoundarySide.LeftSideline : FieldBoundarySide.RightSideline);
            for (int i = 0; i < panels; i++)
            {
                float z = -length * 0.5f + (i + 0.5f) * panelLength;
                Material color = i % 4 < 2 ? yellow : concrete;
                Visual(barrier, "Concrete segment", PrimitiveType.Cube, new Vector3(x, height * 0.5f, z),
                    new Vector3(thickness, height, panelLength - 0.035f), color);
                Visual(barrier, "Cast concrete cap", PrimitiveType.Cube, new Vector3(x, height - 0.045f, z),
                    new Vector3(thickness + 0.035f, 0.09f, panelLength - 0.035f), color);
            }
        }
    }

    static void AddParkFence(Transform root)
    {
        var posts = Material("FencePosts", new Color(0.18f, 0.27f, 0.25f));
        var wire = Material("FenceWire", new Color(0.68f, 0.72f, 0.65f));
        const float entrance = 3.5f;
        const float height = 1.9f;
        const float thickness = 0.13f;
        float x = -FieldConstants.HalfWidth - 0.55f - thickness * 0.5f;
        float length = (FieldConstants.PlayLength - entrance) * 0.5f;
        int panels = Mathf.CeilToInt(length / 3f);
        float panelLength = length / panels;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            float center = sign * (entrance + length) * 0.5f;
            Transform fence = Wall(root, "Park chain-link fence", x, center, length, height, thickness, FieldBoundarySide.LeftSideline);
            float start = center - length * 0.5f;
            for (int i = 0; i <= panels; i++)
            {
                float z = start + i * panelLength;
                Visual(fence, "Steel post", PrimitiveType.Cube, new Vector3(x, height * 0.5f, z),
                    new Vector3(thickness, height, thickness), posts);
                if (i == panels) break;
                var mesh = Visual(fence, "Chain-link panel", PrimitiveType.Quad,
                    new Vector3(x, height * 0.5f, z + panelLength * 0.5f), new Vector3(panelLength, height, 1f), wire);
                mesh.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            foreach (float y in new[] { 0.09f, height - 0.05f })
                Visual(fence, "Steel rail", PrimitiveType.Cube, new Vector3(x, y, center),
                    new Vector3(thickness, 0.07f, length), posts);
        }
    }

    static Transform Wall(Transform root, string label, float x, float z, float length, float height, float thickness, FieldBoundarySide side)
    {
        var segment = new GameObject(label);
        segment.transform.SetParent(root, false);
        var box = segment.AddComponent<BoxCollider>();
        box.center = new Vector3(x, height * 0.5f, z);
        box.size = new Vector3(thickness, height, length);
        var marker = new SerializedObject(segment.AddComponent<FieldBoundaryWall>());
        marker.FindProperty("boundarySide").enumValueIndex = (int)side;
        marker.ApplyModifiedPropertiesWithoutUndo();
        return segment.transform;
    }

    static GameObject Visual(Transform root, string name, PrimitiveType primitive, Vector3 center, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = center;
        part.transform.localScale = size;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    static void BakeVisuals(GameObject root)
    {
        // Keep collision segments individually editable while rendering the apron,
        // rails and panels in one draw per shared material.
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        var materials = renderers.GroupBy(renderer => renderer.sharedMaterial).ToArray();
        var slices = new CombineInstance[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var slice = new Mesh();
            slice.CombineMeshes(materials[i].Select(renderer => new CombineInstance
            {
                mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                transform = renderer.localToWorldMatrix
            }).ToArray());
            slices[i].mesh = slice;
        }
        var mesh = new Mesh { name = root.name + " Geometry" };
        mesh.CombineMeshes(slices, false, false);
        foreach (var slice in slices) Object.DestroyImmediate(slice.mesh);
        foreach (var renderer in renderers) Object.DestroyImmediate(renderer.gameObject);

        string path = FieldSurfaceSetup.Art + "/" + root.name + "Geometry.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            // Preserve any references if the prefab is being recovered.
            EditorUtility.CopySerialized(mesh, saved);
            EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(mesh);
            mesh = saved;
        }
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        root.AddComponent<MeshRenderer>().sharedMaterials = materials.Select(group => group.Key).ToArray();
    }

    static Material Material(string name, Color color)
    {
        string path = FieldSurfaceSetup.Art + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.16f);
        if (name == "FenceWire")
        {
            // A two-sided cutout with repeating diamond links keeps the fence
            // readable from either gameplay camera direction.
            string texturePath = FieldSurfaceSetup.Art + "/FenceLinks.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int a = (x + y) % size;
                int b = (x - y + size) % size;
                texture.SetPixel(x, y, a < 5 || b < 5 ? Color.white : Color.clear);
            }
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.4f;
            importer.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetTextureScale("_BaseMap", new Vector2(10f, 7f));
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.4f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}

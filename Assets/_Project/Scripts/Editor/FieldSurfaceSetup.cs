using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Deterministic placeholders authored through Unity APIs, never hand-written asset YAML.
public static class FieldSurfaceSetup
{
    public const string Art = "Assets/_Project/Art/Fields/Generated";
    public const string Data = "Assets/_Project/Data/Fields";
    public const string Prefabs = "Assets/_Project/Prefabs/Fields";

    [MenuItem("Blacktop/Fields/Generate Missing Phase 1 Assets")]
    public static void GenerateAssets()
    {
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Prefabs);
        AssetDatabase.Refresh();
        var asphalt = SurfaceMaterial("Asphalt", false);
        var grass = SurfaceMaterial("Grass", true);
        var chalk = MarkingMaterial("Chalk", new Color(0.88f, 0.86f, 0.75f, 0.63f));
        var parking = MarkingMaterial("ParkingPaint", new Color(0.78f, 0.72f, 0.5f, 0.55f));
        var repair = MarkingMaterial("Repair", new Color(0.09f, 0.085f, 0.08f, 0.8f));
        var wear = MarkingMaterial("DryGrass", new Color(0.45f, 0.37f, 0.14f, 0.28f));
        var surface = Load<GameObject>(Prefabs + "/SharedSurface.prefab")?.GetComponent<FieldSurface>();
        if (surface == null)
        {
            var root = new GameObject("Shared Surface");
            root.AddComponent<MeshFilter>().sharedMesh = SaveMesh("Surface", Quad(FieldConstants.HalfWidth * 2f, FieldConstants.PlayLength));
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = asphalt;
            var component = root.AddComponent<FieldSurface>();
            Set(component, "surfaceRenderer", renderer);
            surface = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/SharedSurface.prefab").GetComponent<FieldSurface>();
            Object.DestroyImmediate(root);
        }
        var lotDetails = Details("LotDetails", chalk, parking, repair, false);
        var parkDetails = Details("DowntownDetails", chalk, wear, wear, true);
        Definition("TheLot", "the-lot", "The Lot", surface, asphalt, lotDetails);
        Definition("Downtown", "downtown", "Downtown", surface, grass, parkDetails);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Blacktop/Fields/Wire SampleScene (Save Scene)")]
    public static void SetupSampleScene()
    {
        GenerateAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var loader = Object.FindAnyObjectByType<FieldLoader>();
        if (loader == null)
        {
            // Match geometry rather than a GameObject name; preserve its existing collider/GUIDs.
            Renderer ground = null;
            foreach (var candidate in Object.FindObjectsByType<MeshRenderer>())
            {
                if (candidate.GetComponent<MeshCollider>() == null) continue;
                Vector3 size = candidate.bounds.size;
                if (!Mathf.Approximately(size.x, FieldConstants.HalfWidth * 2f) ||
                    !Mathf.Approximately(size.z, FieldConstants.PlayLength)) continue;
                if (ground != null) throw new InvalidOperationException("Multiple ground candidates; wire FieldLoader's legacy renderer manually.");
                ground = candidate;
            }
            if (ground == null) throw new InvalidOperationException("No existing common-footprint ground found.");
            if (Object.FindObjectsByType<FieldBounds>().Length > 0)
                throw new InvalidOperationException("Existing bounds found; add FieldLoader there and assign its renderer manually.");
            var root = new GameObject("FieldRoot");
            root.AddComponent<FieldBounds>();
            loader = root.AddComponent<FieldLoader>();
            Set(loader, "legacyGroundRenderer", ground);
        }
        Set(loader, "fieldBounds", loader.GetComponent<FieldBounds>());
        Set(loader, "defaultField", Load<FieldDefinition>(Data + "/TheLot.asset"));
        Set(loader, "selectedField", Load<FieldDefinition>(Data + "/TheLot.asset"));
        var settings = new SerializedObject(loader);
        var catalog = settings.FindProperty("availableFields");
        catalog.arraySize = 2;
        catalog.GetArrayElementAtIndex(0).objectReferenceValue = Load<FieldDefinition>(Data + "/TheLot.asset");
        catalog.GetArrayElementAtIndex(1).objectReferenceValue = Load<FieldDefinition>(Data + "/Downtown.asset");
        settings.ApplyModifiedPropertiesWithoutUndo();
        // Explicit scene references prevent Awake ordering from choosing another field.
        foreach (var lines in Object.FindObjectsByType<FieldLines>()) Set(lines, "fieldBounds", loader.GetComponent<FieldBounds>());
        foreach (var zone in Object.FindObjectsByType<TouchdownZone>()) Set(zone, "fieldBounds", loader.GetComponent<FieldBounds>());
        foreach (var check in Object.FindObjectsByType<SidelineCheck>()) Set(check, "fieldBounds", loader.GetComponent<FieldBounds>());
        if (!loader.ApplySelectedField()) throw new InvalidOperationException("Field preview could not be created.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Definition(string file, string id, string label, FieldSurface surface, Material material, GameObject details)
    {
        string path = Data + "/" + file + ".asset";
        if (Load<FieldDefinition>(path) != null) return; // Regeneration preserves Inspector edits.
        var definition = ScriptableObject.CreateInstance<FieldDefinition>();
        definition.venueId = id;
        definition.displayName = label;
        definition.surfacePrefab = surface;
        definition.baseMaterial = material;
        definition.textureTiling = new Vector2(15f, 30f); // Repeats a two-unit placeholder tile.
        definition.detailPrefab = details;
        AssetDatabase.CreateAsset(definition, path);
    }

    static Material SurfaceMaterial(string name, bool grass)
    {
        string path = Art + "/" + name + ".mat";
        var existing = Load<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetTexture("_BaseMap", Texture(name, grass, false));
        mat.SetTexture("_BumpMap", Texture(name + "Normal", grass, true));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_BumpScale", 0.25f);
        mat.SetFloat("_Smoothness", grass ? 0.12f : 0.18f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Texture2D Texture(string name, bool grass, bool normal)
    {
        string path = Art + "/" + name + ".png";
        if (!File.Exists(path))
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var random = new System.Random(grass ? 121 : 512);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = (float)random.NextDouble();
                // Fine aggregate/blade grain gives a replaceable, restrained surface placeholder.
                float u = (float)x / size, v = (float)y / size;
                // Blend opposite edges of noise so tiles have no obvious periodic wave pattern.
                float wave = Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(u * 6f, v * 6f), Mathf.PerlinNoise((u - 1f) * 6f, v * 6f), u),
                    Mathf.Lerp(Mathf.PerlinNoise(u * 6f, (v - 1f) * 6f), Mathf.PerlinNoise((u - 1f) * 6f, (v - 1f) * 6f), u), v) - 0.5f;
                Color color = normal
                    ? new Color(0.5f + (grain - 0.5f) * 0.08f, 0.5f + wave * 0.05f, 1f)
                    : grass ? new Color(0.19f, 0.31f, 0.105f) * (0.9f + grain * 0.2f + wave * 0.08f)
                            : new Color(0.24f, 0.245f, 0.25f) * (0.9f + grain * 0.2f + wave * 0.06f);
                texture.SetPixel(x, y, color);
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.SaveAndReimport();
        return Load<Texture2D>(path);
    }

    static Material MarkingMaterial(string name, Color color)
    {
        string path = Art + "/" + name + ".mat";
        var existing = Load<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", MarkingMask(name == "DryGrass"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent - 10; // Draw decoration before gameplay overlays.
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Texture2D MarkingMask(bool wear)
    {
        string path = Art + (wear ? "/WearMask.png" : "/FadedPaintMask.png");
        if (!File.Exists(path))
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var random = new System.Random(19);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float grain = (float)random.NextDouble();
                float alpha = wear ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v))) * (0.6f + grain * 0.4f)
                                   : (grain < 0.13f ? 0.05f : 0.4f + grain * 0.6f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = wear ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        return Load<Texture2D>(path);
    }

    static GameObject Details(string name, Material chalk, Material secondary, Material repair, bool grass)
    {
        string path = Prefabs + "/" + name + ".prefab";
        var existing = Load<GameObject>(path);
        if (existing != null) return existing;
        var root = new GameObject(name);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var random = new System.Random(72);
        // Broken narrow paint/chalk outlines; no official yard numbers or stadium graphics.
        float hw = FieldConstants.HalfWidth;
        float hl = FieldConstants.PlayLength * 0.5f;
        for (float z = -hl; z < hl; z += 1.7f)
        {
            Rect(vertices, triangles, -hw, z + 0.6f, 0.12f, 1.2f, (float)random.NextDouble() * 0.05f);
            Rect(vertices, triangles, hw, z + 0.6f, 0.12f, 1.2f, (float)random.NextDouble() * 0.05f);
        }
        foreach (float z in new[] { -hl, hl })
            for (float x = -hw; x < hw; x += 1.7f) Rect(vertices, triangles, x + 0.6f, z, 1.2f, 0.13f, 0f);
        // End-zone corner indicators sit away from the full-width dynamic goal overlays.
        foreach (float z in new[] { FieldConstants.NearGoalLineZ, FieldConstants.FarGoalLineZ })
            foreach (float x in new[] { -hw + 0.8f, hw - 0.8f }) Rect(vertices, triangles, x, z, 1.5f, 0.11f, 0f);
        Child(root, "Improvised Markings", name + "Chalk", vertices, triangles, chalk, 0.008f);
        vertices.Clear(); triangles.Clear();
        if (grass)
        {
            // Irregular, soft wear is an optional transparent texture slot on this material.
            foreach (Vector3 patch in new[] { new Vector3(-8f, 0f, -12f), new Vector3(9f, 0f, 8f), new Vector3(-3f, 0f, 23f) })
                Patch(vertices, triangles, patch, random);
        }
        else
        {
            for (float z = -24f; z <= 24f; z += 8f)
                foreach (float x in new[] { -10f, 10f }) Rect(vertices, triangles, x, z, 5f, 0.1f, 0f);
            Rect(vertices, triangles, -12.5f, 0f, 0.1f, 48f, 0f);
            Rect(vertices, triangles, 12.5f, 0f, 0.1f, 48f, 0f);
        }
        Child(root, grass ? "Wear Patches" : "Faded Parking Stripes", name + "Secondary", vertices, triangles, secondary, 0.004f);
        if (!grass)
        {
            vertices.Clear(); triangles.Clear();
            foreach (Vector2 origin in new[] { new Vector2(-6f, -18f), new Vector2(5f, 12f), new Vector2(-4f, 24f) })
            {
                for (int i = 0; i < 6; i++) Rect(vertices, triangles, origin.x + i * 0.28f, origin.y + i * 0.7f, 0.07f, 0.9f, -0.35f + (float)random.NextDouble() * 0.35f);
                Rect(vertices, triangles, origin.x + 1f, origin.y - 2f, 1.8f, 1f, 0.15f);
            }
            Child(root, "Cracks and Repairs", name + "Repairs", vertices, triangles, repair, 0.003f);
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Patch(List<Vector3> vertices, List<int> triangles, Vector3 center, System.Random random)
    {
        int start = vertices.Count;
        vertices.Add(center);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            float radius = 1f + (float)random.NextDouble();
            vertices.Add(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            triangles.Add(start); triangles.Add(start + (i + 1) % 12 + 1); triangles.Add(start + i + 1);
        }
    }

    static void Child(GameObject root, string name, string meshName, List<Vector3> vertices, List<int> triangles, Material material, float height)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = Vector3.up * height; // Below FieldLines' existing 0.02 lift.
        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.uv = vertices.ConvertAll(v => new Vector2(v.x, v.z)).ToArray();
        if (name == "Wear Patches")
        {
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++)
            {
                int ring = i % 13 - 1;
                uv[i] = ring < 0 ? Vector2.one * 0.5f : new Vector2(0.5f + Mathf.Cos(ring * Mathf.PI * 2f / 12f) * 0.5f,
                    0.5f + Mathf.Sin(ring * Mathf.PI * 2f / 12f) * 0.5f);
            }
            mesh.uv = uv;
        }
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = SaveMesh(meshName, mesh);
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        // No primitive creation: decorative objects never acquire colliders.
    }

    static void Rect(List<Vector3> vertices, List<int> triangles, float x, float z, float width, float length, float angle)
    {
        int start = vertices.Count;
        foreach (Vector2 p in new[] { new Vector2(-width, -length), new Vector2(-width, length), new Vector2(width, length), new Vector2(width, -length) })
            vertices.Add(new Vector3(x + (p.x * Mathf.Cos(angle) - p.y * Mathf.Sin(angle)) * 0.5f, 0f,
                z + (p.x * Mathf.Sin(angle) + p.y * Mathf.Cos(angle)) * 0.5f));
        triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    static Mesh Quad(float width, float length)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        Rect(vertices, triangles, 0f, 0f, width, length, 0f);
        var mesh = new Mesh { name = "Shared Field Surface" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = Art + "/" + name + ".asset";
        var existing = Load<Mesh>(path);
        if (existing != null) { Object.DestroyImmediate(mesh); return existing; }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void Set(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

// Apply explicitly: OnValidate should not instantiate/destroy hierarchies during import.
[CustomEditor(typeof(FieldLoader))]
public sealed class FieldLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!GUILayout.Button("Apply Selected Field")) return;
        var loader = (FieldLoader)target;
        if (loader.ApplySelectedField() && !Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
    }
}

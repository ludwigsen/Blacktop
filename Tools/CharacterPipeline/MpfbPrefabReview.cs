using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Build the six visual-only prefabs in an isolated project before wiring any match actor.
public static class MpfbPrefabReview
{
    public const string Models = "Assets/_Project/Art/Characters/Models/";
    public const string Prefabs = "Assets/_Project/Prefabs/Characters/";
    public const string Data = "Assets/_Project/Data/Characters/MPFB/";
    const string ScenePath = "Assets/Scenes/MPFBCharacterReview.unity";
    public static readonly string[] Names = {"Slim", "Athletic", "Muscular", "Stocky", "Power", "Massive"};

    public static void Run()
    {
        try
        {
            foreach (var folder in new[]{Prefabs,Data})
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Data+"Body.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Smoothness", .25f);
                AssetDatabase.CreateAsset(material, Data+"Body.mat");
            }
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Art/Animations/BlacktopCharacterController.controller");
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i];
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Models+"BT_"+name+".fbx");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                skin.sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                string path = Prefabs+name.ToUpperInvariant()+".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(model,path);
                UnityEngine.Object.DestroyImmediate(model);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3((i-2.5f)*1.5f,0,0);
                var skeleton = AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>(Data+name+".asset");
                if (!skeleton) {
                    skeleton = ScriptableObject.CreateInstance<CharacterSkeletonDefinition>();
                    AssetDatabase.CreateAsset(skeleton, Data+name+".asset");
                }
                skeleton.id = name.ToUpperInvariant();
                skeleton.referenceModel = prefab;
                skeleton.referenceSkin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                // Calibrate the visual to the existing height profiles using the imported rest skin.
                var rest = new Mesh();
                skeleton.referenceSkin.BakeMesh(rest);
                var heights = rest.vertices.Select(v => skeleton.referenceSkin.transform.TransformPoint(v).y).ToArray();
                skeleton.restHeightMeters = heights.Max() - heights.Min();
                UnityEngine.Object.DestroyImmediate(rest);
                EditorUtility.SetDirty(skeleton);
            }
            var camera = new GameObject("Lineup camera",typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0,1,9);
            camera.transform.LookAt(new Vector3(0,.86f,0));
            camera.orthographic = true;
            camera.orthographicSize = 3;
            var light = new GameObject("Lineup light",typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35,160,0);
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Scene save failed");
            AssetDatabase.SaveAssets();
            var reopened = EditorSceneManager.OpenScene(ScenePath);
            var actors = reopened.GetRootGameObjects().Where(o=>o.GetComponent<Animator>()).ToArray();
            if (actors.Length != Names.Length) throw new Exception("Missing lineup prefab");
            foreach (var actor in actors)
            {
                var a = actor.GetComponent<Animator>();
                var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                if (!PrefabUtility.IsPartOfPrefabInstance(actor) || !a.avatar.isValid || !a.avatar.isHuman ||
                    a.runtimeAnimatorController != controller || a.applyRootMotion || skin.sharedMaterial != material ||
                    actor.GetComponentInChildren<Collider>()) throw new Exception("Invalid lineup prefab "+actor.name);
                a.SetFloat("Locomotion",1);
                a.Play("Base Layer.Locomotion",0,0);
                a.Update(.15f);
                if (!a.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name=="Standard Run" && c.weight>.9f))
                    throw new Exception("Missing run pose "+actor.name);
            }
            Debug.Log("MPFB lineup PASS: six reloaded prefab instances, six local Avatars, shared controller, run clip, no colliders");
            EditorApplication.Exit(0);
        }
        catch (Exception error) {Debug.LogException(error); EditorApplication.Exit(1);}
    }
}

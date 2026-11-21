using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Install in an Editor directory in the isolated review project. All outputs go to Builds.
public static class MpfbModelReview
{
    const string Models = "Assets/_Project/Art/Characters/Models";
    const string Controller = "Assets/_Project/Art/Animations/BlacktopCharacterController.controller";

    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var path in Directory.GetFiles(Models, "BT_*.fbx").OrderBy(p => p))
                Review(path.Replace('\\', '/'));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    static Dictionary<string, string> BoneMap()
    {
        var map = new Dictionary<string, string> {
            {"Hips","pelvis"}, {"Spine","spine_01"}, {"Chest","spine_02"},
            {"UpperChest","spine_03"}, {"Neck","neck_01"}, {"Head","head"}
        };
        foreach (string side in new[] {"Left","Right"})
        {
            string suffix = side == "Left" ? "_l" : "_r";
            foreach (var pair in new Dictionary<string,string> {
                {"Shoulder","clavicle"}, {"UpperArm","upperarm"}, {"LowerArm","lowerarm"}, {"Hand","hand"},
                {"UpperLeg","thigh"}, {"LowerLeg","calf"}, {"Foot","foot"}, {"Toes","ball"} })
                map.Add(side + pair.Key, pair.Value + suffix);
            foreach (var digit in new[] {"Thumb","Index","Middle","Ring","Little"})
            {
                string bone = digit == "Little" ? "pinky" : digit.ToLowerInvariant();
                var segments = new[] {"Proximal","Intermediate","Distal"};
                for (int i = 0; i < segments.Length; i++)
                    map.Add(side + " " + digit + " " + segments[i], bone + "_0" + (i + 1) + suffix);
            }
        }
        return map;
    }

    static void Review(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.addCollider = false;
        importer.optimizeGameObjects = false;
        importer.globalScale = 1;
        importer.bakeAxisConversion = true;
        importer.isReadable = true; // Review measures deformed vertex positions.
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var desc = importer.humanDescription;
        desc.human = BoneMap().Select(pair => new HumanBone {
            humanName = pair.Key, boneName = pair.Value, limit = new HumanLimit {useDefaultValues = true}
        }).ToArray();
        importer.humanDescription = desc;
        importer.SaveAndReimport();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var model = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var animator = model.GetComponent<Animator>();
            Require(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, path + " invalid Avatar");
            var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            var rest = new Mesh();
            skin.BakeMesh(rest);
            var points = rest.vertices.Select(p => skin.transform.TransformPoint(p)).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            Debug.Log($"MPFB {model.name} source bounds {bounds.ToString("F4")} avatar {animator.avatar.name}");
            Require(Mathf.Abs(bounds.size.y - 1.7291f) < .015f, "Unexpected imported height");
            Require(model.GetComponentsInChildren<Collider>().Length == 0, "Unexpected imported collider");
            foreach (var key in BoneMap().Keys)
            {
                int index = Array.IndexOf(HumanTrait.BoneName, key);
                Require(index >= 0 && animator.GetBoneTransform((HumanBodyBones)index), "Missing mapping " + key);
            }
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            var outDir = Path.Combine("Builds/MPFBReview", Path.GetFileNameWithoutExtension(path));
            Directory.CreateDirectory(outDir);
            var rows = new List<string> {"case,samples,vertex_motion_m,minimum_height_m,maximum_height_m"};
            foreach (var test in new[] {
                ("Idle",0f,"","mixamo.com"), ("Run",1f,"","Standard Run"), ("Sprint",2f,"","Fast Run"),
                ("Backpedal",-1f,"","Running Backward"), ("Hurdle",0f,"","Jump"),
                ("CarryReady",0f,"Carry","Carry Ready"), ("CarryTuck",0f,"Carry","Carry Tuck"),
                ("ThrowWindup",0f,"Throw Windup","Throw Windup"), ("ThrowFollowThrough",0f,"Throw Follow Through","Throw Follow Through"),
                ("HandoffGive",0f,"Handoff Give","Handoff Give"), ("HandoffTake",0f,"Handoff Take","Handoff Take") })
            {
                string label = test.Item1;
                animator.SetFloat("Locomotion", test.Item2);
                animator.SetFloat("LocomotionRate", 1);
                animator.SetFloat("HurdleRate", 1);
                animator.SetBool("Hurdling", label == "Hurdle");
                animator.SetFloat("CarryPace", label == "CarryTuck" ? 1 : 0);
                bool football = test.Item3.Length > 0;
                animator.SetLayerWeight(1, football ? 1 : 0);
                float motion = 0, low = float.MaxValue, high = 0;
                Vector3[] first = null;
                for (int sample = 0; sample < 9; sample++)
                {
                    float time = sample / 8f * .96f;
                    animator.Play(label == "Hurdle" ? "Base Layer.Hurdle" : "Base Layer.Locomotion", 0, football ? 0 : time);
                    if (football) animator.Play("Football." + test.Item3, 1, time);
                    animator.SetFloat("ThrowWindupTime", time);
                    animator.SetFloat("FootballActionTime", time);
                    animator.Update(0);
                    var clips = animator.GetCurrentAnimatorClipInfo(football ? 1 : 0);
                    Require(clips.Any(c => c.weight > .9f && c.clip.name == test.Item4), "Expected controller clip " + test.Item4 + ", got " + string.Join(", ", clips.Select(c => c.clip.name + " " + c.weight)));
                    skin.BakeMesh(rest);
                    var baked = rest.vertices.Select(p => skin.transform.TransformPoint(p)).ToArray();
                    first ??= baked;
                    for (int i = 0; i < baked.Length; i++)
                    {
                        Require(float.IsFinite(baked[i].sqrMagnitude), label + " non-finite vertex");
                        motion = Mathf.Max(motion, Vector3.Distance(baked[i],first[i]));
                    }
                    float height = baked.Max(p => p.y) - baked.Min(p => p.y);
                    low = Mathf.Min(low,height);
                    high = Mathf.Max(high,height);
                    if (sample == 2 || sample == 6) Capture(skin, rest, Path.Combine(outDir,label + sample + ".png"));
                }
                Require(motion > .0001f && high < 2.5f && low > .5f, label + " invalid baked motion or scale");
                rows.Add(FormattableString.Invariant($"{label},9,{motion:F6},{low:F5},{high:F5}"));
                File.WriteAllLines(Path.Combine(outDir,"samples.csv"), rows);
            }
            UnityEngine.Object.DestroyImmediate(rest);
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    static void Capture(SkinnedMeshRenderer skin, Mesh sample, string path)
    {
        // A lit three-quarter view at a fixed distance makes the archetypes comparable.
        // Edit-mode camera renders can reuse a cached skin. Render the verified BakeMesh sample.
        var snapshot = new GameObject("Pose sample", typeof(MeshFilter), typeof(MeshRenderer));
        snapshot.transform.SetParent(skin.transform, false);
        snapshot.GetComponent<MeshFilter>().sharedMesh = sample;
        snapshot.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
        skin.enabled = false;
        var cameraObject = new GameObject("Review Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var lightObject = new GameObject("Review Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(32,160,0);
        camera.transform.position = new Vector3(2f,1.20f,3.6f);
        camera.transform.LookAt(new Vector3(0,.92f,0));
        camera.orthographic = true;
        camera.orthographicSize = 1.15f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.18f,.19f,.20f);
        var texture = RenderTexture.GetTemporary(320,320,24);
        var previous = RenderTexture.active;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);
        image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(texture);
        skin.enabled = true;
        UnityEngine.Object.DestroyImmediate(snapshot);
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(lightObject);
    }
}

// Copy into Assets/Editor in a disposable Blacktop project for the two batch commands.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static class FootballClipRoundTrip
{
    const string Art = "Assets/_Project/Art/Animations/";
    const string Candidates = "Assets/FootballAnimationReview/";
    const string ReviewOverride = Candidates + "Football Review.overrideController";
    const string Model = "Assets/_Project/Prefabs/PlayerModel.prefab";
    const string Skeleton = "Assets/_Project/Data/Characters/CanonicalSkeleton.asset";
    const int Rate = 100;
    static readonly string[] Names = { "Throw Windup", "Throw Follow Through", "Carry Ready", "Carry Tuck", "Handoff Give", "Handoff Take" };
    static AvatarMask FootballMask => AssetDatabase.LoadAssetAtPath<AvatarMask>(Art + "Football Upper Body.mask");
    static AnimationClip PreviewBase => AssetDatabase.LoadAllAssetsAtPath(Art + "Breathing Idle.fbx").OfType<AnimationClip>()
        .Single(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));

    [Serializable] class BakeData { public int rate; public Joint[] joints; public Motion[] clips; }
    [Serializable] class Joint { public string name, parent; public float[] rest; }
    [Serializable] class Motion { public string name, guid; public float duration; public bool loop; public Frame[] frames; }
    [Serializable] class Frame { public float[] matrices; }
    [Serializable] class Check
    {
        public string name, reviewGuid, worstPositionJoint, worstRotationJoint;
        public long reviewFileId;
        public int joints, samples, mappedHumanoidBones;
        public float seconds, maxBoneErrorMeters, maxBoneErrorDegrees, maxRelativeScaleError;
        public float maxFootErrorMeters, maxRootErrorMeters, averageRootSpeed, maxWristErrorMeters;
        public bool humanoid, loop, pass;
    }
    [Serializable] class Report { public Check[] clips; }

    // Record evaluated skin-space transforms on the historical player Avatar and rig used to author the baseline.
    public static void Bake()
    {
        using var rig = new PreviewRig();
        var data = new BakeData { rate = Rate, joints = rig.DescribeJoints(), clips = Names.Select(name =>
        {
            var clip = Original(name);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (!clip.humanMotion || bindings.Any(b => b.type != typeof(Animator) || b.path != ""))
                throw new InvalidOperationException($"{name}: expected Humanoid muscle bindings.");
            int count = Mathf.RoundToInt(clip.length * Rate);
            if (Mathf.Abs(count / (float)Rate - clip.length) > 0.00001f)
                throw new InvalidOperationException($"{name}: choose a sampling rate that retains the exact endpoint.");
            // Muscle-only clips lack a root/body trajectory. A fixed base pose gives
            // Blender the same visible layer pose without baking a locomotion cycle.
            using var sampler = new Sampler(rig.Animator, clip, PreviewBase, FootballMask);
            var frames = new Frame[count + 1];
            for (int n = 0; n <= count; n++)
            {
                sampler.Sample(n / (double)Rate);
                frames[n] = new Frame { matrices = rig.Matrices().SelectMany(MatrixValues).ToArray() };
            }
            Debug.Log($"BLACKTOP_BAKE {name}: {bindings.Length} Animator curves, {count + 1} frames, {clip.length:F3}s");
            return new Motion { name = name, guid = AssetDatabase.AssetPathToGUID(Art + name + ".anim"),
                duration = clip.length, loop = clip.isLooping, frames = frames };
        }).ToArray() };
        File.WriteAllText(OutPath("unity-bake.json"), JsonUtility.ToJson(data));
    }

    static HumanDescription CanonicalMapping()
    {
        var reference = AssetDatabase.LoadAssetAtPath<CharacterSkeletonDefinition>(Skeleton);
        return reference.referenceModel.GetComponentInChildren<Animator>(true).avatar.humanDescription;
    }

    [MenuItem("Blacktop/Animations/Import Football Review FBXs")]
    public static void ImportReview()
    {
        var humanoid = CanonicalMapping();
        var replacements = new Dictionary<AnimationClip, AnimationClip>();
        foreach (string name in Names)
        {
            var original = Original(name);
            var clip = ImportCandidate(Candidates + name + ".fbx", original, humanoid);
            if (!clip.humanMotion || Mathf.Abs(clip.length - original.length) > 0.001f ||
                clip.isLooping != original.isLooping || clip.events.Length != 0 || clip.averageSpeed.magnitude > 0.001f)
                throw new InvalidOperationException($"{name}: review clip timing, loop or root motion differs from the original.");
            Debug.Log($"BLACKTOP_REVIEW {name}: {clip.length:F3}s, Humanoid, root speed {clip.averageSpeed.magnitude:F5} m/s");
            replacements.Add(original, clip);
        }
        WireReviewOverride(replacements);
    }

    static void WireReviewOverride(Dictionary<AnimationClip, AnimationClip> replacements)
    {
        // Keep the original state machine and motion slots. Only the disposable
        // override asset points those slots at the imported review clips.
        var original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + "BlacktopCharacterController.controller");
        var existing = AssetDatabase.LoadMainAssetAtPath(ReviewOverride);
        var review = existing as AnimatorOverrideController;
        if (existing != null && (review == null || review.runtimeAnimatorController != original))
            throw new InvalidOperationException($"Choose an empty review location or restore the original base for {ReviewOverride}.");
        if (review == null)
        {
            review = new AnimatorOverrideController(original);
            AssetDatabase.CreateAsset(review, ReviewOverride);
        }
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(review.overridesCount);
        review.GetOverrides(overrides);
        if (overrides.Count(pair => replacements.ContainsKey(pair.Key)) != replacements.Count)
            throw new InvalidOperationException("The selected base controller does not contain all six original football motions.");
        for (int i = 0; i < overrides.Count; i++)
            if (replacements.TryGetValue(overrides[i].Key, out var clip))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
        review.ApplyOverrides(overrides);
        EditorUtility.SetDirty(review);
        AssetDatabase.SaveAssetIfDirty(review);
    }

    public static void ImportAndCompare()
    {
        var humanoid = CanonicalMapping();
        using var originalRig = new PreviewRig();
        using var reviewRig = new PreviewRig();
        var checks = new List<Check>();
        foreach (string name in Names)
        {
            var original = Original(name);
            var path = Candidates + name + ".fbx";
            var clip = ImportCandidate(path, original, humanoid);
            var reviewAvatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Single();
            var sourceMap = humanoid.human.ToDictionary(bone => bone.humanName, bone => bone.boneName);
            var reviewMap = reviewAvatar.humanDescription.human.ToDictionary(bone => bone.humanName, bone => bone.boneName);
            var modelBones = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Transform>(true)
                .ToDictionary(bone => bone.name);
            var jointNames = originalRig.Names;
            if (!reviewAvatar.isValid || !reviewAvatar.isHuman || sourceMap.Count != reviewMap.Count ||
                sourceMap.Any(pair => !reviewMap.TryGetValue(pair.Key, out string mapped) || mapped != pair.Value) ||
                jointNames.Any(joint => !modelBones.ContainsKey(joint)))
                throw new InvalidOperationException($"Invalid canonical skeleton or Humanoid mapping in {path}.");
            foreach (var joint in originalRig.DescribeJoints())
                if (joint.parent != "" && modelBones[joint.name].parent.name != joint.parent)
                    throw new InvalidOperationException($"Changed FBX parent at {name}: {joint.name}.");

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long fileId);
            var check = new Check { name = name, seconds = clip.length,
                joints = jointNames.Length, mappedHumanoidBones = sourceMap.Count, reviewGuid = guid, reviewFileId = fileId,
                humanoid = clip.humanMotion, loop = clip.isLooping, averageRootSpeed = clip.averageSpeed.magnitude };
            // Validate against the same idle reference and a running base pose on the historical authoring Avatar.
            var run = AssetDatabase.LoadAllAssetsAtPath(Art + "Standard Run.fbx").OfType<AnimationClip>()
                .Single(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            int steps = Mathf.RoundToInt(original.length * Rate * 2);
            foreach (var baseClip in new[] { PreviewBase, run })
            {
                using var a = new Sampler(originalRig.Animator, original, baseClip, FootballMask);
                using var b = new Sampler(reviewRig.Animator, clip, baseClip, FootballMask);
                for (int i = 0; i <= steps; i++)
                {
                    double time = Math.Min(i / (Rate * 2.0), original.length);
                    a.Sample(time);
                    b.Sample(time);
                    Compare(originalRig.Matrices(), reviewRig.Matrices(), jointNames, check);
                    check.samples++;
                }
            }
            check.pass = check.humanoid && check.loop == original.isLooping &&
                Mathf.Abs(clip.length - original.length) < 0.001f && clip.events.Length == 0 &&
                check.maxBoneErrorMeters <= 0.01f && check.maxBoneErrorDegrees <= 2f &&
                check.maxFootErrorMeters <= 0.001f && check.maxRootErrorMeters <= 0.001f &&
                check.maxRelativeScaleError <= 0.001f && check.averageRootSpeed <= 0.001f;
            checks.Add(check);
            Debug.Log($"BLACKTOP_COMPARE {name}: {JsonUtility.ToJson(check)}");
        }
        File.WriteAllText(OutPath("unity-round-trip.json"), JsonUtility.ToJson(new Report { clips = checks.ToArray() }, true));
        if (checks.Any(check => !check.pass)) throw new InvalidOperationException("One or more FBX clips exceeded the comparison limits.");
    }

    static AnimationClip ImportCandidate(string path, AnimationClip original, HumanDescription humanoid)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter ??
            throw new FileNotFoundException("Copy the review FBXs into the disposable project.", path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // Keep the canonical mapping but let Unity derive the skeleton from this FBX's rest axes.
        humanoid.skeleton = Array.Empty<SkeletonBone>();
        importer.humanDescription = humanoid;
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.optimizeGameObjects = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var take = importer.defaultClipAnimations.Single();
        take.name = original.name;
        take.loopTime = original.isLooping;
        take.loopPose = false;
        take.lockRootRotation = take.lockRootHeightY = take.lockRootPositionXZ = true;
        take.keepOriginalOrientation = take.keepOriginalPositionY = take.keepOriginalPositionXZ = true;
        importer.clipAnimations = new[] { take };
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(clip => clip.name == original.name);
    }

    static void Compare(Matrix4x4[] reference, Matrix4x4[] actual, string[] joints, Check result)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            var a = reference[i];
            var b = actual[i];
            float distance = Vector3.Distance(a.GetPosition(), b.GetPosition());
            float angle = Quaternion.Angle(a.rotation, b.rotation);
            if (distance > result.maxBoneErrorMeters)
            {
                result.maxBoneErrorMeters = distance;
                result.worstPositionJoint = joints[i];
            }
            if (angle > result.maxBoneErrorDegrees)
            {
                result.maxBoneErrorDegrees = angle;
                result.worstRotationJoint = joints[i];
            }
            if (joints[i] == "LeftHand" || joints[i] == "RightHand")
                result.maxWristErrorMeters = Mathf.Max(result.maxWristErrorMeters, distance);
            for (int axis = 0; axis < 3; axis++)
                result.maxRelativeScaleError = Mathf.Max(result.maxRelativeScaleError,
                    Mathf.Abs(a.GetColumn(axis).magnitude - b.GetColumn(axis).magnitude));
            if (joints[i].EndsWith("Foot", StringComparison.Ordinal) || joints[i].EndsWith("Toes", StringComparison.Ordinal))
                result.maxFootErrorMeters = Mathf.Max(result.maxFootErrorMeters, distance);
            if (joints[i] == "Root") result.maxRootErrorMeters = Mathf.Max(result.maxRootErrorMeters, distance);
        }
    }

    static AnimationClip Original(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + name + ".anim");
    static string OutPath(string name) => Path.Combine(Environment.GetEnvironmentVariable("BLACKTOP_ANIMATION_OUTPUT") ??
        throw new InvalidOperationException("Set BLACKTOP_ANIMATION_OUTPUT to a disposable absolute directory."), name);

    static IEnumerable<float> MatrixValues(Matrix4x4 matrix)
    {
        // The existing Blender character bridge also uses column-major matrices.
        for (int index = 0; index < 16; index++) yield return matrix[index];
    }

    sealed class PreviewRig : IDisposable
    {
        readonly UnityEngine.SceneManagement.Scene scene;
        readonly SkinnedMeshRenderer skin;
        readonly Transform[] bones;
        public Animator Animator { get; }
        public string[] Names => bones.Select(bone => bone.name).ToArray();

        public PreviewRig()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), scene);
            var visual = model.GetComponent<PlayerVisualRig>();
            Animator = visual.animator;
            skin = visual.bodyRenderer;
            Animator.runtimeAnimatorController = null;
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var nodes = new HashSet<Transform>();
            foreach (var joint in skin.bones)
                for (var node = joint; node != Animator.transform; node = node.parent) nodes.Add(node);
            bones = nodes.OrderBy(node => AnimationUtility.CalculateTransformPath(node, Animator.transform), StringComparer.Ordinal).ToArray();
        }

        public Matrix4x4[] Matrices() => bones.Select(b => skin.transform.worldToLocalMatrix * b.localToWorldMatrix).ToArray();
        public Joint[] DescribeJoints() => bones.Zip(Matrices(), (bone, matrix) => new Joint {
            name = bone.name, parent = bones.Contains(bone.parent) ? bone.parent.name : "", rest = MatrixValues(matrix).ToArray()
        }).ToArray();

        public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
    }

    sealed class Sampler : IDisposable
    {
        readonly PlayableGraph graph;
        readonly AnimationClipPlayable action;

        public Sampler(Animator animator, AnimationClip clip, AnimationClip baseClip, AvatarMask mask)
        {
            graph = PlayableGraph.Create("Football animation sampling");
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            action = AnimationClipPlayable.Create(graph, clip);
            action.SetApplyFootIK(false);
            action.SetApplyPlayableIK(false);
            var baseMotion = AnimationClipPlayable.Create(graph, baseClip);
            baseMotion.SetApplyFootIK(false);
            baseMotion.SetSpeed(0);
            // Use the same base pose for each comparison so a leg or root leak remains visible.
            baseMotion.SetTime(baseClip.length * 0.3);
            var layer = AnimationLayerMixerPlayable.Create(graph, 2);
            layer.ConnectInput(0, baseMotion, 0, 1);
            layer.ConnectInput(1, action, 0, 1);
            layer.SetLayerMaskFromAvatarMask(1, mask);
            output.SetSourcePlayable(layer);
            action.SetSpeed(0);
            graph.Play();
        }

        public void Sample(double time)
        {
            action.SetTime(time);
            graph.Evaluate(0);
        }

        public void Dispose() => graph.Destroy();
    }
}

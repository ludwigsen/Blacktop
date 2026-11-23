using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class FootballActionAuthoring
{
    const string Folder = "Assets/_Project/Art/Animations";
    static Animator animator;
    static HumanPoseHandler handler;
    static HumanPose pose;
    static GameObject model;
    static float[] idle;
    static readonly string[] ArmTerms = {
        "Shoulder Down-Up", "Shoulder Front-Back", "Arm Down-Up", "Arm Front-Back",
        "Arm Twist In-Out", "Forearm Stretch", "Forearm Twist In-Out", "Hand Down-Up", "Hand In-Out"
    };

    struct Key
    {
        public float time, turn, lean, rightClose, leftClose;
        public Vector3 right, rightFingers, rightPalm, left, leftFingers, leftPalm;
    }

    static Key Pose(float time, Vector3 right, Vector3 rightFingers, Vector3 rightPalm, float rightClose,
        Vector3 left, Vector3 leftFingers, Vector3 leftPalm, float leftClose, float turn, float lean) =>
        new Key { time = time, right = right, rightFingers = rightFingers, rightPalm = rightPalm, rightClose = rightClose,
            left = left, leftFingers = leftFingers, leftPalm = leftPalm, leftClose = leftClose, turn = turn, lean = lean };

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static int Index(string name) => Array.IndexOf(HumanTrait.MuscleName, name);
    static Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

    [MenuItem("Blacktop Animations/Rebuild Standard Football Clips")]
    public static void Run()
    {
        model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerModel.prefab"));
        animator = model.GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = null;
        var idleClip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Idle.fbx").OfType<AnimationClip>()
            .First(clip => !clip.name.StartsWith("__preview__"));
        var graph = PlayableGraph.Create();
        var idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        AnimationPlayableOutput.Create(graph, "Source stance", animator).SetSourcePlayable(idlePlayable);
        graph.Play();
        graph.Evaluate(0);
        handler = new HumanPoseHandler(animator.avatar, animator.transform);
        handler.GetHumanPose(ref pose);
        idle = (float[])pose.muscles.Clone();
        graph.Destroy();
        // Targets use the canonical rig in meters and are baked into Humanoid muscles.
        var readyR = V(.26f, .46f, .32f);
        var readyL = V(-.12f, .40f, .40f);
        var readyF = V(-.77f, .19f, .58f);
        var readyP = V(-.32f, .88f, -.33f);
        var guideF = V(.81f,.21f,.50f);
        var guideP = V(.20f,.90f,-.27f);
        var tuckR = V(.27f, .44f, .35f);
        var tuckF = V(-.86f, .05f, .44f);
        var tuckP = V(-.25f, .26f, -.94f);
        var freeF = V(.08f,-.77f,.53f);
        var freeP = V(.86f,.08f,.22f);
        Key ready(float time, float bob) => Pose(time, readyR + V(0,bob,0), readyF, readyP, .54f,
            readyL + V(0,bob,0), guideF, guideP, .35f, 0f, .06f + bob);
        Key tuck(float time, float stride) => Pose(time, tuckR + V(0, .010f * stride, .012f * stride), tuckF, tuckP, .60f,
            V(-.30f, .08f + .08f * stride, .04f + .28f * stride), freeF, freeP, .20f, .10f * stride, .24f);
        BuildClip("Carry Ready", new[] { ready(0f,0f), ready(.62f,.021f), ready(1.24f,0f) }, true);
        BuildClip("Carry Tuck", new[] { tuck(0f,-1f), tuck(.16f,0f), tuck(.32f,1f), tuck(.48f,0f), tuck(.64f,-1f) }, true);

        // The throwing elbow loads beside the shoulder. The hand accelerates up
        // and ahead while the guide arm folds toward the ribs.
        var highR = V(.49f, .87f, -.16f);
        var highF = V(-.05f, .98f, -.12f);
        var highP = V(-.12f, .20f, .97f);
        var loadL = V(-.28f, .44f, .45f);
        var releaseR = V(.25f, .99f, .56f);
        var releaseF = V(-.45f, -.87f, .14f);
        var releaseP = V(.04f, .14f, .99f);
        float[] releasePose = BuildClip("Throw Windup", new[]
        {
            ready(0f,0f),
            Pose(.055f,V(.37f, .68f, .14f),V(-.29f,.87f,.40f),V(-.15f,.32f,.92f),.55f,
                V(-.19f,.37f,.37f),guideF,guideP,.30f,-.24f,-.08f),
            Pose(.095f, highR, highF, highP, .54f, loadL, V(-.06f, .58f, .79f), guideP, .20f, -.38f, -.12f),
            Pose(.127f, V(.39f, .99f, .19f), V(-.38f, .86f, .32f), V(.05f, .04f, .99f), .49f,
                V(-.26f, .32f, .27f), guideF, guideP, .18f, -.17f, .04f),
            Pose(.15f,releaseR,releaseF,releaseP,.38f, V(-.28f, .26f, .14f),freeF,freeP,.17f,.11f,.12f)
        }, false);
        BuildClip("Throw Follow Through", new[]
        {
            Pose(0f,releaseR,releaseF,releaseP,.38f, V(-.28f, .26f, .14f),freeF,freeP,.17f,.11f,.12f),
            Pose(.067f,V(.12f, .70f, .65f),V(-.42f,-.30f,.85f),V(-.14f,-.81f,.27f),.06f,
                V(-.29f,.20f,.26f),guideF,guideP,.16f,.38f,.30f),
            Pose(.16f,V(-.15f, .26f, .40f),V(-.57f,-.59f,.40f),V(.14f,-.6f,.55f),.17f,
                V(-.35f,.03f,.03f),freeF,freeP,.18f,.47f,.39f),
            Pose(.31f,V(.34f,-.12f,.10f),V(-.1f,-.92f,.36f),V(-.88f,.14f,.25f),.21f,
                V(-.34f,-.12f,.10f),freeF,freeP,.21f,.06f,.03f)
        }, false, releasePose);
        BuildHandoffs();
        AssetDatabase.SaveAssets();
        handler.Dispose();
        UnityEngine.Object.DestroyImmediate(model);
    }

    static void BuildHandoffs()
    {
        // Open the receiving pocket first, meet at 30 percent, then tuck.
        var carryR = V(.25f, .38f, .33f);
        var carryFingers = V(-.85f, .05f, .40f);
        var carryPalm = V(-.27f, .26f, -.92f);
        var guideL = V(-.24f,.13f,.27f);
        var guideFingers = V(.74f,.06f,.30f);
        var guidePalm = V(.1f,.84f,-.42f);
        var extendedR = V(.69f, .24f, .10f);
        var extendedRFingers = V(.63f, .04f, .77f);
        var extendedRPalm = V(-.13f,.91f,-.18f);
        var actionL = V(-.08f,.17f,.30f);
        var actionLFingers = V(.7f,.1f,.21f);
        var actionLPalm = V(.05f,-.15f,.9f);
        var relaxedR = V(.32f,-.13f,.07f);
        var relaxedL = V(-.32f,-.13f,.07f);
        var relaxRFingers = V(-.16f,-.9f,.36f);
        var relaxLFingers = V(.16f,-.9f,.36f);
        var relaxRPalm = V(-.88f,.14f,.25f);
        var relaxLPalm = V(.88f,.14f,.25f);

        BuildClip("Handoff Give", new[]
        {
            Pose(0f, carryR, carryFingers, carryPalm, .54f, guideL, guideFingers, guidePalm, .33f, 0f, 0f),
            Pose(.065f, V(.50f,.18f,.13f), V(.65f,.2f,.54f), extendedRPalm, .55f,
                actionL, actionLFingers, actionLPalm, .23f, -.12f, .08f),
            Pose(.12f, extendedR, extendedRFingers, extendedRPalm, .30f,
                V(-.10f,.17f,.35f), actionLFingers, actionLPalm, .12f, -.30f, .13f),
            Pose(.21f, V(.70f,.13f,.15f), V(.75f,.17f,.38f), V(-.02f,.85f,.24f), .03f,
                V(-.13f,.10f,.32f), V(.41f,-.2f,.7f), V(.14f,.75f,.24f), .11f, -.19f, .15f),
            Pose(.31f, V(.43f,.03f,.21f), V(.43f,-.37f,.54f), V(-.52f,.41f,.35f), .13f,
                V(-.23f,-.04f,.19f), V(.27f,-.53f,.57f), V(.7f,.31f,.37f), .17f, -.06f, .04f),
            Pose(.40f, relaxedR, relaxRFingers, relaxRPalm, .22f, relaxedL, relaxLFingers, relaxLPalm, .22f, 0f, 0f)
        }, false);

        var takeR = V(.21f, .27f, .58f);
        var takeL = V(-.19f, .19f, .55f);
        var takeRFingers = V(-.12f,.24f,.9f);
        var takeLFingers = V(.27f,.31f,.88f);
        var takeRPalm = V(-.9f,.33f,-.13f);
        var takeLPalm = V(.65f,.61f,-.13f);
        BuildClip("Handoff Take", new[]
        {
            Pose(0f, V(.27f,.10f,.40f), V(-.16f,.3f,.9f), takeRPalm, .07f,
                V(-.25f, .17f, .39f), takeLFingers, takeLPalm, .07f, .02f, .12f),
            Pose(.075f, takeR, takeRFingers, takeRPalm, .07f, takeL, takeLFingers, takeLPalm, .08f, -.02f, .21f),
            Pose(.12f, V(.22f,.27f,.58f), V(-.28f,.18f,.85f), takeRPalm, .47f,
                V(-.15f,.14f,.56f), V(.48f,.19f,.77f), takeLPalm, .34f, -.02f, .24f),
            Pose(.22f, V(.28f,.21f,.38f), V(-.65f,.08f,.47f), V(-.52f,.69f,-.28f), .55f,
                V(-.11f,.14f,.41f), V(.63f,.08f,.59f), V(.24f,.8f,-.22f), .37f, -.11f, .17f),
            Pose(.31f, V(.31f,.19f,.22f), carryFingers, carryPalm, .57f,
                V(-.18f,.10f,.27f), guideFingers, guidePalm, .29f, -.12f, .09f),
            Pose(.40f, carryR, carryFingers, carryPalm, .56f,
                relaxedL, relaxLFingers, relaxLPalm, .22f, -.06f, .07f)
        }, false);
    }

    static float[] BuildClip(string name, Key[] keys, bool loop, float[] openingPose = null)
    {
        Array.Copy(idle, pose.muscles, idle.Length);
        var channels = Enumerable.Range(0, HumanTrait.MuscleCount).Where(index =>
        {
            string muscle = HumanTrait.MuscleName[index];
            return muscle.StartsWith("Spine") || muscle.StartsWith("Chest") || muscle.StartsWith("UpperChest") ||
                muscle.StartsWith("Neck") || muscle.StartsWith("Head") ||
                muscle.Contains("Shoulder") || muscle.Contains("Arm") || muscle.Contains("Forearm") ||
                muscle.Contains("Hand") || muscle.Contains(" Stretched") || muscle.Contains(" Spread");
        }).ToDictionary(index => index, _ => new Keyframe[keys.Length]);
        for (int frame = 0; frame < keys.Length; frame++)
        {
            var key = keys[frame];
            if (frame == 0 && openingPose != null)
            {
                // Match every release channel across the asset boundary. Each
                // independent arm solve can otherwise choose a different elbow bend.
                Array.Copy(openingPose, pose.muscles, openingPose.Length);
                foreach (var entry in channels)
                    entry.Value[frame] = new Keyframe(key.time, pose.muscles[entry.Key]);
                continue;
            }
            if (loop && frame == keys.Length - 1)
            {
                // Keep the same solved pose at both ends of the gait loop.
                foreach (var entry in channels)
                    entry.Value[frame] = new Keyframe(key.time, entry.Value[0].value);
                continue;
            }
            Set("Spine Front-Back", key.lean * .38f);
            Set("Chest Front-Back", key.lean * .62f);
            Set("Spine Twist Left-Right", key.turn * .38f);
            Set("Chest Twist Left-Right", key.turn * .62f);
            Set("Neck Turn Left-Right", key.turn * .25f);
            Set("Head Turn Left-Right", key.turn * .4f);
            Set("Head Nod Down-Up", key.lean * .18f);
            SolveHand(true, key.right, key.rightFingers, key.rightPalm);
            SolveHand(false, key.left, key.leftFingers, key.leftPalm);
            SetFingers(true, key.rightClose);
            SetFingers(false, key.leftClose);
            handler.SetHumanPose(ref pose);
            foreach (var entry in channels)
                entry.Value[frame] = new Keyframe(key.time, pose.muscles[entry.Key]);
        }

        var clip = new AnimationClip { frameRate = 60f, name = name };
        foreach (var entry in channels)
        {
            var curve = new AnimationCurve(entry.Value);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            string nameOfMuscle = HumanTrait.MuscleName[entry.Key];
            string binding = nameOfMuscle;
            if (binding.EndsWith("Stretched") || binding.EndsWith("Spread"))
            {
                var words = binding.Split(' ');
                string channel = words.Length == 4 ? words[2] + " " + words[3] : words[2];
                binding = $"{words[0]}Hand.{words[1]}.{channel}";
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), binding), curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        string path = $"{Folder}/{name}.anim";
        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (old == null) AssetDatabase.CreateAsset(clip, path);
        else
        {
            // Replace the curves in the existing asset so all references keep their GUID.
            EditorUtility.CopySerialized(clip, old);
            EditorUtility.SetDirty(old);
            UnityEngine.Object.DestroyImmediate(clip);
            clip = old;
        }
        Debug.Log($"CLIP {name} length {clip.length:F3} human={clip.humanMotion} curves={AnimationUtility.GetCurveBindings(clip).Length}");
        return (float[])pose.muscles.Clone();
    }



    static void Set(string name, float value)
    {
        int index = Index(name);
        if (index >= 0) pose.muscles[index] = value;
    }

    static void SetFingers(bool right, float close)
    {
        string hand = right ? "Right " : "Left ";
        foreach (string finger in new[] { "Index", "Middle", "Ring", "Little" })
        {
            Set($"{hand}{finger} 1 Stretched", Mathf.Lerp(.32f,-.78f,close));
            Set($"{hand}{finger} 2 Stretched", Mathf.Lerp(.31f,-.80f,close));
            Set($"{hand}{finger} 3 Stretched", Mathf.Lerp(.44f,-.54f,close));
            Set($"{hand}{finger} Spread", Mathf.Lerp(.14f,-.10f,close));
        }
        Set($"{hand}Thumb 1 Stretched", Mathf.Lerp(.15f,-.18f,close));
        Set($"{hand}Thumb 2 Stretched", Mathf.Lerp(.25f,-.43f,close));
        Set($"{hand}Thumb 3 Stretched", Mathf.Lerp(.32f,-.27f,close));
        Set($"{hand}Thumb Spread", Mathf.Lerp(.19f,-.34f,close));
    }

    static void SolveHand(bool right, Vector3 target, Vector3 desiredFingers, Vector3 desiredPalm)
    {
        string side = right ? "Right " : "Left ";
        int[] indices = ArmTerms.Select(term => Index(side + term)).ToArray();
        if (indices.Any(i => i < 0)) throw new InvalidOperationException("Arm muscle name missing: " + side);
        var wrist = Bone(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var indexFinger = Bone(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        var middle = Bone(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        var little = Bone(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        var elbow = Bone(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        var shoulder = Bone(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        var hips = Bone(HumanBodyBones.Hips);
        // A HumanPose is normalized to the avatar. Reference its pelvis so the
        // optimizer stays independent of the imported model-root fitting offsets.
        var desiredFromHips = target - new Vector3(0f, .13f, -.196f);
        desiredFingers.Normalize();
        desiredPalm = Vector3.ProjectOnPlane(desiredPalm, desiredFingers).normalized;
        float Score()
        {
            handler.SetHumanPose(ref pose);
            var fingers = (middle.position - wrist.position).normalized;
            var thumbSide = (indexFinger.position - little.position).normalized;
            var palm = right ? Vector3.Cross(fingers, thumbSide).normalized : Vector3.Cross(thumbSide, fingers).normalized;
            float elbowTooHigh = Mathf.Max(0, elbow.position.y - (wrist.position.y + .05f));
            float elbowTooInside = right ? Mathf.Max(0, .18f - elbow.position.x) : Mathf.Max(0, .18f + elbow.position.x);
            float shoulderElevation = shoulder.position.y - hips.position.y - .48f;
            return (wrist.position - hips.position - desiredFromHips).sqrMagnitude * 30f
                + (1f - Vector3.Dot(fingers, desiredFingers)) * .17f
                + (1f - Vector3.Dot(palm, desiredPalm)) * .10f
                + elbowTooHigh * elbowTooHigh * 1.5f + elbowTooInside * elbowTooInside * 1.2f
                + shoulderElevation * shoulderElevation * .3f
                + (pose.muscles[indices[0]] * pose.muscles[indices[0]]
                + pose.muscles[indices[1]] * pose.muscles[indices[1]]) * .008f;
        }
        var bestSolution = (float[])pose.muscles.Clone();
        float best = Score();
        var random = new System.Random(72);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (attempt > 0)
                foreach (int index in indices)
                    pose.muscles[index] = Mathf.Clamp(bestSolution[index] + (float)(random.NextDouble() - .5) * .9f, -.99f, .99f);
            float localBest = Score();
            for (float step = .40f; step >= .006f; step *= .62f)
            {
                for (int sweep = 0; sweep < 8; sweep++)
                {
                    bool changed = false;
                    foreach (int index in indices)
                    {
                        float original = pose.muscles[index], bestValue = original;
                        for (int direction = -1; direction <= 1; direction += 2)
                        {
                            pose.muscles[index] = Mathf.Clamp(original + step * direction, -.99f, .99f);
                            float candidate = Score();
                            if (candidate >= localBest - .000001f) continue;
                            bestValue = pose.muscles[index];
                            localBest = candidate;
                            changed = true;
                        }
                        pose.muscles[index] = bestValue;
                    }
                    if (!changed) break;
                }
            }
            if (localBest < best)
            {
                best = localBest;
                Array.Copy(pose.muscles, bestSolution, idle.Length);
            }
        }
        Array.Copy(bestSolution, pose.muscles, idle.Length);
        handler.SetHumanPose(ref pose);
    }
}

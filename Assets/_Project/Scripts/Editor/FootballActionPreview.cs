using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

// Fixture paths make the same visual poses easy to compare without starting a match.
public enum FootballPreviewAction { StandardThrow, CarryIdle, CarryMoving, Handoff, HardCutLeft, HardCutRight, HardCutWithBall }

public sealed class FootballActionPreview : EditorWindow
{
    static readonly string[] ActionNames = { "Standard Throw", "Ball Carry (Idle)", "Ball Carry (Moving)", "Handoff",
        "Hard Cut Left", "Hard Cut Right", "Hard Cut with Football" };
    static readonly string[] BuildNames = new[] { "Prefab default (Athletic)" }.Concat(Enum.GetNames(typeof(BodyArchetype))).ToArray();
    static readonly string[] GaitNames = { "Walk", "Jog", "Run", "Fast run", "Sprint" };
    static readonly string[] GaitClips = { "Walking", "Jog Forward", "Running", "Running-Faster", "Running-Sprint" };
    static readonly float[] GaitSpeeds = { 2f, 3.5f, 5.5f, 7.5f, 10f };
    static readonly float[] Speeds = { 1f, .5f, .25f };

    [SerializeField] FootballPreviewAction action;
    [SerializeField] int build = 2;
    [SerializeField] int runnerBuild = 2;
    [SerializeField] PlayerHeight height = PlayerHeight.Ft6In0;
    [SerializeField] PlayerHeight runnerHeight = PlayerHeight.Ft6In0;
    [SerializeField] int gait = 2;
    [SerializeField] int playbackRate;
    [SerializeField] bool movingThrow;
    [SerializeField] bool carryTurn;
    [SerializeField] bool ballCutRight;
    [SerializeField] float meshDistance = 1.5f;
    [SerializeField] bool grid = true;
    [SerializeField] bool markers = true;
    [SerializeField] float yaw = 35f;
    [SerializeField] bool closeView;
    [SerializeField] float time;
    bool playing = true;
    double lastTick;
    string failure;
    PreviewStage stage;

    [MenuItem("Blacktop Animations/Standard Throw")]
    static void OpenThrow() => Open(FootballPreviewAction.StandardThrow);
    [MenuItem("Blacktop Animations/Ball Carry - Idle")]
    static void OpenCarryIdle() => Open(FootballPreviewAction.CarryIdle);
    [MenuItem("Blacktop Animations/Ball Carry - Moving")]
    static void OpenCarryMoving() => Open(FootballPreviewAction.CarryMoving);
    [MenuItem("Blacktop Animations/Handoff")]
    static void OpenHandoff() => Open(FootballPreviewAction.Handoff);
    [MenuItem("Blacktop Animations/Hard Cut Left")]
    static void OpenCutLeft() => Open(FootballPreviewAction.HardCutLeft);
    [MenuItem("Blacktop Animations/Hard Cut Right")]
    static void OpenCutRight() => Open(FootballPreviewAction.HardCutRight);
    [MenuItem("Blacktop Animations/Hard Cut with Football")]
    static void OpenBallCut() => Open(FootballPreviewAction.HardCutWithBall);

    static void Open(FootballPreviewAction next)
    {
        var window = GetWindow<FootballActionPreview>("Football Animations");
        window.minSize = new Vector2(425f, 510f);
        window.action = next;
        window.time = 0f;
        window.playing = true;
        window.Repaint();
    }

    void OnEnable() => lastTick = EditorApplication.timeSinceStartup;

    void Update()
    {
        double now = EditorApplication.timeSinceStartup;
        if (playing && failure == null)
        {
            time = Mathf.Repeat(time + Mathf.Min(.05f, (float)(now - lastTick)) * Speeds[playbackRate], Duration(action));
            Repaint();
        }
        lastTick = now;
    }

    void OnDisable() => ClearStage();

    void ClearStage()
    {
        stage?.Dispose();
        stage = null;
        failure = null;
    }

    void OnGUI()
    {
        var nextAction = (FootballPreviewAction)EditorGUILayout.Popup("Animation", (int)action, ActionNames);
        if (nextAction != action) { action = nextAction; time = 0f; }

        using (var appearance = new EditorGUI.ChangeCheckScope())
        {
            build = EditorGUILayout.Popup(action == FootballPreviewAction.Handoff ? "Quarterback build" : "Build", build, BuildNames);
            height = (PlayerHeight)EditorGUILayout.EnumPopup("Height", height);
            if (action == FootballPreviewAction.Handoff)
            {
                runnerBuild = EditorGUILayout.Popup("Runner build", runnerBuild, BuildNames);
                runnerHeight = (PlayerHeight)EditorGUILayout.EnumPopup("Runner height", runnerHeight);
            }
            if (appearance.changed) ClearStage();
        }

        if (action == FootballPreviewAction.CarryMoving)
        {
            gait = EditorGUILayout.Popup("Gait", gait, GaitNames);
            carryTurn = EditorGUILayout.Toggle("Accelerate, turn, brake", carryTurn);
        }
        if (action == FootballPreviewAction.StandardThrow)
            movingThrow = EditorGUILayout.Toggle("Moving throw", movingThrow);
        if (action == FootballPreviewAction.Handoff)
            meshDistance = EditorGUILayout.Slider("Mesh point separation", meshDistance, .8f, 2f);
        if (action == FootballPreviewAction.HardCutWithBall)
            ballCutRight = EditorGUILayout.Popup("Cut direction", ballCutRight ? 1 : 0, new[] { "Left", "Right" }) == 1;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(64f))) playing = !playing;
            if (GUILayout.Button("Restart", GUILayout.Width(64f))) time = 0f;
            playbackRate = EditorGUILayout.Popup(playbackRate, new[] { "1×", "½×", "¼×" });
        }
        using (var scrub = new EditorGUI.ChangeCheckScope())
        {
            time = EditorGUILayout.Slider("Time (seconds)", time, 0f, Duration(action));
            if (scrub.changed) playing = false;
        }
        yaw = EditorGUILayout.Slider("View angle", yaw, -180f, 180f);
        using (new EditorGUILayout.HorizontalScope())
        {
            grid = GUILayout.Toggle(grid, "Ground grid");
            markers = GUILayout.Toggle(markers, "Foot and ball markers");
            closeView = GUILayout.Toggle(closeView, "Close view");
        }
        if (markers) EditorGUILayout.LabelField("Cyan/orange  left/right foot     Green  release, exchange, or plant", EditorStyles.miniLabel);

        if (failure != null)
        {
            EditorGUILayout.HelpBox(failure, MessageType.Error);
            if (GUILayout.Button("Reload preview assets")) ClearStage();
        }
        Rect rect = GUILayoutUtility.GetRect(250f, 250f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint || failure != null || rect.width <= 0f || rect.height <= 0f) return;
        try
        {
            stage ??= new PreviewStage(build, height, runnerBuild, runnerHeight);
            stage.Sample(action, time, gait, movingThrow, ballCutRight, carryTurn, meshDistance);
            stage.Frame(action, yaw, grid, markers, closeView);
            GUI.DrawTexture(rect, stage.Render(rect.width, rect.height), ScaleMode.StretchToFill, false);
        }
        catch (Exception error)
        {
            ClearStage();
            failure = error.Message;
            Debug.LogException(error);
            Repaint();
        }
    }

    // Loop lengths include entry and exit locomotion around the short action.
    static float Duration(FootballPreviewAction clip) => clip switch
    {
        FootballPreviewAction.StandardThrow => 1.35f,
        FootballPreviewAction.Handoff => 1.32f,
        FootballPreviewAction.CarryIdle => 1.24f,
        _ => 1.20f
    };

    // Reuse the menu sampler for reproducible visual checks on the canonical rig and each catalog body.
    public static void CaptureValidation()
    {
        const int width = 420, height = 300, columns = 6;
        const string directory = "Builds/Validation/StandardFootball";
        var actions = (FootballPreviewAction[])Enum.GetValues(typeof(FootballPreviewAction));
        Directory.CreateDirectory(directory);
        string[] failures = new string[BuildNames.Length];
        for (int buildIndex = 0; buildIndex < BuildNames.Length; buildIndex++)
        {
            string body = BuildNames[buildIndex];
            Texture2D atlas = null;
            try
            {
                var runnerHeight = (buildIndex % 2 == 0) ? PlayerHeight.Ft5In7 : PlayerHeight.Ft6In7;
                using var stage = new PreviewStage(buildIndex, PlayerHeight.Ft6In0, buildIndex, runnerHeight);
                if (buildIndex == 0) Debug.Log(stage.DescribeFootballMesh());
                atlas = new Texture2D(width * columns, height * actions.Length, TextureFormat.RGB24, false);
                for (int row = 0; row < actions.Length; row++)
                for (int column = 0; column < columns; column++)
                {
                    var action = actions[row];
                    stage.Sample(action, ReviewTime(action, column), 2, false, buildIndex % 2 != 0);
                    stage.Frame(action, action == FootballPreviewAction.Handoff ? 12f : 40f, true, true);
                    stage.Capture(atlas, width, height, column * width, (actions.Length - 1 - row) * height);
                }
                stage.Sample(FootballPreviewAction.Handoff, .61f, 2, false, false);
                float shotgunGap = stage.HandoffGripGap;
                stage.Sample(FootballPreviewAction.Handoff, .61f, 2, false, false, meshDistance: 2f);
                float outerGap = stage.HandoffGripGap;
                atlas.Apply();
                File.WriteAllBytes(Path.Combine(directory, body + ".png"), atlas.EncodeToPNG());
                Debug.Log($"[Football animation preview] Captured {body}: {actions.Length} scenarios, {columns} samples per scenario, runner {runnerHeight}. " +
                    $"Contact grip gap at fixture separations 1.5 m / 2 m: {shotgunGap:F3} m / {outerGap:F3} m.");
            }
            catch (Exception error)
            {
                failures[buildIndex] = body + ": " + error.Message;
                Debug.LogException(error);
            }
            finally
            {
                if (atlas != null) DestroyImmediate(atlas);
            }
        }
        var missing = failures.Where(error => error != null).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException(string.Join("\n", missing));
    }

    // Export frames for real-time review from the same sampler and actual body/ball meshes.
    public static void CaptureMotion()
    {
        const int frameRate = 30, width = 720, height = 520;
        const string directory = "Builds/Validation/StandardFootball/Motion";
        using var stage = new PreviewStage(2, PlayerHeight.Ft6In0, 5, PlayerHeight.Ft6In3);
        var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            foreach (var clip in (FootballPreviewAction[])Enum.GetValues(typeof(FootballPreviewAction)))
                Sequence(clip, clip.ToString(), clip == FootballPreviewAction.Handoff ? 15f : 40f, false, false);
            Sequence(FootballPreviewAction.StandardThrow, "ThrowMoving", 70f, true, false);
            Sequence(FootballPreviewAction.StandardThrow, "ThrowCloseSide", 75f, false, false, true);
            Sequence(FootballPreviewAction.CarryMoving, "CarryWalk", 45f, false, false, gait: 0);
            Sequence(FootballPreviewAction.CarryMoving, "CarryJog", 45f, false, false, gait: 1);
            Sequence(FootballPreviewAction.CarryMoving, "CarryFastRun", 45f, false, false, gait: 3);
            Sequence(FootballPreviewAction.CarryMoving, "CarrySprint", 45f, false, false, gait: 4);
            Sequence(FootballPreviewAction.CarryMoving, "CarryTurning", 65f, false, true);
            Sequence(FootballPreviewAction.CarryIdle, "CarryIdleClose", 0f, false, false, true);
            Sequence(FootballPreviewAction.CarryMoving, "CarryRunClose", 75f, false, false, true);
            Sequence(FootballPreviewAction.Handoff, "HandoffClose", 18f, false, false, true);
            Sequence(FootballPreviewAction.Handoff, "HandoffNearMesh", 18f, false, false, true, .95f);
        }
        finally { DestroyImmediate(frame); }

        void Sequence(FootballPreviewAction clip, string name, float angle, bool movingThrow, bool turn, bool close = false, float meshDistance = 1.5f, int gait = 2)
        {
            string folder = Path.Combine(directory, name);
            Directory.CreateDirectory(folder);
            for (int i = 0; i < Mathf.CeilToInt(Duration(clip) * frameRate); i++)
            {
                stage.Sample(clip, i / (float)frameRate, gait, movingThrow, true, turn, meshDistance);
                stage.Frame(clip, angle, !close, !close, close);
                stage.Capture(frame, width, height, 0, 0);
                frame.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"{i:D3}.png"), frame.EncodeToPNG());
            }
            Debug.Log($"[Football animation preview] Captured {name} at {frameRate} fps.");
        }
    }

    static float ReviewTime(FootballPreviewAction action, int column)
    {
        // Contact and release get samples on both sides of the event.
        return action switch
        {
            FootballPreviewAction.StandardThrow => new[] { .22f, .445f, .48f, .50f, .64f, .92f }[column],
            FootballPreviewAction.Handoff => new[] { .27f, .49f, .60f, .62f, .73f, 1.02f }[column],
            FootballPreviewAction.CarryIdle or FootballPreviewAction.CarryMoving => .17f + column * .16f,
            _ => new[] { .26f, .40f, .47f, .52f, .69f, .92f }[column]
        };
    }

    sealed class PreviewStage : IDisposable
    {
        const string Art = "Assets/_Project/Art/Animations/";
        const string Prefabs = "Assets/_Project/Prefabs/";
        const float ThrowStart = .34f, ThrowRelease = .49f, ThrowEnd = .80f;
        const float Exchange = .61f, ReachStart = .41f, HandoffEnd = 1.01f;
        const float CutStart = .40f, CutEnd = .72f;
        static readonly Vector3 Grounded = Vector3.up;
        static readonly string[] Clips = new[] { "Idle", "Carry Ready", "Carry Tuck", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take" }
            .Concat(GaitClips).ToArray();
        static readonly string[] ActionClips = { "Carry Ready", "Carry Tuck", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take" };

        readonly PreviewRenderUtility preview = new();
        readonly StandardHardCut hardCut = new();
        readonly Material[] materials = new Material[4];
        readonly GameObject[] marks = new GameObject[5];
        Rig actor, runner;
        GameObject football, lines;
        Mesh lineMesh;
        Vector3 reference;
        bool hasReference;
        public float HandoffGripGap { get; private set; }

        public PreviewStage(int build, PlayerHeight height, int runnerBuild, PlayerHeight runnerHeight)
        {
            try
            {
                var settings = Require<FrontEndSettings>(FrontEndSetup.SettingsPath);
                var mask = Require<AvatarMask>(Art + "Football Upper Body.mask");
                var clips = Clips.ToDictionary(clip => clip, LoadClip);
                actor = Spawn(settings, build, height, 0, clips, mask);
                runner = Spawn(settings, runnerBuild, runnerHeight, 1, clips, mask);
                hardCut.Bind(actor.Animator, actor.Root.transform);
                football = Object.Instantiate(Require<GameObject>(Prefabs + "FootballVisual.prefab"));
                preview.AddSingleGO(football);
                var style = football.GetComponent<FootballAppearance>();
                if (style != null) style.ApplyStyle(style.SelectedStyle);

                preview.ambientColor = new Color(.55f, .59f, .62f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.30f, .37f, .43f);
                preview.camera.fieldOfView = 35f;
                preview.camera.nearClipPlane = .05f;
                preview.camera.farClipPlane = 40f;
                preview.lights[0].intensity = 2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(38f, 32f, 0f);
                preview.lights[1].intensity = 1.1f;
                BuildMarkers();
                Grip(actor.Animator); // Report a bad canonical grip before playing the fixture.
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        Rig Spawn(FrontEndSettings settings, int build, PlayerHeight height, int teamIndex,
            System.Collections.Generic.Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            var root = new GameObject(teamIndex == 0 ? "Quarterback / carrier" : "Receiving back");
            preview.AddSingleGO(root);
            try
            {
                var model = Object.Instantiate(settings.visualPrefab, root.transform, false);
                var adapter = model.GetComponent<PlayerVisualRig>();
                if (adapter == null) throw new InvalidOperationException("PlayerModel is missing its visual rig adapter.");
                if (!adapter.ValidateRig(out string error)) throw new InvalidOperationException(error);
                var catalog = settings.appearances;
                var heightProfile = catalog.heightProfiles.FirstOrDefault(p => p != null && p.height == height);
                if (heightProfile == null) throw new InvalidOperationException($"Missing height fixture {height}.");
                if (build == 0) adapter.ApplyHeight(heightProfile, 1f);
                else
                {
                    var archetype = (BodyArchetype)(build - 1);
                    var appearance = Object.Instantiate(settings.defaultAppearance);
                    try
                    {
                        appearance.bodyArchetype = archetype;
                        appearance.heightProfile = heightProfile;
                        appearance.hairId = "hair.shaved";
                        var bodyPart = catalog.Find(archetype.ToString().ToUpperInvariant());
                        appearance.bodyId = bodyPart.id;
                        appearance.clothingIds = Array.Empty<string>();
                        var assembler = root.AddComponent<PlayerVisualAssembler>();
                        assembler.enabled = false;
                        assembler.rig = adapter;
                        assembler.catalog = catalog;
                        var team = settings.teams.Length > teamIndex ? settings.teams[teamIndex].identity : null;
                        if (!assembler.TryApply(appearance, team, teamIndex + 12))
                            throw new InvalidOperationException($"{archetype}: {assembler.LastDiagnostics}");
                        if (adapter.bodyRenderer.sharedMesh != bodyPart.skeleton.referenceSkin.sharedMesh)
                            throw new InvalidOperationException($"{archetype}: preview skin does not match the fitted prefab.");
                    }
                    finally { Object.DestroyImmediate(appearance); }
                }
                var bodyBuild = build == 0 ? BodyArchetype.Athletic : (BodyArchetype)(build - 1);
                return new Rig(root, adapter.animator, bodyBuild, clips, mask);
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException($"Preview asset missing at {path}");

        static AnimationClip LoadClip(string clip)
        {
            if (ActionClips.Contains(clip)) return Require<AnimationClip>($"{Art}{clip}.anim");
            return AssetDatabase.LoadAllAssetsAtPath($"{Art}{clip}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(asset => asset.name == clip) ?? throw new InvalidOperationException($"Missing locomotion clip {clip}.");
        }

        static Pose Grip(Animator animator)
        {
            if (PlayerAnimationPresenter.TrySampleFootballGrip(animator, PlayerAnimationPresenter.DefaultFootballGripOffset, out Pose pose))
                return pose;
            throw new InvalidOperationException("The Humanoid rig could not resolve the right-hand football socket.");
        }

        public string DescribeFootballMesh()
        {
            // Inspect the imported renderer and all its nested prefab transforms before grip rotation.
            var renderer = football.GetComponentInChildren<MeshRenderer>();
            var local = renderer.localBounds;
            Vector3 dimensions = local.size;
            Vector3 localAxis = dimensions.x > dimensions.y && dimensions.x > dimensions.z ? Vector3.right
                : dimensions.y > dimensions.z ? Vector3.up : Vector3.forward;
            Vector3 axis = football.transform.InverseTransformDirection(renderer.transform.TransformDirection(localAxis));
            Vector3 center = football.transform.InverseTransformPoint(renderer.transform.TransformPoint(local.center));
            return $"[Football animation preview] FootballVisual mesh wrapper axis={axis:F3} center={center:F3} local dimensions={dimensions:F3}.";
        }

        public void Sample(FootballPreviewAction action, float time, int gait, bool movingThrow, bool rightBallCut, bool turning = false, float meshDistance = 1.5f)
        {
            runner.Root.SetActive(action == FootballPreviewAction.Handoff);
            actor.Place(Grounded, Vector3.forward);
            hasReference = false;
            football.SetActive(action != FootballPreviewAction.HardCutLeft && action != FootballPreviewAction.HardCutRight);
            Pose ballPose = default;
            switch (action)
            {
                case FootballPreviewAction.StandardThrow:
                    ballPose = SampleThrow(time, movingThrow);
                    break;
                case FootballPreviewAction.Handoff:
                    ballPose = SampleExchange(time, meshDistance);
                    break;
                case FootballPreviewAction.HardCutLeft:
                case FootballPreviewAction.HardCutRight:
                case FootballPreviewAction.HardCutWithBall:
                    bool carrying = action == FootballPreviewAction.HardCutWithBall;
                    SampleCut(time, action == FootballPreviewAction.HardCutRight || (carrying && rightBallCut), carrying);
                    if (carrying)
                    {
                        ApplyCarry(actor, 1f);
                        ballPose = Grip(actor.Animator);
                    }
                    break;
                default:
                    ballPose = SampleCarry(time, gait, action == FootballPreviewAction.CarryMoving, turning);
                    break;
            }
            if (football.activeSelf) football.transform.SetPositionAndRotation(ballPose.position, ballPose.rotation);
        }

        Pose SampleCarry(float time, int gait, bool moving, bool turning)
        {
            float speed = moving ? GaitSpeeds[gait] : 0f;
            float travel = (time - .60f) * speed;
            float gaitWeight = moving ? 1f : 0f;
            Vector3 direction = Vector3.forward;
            Vector3 position = Grounded + direction * travel;
            if (moving && turning)
            {
                // Smooth distance and tangential heading on a small arc. Only the fixture root moves.
                const float duration = 1.20f;
                float phase = Mathf.Clamp01(time / duration);
                float distance = speed * duration * (phase * phase * (3f - 2f * phase) - .5f) / 1.5f;
                const float radius = 2.4f;
                float angle = distance / radius;
                direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                position = Grounded + new Vector3(radius * (1f - Mathf.Cos(angle)), 0f, radius * Mathf.Sin(angle));
                gaitWeight = 4f * phase * (1f - phase);
            }
            actor.Place(position, direction);
            string movement = moving ? GaitClips[gait] : "Idle";
            float pace = Mathf.Clamp01(gaitWeight * speed / 3f);
            actor.Sample(movement, time, "Carry Ready", actor.BasePhase("Carry Ready", time),
                "Carry Tuck", actor.BasePhase(movement, time), pace, gaitWeight: gaitWeight);
            ApplyCarry(actor, pace);
            return Grip(actor.Animator);
        }

        static void ApplyCarry(Rig rig, float pace, float weight = 1f) =>
            FootballActionPose.ApplyCarry(rig.Animator, pace, PlayerAnimationPresenter.DefaultFootballGripOffset, rig.Build, weight);

        Pose SampleThrow(float time, bool moving)
        {
            string gait = moving ? "Jog Forward" : "Idle";
            void Place(float t) => actor.Place(Grounded + Vector3.forward * (moving ? (t - ThrowRelease) * 2.4f : 0f), Vector3.forward);
            if (time < ThrowRelease)
            {
                Place(time);
                float windup = Mathf.InverseLerp(ThrowStart, ThrowRelease, time);
                float throwing = Mathf.SmoothStep(0f, 1f, windup * 4f);
                actor.Sample(gait, time, moving ? "Carry Tuck" : "Carry Ready", actor.BasePhase(gait, time),
                    "Throw Windup", windup, throwing);
                ApplyCarry(actor, moving ? 1f : 0f, 1f - throwing);
                if (!moving) FootballActionPose.ApplyThrowStance(actor.Animator, actor.Root.transform, windup, -1f);
                FootballActionPose.ApplyOverhandArm(actor.Animator, actor.Root.transform, windup, -1f,
                    PlayerAnimationPresenter.DefaultFootballGripOffset, throwing);
                return Grip(actor.Animator);
            }

            // The launch marker is the authored release socket. The fixture flight starts there once.
            Place(ThrowRelease);
            actor.Sample(gait, ThrowRelease, "Throw Windup", 1f);
            if (!moving) FootballActionPose.ApplyThrowStance(actor.Animator, actor.Root.transform, 1f, -1f);
            FootballActionPose.ApplyOverhandArm(actor.Animator, actor.Root.transform, 1f, -1f,
                PlayerAnimationPresenter.DefaultFootballGripOffset);
            Pose release = Grip(actor.Animator);
            reference = release.position;
            hasReference = true;
            Place(time);
            float progress = Mathf.InverseLerp(ThrowRelease, ThrowEnd, time);
            actor.Sample(gait, time, "Throw Follow Through", progress, null, 0f, 0f,
                1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.76f, 1f, progress)));
            if (!moving) FootballActionPose.ApplyThrowStance(actor.Animator, actor.Root.transform, -1f, progress);
            FootballActionPose.ApplyOverhandArm(actor.Animator, actor.Root.transform, -1f, progress,
                PlayerAnimationPresenter.DefaultFootballGripOffset);
            float elapsed = time - ThrowRelease;
            var position = reference + new Vector3(0f, 1.5f * elapsed - 2.4f * elapsed * elapsed, 7.5f * elapsed);
            return new Pose(position, Quaternion.AngleAxis(elapsed * 800f, release.rotation * Vector3.forward) * release.rotation);
        }

        Pose SampleExchange(float time, float meshDistance)
        {
            const float contact = PlayerAnimationPresenter.HandoffContactProgress;
            Vector3 direction = new(-.72f, 0f, .69f);
            var runnerAtMesh = Grounded + new Vector3(.90f, 0f, -.30f).normalized * meshDistance;
            runner.Place(runnerAtMesh, direction);
            actor.Sample("Idle", Exchange, "Handoff Give", contact);
            runner.Sample("Jog Forward", Exchange, "Handoff Take", contact);
            reference = FootballActionPose.GetHandoffContact(actor.Animator, runner.Animator, actor.Root.transform, runner.Root.transform);
            hasReference = true;

            const float stride = 2.45f;
            float fromMesh = time - Exchange;
            // Carry the runner's incoming heading through contact, then arc upfield on the QB's outside.
            if (fromMesh <= 0f) runner.Place(runnerAtMesh + direction * (fromMesh * stride), direction);
            else
            {
                const float redirectTime = .14f;
                float incomingWeight = Mathf.Exp(-fromMesh / redirectTime);
                float carryOver = redirectTime * (1f - incomingWeight);
                Vector3 offset = (direction * carryOver + Vector3.forward * (fromMesh - carryOver)) * stride;
                runner.Place(runnerAtMesh + offset, Vector3.Lerp(Vector3.forward, direction, incomingWeight));
            }
            float approach = Mathf.InverseLerp(ReachStart, Exchange, time);
            float recovery = Mathf.InverseLerp(Exchange, HandoffEnd, time);
            float progress = time < Exchange ? contact * approach : Mathf.Lerp(contact, 1f, recovery);
            float approachWeight = Mathf.SmoothStep(0f, 1f, approach);
            float recoverWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, progress));
            actor.Sample("Idle", time, "Carry Ready", actor.BasePhase("Carry Ready", time), "Handoff Give", progress,
                approachWeight, 1f - recoverWeight);
            runner.Sample("Jog Forward", time, "Handoff Take", progress, "Carry Tuck", runner.BasePhase("Jog Forward", time),
                recoverWeight, time < Exchange ? approachWeight : 1f);
            float contactWeight = approachWeight * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(contact, .73f, progress)));
            Vector3 gripOffset = PlayerAnimationPresenter.DefaultFootballGripOffset;
            FootballActionPose.ApplyHandoff(actor.Animator, actor.Root.transform, reference, contactWeight, true, gripOffset);
            FootballActionPose.ApplyHandoff(runner.Animator, runner.Root.transform, reference, contactWeight, false, gripOffset);
            if (time < Exchange) ApplyCarry(actor, 0f, 1f - approachWeight);
            else ApplyCarry(runner, 1f, recoverWeight);
            var from = Grip(actor.Animator);
            var to = Grip(runner.Animator);
            HandoffGripGap = Vector3.Distance(from.position, to.position);
            float transfer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Exchange, Exchange + .075f, time));
            return new Pose(Vector3.Lerp(from.position, to.position, transfer), Quaternion.Slerp(from.rotation, to.rotation, transfer));
        }

        void SampleCut(float time, bool right, bool carrying)
        {
            Vector3 approach = Vector3.forward;
            Vector3 exit = new Vector3(right ? .91f : -.91f, 0f, .42f).normalized;
            string action = carrying ? "Carry Tuck" : null;
            hardCut.Cancel();
            if (time < CutStart)
            {
                actor.Place(Grounded + approach * ((time - CutStart) * 3.2f), approach);
                actor.Sample("Running", time, action, actor.BasePhase("Running", time));
                return;
            }
            actor.Sample("Running", CutStart, action, actor.BasePhase("Running", CutStart));
            bool planted = hardCut.CapturePlant(exit, approach, 4f);
            float progress = Mathf.InverseLerp(CutStart, CutEnd, time);
            float redirect = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .80f, progress));
            Vector3 facing = Vector3.Slerp(approach, exit, redirect);
            float approachTravel = .42f * (1f - Mathf.Pow(1f - progress, 3f));
            float exitTravel = .44f * redirect * redirect + Mathf.Max(0f, time - CutEnd) * 3.55f;
            actor.Place(Grounded + approach * approachTravel + exit * exitTravel, facing);
            actor.Sample("Running", time, action, actor.BasePhase("Running", time));
            if (!planted) throw new InvalidOperationException("The hard cut could not resolve the outside foot on this rig.");
            if (time < CutEnd)
            {
                hardCut.ApplyPose(progress, carrying);
                reference = hardCut.PlantWorldPosition;
                reference.y = .035f;
                hasReference = true;
            }
        }

        void BuildMarkers()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Missing URP Unlit shader for the preview grid.");
            Color[] colors = { new(.55f, .62f, .65f), new(.12f, .9f, 1f), new(1f, .51f, .13f), new(.41f, 1f, .39f) };
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                materials[i].SetColor("_BaseColor", colors[i]);
            }

            lines = new GameObject("25 cm ground grid", typeof(MeshFilter), typeof(MeshRenderer));
            preview.AddSingleGO(lines);
            lineMesh = new Mesh { name = "Ground grid", hideFlags = HideFlags.HideAndDontSave };
            var vertices = new Vector3[25 * 4];
            for (int i = 0; i < 25; i++)
            {
                float offset = (i - 12) * .25f;
                vertices[i * 4] = new Vector3(offset, 0f, -3f);
                vertices[i * 4 + 1] = new Vector3(offset, 0f, 3f);
                vertices[i * 4 + 2] = new Vector3(-3f, 0f, offset);
                vertices[i * 4 + 3] = new Vector3(3f, 0f, offset);
            }
            lineMesh.vertices = vertices;
            lineMesh.SetIndices(Enumerable.Range(0, vertices.Length).ToArray(), MeshTopology.Lines, 0);
            lines.GetComponent<MeshFilter>().sharedMesh = lineMesh;
            lines.GetComponent<MeshRenderer>().sharedMaterial = materials[0];

            for (int i = 0; i < marks.Length; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.name = i == 4 ? "Release, exchange, or plant point" : ((i & 1) == 0 ? "Left foot" : "Right foot");
                marker.transform.localScale = new Vector3(.085f, .025f, .085f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = materials[i == 4 ? 3 : (i % 2) + 1];
                preview.AddSingleGO(marker);
                marks[i] = marker;
            }
        }

        public void Frame(FootballPreviewAction clip, float yaw, bool showGrid, bool showMarkers, bool close = false)
        {
            lines.SetActive(showGrid);
            for (int i = 0; i < marks.Length; i++)
                marks[i].SetActive(showMarkers && (i < 2 || (i < 4 ? runner.Root.activeSelf : hasReference)));

            PlaceFeet(actor, 0);
            if (runner.Root.activeSelf) PlaceFeet(runner, 2);
            if (hasReference)
            {
                marks[4].transform.position = reference;
                marks[4].transform.localScale = Vector3.one * .065f;
            }
            bool paired = clip == FootballPreviewAction.Handoff;
            bool tight = clip == FootballPreviewAction.CarryIdle || clip == FootballPreviewAction.StandardThrow;
            Vector3 center = paired ? Vector3.Lerp(actor.Root.transform.position, runner.Root.transform.position, .5f)
                : actor.Root.transform.position;
            bool throwing = clip == FootballPreviewAction.StandardThrow;
            center.y = close ? (throwing ? 1.60f : 1.36f) : 1.07f;
            float distance = close ? (paired ? 3.5f : throwing ? 2.7f : 2.45f) : (paired ? 5.65f : tight ? 4.65f : 4.75f);
            var offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, close ? .12f : .9f, distance);
            preview.camera.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(-offset));
        }

        void PlaceFeet(Rig rig, int first)
        {
            for (int i = 0; i < 2; i++)
            {
                var foot = rig.Animator.GetBoneTransform(i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                if (foot == null) throw new InvalidOperationException("The Humanoid rig is missing a foot binding.");
                Vector3 location = foot.position;
                location.y = .015f;
                marks[first + i].transform.position = location;
            }
        }

        public Texture Render(float width, float height)
        {
            preview.BeginPreview(new Rect(0f, 0f, width, height), GUIStyle.none);
            preview.Render(true);
            return preview.EndPreview();
        }

        public void Capture(Texture2D atlas, int width, int height, int x, int y)
        {
            // A fresh CPU bake avoids reusing the first GPU skin pose in a single batch editor frame.
            var roots = runner.Root.activeSelf ? new[] { actor.Root, runner.Root } : new[] { actor.Root };
            var baked = new System.Collections.Generic.List<(SkinnedMeshRenderer Skin, GameObject Copy, Mesh Mesh)>();
            var before = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 0);
            try
            {
                foreach (var root in roots)
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skin => skin.enabled))
                {
                    var mesh = new Mesh();
                    var copy = new GameObject("Baked pose", typeof(MeshFilter), typeof(MeshRenderer));
                    baked.Add((skin, copy, mesh));
                    skin.BakeMesh(mesh);
                    copy.transform.SetParent(skin.transform, false);
                    copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = copy.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    var block = new MaterialPropertyBlock();
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        block.Clear();
                        skin.GetPropertyBlock(block, i);
                        renderer.SetPropertyBlock(block, i);
                    }
                    skin.enabled = false;
                }
                Graphics.Blit(Render(width, height), target);
                RenderTexture.active = target;
                atlas.ReadPixels(new Rect(0, 0, width, height), x, y, false);
            }
            finally
            {
                foreach (var entry in baked)
                {
                    Object.DestroyImmediate(entry.Copy);
                    Object.DestroyImmediate(entry.Mesh);
                    entry.Skin.enabled = true;
                }
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        public void Dispose()
        {
            actor?.Dispose();
            runner?.Dispose();
            preview.Cleanup();
            if (lineMesh != null) Object.DestroyImmediate(lineMesh);
            foreach (var material in materials)
                if (material != null) Object.DestroyImmediate(material);
        }

        sealed class Rig : IDisposable
        {
            public GameObject Root { get; }
            public Animator Animator { get; }
            public BodyArchetype Build { get; }
            PlayableGraph graph;
            readonly AnimationLayerMixerPlayable layer;
            readonly AnimationMixerPlayable actions;
            readonly AnimationMixerPlayable locomotion;
            readonly AnimationClipPlayable[] plays;
            readonly AnimationClip[] clips;
            int baseInput = -1;
            int baseSecondary = -1;
            int actionA = -1;
            int actionB = -1;

            public Rig(GameObject root, Animator animator, BodyArchetype build,
                System.Collections.Generic.Dictionary<string, AnimationClip> library, AvatarMask mask)
            {
                Root = root;
                Animator = animator;
                Build = build;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Football pose preview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                layer = AnimationLayerMixerPlayable.Create(graph, 2);
                actions = AnimationMixerPlayable.Create(graph, Clips.Length);
                locomotion = AnimationMixerPlayable.Create(graph, Clips.Length);
                clips = Clips.Select(name => library[name]).ToArray();
                plays = clips.Select(clip => AnimationClipPlayable.Create(graph, clip)).ToArray();
                for (int i = 0; i < plays.Length; i++)
                {
                    plays[i].SetApplyFootIK(false);
                    plays[i].SetApplyPlayableIK(false);
                    graph.Connect(plays[i], 0, ActionClips.Contains(Clips[i]) ? actions : locomotion, i);
                }
                layer.ConnectInput(0, locomotion, 0, 1f);
                layer.ConnectInput(1, actions, 0, 1f);
                layer.SetLayerMaskFromAvatarMask(1, mask);
                AnimationPlayableOutput.Create(graph, "Rig pose", animator).SetSourcePlayable(layer);
                graph.Play();
            }

            public void Place(Vector3 position, Vector3 direction) => Root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

            public float BasePhase(string clip, float time) => Mathf.Repeat(time, clips[Array.IndexOf(Clips, clip)].length) /
                clips[Array.IndexOf(Clips, clip)].length;

            public void Sample(string movement, float time, string first, float phase, string second = null,
                float secondPhase = 0f, float mix = 0f, float layerWeight = 1f, float gaitWeight = 1f)
            {
                if (baseInput >= 0) locomotion.SetInputWeight(baseInput, 0f);
                if (baseSecondary >= 0) locomotion.SetInputWeight(baseSecondary, 0f);
                if (actionA >= 0) actions.SetInputWeight(actionA, 0f);
                if (actionB >= 0) actions.SetInputWeight(actionB, 0f);
                baseInput = Array.IndexOf(Clips, movement);
                actionA = first == null ? -1 : Array.IndexOf(Clips, first);
                actionB = second == null ? -1 : Array.IndexOf(Clips, second);
                bool blendingGait = gaitWeight < 1f && movement != "Idle";
                baseSecondary = blendingGait ? Array.IndexOf(Clips, "Idle") : -1;
                Set(locomotion, baseInput, BasePhase(movement, time), blendingGait ? gaitWeight : 1f);
                Set(locomotion, baseSecondary, BasePhase("Idle", time), 1f - gaitWeight);
                Set(actions, actionA, phase, 1f - mix);
                Set(actions, actionB, secondPhase, mix);
                layer.SetInputWeight(1, first == null ? 0f : layerWeight);
                graph.Evaluate(0f);
            }

            void Set(AnimationMixerPlayable mixer, int input, float phase, float weight)
            {
                if (input < 0) return;
                mixer.SetInputWeight(input, weight);
                plays[input].SetTime(Mathf.Clamp(phase, 0f, .99999f) * clips[input].length);
            }

            public void Dispose()
            {
                if (graph.IsValid()) graph.Destroy();
            }
        }
    }
}

using System.Linq;
using UnityEngine;

// Development-scene controls. Production prefabs and controllers are sampled in place.
[RequireComponent(typeof(Camera))]
public sealed class CharacterLineupReview : MonoBehaviour
{
    static readonly string[] Views = { "Front", "Side", "Three-quarter" };
    // These sample points are thresholds on the shipped directional blend tree.
    public static readonly (string Label, string Clip, float Forward, float Lateral, float Variant, string State)[] Poses = {
        ("Idle", "Idle", 0, 0, 0, "Locomotion"), ("Idle alternate", "Idle-2", 0, 0, 1, "Locomotion"),
        ("Walk", "Walking", 1f/3, 0, 0, "Locomotion"), ("Walk wide", "Walking-Wide", 1f/3, 0, 1, "Locomotion"),
        ("Jog", "Jog Forward", 2f/3, 0, 0, "Locomotion"), ("Backpedal", "Jog Backward", -1, 0, 0, "Locomotion"),
        ("Run", "Running", 1, 0, 0, "Locomotion"), ("Fast run", "Running-Faster", 2, 0, 0, "Locomotion"),
        ("Sprint", "Running-Sprint", 3, 0, 0, "Locomotion"), ("Strafe left", "Strafe", 0, -1, 0, "Locomotion"),
        ("Strafe right", "Strafe", 0, 1, 0, "Locomotion"), ("Hurdle", "Jump", 0, 0, 0, "Hurdle"),
        ("Stumble", "Jogging Stumble", 0, 0, 0, "Stumble"),
        ("Carry ready", "Carry Ready", 0, 0, 0, "Carry"), ("Carry tuck", "Carry Tuck", 0, 0, 0, "Carry"),
        ("Throw windup", "Throw Windup", 0, 0, 0, "Throw Windup"),
        ("Throw release", "Throw Follow Through", 0, 0, 0, "Throw Follow Through"),
        ("Handoff give", "Handoff Give", 0, 0, 0, "Handoff Give"), ("Handoff take", "Handoff Take", 0, 0, 0, "Handoff Take")
    };
    static readonly string[] Motions = Poses.Select(p => p.Label).ToArray();
    float[] durations;
    Animator[] actors;
    string[] choices;
    Camera lens;
    int motion, view, focus;
    float phase;
    bool paused;

    void Awake()
    {
        lens = GetComponent<Camera>();
        actors = gameObject.scene.GetRootGameObjects().Select(o => o.GetComponent<Animator>())
            .Where(a => a).OrderBy(a => a.transform.position.x).ToArray();
        choices = new[] { "All six" }.Concat(actors.Select(a => a.name)).ToArray();
        // A manually sampled Animator has zero speed. Use the authored clip durations for playback.
        var clips = actors[0].runtimeAnimatorController.animationClips;
        durations = Poses.Select(p => clips.First(clip => clip.name == p.Clip).length).ToArray();
        foreach (var actor in actors) actor.speed = 0;
        lens.clearFlags = CameraClearFlags.SolidColor;
        lens.backgroundColor = new Color(.19f, .20f, .22f);
        Sample();
    }

    void LateUpdate()
    {
        if (!paused) phase = Mathf.Repeat(phase + Time.unscaledDeltaTime / durations[motion], 1f);
        Sample();
        // Rotate each body around its feet so the side view keeps six separate silhouettes.
        for (int i = 0; i < actors.Length; i++)
        {
            var actor = actors[i];
            actor.transform.localRotation = Quaternion.Euler(0, view == 1 ? 90 : view == 2 ? 35 : 0, 0);
            actor.gameObject.SetActive(focus == 0 || focus == i + 1);
        }
        float panel = Mathf.Min(175f, Screen.height * .4f);
        lens.rect = new Rect(0, 0, 1, 1 - panel / Screen.height);
        float center = focus == 0 ? 0 : actors[focus - 1].transform.position.x;
        lens.transform.SetPositionAndRotation(new Vector3(center, .88f, 9), Quaternion.Euler(0, 180, 0));
        lens.orthographicSize = Mathf.Max(1.22f, (focus == 0 ? 4.6f : .85f) / lens.aspect);
    }

    void Sample()
    {
        foreach (var actor in actors)
            if (actor.gameObject.activeSelf) SamplePose(actor, motion, phase);
    }

    public static int SamplePose(Animator actor, int index, float time)
    {
        var pose = Poses[index];
        bool football = pose.State != "Locomotion" && pose.State != "Hurdle" && pose.State != "Stumble";
        actor.SetFloat("Locomotion", pose.Forward);
        actor.SetFloat("Lateral", pose.Lateral);
        actor.SetFloat("LocomotionVariant", pose.Variant);
        actor.SetBool("Hurdling", pose.State == "Hurdle");
        actor.SetBool("Stumbling", pose.State == "Stumble");
        actor.SetFloat("LocomotionRate", 1);
        actor.SetFloat("HurdleRate", 1);
        actor.SetFloat("StumbleProgress", time);
        actor.SetFloat("CarryPace", pose.Clip == "Carry Tuck" ? 1 : 0);
        actor.SetLayerWeight(1, football ? 1 : 0);
        actor.Play(football ? "Base Layer.Locomotion" : "Base Layer." + pose.State, 0, football ? 0 : time);
        if (football) actor.Play("Football." + pose.State, 1, time);
        actor.SetFloat("CarryTime", time);
        actor.SetFloat("ThrowWindupTime", time);
        actor.SetFloat("FootballActionTime", time);
        actor.Update(0);
        return football ? 1 : 0;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(6, 4, Screen.width - 12, 170));
        focus = GUILayout.Toolbar(focus, choices);
        view = GUILayout.Toolbar(view, Views);
        int next = GUILayout.SelectionGrid(motion, Motions, 6);
        if (motion != next) { motion = next; phase = 0; }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(paused ? "Play" : "Pause", GUILayout.Width(72))) paused = !paused;
        float nextPhase = GUILayout.HorizontalSlider(phase, 0, .9999f);
        if (!Mathf.Approximately(nextPhase, phase)) { paused = true; phase = nextPhase; }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        foreach (var actor in actors)
        {
            if (!actor.gameObject.activeSelf) continue;
            Vector3 screen = lens.WorldToScreenPoint(actor.transform.position + Vector3.down * .06f);
            GUI.Box(new Rect(screen.x - 45, Screen.height - screen.y, 90, 22), actor.name);
        }
    }
}

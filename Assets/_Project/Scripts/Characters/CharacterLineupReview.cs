using System.Linq;
using UnityEngine;

// Development-scene controls. Production prefabs and controllers are sampled in place.
[RequireComponent(typeof(Camera))]
public sealed class CharacterLineupReview : MonoBehaviour
{
    static readonly string[] Views = { "Front", "Side", "Three-quarter" };
    static readonly string[] Motions = { "Idle", "Run", "Sprint", "Backpedal", "Hurdle",
        "Carry ready", "Carry tuck", "Throw windup", "Throw release", "Handoff give", "Handoff take" };
    static readonly string[] FootballStates = { "Carry", "Carry", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take" };
    static readonly string[] ClipNames = { "mixamo.com", "Standard Run", "Fast Run", "Running Backward", "Jump",
        "Carry Ready", "Carry Tuck", "Throw Windup", "Throw Follow Through", "Handoff Give", "Handoff Take" };
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
        durations = ClipNames.Select(name => clips.First(clip => clip.name == name).length).ToArray();
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
        float panel = Mathf.Min(150f, Screen.height * .4f);
        lens.rect = new Rect(0, 0, 1, 1 - panel / Screen.height);
        float center = focus == 0 ? 0 : actors[focus - 1].transform.position.x;
        lens.transform.SetPositionAndRotation(new Vector3(center, .88f, 9), Quaternion.Euler(0, 180, 0));
        lens.orthographicSize = Mathf.Max(1.22f, (focus == 0 ? 4.6f : .85f) / lens.aspect);
    }

    void Sample()
    {
        bool football = motion >= 5;
        foreach (var actor in actors)
        {
            if (!actor.gameObject.activeSelf) continue;
            actor.SetFloat("Locomotion", motion == 1 ? 1 : motion == 2 ? 2 : motion == 3 ? -1 : 0);
            actor.SetBool("Hurdling", motion == 4);
            actor.SetFloat("HurdleRate", 1);
            actor.SetFloat("LocomotionRate", 1);
            actor.SetFloat("CarryPace", motion == 6 ? 1 : 0);
            actor.SetLayerWeight(1, football ? 1 : 0);
            actor.Play(motion == 4 ? "Base Layer.Hurdle" : "Base Layer.Locomotion", 0, football ? 0 : phase);
            if (football) actor.Play("Football." + FootballStates[motion - 5], 1, phase);
            actor.SetFloat("ThrowWindupTime", phase);
            actor.SetFloat("FootballActionTime", phase);
            actor.Update(0);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(6, 4, Screen.width - 12, 145));
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Owns the entire dead-ball -> live-ball flow now, not just cosmetics.
//
// Flow: PlayEnded -> gather in huddle (animated) -> wait for play call -> break to
// formation (animated) -> wait for snap (player input OR playClockDuration timer) ->
// PlayState.ResetPlay().
//
// "Select play" is stubbed to the Input System's unused "Previous" action (keyboard 1)
// until real play-calling UI exists. Swap SelectPlay() to be called from that UI later —
// the state machine doesn't care where the call comes from.
public class HuddleController : MonoBehaviour
{
    enum HuddlePhase { Idle, Gathering, InHuddle, BreakingToFormation, WaitingForSnap }

    [SerializeField] Transform player;
    [SerializeField] string teammateTag = "Teammate";
    [SerializeField] float huddleRadius = 1.5f;
    [SerializeField] float huddleDistanceBehindLOS = 2f;
    [SerializeField] float moveSpeed = 6f; // shared "walk to spot" speed for huddle + break — same value for everyone, tune later per-need
    [SerializeField] float positionTolerance = 0.1f;
    [SerializeField] float playClockDuration = 40f; // forces a snap if nobody calls one — real football rule, not arbitrary

    HuddlePhase phase = HuddlePhase.Idle;
    float playClockTimer;
    InputSystem_Actions controls;

    void Awake()
    {
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Previous.performed += ctx => SelectPlay();   // TEMP stand-in for play-call UI
        controls.Player.ResetPlay.performed += ctx => TrySnap();     // manual snap, only does anything in WaitingForSnap
    }

    void OnDisable() => controls.Player.Disable();

    void Start()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded += HandlePlayEnded;
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason)
    {
        StopAllCoroutines();
        StartCoroutine(GatherToHuddle());
    }

    List<Transform> GetOffense()
    {
        var members = new List<Transform>();
        if (player != null) members.Add(player);
        foreach (var go in GameObject.FindGameObjectsWithTag(teammateTag))
            members.Add(go.transform);
        return members;
    }

    IEnumerator GatherToHuddle()
    {
        phase = HuddlePhase.Gathering;

        float losZ = PlayState.Instance.CurrentLineOfScrimmageZ;
        Vector3 huddleCenter = new Vector3(0f, 1f, losZ - huddleDistanceBehindLOS);
        var members = GetOffense();
        var targets = CircleTargets(members.Count, huddleCenter, huddleRadius);

        yield return MoveAllTo(members, targets, faceCenter: huddleCenter);

        phase = HuddlePhase.InHuddle;
        // sits here indefinitely until SelectPlay() is called — no auto-advance
    }

    // Hook this up to real play-calling UI later. Anything can call it as long as
    // the huddle is actually waiting on a play (calling it mid-play or mid-break does nothing).
    public void SelectPlay()
    {
        if (phase != HuddlePhase.InHuddle) return;
        StartCoroutine(BreakToFormation());
    }

    IEnumerator BreakToFormation()
    {
        phase = HuddlePhase.BreakingToFormation;

        float losZ = PlayState.Instance.CurrentLineOfScrimmageZ;
        var members = GetOffense();
        var targets = new List<Vector3> { new Vector3(0f, 1f, losZ) }; // player's snap spot
        // TODO: once offensive FormationData exists, pull real per-receiver slots here
        // instead of stacking everyone on the player's spot. Flagged, not blocking.
        for (int i = 1; i < members.Count; i++)
            targets.Add(new Vector3(0f, 1f, losZ) + Vector3.right * (i * 2f));

        yield return MoveAllTo(members, targets, faceCenter: null, faceDownfield: true);

        phase = HuddlePhase.WaitingForSnap;
        playClockTimer = playClockDuration;
    }

    void Update()
    {
        if (phase != HuddlePhase.WaitingForSnap) return;

        playClockTimer -= Time.deltaTime;
        if (playClockTimer <= 0f)
            Snap();
    }

    void TrySnap()
    {
        if (phase == HuddlePhase.WaitingForSnap) Snap();
    }

    void Snap()
    {
        phase = HuddlePhase.Idle;
        PlayState.Instance.ResetPlay();
    }

    List<Vector3> CircleTargets(int count, Vector3 center, float radius)
    {
        var result = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            result.Add(center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
        }
        return result;
    }

    // Animated walk — MoveTowards, no physics, consistent with the rest of the project.
    // Waits until every member is within tolerance before returning, so huddle/break
    // phases don't advance until the visual is actually done.
    IEnumerator MoveAllTo(List<Transform> members, List<Vector3> targets, Vector3? faceCenter, bool faceDownfield = false)
    {
        bool anyMoving = true;
        while (anyMoving)
        {
            anyMoving = false;
            for (int i = 0; i < members.Count && i < targets.Count; i++)
            {
                if (members[i] == null) continue;
                Vector3 pos = members[i].position;
                if (Vector3.Distance(pos, targets[i]) > positionTolerance)
                {
                    members[i].position = Vector3.MoveTowards(pos, targets[i], moveSpeed * Time.deltaTime);
                    anyMoving = true;
                }

                Vector3? faceDir = faceCenter.HasValue ? (faceCenter.Value - members[i].position)
                                  : faceDownfield ? Vector3.forward
                                  : (Vector3?)null;
                if (faceDir.HasValue && faceDir.Value.sqrMagnitude > 0.01f)
                    members[i].rotation = Quaternion.LookRotation(faceDir.Value.normalized);
            }
            yield return null;
        }
    }
}
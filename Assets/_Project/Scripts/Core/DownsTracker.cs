using UnityEngine;

// Answers "where are we in the drive" — distinct from PlayState.PossessionTeamId, which
// only answers "who has the ball." Sits above individual play execution: listens to
// PlayState's snap/play-end events and derives Down/YardsToGo from them, rather than any
// other system computing downs math independently. FieldLines' first-down marker reads
// YardsToGo from here instead of carrying its own placeholder distance.
//
// Yards are measured through PlayState.DirectionFor(driveTeamId) — never raw world Z,
// same rule as the rest of the possession work. "Old LOS" for the yards-gained diff is
// the spot THIS play was actually snapped from (captured on OnPlayReset), not some
// separately-tracked marker — see class doc for why those are mathematically identical
// as long as YardsToGo means "distance remaining," which is what down-and-distance
// already means in real football.
//
// SETUP: drop on GameManager, alongside PlayState. No Inspector wiring required.
public class DownsTracker : MonoBehaviour
{
    public const int MaxDowns = 4;
    const float StartingYardsToGo = 10f;

    public static DownsTracker Instance { get; private set; }

    public int CurrentDown { get; private set; } = 1;
    public float YardsToGo { get; private set; } = StartingYardsToGo;
    public bool IsGoalToGo { get; private set; }

    // Which team this set of downs belongs to. A change of possession always starts a
    // fresh drive for the new team — that's the reset condition itself, not an input
    // to the gained/YardsToGo comparison below.
    public int DriveTeamId { get; private set; }

    // World-Z this specific play was snapped from. Captured once per PLAY (not once per
    // set of downs) — the check is always "this play vs. what was needed before it."
    float preSnapLOS;

    // Fires after Down/YardsToGo/DriveTeamId have all settled for a given resolution —
    // once per snap-to-whistle cycle. UI/HUD hooks in here, not into PlayState directly.
    public event System.Action OnDownsChanged;

    void Awake() => Instance = this;

    // Subscribed in Start(), not OnEnable() — same reasoning as BallController: Unity
    // guarantees every Awake() finishes before any Start() runs, but Awake/OnEnable
    // ordering BETWEEN separate GameObjects isn't guaranteed, so OnEnable could fire
    // before PlayState.Instance exists depending on scene object order.
    void Start()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayReset += HandlePlayReset;
        PlayState.Instance.OnPlayEnded += HandlePlayEnded;

        // PlayState.Awake() already set PossessionTeamId and the opening LOS by now —
        // start the very first drive from that instead of waiting for the first snap.
        StartFreshDrive(PlayState.Instance.PossessionTeamId, PlayState.Instance.CurrentLineOfScrimmageZ);
    }

    void OnDestroy()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayReset -= HandlePlayReset;
        PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
    }

    void HandlePlayReset()
    {
        preSnapLOS = PlayState.Instance.CurrentLineOfScrimmageZ;
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason)
    {
        var ps = PlayState.Instance;

        // Possession already changed hands by now if this was a defensive score
        // (PlayState.ResolvePossessionAtWhistle runs before OnPlayEnded fires) — fresh
        // drive for whoever's holding it now, full stop.
        if (ps.PossessionTeamId != DriveTeamId)
        {
            StartFreshDrive(ps.PossessionTeamId, ps.CurrentLineOfScrimmageZ);
            return;
        }

        // Scoring plays reset to a fresh drive at the kickoff spot PlayState already
        // computed, same team (no receiving-team system yet — see PlayState). Down never
        // increments on a score, regardless of what down it was.
        if (reason == PlayState.PlayEndReason.Touchdown || reason == PlayState.PlayEndReason.Safety)
        {
            StartFreshDrive(ps.PossessionTeamId, ps.CurrentLineOfScrimmageZ);
            return;
        }

        // Still the same team's drive so far — measure this play in THEIR direction only.
        // ps.CurrentLineOfScrimmageZ is already exactly right for both cases: ball position
        // for a tackle/OOB, unchanged (== preSnapLOS) for an incomplete pass — no separate
        // incomplete-pass branch needed anywhere in this method.
        FieldDirection dir = ps.DirectionFor(DriveTeamId);
        float gained = dir.YardsPastLOS(ps.CurrentLineOfScrimmageZ, preSnapLOS);

        if (gained >= YardsToGo)
        {
            CurrentDown = 1;
            YardsToGo = ClampToGoalLine(StartingYardsToGo, ps.CurrentLineOfScrimmageZ);
            OnDownsChanged?.Invoke();
            return;
        }

        CurrentDown++;
        YardsToGo = ClampToGoalLine(YardsToGo - gained, ps.CurrentLineOfScrimmageZ);

        if (CurrentDown <= MaxDowns)
        {
            OnDownsChanged?.Invoke();
            return;
        }

        ps.TrySetPossession(PlayState.OpponentOf(DriveTeamId));
        StartFreshDrive(ps.PossessionTeamId, ps.CurrentLineOfScrimmageZ);
    }

    void StartFreshDrive(int teamId, float startingLOS)
    {
        DriveTeamId = teamId;
        CurrentDown = 1;
        preSnapLOS = startingLOS;
        YardsToGo = ClampToGoalLine(StartingYardsToGo, startingLOS);
        OnDownsChanged?.Invoke();
    }

    // Never let YardsToGo ask for more than what's actually left to the end zone — this is
    // what makes FieldLines' first-down marker never need to hide or clamp at render time
    // anymore; it just draws at LOS + YardsToGo, and that's always inside the field now, by
    // construction, instead of by a check on the rendering side.
    float ClampToGoalLine(float rawYardsToGo, float losZ)
    {
        FieldDirection dir = PlayState.Instance.DirectionFor(DriveTeamId);
        float distanceToGoal = dir.YardsPastLOS(dir.TargetGoalLineZ, losZ);
        IsGoalToGo = rawYardsToGo >= distanceToGoal;
        return Mathf.Min(rawYardsToGo, distanceToGoal);
    }
}
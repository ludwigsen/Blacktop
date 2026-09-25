using UnityEngine;

// The actual scoreboard — deliberately separate from PlayState's OffenseMeter/DefenseMeter
// (the Gamebreaker fill bars). Those ALSO call their setter "AddOffensePoints" and ALSO
// add 10 on a touchdown — same vocabulary, completely unrelated system, pure naming
// coincidence. Don't wire a future HUD to the wrong one.
//
// Sits above play execution the same way DownsTracker does: listens to PlayState.OnPlayEnded,
// derives who scored from PlayState.PossessionTeamId AFTER the whistle rather than resolving
// BallController.Carrier itself — see class-level note on ResolvePossessionAtWhistle's
// ordering for why that's already safe to read here, including on a fumble-return score.
//
// SETUP: drop on GameManager, alongside PlayState/DownsTracker. No Inspector wiring required.
public class ScoreBoard : MonoBehaviour
{
    const int TouchdownPoints = 6;
    const int SafetyPoints = 2;

    public static ScoreBoard Instance { get; private set; }

    public int Team1Score { get; private set; }
    public int Team2Score { get; private set; }

    public event System.Action OnScoreChanged;

    void Awake() => Instance = this;

    // Start(), not OnEnable() — same Awake/Start ordering reasoning as DownsTracker and
    // BallController: guarantees PlayState.Instance already exists regardless of which
    // GameObject's Awake() happens to run first in the scene.
    void Start()
    {
        if (PlayState.Instance == null) return;
        PlayState.Instance.OnPlayEnded += HandlePlayEnded;
    }

    void OnDestroy()
    {
        if (PlayState.Instance != null)
            PlayState.Instance.OnPlayEnded -= HandlePlayEnded;
    }

    void HandlePlayEnded(PlayState.PlayEndReason reason)
    {
        if (reason != PlayState.PlayEndReason.Touchdown && reason != PlayState.PlayEndReason.Safety)
            return;

        var ps = PlayState.Instance;

        // Touchdown: PossessionTeamId is whoever just carried it in — credit them.
        // Safety: PossessionTeamId is whoever was tackled in THEIR OWN end zone — the
        // points go to the OTHER team, not them. This is the one branch that can't
        // just reuse PossessionTeamId directly.
        int scoringTeam = reason == PlayState.PlayEndReason.Touchdown
            ? ps.PossessionTeamId
            : PlayState.OpponentOf(ps.PossessionTeamId);

        int points = reason == PlayState.PlayEndReason.Touchdown ? TouchdownPoints : SafetyPoints;
        Add(scoringTeam, points);
    }

    void Add(int teamId, int points)
    {
        if (teamId == 0) Team1Score += points;
        else Team2Score += points;

        OnScoreChanged?.Invoke();
    }

    public int ScoreFor(int teamId) => teamId == 0 ? Team1Score : Team2Score;
}
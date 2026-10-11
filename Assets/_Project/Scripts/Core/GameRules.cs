using UnityEngine;

// Match rules, kept as data so they're tunable without touching code. Deliberately tiny:
// first-to-N is the whole win condition for now. A timer / mode selection can grow in here
// later (Assets > Create > Blacktop > Game Rules) without anything else changing.
//
// Optional asset: ScoreBoard falls back to a built-in default (21) when none is assigned.
[CreateAssetMenu(menuName = "Blacktop/Game Rules")]
public class GameRules : ScriptableObject
{
    [Tooltip("First team to reach or pass this score wins, immediately, on the play that gets them there. No win-by-2.")]
    [Min(1)] public int scoreToWin = 21;
}
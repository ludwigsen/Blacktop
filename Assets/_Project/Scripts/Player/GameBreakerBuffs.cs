using UnityEngine;

// One Inspector-tunable multiplier keeps the possession boost flat across all twelve attributes.
[CreateAssetMenu(menuName = "Blacktop/Gamebreaker Buffs")]
public class GamebreakerBuffs : ScriptableObject
{
    [SerializeField, Min(1f)] float multiplier = 1.2f;
    public float Get(AttributeStat stat) => multiplier;
}

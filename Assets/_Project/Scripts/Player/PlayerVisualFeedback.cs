using UnityEngine;

namespace Blacktop.Player
{
    // Colors the player capsule based on current PlayerStateMachine state.
    // Cheap way to visually confirm state transitions during playtesting
    // before any real animation/model exists.
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerVisualFeedback : MonoBehaviour
    {
        [Header("Target renderer (the capsule's MeshRenderer)")]
        [SerializeField] private Renderer bodyRenderer;

        [Header("State Colors")]
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color walkColor = Color.cyan;
        [SerializeField] private Color runColor = new Color(0f, 0.3f, 1f);
        [SerializeField] private Color jukeColor = Color.yellow;
        [SerializeField] private Color hurdleColor = Color.green;
        [SerializeField] private Color stiffArmColor = Color.red;

        private PlayerStateMachine stateMachine;

        // Unity idiom: use MaterialPropertyBlock instead of renderer.material.color.
        // Touching .material directly creates a per-instance material clone at runtime
        // (leaks/allocates every call, breaks batching). MPB avoids both.
        private MaterialPropertyBlock mpb;

        private void Awake()
        {
            stateMachine = GetComponent<PlayerStateMachine>();
            mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            // Polling here because PlayerStateMachine doesn't currently expose
            // a state-changed event — just a CurrentState-style property (assumed below).
            // If that's wrong, tell me the real property/enum name.
            ApplyColorForState(stateMachine.CurrentState);
        }

        private void ApplyColorForState(PlayerState state)
        {
            Color c = state switch
            {
                PlayerState.Idle => idleColor,
                PlayerState.Walk => walkColor,
                PlayerState.Run => runColor,
                PlayerState.Juke => jukeColor,
                PlayerState.Hurdle => hurdleColor,
                PlayerState.StiffArm => stiffArmColor,
                _ => idleColor
            };

            bodyRenderer.GetPropertyBlock(mpb);
            // URP Lit shader uses "_BaseColor". If you're on Built-in RP, swap to "_Color".
            mpb.SetColor("_BaseColor", c);
            bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}
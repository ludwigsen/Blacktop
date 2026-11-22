# Football animation readiness

Scope is the production controller on all six MPFB Avatars. Each installed clip was the dominant clip at nine sampled times on each body. The per-body Unity CSVs record baked vertex motion and height. Each Unity image sheet shows samples 2 and 6. The twelve Blender diagnostic poses test skinning. They are separate from playable animation assets.

The five locomotion/hurdle FBXs are imported Humanoid takes from the installed Mixamo set. The six football actions are native Unity Humanoid muscle clips. Their Football upper-body AvatarMask overlays the locomotion layer. The controller and all original animation GUIDs were retained. Every production Animator disables root motion. Transform-driven movement, the possession state and the move clock remain authoritative.

Priorities are 1 for planted locomotion, 2 for possession and exchange, and 3 for reactions or secondary variants. Keep indicates an installed clip with no planned replacement. Production asset links resolve relative to this file.

| Action | Active asset and gameplay usage | Import / retarget | Readiness and next check | Production asset | Priority |
|---|---|---|---|---|---|
| Idle | Breathing Idle.fbx, imported take mixamo.com, locomotion 0 | PASS | Working. Blends into the forward and reverse gaits. | [Breathing Idle.fbx](../../../Assets/_Project/Art/Animations/Breathing%20Idle.fbx) | Keep |
| Walk | Low positive speeds blend idle and Standard Run | NOT TESTED as a dedicated clip | Missing from production. Walking and Walking-Wide FBXs exist in the separate StandardFootball checkout. Review ground contact there. | Missing | 1 |
| Jog | Intermediate forward speed uses the locomotion blend | NOT TESTED as a dedicated clip | Missing from production. The separate checkout contains Jog Forward and Jog Backward. | Missing | 1 |
| Run | Standard Run.fbx at the standard-speed threshold | PASS | Working. Check foot planting at match camera distance. | [Standard Run.fbx](../../../Assets/_Project/Art/Animations/Standard%20Run.fbx) | 1 |
| Sprint | Fast Run.fbx at the fast-speed threshold | PASS | Working. Check cadence across speed attributes and height bands. | [Fast Run.fbx](../../../Assets/_Project/Art/Animations/Fast%20Run.fbx) | 1 |
| Acceleration | Damped locomotion blend follows actual displacement | NOT TESTED as a clip | Missing. Needs a plant and first step for large speed changes. | Missing | 1 |
| Deceleration | Locomotion parameter damps back toward idle | NOT TESTED as a clip | Missing. Needs a readable braking step. | Missing | 1 |
| Backpedal | Running Backward.fbx on negative locomotion | PASS | Working. Coverage and pre-snap footwork still need a controller review. | [Running Backward.fbx](../../../Assets/_Project/Art/Animations/Running%20Backward.fbx) | 1 |
| Lateral shuffle | No dedicated lateral state in the production blend tree | NOT TESTED | Missing from production. Strafe.fbx exists in the separate checkout. | Missing | 1 |
| Hard cut / juke | Gameplay JukeMove changes movement | NOT TESTED | Missing. Author matched left and right cuts around the existing input window. | Missing | 1 |
| Pivot and turn | Gameplay facing rotates the actor | NOT TESTED | Missing. Review planted turns before adding alternate turns. | Missing | 1 |
| Carry | Carry Ready and Carry Tuck blend with pace on the Football layer | PASS | Present but visually inadequate. Refine forearm-to-torso grip, particularly on wider bodies. | [Carry Ready.anim](../../../Assets/_Project/Art/Animations/Carry%20Ready.anim), [Carry Tuck.anim](../../../Assets/_Project/Art/Animations/Carry%20Tuck.anim) | 2 |
| Handoff give | Handoff Give follows the exchange cue and hand reach | PASS | Present but visually inadequate. The staged gameplay capture shows the ball crossing between open hands. Match the giving palm to the receiving pocket at transfer. | [Handoff Give.anim](../../../Assets/_Project/Art/Animations/Handoff%20Give.anim) | 2 |
| Receive handoff | Handoff Take on the receiving runner | PASS | Present but visually inadequate. Check approach, single-ball continuity and separation at match speed. | [Handoff Take.anim](../../../Assets/_Project/Art/Animations/Handoff%20Take.anim) | 2 |
| Throw | Throw Windup and Throw Follow Through around the existing pass-release clock | PASS | Present but visually inadequate. Add lower-body weight transfer and refine wrist path without moving the gameplay release. | [Throw Windup.anim](../../../Assets/_Project/Art/Animations/Throw%20Windup.anim), [Throw Follow Through.anim](../../../Assets/_Project/Art/Animations/Throw%20Follow%20Through.anim) | 2 |
| Catch | Gameplay resolves a reception without an authored catch clip | NOT TESTED | Missing. Prioritize chest-level reach and catch-to-tuck. | Missing | 2 |
| High catch | No dedicated production clip | NOT TESTED | Missing. Add after the standard catch has a reliable contact frame. | Missing | 3 |
| Low catch | No dedicated production clip | NOT TESTED | Missing. Verify hands can reach below the waist while retaining balance. | Missing | 3 |
| Secure possession | Existing carry layer starts after ownership changes | NOT TESTED as a clip | Missing. Connect first ball contact to the carry pose. | Missing | 2 |
| Tackle | Polled contact and whistle resolve the play | NOT TESTED as a clip | Missing. Start with one synchronized tackle and reaction. | Missing | 3 |
| Break tackle | Gameplay can resolve a failed tackle | NOT TESTED | Missing. Author the runner reaction after the base tackle. | Missing | 3 |
| Stumble | Wall recoil tilts the visual wrapper during StumbleMove | NOT TESTED as a clip | Missing from production. Jogging Stumble.fbx exists in the separate checkout. | Missing | 3 |
| Fall | No dedicated production clip | NOT TESTED | Missing. Pair with tackle, ground contact and whistle timing. | Missing | 3 |
| Get up | No dedicated production clip | NOT TESTED | Missing. Fit recovery to the existing dead-ball interval. | Missing | 3 |
| Other collision reactions | No dedicated production clip | NOT TESTED | Missing. Defer variants until tackle and fall read clearly. | Missing | 3 |
| Hurdle | Jump.fbx, timed by the active hurdle move | PASS | Working. The controller enters Hurdle and returns to locomotion on cancel. | [Jump.fbx](../../../Assets/_Project/Art/Animations/Jump.fbx) | Keep |

The Play Mode animation tests exercise forward and backward blending, movement of the actor root, teleport resets, hurdle timing and cancellation, hold and release for human and CPU passes, and both sides of a designed handoff. The presenter resolves grip from the selected Avatar's right hand and finger bases. Ball rulings use the existing authoritative position. Visual facing and root-rotation checks passed on the match actors. Physical-controller cadence, foot sliding over a full match and every body/ball contact at game speed remain NOT TESTED.

The independent, modified checkout under Builds/StandardFootball/Blacktop contains candidate clips and controller changes. They were inventoried only. No result here certifies those candidates. First evaluate straightforward locomotion and planted cuts, then catch-to-tuck and matched handoffs, then tackle, fall and recovery.

**First production task:** evaluate the existing walk/jog candidates against Standard Run at actual gameplay speeds on Slim, Athletic and Power. Establish planted feet through acceleration and braking before authoring cuts. For the subsequent ball-handling pass, use the [staged handoff frames](Handoff-contact.jpg) to align the receiving hands around the existing ownership-transfer clock.

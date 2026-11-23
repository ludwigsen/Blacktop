# Football animation readiness · 2026-11-22

Unity 6000.5.6f1 evaluated the shared StandardFootball controller on Slim, Athletic, Muscular, Stocky, Power and Massive. Each fitted model supplied its own valid Humanoid Avatar. At each of 1,026 samples the expected clip had greater than 90% weight on the sampled layer, the skinned vertices were finite, and the body stayed within the review height limits. All 114 body/pose combinations showed measurable vertex motion. Strafe left and right share a clip. [Per-body measurements](StandardFootball-samples.csv)

The full Editor Play Mode suite passed **186/186**, including the six fitted bodies at three heights, planted cuts in both directions, handoff and pass ownership, and Head 2 Head and Pickup on both player sides. The lineup scene is `Assets/Scenes/MPFBCharacterReview.unity`. Enter Play Mode to select a body, view and motion. Use Pause and the slider to inspect a phase. For two-player exchanges and cuts, use the **Blacktop Animations** menu.

## Production motions

All asset names below are relative to [Assets/_Project/Art/Animations](../../../Assets/_Project/Art/Animations/). PASS in the controller column means Unity sampled that motion on all six bodies. The previous eleven-clip controller's measurements and images remain in the migration checkpoint. [Earlier integration review](Integration.md)

| Action | Production asset | Controller or gameplay result | Quality and priority |
|---|---|---|---|
| Idle | Idle.fbx, Idle-2.fbx | PASS · team variants at rest | Review transition into the first step. |
| Walk | Walking.fbx, Walking-Wide.fbx | PASS · team variants at the walk threshold | 1 · check foot slide on both heights and gaits. |
| Jog and backpedal | Jog Forward.fbx, Jog Backward.fbx | PASS · signed forward pace | 1 · evaluate backward steps and direction changes. |
| Run, fast run and sprint | Running.fbx, Running-Faster.fbx, Running-Sprint.fbx | PASS · three forward thresholds | 1 · compare foot contact against actual field travel. |
| Lateral shuffle | Strafe.fbx | PASS · left mirrored, right unmirrored | 1 · review diagonal blends and exit steps. |
| Hard cut and juke | Procedural pose in Assets/_Project/Scripts/Player/StandardHardCut.cs | PASS · planted outside ankle, pelvis loading, root and carry-arm preservation, interruption | 1 · evaluate plant and recovery during a human-controlled play. |
| Acceleration, deceleration and pivot | No dedicated clips | NOT TESTED as separate motions · gaits blend with displacement | 1 · examine first step, brake and planted turns. |
| Carry | Carry Ready.anim, Carry Tuck.anim | PASS · masked layer and fitted grip | 2 · inspect wrist and torso clearance during turns and sprint. |
| Throw | Throw Windup.anim, Throw Follow Through.anim | PASS · windup, release, recovery and stationary weight transfer | 2 · judge release and lower-body timing at match speed. |
| Handoff give and receive | Handoff Give.anim, Handoff Take.anim | PASS · approach, transfer, single-ball continuity and settling | 2 · FAIL for a closed pocket at 2 m fixture separation. See measurements below. |
| Catch, high/low catch and secure-to-tuck | No dedicated clips | NOT TESTED · gameplay resolves the reception and carry follows possession | 2 · author ordinary catch-to-tuck first. |
| Hurdle | Jump.fbx | PASS · follows existing hurdle duration and cancels | Retain. |
| Wall stumble | Jogging Stumble.fbx | PASS · follows wall recoil progress | 3 · inspect transitions in both directions. |
| Tackle, break tackle, fall, get up and other collision reactions | No dedicated clips | NOT TESTED as character motions | 3 · establish a matched tackle and fall before variants. |

## Handoff and visual limits

The Editor preview captured seven scenarios at six times for each fitted body. At the contact phase, the distance between the giving and receiving grip targets was 7.7–13.5 cm at 1.5 m actor separation and 44.7–56.3 cm at 2 m. These are staged grip distances. The Play Mode handoff cases check the actual gameplay transfer and ball ownership.

| Body | Grip gap at 1.5 m | Grip gap at 2 m |
|---|---:|---:|
| Slim | 9.4 cm | 48.4 cm |
| Athletic | 13.5 cm | 56.3 cm |
| Muscular | 11.6 cm | 54.2 cm |
| Stocky | 8.2 cm | 46.0 cm |
| Power | 11.4 cm | 54.0 cm |
| Massive | 7.7 cm | 44.7 cm |

**NOT TESTED** · Human-played match-speed visual acceptance, physical-controller feel and Windows playback on this revision. The recommended first playtest is a planted left and right cut from run speed followed by a Dive handoff on both sides of the field. Check the receiving pocket against the ball from the game camera before adjusting the exchange pose. Local XML, Editor logs, seven preview sheets and individual model captures are under `Builds/Validation/StandardFootball-MPFB/`.

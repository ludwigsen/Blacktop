# TESTING.md — Blacktop

Repeatable play-test scenarios and known limitations. Blacktop has Play Mode regression tests in `Assets/Tests/PlayMode/PlayResolutionTests.cs` for the four play-resolution fixes; the scenarios below are manual play-testing in the Unity Editor. Run the relevant scenarios after any change to the area it covers, and add a scenario when you add behavior.

Scenarios marked **(KNOWN BROKEN)** are expected to fail right now. A failure there is not a regression.

## Setup

- Unity 6.5, URP, Input System package.
- For the legacy gameplay fixtures below, open SampleScene directly unless the scenario says otherwise. For edited rosters and front-end acceptance, start from MainMenu (build index 0).
- Keep the Console open. Any new red error is a failure, even if gameplay looks fine.
- For repeatability, note the formation and play you used. Where randomness exists, repeat 3 times before calling something fixed.

**How to read each scenario:** Steps → Expected → What to watch for.

## Issue #3: Ball flight, live fumbles and ground glyphs (2026-11-06)

### T-112: Ballistic passes and spiral

Throw short and long passes from each possession direction, including a receiver
selected behind the LOS. Compare the committed destination with the diamond/cross
ground glyph. The flight has constant downward acceleration and points along its
velocity while its long axis spirals. The same Passing-owned lead/scatter/airtime
reaches the target; no animation or marker estimates a separate landing point.
Retest complete passes, failed catches/deflections, interceptions and runner-tracking
pitches. A dead incomplete/deflection never shows a live loose-ball ring or becomes
recoverable. The old human/CPU arc-height tuning has been replaced by BallController's
passGravity; speed/minimum duration/accuracy/attribute defaults are unchanged.

### T-113: Live airborne recovery and exclusive control

Force fumbles from both teams and attempt recovery before first ground contact,
after bouncing, while rolling and after settling. Both teams can recover; a fumble
is `Loose` immediately, so AllyBlocker/DefenderAI keep their existing exclusive
movement roles throughout the pop. Check a selected human defender and a CPU
carrier recovery, then the correct scorer, possession direction, Gamebreaker
lifetime and next drive. Crowded simultaneous candidates and child colliders must
produce one root-transform carrier and one canonical control outcome. Initial
release overlaps must separate or let the ball travel its configured release
clearance: no immediate attachment from the tackle volume that released the ball,
no permanent exclusion if a player chases, and no timed immunity afterward.

### T-114: Contacts, energy loss and boundary precedence

Use solid test walls/fences on the gameplay field root (never in FieldLoader's
collider-free surface/detail art). Test thin walls, players and the shared Y=0
ground plane at slow/normal/fast frame rates. The moving ball must not tunnel;
contacts redirect and lose energy, tip orientation influences the bounce, and
rolling eventually stops without persistent jitter. Both settled and airborne
fumbles stay recoverable; the scoop radius cannot reach through a solid wall/fence.
Verify configured scatter still describes first-contact distance; pop height and
gravity now determine airtime instead of the former preset fumble duration.
Preserve the existing outer boundary/safety-band policy:
an uncontained live loose-ball crossing is whistled before recovery, while forward
passes/pitches keep their existing in-flight rule. No loose/airborne ball scores
without a holder. Player wall stumbling is still #17, not implemented here.

### T-115: Marker visibility and lifecycle

Inspect 1280x720 and 1920x1080 at gameplay distance in both possession directions,
in Street Classic/After Dark, on crowded formations/routes. Yellow ring: the moving
live fumble's current ground projection, including its airborne pop. Cyan diamond
and cross: the forward pass's actual committed destination. The shapes must work
without color cues, retain their dark outlines against field markings, and stay
flat while the football rotates. Catch, recovery, deflection, whistle, game over,
reset, component disable, rematch and scene exit clear the relevant marker.
SampleScene's BallGroundMarkers exposes radius, stroke, lift and colors for tuning.

**Actual final validation:** Graphics-enabled Unity 6000.5.6f1 Play Mode:
**137 passed / 0 failed / 0 skipped**, **142.827 seconds**. All 117
previous regressions remain enabled; the 20 new BallFlightTests cover short/long
and reversed ballistic passes, constant acceleration/spiral/endpoint, airborne
and settled recovery on either team, deterministic simultaneous ownership,
canonical control, multiple colliders, release/continued-chase clearance, thin-wall
and player contact, solid-wall recovery occlusion, preserved first-contact scatter,
30/120 Hz bounce/settle, OOB precedence, dead deflection exclusion, Street pitch
tracking and marker lifecycle. Production synchronization is exercised after player
transforms move without a separate test-side physics sync. Final focused run also
passed **20/20**, **5.757 seconds**.

**Visual evidence:** Inspected staged actual-scene camera renders of the ring and
diamond/cross at 720p/1080p in both directions, plus four close football spiral poses.
This caught and replaced the first edge-on/thin LineRenderer version with outlined
flat meshes. The disposable capture driver reduced recovery radius only in its
fixture to keep the staged ball visible, and is excluded from the PR. Captures do
not establish physical-controller feel, crowded gameplay HUD acceptance, or natural
match balance. Source marker geometry and colors are independent of UI theme;
normal crowded-label/theme integration remains #12. Evidence: ignored
Builds/Validation/BallVisuals and the copied Builds/BallFlight/Visuals delivery.

**Normal macOS build/startup:** Development app build succeeded with **0 errors /
438 warnings**, **435,537,820 bytes**, **79.894 seconds**. Metal startup at 1280x720
initialized the front end and both canonical teams without a runtime exception;
the task-owned process was intentionally stopped after 12 seconds. Shutdown logged
the unchanged FrontEndBackdrop.OnDestroy active-RenderTexture release warning,
recorded separately in PROJECT_STATUS.md for #13. The app never entered a ball scene
in this bounded smoke check. Physical controller, Windows, human-played full matches,
and sustained standalone ball/gameplay performance remain unperformed acceptance.

**Reproduction/artifacts:** Run Unity with -batchmode -projectPath <checkout>
-runTests -testPlatform PlayMode -testResults <results.xml> -logFile <log> using a
graphics device (do not add -nographics). Add -testFilter BallFlightTests for focused
coverage. Final results: ignored Builds/Validation/ball-release-full.xml and .log;
focused results: ball-release-focused.xml; build summary/log and startup log use
ball-build* and ball-standalone.log. Normal app and complete-source review packet
are delivered under the original checkout's ignored Builds/BallFlight directory.
Unity-generated font/settings noise and all disposable capture/build helpers are
excluded from the change. Related-ticket follow-ups are in ISSUE_PRIORITIES.md.

## T-116: Gameplay labels, actor mapping and lifecycle (#12)

Use Head 2 Head in both human sides/possession directions at 1280x720 and 1920x1080,
Street Classic/After Dark. Observe actual routes, force a clustered formation, throw
to each numbered receiver, catch/recover, switch defenders and leave gameplay.
Expect exactly the same 1=WR1 / 2=WR2 / 3=WR3 / 4=RB mapping, correctly named cards,
one highlighted controlled identity, no duplicate receiver nameplate, and labels
outside scorebug/menu/ball bounds. Offscreen/invalid actors, post-play/huddle/game
over, disable and scene exit must hide the appropriate cards. Actor links must make
displaced cards understandable without crossing other cards or the ball. Physical
gamepad Pass/Pitch remains #13; no new binding or invented face-button prompt is
part of this change.

GameplayReadabilityTests compares actual projected RectTransform bounds (not only
layout's planned rectangles), both themes/resolutions/directions, direct queued
target mapping, catch/control changes, defender identity cycling, invalid/offscreen
actors, rebuild/disable and phase cleanup. The disposable capture driver records
the normal camera, actual early routes, staged crowded positions and defender
switching in both directions. Captures are visual evidence, not a human full match.

## T-117: Supplied six-team roster and real rating consumers

Compare the committed CSV and all canonical records: 42 unique supplied GUIDs,
names/jerseys, six teams of seven, and all 504 rating values unchanged. Every team
must be ready, selectable in Head 2 Head and present in Pickup's saved-player pool.
Source broad positions remain recorded preferences; inspect the explicit independent
starting assignments in SourceAssets/Rosters/README.md. New-team crests are optional
missing art, not another team's logo. Regenerate source from its documented command
and verify deterministic data/GUIDs without touching any runtime save.

SuppliedRosterTests compares source parity, legal lineups/highest-Passing QB, and
loads all six teams on both physical match sides. It checks all twelve raw runtime
ratings and the same private PlayerAttributes instance reaching every gameplay
consumer, plus displayed names/numbers. Real movement, committed CPU throw duration
and tackle fumble resistance compare actual supplied player profiles under fixed
conditions. Appearance and height must remain cosmetic, without a rating override.

## T-118: Existing save upgrade, conflicts and persistence

Use disposable old two-team saves. Add a custom player and edited canonical
cosmetics/lineup, then open with rosterRevision 1. New teams and official source
values appear, retired aliases remap, custom people/appearance and valid lineups
remain, and the disk file stays byte-for-byte unchanged until Save. Save/restart;
later player edits must remain edited rather than reimporting source every launch.
Introduce a custom jersey collision with the new official numbers: show the
needs-attention message and keep existing people instead of deleting/renumbering
them. Resolve in Players, Save and reload to finish the upgrade. Retest backup,
unsupported/newer/foreign/malformed data and normal CRUD/transfer/reserve behavior.
Never use the owner's real roster as a disposable test fixture.

## T-119: Blocking and D-Moves single-owner effects

These two ratings previously had no live consumers. Compare the supplied high/low
Blocking players on an otherwise identical block contact: higher Blocking shortens
AllyBlocker's recovery between contact rolls. Compare high/low D-Moves defenders
after block knockback: higher D-Moves releases the existing knockback state sooner.
Neutral values keep the existing 0.4s contact / 0.15s block recovery defaults. The
Run Power/Tackling contact contest, shove distance and ordinary/stiff-arm pushback
remain separate. Inspector base durations and existing curves/caps provide tuning;
conditions and team Gamebreaker must be applied once through PlayerAttributes.
Automated tests exercise actual contact/recovery paths at controlled 120 Hz.
For a human playtest, start Outlaws vs. Reapers. Compare Outlaws' Marcus Vaughn
(19 Blocking, 8 D-Moves) and Dante Holloway (5 Blocking, 18 D-Moves) in the same
offensive and defensive assignments on successive runs. Dante also has 20 Speed
versus Marcus's 12. Manual engagement readability and balance remain #14.

### #12 and roster verification (2026-11-06)

Unity 6000.5.6f1 on macOS: 149 of 149 committed Play Mode cases passed, with no
skips. The final roster check loaded all 42 players on both match sides. The
disposable camera run recorded 32 final views across both themes, resolutions,
directions and route/cluster/defense/cycle states, plus four pre-fix comparisons.
The source generator reproduces the committed assets byte for byte. Logs,
captures and the macOS development app are delivered in the original checkout's
ignored Builds/GameplayReadability directory. A physical-controller full match,
Windows run and human balance acceptance are follow-ups (#13 and #14).

## Core loop

### T-01: Drive start position
**Steps:** Start a game. Observe where the offense lines up. Let the possession change and observe again.
**Expected:** Drives start at -10 / +10 on the possessing team's own side, relative to its direction of attack. Never in the end zone. Both teams, both directions.
**Watch for:** Wrong side on the second possession (possession-relative direction bugs).

### T-02: QB starts with the ball
**Steps:** Reach the line of scrimmage on any play.
**Expected:** QB already holds the ball in Pistol or Shotgun. No snap animation or delay.
**Watch for:** Ball at the QB's feet or floating above the hands; the football has a fitted waist-level carry offset, but a hand-aligned carry pose remains unfinished.

### T-03: Control authority
**Steps:** Start a play; pass to a receiver; let the receiver catch it.
**Expected:** User control switches to whoever `PossessionController.ActivelyControlled` points at. Highlight moves with the carrier.
**Watch for:** User still steering the QB, two players responding to input, highlight stuck on the old carrier.

## Passing and pitching

### T-10: Complete pass
**Steps:** Pass play. Open `ReceiverSelectionUI`, pick a receiver, throw.
**Expected:** Ball travels in a parabolic arc, receiver catches, becomes carrier, user controls them.
**Watch for:** Ball passing through the receiver, catch with no state change, receiver selection not matching who receives.

### T-11: Incomplete pass
**Steps:** Throw to a covered receiver or throw away from everyone.
**Expected:** Play ends as incomplete. No possession change, no score.
**Watch for:** Ball landing in the end zone being treated as a touchdown (held-ball-only scoring regression).

### T-12: Pitch / lateral
**Steps:** Use a pitch.
**Expected:** Ball moves to the target player with the pitch arc; possession transfers cleanly.

## Routes and blocking

### T-20: Route running on pass plays
**Steps:** Call a pass play. Do not throw immediately; watch receivers.
**Expected:** Receivers run routes to completion, unless a ball carrier crosses the LOS, in which case they stop running routes.
**Watch for:** Receivers blocking mid-route, route/block conflicts, receivers standing still.

### T-21: WRs block on run plays
**Steps:** Call a run play. Hand off or run with the QB.
**Expected:** WRs block instead of running routes.
**Watch for:** WRs running routes on run plays. (**Blocking logic is marked untested.** Treat anything odd here as worth reporting rather than assumed intended. Also note: a visible handoff is not built yet.)

### T-22: Blocking effectiveness
**Steps:** Run the same run play 5 times. Observe `AllyBlocker` behavior.
**Expected:** Blockers engage defenders; results vary but blockers visibly do something.
**Watch for:** Blockers frozen, blockers walking through defenders, blockers chasing the wrong target.

## Defense and tackling

### T-30: Defender engagement
**Steps:** Run the ball and watch defenders.
**Expected:** Closest defender engages; others contain. Once the carrier is past the LOS, the override changes behavior.
**Watch for:** All defenders dogpiling, defenders ignoring the carrier, jittery switching.

### T-31: Tackle
**Steps:** Let a defender reach the carrier.
**Expected:** Tackle ends the play; `PlayState.OnPlayEnded` fires; next play sets up.
**Watch for:** Tackle that doesn't end the play, double-ending a play, stuck state after a tackle.

### T-32: Moves versus tackle
**Steps:** Use Juke, Hurdle, and StiffArm against an engaging defender, several times each.
**Expected:** Outcomes depend on attributes. Moves have visible feedback and read clearly.
**Watch for:** Moves that always win or always lose, no feedback on a missed tackle.

## Scoring and game flow

### T-40: Touchdown
**Steps:** Carry the ball across the goal line.
**Expected:** Touchdown registers once; score increases once; play ends; possession changes appropriately.
**Watch for:** Double scoring, no scoring at the goal-line edge, scoring on a caught ball that never crossed.

### T-41: Safety rule
**Steps:** Get tackled in your own end zone. Separately, get tackled outside the end zone after the ball touched it.
**Expected:** Safety only when the play *ends* in the team's own end zone. Merely touching it is not a safety.
**Watch for:** Safety from a ball that just clipped the zone.

### T-42: Win condition
**Steps:** Score until one team reaches 21.
**Expected:** The first team to reach the target wins. No win-by-2. Game ends and reports the winner.
**Watch for:** Game continuing past 21; wrong team declared winner; next possession starting after a win.

### T-43: Field lines
**Steps:** Observe LOS, first-down, and goal lines across several plays and both possessions.
**Expected:** LOS follows the ball spot. Lines are visible and flip with possession direction.
**Watch for:** Lines in the wrong place after a possession change. The first-down line reads `DownsTracker.YardsToGo`, including goal-to-go.

## Out of bounds

### T-50: Out of bounds / goal-line precedence
**Steps:** Run the ball out through the sideline. Repeat on both sides and in both directions.
**Expected:** The play ends with the ball spotted where it went out.
**Current implementation:** automatic SidelineCheck and pre-contact boundary checks share the live ball ruling position. A ball outside the legal scoring rectangle must never score, even past the target goal line.
**When fixing:** retest T-31, T-40, and T-41, since out of bounds touches the same resolution paths.

## Loose ball

### T-60: Fumble and recovery **(partially known limited)**
**Steps:** Force a fumble if possible; watch who recovers.
**Expected (target):** Nearest player recovers.
**Current implementation:** nearest eligible player recovers with an entity-ID tie-break; pursuit exists. Offense pursuit/shove recovery belongs to AllyBlocker; defense pursuit/shove recovery belongs to DefenderAI; selected defenders remain human-controlled.
**Watch for:** Wrong player recovering, recovery from far away, and possession set to the wrong team.

## Gamebreaker

### T-70: Offense and defense activation
**Steps:** In a live play hold T or gamepad X/Square with the controlled carrier to earn meter (neutral Swagger: 2.5 points/second). Moves also earn points; sacks/fumbles/interceptions earn the tackler/interceptor's team meter. At READY press G or LB/L1. Repeat while controlling a defender. A temporary full-meter test setup may call `PlayState.AddTeamPoints(teamId, 100)`; no debug fill ships on a gameplay binding.
**Expected:** Only the controlled human's team spends its meter. The existing scorebug reads ACTIVE and stays lit. Empty/partial meters and dead plays reject activation. CPU uses its own full meter on its next live opportunity, on either side. Both teams may activate. No cutscene or forced turnover.

### T-71: Possession duration and team isolation
**Steps:** Activate, then play several downs including an incompletion, first down, same-team fumble recovery and fourth-down failure. Repeat with a score, safety, interception and opponent fumble recovery. Finish a match and rematch.
**Expected:** Boost persists across ordinary downs, huddles and same-team recoveries. Both teams' active boosts end when the possession ends or a score/game over occurs. Unspent meter remains team-owned; rematch resets everything. Every teammate receives the same 1.2 multiplier before existing caps; opponents remain unchanged unless they activate too. Defense improves normal pursuit/coverage/tackle odds without guaranteeing a fumble or interception. Check both keyboard and controller in a normal standalone app; tune boost strength only after feel review.

### T-72: Issue #2 model facing
**Steps:** Play human offense and human defense with each team possessing the ball, covering both attacking directions. Make forward runs and cuts, switch defenders, pass/catch/recover, and observe AI routes, blocking, pursuit, CPU dropback, huddle/formation movement and play resets. Inspect the original and front-end body/height selections in Editor and the standalone app.
**Expected:** Visible face/chest agrees with the gameplay root's intended facing. Ordinary forward runs look forward-facing. Intentional backpedaling keeps that facing and uses Running Backward; displacement alone does not define correct orientation. The existing shared PlayerModel correction is inherited by all fourteen actors. Preserve visual centering/feet/uniform scale, capsule bounds, gameplay movement/rotation, ball ownership, camera, shared skeleton/Avatar/bind poses and disabled root motion. Idle animation or a particular local Euler angle alone does not verify running direction.
**Watch for:** A redundant 180-degree visual rotation, a scene override or appearance assembly reversing one actor, forward stride during true backpedaling, or a presentation change compensating through gameplay input/root rotation.

**Current-master revalidation (2026-11-05):** Base `937de56` already has the correct visual orientation. No runtime, prefab, scene, model, animation, input or tuning asset change is required. The unmodified graphics-enabled Unity 6000.5.6f1 Play Mode suite passes **117/117**, zero failures/skips, including existing fourteen-player prefab, locomotion, appearance, ownership and possession/reset cases.

Disposable Editor diagnostics additionally run real PlayerMovement/DefenderControl input and AI movement in SampleScene for human sides 0/1 × possession teams 0/1. Close rendered frames were inspected for forward runs, cuts, routes/blocking/pursuit, formation/huddle movement and backward presentation. Per-frame checks compare anatomical chest forward to gameplay-root forward, independently of the saved local angle, and check the shared visual hierarchy/root-motion invariant. Ownership is also cycled through the existing ball attach path for all fourteen actors; this isolates visual/control handoff and does not substitute for the existing real catch/recovery regressions.

The final focused run passes **3/3**, zero failures/skips. It verifies actual right-shoulder defender switching on both human teams and **360 combinations** of five front-end builds × twelve heights × two root directions × standard/fast/backward clips, including Animator root-position/rotation, collider, shared Avatar/controller and fixed visual-rotation checks. A deliberately added temporary 180-degree rotation is rejected as a negative control. All **25,774 actor samples** retain anatomical chest/root alignment (minimum planar dot 0.92790). Thirty representative body/clip/direction poses and live gameplay captures were visually inspected. These checks verify facing and compatibility, not production garment/deformation quality or foot-sliding polish.

Evidence is retained locally under ignored `Builds/Validation/Issue2/`: baseline.xml/log, facing-complete.xml/log, the disposable diagnostic source, CSV samples, report and rendered PNGs. The diagnostic is excluded from shipped Assets. These are scripted gameplay and Codex visual inspections. Native macOS keyboard/mouse smoke reached the match and returned to MainMenu; no owner-played visual sign-off, full human-played match, physical-controller feel, Windows or new standalone build is claimed.

## UI

### T-80: Play-call and formation
**Steps:** Open play-call UI on offense and on defense.
**Expected:** Choices reflect the current possession team. Selected formation lines up correctly.
**Watch for:** Offense and defense options swapped after a possession change.

### T-81: Main menu **(verify if built)**
**Steps:** Open the MainMenu scene; press Start New Game.
**Expected:** Loads the game scene cleanly. Both scenes are in Build Settings.
**Watch for:** Missing scene in Build Settings; game scene starting in a stale state on second load.

## Regression pass (run before committing a larger change)

1. T-01 drive start
2. T-10 complete pass, T-11 incomplete pass
3. T-21 run play, T-31 tackle
4. T-40 touchdown, T-41 safety
5. T-42 play to 21
6. Console clean throughout

## Known limitations (not bugs to fix as side effects)

- Football visuals are imported; hand-aligned carrying and action animations remain unfinished.
- No visible handoff; run plays are not fully designed.
- Downs and distance are implemented; regression-test fourth-down turnovers and goal-to-go.
- No game clock.
- Football carryOffset is fitted at waist level; fingers/hand-bone alignment remains pending carry/handoff animation.
- Blocking is untested; expect rough behavior.
- Recovery uses nearest distance plus entity-ID tie-break; exercise overlapping players.
- Loose-ball pursuit now has exclusive movement ownership; controller feel still requires manual scrum testing.
- Trick plays are not handled in route/block logic.
- Goal-line/sideline precedence fixes require corner testing in both attack directions.
- Automated cases cover the four resolution fixes; controller feel and a full match still require manual play.

## Adding a scenario

Use the same format: **ID, Steps, Expected, Watch for.** Mark it `(KNOWN BROKEN)` if it fails on purpose. Add new limitations to the list above and to `PROJECT_STATUS.md`.

## Four Must Have fixes: regression checklist (2026-10-17)

Open **Window > General > Test Runner > PlayMode**, select `PlayResolutionTests`, and Run Selected. Tests load the real SampleScene and change runtime objects only. No scene/prefab authoring changes are required. Tests deliberately arrange rare recoveries/whistles via reflection; they do not simulate human input or validate game feel.

### T-90: Held-ball scoring and competing terminal sources
**Steps:** Throw/deflect a ball into the target end zone without a catch; separately force a loose fumble there. Carry/catch a held ball across each team's target goal line. Repeat with defender contact at the line and just outside either sideline.
**Expected:** No airborne/loose touchdown. A legal held ball scores once. Goal-line contact cannot roll a fumble after a legal score. Outside the legal scoring rectangle, there is no touchdown; held/loose OOB resolves via the existing boundary rule.
**Watch for:** Wrong result depending on component Update order; score before catch; score while outside the field. Repeat with a FieldBounds test setup as well as the scene's constants fallback.

### T-91: Defensive fumble return and next drive
**Steps:** Force a live defensive recovery (use TackleContact's fumble chance temporarily in Play Mode for repeatability), return for a TD, and repeat for both teams. Include a return that reaches the winning score.
**Expected:** Recovering team gets six exactly once. Scorer's opponent starts the next drive on its own side, first-and-ten. Winning return displays the correct winner and schedules no next huddle. Interceptions still end immediately; return testing uses fumbles.
**Watch for:** Original offense credited, incorrect receiving team/direction, or next drive after game over. Runtime-only tuning changes must not be saved.

### T-92: Live recovery control and CPU direction
**Steps:** On defense, recover with the selected defender and then another teammate; hold movement input through recovery. On offense, let the CPU recover, including its QB. Switch defenders after CPU recovery. Repeat while the returner pitches laterally.
**Expected:** Human recovery becomes carrier control; previous defender locomotion stops. CPU recovery gives the human one defender and runs toward the recovering team's target end zone immediately. A recovering QB runs, never throws to the original offense. Camera/highlight follow the canonical controlled player.
**Watch for:** Two players moving from one stick, a lost highlight, frozen carrier, wrong-way CPU run, or control snapping back to the old carrier. Drive possession changes only at the whistle.

### T-93: Whistle/control loss during committed moves
**Steps:** Hurdle near a defender and end the play mid-arc through OOB or TD. On the next play, allow an ordinary tackle. Also interrupt a pass/pitch windup with a tackle and test a fumble during a hurdle.
**Expected:** Interrupted hurdle returns to its starting height and clears immunity. No interrupted pass/pitch releases after the whistle or after losing carrier control. Normal completed moves still finish normally.
**Watch for:** Floating players, a persistent immune carrier, or a ball released during post-play presentation. Repeat after a rematch to exercise fresh Awake/Start ordering.

Then run T-01, T-10/T-11, T-21/T-31, T-40/T-41 and play to 21, confirm the winner, restart, and complete another play. Keep the Console open throughout.


### Validation recorded for the initial four fixes

- Unity 6000.5.6f1 batchmode Play Mode run in an isolated copy of this repository: **20 passed, 0 failed, 0 skipped** (`PlayResolutionTests`). Includes scene/prefab loading, false-score rejection, goal-line/sideline contact precedence, correct return scorer/drive/winner, both control-update orders, CPU QB return direction, and whistle/control-loss cancellation.
- Runtime source compilation against the installed Unity assembly references passed. Existing obsolete-API/serialized-field warnings remain.
- `git diff --check` passed. No scene, prefab, Input Actions, tuning asset, or gameplay number was changed.
- Not performed: manual controller play, a full match to 21/rematch, visual HUD/animation review, Windows Editor, or a player build. The scenarios above remain required for those checks.


## Loose-ball ownership and complete-match validation

### T-94: One loose-ball movement owner
**Steps:** Force fumbles with each team on offense. Watch offensive linemen, skill players, defenders, and the selected human defender. Include a fumble during blocker/defender pushback, then recover with either team and continue the play.
**Expected:** Offense scrambles through AllyBlocker, defense through DefenderAI, and the selected defender follows input. No player receives both AI displacements. Blocker pushback finishes while the ball is loose, then pursuit resumes. Recovery/control behavior remains unchanged.
**Watch for:** Double-speed pursuit, jitter, a permanently stuck shove, or AI moving the selected defender.

### T-95: Complete match and rematch
**Steps:** Run `PlayResolutionTests` in Test Runner > PlayMode. The two CompleteMatchAndRematch cases cover both winners at the real default target (21), OOB yardage, incompletions, first downs, fourth-down turnovers, safety, scoring/possession transitions, winner overlay, stopped post-win flow, real Confirm-handler scene reload, and another down after rematch. Run the same suite in a macOS standalone test player as well.
**Expected:** Every case passes with one unique QB/RB/WR1/WR2/WR3/OL1/OL2 on each team. A standalone player loads SampleScene and uses the same rules and rematch code outside the Editor.
**Watch for:** Empty formation slots, wrong down/distance/direction, more huddles after victory, scores persisting across rematch, or player-only missing assets/shaders.
**Scope:** Automated cases arrange terminal situations and invoke movement methods/Confirm directly. They do not replace a normal controller-played match or visual inspection. Runtime acceleration is limited to test instances; shipped defaults are unchanged.

A **standalone build** is an exported game executable (macOS `.app`, Windows `.exe`) that runs without Unity Editor. For manual validation, open the normal Blacktop app, play to 21, restart, then complete another play. Check movement, input, scorebug, camera, and recovery in the built game. A test-player build additionally contains Unity's automated test runner and is separate from the normal game app.


### T-96: Standalone field/highlight shader retention
**Steps:** Build and launch the enabled SampleScene as a normal macOS player. Check Player.log and observe LOS/first-down/goal lines plus the carrier and user-control rings.
**Expected:** No `ArgumentNullException` for a missing shader. Procedural line and ring materials use the retained Universal Render Pipeline/Unlit shader, including runtime transparency variants.
**Watch for:** Missing or pink lines/rings, or repeated material-construction exceptions on scene load/rematch. Shader.Find by name alone does not keep a shader in a player build; ProjectSettings/GraphicsSettings.asset explicitly includes the package shader.


### Validation recorded for loose-ball / full-match follow-up

- Unity 6000.5.6f1 Editor Play Mode: **28 passed, 0 failed, 0 skipped** on the final scene/settings.
- macOS standalone test player: **28 passed, 0 failed, 0 skipped** on the same runtime sources, scene and graphics settings.
- The two complete-match/rematch cases ran in each environment: four scripted matches to the real default target of 21, covering both winners, plus a new live down after each shipped scene-reload rematch.
- The first standalone run failed scene setup because URP Unlit was stripped. Retaining the shader fixed the failure; the final player run above is clean of test failures. Results are saved locally in ignored `Builds/Validation/editor-playmode.xml` and `Builds/Validation/macos-player.xml`.
- `git diff --check` passed and the tested runtime source, SampleScene and GraphicsSettings match the working checkout byte for byte. No prefab or gameplay tuning defaults were changed.
- Previous four-fix Editor gameplay was verified by the owner. The new full-match cases are scripted; manual controller play of the normal app, graphics/feel review, and Windows validation remain separate checks.
- Normal macOS development build: succeeded with 0 errors and 468 warnings (primarily installed AI inference compute-shader variants). Output: `Builds/macOS/Blacktop.app`; no test-runner/NUnit assemblies are shipped in that normal app. The headless startup smoke loaded SampleScene and produced no logged runtime exceptions before being stopped intentionally. This does not establish visual quality or physical controller behavior.


## T-97: Humanoid replaces capsule visuals (2026-10-18)

**Steps:** Open SampleScene. Inspect either team's Player instances and the Player prefab. Confirm PlayerVisuals > PlayerModel > PlayerBody, enter Play Mode, call a play, move, pass/pitch, switch defenders, hurdle, and finish a play. Check the feet and model position while both teams face their attack direction.
**Expected:** Fourteen visible humanoids, no visible player capsules or detached preview character. Body rest-pose bounds align with the existing two-unit collider. Movement/contact remains rooted on Player; the invisible capsule collider stays enabled. The existing idle animation plays without moving the gameplay root. State-color feedback appears on the humanoid body. Bone hierarchy is unchanged.
**Watch for:** Floating/offset models, a capsule still visible, missing materials, root-motion drift, feedback on the wrong renderer, or the QB retaining its old visual offset. Full locomotion/football animations remain future work.
**Validation performed:** Isolated Unity asset validation passed across all fourteen scene players, and all 28 existing Editor Play Mode tests passed. No physical controller or Windows validation is claimed for this change.

Issue #2 superseded the earlier preview: gameplay exposed backwards facing on both teams. The visual body's rotation was corrected by 180 degrees relative to that committed state, with fitting offsets mirrored. Running-direction confirmation was outstanding at that historical revision; see T-72 for the current-master rendered revalidation and the remaining human/platform limits.

The normal macOS app was rebuilt successfully with the final model assets (0 errors; 440 existing build warnings). Output: `Builds/macOS/Blacktop.app`. The rendered animated front/back preview is saved locally at `Builds/Validation/PlayerModel-preview.png`; Editor test results are at `Builds/Validation/character-playmode.xml`.

The refreshed app passed a headless scene-startup smoke check with no logged runtime exceptions, then was stopped intentionally. Manual controller play, running/football animation coverage, and Windows remain unverified for this visual change.

## 2026-10-18 — Gamebreaker and issue #2 validation

- Unity 6000.5.6f1 isolated-project Editor Play Mode: **39/39 passed**.
- macOS standalone test player: **39/39 passed**. Includes the prior 28 resolution/match/rematch cases plus 11 Gamebreaker/input/prefab cases.
- New coverage: real G/LB input on offense and defense, one controlled-team activation despite fourteen listeners, underfilled/dead/duplicate/game-over rejection, all twelve team-owned attribute multipliers and caps, defensive coverage/fumble odds without guaranteed outcomes, boosts across downs through fourth-down turnover, CPU activation on both sides, fourteen inherited model/input stacks.
- Synthetic input devices explicitly route to gameplay in the headless Editor; test settings are restored. This does not substitute for a physical-controller playtest.
- Full diff review and `git diff --check` passed. Final UI design, human gameplay balance/readability, visual running-direction review and Windows validation remain manual work.
- Normal macOS development app rebuilt successfully: zero build errors, 408 reported warnings (including obsolete Unity API warnings). Headless startup loaded the match scene without logged exceptions; stopped after startup. Physical controller, graphics and gameplay feel were not checked by that smoke run.


## T-98: Scorebug composition and team configuration (2026-10-18)

**Steps:** Open SampleScene and enter Play Mode at 1920x1080, 1280x720, and a narrower/taller aspect ratio. Inspect the horizontal Kings crest/name/score/meter, center down/distance/ON field position and play clock, then mirrored Vipers meter/score/name/crest. Score a touchdown and change possession. Charge each team's meter through empty, partial, READY and ACTIVE. Confirm names/scores use Old London, soft-white faces and thick black outlines; all supporting copy uses Outfit. Inspect three-digit scores and a long team name for fitting/truncation rather than overlaps.

**Pre-game configuration:** Select Assets/_Project/Data/Teams/Kings/Identity_Kings.asset (or Vipers). Change teamName, logo, accentColor and secondaryAccentColor before entering Play Mode. Verify only that team's name/crest/paint/meter/rule/frame change, while the opponent and roster GUIDs/ratings stay unchanged. Create a new Blacktop > Team Identity asset and assign any Art/Logos sprite plus colors to PlayState's team identity fields. A missing identity or crest should show neutral Team text with no white-square logo. Reapplying branding to an active panel must preserve ACTIVE. Confirm no source font material was modified and rematch has one HUD with the same configured identities.

**Validation:** Unity 6000.5.6f1 compiled the final runtime sources in an isolated project. The existing 39 Editor Play Mode match/Gamebreaker tests passed (39/39, no failures/skips). A temporary visual QA helper verified both matching logos, Old London name/score fonts and SDF outline widths, Outfit meter labels and ACTIVE preservation on branding refresh; rendered 1920x1080, 1280x720, partial meters and READY/ACTIVE were visually inspected. Preview scores 14/17 and meter states were injected for display QA; the scene still starts 0–0. Artifacts: ignored Builds/Validation/scorebug-1920.png, scorebug-1280.png, scorebug-ready-active.png, scorebug-playmode.xml and scorebug-preview.log. The copied Editor search index emitted an internal QuickSearch indexing exception during preview startup; no HUD/gameplay exception occurred and the regression suite passed. Controller play, a new standalone build, Windows and unusual aspect ratios remain unverified.


### T-98 follow-up: larger crests and stronger lettering

**Steps:** At 1920x1080 and 1280x720, compare both teams: crests scale to 125%, with the left crest tilted +20 degrees and the right crest -20 degrees (tops lean outward). Names and scores use bold Old London, retaining soft-white faces and black outlines. Names move outward by 10% of their original name-slot width; name slots and score placement tighten together to prevent overlapping text. Check Kings and Vipers for logo/name overlap, score separation and screen-edge clipping. Center down/distance, field position and clock layout remain unchanged. HUDTheme exposes the four layout adjustments for later tuning.

**Validation:** Refreshed isolated Unity 6000.5.6f1 compilation succeeded. Inspected the rendered 1920x1080 and 1280x720 previews: mirrored outward crests, bold names/scores, tighter spacing and unchanged center panel. Both configured teams fit without obscuring the lettering. Font/logo/outline/ACTIVE-preservation QA checks still pass. Saved scorebug-revision-1920.png, scorebug-revision-1280.png, scorebug-revision-ready-active.png and scorebug-revision-preview.log under ignored Builds/Validation. The earlier 39-test gameplay result above predates this visual-only follow-up; gameplay tests were not rerun.


## T-99: Football model, scale and style isolation (2026-10-18)

**Steps:** Open SampleScene and inspect Ball > FootballVisual. Confirm the sphere renderer is disabled, root scale is one, the football has a pointed silhouette/raised laces/Blacktop stamp, and its approximate dimensions are 0.32 long by 0.19 wide plus the laces. Inspect the QB carry position, then run, pass, pitch, fumble/recover and score in both directions. Confirm the smaller football remains readable at the gameplay camera and that the ball anchor is not above the player's head. A true gripping pose is deferred with the animations.

**Styles:** On FootballAppearance, choose StreetClassic or BacklotLeather before play; in code, SelectStyle(index) chooses from Available Styles. Confirm palette changes, six shared materials remain intact, and mesh, collider bounds, carrier, ball state and ruling position do not change when selecting a style. Invalid indices must return false and retain the current style. The shipped scene defaults to Street Classic; this does not add selection UI or saved player preferences.

**Automated coverage:** FootballStylesLeavePossessionAndGameplayGeometryUntouched checks the shipped prefab/mesh dimensions, hidden sphere, no added Rigidbody, alternate style property blocks, shared-material preservation, unchanged possession/rulings/collider/mesh, invalid indices, and selection restoration.

**Validation:** Blender 5.2.1 generated the mesh, packed editable source, procedural maps and studio render. Unity 6000.5.6f1 imported the final asset and compiled both new runtime components. Final isolated Editor Play Mode suite: **40/40 passed**, zero failures/skips, including the new style-isolation case and all prior 39 gameplay cases. Visually inspected Unity renders at gameplay distance, a close carry-fit view, and both style presets. Unity material inspection exposed a side-panel slot reorder; the final prefab/FootballStyle mapping uses the verified imported order, and the stamped panel faces outward. Modified runtime/scene/art files match the tested copy byte for byte; new asset GUIDs are unique; git diff --check passed. Evidence is saved in ignored Builds/Validation/Football-hero.png, Football-unity-styles.png, Football-carry-fit.png, Football-gameplay.png, football-playmode-final.xml and football-unity-preview-final.log. The copied Editor search cache again emitted an internal QuickSearch indexing exception during preview startup; no gameplay failure occurred. Physical controller play, Windows and a new standalone build remain unverified. Carry grip and the full animation/handoff readability list remain follow-up work.


## T-100: Shared locomotion and hurdle animations (2026-10-18)

**Steps:** Enter SampleScene and inspect both teams during huddle/formation movement, live routes/blocking/pursuit, controlled offense/defense, and CPU QB dropback. Verify idle, Standard Run, Fast Run at higher actual speed and Running Backward when root displacement opposes facing. The same rig/controller must animate all fourteen players even when PlayerStateMachine is disabled for AI. Changing possession must retain animation updates. Movement/turning rates remain unchanged; fast run does not introduce a sprint input. Check both attacking directions against the previous backwards-facing issue.

**Hurdle:** Activate hurdle, then repeat with a whistle or control loss mid-arc. Jump should fit the existing 0.5-second move duration, use the gameplay root's existing arc, and return to locomotion after completion/cancellation. No second imported jump trajectory, added Rigidbody, root-motion movement, lingering jump pose or immunity. Resets/teleports should not briefly trigger a full-speed run.

**Automated scenarios:** PlayerAnimationTests covers all fourteen shared Humanoid stacks; standard/fast/backward animation selection on every actor; root-motion isolation; teleports, vertical-motion filtering and reenable; hurdle rate, imported upward-body-motion isolation and whistle cancellation. Existing match/Gamebreaker/football-style cases remain in the same run.

**Validation:** Unity 6000.5.6f1 isolated Editor Play Mode: **44/44 passed**, zero failures/skips (the prior 40 plus four animation cases). All fourteen actors were exercised at standard/fast/backward speeds with root-position and pelvis-grounding assertions. The imported clips retain Original Y to match the existing fitted body/idle; Jump extracts its trajectory using Original, keeping its body center stable while legs articulate. Root motion remains off. Rendered current-bone poses were inspected for backward, idle, standard/fast run and five jump phases; temporary QA bakes avoid reusing a GPU skinning cache between captures in the same Editor frame. Final runtime/controller/prefab/import files match the tested copy byte for byte; git diff --check is clean. Evidence: ignored Builds/Validation/player-animation-playmode.xml, Player-locomotion-poses.png, Player-hurdle-poses.png and player-animation-preview.log. The copied Editor search cache emitted its existing QuickSearch indexing exception during preview startup; no gameplay test failed. Physical controller feel, Windows and a new standalone build remain unverified.

## T-101: Modular player appearances (2026-10-22)

**Automated:** PlayerAppearanceTests loads the actual match and verifies all fourteen assemblers, shared Avatar/materials, all sixty height/build data combinations with unchanged root/collider/ratings/GUID/possession/bounds, saved-ID reconstruction after catalog reorder, repeated assembly reuse, missing/duplicate/incompatible selection retention, changed bind-pose rejection, skinned/rigid attachment fixtures using live bones, authored LOD mapping, team property-block refresh and roster selection on a newly instantiated player. Attachment fixtures reuse existing mesh assets only for tests; they are not hairstyle art.

**Actual result:** Unity 6000.5.6f1 isolated Editor Play Mode, final suite **62/62 passed, 0 failed, 0 skipped**, including existing gameplay/animation and merged field tests. Results are saved in ignored Builds/Validation/character-foundation-playmode.xml. This is Editor validation, not a Windows or new standalone result.

**Preview:** Open Blacktop > Characters > Appearance Preview. Select each supplied 170/183/201cm appearance, preview idle/backward/standard/fast run and team/number choices, then inspect at match camera distance. Placeholders must be labelled. Change a roster row's appearance and spawn; check number/nameplate, team, input, possession, contact and OOB in both attacking directions. Restore/save authoring choices deliberately. Invalid selections must retain a safe visual and useful diagnostics. Check minimum/maximum-height feet, shoulders, hips, elbows, knees, neck/head and attachment alignment through transitions.

**Art-dependent checks still required:** Three genuinely different bald faces on the same physique; all five actual bodies at every height; skin-tone seams; varied scalp/jaw/head fits; 3–5% head enlargement; garments and visible jersey numbers; LOD silhouette/pop/culling; walk, cut, throw, catch, block, tackle and celebration deformation once clips/art exist. Current controller has idle/run/backward/fast/Jump only. These are not marked passed by the data/fixture tests.

**Performance procedure:** Profile fourteen players in a normal build on macOS and Windows at match distance. Record draw calls/batches, skinning/Animator time, managed allocation peaks, memory and material instances. Compare the same scene/camera/graphics settings, first load separately from repeated selection, and an identical physique/appearance mix. A single Editor timing or render-count comparison does not establish a performance budget. No runtime material cloning or per-character Update loop is added by the foundation.

**Visual/performance evidence:** Native URP previews were inspected for common-pose 170/183/201cm grounding and fourteen running players. Character geometry/material counts stay 70 skinned renderers, 268,590 vertices and two distinct shared materials; unchanged unique mesh memory is 4,286,668 bytes. A 1600×900 whole-scene capture reported 287 draw calls (includes field/shadow/scene passes; no baseline draw-call comparison). Fourteen repeated validated TryApply calls took one measured 25.790ms sample. Batch ProfilerRecorder draw/batch/skinning counters were unavailable and the animation marker reported zero, so no frame-time or skinning-budget result is claimed. Full measurement context is in MODULAR_CHARACTERS.md. Evidence: ignored Builds/Validation/Player-three-heights.png, Player-fourteen-characters.png, character-foundation-visuals.log and character-foundation-audit.txt. The copied Editor cache emitted its known QuickSearch indexing exception during preview startup; gameplay tests were unaffected.

## T-102: Main menu, roster authoring and Quick Play (2026-10-23)

**End-to-end acceptance:** Start MainMenu. Players → Kings → Create Player. Name the player, choose a free jersey number, select 6'3" / Thick, change skin/face/hair/gear selections and all twelve ratings. Confirm missing art is labelled and does not claim a different face/style. Save; verify the selected roster row and reserve count. Assign RB and an independent defense slot; confirm the replaced player moves to reserves. New Game → explicitly choose Kings and Vipers → choose side/venue → Start. Verify fourteen players, saved identity/nameplate/number, appearance and private runtime ratings. Repeat with the human on the other side and reversed teams. Play a match/rematch; Escape / Start / MENU returns to MainMenu. Exercise normal movement, passing/catching, blocking, tackling, scoring, huddles and post-play control using the edited player.

**Data/transaction checks:** Create an eighth and more reserve players. Edit preserves GUID; duplicate creates one only on Save; transfer preserves it and removes the old membership/active reference; delete confirms and removes lineup references. Duplicate names are valid; duplicate numbers on one team fail visibly. Move an active player to reserves and verify Quick Play blocks with a route to roster management. Independently reassign defense slots. Validation or unwritable-save errors must leave memory and the primary save intact. Cancel dirty changes, reset the draft, and reject discard once to confirm the original remains editable.

**Persistence/source checks:** Restart from a temporary save store and verify all records/lineups/selections. Corrupt the primary and recover the backup; unsupported versions use defaults with a message. Restore defaults deliberately. In Editor, save then confirm COMMIT TO SOURCE and inspect TeamRoster, TeamDefinition and per-GUID appearance assets in Git. Ordinary Save/preview/Cancel must not change them. Source commit automated coverage uses copied fixture assets and cleans them up; never use real developer roster files as a disposable test. See FRONT_END.md for direct SampleScene binding limitations after canonical membership changes.

**Input/visual checks:** Inspect 1920x1080 and 1280x720. Use pointer, keyboard and gamepad navigation; verify selected shape markers, category indicator, focus auto-scroll and modal focus. Change height/build and inspect Heavy versus Massive; test all twelve heights and each body, full/head framing, drag/rotate/zoom/reset, idle/run/backward/fast-run and repeated screen teardown. Preview must never add a TeamMember or remain after leaving its screen. Temporary shell garments have rough edges; production seam/animation/LOD fit remains an art acceptance task.

**Actual automated result:** Graphics-enabled isolated Unity 6000.5.6f1 Editor Play Mode: **71 passed, 0 failed, 0 skipped**, 28.387 seconds in the final run. Includes all 62 prior gameplay/animation/field/appearance cases plus nine FrontEndTests. Real UI controls create/save/activate the new Thick 6'3" player and launch SampleScene; assertions verify all twelve ratings, nameplate, unchanged root/capsule, live ball ownership and canonical control handoff. Other new cases cover CRUD/lineups/reserves, invalid data/I/O isolation, backup/default recovery, five real distinct temporary meshes/weight combinations, both human sides/directions/venue/rematch/menu return, cancel/preview cleanup, isolated source commit and virtual gamepad Submit/Back. Test devices explicitly route input and restore settings; this is not a physical-controller playtest.

**Rendered/performance result:** Native URP main menu, roster, physique, face, gear, attributes, confirmation, selected match and five-body comparison were inspected. Main menu/editor also inspected at 720p. The fourteen-player selected sample has 112 skinned renderers, 400,176 vertices, 12 shared meshes and 2 shared materials, versus the legacy direct-scene 70 / 268,590 baseline. Added garment shells increase skinning geometry; no new material instances are used for team colors. One final SaveCurrent sample on the default fourteen-player roster was 27.147ms, after catalog-validation duplication was removed (initial sample 339.035ms). These are Editor samples, not a frame-rate/skinning/allocation or platform budget. Profile normal builds with production art/LODs later.

**Standalone:** A normal macOS development app containing MainMenu and SampleScene built successfully with 0 errors and 408 reported warnings. A graphics-enabled Metal startup smoke run loaded MainMenu with two canonical teams and no logged runtime exception, then was intentionally stopped. Build output is ignored `Builds/macOS/Blacktop-Frontend.app`; validation files and rendered screenshots are under ignored `Builds/Validation/frontend-*` / `Blacktop-*.png`. This does not claim a human-played standalone match, Windows or physical controller feel.

**Environment limits:** The initial `-nographics` run passed 67/71 but could not create preview render textures in four UI cases. The final suite used a graphics device and passed all 71. Visual QA's copied Editor cache emitted its pre-existing internal QuickSearch indexing exception; no final product test failed. Build shader/obsolete API warnings and an optional Unity Cloud Diagnostics symbol-upload credential warning do not invalidate the successful app. Missing production faces/hair/beards/additional gear, visible garment numbers, action-animation deformation, prolonged standalone play and Windows acceptance remain outstanding.

## T-103: Editable Blender character sources and geometry round trip

**Editing workflow:** Open SourceAssets/Characters/BlacktopCharacters.blend, choose a build collection, edit body/integrated face/clothing/eyes in Edit or Sculpt Mode, save, run export_meshes.py, then apply the JSON through Blacktop > Characters > Blender > Apply Blender Mesh Export. Verify the corresponding mesh updates in the runtime player preview and Quick Play. Keep one authoritative source per asset: the combined library, individual build files and head-focused file are independent snapshots targeting the same mesh IDs. Do not export an older snapshot over newer art accidentally. Palette/material configuration stays in Unity.

**Source fidelity:** Blender 5.2.1 LTS reopened all seven .blend files. Across their 69 mesh objects, vertex positions match the Unity reference seed exactly (maximum position error 0); skin weights, vertex/triangle counts and 58-node rest-rig availability are checked. Body skins use the original 57-bone map; eye skins use Head. Real saved body/head sources were rendered and inspected. Distinct face/hair/beard art is not supplied by a format conversion; the current head remains integrated.

**Actual edit/import:** The combined file exported all 24 meshes. A real 0.0123m vertex move in the Athletic file exported and applied to a disposable Unity project. All 24 round trips preserve triangle geometry, mesh GUIDs and bind poses. Source export rejects changed object transforms/posed rig. Unity validation rejects invalid weights, wrong GUIDs, arbitrary paths, empty topology and renamed bones, including an invalid later row after staging a valid earlier row; no partial asset write occurs. UV/normal seams may introduce duplicate vertices while preserving triangle surface geometry. The validation driver lives outside Assets and must be copied into Assets/Editor only in a disposable checkout because it exercises actual asset writes.

**Regression result:** With pristine body/garment data and the new editable-eye references installed, graphics-enabled Unity 6000.5.6f1 Editor Play Mode: **71 passed, 0 failed, 0 skipped**, 28.821 seconds. No new package/assembly dependency, movement/controller/collider change or FBX import change. The four eye surfaces have identical initial geometry/bind poses and retain their existing bones/materials through PlayerModel mesh overrides. Existing scene/model/roster tests pass. This follow-up does not claim a new standalone build or Windows/physical-controller validation.

**Evidence/reproduction:** SourceAssets/Characters/README.md contains commands. validate_sources.py checks initial seed fidelity, export rejection and previews without saving its test sculpt over the .blend. CharacterBlenderRoundTripQA.cs checks the Unity application path in an isolated project. Evidence is retained under ignored Builds/Validation as character-blender-validation.json, character-unity-roundtrip.txt, character-blender-playmode.xml, character-blender-roundtrip.log and Character-blender-body/face.png. Newly sculpted production sources will intentionally differ from the initial seed, so seed-equality checks apply only to conversion, not future artistic edits. Rebuild protection is checked separately: without --overwrite, build_sources.py refuses to replace any existing .blend sources.


## T-104–T-107: Head 2 Head, Pickup, conditions and difficulty

**Automated suite:** `ExhibitionTests` uses disposable roster saves and seeded local randomness. It covers valid Head 2 Head on both sides; same-team/short-roster rejection; mixed-source fourteen-player Pickup; saved reserves/filtering/sorting; duplicate/eighth picks and insufficient pools; independent offense/defense validation/swaps; source/disk/restart isolation; legal seeded Random; every condition, severity and Clear; Accel/Agility separation and one field/Gamebreaker application; real DefenderAI pursuit and released-pass reactions plus CPUPlayCaller decisions; Normal baseline and profile freezing; both modes/sides through scene reload, menu clearing and a subsequent different mode; and real Pickup menu controls through draft/lineup/review/launch. Existing FrontEndTests now follows New Game → mode selection → Head 2 Head → review; all existing regression suites remain enabled.

**T-104 / modes and lifecycle:** From MainMenu, launch Head 2 Head with two saved teams, then Pickup with four players from one team and three from another. Test both home and away. Change every field/condition/severity/difficulty option, including Random teams and side. Verify the final review's concrete teams/settings, all fourteen unique GUIDs, explicit independent slots, names/numbers/appearance, scorebug, tint and possession identity. Complete to 21, rematch, and confirm the exact same draft/CPU assignments/resolved settings. Return to MainMenu, start the other mode, and restart the app. Saved rosters must remain byte-for-byte unchanged by matches.

**T-105 / draft and navigation:** Test mouse, keyboard and physical gamepad on New Game, team cards, search/filter/sort, all seven draft/remove/replace slots, the preview rotate/reset controls, scrolling human/CPU lineup overrides, shared settings, review and Back. Verify focus/disabled states and 1920×1080 / 1280×720 readability. With fewer than fourteen unique saved players, show an actionable block and Manage Players. Invalid Head 2 Head lineups must offer roster management without replacement characters. Preview must never register gameplay actors or survive teardown.

**T-106 / conditions:** Compare Clear and each non-Clear condition on both teams at Clean/Regular/Hard. Check effective movement speed, acceleration, Agility moves, RunPower contact and Carrying fumble resistance against FRONT_END.md's equation. Activate Gamebreaker and verify only one boost and one condition addition, bounded by existing hard caps. Slick acceleration must be -5/-10/-18% from its owned base curve, without a second Agility penalty. Confirm AI routes, blockers, designed exchanges and CPU carriers share the attribute path. Clear ignores severity. Returning to menu and a new Clear match removes the prior effects; direct SampleScene retains its legacy baseline.

**T-107 / difficulty:** Compare Easy/Normal/Hard/Street Legend with identical saved ratings/conditions. Observe delayed Easy pursuit, reaction to actual throws, coverage cushion/zone choices, observed-motion pursuit lead, CPU QB read timing, avoidance and situational play choices. Human input must remain unchanged; no tier gains speed, guaranteed turnovers or score bonuses. Normal keeps prior decision multipliers. Draft ranking uses only saved ratings. Physical-controller feel, balance, Windows and normal standalone play are separate manual acceptance checks.

**Actual Editor result (2026-10-24):** Graphics-enabled Unity 6000.5.6f1 batchmode Play Mode: **97 passed, 0 failed, 0 skipped**, 47.014 seconds in the final run. All 71 prior regressions remain green; 26 exhibition cases cover the new contracts, real CPU consumers, source/save isolation, both human sides, full matches to the actual target of 21 and the shipped winner-confirm rematch. Virtual gamepad Submit/Back enters both modes; pointer callbacks draft and launch Pickup through actual UI. Native rendered mode/pool/seven-slot/lineup/settings/review screens were inspected at 1920×1080, fixing the drafted marker glyph and pinning review actions and fitting PICKUP HOME/AWAY scorebug names. Results/logs/screenshots are in ignored Builds/Validation (exhibition-playmode.xml, exhibition-playmode.log, Exhibition-*.png). No physical-controller, Windows, 720p acceptance or human-played standalone match is claimed.


**Normal macOS build/startup:** Final development build succeeded with **0 errors / 438 warnings** (mostly existing shader/obsolete-API warnings), 13.917 seconds using the warm build cache. A graphics-enabled Metal startup at 1280×720 loaded MainMenu with the two canonical teams and no logged runtime exception, then was intentionally stopped. Output: ignored `Builds/macOS/Blacktop-Exhibition.app`; logs/summary retained as `Builds/Validation/exhibition-build.*` and `exhibition-startup.log`. This is a normal app build/startup smoke check, not a standalone test-player run or a human-played match. Physical controller, 720p visual acceptance, Windows and gameplay balance remain manual. Disposable build driver, performance-test resources and Unity import/build asset noise were removed from the change.


## T-108: Main menu and fixed-page player lab prototype (2026-11-02)

1. Open Assets/Scenes/MainMenu.unity. Verify centered Blacktop Pro Duo title, Barlow Condensed entries, and exactly New Game / Edit Players / Settings / Quit. Observe all eleven supplied 3840x2160 field images over a full cycle, including Warehouse returning to Alley. The Inspector defaults are a six-second hold and 2.4-second blend; crop must cover without stretching. Open and close submenus; background blur eases in and out while text stays sharp. Repeat this in the actual standalone overlay canvas: temporary ScreenSpaceCamera evidence does not validate additional shader-pass behavior in ScreenSpaceOverlay. The visible UI material must not contain the offline Gaussian filter pass. Rapid navigation must not leave extra preview cameras or active old controls.
2. Enter Edit Players. Verify left player / right twelve ratings / bottom roster at 1920x1080 and 1280x720. All twelve 0–20 ratings must be visible without scrolling, including zero and twenty. Attributes accepts mouse dragging and keyboard/gamepad slider steps. Summary is read-only. Cold-to-hot square color supplements the numeric value. Test every Fit & Body tab at both resolutions. Page reserves with Previous 7 / Next 7; change teams; selected identity is GUID-based.
3. Change a rating, body, height, explicit weight, face/skin, hair, fit and accessory selection. Saved changes must survive a restart and reach Head 2 Head / Pickup; unsaved changes must not reach the service. Back to Summary retains a draft; switching players/teams or leaving offers discard. Invalid weight/jersey text blocks Save and requires correction or explicit text discard before changing pages. Zero weight metadata means derive from build/height; explicit bounds are 120–450 lb. Changing build or height in derived mode must refresh both the weight textbox and summary without creating an override; invalid text must remain visible until corrected or discarded. Weight must not change ratings/capsule/shared skeleton.
4. Set allowDevelopmentEditing=false in the settings asset (or run a non-development build). Ratings and identity/physique authoring must be locked; cosmetic fit choices stay available. No in-game setting enables authoring. Restore the asset after the check. The development build intentionally includes full authoring for the owner.
5. Settings SFX: hear a quiet click, set zero and verify silence, restart and verify persistence, then restore the desired level. Version must come from Application.version. Quit cancel returns focus; confirm exits the standalone app (or stops Editor Play Mode).
6. Continue through both existing exhibition modes, launch a match, return to MainMenu and rematch. Original roster GUIDs, independent lineups, match snapshots and player-count rules must remain intact.

MenuPrototypeTests uses unique temporary roster saves, native canvas renders, access-gate checks, Save/Discard/persistence coverage and 1080p/720p control bounds. Existing FrontEndTests and ExhibitionTests cover editor CRUD and match binding. These automated checks do not establish physical-controller feel, Windows behavior, production art or a human-played full match. Use Blacktop > Front End > Build Menu Prototype (macOS) for the owner-test development app. Build output and rendered evidence belong under ignored Builds/.

**Captured validation:** Unity 6000.5.6f1 graphics-enabled batchmode Play Mode full regression: **102 passed, 0 failed, 0 skipped**, 61.747 seconds. After the final SFX percentage/height and blur-downsampling adjustments, MenuPrototypeTests + FrontEndTests: **14 passed, 0 failed, 0 skipped**, 24.787 seconds. Native renders of Main, Settings and every lab page were inspected at 1920x1080 / 1280x720. The first capture helper excluded the UI layer and was corrected before visual inspection; the final helper rejects blank captures. macOS development build succeeded (352,128,663 bytes reported); the built executable initialized the front end and Metal device without a runtime error in a bounded startup smoke check. The Mac was locked, so visible standalone mouse/controller interaction was not performed. Evidence: ignored Builds/Validation/blacktop-menu-all.xml, blacktop-menu-final.xml, associated logs, blacktop-menu-build.log, blacktop-menu-standalone.log and Menu-*.png. The development app and ZIP are under Builds/MenuPrototype/. Windows, physical-controller feel and human-played standalone matches remain unperformed.


**Unlocked standalone follow-up (2026-11-02):** Real mouse/keyboard checks at 1280x720 found an overlay-only rendering problem: the visible material also drew its opaque Gaussian filter pass, overriding submenu blur and crossfade alpha. The filter now has its own explicitly referenced shader. The rebuilt overlay visibly blurs submenus/dialogs and restores sharp fields on Main. Native editing also exposed a stale derived-weight textbox after changing build; Refresh now updates it without creating an override or erasing invalid text. The existing persistence test includes a real Build picker click and checks that textbox/derived mode.

Final graphics-enabled full Play Mode regression after both corrections: **102 passed, 0 failed, 0 skipped**, 66.377 seconds. The final macOS development build succeeded. Actual UI checks exercised mouse rating changes, unsaved-discard protection, roster selection, body preview changes, face/fit pages, keyboard Back/Submit, SFX 65% -> 0% -> 65%, persistence of 65% after restart, Head 2 Head team/settings/review/start into gameplay and Escape return to Main, Pickup pool entry/Back, and confirmed Quit. The final build was reopened and both weight readouts changed from 190 to 220 with Athletic -> Thick; Discard restored 190/Athletic. No roster test changes were saved. The final app remains open on Main. Native screenshots are in the chat's computer-use record; Player logs and final test/build logs are under Builds/Validation/blacktop-menu-native-*. Physical controller, Windows, audible sound quality and a full human-played match remain unperformed. At 720p, overlapping gameplay nameplates/pass prompts were recorded in PROJECT_STATUS.md for the separate HUD readability pass.


**4K backdrop replacement (2026-11-03):** The slideshow now uses the eleven supplied cinematic PNGs in the order documented in Art/MainMenu/README.md. Verify every image appears over one complete loop, the last returns to Alley, aspect ratios stay intact, and transitions remain continuous. Open Settings or Player Lab during rotation: the new image must crossfade while blurred, then return to a sharp field on Main. The existing MenuPrototypeTests also checks all eleven imports retain 3840x2160. Focused graphics-enabled MenuPrototypeTests: **5 passed, 0 failed, 0 skipped** (16.347 seconds), including all eleven 3840x2160 texture imports. All eleven project PNG SHA-256 hashes match the supplied originals and the serialized GUIDs match the documented order. The macOS development build succeeded (434,981,423 bytes reported). Native 1280x720 checks showed Alley on Main, a crossfade with Downtown, blurred Underpass in Settings, and sharp Shipyard after Escape returned to Main. The menu continues rotating while a submenu is open. No roster or SFX edits were saved. The app is left on Main. Logs, XML, source hashes and 1080p Main/Settings renders are under ignored Builds/Validation/blacktop-menu-4k-* and Menu-4K-*.png; native screenshots are in the chat computer-use record. This image-only follow-up did not repeat the prior full 102-test gameplay run, Windows or physical-controller checks.


## T-109: UI themes, contrast and persistence (2026-11-04)

Start MainMenu with no Blacktop.UITheme preference: Street Classic is the default.
Settings → UI THEME selects After Dark using mouse, keyboard arrows/Submit and a
controller. Open elements must recolor immediately; return to menus, enter gameplay,
restart the app, and verify the selection persists. Repeat Street Classic. Bright
selection/pressed surfaces use dark ink text and a shape/focus marker. Team crests,
team accents, ratings, GUIDs, saves, input mappings and gameplay values stay unchanged.
Repeatedly enter/leave pages and switch themes; no stale listeners or missing fonts.

UIThemeTests uses disposable roster saves and restores the original theme preference.
It exercises the actual Settings picker, immediate color/contrast roles, preference
reload, newly built screens and source/team/roster isolation. Native Barlow font
assignments are checked. The four optional mask slots are intentionally unassigned.

## T-110: Logo safe margin, editor accordions and rating tiers

At 1280x720, 1920x1080, 2560x1440 and 3840x2160, open Settings, New Game, both team
screens, Pickup, roster tools and each Player Lab page in both themes. Confirm the
Blacktop glyphs have clear space above them, the header/content do not collide,
fixed controls are on screen, text stays legible and panel geometry stays crisp.
The tunable submenuLogoTopPadding default is 54 reference pixels. Automated checks
measure visible glyph bounds with at least 20 scaled pixels of top clearance.

Summary/Attributes remain fixed. Fit/Body and developer Create/Edit use a vertical
accordion; scroll its form with the mouse and move controller focus to the last
header. Focus must reveal the entry while preview, save/discard and roster strip
stay fixed. Rotate/zoom the model; edit a real field, save, restart, and discard a
second edit. Existing save/access/derived-weight tests remain active. This supersedes
the historical T-108 requirement that customization never scroll; the fixed summary
and rating panes still do not scroll.

All 12 attributes retain exact numbers and twenty discrete cells. At 0,4,5,8,9,12,
13,16,17,20 check the precise filled count and constant tier color across both themes:
red/orange/yellow/blue/light-blue. No green/purple/gradient. Appearance/ratings stay
independent. Native captures and control/glyph bounds are checked at all four sizes.

## T-111: Play selection, clean scorebug and announcement lifecycle

Use both human sides through Head 2 Head and Pickup. Verify ALL/PASS/RUN/TRICK and
ALL/BLITZ/ZONE/MAN categories, category/play keyboard/controller navigation, mouse
selection and confirmation, crisp authored schematics, selected border and selection
marker. Empty authored categories stay empty; no play data is manufactured. The
custom Graphic must have CanvasRenderer and a nonempty mesh (regression for the
initial blank schematic). Schematics illustrate authored routes/jobs, not exact
execution trajectories or unavailable football animations.

Check team crest orientation, original independent team accents/meters, names,
scores, play clock, downs/distance/field spot and possession marker. No distressed
scorebug sprite should cover important text. Trigger touchdown, first down, turnover,
Gamebreaker and game over through normal gameplay. Existing post-play delay, AI
reactions, input handoff, huddle, rematch and return-to-menu must remain unchanged.
Both themes get the same compact geometry with palette-specific surfaces/accents.

Actual final graphics-enabled Unity 6000.5.6f1 full Play Mode run: **117 passed,
0 failed, 0 skipped**, **140.779226 seconds**. This includes a resolved-play test that
scores a real held-ball touchdown and earns a real first down in both themes,
ensuring the receiving-drive reset cannot relabel a touchdown. Earlier focused
UIThemeTests + MenuPrototypeTests passed 19/19. Final evidence: ignored
Builds/Validation/ui-release.xml and ui-release.log.

Both themes have native canvas PNGs at 720p/1080p/1440p/4K; representative screens
were rendered and inspected. Event-treatment captures are component visual fixtures;
full existing gameplay regressions separately validate the event flow. They are not
claims of a human-played match or physical-controller acceptance. Windows and a
human-played/controller match remain unperformed. Legacy gameplay nameplate overlap
and provisional character/play/venue art remain recorded limitations.


Final macOS development build: **0 errors / 408 reported warnings**, 435,525,615
bytes reported (the earlier build reported 438 warnings). Actual standalone native
checks exercised keyboard and mouse navigation, immediate Street Classic/After Dark
switching, background cycling/blur, secondary-logo clearance, and After Dark retained
after a full app restart. Street Classic was restored and the app left on Main.
No runtime exception was found in the captured startup/settings Player log. No roster
edits were saved. This does not claim a physical-controller or human-played full match.
Build and native evidence: ignored Builds/Validation/ui-release-build.log and
ui-native-player.log; screenshots are in the chat computer-use record.

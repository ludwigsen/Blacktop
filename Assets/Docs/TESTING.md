# TESTING.md — Blacktop

Repeatable play-test scenarios and known limitations. Blacktop has Play Mode regression tests in `Assets/Tests/PlayMode/PlayResolutionTests.cs` for the four play-resolution fixes; the scenarios below are manual play-testing in the Unity Editor. Run the relevant scenarios after any change to the area it covers, and add a scenario when you add behavior.

Scenarios marked **(KNOWN BROKEN)** are expected to fail right now. A failure there is not a regression.

## Setup

- Unity 6.5, URP, Input System package.
- Open the game scene (not MainMenu) and press Play, unless the scenario says otherwise.
- Keep the Console open. Any new red error is a failure, even if gameplay looks fine.
- For repeatability, note the formation and play you used. Where randomness exists, repeat 3 times before calling something fixed.

**How to read each scenario:** Steps → Expected → What to watch for.

## Core loop

### T-01: Drive start position
**Steps:** Start a game. Observe where the offense lines up. Let the possession change and observe again.
**Expected:** Drives start at -10 / +10 on the possessing team's own side, relative to its direction of attack. Never in the end zone. Both teams, both directions.
**Watch for:** Wrong side on the second possession (possession-relative direction bugs).

### T-02: QB starts with the ball
**Steps:** Reach the line of scrimmage on any play.
**Expected:** QB already holds the ball in Pistol or Shotgun. No snap animation or delay.
**Watch for:** Ball at the QB's feet, ball offset wrong (`carryOffset` is untuned, so mild offset is a known limitation).

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

### T-70: Meter and activation
**Steps:** Build the meter (`GamebreakerHUD` should reflect it). Activate it when full.
**Expected:** Activation is player-triggered (no cutscene). A flat boost applies for the rest of the possession and clears when the play ends per the possession rule.
**Watch for:** Boost persisting after possession changes; HUD not matching controller state; stacking weirdly with field modifiers.
**Note:** Defensive equivalent is an open design question; nothing to test there yet.

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

- Ball is a sphere placeholder; the football mesh isn't imported.
- No visible handoff; run plays are not fully designed.
- Downs and distance are implemented; regression-test fourth-down turnovers and goal-to-go.
- No game clock.
- `carryOffset` on `BallController` is untuned.
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

The imported body faces local -Z; PlayerModel rotates only its visual child 180 degrees to face the gameplay root's +Z. The animated front/back preview confirmed the corrected facing and existing idle pose.

The normal macOS app was rebuilt successfully with the final model assets (0 errors; 440 existing build warnings). Output: `Builds/macOS/Blacktop.app`. The rendered animated front/back preview is saved locally at `Builds/Validation/PlayerModel-preview.png`; Editor test results are at `Builds/Validation/character-playmode.xml`.

The refreshed app passed a headless scene-startup smoke check with no logged runtime exceptions, then was stopped intentionally. Manual controller play, running/football animation coverage, and Windows remain unverified for this visual change.

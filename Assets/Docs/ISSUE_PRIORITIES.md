# Open issue impact ranking — 2026-11-06

Based on the ten open GitHub issues and current master `03cc7fc`. Impact means
effect on the playable match, readability/control, and dependencies unlocked;
it is not a ranking by implementation effort. Validation tickets describe missing
evidence, not reproduced bugs. No issue labels or scope were changed by this audit.

| Rank | Issue | Triage | Impact and reason |
|---|---|---|---|
| 1 | [#3 — Ball flight, fumbles, markers](https://github.com/ludwigsen/Blacktop/issues/3) | Must Have | The ball is the focal point of every play. Preset fumbles stop at an endpoint, cannot be recovered in the air, and ignore the environment; the missing markers obscure the next action. Fixes the shared foundation before animation and scrum acceptance. |
| 2 | [#12 — Nameplate/receiver prompt overlap](https://github.com/ludwigsen/Blacktop/issues/12) | Must Have | Overlapping labels at 720p obscure actionable receivers and controlled-player identity throughout a match. Directly affects decisions and readability. |
| 3 | [#10 — Pre-snap actions/previews](https://github.com/ludwigsen/Blacktop/issues/10) | Must Have | Adds defensive agency and exposes the called assignments before play. Broad control/phase integration, but the existing match can run without it. |
| 4 | [#11 — Football action animations/grip/handoffs](https://github.com/ludwigsen/Blacktop/issues/11) | Must Have | Makes transfers, throws, catches and hits understandable and gives the game its football identity. Must observe the finalized ball/ownership pipeline. |
| 5 | [#13 — Full-match controller/platform acceptance](https://github.com/ludwigsen/Blacktop/issues/13) | Must Have | Highest remaining release-confidence task. Physical input, human matches and Windows behavior cannot be established by automated tests. Run after these core changes; do not close from automation alone. |
| 6 | [#14 — Blocking/Gamebreaker/conditions/CPU/scrum validation](https://github.com/ludwigsen/Blacktop/issues/14) | Should Have | Existing systems need observed feel/readability evidence. Its scrum checks should use the new ball behavior; this is not authorization to retune unrelated systems. |
| 7 | [#15 — Shared-rig character art slice](https://github.com/ludwigsen/Blacktop/issues/15) | Should Have | High identity/presentation value, but prototype bodies already permit the whole game loop. Validate the slice against football actions rather than expanding the unfinished catalog. |
| 8 | [#17 — Wall/fence stumble hazards](https://github.com/ludwigsen/Blacktop/issues/17) | Later | Adds meaningful venue interaction and personality, but needs authored hazard colliders and player-move integration. Ball-wall response does not implement player stumbling. |
| 9 | [#18 — Authored playbook expansion](https://github.com/ludwigsen/Blacktop/issues/18) | Later | Adds strategic variety after existing assignments, controls and presentation are readable. Unsupported trick mechanics remain dependencies, not filler content. |
| 10 | [#16 — Saved football style picker](https://github.com/ludwigsen/Blacktop/issues/16) | Later | Useful cosmetic choice with the smallest effect on playable decisions; the style API and two assets already exist. |

## Immediate follow-up areas after #3

- **#12:** Inspect both ground glyphs with crowded nameplates, receiver prompts and
  scorebug at 720p/1080p in both themes. These world markers do not solve label overlap.
- **#14:** Recheck airborne/ground scrums, release-contact clearance, both teams'
  pursuit and recovery/control transfer. A fumble is now `Loose` immediately, so the
  existing exclusive AllyBlocker/DefenderAI movement owners engage during the pop.
- **#17:** Author solid wall/fence colliders on the gameplay field root and register
  boundary walls as appropriate. Venue surface/detail children must stay collider-free.
  Free-ball sweeps consume those colliders; player stumble behavior remains separate.
- **#11:** Hand-grip/transfer presentation must preserve `RulingPosition` and derive
  release from live ownership. Spiral/tumble is visual rotation, not a second trajectory
  or animation-driven possession authority.
- **#13:** Repeat physical-controller full matches on Windows/macOS, including the
  changed fumble scramble timing and both marker lifecycles. Automated results are
  recorded in TESTING.md and do not complete that acceptance ticket.
  The bounded standalone startup also exposed an active-RenderTexture release warning
  in unchanged FrontEndBackdrop.OnDestroy at intentional shutdown; check that separate
  front-end exit/scene-teardown path during platform acceptance.
- **#16:** Keep FootballAppearance/style changes below the same ball root. Material
  selection must not reset flight orientation, free motion or ownership.
- **#10/#18:** Pre-snap previews must clear when live ball markers take over. Playbook
  content continues using the existing Throw target/time contract and no-snap flow.

## Immediate follow-up areas after #12

With #3 merged and #12 in review, the remaining issues in impact order are
#10, #11, #13, #14, #15, #17, #18, and #16. #13 is the immediate playtest gate
because it covers the missing controller actions and cross-platform acceptance.

- **[#13](https://github.com/ludwigsen/Blacktop/issues/13):** Add the missing
  gamepad Pass/Pitch bindings there. Run full physical-controller matches on
  macOS and Windows, including route reads, catch/recovery identity, and defender
  cycling at 720p and 1080p. Keyboard receiver keys remain 1–4.
- **[#14](https://github.com/ludwigsen/Blacktop/issues/14):** Play the six-team
  roster across different fields and Gamebreaker states. Compare the feel of
  Blocking contact cadence and D-Moves block recovery alongside loose-ball scrums.
- **[#10](https://github.com/ludwigsen/Blacktop/issues/10):** Keep future pre-snap
  previews inside the gameplay label safe area and clear them when play starts.
- **[#15](https://github.com/ludwigsen/Blacktop/issues/15):** Check that new bodies
  preserve the nameplate anchor through height changes. The four new team
  identities have colors; custom crests were not supplied.

The #12 change does not complete those tickets' acceptance criteria.

## Follow-up areas after #10

- **#11** is next by impact. The run preview describes the called exchange.
  Validate visible handoffs and grip against the actual ball owner and mesh point.
- **#13** covers physical-controller timing for LT/L2 with left-stick movement and
  RB/R1 cycling, the offensive start binding, and full Windows/macOS matches.
- **#14** should include manually repositioned defenders, the variable CPU set
  window, and the restored authored Zone, Blitz and Man jobs in balance checks.
- **#18** can preview authored plays through the same route, block, exchange and
  coverage data. Add drawing support when the playbook gains new assignment types.

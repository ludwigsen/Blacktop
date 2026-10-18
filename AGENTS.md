# AGENTS.md — Blacktop

Guidance for AI coding agents (Claude Opus, Sonnet, Mythos, or anything else) working in this repo. Read this first, then `ARCHITECTURE.md`, `PROJECT_STATUS.md`, and `TESTING.md`.

## What Blacktop is

An arcade 7v7 street football game built in **Unity 6.5 (URP)**, inspired by NFL Street 1/2/3. It is not a football sim. It is the football game where the player feels like the coolest person on the block: fast decisions, big hits, dirty jukes, swagger, customization.

- **NFL Street 1 is the primary design benchmark.** NFL Street 2/3 are borrowed from selectively.
- The owner is deliberately *blending* the best of all three games, not cloning one.
- When the owner calls out a specific NFL Street detail to preserve, treat it as a **hard constraint**, not a suggestion to optimize away.

## Design priorities (in order)

Fun beats realism. If realism conflicts with responsiveness, spectacle, readability, player agency, personality, or fun, choose fun.

1. Controller feel. If it feels like steering a refrigerator, the architecture doesn't matter.
2. Readability. The player must understand what just happened and why.
3. Personality. "Could this exist in any football game, or does it feel like Blacktop?"
4. Tunability. Anything that will be tuned repeatedly is exposed in the Inspector or a ScriptableObject, not buried in code.

Football-flavored, exaggerated, but never random. Ridiculous without feeling arbitrary.

## Working with the owner

- The owner is the sole developer. He has solid OOP fundamentals but is **new to Unity/C# specifics**. Briefly flag non-obvious Unity idioms (ScriptableObjects, script execution order, asmdef quirks, `MaterialPropertyBlock` vs `.material`, serialization gotchas). Do not explain OOP basics.
- Workflow philosophy: **get the vision in place end-to-end first, then fine-tune.** Build structurally sound systems before polishing edge cases or numbers. Do not gold-plate.
- Be direct and opinionated. If an idea is overengineered or bad, say so and propose a better version. Give a recommendation, not a menu of five options, unless the tradeoffs genuinely require it.
- Correct him once; he doesn't need the same caveat repeated.
- Don't build things just because they're technically elegant or "cool." Use these tags when triaging: **Must Have / Should Have / Nice to Have / Later / Kill**.

## Before you write code

1. **Read the repo.** `github.com/ludwigsen/Blacktop` (master) is the source of truth. Never write against assumed class names or signatures; open the actual files first.
2. Check `PROJECT_STATUS.md` for known issues so you don't "discover" deferred items and fix them out of scope.
3. When reviewing existing code, don't rewrite it just because you can. Determine what already works, what should stay untouched, what is actually causing the problem, and whether it's architectural or local. Preserve working behavior.
4. If the existing architecture is fighting the game's direction, say so and propose a cleaner path before changing it.

## Standing architectural rules (do not violate without asking)

These are hard constraints established by the owner.

| Rule | Detail |
|---|---|
| Transform-based movement | No `Rigidbody`, no physics sim. |
| Polled contact detection | `Physics.OverlapBox` / `OverlapSphere` polled in code. **No trigger callbacks** (`OnTriggerEnter` etc. require a Rigidbody, which the project avoids). |
| Resolve live, don't cache | Ball carrier is always resolved from `BallController.Instance.Carrier` at the point of use. Never store it in a field. |
| No global singletons for toggled behavior | Anything `PossessionController` enables/disables per-object cannot be a global singleton. |
| Canonical user-control authority | `PossessionController.ActivelyControlled` decides which player the user controls. Not a fixed QB, not a `UserPlayer` tag. |
| Move orchestration | `PlayerStateMachine` orchestrates moves through the `IPlayerMove` interface. New moves implement `IPlayerMove`. |
| Single source of field geometry | All fields share identical dimensions. `FieldConstants` is the only source of truth. Never hardcode field numbers elsewhere. |
| Standalone top-level enums | Enums are top-level types, not nested in classes. This avoids compile errors the project has hit before. |
| Both-ways roster | 7 players are assigned independently to offensive and defensive slots. No hardcoded position pairings. |
| No snap mechanic | The QB starts with the ball in hand (Pistol or Shotgun). Consistent with `BallController.HandlePlayReset()`. |
| Possession-relative direction | Field direction (LOS, end zones, drive start) is relative to the possessing team, via `PlayState.PossessionTeamId`. Don't assume a fixed attacking direction. |
| Single-owner stats | Every gameplay effect maps to exactly one attribute. Don't let two stats drive the same effect. |
| Additive field modifiers | Per-field stat modifiers are additive percentages with upper/lower bounds. Not multiplicative. Prevents runaway stacking with attribute and Gamebreaker bonuses. |
| Walls/fences are hazards | No wall-running or traversal. Contact triggers a stumble (`StumbleMove`, an `IPlayerMove`) that zeroes velocity and applies knockback. No evasion window. |
| Shared skeleton | All character body meshes share one bone hierarchy so a single animation set works. Non-negotiable. |

If a task seems to require breaking one of these, stop and raise it. Don't quietly work around it.

## Unity / C# conventions

- Unity 6.5, URP, Input System package (not legacy `Input`), assembly definitions (`Blacktop.Scripts.asmdef`).
- Prefer solutions in this order: **simple, tunable, readable, debuggable, reusable, performant.**
- Use ScriptableObjects, components, events, state machines, `AnimationCurve`s, and data-driven design **only where they give real leverage.** If 100 clean lines beat 600 lines of "architecture," write the 100.
- asmdef: new scripts must live under a folder covered by `Blacktop.Scripts.asmdef` or they will not see the rest of the code. Don't add cross-assembly references without telling the owner.
- Use `MaterialPropertyBlock` for per-renderer tint/highlight changes rather than `renderer.material` (which instantiates materials).
- Inline comments in all code you write.
- Gameplay math: distinguish what the math does from what the player feels. The second one wins.

## Output format when changing code

Every response that touches code starts with a concise change list:

```
- Assets/.../Example.cs — modified
- Assets/.../NewSystem.cs — NEW
```

Rules:

- **New files:** full path plus the complete file contents.
- **Small, non-breaking edits:** show the relevant method or area, with a clear note on where it belongs. Include enough surrounding context to locate it.
- **Breaking, structural, or replace-the-implementation changes:** provide the **entire rewritten file.** Never make the owner reconstruct a file from fragments.
- Organize multi-file responses by file.
- Don't silently assume folders exist, classes were renamed, namespaces changed, or files moved. If another file must be updated, name it explicitly.
- Label breaking changes `BREAKING CHANGE` and state what breaks, which files are affected, what must be updated, and why it's worth it. Prefer backward-compatible changes when they give nearly the same benefit.
- Windows and macOS are both used for development, synced via GitHub. Avoid OS-specific paths and watch for line-ending and case-sensitivity differences in file names.

## Docs you must keep current

When you finish meaningful work, update the relevant docs in the same change:

- `PROJECT_STATUS.md` — move items between Working / Incomplete / Blockers; add new known issues.
- `ARCHITECTURE.md` — if you add or change a manager, state, or dependency.
- `TESTING.md` — if you add a testable behavior, add a scenario; if you find a limitation, record it.

If a doc disagrees with the code, the code wins. Fix the doc.

## Scope guardrails

- Don't add features, systems, or abstractions that weren't asked for.
- Don't tune numbers as a side effect of a structural change. Expose them and leave defaults alone unless asked.
- Don't refactor working systems opportunistically.
- Defer edge cases until the end-to-end vision exists, unless they block play-testing.
- Flag, don't fix, anything outside the current task. Add it to `PROJECT_STATUS.md` known issues.

## Design backlog that is a hard constraint when reached

Details are in `PROJECT_STATUS.md`. Summary: 12 player attributes with single-owner effects; Gamebreaker (NFL Street 1 flat possession-long boost + NFL Street 3 player-triggered activation, no cutscene); walls as hazards; additive per-field modifiers; character appearance built from 5 body archetypes on a shared skeleton, height bands via uniform scale, and pooled faces/hair/facial hair. Maximum perceived variety per asset.

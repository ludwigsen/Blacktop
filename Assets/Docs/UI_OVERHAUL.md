# UI overhaul audit and design contract

Audit base: `codex/blacktop-menu-prototype`, a996585. The original checkout is on
`codex/parametric-character-bodies` at 565887f with unrelated Blender/font changes.
GitHub master was inspected before implementation; the menu prototype is newer.
This change extends the menu prototype without merging it or modifying either source checkout.

| Area | Existing owner / reusable behavior | Presentation work |
|---|---|---|
| Main/navigation | FrontEndController / MainMenuView, runtime uGUI, InputSystemUIInputModule | Fixed wordmark, ink scrim, painted focus, preserved four actions |
| Settings | MainMenuView.Preferences, MenuSound PlayerPrefs | Theme preference and live preview |
| Setup | QuickPlayView / MatchSettingsView / PickupDraftView | Preserve modes, independent conditions, Random, review and MatchBootstrap; shared step indicator |
| Rosters/editor | RosterBrowserView / PlayerEditorView / PlayerLabView / PlayerEditorSession | Vertical accordions, fixed 20-step tier bars, preserve GUID transactions |
| Preview | RuntimePlayerPreview, camera orbit/zoom, owned render texture | Preserve resources and working preview; spacious left pane |
| Play calling | PlayCallSelector, authored offense/defense playbooks | Theme categories/cards, actual authored schematic diagrams |
| Scorebug | GameHUD / TeamScorePanel / HUDTheme, live score/down/clock/meter events | Sliced chassis and team-colored paint, Blacktop Pro Duo team names |
| Announcements | GameHUD, existing post-play start/completion events | Painted plates, live Barlow Condensed labels and short unscaled animation |
| Post-play | PlayState, PlayerAnimationPresenter | Preserve existing delay, AI presentation and event resolution |

Both scenes already use uGUI/TMP, not UI Toolkit. The menu reference canvas is
1920x1080 with matching anchors and scale-with-screen-size. Screens are generated
at runtime rather than authored UI prefabs. Shared construction helpers are the
appropriate reusable component boundary; scene/roster/character rebuilds are unnecessary.
Submenu logo previously reached y=.985; its new Inspector margin reserves room
for glyph ascenders and a television-safe top inset.

Existing art: eleven owner-supplied native 4K field images, continuous crossfade,
standalone-safe blur shaders, team crests, legacy distressed scorebug sprites.
Retain field image bytes/order and crest assignments. Legacy scorebug art remains
in the project for compatibility. Runtime scorebug construction uses the shared
street sprites.

Font metadata inventory is in UI_FONT_INVENTORY.json. Keep the already imported
BlacktopProDuo.ttf (family Blacktop, style Regular) for live team names and the source
of the fixed wordmark image.
Available local alternatives include COLR/CPAL white/black fill variants and
monochrome Fill/Paint variants. TMP SDF does not reproduce COLR paint layers, so
the existing monochrome outline source is the consistent runtime choice.
Barlow Condensed SemiBold was the only imported standard face; official Regular,
ExtraBold and BoldItalic plus OFL are added for real supported typography weights.
Do not synthesize extra bold/italic on these faces.


## Runtime design system

`BlacktopTheme` stores the palette as a ScriptableObject. `UIThemeLibrary` is the
single explicit `Resources/BlacktopUI/ThemeLibrary` entry, with both theme assets,
all five typography references, schematic fallback formations and the street
sprite references. `UITheme` is a data-only preference service using the existing
PlayerPrefs approach (`Blacktop.UITheme`, 0=Street Classic, 1=After Dark).
`ThemeableUI` binds a specific Graphic to a semantic role and optional type role;
`ThemeButton` keeps native uGUI input while owning contrast, focus/hover, pressed,
disabled and persistent-selection visuals. Bindings subscribe on enable and
unsubscribe on disable. Themes never search or recolor gameplay objects globally.

| Semantic role | Street Classic | After Dark |
|---|---|---|
| BackgroundPrimary | #15171C | #0B1020 |
| BackgroundSecondary | #10151F | #0A1428 |
| SurfacePrimary | #23272F | #1B2540 |
| SurfaceRaised | #303744 | #253452 |
| TextPrimary | #F3EFE6 | #F2F4FA |
| TextSecondary | #B6BFCC | #92A4C5 |
| TextOnBright | #10151F | #0A1428 |
| SelectionPrimary | #2D7FF9 | #344D91 |
| SelectionHighlight | #59D7FF | #74D9ED |
| ActionConfirm | #FF7B00 | #FFB454 |
| ActionCancel | #E63946 | #FF6B69 |
| StateSuccess | #46B88B | #63C8A5 |
| StateWarning | #FFD23F | #FFB454 |
| AccentSpecial | #FFD23F | #FFB454 |
| BorderSubtle | #495362 | #435879 |
| ShadowPrimary | #080B10 | #050A14 |

Navigation uses native Barlow Condensed SemiBold; body/secondary text uses Regular;
headings, scores and attribute numerals use ExtraBold; announcements use BoldItalic.
Blacktop Pro Duo is the live font for team names on the scorebug. The Blacktop logo
uses a fixed sprite based on the existing wordmark geometry. The themes tint the
shared sprites and use different field shading and accent strengths.

`StreetArt` applies sliced sprite borders and mounts decorative layers that pass
pointer input through. `AngularPanel` supplies a clipped-corner fallback.
`AttributeSegments` paints exactly
20 cells using the fixed five-tier rating colors. The summary/attributes panes
remain fixed; customization and developer create/edit use vertical accordions
with a native ScrollRect. `FrontEndFocus` reveals controller-focused content.
Save/cancel/roster cards and the preview stay outside the scrolling form.

The shared secondary-page logo reserves **54 reference pixels** above an 80-pixel
wordmark slot; `FrontEndSettings.submenuLogoTopPadding` exposes the margin.
Page content starts below the reserved header. Canvas scaling preserves this gap
at 720p, 1080p, 1440p and 4K. Tests check the bounds of the fixed artwork.

`PlayDiagram` renders formation slots, receiver route types, handoff destination
and defensive assignments as clean schematics. They are not exact motion forecasts.
It explicitly requires CanvasRenderer. No formation, route or assignment is written.
The existing authored offensive/defensive categories and input actions remain.
Mouse clicks also select category/card; clicking the selected card confirms it.

The scorebug retains left/center/right anchors, crest mirroring, scores, play clock,
downs, field position and segmented Gamebreaker meters. Its neutral chalk frame
surrounds independent brush plates tinted from each team's primary color. The right
plate mirrors independently of its logo and text. Team colors still own accent
rules, frame and meter fill.
The new `> BALL` marker reflects the existing drive-possession event. No match
clock exists in the game; the existing play clock remains the displayed timing.

`GameAnnouncement` observes the existing post-play lifecycle. Touchdown, turnover,
first-down and Gamebreaker treatments use sliced paint, semantic palette roles and a
220ms slide. The announcement sorts above field prompts and below game over.
Ordinary post-play duration and animation ownership stay with PlayState. Gamebreaker
activation uses the existing team-meter event and self-hides after a short overlay.
No gameplay state, attributes, ratings, GUIDs, save format or input mapping changes.

## Street sprite source (2026-11-07)

The two styleboards inform the chalk edges, paint layers and layout. The project
ships nine separate transparent grayscale sprites in
`Assets/_Project/Art/UI/StreetKit/`. Panels and cards share `AsphaltPanel` and
`ChalkFrame`. Buttons, team plates and event banners reuse `BrushPlate`.
`DryBrush`, `PaintCrown`, `SprayScatter`, `AsphaltGrain`, `InkScrim` and
`BlacktopWordmark` are separate decorative layers, so each can be placed and tinted
without baking in a screen, team or event label.

`SourceAssets/UI/build_street_kit.py` is the editable source. Run it with Python
and Pillow to regenerate the same pixels, then run **Blacktop > UI > Wire Theme
Assets** to repair import settings or references. The three resizable sprites use
full-rect 9-slicing at 200 pixels per unit. Their border widths preserve the
corners and the paint ends as their centers stretch. Use the individual PNGs and
their Unity import settings when composing new screens.

## Editor setup and delivery

The generated theme definitions/font atlases/library are committed, so opening
MainMenu normally requires no setup. **Blacktop > UI > Wire Theme Assets** is the
idempotent repair/setup command. It wires fonts, themes and sprites without regenerating scenes,
rosters, character art or venue prefabs. **Blacktop > UI > Build Themed UI (macOS)**
creates the development app under ignored Builds/UIOverhaul. All runtime components
stay inside the existing Blacktop assembly; no new packages or cross-assembly
references were added.

Physical-controller feel, Windows behavior and human-played matches require owner
acceptance. Historical 720p in-world nameplate/prompt overlap is
recorded in PROJECT_STATUS; this overhaul changes their typography and surface
roles but does not rewrite their world-space positioning.


Original theme rollout macOS development build: **0 errors / 408 reported warnings**, 435,525,615
bytes reported (the earlier build reported 438 warnings). Actual standalone native
checks exercised keyboard and mouse navigation, immediate Street Classic/After Dark
switching, background cycling/blur, secondary-logo clearance, and After Dark retained
after a full app restart. Street Classic was restored and the app left on Main.
No runtime exception was found in the captured startup/settings Player log. No roster
edits were saved. This does not claim a physical-controller or human-played full match.
Build and native evidence: ignored Builds/Validation/ui-release-build.log and
ui-native-player.log; screenshots are in the chat computer-use record.

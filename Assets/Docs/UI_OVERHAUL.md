# UI overhaul audit and design contract

Audit base: `codex/blacktop-menu-prototype`, a996585. The original checkout is on
`codex/parametric-character-bodies` at 565887f with unrelated Blender/font changes.
GitHub master was inspected before implementation; the menu prototype is newer.
This change extends the menu prototype without merging it or modifying either source checkout.

| Area | Existing owner / reusable behavior | Presentation work |
|---|---|---|
| Main/navigation | FrontEndController / MainMenuView, runtime uGUI, InputSystemUIInputModule | Shared theme states, angular panels, preserved four actions |
| Settings | MainMenuView.Preferences, MenuSound PlayerPrefs | Theme preference and live preview |
| Setup | QuickPlayView / MatchSettingsView / PickupDraftView | Preserve modes, independent conditions, Random, review and MatchBootstrap; shared step indicator |
| Rosters/editor | RosterBrowserView / PlayerEditorView / PlayerLabView / PlayerEditorSession | Vertical accordions, fixed 20-step tier bars, preserve GUID transactions |
| Preview | RuntimePlayerPreview, camera orbit/zoom, owned render texture | Preserve resources and working preview; spacious left pane |
| Play calling | PlayCallSelector, authored offense/defense playbooks | Theme categories/cards, actual authored schematic diagrams |
| Scorebug | GameHUD / TeamScorePanel / HUDTheme, live score/down/clock/meter events | Clean angular geometry; team accents remain independent |
| Announcements | GameHUD, existing post-play start/completion events | Semantic event treatments and short unscaled animation; timing unchanged |
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
in the project for compatibility; production scorebug construction uses geometry.
AsphaltGrain, DryBrush, SprayScatter and EdgeWear are missing optional masks;
theme-library sprite slots accept future grayscale art. No composite concept image
is used as a UI sprite. Missing character art remains labelled by existing views.

Font metadata inventory is in UI_FONT_INVENTORY.json. Keep the already imported
BlacktopProDuo.ttf (family Blacktop, style Regular) for the wordmark, natural spacing.
Available local alternatives include COLR/CPAL white/black fill variants and
monochrome Fill/Paint variants. TMP SDF does not reproduce COLR paint layers, so
the existing monochrome outline source is the consistent runtime choice.
Barlow Condensed SemiBold was the only imported standard face; official Regular,
ExtraBold and BoldItalic plus OFL are added for real supported typography weights.
Do not synthesize extra bold/italic on these faces.


## Runtime design system

`BlacktopTheme` stores the palette as a ScriptableObject. `UIThemeLibrary` is the
single explicit `Resources/BlacktopUI/ThemeLibrary` entry, with both theme assets,
all five typography references, schematic fallback formations and four optional
mask slots. `UITheme` is a data-only preference service using the existing
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
Blacktop Pro Duo is exclusive to the wordmark, with zero added letter spacing.
The themes also differ in field shade, optional mask strength and the number of
main-menu decorative slashes. Masks are optional geometry/art extension points,
not presented as finished asphalt/paint textures.

`AngularPanel` supplies scalable clipped corners; `AttributeSegments` paints exactly
20 cells using the fixed five-tier rating colors. The summary/attributes panes
remain fixed; customization and developer create/edit use vertical accordions
with a native ScrollRect. `FrontEndFocus` reveals controller-focused content.
Save/cancel/roster cards and the preview stay outside the scrolling form.

The shared secondary-page logo reserves **54 reference pixels** above an 80-pixel
wordmark slot; `FrontEndSettings.submenuLogoTopPadding` exposes the margin.
Page content starts below the reserved header. Canvas scaling preserves this gap
at 720p, 1080p, 1440p and 4K. Glyph bounds, not only transform bounds, are checked.

`PlayDiagram` renders formation slots, receiver route types, handoff destination
and defensive assignments as clean schematics. They are not exact motion forecasts.
It explicitly requires CanvasRenderer. No formation, route or assignment is written.
The existing authored offensive/defensive categories and input actions remain.
Mouse clicks also select category/card; clicking the selected card confirms it.

The scorebug retains left/center/right anchors, crest mirroring, scores, play clock,
downs, field position and segmented Gamebreaker meters. Team surfaces use clean
geometry; serialized raster-sprite fields remain compatible but are unused by the
active scorebug. Team colors still own crests, accent rules, frame and meter fill.
The new `> BALL` marker reflects the existing drive-possession event. No match
clock exists in the game; the existing play clock remains the displayed timing.

`GameAnnouncement` observes the existing post-play lifecycle. Touchdown, turnover,
first-down and Gamebreaker treatments use semantic palette roles and a 220ms slide.
Ordinary post-play duration and animation ownership stay with PlayState. Gamebreaker
activation uses the existing team-meter event and self-hides after a short overlay.
No gameplay state, attributes, ratings, GUIDs, save format or input mapping changes.

## Editor setup and delivery

The generated theme definitions/font atlases/library are committed, so opening
MainMenu normally requires no setup. **Blacktop > UI > Wire Theme Assets** is the
idempotent repair/setup command. It wires fonts/themes without regenerating scenes,
rosters, character art or venue prefabs. **Blacktop > UI > Build Themed UI (macOS)**
creates the development app under ignored Builds/UIOverhaul. All runtime components
stay inside the existing Blacktop assembly; no new packages or cross-assembly
references were added.

Physical-controller feel, Windows behavior, human-played matches and final texture
art require owner acceptance. Existing 720p in-world nameplate/prompt overlap is
recorded in PROJECT_STATUS; this overhaul changes their typography and surface
roles but does not rewrite their world-space positioning.


Final macOS development build: **0 errors / 408 reported warnings**, 435,525,615
bytes reported (the earlier build reported 438 warnings). Actual standalone native
checks exercised keyboard and mouse navigation, immediate Street Classic/After Dark
switching, background cycling/blur, secondary-logo clearance, and After Dark retained
after a full app restart. Street Classic was restored and the app left on Main.
No runtime exception was found in the captured startup/settings Player log. No roster
edits were saved. This does not claim a physical-controller or human-played full match.
Build and native evidence: ignored Builds/Validation/ui-release-build.log and
ui-native-player.log; screenshots are in the chat computer-use record.

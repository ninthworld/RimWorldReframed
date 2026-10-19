# RimWorld UI Reworked reconstruction specification

Revision 2 · 17 October 2026 · Planning and reference analysis · No new implementation in this revision

The target is a faithful reconstruction of [RimWorld UI Reworked by yarich SA](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked). The existing Reframed UI v0.2 prototype is an experiment, not a visual baseline. Completion requires both reference fidelity and correct RimWorld behavior. A recolored vanilla interface does not meet this target.

This specification divides the reference into independent projects with shared design foundations. The accompanying [test regimen](02-test-regimen.md) defines how each project must be demonstrated in the game. The [work-order plan](03-work-order-plan.md) defines sequencing and the information required before assigning implementation.

The [team workflow](04-team-workflow.md) records delegation, worktrees and continuity. The [inferred-design policy](05-inferred-design-policy.md) is now required reading for missing states: the user directs the team to infer them from the concept's established patterns without repeatedly seeking approval for routine details. Exact source matching governs shown states; documented design consistency and functional parity govern inferred states. Neither class may silently weaken the other.

## 1. Evidence and the meaning of an exact match

Only public images and videos are available; there is no Figma file, font package or icon library. Four original PNG boards were recovered from the project's exposed image sources. They supersede the smaller browser previews for measurement. The user-supplied WebP is retained as alternate evidence, not mixed into the canonical PNG baseline.

| ID | Original raster | Contents | Measurement use |
|---|---|---|---|
| B01 | [1920 × 1027 — local evidence omitted](LOCAL_EVIDENCE.md) | Main menu, save browser, storyteller and playstyle | Individual component geometry and styling; presentation positions are not game anchors |
| B02 | [1920 × 1080 — local evidence omitted](LOCAL_EVIDENCE.md) | Complete colony HUD | Canonical full-screen placement at this raster resolution |
| B03 | [1920 × 1146 — local evidence omitted](LOCAL_EVIDENCE.md) | Item comparison | Comparison geometry; exclude tutorial banner and rounded scene frame |
| B04 | [1920 × 2949 — local evidence omitted](LOCAL_EVIDENCE.md) | Work, Summary, Needs, Social, Gear, DLC | Individual panels and controls; each panel is a separate scene |
| V01 | [Extended concept film](https://www.youtube.com/watch?v=VQU_N40agkU) | Main-menu/load sequence and colony HUD, approximately 21 seconds | Screen context and transitions; not a lossless pixel baseline |
| V02 | [Rim UI Assets 2](https://player.vimeo.com/video/1110005133) | Resource expansion, issues, tools and search | Player reports 1920 × 844, duration 5.316666 s |
| V03 | [Rim Notification](https://player.vimeo.com/video/1110003819) | Green research-complete notification | Player reports 1920 × 1080, duration 3.916666 s |

Hashes, dimensions and source URLs are recorded in [source-manifest.json — local evidence omitted](LOCAL_EVIDENCE.md). Component inspection crops and source coordinates are recorded in [region-manifest.json — local evidence omitted](LOCAL_EVIDENCE.md). All crop coordinates use the top-left origin and exclusive right/bottom edges. A crop is an evidence region, not a claim that every pixel inside it belongs to the component.

Evidence labels used throughout:

- **M — measured:** a coordinate, count or decoded RGB value from a named source raster.
- **O — observed:** a shape, arrangement or interaction visible in the source.
- **E — estimated:** a plausible reconstruction parameter, including radii or nominal font size, still requiring comparison.
- **U — unresolved:** not uniquely recoverable from available evidence, or not yet verified frame by frame.
- **R — required:** a functional or engineering requirement for the mod; it is not presented as the designer's documented behavior.

Pixel equality means identical UI pixels at an explicitly fixed resolution, scale, data state and capture environment. It does not mean that a different colony map, pawn, language or monitor scale will produce the same screenshot. B01 and B04 are presentation boards: their blue grids, captions, outside margins and arrangement of several windows are excluded from the mod. B03's step-by-step banner is explanatory artwork, not a game toolbar. The film's closing title is also presentation material.

The original save, camera state and fully separated UI layers behind B02 are unavailable. Therefore, a full-screen zero-difference claim against B02 is currently impossible. UI regions can be reconstructed and compared; a whole-scene demonstration needs a matching fixture or a documented separation of UI from scene pixels. This limitation must never be hidden with an expansive ignore mask.

## 2. Shared visual system

### 2.1 Palette

These are recurring exact decoded colors in the PNG evidence. They are useful starting values, not proof of the underlying layer opacity or blend mode. A sampled color on a flattened image is the final pixel after compositing.

| Role | Measured RGB | Evidence and use |
|---|---|---|
| Deep inset | `#060A0D` | B01 storyteller left column; B02 issue rows; B04 work lanes/popover |
| Main panel | `#0E1317` | All boards: menu shell, HUD shells, pawn header, comparison shell |
| Content surface | `#181F25` | Storyteller cards/footer, comparison data columns, pawn tab content |
| Normal button | `#212832` | Main menu and HUD navigation |
| Raised/selected surface | `#272F3C` | Work tags, social portrait cards, selected playstyle, comparison headers |
| Fine table rule | `#303948` | B03 repeated comparison separators |
| Primary text | `#D8E2EA` | B03 solid text interiors; other text includes lighter pixels and antialiasing |
| Secondary text | `#B6C6D4` | B03 values and secondary copy |
| Muted text/icons | `#84929F` | Repeated across boards |
| Best-value emphasis | `#FDC700` | B03 highlighted values |
| Destructive face | `#82181A` | Main-menu Quit button |
| Positive face | `#0D542B` | Storyteller Next and save-load action |

Semantic accents are not interchangeable: the reference uses green for positive states, blue for quests, yellow for warnings and favorable comparison values, red for negative values or destructive actions, cyan for ideology symbols, and category-specific strips on work tags. Their exact rendered pixels vary with edges and gradients. Do not replace all green or all red instances with one guessed global accent.

[Pixel palette and control details — local evidence omitted](LOCAL_EVIDENCE.md)

The [measurement appendix — local evidence omitted](LOCAL_EVIDENCE.md), [32 point samples — local evidence omitted](LOCAL_EVIDENCE.md), and [nine complete edge/gradient scanlines — local evidence omitted](LOCAL_EVIDENCE.md) preserve concrete evidence for reconstruction. These are representative measurements, not a completed tracing of every state.

### 2.2 Typography

**U: exact font family, files, weights and rasterizer settings.** No font declaration inside the Behance website identifies the font baked into the artwork. Behance's own CSS must not be treated as evidence for the concept's typography. The letters are a humanist sans serif, with regular body text, stronger names/labels, and small uppercase headings with extra tracking. Calling it Inter, Open Sans, Roboto or Source Sans without a matching glyph audit would be an unsupported claim.

| Type role | Visible treatment | Starting search range in source pixels, E | Evidence |
|---|---|---:|---|
| Standard labels/buttons | Regular, mixed case, compact line box | 14–16 nominal px | B01 Load/Tutorial; B02 navigation; B04 tags |
| Primary name/row title | Semibold or bold, same general family | 14–16 px | B03 item title and stat labels; B04 pawn name |
| Secondary description | Regular, muted, multiline | 12–14 px | Storyteller cards, social event descriptions |
| Micro section heading | Bold uppercase, wider tracking | 8–10 px | MATERIALS, SKILLS, EQUIPMENT, DIFFICULTY |
| Numeric readout | Regular or bold by hierarchy; percent and unit smaller in some contexts | 12–16 px | Clock, quantities, bars, weights |
| Presentation headings | Larger and not an in-game control style | Excluded | Background, Goal, Item Comparison Tool captions |

The ranges above are not final font sizes. [Unscaled glyph crops — local evidence omitted](LOCAL_EVIDENCE.md) are provided. Resolve typography as its own project before panel-by-panel implementation:

1. Collect alphabets from the source, including the distinctive forms of `a`, `g`, `R`, `1`, `4`, `%`, punctuation, apostrophes and arrows. Separate regular, bold and micro-heading samples.
2. Render candidate fonts at fractional and integer sizes. Compare glyph outline, advance width, x-height, cap height, baseline, kerning, line height and weight. Use multiple words; matching a single word is insufficient.
3. Freeze a text-style table and the native asset identity/provenance available from the installed game. **The user's later direction restricts this work to existing game fonts: do not create, download or bundle fonts or reconstruct glyphs.** Record an unavailable raw font-file hash as unknown with its reason. If no existing candidate matches, retain the measured mismatch and continue calibration; do not silently accept a near match.
4. Validate the actual Unity text path. The same font can differ through hinting, subpixel placement, atlas behavior, filtering or gamma. Use the existing native font rendering path under the current scope; text must remain data-driven, with glyph fallback for characters outside the canonical scene. A matching resource/fallback name alone does not establish exact glyph identity.
5. Specify overflow per control: ellipsis, wrapping, clipping, scrolling or expansion. Preserve the reference baseline and padding. Do not shrink every string until it fits.

### 2.3 Surfaces and component geometry

The visible design uses several radii, not one universal rounded rectangle. E: tiny buttons/tags approximately 3–4 source pixels; compact HUD containers around 12 pixels; larger menu/comparison containers around 16 pixels; pawn panels around 20 pixels. These must be fitted against corner crops at the original scale, including antialiased edge coverage. Presentation-board corners are much larger and are excluded.

Common construction is outer shell → content inset or header → hairline separator → button/card → icon/text. Panels often have nearly opaque dark centers. U: whether a specific panel uses opacity, a shadow, or a darker underlying layer. Flattened artwork cannot uniquely solve `observed = alpha × foreground + (1 − alpha) × background` when both alpha and foreground are unknown. Multiple backgrounds or a deliberately chosen reconstruction recipe are needed.

**User override, 18 October 2026:** every window/panel must animate in and contract out, including nested/secondary windows, and have a slightly translucent background. This is an explicit product rule independent of uncertainty in flattened source art. Opening and closing both need validation; paint opacity must be effective after all backing layers are considered. Existing unconverted surfaces remain coverage gaps, not implied exceptions. See DEC-060 and the extension policy.

Primitives must expose independent parameters for face, border sides, corner coverage, inset, outer shadow, selected state, icon tint and text. Nine-slice borders must preserve corners and one-pixel rules rather than stretching a screenshot of a labeled button. Source positions must map consistently into screen pixels; changing `UI.scale` is not permission to improvise spacing.

### 2.4 Button outlines, shading and gradients

The ordinary main-menu button center is a **flat `#212832`**, not a broad vertical gradient. At B01 x=220, the New colony button has the following exact vertical samples:

| y | RGB | Rendered feature |
|---:|---|---|
| 508 | `#0D1216` | Outside-edge sample |
| 509 | `#151C23` | Outer fringe |
| 510 | `#35455B` | Bright top edge |
| 511 | `#1F242C` | Darker inner edge |
| 512–550 | `#212832` | Constant face along this scanline |
| 551 | `#222933` | Lower edge transition |
| 552 | `#232B32` | Bottom edge |
| 553 | `#0A0F12` | Dark outside sample |
| 554 | `#0E1317` | Panel background |

This is a raster profile, not an inferred Photoshop/Figma effect stack. The Quit button has a flat `#82181A` center but a different edge profile: at B01 x=470, y=666 is `#5B0809`, y=667 is `#8C1A1C`, y=668–694 is the face, and y=697 is `#42090A`. It must not simply inherit a blue border from normal buttons. The Next button has a flat green center and its own profile in the CSV.

Button families to build separately:

| Family | Visible design | Required state coverage |
|---|---|---|
| Text and icon action | Normal face, light top outline, compact radius, left glyph | Rest, hover, pressed, disabled, keyboard focus |
| Destructive/positive | Red/green face and matching edge treatment | Same states; semantic action preserved |
| Icon-only action | Square footprint, optically centered glyph, optional shortcut badge | Hover tooltip, selected, pressed, disabled |
| Outline tag/pill | Dark interior, fine border, narrow category stripe | Inactive, selected/filled, unavailable |
| Tab | Icon plus label, top/side border, content joins selected tab | Active/inactive, hover, overflow |
| Toggle | Selected fill or colored outline, not merely transient hover | On/off, unavailable, persistent state |
| Checkbox | Small filled square and check mark | Checked/unchecked/disabled/focus |
| Add-group tile | Dashed outline and oversized plus | Empty group affordance, hover, action |

The boards do not show every hover/pressed/focus state. Unseen states remain U until video evidence or an explicit extension specification establishes them. Never describe a fabricated hover lightening of “10%” or an invented 150 ms fade as an exact design fact.

Status meters **do** have visible left-to-right gradients, plus dark tracks and subtle outlines. M: health samples at B04 y=790 progress from `#AE3A3A` at x=1110 to `#7E3737` at x=1180 and `#5C3535` at x=1230. The yellow mood meter, green food meter, blue-violet recreation meter and green Needs mood meter each have distinct profiles. Temperature uses a multicolor blue→green→yellow→orange→red ramp with an inner range indicator. Track and fill must be separate layers; specify clipping, zero/full endpoints and whether the gradient spans the whole track or just the filled section.

### 2.5 Symbol and asset catalog

No emoji or platform-dependent Unicode glyph may substitute for a reference symbol. Each asset needs a stable ID, source crop, silhouette, optical bounds, stroke/fill recipe, baseline, tint behavior and state variants. Reuse a RimWorld texture only after comparison proves it is the same asset.

| Asset family | Visible symbols to inventory | Evidence |
|---|---|---|
| Main menu | Plus, load tray/arrow, scroll, cog, briefcase/list, open book, exit door/arrow | B01 menu |
| Save browser | Back bend-arrow, magnifier, floppy disk, colored colony houses, play triangle, trash bin | B01 load |
| Storyteller | Three portraits, short difficulty strips, back arrow, next arrow | B01 storyteller |
| Main navigation | Briefcase, clipboard, assignment clipboard, flag/animal, paw, flask, scroll, linked world shape, settlement, ideology, chart | B02 dock |
| HUD controls | Four dots, list/menu, plus, pause bars, speed labels, cloud, category chevrons | B02 |
| Issues and letters | Bed, unhappy face, idle person, medicine, heat-wave symbol, electrical warning, quest scroll, success check | B02 and V02/V03 |
| Work | Pawn portraits, white skull, cyan emblem, green paired emblem, copy, paste, category strips, passion flame | B04 work |
| Pawn header/tabs | Four dots, book, bars, backpack, heart, people, bubbles; medical/status, red crossed symbol, cyan emblem, skull, house | B04 panels |
| Social | Heart, blue speech bubbles, red angry face, purple bubbles, plus and cog, relation portraits | B04 Social |
| Gear | Actual item/apparel thumbnails, armor/mass/temperature meters | B04 Gear |
| Comparison | A/B comparison glyph, selection brackets, close X, cog, checkbox check | B03 |
| DLC | Five colored expansion marks, selected expansion logo, external-link arrow, promo imagery/video | B04 DLC |

Some tiny status symbols are visible but their gameplay meaning is not established by the artwork. Map them to real game state during the relevant work order; do not guess a function from their shape. Pawn and item portraits are live assets, with canonical fixture appearance recorded separately from the container design.

## 3. Component projects

Every project below inherits the shared design direction. Under the user's updated delivery policy, first ship coherent usable feature increments and refine visual differences afterward. The source crops and final fidelity targets remain useful, but incomplete pixel calibration must not block the next feature. Record approximations honestly and batch representative testing across multiple features.

### P01 Main menu

[Main menu — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B01. M inspection rectangle `[186,494,498,714)`; 312 × 220 pixels. The film places this compact menu beside the planet background and below the RimWorld logo; B01 does not establish its screen anchor.

**Components and design:** full-width New colony with centered plus/text; second row Load and Tutorial; generous separator gap and fine horizontal rule; Options and Mods; Credits and red Quit. Interior left/right padding is approximately 16 px. E starting sizes: primary action 280 × 44; paired actions 136 × 32 with an 8 px gutter. Preserve the asymmetry between the primary row and lower rows, exact icon silhouettes, and destructive button treatment.

**Behavior:** route to native new-game, load, tutorial, options, mods, credits and quit flows. Keep any game confirmations. Pointer hover, keyboard traversal, Escape behavior, missing/disabled actions and return from submenus are separate states. Logo, planet backdrop, footer information and DLC selector placement require a full-scene film frame before final screen acceptance.

**Steps and exit:** measure all seven controls; fit normal/destructive/icon styles; establish full-screen anchor from V01; connect actions; capture rest/hover/pressed/focus/return states. Tests P01: each command fires once; dismissing a submenu returns correctly; no invisible native button remains clickable underneath.

### P02 Save browser

[Save browser — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B01, inspection rectangle `[552,417,1098,791)`, 546 × 374. Rounded shell with top navigation strip, left save list and right selected-save card. Header includes back, search, All and named colonies with colored house glyphs. The right card contains a colony preview fading into a dark lower section, save title, colony name, green load button, trash icon, then play time, colonist count, storyteller and colony wealth in two columns.

**Details:** preserve list-row baseline, floppy-disk color variation, relative timestamps, selected-row treatment, tab borders, preview crop and fade profile. M approximate preview region `[842,481,1081,775)` includes both image and metadata; do not stretch the map thumbnail to fill the entire card. Tabs that exceed width need specified scrolling or clipping behavior.

**Behavior:** selecting a row updates preview and metadata; search and colony tabs filter actual saves; load targets the selected file. R: handle absent thumbnail, old save schema, renamed/deleted/corrupt file, long title, duplicate colony names, empty search and a large list. Deletion uses the game's confirmation and never auto-selects a different file during confirmation.

**Steps and exit:** define metadata source and preview cache; implement selection/filtering separately from rendering; preserve native load/delete behavior; validate exact sample and edge-case fixtures. Tests P02 include correct file identity, no writes on preview, search clearing, selection after deletion and graceful missing metadata.

### P03 Storyteller and playstyle

[Storyteller — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B01 `[1153,278,1735,930)`, 582 × 652. Dark left list with title/introduction and three portrait cards; right panel with selected storyteller name/description, difficulty pills, event-frequency curve and two large playstyle options; footer back button, explanatory copy and green Next.

**Details:** Cassandra/Phoebe/Randy portraits and text must retain their relative size. Difficulty choices have small colored leading strips, including neutral Custom. The selected Community builder pill and selected Commitment mode card demonstrate filled selection. The line graph is a thin light curve inside a bordered dark rectangle with its own caption. Do not approximate it with a generic stock chart; trace the visible path for the canonical selected state.

**Behavior:** selections update actual storyteller, difficulty and save mode. R: custom difficulty opens its native options; changing selection preserves unrelated setup; Back/Next and keyboard navigation work. The curve is conceptual; do not change incident generation to fit it or invent a numerical probability forecast.

**Steps and exit:** trace card and curve geometry; define every selected state; bind to game setup; test all storytellers and modes plus back/forward persistence. P03 must distinguish the visual selected state from a hover state.

### P04 Global HUD placement and navigation

[Navigation dock — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B02 is the full-screen canonical reference at 1920 × 1080. Core screen-space measurements are M inspection extents, with subpixel corner edges still subject to tracing:

| Region | Source rectangle | Placement requirement |
|---|---|---|
| Resources | `[16,16,107,408)` | Top-left inset 16 px |
| Colonist group container | `[590,16,1330,128)` | Top-center in this nine-pawn fixture |
| Clock and speed | `[1747,16,1906,106)` | Top-right with speed strip below clock |
| Letters and issue stack | `[1676,495,1904,809)` | Right-side stack in this fixture |
| Navigation including architect | `[16,1019,1333,1068)` | Bottom-left; 12 px to lower raster edge |
| Tools and main menu | Approximately x=1856–1904, y=964–1068 | Bottom-right, separated vertically |

**Components:** architect plus with X hint; Work, Schedule, Assign, Animals, Wildlife, Research, Quests, World, Factions, Ideoligions and History buttons; each with its individual icon. Separate rounded tools and menu controls carry C and ESC corner badges. The terrain/mouse readout remains above the lower-left controls.

**Behavior:** use native tab semantics, shortcuts and selection. World view, no-map state, modal windows, portrait selection, letter stack and learner panel must not collide with the dock. More tabs from mods and long translations require an explicitly specified overflow layout outside canonical fidelity mode.

**Steps and exit:** build an anchor/occupied-area layout model; reproduce B02 at its scale; verify every tab; add collision and overflow cases. No truncating the canonical labels or dropping Factions/Ideoligions/History to make the row fit. P04 regression includes tools, architect, clock, notifications and pawn panels together.

### P05 Colonist grouping and cards

**Extension contract:** EXT001–EXT004 and EXT026 in the [inference policy](05-inferred-design-policy.md). Three groups are an illustrated example, not a maximum. Additional groups, overflow, ungrouped pawns and lifecycle changes must be designed and tested consistently.

[Colonist groups — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B02. Three labeled groups: WARRIORS, ENGINEERS & BUILDERS, CREATIVE; nine portrait/name cards and a dashed add-group tile. Portrait cards are approximately 60 × 78 px with 8 px gaps; group spacing is larger. Uppercase headings sit above the cards inside one dark container.

**Current implementation scope, user correction 17 October 2026:** retain native selection, multiple selection, camera jump and roster distinctions while establishing the visual composition. Use placeholder headings; no separate grouping editor or manual membership/persistence system. The plus tile temporarily opens Work. Work-derived grouping semantics are deferred until more UI exists. This supersedes the earlier proposed create/rename/delete flow; the shown plus tile never established such a dialog.

**Steps and exit:** implement the compact shell, small tracked white uppercase headings, portrait/name cards and dashed plus in the first visual pass. Test selectability and combined HUD placement together. Keep final grouping, native reorder, exact portrait masks, dense overflow and lifecycle matrix explicitly open. A functional result does not close visual reconstruction.

### P06 Resource counter and weekly trends

[Resource counter — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** compact B02 column and expanded V02 state. Categories MATERIALS, WEAPONS and FOOD use tracked uppercase text. Compact rows contain item thumbnail and rightward quantity with no item name. O expanded state includes names and green/red/neutral trend markers while preserving category grouping. The caption ties the hover behavior to weekly trends.

**Behavior:** hover expansion must remain usable while the pointer enters the expanded panel. R: define the seven-day comparison, unavailable history, zero/negative/large values, category membership, hidden/unavailable resources, map switching and data sampling cadence. Do not fabricate a trend at first load or treat price change as quantity change.

**Steps and exit:** measure compact column from B02 and expanded width from a V02 frame; trace item icons; implement resource adapter and history; fit expansion and reverse transition; verify no count discrepancies. Tests P06 include pointer crossing, no layout obstruction, 0/1/999/100000 quantities and added resource definitions.

### P07 Clock, weather and speed

[Clock — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B02 top-right card: bold 22:00, 13°C and cloud on the first line; muted date on the next line. A separate compact strip below contains pause and 1x, 2x, 3x. Preserve this scale; the prior prototype's large vanilla time block is not the target.

**Behavior:** native game speed, pause restrictions, forced pause, keyboard shortcuts, date/season/weather, map temperature and world fallback. R: temperature preferences, long date strings and hidden weather need defined layouts. Labels are symbolic speed choices, not a promise of achieved simulation throughput.

**Steps and exit:** trace cloud and speed states; bind readouts; verify click and keyboard parity plus forced pause. P07 tests time progression, weather transitions, map/world switching, tooltip positioning and no interaction with the UI behind the speed strip.

### P08 Colony needs and issues

[Issues and letters — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B02 and V02. Three collapsible groups COLONISTS, RESOURCES, ENVIRONMENT. Headers use tiny uppercase text, horizontal rules and chevrons. Alert rows have dark inset faces and a leading semantic icon. Examples include beds, recreation, idle colonist, medicine and heat wave.

**Behavior:** grouping is by subject, not severity. Preserve critical-alert urgency separately. Header clicks only collapse that group; row clicks invoke the corresponding native alert action. R: preserve tooltips, cycling targets, dismissal policies, custom alert classes, alert updates and expansion state. A collapsed group must not silently erase critical information.

**Steps and exit:** define an alert-to-category mapping with a fallback for unknown/modded alerts; reproduce open/closed group states and stack reflow; verify native action delegation. P08 tests empty groups, mixed severity, disappearing alerts, very long text, many alerts and an alert that overrides its own drawing behavior.

### P09 Notifications and letters

**Extension contract:** EXT005–EXT010 and EXT024 in the [inference policy](05-inferred-design-policy.md). The reference toasts establish a design language, not a complete event catalog. Audit all supported notification channels and define event/family mappings, icon and severity roles, actions, lifecycle and a tested unknown-event fallback. The [static installed-definition inventory — local evidence omitted](LOCAL_EVIDENCE.md) is a starting point, not a claim of full runtime coverage.

**Target:** B02 compact Bzzzt and quest toasts; V03 research-complete example. Rounded dark pills contain a colored icon/title on the left and muted relative timestamp on the right. The research toast has a green success check and a colored glow/fade strongest toward the left. Warning and quest colors differ. Do not use a uniform colored rectangle.

**Motion:** O a dedicated notification animation exists; U exact entry/exit duration, opacity curve, translation, dwell and easing. V03's 3.916666-second clip duration is not the notification's dwell time. Frame extraction and event-relative measurements are a prerequisite to motion sign-off.

**Behavior:** preserve letter identity, target/camera jump, opening content, dismissal and queue order. Tests must cover simultaneous letters, duplicate labels with different IDs, paused time, old/new timestamps and disappearance without shifting a click to another letter.

**Steps and exit:** establish per-type icon/color vocabulary; trace default pills from B02; isolate animation frames from V03; implement state machine and reflow; capture event-relative frames in game. P09 must pass both visual and event-count tests.

### P10 Tools menu

**Target:** V02 and B02 four-dot launcher. O opens on hover. Panel includes a search field; Codex and Compare; GAMEPLAY controls for repair damaged buildings and automatic roofing; INTERFACE controls for colonists, helper and categorized resources; OVERLAYS for beauty, heat, pollution, terrain, fertility, zones, roofs and room stats. Controls form a mixture of full-width rows and pairs. Selected toggles have visible emphasis.

**Motion (user clarification, 18 October 2026; DEC-045):** the outer panel expands from the launcher button when opening and retracts into that same button when closing. Submenu navigation is a separate animation inside the open panel: content slides left to reveal the submenu within the panel bounds. Keep the outer shell open during that internal transition. Exact duration, easing and intermediate geometry remain unmeasured; returning with a rightward slide is an inferred reverse, not an independently observed source fact.

**Behavior:** bridge launcher-to-panel pointer travel without premature closing. Retain native overlay exclusivity and DLC availability. R: define click/keyboard access as well as hover, tooltip delay, leave region, Escape, focus and overlay-state persistence. These accessibility behaviors are extensions where not demonstrated.

**Steps and exit:** capture clean V02 tools and intermediate motion frames; inventory every icon and toggle; map controls to native game settings; implement open/close hit-region rules; test selecting every item and returning to the map. Verify expansion from and retraction into the launcher separately from the clipped leftward submenu slide. P10 includes no click-through into the map and no unwanted setting change on hover; hidden or transitioning controls must not receive unintended input.

### P11 Search

**Target:** V02 search state. Header SEARCH, back arrow, magnifier/input and clear X. Query “steel” produces a vertical list of matching resources, stacks and constructed objects; rows contain thumbnails, names, quantities, quality or hit-point percentages. A damaged item has red text. The list fades/clips at the bottom.

**Transition:** enter this submenu through P10's internal leftward content slide while the outer tools panel remains open (DEC-045). A rightward Back transition is the inferred reverse; retain the tools navigation context.

**Behavior:** define actual searchable scope, matching rules, sorting, result identity, selection/jump behavior and pagination/scrolling. R: handle zero results, punctuation, Unicode, case, destroyed or moved objects and a large colony. Do not index every thing on each repaint.

**Steps and exit:** obtain a frame with full list bounds; separate search service from renderer; implement cancellation and navigation; test live changes and repeated queries. P11 visual fixtures include empty, typed, populated, scrolled and cleared states. No hidden result may receive a click through the panel fade.

### P12 Item comparison

[Item comparison — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B03. O flow: hover Tools → Compare → select two or more items. The main shell spans approximately x=634–1346, y=370–937; the inspection crop includes its external close/settings controls. Left column has uppercase category headings and strong stat labels; each selected item has a blue-gray header with thumbnail, name and muted quality, then a bordered dark data column. Rules align across columns. Favorable values are yellow and bold. A settings popover contains Only show differences and Highlight best values checkboxes.

**Data:** BASICS includes quality, hit points, market value, deterioration; ranged weapon section has four accuracy distances; melee section includes DPS and armor penetration. Reference values are illustration data, not changes to game balance. Best-value comparison must understand whether higher or lower is desirable; deterioration is lower-is-better, while some stats are neutral or incomparable.

**Behavior:** maintain item IDs, selection mode, cancellation, add/remove item and support more than two columns. R: handle unequal stat sets, units, ties, missing values, live damage, destroyed items, long names, scroll alignment and modded stats. Checkbox meanings must affect data and highlight consistently.

**Steps and exit:** establish stat adapters and direction metadata; trace columns/rules/type; implement mode and controls; test semantic comparisons independently of drawing; capture two-item and three-item cases. P12 fails if gold means “numerically largest” for every row or if selecting an item issues an unintended world command.

### P13 Work tags and priorities

[Work tags — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04. Seven compact pawn rows, each with portrait, name/role, status symbols, dark task lane, ordered work tags, copy and paste squares, and Autosort. A large hover popover has six columns and a small downward pointer anchored to the relevant row. Tags have narrow leading category stripes, labels, optional skill numbers and yellow passion symbols. Filled and outlined tags are distinct states.

| Popover category | Visible color | Visible options |
|---|---|---|
| MEDICAL & CARE | Blue | Patient, Doctor, Bed rest, Childcare |
| LEARNING | Cyan/teal | Dark study, Research |
| FARMING | Green | Grow, Plant cut, Mine, Hunt |
| PRODUCTION | Yellow | Cook, Smith, Tailor, Craft, Art |
| COMBAT | Orange | Firefight |
| CONSTRUCTION | Red | Construct, Haul, Clean, Warden, Handle |

The source's grouping is canonical even where vanilla categories differ. **The number inside a tag is a skill level, not a work priority.** O copy/paste and autosort are promised; U exact drag behavior, priority ordering algorithm, ties, right-click behavior and mapping to native priorities.

**18 October implementation decision (DEC-055):** the user supersedes hover opening with persistent click selection and a highlighted row. Preserve native scheduling score/ties, manual/checkbox cycles, Shift bulk actions, status controls, comparers and clipboard. Skill numbers are not priorities. Explicit Autosort adjusts only enabled, eligible skilled jobs using skill plus passion, with conflict-safe Undo; no-skill support work and disabled jobs remain unchanged. Full policy and limits: [Batch20 — local evidence omitted](LOCAL_EVIDENCE.md). This private implementation is not yet live accepted.

**Original required design decisions (resolved for this increment above):** explain left-to-right priority semantics; support tied priorities or explicitly model their order; define incapable/disabled work; preserve manual priorities and modded WorkTypeDefs. Define copy/paste payload, how it applies to different capabilities, what Autosort optimizes and whether undo is offered. These must be reviewable before gameplay changes.

**Latest user override (DEC-056):** upper assignment tags toggle on/off in one click. Each pawn has four full-width horizontal priority rows; dragging moves work between native levels with a pointer-following tag and a landing ghost. Same-tier order remains native. This replaces click cycling/Shift bulk in the custom editor, tall columns and adaptive bands. Checkbox mode retains one enabled lane. [Batch22 — local evidence omitted](LOCAL_EVIDENCE.md) records v0.20.3 checks, installation/startup and historical v0.20.2 evidence; user testing, no new live acceptance.

**Current user override (DEC-057):** return to the original compact single pawn row of enabled work in native order. The click-selected upper editor toggles work; a separate button next to each enabled tag cycles priority2→1→4→3→2. Four rows and dragging are removed. Number clicks activate native priorities if Off; opening stays read-only. [Batch23 — local evidence omitted](LOCAL_EVIDENCE.md) records implementation, checks and installation. User performs live testing.

**Priority-control refinement (DEC-058):** fixed leading colored badges align before every editor tag; disabled work keeps a non-clickable dash. The round numeric badge is distinct from the rectangular category-striped tag. Tooltips use named priority and plain click instructions without cycle notation. v0.20.5 is privately checked; live review pending.

**Steps and exit:** trace row rhythm, tags, icon buttons and six-column popover; establish a priority model that round-trips native data; implement selection and reorder only after semantics are settled; verify every work type. P13 tests incapable pawns, passions, zero skill, missing DLC work, long lanes, copying across pawn types and save/reload without priority loss.

### P14 Pawn panel shell and tabs

**Target:** all four pawn panels in B04 share a header: large pawn portrait, bold name, muted role, sex/age at right, then status glyphs. A tab strip below has Summary represented by four dots, Bio, Needs, Gear, Health, Social and Log with icons. The active tab joins the content surface; inactive tabs retain dark faces and fine borders. M inspection width is 688 px for the illustrated panels; their heights vary with content.

**Behavior:** selection changes update all data atomically; selected tab should persist according to an explicit rule. R: support pawns with long names, missing roles, children, non-human pawns, dead/downed pawns, DLC status icons and multiselection. Do not render a stale pawn portrait with another pawn's stats.

**Steps and exit:** make header/tab primitives once; measure name and age baselines, icon slots and tab widths; bind selector and native tabs; test changing pawn during each panel. P14 includes clipping boundaries and shared header regressions across P15–P18. Bio, Health and Log body designs are not supplied and are handled under P20.

### P15 Summary tab

[Summary — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04 `[989,669,1677,956)`. Left upper inset has Health, Mood, Food and Recreation meters, percentages right aligned; below are separate ACTIVITY and EQUIPMENT cards. The right inset has SKILLS in two columns, six rows each, with numeric skill levels, yellow passions and red zero values. Activity and equipment use live text and item thumbnail.

**Reference discrepancy:** the presentation caption mentions “rest,” but the illustrated fourth meter is **Recreation**. The visible panel governs visual reconstruction. Some illustrated meter lengths do not obviously equal their printed percentages; see the discrepancy register before defining fill math.

**Steps and exit:** build stat/meter adapter, fit distinct gradients, align labels/values, implement activity/equipment truncation and skill grid; test 0/100 values, multiple passions, no equipment, changed activity and a pawn lacking normal needs. P15 must never hard-code the illustrated stats into live play.

### P16 Needs tab

[Needs — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04 `[989,988,1677,1371)`. Wide left mood section has green meter and percent, then thought rows; positive numbers are green, negative numbers red, and a fine line separates groups. Small gray-blue dots mark newly appeared factors according to the source caption; cyan symbols mark particular ideological thoughts. Right column contains Food and Recreation above a rule, Beauty and Outdoors below. The body is the lighter content surface.

**Behavior:** preserve thought grouping, intensity, duration and tooltip detail. R: determine what counts as “new,” when the dot clears, stacked thoughts and unavailable need types. Need data must come from the pawn, including DLC/mod additions; do not omit rest or other real needs without a specified way to access them.

**Steps and exit:** trace column divider, bar lengths and thought rhythm; model thought identity/novelty; build list/scroll behavior and tooltips; test changing thoughts while open, long descriptions, missing needs and extreme mood. P16 includes scrolling without losing row identity or misapplying a tooltip.

### P17 Social tab

[Social — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04 `[989,1403,1677,1852)`. Ideology line with cyan emblem, name and percentage; role subtitle; Assign role, Romance and cog on the right. Left area is a three-column portrait-card grid, with name, relation and two directional opinion values. Right is an event feed with colored type icon, title, right-aligned timestamp and smaller description. The bottom visibly clips further cards and events.

**Details:** preserve two different opinion numbers; they are not duplicates. Visible examples include father, husband, sister, ex-lover, friend and a truncated acquaintance label. Events include romance attempt, chitchat, slight and deep talk. Card backgrounds, selected state, image crop, ellipsis and row gaps need separate measurements.

**Behavior:** bind real relations and interaction history; define independent or linked scrolling; retain role eligibility and native romance restrictions. R: support absent/dead/off-map relations, duplicate names, no ideology, missing expansion and long interaction text. A decorative Romance button is not completion.

**Steps and exit:** implement relation and event adapters; fit grid/feed; wire native actions; test directional opinions, role assignment, romance refusal and updates during play. P17 must not create social events merely because the panel repaints.

### P18 Gear tab

[Gear — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04 `[989,1883,1677,2280)`. Left column contains OVERALL ARMOR with Sharp/Blunt/Heat bars, CORE STATS with carried mass and capacity, and a temperature ramp with visible inner range and endpoint labels. Right has EQUIPMENT, APPAREL and INVENTORY, each with item thumbnail, name, quality/condition and right-aligned mass. Thin separators divide sections.

**Illustrated values:** Sharp 35%, Blunt 70%, Heat 70%; mass 6.05 / 35 kg; temperature −1°C to 40°C. These values are part of the reference fixture only. The steel knife, turret pack, clothing and meal have distinct thumbnails and units.

**Behavior:** preserve information-card access, equip/drop/consume or other native actions where applicable, sorting and encumbrance rules. R: support empty lists, many items, nested containers, extreme temperature range, metric/other preferences and modded equipment.

**Steps and exit:** map actual armor/mass/temperature calculations; construct rainbow ramp independently from its selected interval; fit item rows and scrolling; validate aggregates against game data. P18 fails if the redesign changes armor calculations or displays a decorative ramp with incorrect endpoints.

### P19 DLC information

[DLC information — local evidence omitted](LOCAL_EVIDENCE.md)

**Target:** B04. A compact strip holds five colored expansion symbols; the selected expansion opens a larger card below. The shown Anomaly card has a preview image fading into the panel, expansion logo, three explanatory columns and a separated footer with external Steam link and ownership note. Source caption calls for video clips instead of static images.

**Behavior:** selected/unselected/hover states, owned/unowned, installed/enabled and unavailable-media states are distinct. R: source ownership from the game; link the correct expansion; preserve opt-in settings for motion/audio and a static fallback. Other expansion copy/layout is an extension unless visible in additional evidence.

**Steps and exit:** trace five marks and selected-card connection; map ownership and media; implement image-to-content fade and three-column text layout; test every installed/absent combination without requiring a purchase. P19 includes no autoplay audio surprise, no repeated loading per frame, and no incorrect owned/unowned label.

### P20 Unspecified screens and extension states

**Standing instruction:** infer missing states from the nearest demonstrated pattern under the [inferred-design policy](05-inferred-design-policy.md), record them in the [inference register](inference-register.csv), and review them through ordinary team integration. Absence from the concept alone is not a reason to defer the work or request user approval.

The reference names many screens without showing their bodies: Bio, Health, Log, Schedule, Assign, Animals, Wildlife, Research, Quests, World, Factions, Ideoligions, History, most Architect controls, Options, Mods, Credits, learning helper and most modal dialogs. Their launcher styling is in scope; a detailed pixel target for their contents is absent.

**Required approach:** inventory every reachable screen; retain functional access; apply only established shared primitives where safe; give each newly designed body its own extension specification. Mark it “reference absent,” not “pixel match passed.” Do not hide a native screen or remove its features to make the overhaul appear complete. Tooltips, dropdowns, context menus, scrollbars, drag previews, focus rings, empty states, errors and mod settings need the same inventory treatment.

**BOTTOM30 first pass:** the user requests Schedule, Assign, Animals, Wildlife, Research, Quests, World, Factions and History. World keeps its actual globe/navigation and receives the existing modern HUD, selection and action styling. [Batch29 — local evidence omitted](LOCAL_EVIDENCE.md) records inferred body compositions, shared animated translucent shells, native callback preservation and remaining acceptance. These private implementations are not reference replicas or live-accepted screens. Ideoligions and other unrequested bodies remain separately tracked.

**Exit:** no unreachable vanilla feature, no accidental global recoloring that makes another mod unreadable, and no unreviewed invented screen counted as a reference replica.

### P21 Runtime integration and settings

**Target:** RimWorld 1.6 installation supplied by the user. The prior runtime reported 1.6.4871 rev591; Version.txt and runtime revision differed, so every test run must record the actual loaded assembly version/hash. Harmony and installed expansions also require a run manifest.

**Architecture:** keep reference tokens/assets, layout, game-data adapters, interaction state and rendering separate. One module owns each native surface. Use native commands and data; do not duplicate simulation logic. Explicitly scope Harmony patches, preserve custom subclass behavior, and restore GUI color/font/anchor/matrix after drawing. Create textures and glyph atlases outside repaint loops. Avoid double-drawing native and replacement controls.

**Persistence:** version settings for groups, collapsed sections, trends and display preferences. Loading old settings must not corrupt saves. Provide a recoverable master switch and per-module switches; settings changes must clean up overlays and input capture. Temporary test saves remain separate from the user's colony.

**Exit:** load-order diagnostics, graceful fallback on incompatible versions or missing dependencies, no save corruption, no unintended gameplay modifications, no repeated error logging and no leftover handlers after disabling a module. This is an integration project, not permission to change game files during the specification phase.

## 4. Discrepancy and uncertainty register

| ID | Issue | Required resolution before relevant sign-off |
|---|---|---|
| U01 | Font family and rasterization unknown | Candidate glyph comparisons, frozen font/atlas and exact baseline measurements |
| U02 | Original vector icon paths unavailable | Compare native assets or reconstruct silhouettes; record source and per-state optical bounds |
| U03 | Layer alpha, shadows, blur and blend recipes unknown | Match pixel profiles and corners; test over more than one background; distinguish raster match from recovered recipe |
| U04 | Missing hover/pressed/focus/disabled states | Capture video evidence where available; specify unshown extensions separately |
| U05 | Motion easing and durations unmeasured | Frame-indexed evidence and event-relative timelines; clip duration is not a timing token |
| U06 | B01/B04 presentation placement versus game placement | Calibrate component scale and establish screen anchors from full-screen evidence |
| U07 | Exact original colony and camera unavailable | UI-region comparison plus a deterministic equivalent fixture; no whole-frame equality claim yet |
| D01 | Summary caption says rest; panel says Recreation | Use visible Recreation in canonical panel; retain access to actual rest need elsewhere |
| D02 | Some illustrated fill lengths and printed percentages appear inconsistent | Measure track/fill bounds, document discrepancy and choose an explicit visual-fixture versus live-data rule; never falsify live values to force a screenshot |
| D03 | Comparison examples may not equal installed-version game stats | Keep source-value render fixture separate from real-item behavior tests |
| U08 | Work tag order, ties, autosort and manual-priority mapping underdefined | Write and approve a behavioral contract before changing work assignments |
| U09 | Meanings of several tiny status glyphs unclear | Match against actual game states; do not attach guessed tooltips or actions |
| U10 | Unshown screens, responsive layouts and translations | Apply 05-inferred-design-policy.md and inference-register.csv; extend the inventory as new cases arise; no unsupported pixel-fidelity claims |

A strict whole-product “1:1” claim remains conditional on resolving these issues. The plan does not lower the requested target: it makes missing evidence and contradictions visible, so they cannot become unnoticed compromises.

## 5. Current prototype gap audit

The prototype proves that selected RimWorld drawing paths can be replaced. It does not prove that the reference has been reproduced. Its generic margins, tiny-font navigation, substitute icons, ungrouped colonists, oversized clock, resource names in the compact state, severity-based alert grouping and click-based tools reveal require rework against the sources above. Menu/save/storyteller screens, real work tags, comparison behavior, detailed pawn tabs and DLC presentation require dedicated projects.

The previously reported 216 layout arithmetic checks and limited in-game smoke checks are historical prototype evidence only. They do not satisfy the new pixel, interaction, persistence or regression requirements. No screenshot generated from v0.2 should become the approved golden reference merely because it already exists.

The next implementation phase starts with evidence calibration, typography, symbols, shared primitives and the test fixture. Individual feature work orders follow only when their inputs and acceptance criteria are concrete.

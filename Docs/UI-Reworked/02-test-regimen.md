# In-game acceptance and regression regimen

> **Separate completion records:** record functional results and remaining functional fixtures independently from visual completion. The [project register](project-register.csv) and [visual backlog](visual-completion.md) retain both. A build, smoke pass, or usable increment must not change visual status or final acceptance. The main menu and all current feature visuals remain unfinished until their own evidence gates are satisfied.

> **Current iteration policy:** follow the user's [complete review and batched correction direction](04-team-workflow.md#current-delivery-policy--user-direction-17-october-2026). Every implemented scope receives a comprehensive visual and functional review, a complete discrepancy list, a consolidated correction pass and another comprehensive review. Fewer expensive capture sessions means more findings assessed per session, not less scrutiny. Exact pixel certification and exhaustive matrices below are separate acceptance milestones; ordinary screen review must not wait for them.

Revision 5 · Acceptance plan; executed results are recorded separately

Foundation wave 2 results are in the [integration report — local evidence omitted](LOCAL_EVIDENCE.md), including final-DLL provenance, observed physical mapping and the fractional-scale seam regression. [Wave 1 evidence — local evidence omitted](LOCAL_EVIDENCE.md) remains historical. Unlisted cases below remain planned; no blanket execution or acceptance is implied.

Every feature must pass separate visual, behavioral, integration and persistence gates. A build that resembles the concept but changes the wrong pawn's work, loses a save selection, obscures alerts or breaks after adding another panel fails. A fully functional panel with substituted typography also fails its fidelity gate.

This regimen accompanies the [design specification](01-design-specification.md) and [work-order plan](03-work-order-plan.md). Test IDs are permanent. Evidence is attached to the implementing work order and release manifest; the specification does not claim these tests have already run.

## Required implementation and correction campaign

Apply this loop after every bounded implementation batch, including later corrections. Several independent features can share the build and game session; each affected screen still receives a complete review.

1. **Implement the agreed scope.** Inspect its reference and existing findings first. Specify the screens, relevant states and native actions to exercise. Implement the whole batch with cheap, meaningful build/static checks before spending a live session. Do not treat a generic approximation of source-shown details as sufficient.
2. **Inspect the entire implementation.** Capture the integrated build in one planned session and exercise the scoped behavior. Compare every region of each affected screen against the reference: overall bounds, proportions, margins, hierarchy, alignment, row rhythm, typography size/weight/color, icon silhouette/stroke/color, surface colors, borders, gradients, shading, clipping, overflow and control states. Check data, input, navigation and persistence where relevant. Reuse the captures for all these checks. Review source-absent states against documented inference rules.
3. **Finish a detailed problem list before fixing.** Record all visible discrepancies and functional failures together, including fine details. Each finding needs an ID, screen/state/region, expected versus actual result, source and capture evidence, severity, owner, status and verification condition. Distinguish observed mismatches from uncertain causes and missing evidence. A blocker that prevents inspection can be repaired first, but the unreviewed scope stays explicitly pending. The [W02 design audit — local evidence omitted](LOCAL_EVIDENCE.md) demonstrates the expected level of scrutiny; its findings are the starting inventory for the next W02 campaign.
4. **Fix the batch.** Address the actionable findings together, parallelizing independent owned paths when useful. Include small corrections in this pass rather than assigning each its own capture cycle. Keep genuine blockers or intentional deferrals visible with a reason, owner and next checkpoint; do not quietly move them to an unspecified refinement phase or advance to another feature to avoid them.
5. **Recheck the complete result.** Test the integrated corrected build in another shared session. Verify every claimed fix against its condition, repeat the affected interactions and inspect the whole screen against the reference afresh. Look for new or previously missed discrepancies as well as regressions in dependent surfaces. A changed source file is only fixed-pending-verification, not verified. Reopen failed findings, add new findings and repeat steps 3–5 as a batch.
6. **Record the campaign outcome.** Retain the build/hash, captures, functional results, finding dispositions and remaining coverage. Finish only when the scoped findings are verified or any unresolved exceptions are explicitly recorded and surfaced. A campaign with unresolved findings is partial; it must not be described as visually complete. Functional progress, visual completion and final acceptance remain separate. No build or smoke check automatically closes a campaign, promotes a baseline or justifies advancing past unresolved design work.

Finding statuses: **open → fixed-pending-verification → verified**, with **reopened**, **blocked** and **explicitly deferred** as needed. Keep old evidence and findings when updating status. Deferrals retain both their reason and the condition that brings them back into the next campaign.

For ordinary comparison, document relative scale and use diagnostic normalization when screenshots differ in size; retain the raw images and do not present a resized diagnostic as pixel-equality proof. Different live names or values are not automatically defects, and missing source detail is not license to claim an exact match. Formal acceptance uses the frozen configuration and comparison procedures below. Never substitute fake data in ordinary play to make a screenshot look complete.

## 1. Acceptance classes

| Class | What is compared | Passing condition |
|---|---|---|
| Reference reconstruction | Canonical UI crop against original PNG evidence | Every included pixel identical for an exact-match claim; unresolved differences remain open |
| Inferred design | Unshown state against a recorded extension specification and accepted game baseline | Consistent visual rules, correct native behavior, complete access to information/actions and unchanged canonical states; never claimed as source-pixel equality |
| Deterministic regression | Current game capture against an approved capture from the same harness/environment | Zero changed pixels in deterministic regions, no moved boundaries or new clipping |
| Motion reconstruction | In-game transition against event-relative video frames | Geometry, order, color progression and timing match the documented sampled frames; uncertainty from compression/sampling remains explicit |
| Functional | Input and resulting game/UI state | Specified result occurs exactly once, native semantics preserved, no unintended action |
| Compatibility | Supported resolution/DLC/mod configuration | Controls reachable, text readable, no overlap/input loss, no exceptions; exact reference match only at the canonical configuration |
| Release readiness | All scoped projects and regression suite | No open critical/high defects, no unexplained screenshot changes, all required evidence present |

“Visually close,” a high SSIM score, compilation success and a single screenshot are not substitutes for these conditions. A video-only state cannot honestly receive a lossless PNG-equality certificate. It receives a motion evidence result with the source limitations recorded.

## 2. Deterministic test environment

### 2.1 Run manifest

Every run records:

- Game version, revision, executable and Assembly-CSharp hashes; Unity version; Harmony version; mod build hash and source commit or source archive hash.
- Active mod IDs and order; expansion IDs, versions and activation state; all test-relevant mod settings.
- Output client-area width/height, UI scale, Windows DPI scaling, display mode, graphics API, color space, screenshot path and any rendering overrides.
- Language, temperature/number preferences, font file/atlas hashes, texture hashes, filtering and pixel rounding settings.
- Fixture save and scenario hashes, map ID, camera position/zoom, selected IDs, current tab, expanded groups, scroll positions, pointer location, focus, tick and clock state.
- Test ID, input sequence, capture frame number, timing source, logs, expected result and actual result.

The canonical full HUD is B02 at **1920 × 1080 client pixels**, English, with the layout calibrated to that raster. Do not include the Windows title bar in the capture. UI scale must be chosen and frozen during calibration; do not assume the user's earlier 1.5 setting matches the artwork. Presentation-board panel dimensions are calibrated separately before conversion into in-game coordinates.

### 2.2 Fixture isolation

Create a dedicated savedata folder and copied test saves. Never run automated destructive/save tests on the user's live colony. Record the original save hash before and after fixture creation. Use recoverable test files for save/load/delete cases. Disable autosaves only in the isolated test environment when needed for deterministic capture, and record that setting.

Use two complementary fixture families:

1. **Reference-value render fixtures.** Inject controlled view-model data into the same renderer used in game. These can reproduce the exact names, labels, percentages and layout from B01–B04. They isolate visual defects from simulation changes. They do not prove live data binding.
2. **Live game fixtures.** Actual pawns, items, needs, relations, work priorities, letters, saves and DLC state. All displays and actions must agree with the game. These prove correctness even when the conceptual example values do not occur naturally in this game version.

Do not ship fake gameplay data to obtain a clean screenshot. Reference injection belongs in a development/test mode and must be unavailable in ordinary play. A panel is complete only after both fixture families pass.

### 2.3 Minimum fixture inventory

| Fixture | Controlled content |
|---|---|
| FX01 Menu/setup | All menu actions, each storyteller/difficulty/save mode, owned/unowned DLC cases |
| FX02 Save browser | Source-like list, missing preview, duplicate colony names, corrupt/old save, Unicode/long name, empty result, 200-save list |
| FX03 Canonical HUD | Nine illustrated pawn slots, three groups, listed resources, two letters, open COLONISTS and closed other issue groups, clock/date/weather |
| FX04 Dense HUD | 30/100 pawns, many resources, 30 alerts/letters, long labels, overlays and inspection panel open |
| FX05 Work | Seven source-like rows, incapable pawn, tied priorities, zero skill, one/two passions, mod-added work type |
| FX06 Pawn tabs | Summary values, needs/thoughts, relationships/events, gear; a second pawn with contrasting data to expose stale state |
| FX07 Comparison | Two source-like item view models; live weapons, different stat sets, lower-is-better stats, ties, missing units, destroyed item, 3+ columns |
| FX08 Search | Repeated names, stacks, buildings, damaged items, off-map/destroyed entries, zero results, large map |
| FX09 Persistence | Groups, collapse states, trends and preferences across save/reload, disabling/re-enabling and setting-version upgrades |
| FX10 Missing features | Core only; each relevant expansion unavailable; no map; world/caravan; no pawn selected; empty inventories and logs |

Fixture generation must be deterministic and versioned. If a native stat formula changes with a game update, update the live expectation with evidence; never alter the reference PNG to conceal it.

## 3. Visual comparison procedure

### 3.1 Source preparation

1. Verify source SHA-256 values against source-manifest.json.
2. Define a reference-region record: source ID, exact crop, nominal component origin, scale calibration, component/state ID and evidence class.
3. Trace the component's opaque/edge region. Exclude surrounding blueprint/caption/map pixels only with an explicit source mask. Never mask the UI border, icon, text, gradient, clipping edge or error area.
4. Resolve layer ambiguities, fonts and source contradictions relevant to that state. If unresolved, record the state as incomplete rather than inventing an exact token.
5. Freeze a canonical capture configuration and data fixture before first acceptance.

### 3.2 Capture protocol

Launch the test build in the isolated environment. Load the fixture, set the exact camera/UI state, pause simulation if appropriate, freeze UI timestamps/animations only through the test harness and put the pointer at a recorded position. Wait for assets/fonts to be ready and layout to settle. Capture lossless PNG from the game render target or a verified client-area capture path.

For a hover test, the pointer must be in a documented hit region. For a normal-state test, move it away without triggering another surface. For motion, capture an ordered series at fixed event-relative times without freezing the transition. Record the actually presented frame, not a delayed OS screenshot timestamp alone.

Save the full frame as well as the component crop. When using a development-only UI render target, capture RGBA or the UI against known backgrounds to diagnose compositing. The isolated output must use the actual game renderer, not an HTML recreation. Final proof includes the real full game screenshot.

### 3.3 Comparison and metrics

Use fixed coordinates. Do not auto-align, resize, blur, recolor or individually shift components before final comparison. These transformations hide spacing and glyph defects. A diagnostic registration preview may exist, but its transform must be disclosed and cannot be the acceptance comparison.

For each included pixel and RGB channel compute the absolute difference. Record:

- Mismatched pixel count and fraction, with denominator equal to the included UI pixels.
- Maximum channel error, mean absolute channel error and total error.
- Separate mismatch counts for text, icons, panel faces, border/corner pixels and gradients.
- Bounding box of differences and connected regions, plus geometry changes to anchors/baselines.
- A raw difference PNG, amplified heatmap, 50/50 overlay and side-by-side source/result at 100% scale.

Exact static acceptance requires zero mismatched pixels in the agreed deterministic region. A diagnostic “within 1 RGB level” statistic may help find compression/rounding differences, but must not be relabeled as an exact match. Large flat backgrounds must not dilute a wrong icon or font into an impressive aggregate percentage.

Video comparison requires the same controls and event-relative states. Use frame indices and documented source frame rate where available. Never infer the frame rate solely from a duration ending in .916666. Obtain a clean frame sequence or record this measurement as unavailable. Compression, sampling and browser rescaling prevent claiming subpixel or millisecond certainty from the presently observed playback alone.

### 3.4 Review and baseline policy

The source PNGs are immutable. Approved game goldens are versioned separately and identify the source evidence they implement. A feature change never automatically regenerates goldens. Every changed golden requires a visible before/after/diff and a reason tied to a specification change or corrected implementation.

Shared typography, icons, primitives, scale transforms or GUI state changes invalidate every dependent golden. Re-run those states before merging or installing the change. A change limited to search sorting does not require unrelated glyph measurements again, but it does require search, navigation and live-selection regression.

## 4. Component acceptance matrix

Each row is a test family. A work order expands it into executable cases with exact inputs and expected results. All listed cases are **planned**, not passed.

| Test family | Visual states | Required behavior and failure probes |
|---|---|---|
| P01 Main menu | Rest, each hover/press/focus, destructive action, return | Every route opens once; native confirmations; no duplicate invisible controls |
| P02 Saves | Selected/unselected tabs/rows, search, empty, missing preview, long title | Correct file ID; metadata accuracy; cancellation; corruption; filter changes; no preview writes |
| P03 Storyteller | All three portraits selected, each difficulty, custom, both playstyles | Exact native selections; Back/Next persistence; custom editor; curve never changes mechanics |
| P04 HUD/dock | Canonical, each active tab, world, overflow, modal overlap | All buttons and shortcuts; architect; no double activation or click-through |
| P05 Colonists | Three groups, selected/multiselected, status changes, add/empty/overflow | Select/jump/reorder/group persistence; death/map changes; 30/100 pawn stress |
| P06 Resources | Compact, expanded, positive/negative/no history, large quantities | Correct counts/map; seven-day history; pointer transit; definition changes |
| P07 Clock | Pause/1x/2x/3x, weather, long date, preferences | Speed/shortcut parity; forced pause; map/world; no stale temperature |
| P08 Issues | Each group open/closed, empty, mixed severity, many rows | Correct native action; custom alert override; target changes; critical visibility |
| P09 Letters | Each type, arrival/dwell/exit frames, stack reflow, long text | Correct letter/target; queue identity; one action; timestamp and pause behavior |
| P10 Tools | Closed, hover-open, launcher expansion/retraction frames, submenu slide frames, each toggle, keyboard, unavailable | Separate shell and content transitions (DEC-045); panel clipping and hidden-control input; pointer bridge; native overlays; no change on hover; state persistence |
| P11 Search | Empty/typing/populated/scrolled/zero/clear, damaged result | Correct target; cancellation; stale entries; large-map cadence; no click on clipped row |
| P12 Compare | Two/three columns, settings, ties/differences, long labels | Higher/lower/neutral stat rules; units; add/remove/cancel; destroyed item |
| P13 Work | Filled/outline/disabled tags, six-column popover, copy/paste, autosort | Skill versus priority separation; ties/incapacity/modded type; round-trip and save/reload |
| P14 Pawn shell | Each tab, changed pawn, long names, all header symbols | Atomic pawn change; tab persistence; keyboard; correct symbol-to-state mapping |
| P15 Summary | Each gradient, 0/full, passions/zero skill, no equipment | Live data correct; no hard-coded values; inaccessible needs remain reachable |
| P16 Needs | Positive/negative/new/expired thoughts, long list | New-dot lifecycle; stacking; DLC needs; live update; tooltips follow row identity |
| P17 Social | Portrait grid, directional opinions, feed, empty/clipped/long text | Relations and event order; romance/role restrictions; no event on repaint |
| P18 Gear | Bars/range, empty/full lists, long names, extreme values | Native armor/mass/temp; item actions; changing equipment; units and scroll |
| P19 DLC | All marks, selected, owned/unowned, media fallback | Correct ownership/link; no missing-media crash; no repeated load; no unexpected audio |
| P20 Extensions | Every reachable native screen and modal | No feature removed; no invented design labeled a match; other mods remain usable |
| P21 Integration | Modules/master on/off, fallback screens, settings UI | Load-order/version checks; no stale patches/input; migration; log and save health |

All interactive families also cover pointer down/up across boundaries, dragging outside, double-click, right-click where supported, keyboard focus, Escape, modal interception and changing game state while the panel is open. UI labels and tooltips must describe the actual action.

## 5. Tests below the screenshot layer

### 5.1 Data and behavior tests

Use focused tests for real decisions: comparison ordering and stat direction, resource trend calculations across insufficient history, work-priority conversion and round-trip, grouping lifecycle, save filtering/selection identity, thought novelty, search invalidation, date/number formatting and settings migrations.

Property tests should exercise invariants: UI repaint does not modify gameplay data; converting priorities to tags and back preserves the native assignments; sorting preserves all items and deterministic ties; filtering cannot return a deleted item; collapsing a group does not change alert state; equal values never receive contradictory winner highlights.

Avoid tests that merely duplicate a rectangle formula and assert its own output. Geometry tests must instead establish meaningful constraints: no forbidden overlap, all hit targets inside the declared panel, scrolling excludes clipped rows, and one coordinate maps to no more than one actionable foreground control.

### 5.2 Input and GUI-state tests

Instrument commands in the development harness. Record semantic event count and target ID; a click must call the intended command once, even when the same frame has Layout/Repaint/input events. Verify GUI color, font, anchor, enabled state, clip stack and transform are restored after each module. A renderer error must not tint or offset unrelated windows.

Test focus and pointer ownership: hover changes presentation only; opening a popup captures only its intended region; closing it restores normal map input; Escape closes the correct topmost surface; a key used in a search field must not also trigger a global tab shortcut.

### 5.3 Persistence and save integrity

Exercise save/load across fresh install, updated settings schema, disabled modules and missing DLC. UI-only fields need a versioned migration or safe default. Verify pawn/work/item gameplay data against pre-action snapshots, allowing only the intended mutations. Byte-identical saves are not a valid expectation after advancing simulation; test semantic fields and separately check that untouched original fixture files remain untouched.

Test bad/missing settings, obsolete pawn IDs, deleted groups, corrupt trend history and conflicting mod IDs. Failure should leave the game usable and produce a clear single diagnostic rather than continuous exceptions.

## 6. Integration and compatibility matrix

The canonical reference configuration is one fixed case. Functional coverage should also include these planned variants:

| Dimension | Cases |
|---|---|
| Resolution | Canonical 1920×1080; 1920×1200 matching the user's earlier window; 1280×720; 2560×1440; ultrawide 3440×1440 |
| UI scale | Canonical calibrated scale, 1.0, 1.25, 1.5, 2.0 where supported |
| OS DPI | 100%, 125%, 150% while keeping exact client pixels recorded |
| Display | Windowed and fullscreen; resize while open; minimize/restore |
| Expansions | Core only, user's installed combination, each relevant missing expansion, supported full combination |
| Language | English fidelity fixture; long Latin strings; Unicode/non-Latin fallback; right-to-left only if claimed supported |
| Game state | Map, world/caravan, multiple maps, paused/running, loading, no selection, modal open |
| Mod composition | Harmony+this mod; a mod adding a main tab, work type, resource/stat and alert; representative UI patch conflict |
| Colony size | Small canonical, medium, large; large save list and inventory |

Do not claim every Cartesian combination has been tested. Use full coverage for the canonical case, risk-based pairwise combinations elsewhere, and explicitly list supported combinations in the release. UI mods that replace the same surface require a compatibility policy or graceful disable/fallback, not a guarantee based on load order alone.

## 7. Performance and soak checks

Capture a baseline with the same fixture and game environment before enabling the overhaul. Measure UI CPU time, allocation rate, texture/material count and frame-time distribution over a fixed camera and input sequence. Repeat with dense resources, alerts, portraits, search and comparison.

Set a numerical performance budget during F04 calibration based on the target machine and baseline; no arbitrary budget is represented as already accepted. Track median, p95/p99 and worst frame, not average FPS alone. No new texture/font construction or full-world search should happen on every repaint.

Run a documented soak through repeated open/close, selection changes, save/load, module toggles and map transitions. A planned starting sequence is 1,000 deterministic UI operations followed by a longer live-play soak, with counts/duration fixed before the run. Check memory trend, texture growth, input leakage and logs. A successful soak reduces known risk; it cannot prove the absence of every bug.

## 8. Incremental delivery and regression gates

1. **G0 Evidence ready:** source and crop recorded for shown states; inference ID, source analog and concrete design contract recorded for unshown states; unresolved details enumerated, affected states named and behavioral contract concrete.
2. **G1 Primitive verified:** typography, icons, edges and gradients match isolated source fixtures; no arbitrary replacement assets.
3. **G2 In-game visual:** renderer runs in RimWorld and source/actual/diff evidence exists for every required canonical state. Inferred states have extension-specification/actual evidence and separately identified regression baselines.
4. **G3 Functional:** live adapters and commands pass component cases; original gameplay semantics preserved.
5. **G4 Integration:** module tested with all previously accepted modules; shared dependencies rerun; no unexplained golden changes.
6. **G5 Release:** clean install/update/disable paths, compatibility subset, persistence and performance checks pass; evidence manifest complete.

Work may proceed iteratively, but a failed earlier gate cannot be silently skipped. A feature can be “implemented, fidelity incomplete” or “visual fixture matched, live binding incomplete”; neither is “done.”

Critical defects include corrupted/lost saves, game crashes, unusable input or unintended destructive actions. High defects include broken native commands, wrong pawn/item targets, missing critical alerts or persistent major layout failure. Visual deviations are separately tracked; a visible mismatch need not crash the game to fail the replica requirement.

## 9. Evidence package per work order

Keep a small, reproducible folder containing:

- Requirement IDs and source regions; build/run/fixture manifests and exact reproduction steps.
- Source PNG/crop reference and hash; actual full game PNG; actual component crop; diff/overlay/metrics.
- For motion: event/frame manifest, ordered captures and timing comparison.
- Functional case table with inputs, expected/actual state, command target/count and status.
- Relevant logs, performance comparison, compatibility cases and open defects.
- Regression selection with reasons; changed goldens with explicit change rationale.

Statuses are **Not run**, **Pass**, **Fail**, **Blocked by evidence** and **Not applicable with reason**. A missing screenshot or unperformed check cannot be reported as Pass. Subsequent additions are accepted only after the affected prior states still pass.

## 10. Inferred-state coverage

Every work order checks the [extension inventory](05-inferred-design-policy.md) and lists the relevant IDs from inference-register.csv. A new case becomes a recorded design/test obligation, not an invisible local workaround. A missing concept image does not block routine inference under the user's direction.

| Extension test family | Required cases | Failure examples |
|---|---|---|
| EX-GROUP | 0/1/3/4/8/20 groups; an oversized group; long/duplicate labels; ungrouped/new pawn; map/death/removal; saved order | Hard three-group cap; unreachable pawn; changed work priorities; canonical cards shrink when another group is added |
| EX-NOTIFY-MAP | Loaded letter/message/alert catalog; code-generated event audit; known family mappings; mod/unknown fallback | A known event silently lacks a mapping; classification depends on English title text; fallback misrepresents severity |
| EX-NOTIFY-BEHAVIOR | Threat-bearing quest; choice letter; rejection/caution/silent feedback; two events with the same label; simultaneous arrivals; expiry/history/persistence | Urgent quest looks harmless; action or sound fires twice; transient message becomes permanent; queue reflow changes the clicked target |
| EX-NEW-CONTENT | New work/resource/stat/need/tab/relationship/item definitions; unavailable DLC; missing media/assets | Hidden native feature; crash on unrecognized definition; meaningless placeholder pretending to be the correct icon |
| EX-OVERFLOW | Counts and long text across every repeated-content panel, alternate scale/resolution/localization, keyboard access | Clipped actionable content; microscopic fonts; overlap with protected HUD areas |
| EX-CONTINUITY | Resume with an unfinished worker, dirty worktree, changed integration commit or stale game DLL | Lost work, duplicate deployment, tests attributed to the wrong build, accepted status reset by document generation |

For notifications report three separate quantities: discovered supported identities, identities with explicit or justified family mappings, and identities whose required runtime cases were executed. Track unknown fallback coverage separately. Static XML counts do not supply the denominator for full runtime completion. Each update to supported game/DLC/mod content triggers a catalog comparison and appropriate new cases.

A proposed extension layout is not a passed test. Acceptance requires its actual visual specimen, live behavior and canonical-state regression evidence. The lead can adopt and verify routine inferred designs; this gate does not require the user to review every small decision.

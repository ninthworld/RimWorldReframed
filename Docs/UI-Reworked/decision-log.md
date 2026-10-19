# Cross-project decision log

Record a new entry when a durable design/architecture choice changes. Include status, rationale, source, affected IDs and tests. Never overwrite a prior decision silently; mark it superseded and link its replacement.

| ID | Date | Status | Decision and rationale | Affects |
|---|---|---|---|---|
| DEC001 | 2026-10-17 | Standing user direction | Match shown concept UI precisely; use its established visual patterns to infer states it does not show. Routine inferences proceed without repeated user approval. | All projects, inference policy |
| DEC002 | 2026-10-17 | Operating structure documented | Use a capable lead, bounded parallel workers and independent review; preserve one integration owner and one live-game controller. | Team workflow, all work orders |
| DEC003 | 2026-10-17 | Initial model recommendation | Astra xhigh lead; Sol high substantial workers; Luna medium/high bounded work. Benchmark accepted outcomes; availability/capacity must be rechecked. | Dispatch and usage planning |
| DEC004 | 2026-10-17 | Superseded by DEC008 before setup | Original recommendation placed the primary repository outside Steam. The user selected the installed mod directory instead. Worktree isolation and separate outputs remain required. | F05, integration |
| DEC005 | 2026-10-17 | Required extension invariant | Three illustrated colonist groups are examples, not a maximum. Additional groups remain reachable without shrinking canonical cards arbitrarily or hiding pawns. | P05, EXT001–EXT004 |
| DEC006 | 2026-10-17 | Required extension invariant | Bzzzt/quest/research examples establish reusable notification treatments; all supported event channels need a semantic mapping and unknown-event fallback. | P08–P09, EXT005–EXT010 |
| DEC007 | 2026-10-17 | Required acceptance distinction | Compare shown states to source pixels; compare inferred states to their documented design and approved in-game baselines. Never label an inferred screen an exact source match. | G0–G5, all extensions |
| DEC008 | 2026-10-17 | User-directed; Git initialized | Keep the authoritative project at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimWorldReframed` on main for direct local builds. Only the integrator writes/builds there; worker worktrees remain outside Mods. Preserve the prototype, ignore generated binaries and restart the game after DLL changes. | Repository setup, F05, all integration |

Proposed details such as the overflow drawer geometry and final notification accent values remain in the inference register. Recording this policy does not claim those designs are implemented or visually accepted.

## Foundation wave 1 decisions

- DEC009 (implemented): first increments may establish safe runtime/test infrastructure before F01/F02 are exact, but full F03 acceptance still depends on them. Vanilla Tiny and inferred states remain labeled provisional; this does not relax fidelity gates.
- DEC010 (implemented): centralize GUI state restoration and geometry bounds; initialize reusable textures at startup. The live exception probe and geometry tests cover this contract.
- DEC011 (implemented): retain native commands and owned Harmony surfaces behind exact-member startup preflight. Diagnostics preserve native event/channel values; zero bespoke notification mappings are claimed.
- DEC012 (implemented): each Unity capture records DLL hash and an immutable runtime XML/hash. Exact repeatability is a smoke result, not source or golden approval. Keep unknown capture/DPI/performance fields explicit.
- DEC013 (implemented, EXT018): provisional hover #2D3847 and pressed #181F28 extend the blue-gray button family; disabled presentation reduces contrast while behavior blocks dispatch. These are inferred design parameters, not recovered art. Focus and the full state atlas remain open. Keep comparison-best yellow context-specific; do not reuse it as a generic threat accent.

## Foundation wave 2 decisions

- DEC014 (user direction, implemented): reuse existing game fonts only. Clone native styles for calibration; do not download, create or bundle fonts. Asset/fallback names are evidence, not proof of actual glyph identity. F01-B compares 35 candidates and five source strings; no exact font is selected.
- DEC015 (implemented): capture the actual rendered frame, hash completed PNG/XML/metadata, and verify frame identity before attaching candidate metadata. Measure physical coordinates from four fiducials. At scale 1.5 the reported GUIToScreen origin differs from the physical image; never derive the acceptance crop from that unverified origin. Automatic unrun GUI probes remain incomplete, separate from manual PASS evidence.
- DEC016 (user readability correction, implemented v0.4.1): button labels use the native Small game style, matching the smoother status text requested by the user. Dock widths and height follow its metrics. Compact metadata remains Tiny. This is not a claim that Small matches every concept-art font role.
- DEC017 (bug fix, implemented v0.4.2): draw rounded fills with the installed Unity single-draw rounded material. The prior multi-piece fill caused fractional-scale seams. The same predetermined sideband check fails the old 1.5× capture (164 pixels) and passes the final 1×/1.5× captures (zero). Corner antialiasing remains provisional pending source comparison; complete source visual acceptance stays open.

## HUD feature batch decisions

- DEC018 (implemented v0.8.0, EXT035): preserve the native 35-unit bottom strip and native MainButtonsOnGUI hotkeys; replace only dock drawing. Keep Menu and tools separate, retain native custom workers and give remaining actions named overflow access. Pure overflow geometry and named live routes passed; live overflow remains open.
- DEC019 (implemented v0.8.0, EXT036): retain native speed/event semantics and perceived weather while moving them into compact independent surfaces. Use shared occupied areas and native fallback; prior drawn extents can conservatively retain fallback in crowded states. Existing fonts, native speed symbols and weather text are adequate for this first functional increment; visual matching follows later.

## Reference structure correction decisions

- DEC020: functional progress, visual completion and final acceptance are independent fields for all 21 projects. A passed route or build never means the source layout is finished.
- DEC021: the user rejected the standalone colonist grouping system and requested placeholders until more UI exists. Remove its editor/model/persistence; retain real native pawns under temporary source headings. Defer work-derived semantics. The old EXT002 proposal is superseded.
- DEC022: feature-first means implement known major source design details and batch verification. It does not mean defer panel proportions, labels, typography hierarchy/brightness, icon silhouettes, grouping or functional layout. Fine raster tuning can wait. This resolves the earlier overcorrection toward generic UI.
- DEC023: Language and Links remain available inside the entry-menu panel, above Options/Mods, per explicit user direction. This added row is user-directed, not shown in the concept. Quit uses a red-family border instead of the shared blue highlight; enabled button text/icons are bright.
- DEC024 (user clarification): every implementation follows a complete campaign: implement the bounded scope, inspect the entire result against the reference and scoped functional requirements, list all discrepancies, fix the batch, then recheck the entire result for resolved, new and missed issues; repeat as needed. Expensive testing is batched, not reduced to a functional smoke pass. Fine details belong in the discrepancy inventory and consolidated correction pass, not an automatic later-refinement bucket. Explicit exceptions retain reasons, owners and next checkpoints; unresolved campaigns remain partial. This clarifies DEC022 and supersedes any earlier wording that allowed advancing on rough visual similarity alone. The W02 audit establishes the review standard and remains the next correction inventory; this documentation update performs no runtime work.

- DEC025 (W02 user direction): glyphs must be static copied/created assets, not runtime line/shape drawing. The correction uses SVG masters and high-resolution raster exports. Header back/search and tabs share top/side outlines and open bottoms; visible arrow navigation extends unshown overflow behavior.
- DEC026 (user-directed source departure): dark panels should show the background ever so slightly. W02 applies98.5% opacity after the full review found94% too transparent over bright artwork; text and icons stay opaque. Carry this intent to later panel campaigns, without treating the parameter as measured source art.


## DEC-027 — Shared shaded button rim

The user supplied direct edge comparisons and requested correction across buttons. Standard mod-owned button faces share a single rounded shaded rim: brighter upper edge, quieter sides/bottom, hue from the face (including green Play/red Quit), flat interior. Tabs retain their open lower edge. Runtime-generated gradient texture is a primitive, not an icon; icons remain authored image assets. The compressed reference does not establish a blur/glow recipe. Integrated verification is part of Batch06.

## DEC-028 — Full-scene entry composition from supplied film frames

The user explicitly begins W01 and provides snapshot1/snapshot2. Their 1280×720 frames establish anchors absent from the component board: compact right-side logo/menu or browser, compact DLC marks beneath menu, bottom-left publisher/account/language. A 312×220 board crop is not 312×220 logical units at UI 1.5; the film menu is 208×147. Use uniform scene-relative scaling with centered overflow margins for other aspect ratios. Native BGPlanet/GameTitle/LudeonLogoSmall/SteamWorkshop/active-language textures are loaded at runtime, not redistributed. Preserve native flows, translation information through secondary links, version-hook load-error side effects, and centered in-game browser size. DLC idle selection starts closed and hover no longer swaps the reference backdrop. These full-scene placements are observed; responsive extensions and publisher-link interaction are inferred.

## DEC-029 — User-directed entry motion and preserved context

The fixed DLC strip must not move above its card. Same-click open/close expands/contracts; switching crossfades content. Load morphs menu into browser and reverses on Back; language grows upward without hiding menu/DLC. Native flags and icon rebind survive reload. These requirements are explicit; timings/easing/reveal fade are inferred. Entry opacity .96 supersedes DEC026's .985 for these surfaces. DEC023's extra language/links row is superseded by the observed full-scene footer in DEC028.

## DEC-030 — Native scale remains authoritative

Remove the entry mapping's cancellation of native UI scale while keeping scene/footer anchors coherent. Preserve native minimum1024×768 logical size and Keep/Revert workflow;1920×1080 permits1×/1.25×, despite legacy1.5× rendering. Geometry checks do not establish a native selector pass. Native in-game scale testing remains pending at this handoff.

## DEC-031 — Build identity and motion evidence

Two0.9.6 builds exist:2566b99/39EA has detailed route/frame evidence;ca6aa8b/A009 contains final DLC rendering corrections with build/startup evidence only. Never transfer acceptance merely by version number. Opt-in end-of-frame PNGs/hash receipts support sampled rendering review; readback affects timing and cannot certify performance. The user requested a handoff before final retest; preserve the partial campaign.

## DEC-032 — Resumed native-scale verification and readable geometry

v0.9.8 uses one logical unit for entry controls; native UI scale determines physical dimensions while scene/footer anchors adapt. The non1× native minimum is1024×768 logical pixels;1× is exempt. Actual native1×/1.25× Keep and final1280×960/1.25× Keep plus1920 restoration passed. Earlier DEC030 pending wording describes the old checkpoint. Do not bypass native restrictions or claim geometry tests alone are live proof.

## DEC-033 — Batched motion/type corrections and retained minor findings

R23–R29 were reviewed and fixed together across the resumed batch and its correction retest. English Anomaly uses observed line breaks and native8logical font;10.667logical pitch is the reconstruction parameter. Other content retains flowing text. Browser content appears only when its shell inset fits; Back uses the temporal reverse of opening. These easing/reveal values are inferred. R30 minimum-layout empty-edge overlap and R31 faint preview-to-translucent-shell seam remain explicit minor polish with named owners/next batch, under the user's instruction to defer minor refinements. Major structure is not covered by that exception. No visual completion is asserted.


## DEC-034 — User-directed entry preferences and browser trial

The user accepted a25% larger main menu and requested a larger browser independently. After a25% browser increase remained too small, the next trial adds50% width/height with contents scaled together; native Window rounding is retained. Original artwork is enabled by default, with explicit saved false preserved. DLC motion must grow from a fixed bottom-right corner; active icons are brighter, not their panels. Footer language button moves up2logical units. These are user overrides, not newly measured source facts. Browser size is awaiting user assessment. Final tab-width rounding fixes a fractional-scale regression without enlarging the main menu or disabling genuine overflow. Actual rendered intermediate frames, not only geometry, establish corrected DLC transform order. See Batch06/preferences-v099.md for per-build evidence; no visual golden promoted.


## DEC-035 — New-game entry preserves native configuration

The user requests a reference-based New colony menu with the same animation as Load. Own only native scenario/storyteller pages linked to that command; reuse their fields, native validation and navigation. Preserve full Custom controls in a native modal and keep later setup pages native until their own design work. Scenario layout and alternate curves are explicitly inferred (EXT041/EXT042); curves illustrate character rather than predicting incidents. B01's582×652 source layout becomes the storyteller shell, fitted uniformly to viewport and multiplied by native UI scale. Native Large portraits are sampled through private bounded mipmapped copies rather than altering shared textures or packaging game artwork.

The full review found and corrected structural, sampling, clipping, lifecycle and motion defects in two consolidated correction batches. See Batch07 for exact build/test attribution. Native font/framing/curve residuals and untested compatibility remain explicit; no visual completion or source golden is declared.


## DEC-036 — Browser shell and control scale are independent

The user explicitly preserves the enlarged browser shell but requests smaller contents matched to new-colony. This supersedes DEC034's contents-scaled-together trial. Keep shell/preview/columns/row pitch, match control/type roles using the new-colony fit scale, and protect long labels from actions. Batch08 records the full layout, overflow/search and return checks; larger geometry is a user override, not recovered source measurement.

## DEC-037 — Readiness and shared entry tooltips

Next uses a neutral disabled face and rejects activation until all native storyteller choices exist. Native validation remains on valid attempts only; rendering must not invoke mutating CanDoNext. Entry tooltip style is shared with the browser while preserving native text/rich text/lifecycle/placement. Keep concise concept card copy separate from complete translated help. Disabled colors and unshown tooltip geometry are inferred extensions; in-game tooltip styling retains separate compatibility scope. Batch08 preserves the independent review finding and correction rather than declaring capture09's short help complete.


## DEC-038 — Create world extends the setup shell without replacing game logic

The Create world body is absent from the concept. Reuse the observed setup shell and shared controls while retaining the real linked native page, fields, resets, faction policies and asynchronous validation/generation. Advanced settings and InfoCards preserve their complete native bodies until separately scoped. This extends DEC035 to Create world; subsequent landing and pawn pages remain native. Record EXT044 and scoped evidence in Batch09.

Native Back adds the previous page before closing the site-selection globe, so scene-active checks are temporarily false during initial sizing. Repair owned geometry before WindowOnGUI computes local bounds and paints chrome; do not mutate native world mode or reset settings to repair a presentation bug. CW08 was reproduced, corrected and live-retested in v0.11.2. No whole-product fidelity acceptance follows.

## DEC-039 — Native cue semantics and bounded roster viewport

User requests full-card mood, separate health and coherent status icons. Preserve native predicates, priorities, warning colors, preferences, portrait poses and selection. Exceptional states absent from the concept are inferred extensions, not source art. Keep fixed-size cards with greedy native-order/group packing, wheel/scrollbar and authored expand/hidden-warning icons. Remove the plus tile per user. Collapsed rows1–3(default2) and expansion up to4are explicit user-directed clutter controls. See Batch10; no new hunger/status mechanics or work-group assignment model.

## DEC-040 — Flat panel family and direct save activation

User explicitly rejects the decorative top gloss on all dark gameplay panels and requests slight translucency. Share a90%default fill across their backings; preserve button rims and semantic lines. Existing entry surfaces already have their separate translucent baseline. Double-click selected save rows dispatch through existing file-stamp validation/native load, with single-click preview retained. Final in-game checks and limitations are in Batch10.


## DEC-041 — Compact resource reveal and shared HUD motion

User approved the materials discrepancy inventory and pawn animation. Use anchored icon columns, single-line resource details and reversible real-time reveal while paused. Hover previews; compact/in-progress clicks pin without activating items; revealed resource clicks preserve native information access. Header/chrome unpins, separate category chevrons preserve collapse access. Direction glyphs distinguish observed movement/zero from unavailable ellipsis; exact deltas stay in tooltips. Retain resource category priority for improvised weapons such as wood. Native counts/history are preserved; synthetic trend history is diagnostic-only. Exact timing, widths and control split are inferred adaptations, scoped-tested at UI1.5 in Batch11, not source-pixel certification.

## DEC-042 — Hover-only resources supersede click pinning

The user rejected click locking after Batch11. Expansion now follows hover plus the short leave grace only. Revealed rows retain native InfoCard clicks, category chevrons retain collapse; header/chrome absorbs clicks without changing expansion state. This supersedes the pin behavior in DEC-041. Native weekly availability remains data-dependent: seven in-game days of per-resource observations are required; vanilla does not supply historic per-item weekly counts.

## DEC-043 — Readable HUD and standalone-button treatment

The user corrected the first compact HUD build as too small at UI1.5. Increase dock text/icons, tools/menu and clock readout about20%, keep native speed input geometry, and use real group dividers. Preserve Architect's distinct size/alignment and shaded rim; standalone Esc/C controls reuse that rim while background panels stay flat. Badges display actual native/rebindable keys, so Architect shows Tab in this profile rather than the reference's X. Weather glyphs infer unseen states from native perceived weather with full text tooltips. EXT048 records the extension.

## DEC-044 — Pawn selection stays within its card

The user demonstrated native selection corners clipped by the card/viewport. Replace only the grouped-card selection paint with a continuous pale inset rounded outline and dark inner separator, preserving native selection decisions and viewport clipping. No mood/health tint or gameplay selection logic changes. EXT049 records this inferred selection state.

## DEC-045 — Tools shell reveal and internal submenu slide

18 October 2026. The user clarifies the reference motion: the tools panel expands from its launcher button and retracts into that button when closing. Inside the open panel, submenu content slides left into view. Treat shell expansion/retraction and internal content navigation as separate transitions, with content clipped to the panel. Exact timing, easing and intermediate geometry remain unmeasured; a rightward Back slide is an inferred reverse. This is a documentation clarification for P10/W10 and P11/W11, with no runtime implementation, build or in-game acceptance performed in this update.

## DEC-046 — Tools native behavior and bounded search extension

18 October 2026. Implement DEC-045 with separate reversible launcher shell and clipped submenu transitions. The source labels Repair buildings/Automatic roofing lack equivalent native switches; use truthful Rebuild destroyed buildings (`autoRebuild`) and Automatic home area (`autoHomeArea`) rather than adding gameplay mechanics. Preserve extra native controls in More, keep Compare unavailable pending W12, gate Codex with native availability, and explain Helper's active-lesson behavior. Categorized resources must update both native preference and custom list.

Infer revealed-map search using real selectable Things, debounced literal case-insensitive matching, native thumbnails/metadata, bounded rows and native jump/select with revalidation. Hover/C/pin/Escape/outside-click and owned-focus rules are extensions. Exact source timing/easing remain unmeasured. EXT050 and Batch13 record adopted parameters, initial failures, consolidated visual/hover corrections and v0.15.2 Back-focus correction with live retest. No overall visual or final work-order acceptance follows.

## DEC-047 — Inferred Architect body and pawn-style tile labels

18 October 2026. The user chose the existing concept and panel style for the build menu; the concept does not show its full body. Infer a compact category/grid/information panel above the launcher while preserving native designators, filtering, search, material menus, styles, costs, placement and command dispatch. Native row ordering is retained within a top-aligned compact block; larger grids scroll from the native first row. A dedicated setting and context/IL fallback retain original Architect behavior.

The user then explicitly requested labels near the bottom inside each tile, with a gradient rising behind them like pawn names. Reuse the actual pawn-name backdrop and 12px centered label treatment, shortening long labels with ellipsis. Fit stock native icons within the tile using their actual definition/style scale; leave custom-assembly overrides native. Parameters and body layout are inferred, not source measurements. Batch14 preserves the first-pass defects and v0.16.2 corrections. The user paused game testing; no final acceptance is implied.

Subsequent user refinement supersedes the initial 12px labels: 10px bottom tile labels with the retained pawn gradient; flat native material popups with 12px labels; 4px draggable scrollbars; full-height categories; a 104px information pane (previously 155px) aligned with the grid; compact stock rotation buttons using native callbacks; and shell opacity capped at 84%. These values are inferred from the requested density/transparency and established panel family, not source measurements. v0.16.3 is staged and offline verified; no deployment or game testing during user pause.

## DEC-048 — Panel origin, distinct search actions and explicit Tools toggle

18 October 2026. User requests no Q/E buttons, actual slight transparency and visible open/close motion. v0.16.4 fixes native shadow ownership and keyboard dispatch, removes stock visual arrows while retaining native rotation, and scoped live evidence verifies those corrections. The user then clarifies that Architect must expand from its own bottom-left corner because the + launcher remains outside it. v0.16.5 stages fixed left/bottom edges, full-size clipped opening content and immediate native close followed by an empty retracting shell.

The adjacent search-clear X and panel-close X were confusing. Infer an interior text Clear action with native search identity/cache/focus and a separated exterior X close. For Tools, the final user instruction is click-only opening, button retained over the panel as a click toggle, and a visible pressed state. This supersedes the interim hide-button request and the older hover/leave/pin behavior. Tools keeps its launcher-origin shell and internal submenu slides. New source is privately built/reviewed; physical Escape stopped live testing, so no new live acceptance is claimed. Parameters and unseen states remain inferred; root owns final deployment/retest.

## DEC-049 — Complete dock labels and reference-based selection panels

18 October 2026. The user requested installation/restart for their own testing, wider bottom buttons, Architect-style selected-object actions, and a selected-thing inspector prioritizing the documented pawn tabs. v0.16.6 is installed and startup-verified. Dock labels reserve their full measured width with rounding space; excess buttons use the existing native overflow. Root relinquishes gameplay input after the restart while Selection15 is developed privately.

Use the native map command dispatcher with scoped paint substitution, preserving command input, grouping, toggles, hotkeys and custom-mod fallback. Use the native Inspect tab owner and actual visible tab instances for the new shell. B04 Summary/Needs/Social/Gear supply the composition; a 560-logical-pixel pawn shell, single-thing description treatment, extra-tab arrows and native-body adapters for Bio/Health/Log are inferred extensions. Native data controls all values; meter illustration discrepancies do not change the math. Keep all real needs, both opinion directions, native role/romance eligibility, item actions and mod/DLC tabs accessible. Do not invent new-thought novelty rules. Shared glyphs are authored reconstructions; exact source type/scale and live comparison remain open. See EXT052 and Batch15 for ownership, tests and pending acceptance.

## DEC-050 — Refine pawn panels against supplied actual/concept pairs

18 October 2026. User approves all identified discrepancies and requests installation/restart for their own test. Normalize crop proportions rather than treating different-resolution screenshots as identical logical sizes. Implement the complete shared-shell/Summary/Needs/Social/Gear refinement inventory in Batch16, preserve native data/actions and leave missing body references native. Use compact primary11/secondary10/heading8 typography and slight cooler transparency. Native extra actions move to compact cogs instead of reserving a More actions row.

This supersedes DEC-049's unresolved new-thought-dot policy: the first observed set is an unmarked baseline; newly observed or reappearing native groups get a session dot that expires after417 unpaused game ticks while fully visible. This is an explicitly inferred lifecycle, not a source claim or fabricated creation timestamp. Normal zero skills use muted ink; only passion uses yellow, while disabled skills show a dash. The source's inconsistent red zero treatment provides no stable gameplay rule. Preserve native conditions and complete tooltips. User owns live gameplay testing after restart; root verifies final build/install/startup identity.

## DEC-051 — Fitted inspector text and inferred Bio, Health and Log

18 October 2026. User requests complete tab/name labels, flush tab bottoms, styled romance/cog menus, and inferred Bio/Health/Log; explicitly directs Bio to inherit Overview. Measure labels in their painted style (bold name), reserve icon/padding/rounding independently of gaps, prioritize name and wrap role when necessary. Use30px tabs with top-only rounded geometry and a flush selected join. Extend existing Architect FloatMenu chrome only to inspector-origin exact menus and their nested menus, preserving native input, option callbacks and Widgets scroll bookkeeping. Separate dialogs retain existing styling for their later dropdowns.

Bio uses Overview inset biography and skills cards; Health uses compact status/conditions and an operations queue; Log uses Social-style real event cards. Width560/header43 and Bio/Log400/Health420 heights are inferred geometry. Exact native data, gating, callbacks, grouping, filters and complete tooltips remain authoritative. Native extra/developer/mod controls stay accessible; custom bill/log subclasses retain their own rendering. No invented events or gameplay changes. All ordinary data is reachable through slim rails. Native Bio action-header clipping and Health listing substitution are guarded; unsupported states fall back. Source and independent reviews plus offline checks precede the user-authorized installation/restart; user performs live acceptance. See EXT054 and Batch17. No unseen source pixel match is claimed.

## DEC-052 — Shared Bio background, open-bottom tabs and styled information actions

18 October 2026. User corrects DEC-051's interpretation: reuse Overview skills, not its inset black panels. Remove Bio's two inset fills/padding. Tab row and content share a single background; active tabs add only top/side outline so alpha layering cannot produce a false bottom seam. Preserve the Overview cards themselves. The supplied user crop is pre-correction evidence, not an accepted baseline.

The same feedback batch explicitly rejects Health/Social cogs navigating to old native bodies and requests the global information overlay to use the new UI for every subject. Normal cogs become true styled options menus, preserving native Social pregnancy preferences, remaining Health actions and developer options. Generic information cards preserve native subject/tab/material/history/stat/search/link/callback semantics while adopting common compact panels, type, buttons and rails. Missing card/tab states are inferred under P20; keep exact-data preservation separate from visual acceptance. Root integrates two isolated workers and independent audit; private implementation first while user tests the installed build.

## DEC-053 — Compact information popover and corrected tab band

18 October 2026. User clarifies the lighter background starts below the tabs, superseding DEC-052's whole-row body fill. Paint a dark row with lighter fill only in the selected slot, and keep its bottom open. Paint disjoint regions once to preserve slight transparency. The user also rejects the oversized original-dialog composition for information windows: use the bottom-left inspector as the analog,640×420 maximum,40px header,28px tabs, header search/material/back, one close X, no bottom Close footer or nested frame. These dimensions are inferred. Native subjects, data, search, history, eligibility and actions remain; restore native captured flags on fallback/close. A560×300 logical-screen eligibility minimum avoids header collisions.

Root prepares v0.19.2 privately after the user's physical Escape stopped Computer Use. No new game input/restart without renewed release. Existing live captures remain attributed to v0.19.0/v0.19.1 and do not validate the new layout. See EXT055 and Batch18.

## DEC-054 — Consistent inspector layout, actual inputs and content-sized scrolling

18 October 2026. User correction supersedes cramped popover portrait sizing, redundant info Health navigation, mismatched search/close rows, varied detail heights and legacy object health/scrolling. Reuse the bottom-left50×56 portrait lane while honoring native icon overrides; defer portrait paint until after tabs. Use aligned24px header controls and420px detail height for every non-Overview pawn tab, keeping234px Overview. Remove only the redundant single Health choice in information cards, preserving actual operations eligibility and native data.

Bio cog becomes a compact action list and real input fields with native filters, focus, validation and confirmations. Information ownership follows its captured pawn/card and setting independently of map selection or WindowTheme. Object descriptions preserve the native pane and composed strings, measure the true text viewport, and allocate the shared rail only on overflow; native WidgetRow health geometry remains. Dimensions, compact action/editor layout and full description on hover are inferred extensions of the established inspector style, not new source-exact claims. Source/native review and offline checks pass for private v0.19.3; user testing of installed v0.19.2 remains uninterrupted. No live acceptance or baseline promotion. See EXT056 and Batch19.

## DEC-055 — Click-selected Work editor and reversible enabled-job Autosort

18 October 2026. User replaces the source hover interaction with obvious click selection, a highlighted row and persistent editor. The six category columns, tag stripes/fill states, actual skill/passion values, compact roster and action squares follow P13. Additional loaded work types, scrolling, selection marker, native Sort menu and manual-mode control are inferred extensions. Unsupported custom columns/priority workers keep the complete native table. Opening/selecting never writes priorities.

User explicitly chooses enabled-job priority adjustment with Undo. Autosort requires manual priorities; only enabled eligible work with relevant skills is changed. Average relevant skill plus2 per highest passion level maps scores14/9/4 to priorities1/2/3, otherwise4. No-skill care/emergency/support, disabled and temporarily incapable jobs stay unchanged. Preserve native scheduling score/ties, click cycles, Shift bulk actions, status callbacks, sorting comparers and native clipboard payload. Undo restores actual deltas only and expires on conflicting edits, context, selection, mode or eligibility changes. No new save schema or automatic reapplication.

Private v0.20.0 passes source review and offline native/policy checks. Installed v0.19.3 and the user's testing session remain untouched. Full panel visuals, gameplay, large roster, DLC/mod/language/scale and save/reload acceptance remain open. See EXT057 and Batch20; inferred states are not source-exact claims.

## DEC-056 — Four horizontal priority rows with explicit drag feedback

18 October 2026. User supersedes DEC-055's click cycling/Shift bulk editor with simple single-click on/off above the roster and draggable native priority levels below. The rejected equal-width columns and later adaptive horizontal bands are superseded by the latest explicit request: four rows per pawn, one per priority, a tag following the pointer and a landing ghost. Assign work remains above the roster; selection stays click-based and highlighted. Enabled work restores its remembered native priority on reenable, default3; no repeated-click cycling in the editor.

The fixed142px pawn group (32px header, four26px lanes,6px gap), horizontal overflow,4-screen-pixel drag threshold, overlay styling and cancellation rules are inferred from existing compact controls. Pointer paint stays outside clips and preserves the grabbed point; the ghost occupies the native natural-priority/definition-order insertion slot. A valid same-pawn release changes only the native priority; same-tier drops never create custom scheduling order. Checkbox mode shows a single lane containing all enabled work. Autosort/Undo retains DEC-055 policy and remains explicit. No scheduler or save-schema change.

Private v0.20.3 passes source review and offline checks; installed v0.20.2 remains unchanged after the user's physical Escape stop. Historical v0.20.2 toggle/drag evidence is scoped and its final cancellation/Autosort/Undo pass unfinished. No new game input or visual/source acceptance. See EXT058 and Batch22 for exact identities, retained evidence and pending gates.

## DEC-057 — Return to compact Work rows and separate priority controls

18 October 2026. User explicitly supersedes DEC-056 four-row/drag composition with the original compact reference. One row per pawn shows enabled work in native order; a click-selected upper editor enables/disables work. Each enabled editor tag has a separate numbered button cycling2→1→4→3→2; no Off in this cycle. Skill stays inside the tag and priority outside it. Remove dragging, four rows and their overlay. Restore inset dark category card, compact inline identity/actions and quiet selection. Extra jobs, overflow, Undo and small toolbar are coherent inferred additions, not source-exact claims.

The priority button always exists for enabled work. A valid explicit number click activates colony-wide native manual priorities if needed and cycles the displayed value (native3 while Off) to2. Preserve other raw priorities and native mode notifications. Opening, hovering and selection never mutate gameplay. A tag click remains a single native Toggle; number click returns before that branch. Row order uses the actual native combined priority/natural-priority key with stable definition ties. Autosort/Undo policy remains unchanged and explicit; no scheduler/save-schema patch.

Private v0.20.4 passes independent native/input and geometry/style review plus offline checks. User chooses installation/restart and their own gameplay testing; startup is a separate gate, not live acceptance. See EXT059 / Batch23. Prior rejected screenshots and historical evidence remain attributed to their versions.

## DEC-058 — Aligned leading priority badges

18 October 2026. User refines DEC-057: priority controls precede every editor tag at the same column alignment. Off work retains a disabled dash control; distinct numeric colors and a distinct button shape identify priority. The cycling sequence is an implementation instruction, not tooltip copy. Keep the established native cycle and separate toggle/priority paths unchanged.

Use the existing22px reserve as a19px leading rounded outlined badge plus3px gap. Center a bold number; infer1 green,2 blue,3 amber,4 purple and a muted disabled dash, with priority names in tooltips so color is not the only cue. Work tags retain their category stripes and rectangular shape. Ordinary tooltips say the priority name and click instruction; first activation retains the colony-wide mode explanation. Subsequent user feedback replaces the tiny per-letter Work heading with the normal bold label and removes routine action notices from the menu header; bulk-action details stay in their button tooltips. Private v0.20.5 built/reviewed; installed v0.20.4 and user testing uninterrupted. See EXT059 / Batch23 refinement.

## DEC-059 — Selected-pawn Auto assign, Clear and raw Undo beside Basic

18 October 2026. User supersedes enabled-only Autosort with simple automatic job enablement and priority selection. Place Auto assign, Undo and Clear at the right of Basic in the top section, not per-pawn or in the lower toolbar. Infer explicit skill+passion thresholds with essential care/emergency/support defaults (Batch24); preserve native availability and hidden/unknown unskilled jobs. Require manual priorities for Auto assign without silently changing colony-wide mode. Clear works in either mode. Shared raw-delta Undo restores exact assignments, validates full context/poststate and expires on conflicts. Basic and actions remain fixed while categories scroll.

User also requests an obvious Priorities toggle, full Sort text and unaccented title. Native translation inspection identifies DevMode pseudo-translation of nonexistent Work key; read native MainButtonDef LabelCap instead. On/Off plus track/knob and measured button widths complete this batch. Geometry/policy are inferred extensions under EXT060. Private build/tests/review are separate from pending live/native acceptance; installed v0.20.5 remains unchanged.

## DEC-060 — Independent Work priorities and universal animated translucent windows

18 October 2026. User supersedes DEC-059's manual-mode prerequisite and DEC-057's automatic first-number activation. Auto assign works in either mode without changing it. Priorities On enables colored number controls; Off leaves the stored numbers visible in gray and disables editing. Read raw values separately from native checkbox-mode effective3; preserve them across mode changes and tag disable/reenable. Keep native effective row ordering and conflict-safe raw Undo. The selected-pawn heuristic and Basic-row actions otherwise remain unchanged.

User also explicitly requires Work and the upper assignment window to animate open and contract closed, and makes animation in both directions plus slight transparency a rule for every window. Apply this by default in all future work and audit older gaps explicitly. Infer reversible220ms bottom-left reveals using existing Tools motion, inactive clipped opening content and inert outgoing shells with immediate native cleanup. Reserve outgoing editor space until finished and suppress its ghost above a newer input-owning modal. A single shared-alpha background replaces the opaque-backed editor fill. This batch covers Work/assignment only; no all-surface or in-game acceptance claim. See EXT061 / Batch25.

## DEC-061 — Grouped colony issues and semantic animated notifications

18 October 2026. User supplies four reference crops and requests right-side colony needs/issues expansion/contraction with slight transparency plus animated alerts with distinct icons/colors. Implement the three subject groups, retain critical urgency independently of collapse, and preserve native report/actions. Heat wave is a native game condition; adapt its display and source/quest action without adding a native alert. Preserve unknown custom renderers through whole-stack fallback and native overflow bundles. Match native event identity and typed incident context rather than translated titles; validate electrical arguments against unrelated nested receipts.

Ordinary research completion uses a native Dialog_NodeTree, so adapt the unique constructor site and keep its original pause, input delay and options. Infer compact green card, one close action and a research CTA; no fabricated completion letter or expiry. Reversible220ms entry/exit,180ms reflow,220px issue/letter width and320x82 research card extend the shared style. All backgrounds use actual shared alpha. Native transient messages and hover details remain separately scoped. Private build and offline/native metadata checks do not establish Unity/reference acceptance. See EXT062 / Batch26; installed v0.20.7 remains unchanged.

## DEC-062 — Esc uses the Tools interaction and animation model

18 October 2026. User requests Esc to work and animate like Tools above it. Keep the actual native Menu window for pause, PreOpen and original callbacks/confirmations. Present238x366 translucent shell with reversible220ms launcher reveal and200ms Links slide, persistent pressed Esc launcher and native Escape/Back. Handle sibling launchers in the owning GUI.Window to avoid duplicate/behind-window input. Preserve full native fallback for custom option types and patched capture methods. Child dialogs remain separate scope. Timing/geometry are inferred from Tools, not newly measured source art. EXT063 / Batch27; v0.21.0 remains installed.

## DEC-063 — Issue groups follow content and the left Resources type scale

18 October 2026. User reports disappearing disclosure arrows, unnecessary empty groups, absent collapsed counts and larger section labels than the Resources panel on the left. Installed native code shows the scale-aware UI rotation wrapper; replace direct Unity rotation and guard that method. Track live totals independently of critical totals and content expansion. Show the total on headers in either state, retain critical tint/tooltip/rows, and animate section presence separately so last-issue removal contracts headers/gaps and replacement arrivals reverse continuously. Sparse groups leave no orphan spacing. Use the existing left Resources7px bold uppercase/0.65tracking heading renderer for both panels, retaining11px body size and modal-safe resource ink. This supersedes Batch26's always-present headings/critical-only header number. Private source/metadata/motion verification is distinct from UI scale and visual acceptance. EXT064 / Batch27.
## DEC-064 — Critical issues belong in separate alert cards

18 October 2026. User explicitly rejects uncollapsible rescue/medical emergency rows and directs them into the separate alert display above the groups. Native audit confirms both examples inherit Alert_Critical (Priority2), while Need doctor and Medical treatment needed are High1 and remain in groups. Separate by actual native priority, not label matching. Show critical instances as30px animated translucent notification cards with red semantic treatment, preserved native update/condition/hover/target behavior and overflow scroll. No fabricated Letter, archive item, timestamp, independent expiry or dismissal. Group counts exclude these cards; all group content fully collapses. Preserve immediate native removal with inert outgoing visuals, atomic narrow-stack fallback and input gates across moving adjacent regions. This supersedes the earlier critical-collapse exception, including DEC-063's initial retained critical row. EXT065 / Batch27; source/private checks are not live acceptance.

## DEC-065 — Esc covers C and keeps Links navigation in place

18 October 2026. User rejects C being redrawn above Esc, absent action icons, and a full-width Links entry whose return moves to the top. Supersede DEC-062's reciprocal sibling-launcher handoff: hide and block Tools from native Menu selection through the remaining Esc reveal, while keeping Esc's persistent pressed launcher. Use existing action icons only after exact native translated-key/order validation; fallback icons do not replace callbacks. Infer shorter238×330 shell and fixed80×24 Links/Back footer outside both sliding page viewports. Preserve native pause, Escape, confirmation and unsupported-context fallback. Moving/destination-unpainted navigation cannot activate. Private v0.22.1 passes source review and offline checks; v0.22.0 installed unchanged and user testing uninterrupted. EXT066 / Batch28; live appearance and input gates remain open.

## DEC-066 — Remaining colony menus and coherent World UI

18 October 2026. User requests best first-pass implementations of Schedule, Assign, Animals, Wildlife, Research, Quests, World, Factions and History using established compact Work/inspector principles. These bodies are absent from the concept; infer routine layouts and retain actual native functions. Use portrait rosters for the four table families; browser/detail views for Research, Quests and Factions; compact Graph/Messages/Statistics for History. Shared icon/title/close, thin rails, slight transparency and reversible expansion/contraction apply to these windows and their owned built-in child dialogs/dropdowns.

User explicitly corrects World scope: preserve existing globe/navigation, restyle Form caravan and Jump to location, and make modern pawn display, speeds, Tools and selection UI available there. Reuse existing components and native world selection/route/layer semantics. No alternate world-navigation design. Native child callbacks/cleanup stay immediate; only inert visuals survive close. Native/mod fallback remains reachable, including explicit Research developer authoring access. All source/private checks are separate from live acceptance; v0.22.1 remains installed. See Batch29 and EXT067–EXT069.

## DEC-067 — Compact menus preserve the research graph and communicate control roles

18 October 2026. User rejects the v0.23.0 oversized/native-like roster compositions, horizontal policy/animal scrolling, missing research graph, ambiguous History controls and bottom-left expansion. Supersede those BOTTOM30 inferences: content-sized portrait rosters with compact details; a colored Schedule timeline; explicit policy dropdown chevrons; normal/default Research node-and-dependency graph using real project positions and requirements; modern History recorder chart with tabs, group dropdown and segmented ranges. Preserve Factions and Quests layouts. Main menus reveal at full width from the bottom; child dropdowns anchor to their actual controls and unfold from the adjacent edge. Keep all native callbacks/data and inert outgoing animation. Private implementation/testing is distinct from the installed v0.23.0 user session and pending live comparison. See Batch30 / EXT070–EXT072.

## DEC-068 — Whole-game UI backlog and scoped source publication

19 October 2026. User requests the remaining old-style UI audit and GitHub preparation, choosing MIT for original code/assets. AUDIT32 separates declaration discovery, source coverage assessment, inferred triage, live spot checks and final acceptance. A patched-method count is never a screen-completion count. Every discovered native hierarchy/helper row has an open follow-up group, including older window opening/closing/translucency gaps.

Preserve the authoritative installed checkout and all local evidence/history. Prepare a fresh allowlisted source repository rather than publishing historical reference pixels, game captures/logs or machine records. Retain concept/dependency attribution and no blanket MIT claim over third-party material. Portable build/tests, CI and source/runtime packaging are local preparation; no remote creation, upload or publication is authorized by this task. See [Audit32](Audit32/README.md) and [GitHub readiness](07-github-readiness.md).

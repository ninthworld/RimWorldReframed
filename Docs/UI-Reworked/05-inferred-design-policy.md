# Inferred designs and complete UI coverage

Revision 2 · 17 October 2026 · Standing rule for extending the concept

The concept is a set of examples, not an exhaustive UI specification. The user expects missing states to arise and explicitly directs the team to infer their appearance from what the artwork establishes. Agents should proceed with routine design inferences, record them and test them. They should not stop for approval on each ordinary icon, overflow state or new event treatment.

The previous specification identified missing screens, additional groups, alert classes and overflow, but did not define an exhaustive extension system. This document supplies the shared rules and an expandable [inference register](inference-register.csv). The register is a working inventory, not a claim that every future state is already known.

## 1. Two acceptance targets

**Shown states:** reproduce source geometry, typography, color, icons and behavior as precisely as the evidence permits. Preserve the exact canonical example when additional content is absent. An extension must not change the illustrated three-group HUD or ordinary Bzzzt toast merely because the implementation can now handle more content.

**Unshown states:** produce the design that most consistently extends the reference's visual language while preserving actual game behavior. Document the inference and compare the implementation with its recorded extension specification. Once accepted, it becomes a regression baseline. It is never labeled a pixel match to an image that does not exist.

Every new state is classified as observed, measured, inferred or required for functionality. Unknown font identity or layer composition remains unresolved evidence, even if an initial reconstruction recipe has been chosen.

## 2. How an agent makes an inference

Use this sequence whenever a work order encounters an absent state:

1. Identify the real data and native behavior: what must be displayed, what the control does, what changes over time and which actions must remain available.
2. Find the nearest demonstrated component. Extend its layout, density, hierarchy, spacing, typography, borders and state vocabulary. Use the work-tag style for another work tag, the notification shell for another event, and the pawn shell for another pawn tab.
3. Check existing extension decisions. Reuse an established overflow, icon, severity or empty-state rule instead of inventing a new one locally.
4. Choose the smallest consistent extension. Prefer data-driven counts, reusable tokens and native semantics over special cases based on the screenshot's sample content.
5. Record the decision ID, source analog, rationale, alternatives if material, affected modules and tests. Provide a concrete mockup or layout/state table when a new composition is necessary.
6. Implement within the worker's ownership. Coordinate shared components through the lead. Test both the original canonical state and the new state.

Routine decisions are delegated to the implementing agent and reviewed by the lead during normal integration. The lead resolves cross-module consistency and may revise an inference with recorded reasons. Ask the user only when competing directions materially affect the intended experience, preserving gameplay would conflict with the requested design, or the choice changes scope or mechanics. Missing art alone is not a reason to stop work.

## 3. Visual rules that carry into every extension

- **User-wide motion/material rule, 18 October 2026:** every window or panel opens with expansion/reveal and closes with contraction, including secondary/selected-content panels. Backgrounds must remain slightly translucent; a solid backing beneath a translucent layer defeats the requirement. Use established reversible real-time timing/anchors, keep content legible and block moving controls from acting. Native dismissal and gameplay cleanup remain immediate; outgoing visuals never retain input or invoke native callbacks. This applies by default to every subsequent window implementation and refinement. Record existing coverage gaps rather than implying a global retrofit is complete.
- **Bottom-menu correction, 18 October 2026 (DEC-067):** the user rejects bottom-left expansion for the colony menus. Keep their width steady and reveal vertically from the bottom edge. Dropdowns belong to their triggering control and unfold from the adjoining edge, flipping above when needed. A nearby popup must not appear to grow from a distant screen corner. Compact content and clear dropdown/toggle/navigation semantics take precedence over reproducing a wide native table.
- Reuse the measured dark surface hierarchy, compact type scale, subtle borders, corner families and icon treatment. Do not add unrelated decorative styles.
- Preserve information hierarchy: primary label, secondary context, numeric/temporal detail, action. Use the reference's muted text and small uppercase section headings for corresponding roles.
- Separate semantic color from selection/hover. Selection should remain legible on warning, positive and destructive controls.
- Icons need a coherent silhouette, optical size and stroke/fill style. Reuse a matching asset or reconstruct it; platform emoji are not substitutes. A new symbol gets an asset ID and meaning.
- Use a common overflow vocabulary across the mod: ellipsis with access to full text, scrolling for repeated content, and a discoverable expanded view when needed. Never silently drop game data.
- Maintain source density at the canonical resolution. Do not progressively shrink fonts/cards to fit arbitrary amounts of content.
- Empty, unavailable, loading, error, focus and disabled states remain understandable and retain native functionality. These states may be inferred even when the concept only shows populated panels.
- Preserve accessibility through text, icon, tooltip and interaction, rather than relying on color alone. Compatibility adjustments are recorded separately from canonical fidelity.

## 4. Colonist groups beyond the three shown

### 4.1 Current user-directed scope

The standalone manual grouping editor and persisted assignment model were rejected by the user on 17 October 2026. They were an unsupported expansion of the concept, not an accepted feature. The user then explicitly directed: use placeholders until more of the UI is established. Do not resume create/rename/membership/deletion work or invent work-role classification now.

The current W06 composition uses the source headings WARRIORS, ENGINEERS & BUILDERS and CREATIVE as temporary labels, with real native-eligible pawns divided into stable contiguous display chunks. The labels do not claim those pawns have those roles. No work priorities, jobs or native display order are changed. All eligible pawns remain reachable; unsupported contexts retain native fallback. The plus tile temporarily opens the existing Work tab. Actual work-derived grouping rules and further categories remain deferred.

### 4.2 Visual requirements apply now

Use the B02 card/header hierarchy, content-sized dark shell, small tracked white uppercase headings, portrait/name cards, larger inter-group gaps and dashed plus tile. Do not add a permanent Groups/Manage/reorder toolbar or reserve a large empty panel. Overflow controls appear only when content requires them. Preserve native selection, multiple selection, camera jump and map/caravan/death distinctions. Right-drag ordering and exact portrait reconstruction remain explicitly open, not implied by the placeholder implementation.

### 4.3 Future grouping work

Revisit work-derived grouping together with W13 Work UI after the broader UI exists. Reuse native work settings and existing Work interactions where possible. Before implementing, define multiple jobs, ties, unassigned pawns and lifecycle behavior without introducing a competing management system. Three illustrated labels are not a permanent product limit; the current temporary composition is not the final category model.

## 5. Notifications beyond the illustrated events

### 5.1 Keep channels and semantics intact

RimWorld exposes multiple kinds of information. Persistent alerts/needs belong to P08's issue groups. Letters and actionable event entries belong to P09's letter/notification treatment. Transient messages and rejected-input feedback have different lifetimes. A notification can open a choice dialog with additional actions. Applying the concept's style must not flatten these distinctions or turn every message into an identical persistent letter.

The reference shows an electrical-warning toast, a quest toast and a research-complete treatment. These establish examples of a reusable shell, icon slot, label, timestamp and accent treatment. They do not define every event's icon, severity, duration or action. In particular, the Bzzzt label must be mapped to its actual electrical-event context; a power-related icon alone does not mean every loss of electricity should use that same event.

### 5.2 Discovery and coverage

A read-only scan of the installed Data folders found **24 named LetterDef rows, 12 MessageTypeDef rows and 69 IncidentDef rows** across Core, Biotech and Ideology. The [definition inventory — local evidence omitted](LOCAL_EVIDENCE.md) and [manifest — local evidence omitted](LOCAL_EVIDENCE.md) record the source files and hashes. These are raw static definitions: XML inheritance, patches, conditional requirements and runtime identity are not resolved. An incident is not necessarily one notification, and many notifications originate elsewhere.

Before W08/W09 sign-off:

1. Enumerate the actual loaded letter/message definitions and alert types in the supported game/mod configuration, resolving inheritance and patches.
2. Audit code-generated, quest, health, pawn, caravan, world, DLC and choice-letter paths. Register semantic event context where it can be obtained reliably.
3. Associate each supported entry with a presentation mapping or an explicit tested fallback. Keep discovery coverage, bespoke design coverage and test coverage as separate numbers.
4. Exercise representative events in game, including their action targets, choices, sounds, pause behavior, timestamps, dismissal and persistence.
5. Re-run coverage discovery after game/DLC/mod updates. New entries become visible registry work rather than silently inheriting an incorrect bespoke icon.

Do not use English title matching as the primary event classifier. Titles vary by language, substitutions and mods. Use stable definition/class/context identities; where they are unavailable, use a documented channel/severity fallback.

### 5.3 Mapping contract

Each concrete notification mapping must contain:

| Field | Purpose |
|---|---|
| Mapping ID and source identity | Stable key; definition/class or contextual event key; applicable game/DLC/mod version |
| Native channel and object ID | Keep the actual message/letter/alert and its identity |
| Event domain and subtype | Electrical, combat, health, weather, quest, social, research, trade, travel, etc. |
| Urgency and outcome | Preserve native threat/severity and distinguish beneficial, adverse, neutral, completed and resolved |
| Icon asset and fallback | Specific semantic symbol, family symbol and last-resort native/generic treatment |
| Accent/text/surface tokens | Reuse demonstrated roles; exact new color values become documented inferred tokens |
| Content behavior | Title/body/target/time, truncation, full-content access and localization |
| Interaction and lifecycle | Open/jump/choice/dismiss, sound, native pause rules, expiry or persistence, stacking |
| Evidence and tests | Reference analog or inference ID, fixtures, visual state cases and live action checks |

Icon domain and severity are separate dimensions. For example, a quest can be dangerous; the quest icon must not force a calm blue presentation that conceals a major threat. An electrical icon does not establish that an event is minor. Severity and native urgency win when styles conflict.

### 5.4 Initial semantic style families

These families are inferred extensions unless a specific reference example is named. They define direction, not final unmeasured RGB values or guessed event bindings.

| Family | Intended icon language | Accent direction | Behavior to preserve |
|---|---|---|---|
| Major threat | Specific threat glyph or prominent threat fallback | High-priority red treatment derived from the established red vocabulary; contrast and prominence tested | Native urgency, sound/pause and target; never quietly expire actionable content |
| Minor threat | Specific subtype with a distinguishable threat cue | Orange/amber family, distinct from major threat | Native severity and action |
| Adverse nonurgent event | Domain-specific warning glyph | Yellow warning family; Bzzzt is one reference example | Actual event detail and target; no false upgrade/downgrade of urgency |
| Health harm or death | Health/death semantic symbol | Adverse or critical token according to native semantics | Correct pawn/target and special letter behavior |
| Quest/offer | Reference scroll or related quest symbol | Reference blue for ordinary quest; threat presentation when native event is threatening | Choices, expiry and associated quest target |
| Task completion | Success check or specific completion glyph | Reference green research-complete treatment | Native completion semantics, no duplicate notice from repaint |
| Beneficial unexpected event | Domain-specific positive glyph | Positive family consistent with the reference; distinguish from simple completion where necessary | Native event and any action |
| Situation resolved | Resolution/check treatment | Muted positive or neutral resolution family | Link to the resolved situation when available; no stale urgency |
| Neutral information/trade/travel/social | Recognizable domain glyph | Neutral/muted informational treatment unless native context says otherwise | Full content and native target/action |
| Input rejection/caution/silent feedback | Small feedback glyph or existing message treatment | Error/caution/neutral according to native message type | Keep transient lifecycle and input feedback; do not create an actionable letter |
| Unknown/mod-provided | Registered mod glyph if compatible; otherwise native or family fallback | Preserve any known native severity/color; visibly distinct generic state if severity itself is unknown | Never drop, invent an action, or downgrade a threat because no bespoke mapping exists |

Candidate domains to audit include electrical faults, fire, raids, animal threats, disease/injury/death, mental/social events, weather and environmental conditions, resources and harvest, trade/visitors, quests and offers, research/completions, travel/caravans, faction/world events and expansion-specific events. This list guides discovery; it is not asserted to be a complete set of installed event identifiers.

### 5.5 Resolution and fallback rules

Resolve a presentation in this order: explicit event/context mapping → compatible registered definition/class mapping → documented channel/severity family → native/generic fallback. A more specific style must still preserve the actual event's urgency and actions. Generic fallbacks are useful compatibility coverage; they are not counted as completed bespoke designs for known core events.

Known native content should receive an explicit mapping or a justified family mapping before release. Unsupported mod additions remain usable through the fallback and produce a development diagnostic keyed by identity, without spamming normal play. Do not invent semantic detail from a title, and do not replace every unknown icon with the electrical-warning symbol.

Stacking must retain identity and access to full content. Long titles use ellipsis plus full-content access. Bursts use a scrollable/history access path without losing actionable entries. Two identical labels can refer to different events; deduplicate only when the native event identity/contract permits it. Never dismiss a letter or execute a choice because the stack rearranged under a pointer. Animation affects presentation, not native expiry or urgency.

## 6. Extension inventory across the whole UI

Every project must inspect more than its illustrated happy path:

| Missing-state family | Required extension questions |
|---|---|
| Variable counts | 0/1/many/maximal data; extra groups, tabs, resources, results, items, relations and thoughts |
| Dynamic lifecycle | Creation, removal, expiry, updates while open, map switch, pawn death, destroyed item and stale selection |
| New content | DLC/mod work types, resources, skills, needs, stats, alerts, letters and entire tabs |
| Interaction | Hover/press/focus/disabled, drag/reorder, context menu, tooltips, search, modal priority and cancellation |
| Layout | Narrow/large/ultrawide screens, UI scale, long labels, text wrapping, scrolling and protected screen regions |
| Localization | Numbers/units, translated strings, glyph fallback and text direction when supported |
| Missing data/errors | Empty records, missing preview/icon/media, incompatible definitions, loading and error recovery |
| Unshown bodies | Bio/Health/Log and other native screens: preserve information/actions, extend established shell/content patterns |
| Persistence | New/old settings, renamed IDs, save/load, disable/re-enable, removed DLC/mod and migrations |
| Accessibility | Keyboard traversal, visible focus, full-label access, semantic distinction beyond color and motion preferences |

This inventory is expanded when new cases appear. A worker must not label an unexpected case “out of scope” solely because the concept omitted it. Keep a safe native/family fallback, record the missing design, and finish or explicitly track it under its owning module.

## 7. Decision status and acceptance

Each inference-register entry has an owner project, nearest source analog, proposed or adopted rule, status, relevant work order and tests. Use states: identified → proposed → adopted for implementation → implemented → verified → accepted. Superseded decisions remain traceable. User direction is distinguished from the team's proposed geometry or tokens.

An inferred state passes only when its rationale is consistent with the shared design, all required native information/actions remain available, its visual specimen matches the implementation and the original canonical source states still pass. The lead may adopt routine inferred decisions within the user's direction; adoption is not a requirement for another user confirmation.

Required extension tests include 0/1/3/4/8/20 colonist groups, one oversized group, long names, an ungrouped/new pawn, map/lifecycle changes, each resolved notification family, threat-bearing quests, transient messages, actionable choices, unknown/mod events, simultaneous arrivals, long localized labels and persistence. A reported coverage percentage must state its denominator and whether it measures discovery, mappings or executed tests.

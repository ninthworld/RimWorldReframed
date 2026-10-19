# Post-generation new-colony UI follow-up

19 October 2026. Six user-provided screenshots establish that these surfaces still use the native layout and controls. They extend UI-021 into actionable child tasks in [ui-todo.csv](ui-todo.csv); shared ideology/Biotech work also relates to UI-008 and UI-013. Screenshot numbering follows the user's submission. Original images are preserved locally as evidence, outside the published source media allowlist.

| Screenshot | Task | Observed surface and controls |
|---|---|---|
| 1 | UI-036 | Select starting site: heading, bottom navigation, random site, Factions, learning helper, world search and lower-right route/layer controls. Preserve the globe. |
| 2 | UI-037 | Selected-tile summary, Planet/Terrain tabs, biome details and climate/terrain/movement/pollution statistics, information and close controls. |
| 3 | UI-038 | Starting-site Factions dialog: roster, leaders, ideology links, goodwill/hostility, enemy indicators, scrolling and Close. |
| 4 | UI-039 | Choose your ideoligion: inactive system, fluid/fixed custom options, saved/preset choices, structure/styles, scrolling and Back/Next. |
| 5 | UI-040 | Create characters: selected/left-behind roster, reordering, name fields, pawn details, randomization, apparel toggles, skills/traits/health/relations, team summary, xenotype editor and Start. |
| 6 | UI-041 | Create xenotype: search, selected genes, categories/cards, expand/collapse, complexity/metabolism, name/icon, randomization/options, inheritance/restrictions, load choices and Save and apply. |

All six tasks remain **open**. The screenshots establish the visible states only. Nested editor/load/confirmation states are required follow-up coverage, not claimed observations. Each window and secondary panel needs opening and closing motion, a genuinely translucent backing, retained native callbacks, focus and immediate close cleanup.

## Context boundary and regression requirement

The same world renderer appears during new-colony setup (`UIRoot_Entry`) and an active colony (`UIRoot_Play`). A visible world does not establish a current map, play tab manager, colony or caravan. The new-colony Factions dialog is not the pre-generation Add faction picker or the active-game Factions tab.

Every shared world control must explicitly validate the services it requires. Test generation/site selection with the master switch enabled and disabled; active-colony world navigation and caravan/expedition controls; no current map; and return to the main menu. Native UI must remain usable whenever a replacement is ineligible.

The v0.23.2 freeze fix addresses an unconditional `Find.MainTabsRoot` read in Tools preparation before the eligibility return. That getter casts the UI root to the play root and threw every GUI frame during site selection, even with the mod's own switch disabled. The fix resets owned state and returns before accessing play services; Work closing-state cleanup has the same ordering protection. Regression tests compile the production Tools context code against throwing entry-root services. This functional repair does not complete any of the redesign tasks above.

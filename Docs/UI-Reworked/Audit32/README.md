# Remaining UI audit and implementation TODO

19 October 2026 · AUDIT32 · installed Reframed UI 0.23.1

The principal remaining work is native dialog/content redesign and retrofitting opening/closing motion on older custom surfaces. A changed window background or modern dock button does not establish a redesigned destination. The detailed [TODO list](ui-todo.csv) records 35 open work groups, with priorities, access paths, required changes, acceptance checks and source evidence. [Every discovered native type](native-ui-coverage.csv) has a follow-up group; its assignment is **inferred triage**, not a claim that it was visited or that it uses a particular renderer.

## Audit boundary and evidence

The game metadata scan read **16,158 type definitions** in the installed `Assembly-CSharp.dll` and retained **715 declarations**: **219 distinct Window/InspectTabBase hierarchy declarations** plus **496 UI helper candidates**. Of the 219 hierarchy declarations, 189 are concrete and 30 abstract. These are declarations, not 715 screens or 189 guaranteed reachable screens. Helpers include reusable drawing/state classes and nonvisual support types. Shared dialogs can implement many flows; dynamically generated controls and third-party mod windows are not exhaustively discoverable from this assembly.

| Inventory family, including root | Declarations | Concrete | Abstract |
|---|---:|---:|---:|
| Window, including Page/MainTabWindow | 169 | 145 | 24 |
| InspectTabBase, including map/world inspectors | 50 | 44 | 6 |
| Page subset | 12 | 11 | 1 |
| MainTabWindow subset | 16 | 14 | 2 |
| Map ITab subset | 39 | 35 | 4 |
| World WITab subset | 10 | 9 | 1 |

Subset counts overlap; never sum all rows. The [raw inventory](native-ui-inventory.csv) records generic-aware inheritance, abstract/root status, declared UI method names and source text references. A text reference is not proof of a replacement; absence is not proof of no global styling. The [manifest](native-ui-manifest.json) records hashes, source inputs and the selection boundary. The [mapping summary](coverage-summary.json) reports 715 assigned rows and no unmapped rows, not a restyling percentage.

Observed installation: Core, Ideology and Biotech data; audit profile activates Harmony, those three packages and Reframed UI. Other DLC class declarations exist in the shared assembly but do not establish availability or ownership. `Version.txt` reports **1.6.4871 rev590** while this session's startup log reports **rev591**; both observations are preserved instead of silently normalizing them. Game assembly SHA256: `5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A`. Installed DLL: `1851FCAF146B322CAD9AA6A630708C897A071D5BF5E8DD3D7600B690A48915B8`, source checkpoint `d5a8099c56e9dc2850875c17c908743fc5d493f4`; audit began at clean HEAD `8daf8a7d04d0a9f873c54d01bb8c63fe60147272`.

The source review follows actual patch registration and context predicates. The startup message **123 owned UI surfaces** counts patched targets; it is not a screen-completion count. `ToolsAndWindows.WindowTheme` replaces a background helper during Playing, while native controls and body layouts generally remain. `BottomMenuChildren` adds skin/motion only for eligible dialogs opened from owned contexts. `NewGameSetup` owns three exact page types, not the entire new-colony flow.

## Prioritized work

All rows remain open. Priority means suggested development order, not permission to bypass native behavior or earlier acceptance gates.

| Group | Work |
|---|---|
| UI-001–003 | Inspector, information card, Bio/naming and other secondary-panel open/close motion |
| UI-004 | Native Options and mod settings redesign; investigate blank Reframed settings body observed live |
| UI-005–007 | Colony Load/Save, dropdowns, tooltips, transient messages and alert explanations |
| UI-008–009 | Ideoligions and Mechs destination bodies and their descendants |
| UI-010–014 | Storage/filter, bills, prisoner/slave/training, Biotech and other native inspector bodies |
| UI-015–018 | World/caravan tabs, trade/transfers, policies/areas and full letter/choice/reward bodies |
| UI-019–022 | Mods/Credits/Tutorial, scenario/difficulty/advanced editors, site/pawn setup and missing setup transitions |
| UI-023–025 | Native fragments inside redesigned menus, learning/target overlays and specialized gizmos |
| UI-026–029 | Codex, unavailable-DLC follow-up, remaining native modals and developer UI |
| UI-030–032 | Missing Compare feature, explicit compatibility fallbacks and remaining helper/dynamic UI triage |
| UI-033 | Current-build visual/functional acceptance for already-restyled surfaces |
| UI-034–035 | Shared native input widgets, loading/progress and cinematic screens |

Start with the visible settings defect and the frequent native storage/bills/policy flows; batch shared motion work across inspector/info/dropdowns. Keep native data models and callbacks. The common shell is a useful shared primitive but cannot supply bespoke content, cleanup correctness or motion for every caller by itself.

## Live spot checks

The integrator launched the **unchanged installed DLL** in a new isolated copy of the existing test profile under ignored `work/audit32/profile`. The original test save is preserved. The session uses the existing 2062×1236 client configuration at UI scale 1.5. Read-only navigation is evidence of the listed observations, not gameplay acceptance, a timing test or baseline promotion.

| Observation | Result | Follow-up |
|---|---|---|
| Main menu → Options | Native rectangular body, brown buttons/checkmarks and native category/control layout visibly remain | UI-004 |
| Options → Mod options → Reframed UI | Settled panel shows title/version/Close but no expected settings controls; no matching exception seen in the inspected log. Cause not established | UI-004; investigate before normal settings acceptance |
| Entry Load | Custom browser and translucent panel observed; this does not settle in-game browser motion | UI-005 / UI-033 |
| Loading | Native progress box, gameplay tip and content-list panels remain | UI-035 |
| Paused colony | Native learning-helper text/controls and mouse readout remain alongside custom HUD | UI-024 |
| Pawn inspector / Assign | Custom inspector/roster body observed; native tooltip panel remains opaque in appearance. Motion direction acceptance not sampled | UI-001 / UI-007 / UI-023 / UI-033 |
| Assign → food policy → Edit | Translucent shared shell/compact buttons surround the native policy list, category tree, quality slider and bright native checkbox controls | UI-010 / UI-017 / UI-034 |

Local captures and session log stay under ignored `work/audit32/`; they are intentionally excluded from the GitHub source snapshot. Further spot checks, exact checks run and final save/DLL integrity results are recorded in [verification.json](verification.json). Static lifecycle findings are marked source-inspected; screenshots do not prove both animation directions.

## Definition of done for every implementation group

1. Register actual access paths and all reachable child windows, including empty/dense/readonly/disabled and DLC variants. Assign an owner and bounded source paths before parallel editing.
2. Distinguish source-shown design from an inferred extension. Preserve native controls, data, callbacks, pause state and cancellation.
3. Verify **opening and contracting dismissal**, including nested/secondary panels and rapid reversal. Native closure/input cleanup happens immediately; outgoing presentation owns no live actions.
4. Verify a slightly translucent background without an opaque layer underneath. Check clipping, content reveal, screen-edge anchoring, focus and modal input.
5. Exercise supported scales, long/localized labels, installed DLC and explicit mod/native fallback. Record omitted states honestly.
6. Capture the exact build/hash and complete-screen evidence; fix related findings together. Update this TODO and the existing [visual backlog](../visual-completion.md) and [project register](../project-register.csv). A compile, metadata check or this audit cannot close a visual acceptance gate.

## Refreshing the audit

```powershell
dotnet run --project Tools/UIInventory/UIInventory.csproj --configuration Release -- . $env:RIMWORLD_DIR Docs/UI-Reworked/Audit32
python Tools/classify-ui-inventory.py
```

Run from the repository root with a valid game directory. The first command reads PE metadata without loading game code or exporting method implementations. The second reapplies explicitly ordered name rules in `ui-todo.csv`; review collisions and candidate assignments manually after updates. `coverage-rules.json` makes those rules visible. Repeated unchanged inputs produce identical inventory output. Public CI does not need proprietary game assemblies; a future game/mod configuration needs a fresh local discovery pass.

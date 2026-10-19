# Work-order sequence and template

> **Execution update, 17 October 2026:** usable features now take priority over foundation micro-calibration. Relevant foundation approximations can support feature increments before full fidelity acceptance. W01/W02 and W03/W19 have scoped live verification. W04/W05 share a HUD placement contract and are integrated with scoped checks as v0.8.0. Next define W06 group lifecycle/persistence and W07 resource-history contracts before parallel dispatch; follow [the current delivery policy](04-team-workflow.md#current-delivery-policy--user-direction-17-october-2026). Original exit gates below remain final refinement/release targets rather than blockers for these increments.

Revision 5 · Menu/setup feature increments integrated; full order acceptance remains open

See the [foundation register](foundation-register.csv) and [wave 2 report — local evidence omitted](LOCAL_EVIDENCE.md) for actual assignments and evidence. The table below still defines the complete parent-order exit gates; first increments do not satisfy all of them.

Each order has one clear visual/behavioral scope and a regression obligation. Project IDs refer to the [design specification](01-design-specification.md); gates and test families refer to the [test regimen](02-test-regimen.md). Estimates of duration should follow the evidence and API audit, not precede them.

Use [04-team-workflow.md](04-team-workflow.md) for model routing, ownership, Git/worktrees and stop/resume procedures. Use [05-inferred-design-policy.md](05-inferred-design-policy.md) for routine decisions outside the concept. Assignments and resource ownership are recorded in execution-state.json, and resumable progress in HANDOFF.md. These documents make the user's preference for useful parallel delegation and coherent inference explicit.

## 1. Foundations before feature work

| Order | Deliverable | Dependencies | Exit evidence |
|---|---|---|---|
| F00 Reference calibration | Immutable sources; component/state and inference registers; full-screen/video frame extraction; coordinate/scale mapping; discrepancy decisions | Existing evidence package | Each target state has a source or documented source analog, a geometry contract and an evidence/inference status |
| F01 Typography | Candidate glyph audit; selected font/atlas hashes; size/weight/spacing/baseline table; fallback policy | F00 | Multi-string crops match; actual Unity raster path verified |
| F02 Symbols and textures | Asset IDs, silhouettes, native-asset matches or reconstructions, icon bounds, tint/state variants | F00 | Icon atlas compared to source at original scale |
| F03 Shared primitives | Panel, button families, tabs, tags, checkbox, meters, dividers, clipping and tooltip primitives | F01, F02 | All visible primitive states pass source comparisons; unknown states separately specified |
| F04 Test harness | Isolated fixtures, capture/diff process, manifests, input instrumentation, baseline performance and regression runner | F00; evolves with F03 | One intentional position/color/input defect is detected; clean repeat capture is stable |
| F05 Runtime boundaries | Installed-version API audit, ownership of patched surfaces, native command/data adapters, module settings/fallback | F00 | No duplicated rendering/input; clean start and disable path; supported-version manifest |

F00 is not complete merely because the four boards were recovered. Typography, exhaustive corner tracing and motion extraction remain reconstruction tasks. F04 must test the harness itself: introduce an intentional one-pixel shift, missing icon, altered glyph and duplicate command, then prove the appropriate check fails. This prevents a test suite that always looks green.

## 2. Feature work-order groups

| Order | Scope | Prerequisites beyond shared foundations | Required regression |
|---|---|---|---|
| W01 | P01 Main menu and full-screen anchor | V01 scene calibration | Shared controls, submenu return |
| W02 | P02 Save browser | Native save metadata/thumbnail audit; W01 route | Menu, file identity, native load/delete |
| W03 | P03 Storyteller/playstyle | Selection contract and curve tracing | Setup navigation, save mode |
| W04 | P04 HUD anchors and dock | Canonical scale and collision model | Every dock route, world/modal states |
| W05 | P07 Clock/weather/speed | Native readout/speed audit; W04 | Dock, alerts reserved area, shortcuts |
| W06 | P05 Colonist cards/groups | EXT001–EXT004/EXT026; group lifecycle, membership, overflow and persistence contract; W04 | Canonical groups, 0/1/4/8/20 groups, pawn selection and all inspection panels |
| W07 | P06 Resources/trends | Seven-day-history contract; V02 frames; W04 | HUD width, map switching, count accuracy |
| W08 | P08 Issues/categories | Native/custom alert mapping; W04 | Letter stack, target actions, critical visibility |
| W09 | P09 Notifications | EXT005–EXT010/EXT024; loaded event/channel mapping and fallback catalog; frame-indexed motion spec; W08 stacking model | All supported families, unknown fallback, issues, modal/letter actions, queue identity and canonical toasts |
| W10 | P10 Tools | Hover hit-region/state contract; V02 frames; W04 | Map input, overlays, shortcuts |
| W11 | P11 Search | Search scope/performance contract; W10 | Tools navigation, selection and stale items |
| W12 | P12 Compare | Stat direction/unit model; W10 | Item selection, overlays, search/modal input |
| W13 | P13 Work | Priority mapping/ties/autosort decisions | Native work scheduling, pawn lifecycle, persistence |
| W14 | P14 Pawn shell | Header status-symbol mapping; W06 selection contract | All tabs and pawn switching |
| W15 | P15 Summary | Meter discrepancy decision; W14 | Pawn shell, stats, equipment, needs |
| W16 | P16 Needs | Thought novelty and extended-needs contract; W14 | Summary, live mood, tooltips/scrolling |
| W17 | P17 Social | Relation/event adapters and action eligibility; W14 | Pawn shell, role/romance, selection |
| W18 | P18 Gear | Armor/mass/temp adapters; W14 | Summary equipment, item info/actions, comparison |
| W19 | P19 DLC | Media/ownership assets and fallback contract | Main menu, missing expansion, links |
| W20 | P20 Extensions | Inventory of all unshown reachable screens | Native feature parity and other UI mods |
| W21 | P21 Integration and release | All scoped W orders; F04 evidence | Full accepted suite, persistence, supported configurations, performance/soak |

All feature orders depend on the relevant portions of F00–F05. Independent orders can eventually be assigned separately, but only one owner should change a shared primitive or native drawing surface at a time. A shared-token change is its own reviewed change with dependent captures, not an incidental adjustment hidden in a feature order.

## 3. Required work-order fields

Use this template when assignments begin:

```text
Order ID and title:
Project and requirement IDs:
Purpose and visible result:
Assigned role/model and worker ID:
Worktree absolute path, branch and base commit:
Owned paths and shared-resource access:

Source evidence:
  Board/video ID, source hash, crop or event/frame range
  Canonical client resolution, UI scale and component origin
  Evidence classification and relevant unresolved issues
  Inference IDs, adopted rules and source analogs for unshown states

Components and states:
  Exact controls, assets, labels, geometry and style token IDs
  Normal / hover / pressed / selected / disabled / focus
  Empty / overflow / loading / missing-data / error states as applicable
  Animation timeline and pointer/focus behavior

Behavioral contract:
  Native data sources and command targets
  Before/after examples with concrete game state
  Persistence, unavailable-feature and cancellation behavior

Implementation boundary:
  Owned renderer/patch/module and shared dependencies
  Allowed extension behavior where reference is absent
  Excluded changes and fallback behavior

Acceptance:
  Visual source/actual/diff cases and exact-match scope
  Live functional cases and expected command counts/IDs
  Integration, compatibility and persistence cases
  Performance budget and affected regression families

Deliverables:
  Source/assets/build, run manifest, fixture and screenshots
  Test results, logs, open discrepancies and reproducible steps
  Updated execution-state and handoff, dirty files and next concrete action

Completion status:
  Evidence ready / implemented / visual verified / functional verified /
  integration verified / accepted
  Do not collapse incomplete states into Done.
```

## 4. Change and regression policy

When later work changes a shared component, list every dependent project before coding. Re-run those captures and input cases after the change. Updating a baseline requires showing what changed and why; a baseline update cannot be used to approve a new mismatch. Fixes to game behavior must retain the approved visual state unless a documented conflict makes that impossible.

Keep source evidence, measured raster values, inferred rendering parameters and approved extension decisions in separate fields. A proposed font or corner radius is not promoted to measured fact when it first appears in code.

Do not replace real controls with a baked screenshot of the reference. Raster assets may faithfully reproduce borders, icons or glyphs, but controls must remain interactive, data-driven and adaptable. A reference-value render fixture demonstrates fidelity; live-game cases demonstrate usefulness.

The first assignable package is F00–F05, followed by a small vertical slice such as W04 plus W05. That establishes a measured HUD component, native behavior and repeatable in-game proof before scaling up to every panel. This sequence does not reduce the final scope.

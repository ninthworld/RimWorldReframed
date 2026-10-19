## WORK26 — Priorities controls number editing (current)

This contract supersedes the historical mode prerequisites below. `AutoAssign` works with Priorities On or Off and never switches global mode. It applies the existing assignment policy to raw values, including enable/disable and skill/passion priorities, through the unchanged Work25 `WorkPanelPriorityDelta` transaction and raw Undo/conflict protections. Clear and Undo also preserve either mode. A mode change still expires Undo rather than restoring into a different work context.

`WorkPanelCell.StoredPriority` and `WorkPanelNative.StoredPriority(pawn,type)` expose the native saved number for the view's gray read-only Off state. `Cell.Priority`, `Enabled`, the original native tooltip and scheduling-order semantics remain effective/native. Missing or unusable raw data returns0 in the readonly accessor and fails closed in mutating actions; no settings are initialized on read/open. Existing raw-field guards remain sufficient.

`CycleEnabledPriority` refuses Priorities Off without changing mode or assignment, and cycles the saved number1→4→3→2→1 only while On. The left tag still toggles through original MouseDown/native callbacks, but its before/after values and remembered enabled number are raw in both modes. Thus a saved1/2/4 survives disabling and reenabling a job while Off instead of becoming the native effective3. An unremembered job defaults to3. Mode notifications/`SetManual` invalidate Undo while preserving session memory; only full game/map/window `ResetSession` clears that memory. Age/permanent-disabled rejection, temporary capacity rules, native sounds/warnings/tutorials and setter notifications are retained.

The view/integrator must use `StoredPriority` for the displayed number and enable number editing only with `Manual && cell.Enabled && cell.Editable`. Auto assign must not be gated on `Manual`. No new Harmony class, reflection surface or native guard is required. Focused tests cover saved1..4 off/on round trips, read-only Off cycle gating, Auto assign/Undo in either mode with unchanged mode, raw hidden/disabled values and callback/context conflicts. Build output is private `work/work26-build/`; no primary/game/process/save/installation actions are part of this worker task.

Worker receipts: private Release build0 warnings/errors;1849 policy/raw-delta/toggle/cycle/audit checks;163 installed native metadata/guard checks; exact1checkbox+4setter and missing/partial-seam fail-open checks. Read-only native review found no concrete blocker. These are offline receipts, not in-game input/visual/persistence acceptance.

## WORK25 — Auto assign, Clear and raw Undo (historical)

v0.20.6 supersedes enabled-only Autosort with explicit selected-pawn Auto assign. See `Docs/UI-Reworked/Features/Batch24/README.md` for the user-directed footer and inferred policy. Auto assign requires manual priorities; Clear and Undo preserve either mode. Both use the actual shared `WorkPanelPriorityDelta` apply/match/count path over raw priorities, with native adapter context/eligibility checks and native setters. Unknown unskilled, disabled and temporarily incapable jobs are preserved by Auto assign; Clear may disable temporarily incapable work. Hidden definitions remain unchanged.

1816 policy/transaction/toggle/cycle/audit checks pass, including exact raw0..4 round trips, enable/disable/priority restoration, mode/context conflicts, null raw values and callback side effects.321 layout and28 view checks plus364 game-member guards and exact1checkbox+4setter/fail-open checks pass separately. Native live Clear/Auto assign/Undo and visual checks remain pending. Historical Autosort evidence below is retained and is not v0.20.6 acceptance.

# Work native adapter contracts

## WORK23 separate numbered button (current)

User direction supersedes the four-row/drag controller described below. Left tag still calls `Toggle` for one-click on/off. A separate enabled-tag number button calls `CycleEnabledPriority(window,pawn,type)` with the original unconsumed left MouseDown; the controller consumes it afterward. This method cycles the DISPLAYED `Cell.Priority` as2→1→4→3→2, never Off. With Manual Off, effective displayed3 therefore targets2 regardless of stored raw1..4.

The number-button intent is validated before any mode change: native roster/type, initialized/EverWork settings, enabled priority1..4 and `Cell.Editable`. Temporary total capacity incapacity remains editable, matching native semantics; disabled/age-disabled work cannot activate manual mode through this button. A valid Off-mode click calls existing `SetManual(true)`, preserving every stored priority and native all-player-pawn cache notifications, then revalidates game/map/settings/eligibility before writing only the target type through existing `SetEnabledPriority`. If raw target was already desired, mode activation succeeds with change count0 and an explicit notice; no redundant setter is called. Composite `cycle-priority` audit begins before mode activation so false→true is recorded. No synthetic/repeated toggle input, new patch class, reflection surface or guard is introduced. Autosort does not auto-enable manual priorities.

`WorkPanelPolicy.NextPriority` preserves invalid/Off inputs; `CanCycleEnabledPriority` rejects them and noneditable work. Focused tests cover the full cycle from every starting value, invalid/disabled cases, no Off selection and effective3→2. All build output stays in `work/work23-build/`; no game/process/save/UI/deployment actions are performed by this worker.

## WORK21 one-click delta (current)

Current owned files also include `Source/WorkPanelAudit.cs`. The top editor now uses `Toggle(window,pawn,type)` on the original unconsumed left MouseDown. This one call disables every enabled native priority1..4, or restores the last enabled manual priority for that settings/work-def pair (default3). Checkbox mode retains native0/3. No synthetic events, repeated clicks, mass enable or scheduling changes are introduced. The original native body still owns age/permanent disability handling, temporary incapacity semantics, warnings, tutorial knowledge and sounds. Its exact1checkbox+4setter calls are replaced together, preserving stack/control flow; any mismatch fails open. `Activate` remains only as legacy cycle compatibility. The integrated controller now uses four horizontal priority rows and calls SetEnabledPriority only on a valid same-pawn drop. The upper editor no longer exposes priority cycles or Shift bulk edits; historical adapter APIs remain available for native compatibility.

`Reset()` clears Undo/notices only. `ResetSession()` additionally clears enabled-priority memory and belongs to the full window/map/game/feature lifecycle, never ordinary pawn selection. Mode changes clear session memory; switching selected pawns does not. `SetEnabledPriority(window,pawn,type,priority)` is used by the drag controller:1..4 requires already-enabled work/manual mode;0 is Off via the native setter and remembers the prior value. It creates no menu or UI assumption.

Autosort/Undo return nonempty success notices and `LastChangeCount`/`LastNotice`, including an explicit Undo-unavailable warning if post-operation validation rejects the snapshot. Rejected/no-change explicit Toggle, Autosort, Undo and SetEnabledPriority attempts are also audited when enabled. Audit is passive and uses the existing `-reframed-pawn-bar-audit` plus `Prefs.DevMode` gate through `PawnBarDiagnostics.Enabled`; it writes `<SaveDataFolder>/ReframedUI/WorkAudit/actions.jsonl`. Each record includes complete native roster, all loaded work definitions, raw/effective priorities and manual state before/after. Raw values are read from private `Pawn_WorkSettings.priorities`; no diagnostic setter or command is added. At1MiB per record/8MiB total the writer stops with one warning, preserving all existing evidence; it never truncates snapshots or deletes/rotates evidence.

WORK21 guards: unchanged28 methods plus9fields (one new `Pawn_WorkSettings.priorities:Verse.DefMap<WorkTypeDef,int>`). Patch registration remains the same3classes below. Private build command uses `-p:OutputPath=../work/work21-build/`. Run the two test projects sequentially because their directory shares MSBuild intermediate output. Current receipts:655 pure policy/toggle/memory/audit checks;827 installed method bodies parsed with0failures;163 native metadata/IL/guard checks; candidate exact1+4 substitutions, missing-seam and partial-setter fail-open checks pass. No game/UI/process/save/deployment input performed.

## Original adapter contracts (retained)

Owned implementation: `Source/WorkPanelNative.cs`, `Source/WorkPanelPolicy.cs`.

The table remains native. `MainTabWindow_PawnTable.PostOpen()` must run unchanged so its private `table` is initialized. `Roster(window)` returns its actual sorted `PawnsListForReading`; `WorkTypes(window)` reads its visible `Columns`. No read/open path initializes work settings or changes a priority. `Compatible(window)` rejects unknown non-work columns and custom priority workers so the integrator can retain the whole native window. Additional work definitions with the standard native priority worker remain supported.

`Cell.Priority` is the native effective priority; `Skill` is the native mean of relevant skills, or null for no-skill work (never native fallback3 disguised as skill). Native `TipForPawnWorker` already reports translated Priority0..4, real disabled reasons, capacities, ideology warnings and skill details. `Editable` excludes disabled work; temporary total capacity incapacity still permits native editing. `Activatable` additionally allows age-disabled work to emit its original rejection message.

Call `Activate(window,pawn,type,button)` with the original, unconsumed MouseDown0/1. It runs actual `WidgetsWork.DrawWorkBoxFor` centered under the pointer on this input event only. No old box paints because it never runs on Repaint. Native warnings, knowledge, sounds, setters and disable notifications remain in that method. Its one checkbox `ButtonInvisible(Rect,bool)` call is redirected only during this explicit activation; elsewhere it delegates unchanged. No synthetic event is generated. Caller should not invoke a second native callback. Shift+tag uses `ActivateHeader` instead, independent of the selected pawn's eligibility, to retain native all-roster bulk editing. `Sort`/`SortColumn` use native table comparers and exact native three-state sort cycles without replaying modifiers.

Render every `StatusColumns` entry using actual `DrawStatus` native callbacks/tooltips. Stock icons need `StatusWidth` (26px minimum) and 30px height: native icon layout hardcodes its vertical row height30. The view owns clipping and must not consume the event before those cells. `CanPasteFor(window)` respects the table's actual copy worker; `CanPaste` only reflects the ordinary native static clipboard. Copy/Paste invoke the native protected callback once, preserving all-loaded-def masks and notifications.

Autosort is a user-approved inference: explicit click, selected roster pawn, manual mode required. Enabled skill jobs use `average skill + 2 × highest passion` (None0/Minor1/Major2): scores >=14 -> priority1, >=9 ->2, >=4 ->3, otherwise4. Disabled0, permanent disabled, temporary total incapacity and no-relevant-skill jobs remain untouched. This preserves existing emergency/care/support priorities when they have no skill. It operates on the native table's visible work jobs; it preserves all other loaded definitions. It never mass-enables work or changes the global work mode.

Undo snapshots exact changed values and validates all loaded definitions, all current priority values, native disabled/capacity states, the settings instance, pawn, game, map and manual mode before writing any delta. A narrow SetPriority observer expires Undo after outside value changes, including changed-away-and-back edits. Native mode notification expires it immediately. Integrator calls `Reset()` when selected pawn, map/game/window lifecycle or feature eligibility changes; call `CanUndo` only for the currently selected pawn, not every roster row. No Undo record survives a conflicted validation. Mod callbacks that alter additional values during Autosort prevent creation of an unsafe Undo record.

Register exactly these three Harmony classes:

- `WorkPanelCheckboxActivationPatch`
- `WorkPanelPriorityObservationPatch`
- `WorkPanelModeObservationPatch`

`Ready` requires reflected native surfaces and the unique checkbox seam. Unmatched IL returns original instructions and leaves Ready false. `NativeGuards.json` specifies28 exact method signatures and8 fields, including every private/protected reflection target; the integrator deduplicates existing guards. The view/integrator owns setting, window/lifecycle geometry patches, native fallback and diagnostics.

Run from the worker root (all output below ignored `work/`; installed assemblies remain read-only):

```powershell
dotnet build Source/ReframedUI.csproj -c Release -p:OutputPath=../work/work20-build/ -p:NuGetAudit=false --no-restore
dotnet run --project Tests/WorkPanelNativeContracts/WorkPanelPolicyContracts.csproj -c Release -p:OutputPath=../../work/work-policy-tests/
dotnet run --project Tests/WorkPanelNativeContracts/NativeAudit.csproj -c Release -p:OutputPath=../../work/work-native-audit/ -- 'C:/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed/Assembly-CSharp.dll' work/work-native/native.json
& Tests/WorkPanelNativeContracts/Test-NativeSeams.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/WorkPanelNativeContracts/Test-InstalledSeam.ps1 -ModAssembly work/work20-campaign/v0200-build/ReframedUI.dll
```

Run the two projects in this directory sequentially: they share the default intermediate directory. The candidate seam audit executes the actual transpiler against installed IL, checks its one stack-compatible substitution and unchanged control flow, then verifies that a missing seam leaves the input unchanged and disables ownership. It never installs patches or invokes gameplay.

Receipts: private full build0 warnings/errors;575 pure policy checks;827 installed method bodies parsed with0 failures;159 installed metadata/IL/guard checks. The PE reader never loads/initializes the target assembly. These checks establish policy and installed seams; they do not establish in-game visual/input, mod compatibility or save/reload acceptance. No game/process/UI/save input or live DLL deployment was performed by this worker.

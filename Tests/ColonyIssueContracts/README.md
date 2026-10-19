# Colony issues contracts

This worker owns only `ColonyIssues.cs`, `ColonyIssuePolicy.cs`, `ColonyIssueMotion.cs` and this test directory. `RightHudIcons.cs` is a root-supplied compile aid, excluded from delivery. No Harmony patch class is added here.

## Coordinator API

- `CanHandle(List<Alert>)` requires the installed native base `DrawAt(float,bool)` for every instance. A custom override declines the entire replacement so native drawing and arbitrary custom controls survive. Unknown alerts inheriting native drawing remain reachable under Environment with the warning icon.
- `Prepare(List<Alert>, float width, float maxHeight, double now)` consumes already calculated native `Active`, `Label` and `Priority`; never recalculates native game reports. It returns bounded animated height. Call with an empty list until closing completes, rather than dropping ownership immediately.
- `Draw(Rect screenBounds, bool input)` paints within the supplied screen rectangle. `input=false` is paint-only: no clicks, wheel acquisition or delayed tips. Root should pass false beneath modal surfaces or during coordinator motion.
- `HoveredAlert` / `HoveredRect` expose settled, fully visible hovered rows. Root maps native instances back to the original `activeAlerts` index, calls native `DrawInfoPane`, demonstrates Alerts knowledge, and invokes `CheckAddOrRemoveAlert`. Root must preserve native Update/highlight timing. Supplemental condition wrappers are skipped for native index/recheck.
- `ExternalKind: Func<Alert,ColonyIssueKind?>` allows the coordinator to classify real condition wrappers before native type mapping. Reset intentionally retains this hook. The wrapper's real condition text/report/action remains authoritative; heatwave is a GameCondition rather than a manufactured native Alert.
- `Reset()` clears animation, entries, scroll and group choices for a full lifecycle change. It does not run gameplay callbacks.

## Native seams / root guards

Direct worker dependencies (all on `RimWorld.Alert`):

| Member | Exact shape |
| --- | --- |
| DrawAt | public virtual instance `UnityEngine.Rect (System.Single,System.Boolean)` |
| OnClick | protected virtual instance `System.Void ()` |
| get_Active | public instance `System.Boolean ()` |
| get_Label | public instance `System.String ()` |
| get_Priority | public virtual instance `RimWorld.AlertPriority ()` |
| AlertPriority | enum Medium=0, High=1, Critical=2 |

Root native hover integration: `Alert.DrawInfoPane():void`, `Alert.GetReport():AlertReport`, private `AlertsReadout.activeAlerts:List<Alert>`, private `AlertsReadout.mouseoverAlertIndex:int`, private instance `AlertsReadout.CheckAddOrRemoveAlert(Alert,bool):void`. Root owns any patch registrations/extra guards for its coordinator. Worker adds none.

The worker calls the original native `Widgets.ButtonInvisible` once, then the protected virtual native OnClick through an open-instance delegate. Native target cycling and mod overrides retain their actual instance state and original event. Both .NET10 and Windows .NET Framework offline tests prove this virtual-delegate dispatch shape. No game alert is constructed or activated during tests.

## Inferred presentation policy

Canonical groups are Colonists, Resources and Environment. Type-based semantic classification uses native names, never translated label matching. Default is all expanded, matching the expanded source image. Batch27 user corrections supersede the original always-visible headings and critical-collapse exception: only populated groups remain after contraction, every group fully collapses and retains its total count, and native Critical alerts are routed to independent notification cards by RightHud. High is not Critical. Within a group priority descends, then original native active index breaks ties.

220px width, 24px rows, 20px headings and 8px padding are inferred from P08/reference style. Slightly translucent shared panel fill and inset row fill do not add an opaque backing. Labels use paint-only11px type; full native labels survive in tooltip/native detail even when fitted to the row. Grapheme-safe ellipsis avoids damaged Unicode; rich native/mod tags remain intact and clipped. Shared rails appear only on actual vertical overflow; no horizontal scrolling.

Opening/closing and group/row reveal use reversible220ms real-time motion. Paused simulation does not freeze the clock. Rows cannot receive clicks or hover tips while any layout is moving or their row is partially clipped. During reflow only the most recently toggled heading can reverse, and only if its actual screen rectangle has stayed fixed; moving targets cannot capture unrelated clicks. The root's own moving shell should pass input=false. Entries disappearing from the native list become inert during contraction.

Actual relative-order inversions among surviving entries (priority changes or native tie-order changes) use a180ms position tween from their current rendered slots. Input remains blocked until every position settles. A second inversion retargets continuously; ordinary arrival/removal index shifts do not restart sorting motion, and group disclosure keeps its existing layout flow. Position progress uses a double to reach exact settlement consistently across frame rates.

## Checks

From this isolated checkout:

```powershell
dotnet build Source/ReframedUI.csproj -c Release -p:OutputPath=../work/issues-build/ -p:NuGetAudit=false --ignore-failed-sources
dotnet run --project Tests/ColonyIssueContracts/ColonyIssueContracts.csproj -c Release -p:OutputPath=../../work/issues-tests/ -p:NuGetAudit=false --ignore-failed-sources
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/ColonyIssueContracts/Test-NativeGuards.ps1
```

Actual worker results: private full build0warnings/0errors;1396 policy/motion/callback checks (including position inversion/reversal, invalid clocks and frame-independent settlement);17 installed metadata/.NET Framework dispatch checks. The initial test target net8 requested an unavailable local reference pack; the project now uses the installed net10 SDK and passes without adding a runtime dependency to the mod (which remains net472).

No game, save, process control, screenshot automation, installed DLL replacement or in-game acceptance was performed. Integration still requires root live checks for all3 groups, collapse/reversal, critical visibility, long/mod labels, dense overflow, native target cycling/hover, notification reservation, condition wrappers and complete custom-DrawAt fallback.

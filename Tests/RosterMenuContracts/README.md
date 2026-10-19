# Compact roster menu contracts — BOTTOM31

Owned implementation: `Source/RosterMenus*.cs`. Design record: `Docs/UI-Reworked/Features/Batch30/roster-design.md`. No new Harmony patch classes. The integrator owns the shared shell, native primitive skin, opening/closing motion, child windows and runtime registration.

API remains `RosterMenus.Supports(Window)`, `PreferredSize(Window)`, `Draw(Window,Rect body)`, `Reset(Window)`, `Ready` and scoped `Drawing`. New `Func<Rect,IDisposable> PopupScope` wraps actual UI-local control rectangles; the integrator assigns its child-popup anchor helper. The callback is optional and never used to invoke gameplay actions.

Supports requires Playing/current map, exact Schedule/Assign/Animals/Wildlife window, initialized native table, native-assembly table/workers, exactly one exact Label worker and all private seams ready. Unknown custom windows, tables or workers decline atomically. Foreign Harmony patches on loaded DoHeader methods/base chains, or the custom-painted Schedule cell/palette, also decline atomically; only `chris.reframedui` is allowed. Additional non-core Timetable columns decline rather than disappear. Native PostOpen initializes the table; no roster, column or DLC definitions are reconstructed.

## Composition and preserved controls

- Schedule: compact palette and24 rounded hourly tiles, original assignment selection/held painting and copy/paste. Its exact native cell runs with transparent paint, then the custom visual is drawn. No scheduling setter is implemented here.
- Assign: Food/Drug dropdowns, medical and hostility icons. Grouped drawer retains apparel, reading, inventory carry, identity/status, policy management, forced-apparel clear and other loaded native data columns.
- Animals: master, bond, medicine and slaughter; Care/Training/Details drawer retains every additional column, prerequisites, areas/pen conditions, release, follow, sterilization and native bulk actions. Actual AutoSlaughter dialog remains available.
- Wildlife: Hunt/Tame, actual danger chances, predator/info; drawer retains additional identity/status and original controls, native rejection checks and tooltips.

Default detail drawer is closed. Name or More toggles it; portrait retains native jump/select/escape behavior.32px rows,2px gaps; roster height grows to8 visible rows then scrolls vertically. Full shell widths: Schedule780, Assign660, Animals680, Wildlife620. Three-pawn default heights: Schedule248, other214;8-pawn default heights:418/384. Details add only measured controls, capped at252px content with a vertical rail. Root screen clamp can reduce it further. There is no horizontal scroll view or horizontal rail in this renderer.

Drawer height changes tween220ms both directions, retain current height on reversal, and disable the body's controls/pointer until settled. Root still owns whole-window motion. Native timetable held input ignores GUI.enabled, so clipped/moving rows also receive an offscreen mouse; scope cleanup restores GUI and mouse in finally. Dropdown chevrons map only their trailing16px pointer position into the adjacent actual native control, preserving the real event/type/button and GUI capture. No synthetic events or repeated native clicks are used.

## Exact additional guards

Retain existing BOTTOM30 guards (`MainTabWindow_PawnTable.table:PawnTable`, private `PawnColumnWorker_Label.GetLabel(Pawn):TaggedString`, native table/cell/header/camera/portrait/dialog surfaces). New reflected seams:

- `RimWorld.PawnColumnWorker.HeaderClicked(UnityEngine.Rect,RimWorld.PawnTable):void` — protected virtual instance, derived bulk callbacks dispatch normally.
- `RimWorld.PawnColumnWorker.GetHeaderTip(RimWorld.PawnTable):string` — protected virtual instance.
- `RimWorld.TimeAssignmentSelector.DrawTimeAssignmentSelectorFor(UnityEngine.Rect,RimWorld.TimeAssignmentDef):void` — private static.

New direct public properties: `PawnColumnDef.get_HeaderIcon():Texture2D` and `get_HeaderInteractable():bool`. Tiny primary headings use the real semantic icon; full native header text remains in tooltips. Management headers preserve their actual upper32px action controls at native65px geometry under clip; separate custom headings call original HeaderClicked. All real DoCell callbacks remain in charge of gameplay.

## Checks and limits

```powershell
dotnet build Source/ReframedUI.csproj -c Release '-p:RimWorldDir=C:/Program Files (x86)/Steam/steamapps/common/RimWorld' -p:OutputPath=../work/bottom31-build/ -p:NuGetAudit=false --no-restore
dotnet run --project Tests/RosterMenuContracts/RosterMenuContracts.csproj -c Release -p:OutputPath=../../work/roster31-tests/ -p:NuGetAudit=false --ignore-failed-sources
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/RosterMenuContracts/Test-NativeSeams.ps1
```

Pure checks cover compact extents, scroll shrink/end reachability, actual vertical-only header reservation, complete-row input gating and reversible height motion at15/30/60/144Hz including invalid/regressing clocks. Installed metadata/IL checks verify native seams, held-input scheduler ownership, actual cell/header calls, no scheduling/training setter replacement, no PawnTableOnGUI wrapper and finally cleanup. Tests construct no game/window, invoke no game callbacks, install no patches, perform no GUI/game input and keep proprietary audit/build output ignored.

Actual worker checks:555 pure contracts and30 installed metadata/IL contracts pass. Private Release build succeeds with0errors; its only warning is the unassigned PopupScope integration hook, which the root assigns. Independent source review released after correcting the81px small-screen detail viewport: each heading/cell/management button is gated separately, so a complete32px management button remains reachable at the scroll end even when its combined83px card exceeds the viewport. Normal enabled painting is independent of pointer hover.

Live acceptance belongs to the integrator/user: readability at UI scales and localization, painted schedules and drag across rows, copy/paste, all policy management and forced apparel, native area/pen layouts, training/bulk prerequisites, AutoSlaughter, Wildlife safety checks/chances, drawer/whole-shell motion and input, dropdown anchoring, DLC and custom-mod fallback. These are not claimed as passed by compilation or metadata tests.

# Bottom30 World/Factions/History contracts

Owned files: `Source/WorldMenus*.cs`, this directory and `Docs/UI-Reworked/Features/Batch29/world-design.md`. Private worktree build only; no installed assembly, game, process or save touched.

Run:

```powershell
dotnet run --project Tests/WorldMenuContracts/WorldMenuContracts.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/WorldMenuContracts/Test-Native.ps1
dotnet build Source/ReframedUI.csproj -c Release -p:OutputPath=../work/bottom30-build --no-restore
```

The pure test links the actual policy and existing actual ToolsPanelMotion. It verifies native faction and archive visibility truth tables (including archived dialogs), real chart date bounds, complete-row activation, fixed-bottom monotonic220ms reveal, reverse continuity and immediate-close inert-shell expiry. The metadata test loads assemblies for reflection only; it does not apply Harmony or invoke game initializers.

## Integration API

`WorldMenus.Supports(Window)`, `PreferredSize(Window)`, `Draw(Window,Rect body)`, `Reset(Window)` support exact Factions/History windows. Root owns their main-tab margin/header/native lifetime/open+close motion and common translucent shell. Native PreOpen must still run: History initializes recorder group/range, recounts wealth and resets search; Factions clears scroll target. Call Reset on true close/context reset. `Supports` is intended after native PreOpen for History; native tabs must number3. Unknown subclasses or foreign Harmony changes to the replaced native renderer/page/preopen methods use original whole-window presentation rather than dropping extensions.

Wire `WorldMenus.Enabled` to the root common setting. World inspector owns its own native-window-specific reversible motion and no opaque backing. Expose `WorldMenus.WorldDrawing` to root native typography/widget scope and popup ownership. OR `WorldMenus.CommandDrawing` into the existing shared Architect command face/icon/label paint guards; do not add another Command IL transpiler. Its scope surrounds real WorldGizmoUtility.WorldUIOnGUI and stock-assembly Command.GizmoOnGUIInt only. Native command geometry/input, gizmo enumeration, disabled state, right-click, hotkeys, tutorial state and callbacks remain native.

Register these8 patch classes:

| Class | Exact native surface |
|---|---|
| WorldMenusPreparePatch | Verse.WindowStack.WindowStackOnGUI(), prefix before shadow pass/postfix inert close shell |
| WorldMenusContentsPatch | RimWorld.Planet.WorldInspectPane.DoWindowContents(Rect), positional `__0` |
| WorldMenusExtraPatch | RimWorld.InspectPaneUtility.ExtraOnGUI(IInspectPane), scoped actual World pane |
| WorldMenusWidthPatch | RimWorld.InspectPaneUtility.PaneWidthFor(IInspectPane), right edge includes8px inset |
| WorldMenusClosePatch | Verse.Window.PostClose(), prefix, exact owning World pane only |
| WorldMenusTabRectPatch | Verse.InspectTabBase.get_TabRect(), only actual filling/extra tab |
| WorldMenusCommandsPatch | RimWorld.Planet.WorldGizmoUtility.WorldUIOnGUI(), scope prefix/finalizer |
| WorldMenusCommandPaintPatch | Verse.Command.GizmoOnGUIInt(Rect,GizmoRenderParms), scope prefix/finalizer |

No native IL changes in this module. Existing shared command painter has its own independently validated exact substitutions; readiness/fallback remains root-owned.

## Guard inventory

The executable metadata test is the authoritative exact signature list:36 checks. Reflection-only fields are Factions.scrollToFaction; History.historyAutoRecorderGroup, graphSection, curTab, showLetters, showMessages, tabs; InspectTabBase.size. Reflection-only methods are FactionUIUtility.GetOngoingEvents/GetRecentEvents/GetNaturalGoodwillExplanation(Faction):TaggedString; InspectTabBase.get_StillValid():bool, FillTab/ExtraOnGUI/UpdateSize():void; InspectPaneUtility.InterfaceToggleTab(InspectTabBase,IInspectPane):void; Window.SetInitialSizeAndPosition():void. Existing public APIs are directly compiler-bound and additionally tested for patched/lifecycle/action-critical members.

## Receipts and open gates

Actual private result:152 pure checks and36 installed metadata checks pass; full private Release succeeds with zero compiler errors/warnings, one cached NU1900 vulnerability-feed warning. The candidate is in ignored `work/bottom30-build`; integration runtime registration, startup, visual/motion/transparency, UI scales, world tab coordinate-dependent extras, custom mods, route/caravan/jump interactions and archive actions remain live gates for the root/user. No game acceptance claim.

## BOTTOM31 refinement

`WorldMenusHistoryChart.cs` now paints the graph from actual native arrays rather than invoking DrawGraph. `WorldMenusHistoryGraphPolicy.cs` provides the production interpolation, bounds, peak-preserving reduction, clipping and compact legend sizing used by107 new direct checks in `HistoryGraphTests.cs`. Existing152 tests remain, and `Test-Native.ps1` now validates51 members (the36 above plus15 direct chart fields/getters). The native renderer names remain compatibility seams: foreign patches on HistoryAutoRecorderGroup.DrawGraph or SimpleCurveDrawer.DrawCurves cause whole-History native fallback rather than skipping mod-added plot behavior.

Root wires `WorldMenusHistory.PopupScope` to `BottomMenuChildren.AnchorDropdown`; no additional Harmony patch. Root may use780×460 preferred size. New visible differences include outlined navigation tabs, chevron group selector, active segmented time range, checkbox archive filters, muted chart/axes, actual tale markers, complete legend and nearest-series hover. Root owns shell/popup motion and integration. Actual private Release output is ignored `work/bottom31-build`, zero compiler errors/warnings with1 cached NU1900. No startup/live acceptance. See Batch30/history-design.md.

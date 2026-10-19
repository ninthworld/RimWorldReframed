# Selection15 action command contracts

`Source/SelectionCommands.cs` adds a nested paint ownership scope around `RimWorld.MapGizmoUtility.MapUIOnGUI()` and native `Verse.Command.GizmoOnGUIInt(Rect, GizmoRenderParms)`. Both patches have `void` prefixes/finalizers; neither skips native execution, changes a `GizmoResult`, swallows an exception or adds a second Command transpiler. Scope snapshots restore on ordinary and exceptional exits. Mod-provided command subclasses and non-Command gizmos retain their native rendering.

Integrated shared changes, owned by root:

1. Use the dedicated `Settings.SelectionPanels` gate.
2. Register `SelectionMapCommandsPatch` and `SelectionCommandPaintPatch` in the explicit bootstrap allow-list.
3. Guard `MapGizmoUtility.MapUIOnGUI`: static `void`, no arguments. The existing `Command.GizmoOnGUIInt` guard remains instance `GizmoResult`, arguments `Rect`, `GizmoRenderParms`.
4. In `ArchitectPanel.DrawCommandBackground`, `DrawNativeLabelBackground`, `DrawTileIcon` and `DrawTileLabel`, expand each `!DrawingGrid` guard to `(!DrawingGrid && !SelectionCommands.Drawing)`. Retain the original atlas/native-assembly checks. Keep grid geometry and Architect extras scoped to `DrawingGrid` only.

The shared painter supplies the accepted flat tile surface, native icon, bottom pawn-style gradient, 10-pixel label and native lowlight/disabled tint. Full label/description access continues through native command tooltips. The native dispatch retains toggle checkboxes, grouping/merged toggle semantics, original hotkeys, selection actions, material/color targeting, custom overlays, disabled reasons and event consumption. Native 36-pixel shrinking remains icon-only with its native tooltip; this increment does not invent shrunk labels or reduce custom gizmo sizes.

The native map selection grid still computes its starting X as 14 plus `InspectPaneUtility.PaneWidthFor` for the current inspect window. Root/inspector integration must keep that pane-width contract aligned with the new inspector. No action-grid geometry is changed by this source. World gizmos have a separate native caller and remain outside the scope.

`dotnet run --project Tests/SelectionContracts/SelectionContracts.csproj -c Release` checks 13 explicit ownership invariants plus 200 alternating eligible/ineligible GUI contexts. These exercise outside-grid commands, nested custom commands, nested ineligible dispatchers, restoration and exceptional exits.

`powershell.exe -NoProfile -File Tests/SelectionContracts/Test-InstalledCommandScopes.ps1` checks installed native method signatures/caller seam, ensures the added patches cannot replace results/skip native behavior, admits native Action/Toggle types and rejects a dynamic mod-derived Command type without constructing or initializing any game command. It does not patch or render the game.

Worker build command: `dotnet build Source/ReframedUI.csproj -c Release -p:OutputPath=../work/selection-actions-build/`. Output stays in ignored work; no installed DLL is written. Live styling requires the root-owned shared helper changes and bootstrap integration, followed by the authorized in-game campaign.

Required live coverage: single-entity Allow/Forbid and other native actions; multi-selection toggle/group behavior; hover/full tooltip/disabled/hotkey; native special command icon/color behavior; pawn ability and weapon rows; command shrinking/large mixed gizmo sets; custom-mod fallback; world/tutorial/settings fallback; new inspector and dock collision review. No live acceptance or source-pixel match is claimed by these offline checks.

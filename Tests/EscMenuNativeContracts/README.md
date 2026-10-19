# ESC28 native option adapter

Owned deliverable: `Source/EscMenuNative.cs` and this directory. The worker does not own the panel, window lifecycle, geometry, settings, dock/Tools integration or runtime bootstrap.

## API / ownership

`EscMenuNative.TryCollect(MainTabWindow_Menu owner, out EscMenuOptions options)` returns the original native option objects under `options.Actions` and `options.Links`, each an `IReadOnlyList<EscMenuOption>`. The exact native window must already be open in the WindowStack after its real PreOpen and be the current main tab, with ProgramState.Playing and a real Current.Game. PreOpen remains native: knowledge/countdown/file discovery are never repeated here.

Each option supplies `Native`, `Label`, `Tooltip`, `Icon` (also `DrawIcon` alias), `Activate():bool`, and `Highlight(Rect)`. Root draws real settled GUI buttons and calls Activate once only when that button returns true. Highlight retains the original private tutorial tag; call it only for fully shown, input-enabled rows. Activate also checks the original window/game, native input ownership and captured commitment/saving/file-availability state. Root should collect every GUI pass so actual native availability remains fresh; never cache a snapshot across closing/reopening or a game change.

Activate dispatches the original native `action` delegate, or the original exact WebLink URL when action is null. The native action-before-URL branch is unchanged. No extra close, confirmation, save, commitment-mode policy or quit behavior is created. Native ButtonText has no separate click sound; the root's native ButtonInvisible preserves its mouseover control behavior. WebLink soundtrack actions retain their original FloatMenu callbacks. No actual URL or game action is invoked by tests.

## Capture safety / fallback

One narrowly scoped listing prefix captures two calls and suppresses their entire native draw/control loops while collection is active. Outside that scope it returns true without changes. The native controller still constructs its actual lists and closures using current game state. GUI.enabled=false and an empty clip hide paint and guard controls; no Event replacement, mutation, Use or synthetic click is performed. Finally restores GUI style/state, keyboard/hot focus and MainMenuDrawer's private webRect/webBackgroundYMax caches.

Exact plain `ListableOption` and exact `ListableOption_WebLink` are supported. Null/malformed options, unknown subclasses (including custom DrawOption controls), nonplaying contexts, custom menu subclasses, nested collection and listing-count mismatch reject the whole custom menu. Root must render its complete native fallback, not discard unsupported options. Collection never invokes DrawOption or an option action.

Any foreign Harmony prefix/postfix/transpiler/finalizer on either the native menu controller or option-listing surface also declines ownership before collection. Only this adapter's own exact listing prefix is permitted. This intentionally conservative fallback preserves mod-added drawing, later list mutations and arbitrary controls outside the captured lists.

## Registration / exact guards

Register only `EscMenuOptionListingPatch`, targeting:

- public static `Verse.OptionListingUtility.DrawOptionListing(UnityEngine.Rect,System.Collections.Generic.List<Verse.ListableOption>):System.Single`

New native members relied upon:

| Declaring type | Member / exact shape |
| --- | --- |
| RimWorld.MainMenuDrawer | public static `DoMainMenuControls(Rect,bool):void` (installed body has exactly2 listing calls) |
| RimWorld.MainMenuDrawer | private static `webRect:Rect`, `webBackgroundYMax:float` |
| RimWorld.MainTabWindow_Menu | private instance `anyGameFiles:bool` |
| RimWorld.MainTabWindow_Menu | public override `PreOpen():void`, `DoWindowContents(Rect):void` (root retains these) |
| Verse.ListableOption | public virtual `DrawOption(Vector2,float):float` |
| Verse.ListableOption | public instance fields `label:string`, `action:System.Action`; private `uiHighlightTag:string` |
| Verse.ListableOption_WebLink | public override `DrawOption(Vector2,float):float`; public fields `url:string`, `image:Texture2D` |
| Verse.UIHighlighter | public static `HighlightOpportunity(Rect,string):void` |

Standard existing lifecycle calls are `MainTabsRoot.get_OpenTab`, `WindowStack.get_Windows`, `WindowStack.GetsInput(Window)`, current game/ProgramState and `GameDataSaveLoader.get_SavingIsTemporarilyDisabled`. Root owns those shared guards and its window patches. Worker adds no menu-controller transpiler and does not patch ListableOption.DrawOption globally.

## Actual checks / limitations

```powershell
dotnet build Source/ReframedUI.csproj -c Release '-p:RimWorldDir=C:/Program Files (x86)/Steam/steamapps/common/RimWorld' -p:OutputPath=../work/esc28-build/ -p:NuGetAudit=false --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/EscMenuNativeContracts/Test-NativeContracts.ps1
```

Actual private build:0warnings/0errors. Native metadata/IL/capture/dispatch:35 checks passed. The script loads assemblies but applies no Harmony patches, creates no game/save/window, executes no GUI and invokes only test-owned delegates/fake URL receivers. It checks actual2-listing seam and native callbacks, original object identity, unknown custom rejection, all4 foreign-patch categories, capture transparency/suppression, finally cleanup and absence of Event mutation.

Live acceptance remains root-owned: ordinary/commitment/no-save availability, scenario/options/load/save/quit confirmations, soundtrack/web actions, tutorial highlight, Escape/native focus, scale/localization/long rows and unsupported mod fallback. No live game/process/save access, installed build or DLL deployment occurred in this worker.

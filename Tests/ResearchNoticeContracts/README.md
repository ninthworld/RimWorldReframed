# Research completion notice contracts

Owned files: `Source/ResearchNotice.cs`, `ResearchNoticePatches.cs`, `ResearchNoticePolicy.cs`, this test directory. `RightHudIcons.cs` is a root-provided compile aid, not a worker deliverable.

The ordinary native research completion is `Dialog_NodeTree`, not a Letter. The constructor adapter retains that exact instance, the actual ResearchProjectDef, the two original DiaOption objects, the native force-pause/absorb-input flags, the original delayed interactivity deadline and all original completion/discovery IL. Each native option is activated through its own original private `Activate()` method: close-before-action, pre/post-close, and Research tab selection remain native. Custom or changed nodes/options/callbacks retain the original dialog. No English matching is used for ownership.

Inferred presentation: 320×82 logical pixels, full-size clipped220ms bottom-left reveal, green check/title/time, an always-visible research action and a single X which activates the original OK option. Full native node text remains available by tooltip after the native input deadline. Native close cleanup is immediate; a shell-only noninteractive ghost contracts for220ms. An absorbing newer window hides outgoing paint. Background/shadow ownership is established before the WindowStack shadow pass, restored on setting/fallback/close, and excludes an opaque native underlay. Small viewports fall back to the original dialog. Root can set `ResearchNotice.Enabled` and `ProtectedTop` when integrating shared HUD geometry.

Run private tests:

```
dotnet run --project Tests/ResearchNoticeContracts/ResearchNoticeContracts.csproj -c Release
powershell.exe -NoProfile -File Tests/ResearchNoticeContracts/Test-NativeSeam.ps1 -ModAssembly work/research-build/ReframedUI.dll
```

The native seam test loads metadata/IL and executes only the transpiler, never a game, save, research completion or window constructor. It verifies one constructor replacement plus actual project argument, all remaining native IL/control-flow unchanged, and missing/duplicate/nonadjacent fail-open cases. The pure tests execute the real shared timing class and layout policy. These tests do not claim live Unity presentation or native UI input acceptance.

Root registration: `ResearchNoticeCreationPatch`, `ResearchNoticePreparePatch`, `ResearchNoticeSizePatch`, `ResearchNoticeMarginPatch`, `ResearchNoticeContentsPatch`, `ResearchNoticeClosePatch`. No globally scoped native paint patches are needed; custom painting is entirely within the exact owned dialog's `DoWindowContents` callback. Reset API restores active native owners and clears ghosts; root may invoke it on global UI reset.

Required native guards: `ResearchManager.FinishProject(ResearchProjectDef,bool,Pawn,bool):void`; `Dialog_NodeTree(DiaNode,bool,bool,string)`; `WindowStack.Add(Window):void`; `WindowStack.WindowStackOnGUI():void`; `Dialog_NodeTree.get_InitialSize():Vector2`; `Window.get_Margin():float`; `Dialog_NodeTree.DoWindowContents(Rect):void`; `Dialog_NodeTree.PostClose():void`; `Window.SetInitialSizeAndPosition():void`; `Dialog_NodeTree.get_InteractiveNow():bool` (private); `DiaOption.Activate():void` (private); `Dialog_NodeTree.curNode:DiaNode` (private), `screenFillColor:Color`; `DiaNode.options:List<DiaOption>`; `DiaNode.text:TaggedString`; `DiaOption.dialog:Window`, `action:Action`, `resolveTree:bool`, `disabled:bool`, `link:DiaNode`, `linkLateBind:Func<DiaNode>`, `hyperlink:Dialog_InfoCard.Hyperlink`; `Window.doWindowBackground/drawShadow/doCloseX/doCloseButton:bool`; `Window.windowRect:Rect`; `WindowStack.GetsInput(Window):bool`. Native constructor's pause/absorb/input flags are intentionally never assigned by this module.

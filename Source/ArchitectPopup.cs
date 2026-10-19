using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Shared dropdown chrome. Ownership follows actual Architect or inspector
    // controls; unrelated map/order menus and custom subclasses stay native.
    internal static class ArchitectPopup
    {
        private sealed class Owner { internal bool inspector, information, work, bottomMenu; }
        private static readonly ConditionalWeakTable<FloatMenu, Owner> Menus = new ConditionalWeakTable<FloatMenu, Owner>();
        private static readonly FieldInfo ScrollPosition = AccessTools.Field(typeof(FloatMenu), "scrollPosition");
        internal static bool Ready, Drawing, DrawingInspector, DrawingInformation;
        internal static FloatMenu CurrentMenu;
        internal static Rect ScrollViewport;
        internal static float ScrollHeight;
        private static GUIStyle labelStyle;

        internal static bool Owned(FloatMenu menu) => Ready && menu != null && Menus.TryGetValue(menu, out Owner owner) &&
            ReframedMod.PatchesReady && ReframedMod.Settings != null && ReframedMod.Settings.Enabled &&
            (owner.bottomMenu ? ReframedMod.Settings.BottomMenus : owner.information ? ReframedMod.Settings.InformationCards :
            ReframedMod.Active && (owner.work ? ReframedMod.Settings.WorkPanel : owner.inspector ? ReframedMod.Settings.SelectionPanels : ReframedMod.Settings.BuildMenu));
        internal static bool InspectorOwned(FloatMenu menu) => menu != null && Menus.TryGetValue(menu, out Owner owner) && owner.inspector;
        internal static bool InformationOwned(FloatMenu menu) => menu != null && Menus.TryGetValue(menu, out Owner owner) && owner.information;
        internal static bool WorkOwned(FloatMenu menu) => menu != null && Menus.TryGetValue(menu, out Owner owner) && owner.work;
        internal static bool SuppressLauncherTooltip => !Drawing && Find.WindowStack.Windows.OfType<FloatMenu>().Any(Owned);

        internal static void RegisterOwned(FloatMenu menu)
        { if (Ready) Menus.GetValue(menu, _ => new Owner()); }
        internal static void RegisterWork(FloatMenu menu)
        { if (Ready) Menus.GetValue(menu, _ => new Owner()).work = true; }
        internal static void RegisterBottomMenu(FloatMenu menu)
        { if (Ready) Menus.GetValue(menu, _ => new Owner()).bottomMenu = true; }

        internal static void Register(Window window)
        {
            if (!Ready || window?.GetType() != typeof(FloatMenu)) return;
            bool information = InfoCardPanel.Drawing || InformationPopupScope.Active || (Drawing && DrawingInformation);
            bool inspector = PawnInspectorPopup.Active || (Drawing && DrawingInspector);
            bool work = WorkPanel.Drawing || (Drawing && WorkOwned(CurrentMenu));
            if (!(information || inspector || work || ArchitectPanel.DrawingGrid || ArchitectPanel.DrawingExtras || Drawing)) return;
            Menus.GetValue((FloatMenu)window, _ => new Owner { inspector = inspector, information = information, work = work });
        }

        internal static void BeginScroll(Rect viewport, ref Vector2 position, Rect view, bool showScrollbars)
        {
            if (!Drawing) { Widgets.BeginScrollView(viewport, ref position, view, showScrollbars); return; }
            ScrollViewport = viewport; ScrollHeight = view.height;
            // Keep native mouse-over stack, Steam Deck handling and GUI fixes.
            Widgets.BeginScrollView(viewport, ref position, view, false);
        }
        internal static void EndScroll()
        {
            Widgets.EndScrollView();
            if (!Drawing || CurrentMenu == null || ScrollHeight <= ScrollViewport.height) return;
            var position = (Vector2)ScrollPosition.GetValue(CurrentMenu);
            PawnInspectorWidgets.Rail(new Rect(ScrollViewport.xMax - 5, ScrollViewport.y, 4, ScrollViewport.height),
                ref position.y, ScrollHeight, ScrollViewport.height, false);
            ScrollPosition.SetValue(CurrentMenu, position);
        }

        internal static void DrawOptionBackground(Rect rect, Texture texture, FloatMenuOption option)
        {
            if (!Drawing) { GUI.DrawTexture(rect, texture); return; }
            using (new Theme.GuiScope(true))
            {
                float fade = GUI.color.a;
                GUI.color = Color.white;
                Color face = !option.Disabled && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface;
                face.a = (option.Disabled ? .35f : .72f) * fade;
                Theme.RoundRect(rect.ContractedBy(3f, 1f), face, 3f);
            }
        }

        internal static void DrawOptionAtlas(Rect rect, Texture2D texture)
        { if (!Drawing) Widgets.DrawAtlas(rect, texture); }

        internal static void DrawOptionIcon(Rect rect, Texture texture, float scale, Vector2 proportions, Rect texCoords,
            float angle, Material material, float uiScale)
        {
            bool checkedState = ReferenceEquals(texture, Widgets.GetCheckboxTexture(true));
            if (!Drawing || (!checkedState && !ReferenceEquals(texture, Widgets.GetCheckboxTexture(false))))
            { Widgets.DrawTextureFitted(rect, texture, scale, proportions, texCoords, angle, material, uiScale); return; }
            using (new Theme.GuiScope(true))
            {
                float fade = GUI.color.a;
                GUI.color = new Color(1, 1, 1, fade);
                Rect box = new Rect(rect.center.x - 5.5f, rect.center.y - 5.5f, 11, 11);
                Theme.RoundRect(box, checkedState ? Theme.Good : Theme.Muted, 2);
                Theme.RoundRect(box.ContractedBy(1), Theme.Surface, 1);
                if (checkedState) Theme.RoundRect(box.ContractedBy(3), Theme.Good, 1);
            }
        }

        internal static void DrawOptionLabel(Rect rect, string label)
        {
            if (!Drawing) { Widgets.Label(rect, label); return; }
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (labelStyle == null || labelStyle.font != Text.CurFontStyle.font)
                {
                    labelStyle = new GUIStyle(Text.CurFontStyle) { fontSize = 12, alignment = TextAnchor.MiddleLeft,
                        wordWrap = true, richText = true, clipping = TextClipping.Clip,
                        padding = new RectOffset(), margin = new RectOffset() };
                    labelStyle.normal.textColor = Color.white;
                }
                // Preserve native disabled/fade tint. Icons, callbacks, tooltips,
                // extra-part controls and tutorial/input paths are untouched.
                GUI.Label(rect, label, labelStyle);
            }
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class ArchitectPopupOwnerPatch
    {
        private static void Prefix(Window __0) => ArchitectPopup.Register(__0);
    }

    [HarmonyPatch(typeof(FloatMenu), nameof(FloatMenu.DoWindowContents))]
    internal static class ArchitectPopupContentsPatch
    {
        private static readonly FieldInfo BaseColor = AccessTools.Field(typeof(FloatMenu), "baseColor");
        private sealed class State { internal bool drawing, inspector, information, owned; internal FloatMenu menu; internal Theme.GuiScope scope; internal Rect viewport; internal float height; }
        private static void Prefix(FloatMenu __instance, Rect __0, out State __state)
        {
            __state = new State { drawing = ArchitectPopup.Drawing, inspector = ArchitectPopup.DrawingInspector, menu = ArchitectPopup.CurrentMenu,
                information = ArchitectPopup.DrawingInformation, viewport = ArchitectPopup.ScrollViewport, height = ArchitectPopup.ScrollHeight };
            ArchitectPopup.Drawing = ArchitectPopup.Owned(__instance);
            ArchitectPopup.DrawingInspector = ArchitectPopup.InspectorOwned(__instance);
            ArchitectPopup.DrawingInformation = ArchitectPopup.InformationOwned(__instance);
            ArchitectPopup.CurrentMenu = __instance;
            if (!ArchitectPopup.Drawing) return;
            __state.owned = true;
            __state.scope = new Theme.GuiScope(true);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                // Retain the native distance fade from its latest GUI event.
                Color face = Theme.PanelFill; face.a = .94f * ((Color)BaseColor.GetValue(__instance)).a;
                Theme.RoundRect(__0, face, 4f);
            }
        }
        private static void Finalizer(State __state)
        {
            if (__state == null) return;
            ArchitectPopup.Drawing = __state.drawing;
            ArchitectPopup.DrawingInspector = __state.inspector;
            ArchitectPopup.DrawingInformation = __state.information;
            ArchitectPopup.CurrentMenu = __state.menu;
            ArchitectPopup.ScrollViewport = __state.viewport;
            ArchitectPopup.ScrollHeight = __state.height;
            if (__state.owned) __state.scope.Dispose();
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source = instructions.ToList();
            var begin = AccessTools.Method(typeof(Widgets), nameof(Widgets.BeginScrollView),
                new[] { typeof(Rect), typeof(Vector2).MakeByRefType(), typeof(Rect), typeof(bool) });
            var end = AccessTools.Method(typeof(Widgets), nameof(Widgets.EndScrollView), Type.EmptyTypes);
            if (source.Count(i => i.Calls(begin)) != 1 || source.Count(i => i.Calls(end)) != 1)
            {
                Log.Warning("[Reframed UI] Dropdown scroll seam changed; native scrollbar retained.");
                return source;
            }
            return source.Select(original =>
            {
                var instruction = new CodeInstruction(original);
                if (instruction.Calls(begin)) instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.BeginScroll));
                else if (instruction.Calls(end)) instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.EndScroll));
                return instruction;
            }).ToList();
        }
    }

    [HarmonyPatch(typeof(FloatMenu), "get_ColumnWidth")]
    internal static class ArchitectPopupWidthPatch
    {
        private static void Postfix(FloatMenu __instance, ref float __result)
        { if (ArchitectPopup.Owned(__instance)) __result = Math.Max(180f, __result); }
    }

    [HarmonyPatch(typeof(FloatMenuOption), nameof(FloatMenuOption.DoGUI))]
    internal static class ArchitectPopupOptionPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source = instructions.ToList();
            MethodInfo background = AccessTools.Method(typeof(GUI), nameof(GUI.DrawTexture), new[] { typeof(Rect), typeof(Texture) });
            MethodInfo atlas = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawAtlas), new[] { typeof(Rect), typeof(Texture2D) });
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            MethodInfo icon = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawTextureFitted), new[] {
                typeof(Rect), typeof(Texture), typeof(float), typeof(Vector2), typeof(Rect), typeof(float), typeof(Material), typeof(float) });
            if (source.Count(c => c.Calls(background)) != 1 || source.Count(c => c.Calls(atlas)) != 1 || source.Count(c => c.Calls(label)) != 1 || source.Count(c => c.Calls(icon)) != 1)
            {
                ArchitectPopup.Ready = false;
                Log.Warning("[Reframed UI] Native Architect popup paint IL changed; native popup retained.");
                return source;
            }
            var result = new List<CodeInstruction>();
            foreach (var original in source)
            {
                var instruction = new CodeInstruction(original);
                if (instruction.Calls(background))
                {
                    var owner = new CodeInstruction(OpCodes.Ldarg_0);
                    owner.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    result.Add(owner);
                    instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.DrawOptionBackground));
                }
                else if (instruction.Calls(atlas))
                    instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.DrawOptionAtlas));
                else if (instruction.Calls(label))
                    instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.DrawOptionLabel));
                else if (instruction.Calls(icon))
                    instruction.operand = AccessTools.Method(typeof(ArchitectPopup), nameof(ArchitectPopup.DrawOptionIcon));
                result.Add(instruction);
            }
            ArchitectPopup.Ready = true;
            return result;
        }
    }
}

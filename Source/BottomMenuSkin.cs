using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Native specialized controls retain their callbacks, but share the typography,
    // flat button treatment and narrow overflow rail of the surrounding redesign.
    internal static class BottomMenuSkin
    {
        private static GUIStyle label;
        internal static bool Active => BottomMenuShell.Drawing || BottomMenuChildren.Drawing || WorldMenus.WorldDrawing;
        internal static GUIStyle Style
        {
            get
            {
                if (label == null || label.font != Text.CurFontStyle.font)
                {
                    label = new GUIStyle(Text.CurFontStyle) { richText = true, clipping = TextClipping.Clip,
                        padding = new RectOffset(), margin = new RectOffset() };
                    label.normal.textColor = Color.white;
                }
                label.fontSize = Text.Font == GameFont.Tiny ? 11 : 12;
                label.wordWrap = Text.WordWrap; label.alignment = Text.Anchor;
                return label;
            }
        }
        internal static void Label(Rect rect, string value) => GUI.Label(rect, value ?? "", Style);
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })]
    internal static class BottomMenuLabelPatch
    { private static bool Prefix(Rect __0, string __1) { if (!BottomMenuSkin.Active) return true; BottomMenuSkin.Label(__0, __1); return false; } }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcHeight), new[] { typeof(string), typeof(float) })]
    internal static class BottomMenuTextHeightPatch
    { private static bool Prefix(string __0, float __1, ref float __result) { if (!BottomMenuSkin.Active) return true; __result = BottomMenuSkin.Style.CalcHeight(new GUIContent(__0 ?? ""), Math.Max(1, __1)); return false; } }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcSize), new[] { typeof(string) })]
    internal static class BottomMenuTextSizePatch
    { private static bool Prefix(string __0, ref Vector2 __result) { if (!BottomMenuSkin.Active) return true; __result = BottomMenuSkin.Style.CalcSize(new GUIContent(__0 ?? "")); return false; } }
    [HarmonyPatch(typeof(Text), "get_LineHeight")]
    internal static class BottomMenuLineHeightPatch
    { private static bool Prefix(ref float __result) { if (!BottomMenuSkin.Active) return true; __result = BottomMenuSkin.Style.CalcHeight(new GUIContent("Ag"), 1000); return false; } }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawButtonGraphic))]
    internal static class BottomMenuButtonPatch
    { private static bool Prefix(Rect __0) { if (!BottomMenuSkin.Active) return true; Theme.ButtonFace(__0, GUI.enabled && Mouse.IsOver(__0) ? Theme.Hover : Theme.Surface, !GUI.enabled); return false; } }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawMenuSection))]
    internal static class BottomMenuSectionPatch
    { private static bool Prefix(Rect __0) { if (!BottomMenuSkin.Active) return true; Theme.RoundRect(__0, Theme.Surface.ToTransparent(.3f), 3); return false; } }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawHighlightSelected))]
    internal static class BottomMenuSelectedPatch
    { private static bool Prefix(Rect __0) { if (!BottomMenuSkin.Active) return true; Theme.RoundRect(__0, Theme.Selected.ToTransparent(.65f), 3); return false; } }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawBoxSolid))]
    internal static class BottomMenuRewardSurfacePatch
    {
        private static bool Prefix(Rect __0, Color __1)
        {
            // Native quest rewards use this exact neutral row backing. Preserve
            // semantic fills, progress meters and unrelated dialogs unchanged.
            if (!(BottomMenuShell.Current is MainTabWindow_Quests) ||
                __1 != new Color(.13f, .13f, .13f)) return true;
            Theme.RoundRect(__0, Theme.Surface.ToTransparent(.45f), 3); return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.BeginScrollView))]
    internal static class BottomMenuScrollPatch
    {
        private static void Prefix(ref Rect __0, ref Vector2 __1, Rect __2, ref bool __3)
        {
            if (!BottomMenuSkin.Active || !__3) return;
            Rect outer = __0;
            InfoCardLayout.ScrollSpace(outer.width, outer.height, __2.width, __2.height, out float width, out float height, out bool vertical, out bool horizontal);
            __1.x = Mathf.Clamp(__1.x, 0, Math.Max(0, __2.width - width));
            __1.y = Mathf.Clamp(__1.y, 0, Math.Max(0, __2.height - height));
            if (vertical) PawnInspectorWidgets.Rail(new Rect(outer.xMax - 5, outer.y, 4, height), ref __1.y, __2.height, height, false);
            if (horizontal) PawnInspectorWidgets.Rail(new Rect(outer.x, outer.yMax - 5, width, 4), ref __1.x, __2.width, width, true);
            __0.width = width; __0.height = height; __3 = false;
        }
    }
    [HarmonyPatch(typeof(QuickSearchWidget), nameof(QuickSearchWidget.OnGUI))]
    internal static class BottomMenuNativeSearchPatch
    {
        private static bool Prefix(QuickSearchWidget __instance)
        {
            Window window = Find.WindowStack.currentlyDrawnWindow;
            // Body renderers place the native search in their toolbar. Suppress
            // only Window's automatic footer copy outside that body scope.
            return BottomMenuShell.Drawing || window == null || !BottomMenuShell.Eligible(window) || window.CommonSearchWidget != __instance;
        }
    }
    [HarmonyPatch(typeof(WindowStack), "get_AnySearchWidgetFocused")]
    internal static class BottomMenuSearchFocusPatch
    { private static void Postfix(ref bool __result) { if (WorldMenusHistory.SearchFocused) __result = true; } }
}

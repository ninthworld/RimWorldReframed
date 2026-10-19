using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Share presentation with the browser without replacing native tooltip
    // registration, text getters, delay, lifetime or mouse-relative placement.
    internal static class MenuTooltipStyle
    {
        private static readonly MethodInfo FinalText = AccessTools.PropertyGetter(typeof(ActiveTip), "FinalText");
        private static GUIStyle nativeStyle;
        private static float lastScale;
        private static bool failed;
        internal static bool Enabled => !failed && (EntryScene.Active ||
            ReframedMod.Active && ReframedMod.Settings.WindowTheme);
        internal static float Scale => Current.ProgramState == ProgramState.Playing ? 1f : Mathf.Min(1f, NewGameSetup.FitScale);
        internal static GUIStyle NativeStyle
        {
            get
            {
                using (new Theme.GuiScope(true))
                {
                    Text.Font = GameFont.Small;
                    if (nativeStyle == null || lastScale != Scale || nativeStyle.font != Text.CurFontStyle.font)
                    {
                        lastScale = Scale;
                        nativeStyle = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.RoundToInt(12f * Scale),
                            alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true,
                            padding = new RectOffset(), margin = new RectOffset() };
                        nativeStyle.normal.textColor = Color.white;
                    }
                    return nativeStyle;
                }
            }
        }
        internal static Rect Measure(ActiveTip tip)
        {
            string label = (string)FinalText.Invoke(tip, null);
            float padding = 8f * Scale, maxWidth = Mathf.Min(260f * Scale, UI.screenWidth - 16f);
            float width = Mathf.Clamp(NativeStyle.CalcSize(new GUIContent(label)).x + padding * 2, padding * 2 + 1, maxWidth);
            float height = NativeStyle.CalcHeight(new GUIContent(label), width - padding * 2) + padding * 2;
            return new Rect(0, 0, Mathf.Ceil(width), Mathf.Ceil(height));
        }
        internal static void Draw(Rect rect, string label, GUIStyle style, float padding, float scale)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                Theme.RoundRect(rect, Theme.Rgb(0x27323B), 4f * scale);
                Theme.RoundRect(rect.ContractedBy(.65f * scale), Theme.Rgb(0x111A21), 3.4f * scale);
                GUI.color = Theme.Rgb(0xBAC9D2);
                GUI.Label(rect.ContractedBy(padding), label, style);
            }
        }
        internal static void Fail(Exception error)
        {
            failed = true;
            Log.Warning("[Reframed UI] Menu tooltip styling disabled; native tooltips retained. " + error.Message);
        }
    }

    [HarmonyPatch(typeof(ActiveTip), "get_TipRect")]
    internal static class MenuTooltipMeasurePatch
    {
        private static bool Prefix(ActiveTip __instance, ref Rect __result)
        {
            if (!MenuTooltipStyle.Enabled) return true;
            try { __result = MenuTooltipStyle.Measure(__instance); return false; }
            catch (Exception error) { MenuTooltipStyle.Fail(error); return true; }
        }
    }
    [HarmonyPatch(typeof(ActiveTip), "DrawInner")]
    internal static class MenuTooltipDrawPatch
    {
        private static bool Prefix(Rect bgRect, string label)
        {
            if (!MenuTooltipStyle.Enabled) return true;
            try { MenuTooltipStyle.Draw(bgRect, label, MenuTooltipStyle.NativeStyle, 8f * MenuTooltipStyle.Scale, MenuTooltipStyle.Scale); return false; }
            catch (Exception error) { MenuTooltipStyle.Fail(error); return true; }
        }
    }
}

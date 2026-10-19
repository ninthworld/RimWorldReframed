using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(InspectPaneFiller), "DrawInspectString")]
    internal static class ObjectInspectorDescriptionStringPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo native = AccessTools.Method(typeof(Widgets), nameof(Widgets.LabelScrollable),
                new[] { typeof(Rect), typeof(string), typeof(Vector2).MakeByRefType(), typeof(bool), typeof(bool), typeof(bool) });
            ObjectInspectorDescription.StringReady = codes.Count(code => code.Calls(native)) == 1;
            if (!ObjectInspectorDescription.StringReady)
            { Log.Warning("[Reframed UI] Object inspect-string seam changed; retaining native descriptions."); return codes; }
            foreach (CodeInstruction code in codes) if (code.Calls(native)) code.operand = AccessTools.Method(typeof(ObjectInspectorDescription), nameof(ObjectInspectorDescription.InspectString));
            return codes;
        }
    }
    [HarmonyPatch(typeof(InspectPaneFiller), "DrawHealth")]
    internal static class ObjectInspectorDescriptionHealthPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo native = AccessTools.Method(typeof(WidgetRow), nameof(WidgetRow.FillableBar),
                new[] { typeof(float), typeof(float), typeof(float), typeof(string), typeof(Texture2D), typeof(Texture2D) });
            ObjectInspectorDescription.HealthReady = codes.Count(code => code.Calls(native)) == 1;
            if (!ObjectInspectorDescription.HealthReady)
            { Log.Warning("[Reframed UI] Object health paint seam changed; retaining native descriptions."); return codes; }
            foreach (CodeInstruction code in codes) if (code.Calls(native))
            { code.opcode = System.Reflection.Emit.OpCodes.Call; code.operand = AccessTools.Method(typeof(ObjectInspectorDescription), nameof(ObjectInspectorDescription.HealthMeter)); }
            return codes;
        }
    }
    [HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.FillableBar))]
    internal static class ObjectInspectorDescriptionBarPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo native = AccessTools.Method(typeof(Widgets), nameof(Widgets.FillableBar),
                new[] { typeof(Rect), typeof(float), typeof(Texture2D), typeof(Texture2D), typeof(bool) });
            ObjectInspectorDescription.BarReady = codes.Count(code => code.Calls(native)) == 1;
            if (!ObjectInspectorDescription.BarReady)
            { Log.Warning("[Reframed UI] Object health background seam changed; retaining native descriptions."); return codes; }
            foreach (CodeInstruction code in codes) if (code.Calls(native)) code.operand = AccessTools.Method(typeof(ObjectInspectorDescription), nameof(ObjectInspectorDescription.Fill));
            return codes;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })]
    internal static class ObjectInspectorDescriptionLabelPatch
    {
        private static bool Prefix(Rect __0, string __1)
        { if (!ObjectInspectorDescription.Drawing) return true; ObjectInspectorDescription.Label(__0, __1); return false; }
    }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcHeight), new[] { typeof(string), typeof(float) })]
    internal static class ObjectInspectorDescriptionHeightPatch
    {
        private static bool Prefix(string __0, float __1, ref float __result)
        {
            if (!ObjectInspectorDescription.Drawing) return true;
            __result = ObjectInspectorDescription.Style(Text.WordWrap).CalcHeight(new GUIContent(__0 ?? ""), Math.Max(1, __1)); return false;
        }
    }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcSize), new[] { typeof(string) })]
    internal static class ObjectInspectorDescriptionSizePatch
    {
        private static bool Prefix(string __0, ref Vector2 __result)
        {
            if (!ObjectInspectorDescription.Drawing) return true;
            __result = ObjectInspectorDescription.Style(false).CalcSize(new GUIContent(__0 ?? "")); return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawButtonGraphic))]
    internal static class ObjectInspectorDescriptionButtonPatch
    {
        private static bool Prefix(Rect __0)
        {
            if (!ObjectInspectorDescription.Drawing) return true;
            Theme.ButtonFace(__0, GUI.enabled && Mouse.IsOver(__0) ? Theme.Hover : Theme.Surface, !GUI.enabled); return false;
        }
    }
}

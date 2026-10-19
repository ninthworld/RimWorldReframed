using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The native permit detail had no scroll and anchored Accept over long text.
    // Keep all native eligibility/callback code; measure actual labels and move
    // only its bottom anchor below them inside an owned vertical scroll area.
    internal static class InfoCardPermits
    {
        private sealed class View { internal Vector2 Scroll; internal float Height; internal RoyalTitlePermitDef Permit; }
        private static readonly ConditionalWeakTable<Dialog_InfoCard, View> views = new ConditionalWeakTable<Dialog_InfoCard, View>();
        private static readonly MethodInfo Native = AccessTools.Method(typeof(PermitsCardUtility), "DoLeftRect", new[] { typeof(Rect), typeof(Pawn) });
        internal static bool DispatchReady, DetailReady;
        internal static bool Ready => Native != null && DispatchReady && DetailReady;
        private static bool drawing;
        private static float bottom;

        internal static void Draw(Rect rect, Pawn pawn)
        {
            if (!InfoCardPanel.Drawing) { Native.Invoke(null, new object[] { rect, pawn }); return; }
            View view = views.GetValue(InfoCardPanel.CurrentCard, card => new View());
            if (view.Permit != PermitsCardUtility.selectedPermit)
            { view.Permit = PermitsCardUtility.selectedPermit; view.Scroll = Vector2.zero; view.Height = 0; }
            float height = Math.Max(rect.height, view.Height);
            float width = PawnInspectorWidgets.BeginScroll(rect, ref view.Scroll, height);
            bool old = drawing; float oldBottom = bottom;
            drawing = true; bottom = 0;
            try
            {
                Native.Invoke(null, new object[] { new Rect(0, 0, width, height), pawn });
                // Measure labels only: the native bottom-anchored button must not
                // make a previous oversized extent permanently unable to shrink.
                view.Height = InfoCardLayout.PermitHeight(rect.height, bottom);
            }
            finally
            {
                drawing = old; bottom = oldBottom;
                PawnInspectorWidgets.EndScroll(rect, ref view.Scroll, height, 180803);
            }
        }
        internal static void Label(ref Rect rect, string text, bool updateHeight, bool noWrap)
        {
            Widgets.LabelCacheHeight(ref rect, text, updateHeight, noWrap);
            if (drawing) bottom = Math.Max(bottom, rect.yMax);
        }
        internal static float AcceptHeight(ref Rect rect) => drawing ? InfoCardLayout.PermitHeight(rect.height, bottom) : rect.height;
    }

    [HarmonyPatch(typeof(PermitsCardUtility), nameof(PermitsCardUtility.DrawRecordsCard))]
    internal static class InfoCardPermitDispatchPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo native = AccessTools.Method(typeof(PermitsCardUtility), "DoLeftRect", new[] { typeof(Rect), typeof(Pawn) });
            InfoCardPermits.DispatchReady = codes.Count(code => code.Calls(native)) == 1;
            if (!InfoCardPermits.DispatchReady) { Log.Warning("[Reframed UI] InfoCard permit dispatch seam changed; retaining native cards."); return codes; }
            foreach (CodeInstruction code in codes) if (code.Calls(native)) code.operand = AccessTools.Method(typeof(InfoCardPermits), nameof(InfoCardPermits.Draw));
            return codes;
        }
    }
    [HarmonyPatch(typeof(PermitsCardUtility), "DoLeftRect")]
    internal static class InfoCardPermitDetailPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.LabelCacheHeight), new[] { typeof(Rect).MakeByRefType(), typeof(string), typeof(bool), typeof(bool) });
            MethodInfo height = AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.height));
            var anchors = new List<int>();
            for (int i = 1; i + 2 < codes.Count; i++)
                if (codes[i].Calls(height) && codes[i - 1].opcode == OpCodes.Ldloca_S && LocalIndex(codes[i - 1].operand) == 2 &&
                    codes[i + 1].opcode == OpCodes.Ldc_R4 && Equals(codes[i + 1].operand, 50f) && codes[i + 2].opcode == OpCodes.Sub) anchors.Add(i);
            InfoCardPermits.DetailReady = codes.Count(code => code.Calls(label)) == 3 && anchors.Count == 1;
            if (!InfoCardPermits.DetailReady) { Log.Warning("[Reframed UI] InfoCard permit detail seam changed; retaining native cards."); return codes; }
            foreach (CodeInstruction code in codes) if (code.Calls(label)) code.operand = AccessTools.Method(typeof(InfoCardPermits), nameof(InfoCardPermits.Label));
            codes[anchors[0]].operand = AccessTools.Method(typeof(InfoCardPermits), nameof(InfoCardPermits.AcceptHeight));
            return codes;
        }
        private static int LocalIndex(object operand) => operand is LocalBuilder local ? local.LocalIndex : Convert.ToInt32(operand);
    }
}

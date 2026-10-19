using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnInspectorExtrasHealth
    {
        internal static readonly MethodInfo DebugOptions = AccessTools.Method(typeof(HealthCardUtility), "DoDebugOptions");
        private static readonly FieldInfo MenuOptions = AccessTools.Field(typeof(FloatMenu), "options");
        private static bool opening;
        internal static bool MenuReady;
        internal static List<Hediff> Conditions(Pawn pawn) => PawnHealthNative.ConditionGroups(pawn).SelectMany(group => group).ToList();
        private static bool Dev(Pawn pawn, InspectTabBase tab) => PawnInspectorExtras.Valid(pawn, tab) &&
            PawnInspectorExtrasPolicy.HealthDeveloper(Prefs.DevMode, Current.ProgramState == ProgramState.Playing);
        private static bool ConditionDev(Pawn pawn, InspectTabBase tab, Hediff hediff) => PawnInspectorExtras.Valid(pawn, tab) &&
            PawnInspectorExtrasPolicy.ConditionDeveloper(DebugSettings.ShowDevGizmos, Current.ProgramState == ProgramState.Playing) &&
            pawn.health.hediffSet.hediffs.Contains(hediff);
        internal static void ShowNativeMenu(Pawn pawn, InspectTabBase tab)
        {
            if (!MenuReady || !Dev(pawn, tab)) return;
            using (PawnInspectorPopup.Enter())
            using (new Theme.GuiScope(true))
            {
                var before = new HashSet<Window>(Find.WindowStack.Windows);
                bool previous = opening; opening = true;
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                try { DebugOptions.Invoke(null, new object[] { new Rect(0, 0, 400, 40), pawn }); }
                finally { opening = previous; }
                // Keep exact native menu contents/callbacks, with a stale-subject
                // guard added at invocation. No generated closure reflection.
                foreach (FloatMenu menu in Find.WindowStack.Windows.OfType<FloatMenu>().Where(window => !before.Contains(window)))
                    foreach (FloatMenuOption option in (List<FloatMenuOption>)MenuOptions.GetValue(menu))
                    {
                        Action native = option.action;
                        if (native != null) option.action = () =>
                        { if (Dev(pawn, tab)) using (PawnInspectorPopup.Enter()) native(); };
                    }
            }
        }
        internal static bool OpenButton(Rect rect, string text, bool background, bool sound, bool active, TextAnchor? anchor) =>
            opening || Widgets.ButtonText(rect, text, background, sound, active, anchor);
        internal static void ShowConditions(Pawn pawn, InspectTabBase tab)
        {
            if (!PawnInspectorExtras.Valid(pawn, tab) || !PawnHealthPanel.Ready ||
                !PawnInspectorExtrasPolicy.ConditionDeveloper(DebugSettings.ShowDevGizmos, Current.ProgramState == ProgramState.Playing)) return;
            using (PawnInspectorPopup.Enter())
            {
                var options = new List<FloatMenuOption>();
                foreach (Hediff hediff in Conditions(pawn))
                {
                    Hediff captured = hediff;
                    string label = (hediff.Part?.LabelCap ?? "WholeBody".Translate().ToString()) + ": " + hediff.LabelCap;
                    options.Add(new FloatMenuOption(label, () =>
                    {
                        if (!PawnInspectorExtras.Valid(pawn, tab) || !pawn.health.hediffSet.hediffs.Contains(captured)) return;
                        using (PawnInspectorPopup.Enter()) ShowConditionActions(pawn, tab, captured);
                    }) { tooltip = new TipSignal(() => captured.GetTooltip(pawn, false), 18212 + captured.loadID) });
                }
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        private static void ShowConditionActions(Pawn pawn, InspectTabBase tab, Hediff hediff)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("ThingInfo".Translate(), () =>
                {
                    if (PawnInspectorExtras.Valid(pawn, tab) && pawn.health.hediffSet.hediffs.Contains(hediff))
                        using (PawnInspectorPopup.Enter()) Find.WindowStack.Add(new Dialog_InfoCard(hediff));
                })
            };
            void Add(string label, Action action) => options.Add(new FloatMenuOption(label, () =>
            { if (ConditionDev(pawn, tab, hediff)) using (PawnInspectorPopup.Enter()) action(); }));
            if (ConditionDev(pawn, tab, hediff))
            {
                // These are the exact native DrawHediffRow developer actions.
                Add("DEV: Remove condition", () => pawn.health.RemoveHediff(hediff));
                if (PawnInspectorExtrasPolicy.SeverityAvailable(hediff.def.maxSeverity, hediff.def.lethalSeverity))
                    Add("DEV: Set severity…", () => Find.WindowStack.Add(new Dialog_DebugSetSeverity(hediff)));
                if (hediff.TryGetComp<HediffComp_Disappears>() != null)
                    Add("DEV: Set remaining time…", () => Find.WindowStack.Add(new Dialog_DebugSetHediffRemaining(hediff, null)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    [HarmonyPatch]
    internal static class PawnInspectorExtrasHealthMenuPatch
    {
        private static MethodBase TargetMethod() => PawnInspectorExtrasHealth.DebugOptions;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source = instructions.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo native = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText),
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            PawnInspectorExtrasHealth.MenuReady = source.Count(code => code.Calls(native)) == 1;
            if (!PawnInspectorExtrasHealth.MenuReady)
            {
                Log.Warning("[Reframed UI] Health developer-menu seam changed; native developer menu adapter unavailable.");
                return source;
            }
            foreach (CodeInstruction code in source)
                if (code.Calls(native)) code.operand = AccessTools.Method(typeof(PawnInspectorExtrasHealth), nameof(PawnInspectorExtrasHealth.OpenButton));
            return source;
        }
    }
}

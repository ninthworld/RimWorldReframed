using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Cogs expose focused settings/actions. They never navigate to a different
    // renderer for the ordinary tab body; unsupported mod tabs stay with their owner.
    internal static class PawnInspectorExtras
    {
        internal static bool CanShow(Pawn pawn, InspectTabBase tab) => pawn != null && !pawn.Destroyed &&
            (tab == null || tab.GetType() == typeof(ITab_Pawn_Health) || tab.GetType() == typeof(ITab_Pawn_Social) ||
             tab.GetType() == typeof(ITab_Pawn_Character) || tab.GetType() == typeof(ITab_Pawn_Log) ||
             tab.GetType() == typeof(ITab_Pawn_Needs) || tab.GetType() == typeof(ITab_Pawn_Gear));
        internal static bool Valid(Pawn pawn, InspectTabBase tab) => CanShow(pawn, tab) && SelectionInspector.Active &&
            (Find.Selector.SingleSelectedThing == pawn || Find.Selector.SingleSelectedThing is Corpse corpse && corpse.InnerPawn == pawn) &&
            (tab == null || tab.IsVisible);
        internal static void Show(Pawn pawn, InspectTabBase tab)
        {
            if (!Valid(pawn, tab)) return;
            using (PawnInspectorPopup.Enter())
            {
                var options = new List<FloatMenuOption>
                {
                    Option("ThingInfo".Translate(), pawn, tab, () => Find.WindowStack.Add(new Dialog_InfoCard(pawn)))
                };
                if (tab?.GetType() == typeof(ITab_Pawn_Health)) AddHealth(options, pawn, tab);
                else if (tab?.GetType() == typeof(ITab_Pawn_Social)) AddSocial(options, pawn, tab);
                else if (tab?.GetType() == typeof(ITab_Pawn_Character) && PawnBioActionsNative.Ready && PawnBioActionsNaming.Ready)
                    options.Add(Option("Character actions…", pawn, tab, () => Find.WindowStack.Add(new PawnBioActionsWindow(pawn))));
                else if (tab is ITab_Pawn_Log log) AddLog(options, pawn, log);
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        private static FloatMenuOption Option(string label, Pawn pawn, InspectTabBase tab, Action action) =>
            new FloatMenuOption(label, () =>
            {
                if (!Valid(pawn, tab)) return;
                using (PawnInspectorPopup.Enter()) action();
            });
        private static void AddHealth(List<FloatMenuOption> options, Pawn pawn, InspectTabBase tab)
        {
            if (PawnHealthPanel.Ready && PawnInspectorExtrasPolicy.ConditionDeveloper(DebugSettings.ShowDevGizmos,
                Current.ProgramState == ProgramState.Playing) && PawnInspectorExtrasHealth.Conditions(pawn).Count > 0)
                options.Add(Option("Developer condition tools…", pawn, tab, () => PawnInspectorExtrasHealth.ShowConditions(pawn, tab)));
            if (PawnInspectorExtrasPolicy.HealthDeveloper(Prefs.DevMode, Current.ProgramState == ProgramState.Playing))
            {
                if (PawnInspectorExtrasHealth.MenuReady)
                    options.Add(Option("Developer tools…", pawn, tab, () => PawnInspectorExtrasHealth.ShowNativeMenu(pawn, tab)));
            }
        }
        private static void AddSocial(List<FloatMenuOption> options, Pawn pawn, InspectTabBase tab)
        {
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                options.Add(Option("StatsReport_Ideoligion".Translate() + ": " + pawn.Ideo.name, pawn, tab,
                    () => { if (pawn.Ideo != null) IdeoUIUtility.OpenIdeoInfo(pawn.Ideo); }));
            if (PawnInspectorExtrasCertainty.Available(pawn))
                options.Add(Option("Certainty details…", pawn, tab, () => Find.WindowStack.Add(new PawnInspectorExtrasCertaintyWindow(pawn, tab))));
            if (PawnInspectorExtrasSocial.Ready && PawnInspectorExtrasSocial.PregnancyAvailable(pawn))
                options.Add(Option("PregnancyApproach".Translate() + "…", pawn, tab,
                    () => Find.WindowStack.Add(new PawnInspectorExtrasPartnersWindow(pawn, tab))));
            if (Prefs.DevMode && !pawn.Dead && PawnInspectorExtrasSocial.Ready)
                options.Add(FloatMenuOption.CheckboxLabeled("DEV: All relations", () =>
                {
                    if (!Valid(pawn, tab) || !Prefs.DevMode || pawn.Dead) return;
                    PawnInspectorExtrasSocial.ToggleAllRelations();
                }, PawnInspectorExtrasSocial.AllRelations));
        }
        private static void AddLog(List<FloatMenuOption> options, Pawn pawn, ITab_Pawn_Log tab)
        {
            void Toggle(string key, Func<bool> read, Action<bool> write)
            {
                options.Add(FloatMenuOption.CheckboxLabeled(key.Translate(), () =>
                {
                    if (!Valid(pawn, tab)) return;
                    write(!read()); PawnLogPanel.Reset();
                }, read()));
            }
            Toggle("ShowAll", () => tab.showAll, value => tab.showAll = value);
            Toggle("ShowCombat", () => tab.showCombat, value => tab.showCombat = value);
            Toggle("ShowSocial", () => tab.showSocial, value => tab.showSocial = value);
            options.Add(Option("CopyLogTip".Translate(), pawn, tab, () =>
            {
                var output = new StringBuilder();
                foreach (var line in ITab_Pawn_Log_Utility.GenerateLogLinesFor(pawn, tab.showAll, tab.showCombat, tab.showSocial, PawnLogLayout.EntryLimit))
                    line.AppendTo(output);
                GUIUtility.systemCopyBuffer = output.ToString();
            }));
        }
    }
}

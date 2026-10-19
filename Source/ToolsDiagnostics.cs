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
    // Read-only evidence through the existing CLI + DevMode gated fixture.
    // No UI actions, native preference mutations or save writes.
    internal static class ToolsDiagnostics
    {
        private static readonly FieldInfo[] Fields = typeof(PlaySettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(field => field.FieldType == typeof(bool)).ToArray();

        internal static object State()
        {
            if (!PawnBarDiagnostics.Enabled) return null;
            var settings = new Dictionary<string, object>();
            foreach (FieldInfo field in Fields) settings[field.Name] = field.GetValue(Find.PlaySettings);
            var method = AccessTools.DeclaredMethod(typeof(ToolsMenu), "AnimationAuditState", Type.EmptyTypes);
            return PawnBarDiagnostics.D("animation", method?.Invoke(null, null), "native_play_settings", settings,
                "categorized_resources", Prefs.ResourceReadoutCategorized,
                "selected_things", Find.Selector.SelectedObjects.OfType<Thing>().Select(thing => thing.ThingID).ToArray(),
                "absorbing_window", Find.WindowStack.AnyWindowAbsorbingAllInput,
                "windows", Find.WindowStack.Windows.Select(window => window.GetType().FullName).ToArray());
        }
    }
}

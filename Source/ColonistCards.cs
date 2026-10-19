using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace ReframedUI
{
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    internal static class ColonistCards
    {
        internal static bool Active => ReframedMod.Active && ReframedMod.Settings.ColonistCards && !TutorSystem.TutorialMode;
        // Native fallback stays intact. Developer evidence records the same real
        // pawn identities in native and reconstructed modes.
        private static bool Prefix(Pawn colonist, Rect rect)
        { PawnBarDiagnostics.RecordDraw(colonist, rect); return true; }
    }
    // Grouped cards replace only native bracket painting with an inset rounded
    // selection border. Native selection decisions and the fallback/world bar stay intact.
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawSelectionOverlayOnGUI")]
    internal static class ColonistCardSelectionOverlay
    {
        private static bool Prefix(Pawn colonist, Rect rect)
        {
            if (!ColonistGroupsHud.Drawing) return true;
            ColonistGroupsHud.QueueSelectionOverlay(colonist, rect);
            return false;
        }
    }
    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
    internal static class ColonistRail
    {
        private static bool Prefix(ColonistBar __instance, List<Pawn> ___colonistsToHighlight, ref float ___cachedScale)
        {
            PawnBarDiagnostics.Update();
            return !ColonistGroupsHud.Draw(__instance, ___colonistsToHighlight, ref ___cachedScale);
        }
    }
}

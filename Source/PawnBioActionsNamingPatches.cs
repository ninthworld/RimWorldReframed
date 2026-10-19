using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
namespace ReframedUI
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class PawnBioActionsNameOwnerPatch { private static void Prefix(Window __0) => PawnBioActionsNaming.Register(__0); }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class PawnBioActionsNamePreparePatch { private static void Prefix() => PawnBioActionsNaming.Prepare(); }
    [HarmonyPatch(typeof(Dialog_NamePawn), "get_InitialSize")]
    internal static class PawnBioActionsNameSizePatch
    { private static void Postfix(Dialog_NamePawn __instance, ref Vector2 __result) { if (PawnBioActionsNaming.Owned(__instance)) __result = PawnBioActionsNaming.Dimensions(__instance); } }
    [HarmonyPatch(typeof(Window), "get_Margin")]
    internal static class PawnBioActionsNameMarginPatch
    { private static void Postfix(Window __instance, ref float __result) { if (__instance is Dialog_NamePawn dialog && PawnBioActionsNaming.Owned(dialog)) __result = 0; } }
    [HarmonyPatch(typeof(Dialog_NamePawn), nameof(Dialog_NamePawn.DoWindowContents))]
    internal static class PawnBioActionsNameContentsPatch
    {
        private static bool Prefix(Dialog_NamePawn __instance, Rect __0) => PawnBioActionsNaming.Draw(__instance, __0);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(code => new CodeInstruction(code)).ToList();
            var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            PawnBioActionsNaming.ButtonsReady = codes.Count(code => code.Calls(button)) == 2;
            if (!PawnBioActionsNaming.ButtonsReady) { Log.Warning("[Reframed UI] Naming acceptance seam changed; native naming retained."); return codes; }
            foreach (var code in codes) if (code.Calls(button)) code.operand = AccessTools.Method(typeof(PawnBioActionsNaming), nameof(PawnBioActionsNaming.Footer));
            return codes;
        }
    }
    [HarmonyPatch]
    internal static class PawnBioActionsNameRowsPatch
    {
        private static MethodBase TargetMethod() => PawnBioActionsNaming.MakeRow;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(code => new CodeInstruction(code)).ToList();
            var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            var label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            var tagged = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(TaggedString) });
            PawnBioActionsNaming.RowsReady = codes.Count(code => code.Calls(button)) == 2 && codes.Count(code => code.Calls(label)) == 1 && codes.Count(code => code.Calls(tagged)) == 1;
            if (!PawnBioActionsNaming.RowsReady) { Log.Warning("[Reframed UI] Naming row seam changed; native naming retained."); return codes; }
            foreach (var code in codes)
                if (code.Calls(button)) code.operand = AccessTools.Method(typeof(PawnBioActionsNaming), nameof(PawnBioActionsNaming.RowButton));
                else if (code.Calls(label)) code.operand = AccessTools.Method(typeof(PawnBioActionsNaming), nameof(PawnBioActionsNaming.RowLabel));
                else if (code.Calls(tagged)) code.operand = AccessTools.Method(typeof(PawnBioActionsNaming), nameof(PawnBioActionsNaming.TaggedLabel));
            return codes;
        }
    }
    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DoNameInputRect))]
    internal static class PawnBioActionsNameInputPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(code => new CodeInstruction(code)).ToList();
            var field = AccessTools.Method(typeof(Widgets), nameof(Widgets.TextField), new[] { typeof(Rect), typeof(string) });
            PawnBioActionsNaming.InputReady = codes.Count(code => code.Calls(field)) == 1;
            if (!PawnBioActionsNaming.InputReady) { Log.Warning("[Reframed UI] Naming input seam changed; native naming retained."); return codes; }
            foreach (var code in codes) if (code.Calls(field)) code.operand = AccessTools.Method(typeof(PawnBioActionsNaming), nameof(PawnBioActionsNaming.TextField));
            return codes;
        }
    }
}

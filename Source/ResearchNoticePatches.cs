using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    internal static class ResearchNoticeCreationPatch
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var original = source.ToList();
            var ctor = AccessTools.Constructor(typeof(Dialog_NodeTree), new[] { typeof(DiaNode), typeof(bool), typeof(bool), typeof(string) });
            var add = AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add), new[] { typeof(Window) });
            int[] matches = Enumerable.Range(0, Math.Max(0, original.Count - 1))
                .Where(i => original[i].opcode == OpCodes.Newobj && Equals(original[i].operand, ctor) && original[i + 1].Calls(add)).ToArray();
            ResearchNotice.SeamReady = matches.Length == 1 && original[matches[0]].blocks.Count == 0 &&
                original.Count(code => code.opcode == OpCodes.Newobj && Equals(code.operand, ctor)) == 1;
            if (!ResearchNotice.SeamReady) return original;
            var helper = AccessTools.Method(typeof(ResearchNotice), nameof(ResearchNotice.Create));
            var result = new List<CodeInstruction>(original.Count + 1);
            for (int i = 0; i < original.Count; i++)
            {
                var copy = new CodeInstruction(original[i]);
                if (i == matches[0])
                {
                    // Incoming branches and exception boundaries still enter at
                    // the first instruction which consumes the original stack.
                    var project = new CodeInstruction(OpCodes.Ldarg_1);
                    project.labels.AddRange(copy.labels); copy.labels.Clear();
                    project.blocks.AddRange(copy.blocks); copy.blocks.Clear();
                    result.Add(project); copy.opcode = OpCodes.Call; copy.operand = helper;
                }
                result.Add(copy);
            }
            return result;
        }
    }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class ResearchNoticePreparePatch
    {
        private static void Prefix(WindowStack __instance)
        {
            ResearchNotice.Tick();
            foreach (Window window in __instance.Windows) if (window is Dialog_NodeTree tree) ResearchNotice.Prepare(tree);
        }
        private static void Postfix() => ResearchNotice.DrawClosing();
    }
    [HarmonyPatch(typeof(Dialog_NodeTree), "get_InitialSize")]
    internal static class ResearchNoticeSizePatch
    {
        private static void Postfix(Dialog_NodeTree __instance, ref Vector2 __result)
        { if (ResearchNotice.Eligible(__instance)) __result = ResearchNotice.Size; }
    }
    [HarmonyPatch(typeof(Window), "get_Margin")]
    internal static class ResearchNoticeMarginPatch
    {
        private static void Postfix(Window __instance, ref float __result)
        { if (ResearchNotice.Eligible(__instance)) __result = 0; }
    }
    [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.DoWindowContents))]
    internal static class ResearchNoticeContentsPatch
    {
        private static bool Prefix(Dialog_NodeTree __instance, Rect __0)
        { if (!ResearchNotice.Eligible(__instance)) return true; ResearchNotice.Draw(__instance, __0); return false; }
    }
    [HarmonyPatch(typeof(Dialog_NodeTree), nameof(Dialog_NodeTree.PostClose))]
    internal static class ResearchNoticeClosePatch
    {
        private static void Prefix(Dialog_NodeTree __instance) => ResearchNotice.Closing(__instance);
        private static void Postfix(Dialog_NodeTree __instance) => ResearchNotice.Release(__instance);
    }
}

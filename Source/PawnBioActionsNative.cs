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
    internal static class PawnBioActionsNative
    {
        internal enum Kind { Banish, Rename, Renounce, Execute }
        private static List<Kind> captured;
        private static Kind? requested;
        internal static bool Ready;
        internal static bool Active => captured != null;
        internal static List<Kind> Actions(Pawn pawn) => Run(pawn, null);
        internal static void Invoke(Pawn pawn, Kind action) => Run(pawn, action);
        private static List<Kind> Run(Pawn pawn, Kind? action)
        {
            var found = new List<Kind>();
            if (!Ready || pawn == null || pawn.Destroyed) return found;
            var previous = captured; var previousRequest = requested; captured = found; requested = action;
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false;
                GUI.BeginGroup(new Rect(-10000, -10000, 1, 1));
                try { CharacterCardUtility.DrawCharacterCard(new Rect(0, 0, 560, 32), pawn, null, default(Rect), true); }
                finally { GUI.EndGroup(); captured = previous; requested = previousRequest; }
            }
            return found;
        }
        internal static bool Button(Rect rect, Texture2D texture, bool sound, string tip)
        {
            if (!Active) return Widgets.ButtonImage(rect, texture, sound, tip);
            Kind? kind = texture == TexButton.Banish ? Kind.Banish : texture == TexButton.Rename ? Kind.Rename :
                texture == TexButton.RenounceTitle ? Kind.Renounce : texture == TexButton.ExecuteColonist ? Kind.Execute : (Kind?)null;
            if (!kind.HasValue) return false;
            captured.Add(kind.Value); bool result = requested == kind;
            if (result) { requested = null; Event.current.Use(); }
            return result;
        }
        internal static bool StopBeforeBody() => Active;
        internal static string Label(Kind kind, Pawn pawn) => kind == Kind.Banish ? "Banish".Translate().ToString() :
            kind == Kind.Rename ? "RenameColonist".Translate().ToString() : kind == Kind.Renounce ? "RenounceTitle".Translate().ToString() :
            pawn.guilt?.awaitingExecution == true ? "Cancel execution" : "ExecuteColonist".Translate().ToString();
        internal static string Tooltip(Kind kind, Pawn pawn) => kind == Kind.Banish ? PawnBanishUtility.GetBanishButtonTip(pawn) : Label(kind, pawn);
    }
    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard))]
    internal static class PawnBioActionsHeaderPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.Select(code => new CodeInstruction(code)).ToList();
            var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonImage), new[] { typeof(Rect), typeof(Texture2D), typeof(bool), typeof(string) });
            int top = codes.FindIndex(code => code.operand is MethodInfo method && method.DeclaringType == typeof(CharacterCardUtility) && method.Name == "DoTopStack");
            int boundary = top - 12;
            PawnBioActionsNative.Ready = codes.Count(code => code.Calls(button)) == 4 && boundary >= 0 &&
                codes[boundary].opcode == OpCodes.Ldloca_S && codes[boundary + 1].Calls(AccessTools.PropertyGetter(typeof(Rect), "height")) &&
                codes[boundary + 2].opcode == OpCodes.Ldc_R4 && Equals(codes[boundary + 2].operand, 10f) && codes[boundary + 3].opcode == OpCodes.Add;
            if (!PawnBioActionsNative.Ready) { Log.Warning("[Reframed UI] Bio action header seam changed; compact actions unavailable."); return codes; }
            foreach (var code in codes) if (code.Calls(button)) code.operand = AccessTools.Method(typeof(PawnBioActionsNative), nameof(PawnBioActionsNative.Button));
            Label resume = generator.DefineLabel();
            var check = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PawnBioActionsNative), nameof(PawnBioActionsNative.StopBeforeBody)));
            check.labels.AddRange(codes[boundary].labels); codes[boundary].labels.Clear(); codes[boundary].labels.Add(resume);
            codes.InsertRange(boundary, new[] { check, new CodeInstruction(OpCodes.Brfalse, resume),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Widgets), nameof(Widgets.EndGroup))), new CodeInstruction(OpCodes.Ret) });
            return codes;
        }
    }
}

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
    internal static class PawnInspectorExtrasCertainty
    {
        internal static readonly MethodInfo Native = AccessTools.Method(typeof(SocialCardUtility), "DrawPawnCertainty");
        private static bool drawing;
        internal static bool Ready;
        internal static bool Available(Pawn pawn) => Ready && ModsConfig.IdeologyActive && !pawn.Dead && pawn.Ideo != null && !pawn.Ideo.classicMode;
        internal static void Draw(Rect row, Pawn pawn)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                bool previous = drawing; drawing = true;
                try { Native.Invoke(null, new object[] { pawn, row }); }
                finally { drawing = previous; }
                // Match the native interactive region (x+17 through width-26),
                // preserving its complete tooltip and native Ideo link.
                Rect content = new Rect(row.x + 17, row.y, Math.Max(1, row.width - 43), row.height);
                float name = content.width * .5f;
                GUI.color = pawn.Ideo.Color;
                GUI.DrawTexture(new Rect(content.x, content.center.y - 7, 14, 14), InspectorIcons.Get("ideology"));
                GUI.color = Color.white;
                PawnInspectorView.Label(new Rect(content.x + 20, row.y, Math.Max(1, name - 25), row.height), pawn.Ideo.name, Theme.Ink);
                Rect bar = new Rect(content.x + name, content.center.y - 5, Math.Max(1, content.width - name - 39), 10);
                PawnInspectorView.Bar(bar, pawn.ideo.Certainty, Theme.Good);
                PawnInspectorView.SmallLabel(new Rect(bar.xMax + 3, row.y, 36, row.height), pawn.ideo.Certainty.ToStringPercent("F0"), Theme.Muted, true);
            }
        }
        internal static void Icon(Ideo ideo, Rect rect) { if (!drawing) ideo.DrawIcon(rect); }
        internal static void Label(Rect rect, string text) { if (!drawing) Widgets.Label(rect, text); }
        internal static void Highlight(Rect rect) { if (!drawing) Widgets.DrawHighlight(rect); }
        internal static Rect Fill(Rect rect, float fill, Texture2D texture) => drawing ? rect : Widgets.FillableBar(rect, fill, texture);
    }
    internal sealed class PawnInspectorExtrasCertaintyWindow : Window
    {
        private readonly Pawn pawn;
        private readonly InspectTabBase tab;
        internal PawnInspectorExtrasCertaintyWindow(Pawn pawn, InspectTabBase tab)
        {
            this.pawn = pawn; this.tab = tab;
            doWindowBackground = drawShadow = false; doCloseX = true; closeOnClickedOutside = true; absorbInputAroundWindow = true;
        }
        public override Vector2 InitialSize => new Vector2(460, 143);
        public override void DoWindowContents(Rect inRect)
        {
            if (!PawnInspectorExtras.Valid(pawn, tab) || !PawnInspectorExtrasCertainty.Available(pawn)) { Close(); return; }
            using (PawnInspectorPopup.Enter())
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Theme.RoundRect(inRect, PawnInspectorView.BodyFill, 5);
                PawnInspectorView.Heading(new Rect(10, 7, inRect.width - 42, 18), "CERTAINTY");
                PawnInspectorExtrasCertainty.Draw(new Rect(0, 29, inRect.width, 32), pawn);
                PawnInspectorView.SmallLabel(new Rect(17, 64, inRect.width - 34, 24), "Hover for the full breakdown; click to view the ideoligion.", Theme.Muted);
            }
        }
    }
    [HarmonyPatch]
    internal static class PawnInspectorExtrasCertaintyPatch
    {
        private static MethodBase TargetMethod() => PawnInspectorExtrasCertainty.Native;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source = instructions.Select(code => new CodeInstruction(code)).ToList();
            var substitutions = new Dictionary<MethodInfo, MethodInfo>
            {
                [AccessTools.Method(typeof(Ideo), nameof(Ideo.DrawIcon), new[] { typeof(Rect) })] = AccessTools.Method(typeof(PawnInspectorExtrasCertainty), nameof(PawnInspectorExtrasCertainty.Icon)),
                [AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })] = AccessTools.Method(typeof(PawnInspectorExtrasCertainty), nameof(PawnInspectorExtrasCertainty.Label)),
                [AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawHighlight), new[] { typeof(Rect) })] = AccessTools.Method(typeof(PawnInspectorExtrasCertainty), nameof(PawnInspectorExtrasCertainty.Highlight)),
                [AccessTools.Method(typeof(Widgets), nameof(Widgets.FillableBar), new[] { typeof(Rect), typeof(float), typeof(Texture2D) })] = AccessTools.Method(typeof(PawnInspectorExtrasCertainty), nameof(PawnInspectorExtrasCertainty.Fill))
            };
            PawnInspectorExtrasCertainty.Ready = substitutions.Keys.All(method => source.Count(code => code.Calls(method)) == 1);
            if (!PawnInspectorExtrasCertainty.Ready)
            {
                Log.Warning("[Reframed UI] Social certainty paint seams changed; compact certainty adapter unavailable.");
                return source;
            }
            foreach (CodeInstruction code in source)
                foreach (var pair in substitutions)
                    if (code.Calls(pair.Key)) { code.opcode = OpCodes.Call; code.operand = pair.Value; break; }
            return source;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // GUI.Window can defer its delegate until after WindowOnGUI returns.
    // Scope the actual callback so background, contents and footer share it.
    [HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
    internal static class InfoCardWindowPatch
    {
        internal sealed class State : IDisposable
        {
            private readonly Dialog_InfoCard previous;
            private readonly Theme.GuiScope? gui;
            private bool disposed;
            internal State(Window window)
            {
                previous = InfoCardPanel.CurrentCard;
                bool eligible = InfoCardPanel.Eligible(window);
                gui = eligible || previous != null ? new Theme.GuiScope(true) : (Theme.GuiScope?)null;
                InfoCardPanel.CurrentCard = eligible ? (Dialog_InfoCard)window : null;
            }
            public void Dispose()
            {
                if (disposed) return;
                InfoCardPanel.CurrentCard = previous; gui?.Dispose(); disposed = true;
            }
        }
        private static void Prefix(Window __instance, out State __state) => __state = new State(__instance);
        private static Exception Finalizer(Exception __exception, State __state) { __state?.Dispose(); return __exception; }
    }

    // WindowStack paints all shadows before calling any WindowOnGUI. Prepare at
    // the owner boundary, including restoration after a live setting change.
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class InfoCardPreparePatch
    {
        private static void Prefix(WindowStack __instance)
        {
            foreach (Window window in __instance.Windows) if (window is Dialog_InfoCard card) InfoCardPanel.Prepare(card);
        }
    }

    [HarmonyPatch(typeof(Dialog_InfoCard), "get_InitialSize")]
    internal static class InfoCardSizePatch
    {
        private static void Postfix(Dialog_InfoCard __instance, ref Vector2 __result)
        {
            if (InfoCardPanel.Eligible(__instance)) __result = new Vector2(InfoCardLayout.WindowWidth(UI.screenWidth), InfoCardLayout.WindowHeight(UI.screenHeight));
        }
    }

    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    internal static class InfoCardContentsPatch
    {
        private static void Prefix(Dialog_InfoCard __instance) { if (InfoCardPanel.CurrentCard == __instance) InfoCardPanel.TabCount = 1; }
        private static void Postfix(Dialog_InfoCard __instance) { if (InfoCardPanel.CurrentCard == __instance) InfoCardPanel.DrawPortrait(); }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            MethodInfo thingIcon = AccessTools.Method(typeof(Widgets), nameof(Widgets.ThingIcon), new[] {
                typeof(Rect), typeof(Thing), typeof(float), typeof(Rot4?), typeof(bool), typeof(float), typeof(bool) });
            var tabs = codes.Where(code => code.operand is MethodInfo method && method.DeclaringType == typeof(TabDrawer) && method.Name == "DrawTabs" &&
                method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(TabRecord) }) && method.GetParameters().Length == 3).ToList();
            ConstructorInfo rectangle = AccessTools.Constructor(typeof(Rect), new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            MethodInfo getX = AccessTools.PropertyGetter(typeof(Rect), "x"), getY = AccessTools.PropertyGetter(typeof(Rect), "y");
            var icon = new List<CodeInstruction>();
            for (int i = 10; i < codes.Count; i++)
                if (codes[i].opcode == OpCodes.Call && Equals(codes[i].operand, rectangle) &&
                    codes[i - 1].opcode == OpCodes.Ldc_R4 && Equals(codes[i - 1].operand, 34f) &&
                    codes[i - 2].opcode == OpCodes.Ldc_R4 && Equals(codes[i - 2].operand, 34f) &&
                    codes[i - 3].Calls(getY) && codes[i - 4].opcode == OpCodes.Ldloca_S &&
                    codes[i - 5].opcode == OpCodes.Add && codes[i - 6].opcode == OpCodes.Ldc_R4 && Equals(codes[i - 6].operand, 9f) &&
                    codes[i - 7].Calls(getX) && codes[i - 8].opcode == OpCodes.Ldarga_S && codes[i - 9].opcode == OpCodes.Ldloca_S)
                    icon.Add(codes[i]);
            InfoCardPanel.ContentsReady = codes.Count(code => code.Calls(label)) == 1 && tabs.Count == 1 && icon.Count == 1 && codes.Count(code => code.Calls(thingIcon)) == 1;
            if (!InfoCardPanel.ContentsReady) { Log.Warning("[Reframed UI] InfoCard header seam changed; retaining native information cards."); return codes; }
            foreach (CodeInstruction code in codes)
            {
                if (code.Calls(label)) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawTitle));
                else if (ReferenceEquals(code, tabs[0])) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawTabs));
                else if (ReferenceEquals(code, icon[0])) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.CompactIcon));
                else if (code.Calls(thingIcon)) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawSubjectIcon));
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(Dialog_InfoCard), "FillCard")]
    internal static class InfoCardFillPatch
    {
        private static void Prefix(Dialog_InfoCard __instance, ref Rect __0)
        {
            if (InfoCardPanel.CurrentCard != __instance) return;
            if (InfoCardPanel.TabCount == 1) InfoCardPanel.SingleTab();
            float top = InfoCardLayout.Header + InfoCardLayout.Tabs + 10;
            __0 = new Rect(InfoCardLayout.Padding, top, Math.Max(1, __instance.windowRect.width - 2 * InfoCardLayout.Padding),
                Math.Max(1, __instance.windowRect.height - top - InfoCardLayout.Padding));
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            var native = AccessTools.Method(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard),
                new[] { typeof(Rect), typeof(Pawn), typeof(Action), typeof(Rect), typeof(bool) });
            var health = AccessTools.Method(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawPawnHealthCard),
                new[] { typeof(Rect), typeof(Pawn), typeof(bool), typeof(bool), typeof(Thing) });
            InfoCardPanel.BioReady = codes.Count(code => code.Calls(native)) == 1 && codes.Count(code => code.Calls(health)) == 1;
            if (!InfoCardPanel.BioReady) { Log.Warning("[Reframed UI] InfoCard biography seam changed; retaining native information cards."); return codes; }
            foreach (CodeInstruction code in codes)
            {
                if (code.Calls(native)) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawBio));
                else if (code.Calls(health)) code.operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawHealth));
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawHealthSummary))]
    internal static class InfoCardHealthSummaryPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var codes = source.Select(code => new CodeInstruction(code)).ToList();
            var tabs = codes.Where(code => code.operand is MethodInfo method && method.DeclaringType == typeof(TabDrawer) && method.Name == "DrawTabs" &&
                method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(TabRecord) }) &&
                method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(Rect), typeof(List<TabRecord>), typeof(float) })).ToList();
            InfoCardPanel.HealthReady = tabs.Count == 1;
            if (!InfoCardPanel.HealthReady) { Log.Warning("[Reframed UI] InfoCard health navigation seam changed; retaining native cards."); return codes; }
            tabs[0].operand = AccessTools.Method(typeof(InfoCardPanel), nameof(InfoCardPanel.DrawHealthTabs));
            return codes;
        }
    }

    [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsWorker")]
    internal static class InfoCardStatsPatch
    {
        private static bool Prefix(Rect __0, Thing __1, WorldObject __2)
        {
            if (!InfoCardPanel.Drawing || !InfoCardStats.Ready) return true;
            InfoCardStats.Draw(__0, __1, __2); return false;
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })]
    internal static class InfoCardLabelPatch
    {
        private static bool Prefix(Rect __0, string __1)
        { if (!InfoCardPanel.Drawing) return true; InfoCardPanel.Label(__0, __1); return false; }
    }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcHeight), new[] { typeof(string), typeof(float) })]
    internal static class InfoCardTextHeightPatch
    {
        private static bool Prefix(string __0, float __1, ref float __result)
        { if (!InfoCardPanel.Drawing) return true; __result = InfoCardStats.MeasuredEntryHeight(__0, __1); return false; }
    }
    [HarmonyPatch(typeof(Text), nameof(Text.CalcSize), new[] { typeof(string) })]
    internal static class InfoCardTextSizePatch
    {
        private static bool Prefix(string __0, ref Vector2 __result)
        { if (!InfoCardPanel.Drawing) return true; __result = InfoCardPanel.Style(false).CalcSize(new GUIContent(__0 ?? "")); return false; }
    }
    [HarmonyPatch(typeof(Text), "get_LineHeight")]
    internal static class InfoCardLineHeightPatch
    {
        private static bool Prefix(ref float __result)
        { if (!InfoCardPanel.Drawing) return true; __result = InfoCardPanel.Style(false).CalcHeight(new GUIContent("Ag"), 1000); return false; }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawButtonGraphic))]
    internal static class InfoCardButtonFacePatch
    {
        private static bool Prefix(Rect __0)
        {
            if (!InfoCardPanel.Drawing) return true;
            Theme.ButtonFace(__0, GUI.enabled && Mouse.IsOver(__0) ? Theme.Hover : Theme.Surface, !GUI.enabled); return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawMenuSection))]
    internal static class InfoCardSectionPatch
    {
        private static bool Prefix(Rect __0)
        {
            if (!InfoCardPanel.Drawing) return true;
            // Content sits on the shared body, without the old nested frame.
            return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.ListSeparator), new[] { typeof(float), typeof(float), typeof(string) }, new[] { ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
    internal static class InfoCardSeparatorPatch
    {
        private static bool Prefix(ref float __0, float __1, string __2)
        {
            if (!InfoCardPanel.Drawing) return true;
            PawnInspectorView.Heading(new Rect(0, __0 + 3, __1, 18), (__2 ?? "").ToUpperInvariant());
            PawnInspectorWidgets.Divider(new Rect(0, __0 + 23, __1, 1)); __0 += 26; return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawHighlightSelected))]
    internal static class InfoCardSelectedPatch
    {
        private static bool Prefix(Rect __0)
        { if (!InfoCardPanel.Drawing) return true; Theme.RoundRect(__0, Theme.Selected.ToTransparent(.65f), 2); return false; }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.BeginScrollView))]
    internal static class InfoCardScrollPatch
    {
        private static void Prefix(ref Rect __0, ref Vector2 __1, Rect __2, ref bool __3)
        {
            if (!InfoCardPanel.Drawing || !__3) return;
            Rect outer = __0;
            InfoCardLayout.ScrollSpace(outer.width, outer.height, __2.width, __2.height, out float width, out float height, out bool vertical, out bool horizontal);
            __1.x = Mathf.Clamp(__1.x, 0, Math.Max(0, __2.width - width));
            __1.y = Mathf.Clamp(__1.y, 0, Math.Max(0, __2.height - height));
            if (vertical) PawnInspectorWidgets.Rail(new Rect(outer.xMax - 5, outer.y, 4, height), ref __1.y, __2.height, height, false);
            if (horizontal) PawnInspectorWidgets.Rail(new Rect(outer.x, outer.yMax - 5, width, 4), ref __1.x, __2.width, width, true);
            __0.width = width; __0.height = height; __3 = false;
            // Continue the ORIGINAL wrapper: mouseOverScrollViewStack, SteamDeck
            // touch handling and UnityGUIBugsFixer bookkeeping remain intact.
        }
    }
    [HarmonyPatch(typeof(QuickSearchWidget), nameof(QuickSearchWidget.OnGUI))]
    internal static class InfoCardSearchPatch
    {
        private static bool Prefix(QuickSearchWidget __instance, Rect __0, Action __1, Action __2)
        {
            if (!InfoCardPanel.Drawing || InfoCardPanel.CurrentCard.CommonSearchWidget != __instance) return true;
            InfoCardPanel.DrawSearch(__instance, __0, __1, __2); return false;
        }
    }
    [HarmonyPatch(typeof(Window), "QuickSearchWidgetRect")]
    internal static class InfoCardSearchRectPatch
    {
        private static bool Prefix(Window __instance, Rect __0, ref Rect __result)
        {
            if (InfoCardPanel.CurrentCard != __instance) return true;
            __result = InfoCardPanel.SearchRect; return false;
        }
    }
    [HarmonyPatch(typeof(Dialog_InfoCard), "ShowMaterialsButton")]
    internal static class InfoCardMaterialsPatch
    {
        private static bool Prefix(Rect __0, bool __1, ref bool __result)
        {
            if (!InfoCardPanel.Drawing) return true;
            __result = PawnInspectorWidgets.Button(InfoCardPanel.MaterialsRect, "ShowMaterials".Translate()); return false;
        }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.BackButtonFor))]
    internal static class InfoCardBackPatch
    {
        private static bool Prefix(Rect __0, ref bool __result)
        {
            if (!InfoCardPanel.Drawing) return true;
            Rect button = new Rect(10, 8, 24, 24);
            TooltipHandler.TipRegion(button, "Back".Translate());
            __result = PawnInspectorWidgets.Button(button, "‹"); return false;
        }
    }
    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class InfoCardClosePatch
    {
        private static void Prefix(Window __instance) { if (__instance is Dialog_InfoCard card) InfoCardPanel.Release(card); }
    }
    [HarmonyPatch(typeof(Widgets), nameof(Widgets.CloseButtonFor))]
    internal static class InfoCardCloseButtonPatch
    {
        private static bool Prefix(Rect __0, ref bool __result)
        {
            if (!InfoCardPanel.Drawing) return true;
            Rect button = InfoCardPanel.CloseRect;
            using (new Theme.GuiScope(true))
            {
                Theme.ButtonFace(button, Mouse.IsOver(button) ? Theme.Hover : Theme.Surface);
                GUI.color = Theme.Ink;
                GUI.DrawTexture(button.ContractedBy(6), ToolsIcons.Get("clear"));
                __result = Widgets.ButtonInvisible(button);
            }
            return false;
        }
    }
}

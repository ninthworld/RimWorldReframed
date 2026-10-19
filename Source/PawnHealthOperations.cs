using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    internal static class PawnHealthOperations
    {
        private static readonly Dictionary<Bill, float> nativeHeights = new Dictionary<Bill, float>();
        private static GUIStyle actionStyle;
        internal static void Reset() => nativeHeights.Clear();
        internal static Bill Draw(Rect bounds, Pawn pawn, ITab_Pawn_Health tab, BillStack stack, Func<List<FloatMenuOption>> factory, ref Vector2 scroll)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                bool Valid() => PawnHealthNative.CanOperate(pawn, tab) && ReferenceEquals(pawn.BillStack, stack);
                Rect add = new Rect(bounds.x, bounds.y, Math.Min(154, bounds.width), 24);
                if (PawnInspectorWidgets.Button(add, "AddBill".Translate(), "plus", Valid() && stack.Count < BillStack.MaxCount))
                {
                    // This is the exact delegate built by native DrawMedOperationsTab.
                    // It retains the complete recipe/ingredient/DLC/part availability and surgery callbacks.
                    List<FloatMenuOption> options = factory();
                    foreach (FloatMenuOption option in options)
                    {
                        Action action = option.action;
                        if (action != null) option.action = () => { if (Valid()) action(); };
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                UIHighlighter.HighlightOpportunity(add, "AddBill");
                PawnInspectorView.SmallLabel(new Rect(add.xMax + 8, add.y, Math.Max(1, bounds.xMax - add.xMax - 8), 24),
                    stack.Count + "/" + BillStack.MaxCount, Theme.Muted, true);
                Bill[] bills = stack.Bills.ToArray();
                foreach (Bill removed in nativeHeights.Keys.Where(bill => !bills.Contains(bill)).ToArray()) nativeHeights.Remove(removed);
                float Height(Bill bill) => bill.GetType() == typeof(Bill_Medical) ? PawnHealthLayout.BillRow :
                    (nativeHeights.TryGetValue(bill, out float measured) ? measured : 90);
                float height = Math.Max(25, bills.Sum(Height));
                Rect viewport = new Rect(bounds.x, bounds.y + 30, bounds.width, Math.Max(1, bounds.height - 30));
                bool pointerInViewport = Mouse.IsOver(viewport);
                bool pointerEvent = Event.current.isMouse || Event.current.type == EventType.ScrollWheel;
                float width = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, height);
                Bill hovered = null;
                try
                {
                    float y = 0;
                    if (bills.Length == 0) PawnInspectorView.SmallLabel(new Rect(0, 0, width, 24), "None".Translate(), Theme.Muted);
                    foreach (Bill bill in bills)
                    {
                        float rowHeight = Height(bill);
                        Rect row = new Rect(0, y, width, rowHeight);
                        bool full = PawnHealthLayout.FullyVisible(y, rowHeight, scroll.y, viewport.height);
                        bool native = bill.GetType() != typeof(Bill_Medical);
                        bool visible = PawnHealthLayout.Intersects(y, rowHeight, scroll.y, viewport.height);
                        using (new Theme.GuiScope(true))
                        {
                            // Tall mod interfaces may exceed the whole viewport. Native controls
                            // remain usable in its visible clipped portion; pointer events must
                            // originate inside the viewport, even if a child hit rect extends out.
                            GUI.enabled &= (native ? visible && (!pointerEvent || pointerInViewport) : full) && Valid() && !bill.DeletedOrDereferenced;
                            if (native)
                            {
                                // A modded bill can override its configuration and status interface.
                                // Preserve that actual interface rather than guessing its actions.
                                GUI.color = GUI.contentColor = Color.white;
                                if (GUI.enabled || Event.current.type == EventType.Repaint)
                                {
                                    Rect actual = bill.DoInterface(0, y, width, stack.IndexOf(bill));
                                    if (Event.current.type == EventType.Repaint) nativeHeights[bill] = actual.height + 6;
                                }
                            }
                            else DrawBill(row, bill, stack);
                        }
                        if (full && Mouse.IsOver(row) && !bill.DeletedOrDereferenced) hovered = bill;
                        y += rowHeight;
                    }
                }
                finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, height, 17933); }
                return hovered;
            }
        }
        private static void DrawBill(Rect row, Bill bill, BillStack stack)
        {
            if (Mouse.IsOver(row)) Theme.RoundRect(row, Theme.Hover, 2);
            const float actionWidth = 21;
            float actionX = row.xMax - actionWidth * 5;
            string label = bill.LabelCap;
            PawnInspectorView.Label(new Rect(row.x + 2, row.y, Math.Max(1, actionX - row.x - 9), 22), label, bill.suspended ? Theme.Muted : Theme.Ink);
            if (bill.suspended) PawnInspectorView.SmallLabel(new Rect(row.x + 2, row.y + 17, Math.Max(1, actionX - row.x - 9), 14), "SuspendedCaps".Translate(), Theme.Muted);
            TooltipHandler.TipRegion(new Rect(row.x, row.y, Math.Max(1, actionX - row.x), row.height), label + "\n" + bill.recipe.description);
            Rect button = new Rect(actionX, row.y + 3, 19, 22);
            int index = stack.IndexOf(bill);
            if (ActionButton(button, "↑", "ReorderBillUpTip".Translate(), index > 0))
            { stack.Reorder(bill, -1); SoundDefOf.Tick_High.PlayOneShotOnCamera(); }
            button.x += actionWidth;
            if (ActionButton(button, "↓", "ReorderBillDownTip".Translate(), index >= 0 && index < stack.Count - 1))
            { stack.Reorder(bill, 1); SoundDefOf.Tick_Low.PlayOneShotOnCamera(); }
            button.x += actionWidth;
            if (ActionButton(button, bill.suspended ? "▶" : "Ⅱ", "SuspendBillTip".Translate()))
            { bill.suspended = !bill.suspended; SoundDefOf.Click.PlayOneShotOnCamera(); }
            button.x += actionWidth;
            if (PawnInspectorWidgets.IconButton(button, "info", "ThingInfo".Translate())) Find.WindowStack.Add(new Dialog_InfoCard(bill.recipe));
            button.x += actionWidth;
            if (ActionButton(button, "×", "DeleteBillTip".Translate()))
            { stack.Delete(bill); SoundDefOf.Click.PlayOneShotOnCamera(); }
            PawnInspectorWidgets.Divider(new Rect(row.x, row.yMax - 2, row.width, 1));
        }
        private static bool ActionButton(Rect rect, string label, string tip, bool enabled = true)
        {
            TooltipHandler.TipRegion(rect, tip);
            using (new Theme.GuiScope(true))
            {
                bool available = enabled && GUI.enabled;
                Theme.ButtonFace(rect, available && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, !available);
                GUI.enabled = available; GUI.color = available ? Theme.Ink : Theme.Muted;
                if (actionStyle == null) actionStyle = new GUIStyle(Text.CurFontStyle)
                { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(), margin = new RectOffset() };
                GUI.Label(rect, label, actionStyle);
                return available && Widgets.ButtonInvisible(rect);
            }
        }
    }

    [HarmonyPatch]
    internal static class PawnHealthOperationsPatch
    {
        private static MethodBase TargetMethod() => PawnHealthNative.Operations;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> codes = source.Select(code => new CodeInstruction(code)).ToList();
            MethodInfo listing = AccessTools.Method(typeof(BillStack), nameof(BillStack.DoListing),
                new[] { typeof(Rect), typeof(Func<List<FloatMenuOption>>), typeof(Vector2).MakeByRefType(), typeof(float).MakeByRefType() });
            PawnHealthPanel.OperationsReady = codes.Count(code => code.Calls(listing)) == 1;
            if (!PawnHealthPanel.OperationsReady)
            {
                Log.Warning("[Reframed UI] Health operation listing seam changed; preserving the native Health body.");
                return codes;
            }
            MethodInfo adapter = AccessTools.Method(typeof(PawnHealthPanel), nameof(PawnHealthPanel.DrawBillListing));
            foreach (CodeInstruction code in codes)
                if (code.Calls(listing)) { code.opcode = OpCodes.Call; code.operand = adapter; }
            return codes;
        }
    }
}

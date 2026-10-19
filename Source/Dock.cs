using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Patch only the drawing call. MainButtonsRoot.MainButtonsOnGUI handles every
    // native hotkey after DoButtons, including buttons currently in overflow.
    [HarmonyPatch(typeof(MainButtonsRoot), "DoButtons")]
    [StaticConstructorOnStartup]
    internal static class Dock
    {
        private struct Tab
        {
            internal MainButtonDef Def;
            internal float Width;
            internal int NativeIndex, StockOrder, Group;
        }

        private static readonly List<Tab> Visible = new List<Tab>();
        private static readonly List<float> Widths = new List<float>();
        private static readonly List<int> Groups = new List<int>();
        private static readonly Dictionary<string, float> LabelWidths = new Dictionary<string, float>(StringComparer.Ordinal);
        private static GUIStyle labelStyle;
        private static GUIStyle shortcutStyle;
        private static Texture2D groupDivider;
        private const float Gap = 3f;
        private const float GroupGap = 11f;
        private const float OverflowWidth = 24f;
        private const float ArchitectSize = 32f;
        private const float SplitGap = 5f;
        private const float PanelInset = 4f;
        private const float ButtonHeight = 22f;

        private static bool Prefix(List<MainButtonDef> ___allButtonsInOrder)
        {
            if (!HudLayout.DockEnabled || UI.screenHeight < 480) return true;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                CollectTabs(___allButtonsInOrder);

                float totalAvailable = HudLayout.DockAvailableWidth;
                float rowAvailable = totalAvailable - ArchitectSize - SplitGap - PanelInset * 2f;
                int visibleCount = DockLayout.VisibleCount(Widths, Groups, rowAvailable, Gap, GroupGap,
                    OverflowWidth, out float contentsWidth, out bool overflow);
                if (contentsWidth > rowAvailable || (overflow && rowAvailable < OverflowWidth)) return true;

                bool inputAvailable = GUI.enabled && Find.WindowStack != null &&
                    !Find.WindowStack.AnyWindowAbsorbingAllInput;
                DrawArchitect(inputAvailable);
                Rect panel = default(Rect);
                if (Visible.Count > 0)
                {
                    panel = new Rect(HudLayout.DockLeft + ArchitectSize + SplitGap, HudLayout.DockY,
                        contentsWidth + PanelInset * 2f, HudLayout.DockHeight);
                    // The reference uses one quiet dock surface and flat, individually
                    // separated controls; the prototype gloss strip is deliberately absent.
                    Theme.RoundRect(panel, Theme.Rgb(DesignTokens.Panel).ToTransparent(.96f), 5f);
                    float x = panel.x + PanelInset;
                    for (int i = 0; i < visibleCount; i++)
                    {
                        if (i > 0)
                        {
                            bool groupBreak = Groups[i] != Groups[i - 1];
                            if (groupBreak) DrawGroupDivider(new Rect(x + (GroupGap - 1f) * .5f,
                                panel.center.y - 10f, 1f, 20f));
                            x += groupBreak ? GroupGap : Gap;
                        }
                        Tab tab = Visible[i];
                        Rect rect = new Rect(x, panel.y + (panel.height - ButtonHeight) * .5f,
                            tab.Width, ButtonHeight);
                        if (HasCustomButton(tab.Def.Worker))
                        {
                            if (inputAvailable) tab.Def.Worker.DoButton(rect);
                            else PaintTabFace(rect, Theme.Surface, true);
                        }
                        else DrawButton(tab.Def.Worker, rect, inputAvailable);
                        x += tab.Width;
                    }

                    if (overflow)
                    {
                        if (visibleCount > 0) x += Gap;
                        Rect more = new Rect(x, panel.y + (panel.height - ButtonHeight) * .5f,
                            OverflowWidth, ButtonHeight);
                        bool hover = inputAvailable && Mouse.IsOver(more);
                        PaintTabFace(more, hover ? Theme.Hover : Theme.Surface, !inputAvailable);
                        DrawSmallLabel(more, "…", inputAvailable ? Theme.Rgb(DesignTokens.Secondary) : Theme.Muted, TextAnchor.MiddleCenter);
                        if (inputAvailable && hover) TooltipHandler.TipRegion(more, "More tabs");
                        if (inputAvailable && Widgets.ButtonInvisible(more)) OpenOverflow(visibleCount);
                    }
                }

                HudLayout.DrawMenuButton();
                // Absorb unused gaps after the Architect and menu controls have had input.
                GenUI.AbsorbClicksInRect(new Rect(0f, HudLayout.DockY - 1f,
                    EscMenuPanel.OwnsWindow ? HudLayout.MenuButtonBounds.x - 1 : UI.screenWidth,
                    UI.screenHeight - HudLayout.DockY + 1f));
            }
            return false;
        }

        private static void CollectTabs(List<MainButtonDef> nativeOrder)
        {
            Visible.Clear();
            Widths.Clear();
            Groups.Clear();
            if (nativeOrder == null) return;
            for (int i = 0; i < nativeOrder.Count; i++)
            {
                MainButtonDef def = nativeOrder[i];
                if (def == null || def == MainButtonDefOf.Menu || def == MainButtonDefOf.Architect || !def.Worker.Visible) continue;
                StockPosition(def.defName, out int order, out int group);
                Visible.Add(new Tab { Def = def, Width = ButtonWidth(def), NativeIndex = i, StockOrder = order, Group = group });
            }
            Visible.Sort((left, right) =>
            {
                int rank = left.StockOrder.CompareTo(right.StockOrder);
                return rank != 0 ? rank : left.NativeIndex.CompareTo(right.NativeIndex);
            });
            foreach (Tab tab in Visible)
            {
                Widths.Add(tab.Width);
                Groups.Add(tab.Group);
            }
        }

        private static void StockPosition(string defName, out int order, out int group)
        {
            switch (defName)
            {
                case "Work": order = 0; group = 0; return;
                case "Schedule": order = 1; group = 0; return;
                case "Assign": order = 2; group = 0; return;
                case "Animals": order = 3; group = 1; return;
                case "Wildlife": order = 4; group = 1; return;
                case "Research": order = 5; group = 2; return;
                case "Quests": order = 6; group = 2; return;
                case "World": order = 7; group = 3; return;
                case "Factions": order = 8; group = 3; return;
                case "Ideos": case "Ideoligions": order = 9; group = 3; return;
                case "History": order = 10; group = 3; return;
                default: order = 1000; group = 4; return;
            }
        }

        private static void DrawArchitect(bool inputAvailable)
        {
            MainButtonDef architect = MainButtonDefOf.Architect;
            if (architect == null || !architect.Worker.Visible) return;
            Rect rect = new Rect(HudLayout.DockLeft, HudLayout.DockY - 1f, ArchitectSize, ArchitectSize);
            bool disabled = architect.Worker.Disabled || !inputAvailable;
            bool hover = !disabled && Mouse.IsOver(rect);
            Theme.ButtonFace(rect, hover ? Theme.Hover : Theme.Surface, disabled, radius: 5f,
                rim: Theme.Rgb(0x465B6B));
            DockIcons.DrawArchitectPlus(new Rect(rect.x + 8f, rect.y + 6f, 15f, 15f),
                disabled ? Theme.Muted : Theme.Rgb(0xA5B7C4));
            string key = architect.hotKey != null && architect.hotKey.MainKey != KeyCode.None ? architect.hotKey.MainKeyLabel : "";
            if (!string.IsNullOrEmpty(key))
            {
                Text.Font = GameFont.Small;
                if (shortcutStyle == null || shortcutStyle.font != Text.CurFontStyle.font)
                {
                    shortcutStyle = new GUIStyle(Text.CurFontStyle) { fontSize = 8, alignment = TextAnchor.MiddleCenter,
                        wordWrap = false, richText = false, clipping = TextClipping.Clip,
                        padding = new RectOffset(), margin = new RectOffset() };
                    shortcutStyle.normal.textColor = Color.white;
                }
                float width = Mathf.Clamp(shortcutStyle.CalcSize(new GUIContent(key)).x + 3f, 8f, rect.width - 4f);
                Rect shortcut = new Rect(rect.xMax - width - 2f, rect.yMax - 10f, width, 8f);
                Theme.RoundRect(shortcut, Theme.Rgb(0x151D24), 1.5f);
                using (new Theme.GuiScope(true))
                { GUI.color = Theme.Muted; GUI.Label(shortcut, key, shortcutStyle); }
            }
            if (inputAvailable && Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, architect.LabelCap.Colorize(ColorLibrary.Yellow) +
                    (string.IsNullOrEmpty(key) ? "" : " (" + key + ")") + "\n\n" + architect.description);
            if (disabled)
            {
                if (inputAvailable && Event.current.type == EventType.MouseDown && Mouse.IsOver(rect)) Event.current.Use();
                return;
            }
            if (Widgets.ButtonInvisible(rect)) architect.Worker.InterfaceTryActivate();
            if (inputAvailable && Find.WindowStack != null && !Find.WindowStack.NonImmediateDialogWindowOpen)
                UIHighlighter.HighlightOpportunity(rect, architect.cachedHighlightTagClosed);
        }

        private static float ButtonWidth(MainButtonDef def)
        {
            float iconWidth = 13f;
            string label = def.LabelCap;
            GUIStyle style = LabelStyle();
            if (!LabelWidths.TryGetValue(label, out float measuredWidth))
            {
                measuredWidth = style.CalcSize(new GUIContent(label)).x;
                LabelWidths[label] = measuredWidth;
            }
            // Reserve the full label plus glyph/scale rounding room. Long names
            // move as whole buttons into overflow rather than clipping at a cap.
            return Mathf.Max(43f, iconWidth + Mathf.Ceil(measuredWidth) + 17f);
        }

        private static bool HasCustomButton(MainButtonWorker worker)
        {
            var method = worker.GetType().GetMethod(nameof(MainButtonWorker.DoButton),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(Rect) }, null);
            // The stock Assembly-CSharp workers use the shared base DoButton. A
            // mod-defined override keeps its native custom control; shipped stock
            // actions always use the corrected flat icon-and-label treatment.
            return method != null && method.DeclaringType != typeof(MainButtonWorker) &&
                worker.GetType().Assembly != typeof(MainButtonWorker).Assembly;
        }

        private static void DrawButton(MainButtonWorker worker, Rect rect, bool inputAvailable)
        {
            using (new Theme.GuiScope(true))
            {
                MainButtonDef def = worker.def;
                bool disabled = worker.Disabled || !inputAvailable;
                bool hover = !disabled && Mouse.IsOver(rect);
                bool selected = Find.MainTabsRoot.OpenTab == def;
                PaintTabFace(rect, selected ? Theme.Selected : hover ? Theme.Hover : Theme.Surface, disabled);

                float progress = Mathf.Clamp01(worker.ButtonBarPercent);
                if (progress > 0f)
                    Widgets.DrawBoxSolid(new Rect(rect.x + 3f, rect.yMax - 1f, (rect.width - 6f) * progress, 1f), Theme.Muted);

                Rect iconRect = new Rect(rect.x + 3f, rect.center.y - 6.5f, 13f, 13f);
                DockIcons.Draw(def, iconRect, disabled ? Theme.Muted : Theme.Rgb(0xA5B7C4));
                Rect labelRect = new Rect(iconRect.xMax + 3f, rect.y, Mathf.Max(1f, rect.xMax - iconRect.xMax - 6f), rect.height);
                string label = def.LabelCap;
                DrawSmallLabel(labelRect, label, disabled ? Theme.Muted : selected || hover ? Theme.Ink : Theme.Rgb(DesignTokens.Secondary), TextAnchor.MiddleLeft);

                if (inputAvailable && Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, def.LabelCap.Colorize(ColorLibrary.Yellow) + "\n\n" + def.description);
                if (disabled)
                {
                    if (inputAvailable && Event.current.type == EventType.MouseDown && Mouse.IsOver(rect)) Event.current.Use();
                    return;
                }
                if (Widgets.ButtonInvisible(rect)) worker.InterfaceTryActivate();
                if (inputAvailable && !selected && Find.WindowStack != null && !Find.WindowStack.NonImmediateDialogWindowOpen)
                    UIHighlighter.HighlightOpportunity(rect, def.cachedHighlightTagClosed);
            }
        }

        private static void DrawSmallLabel(Rect rect, string label, Color color, TextAnchor anchor)
        {
            if (Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                GUIStyle style = LabelStyle();
                style.alignment = anchor;
                GUI.color = color;
                GUI.Label(rect, label, style);
            }
        }

        // Dock tabs use a flat perimeter, without the shared button's bright top rim.
        private static void PaintTabFace(Rect rect, Color face, bool disabled)
        {
            if (Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Theme.RoundRect(rect, Theme.Rgb(0x303C47).ToTransparent(.7f), 2f);
                Theme.RoundRect(rect.ContractedBy(.6f), face, 1.5f);
                if (disabled) Theme.RoundRect(rect, new Color(.025f, .035f, .045f, .35f), 2f);
            }
        }

        private static void DrawGroupDivider(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (groupDivider == null)
            {
                groupDivider = new Texture2D(1, 32, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[32];
                for (int y = 0; y < pixels.Length; y++)
                    pixels[y] = new Color(1, 1, 1, Mathf.Sin(y / 31f * Mathf.PI));
                groupDivider.SetPixels(pixels);
                groupDivider.Apply(false, true);
            }
            using (new Theme.GuiScope(true))
            {
                Color color = Theme.Muted; color.a = .65f;
                GUI.color = color;
                GUI.DrawTexture(rect, groupDivider);
            }
        }

        private static GUIStyle LabelStyle()
        {
            Text.Font = GameFont.Small;
            GUIStyle native = Text.CurFontStyle;
            if (labelStyle == null || labelStyle.font != native.font)
            {
                labelStyle = new GUIStyle(native)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    richText = true,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(),
                    margin = new RectOffset()
                };
                labelStyle.normal.textColor = Color.white;
                LabelWidths.Clear();
            }
            return labelStyle;
        }

        private static void OpenOverflow(int first)
        {
            var options = new List<FloatMenuOption>();
            for (int i = first; i < Visible.Count; i++)
            {
                MainButtonDef def = Visible[i].Def;
                MainButtonWorker worker = def.Worker;
                Action action = worker.Disabled ? null : () =>
                {
                    if (worker.Visible && !worker.Disabled) worker.InterfaceTryActivate();
                };
                var option = new FloatMenuOption(def.LabelCap, action,
                    MenuOptionPriority.Default,
                    rect => TooltipHandler.TipRegion(rect, def.LabelCap.Colorize(ColorLibrary.Yellow) + "\n\n" + def.description));
                options.Add(option);
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}

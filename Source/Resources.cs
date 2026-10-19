using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
    internal static class Resources
    {
        private sealed class Group
        {
            internal string Id, Name;
            internal int Order;
            internal readonly List<ThingDef> Items = new List<ThingDef>();
            internal readonly HashSet<string> LegacyIds = new HashSet<string>();
        }
        private sealed class View
        {
            internal readonly List<Group> Groups = new List<Group>();
            internal Vector2 Scroll;
            internal Rect LastPanel;
            internal bool Target, Categorized;
            internal float Progress, RefreshAt, HoldUntil, UpdatedAt = -1f;
            internal int UpdatedFrame = -1;
            internal Dictionary<ThingDef, int> Counts;
        }
        private static readonly ConditionalWeakTable<Map, View> Views = new ConditionalWeakTable<Map, View>();
        private const float CompactWidth = 76f, ExpandedWidth = 164f;
        private const float Padding = 8f, HeaderHeight = 14f, GroupGap = 10f, RowHeight = 16f;
        private const float Duration = .24f, HoverGrace = .18f;
        private static GUIStyle body, scrollTrack, scrollThumb;
        private static Texture2D trendUp, trendDown, trendFlat, trendUnknown, disclosureOpen, disclosureClosed;
        private static readonly Color HeadingInk = Theme.Rgb(0xA8B5BE);

        private static bool Prefix()
        {
            if (!ReframedMod.Active || !ReframedMod.Settings.ResourcePanel || Find.CurrentMap == null ||
                WorldRendererUtility.WorldSelected || TutorSystem.TutorialMode || UI.screenWidth < 800 || UI.screenHeight < 480)
                return true;
            if (Event.current.type == EventType.Layout || Find.MainTabsRoot.OpenTab == MainButtonDefOf.Menu) return false;
            Map map = Find.CurrentMap;
            var history = map.GetComponent<ResourceHistoryComponent>();
            if (history == null) return true;
            Dictionary<ThingDef, int> counts = history.CurrentCounts;
            var view = Views.GetValue(map, ignored => new View());
            float now = Time.realtimeSinceStartup;
            if (!ReferenceEquals(view.Counts, counts) || now >= view.RefreshAt || view.Categorized != Prefs.ResourceReadoutCategorized)
            {
                if (view.Categorized != Prefs.ResourceReadoutCategorized) view.Scroll = Vector2.zero;
                view.Categorized = Prefs.ResourceReadoutCategorized;
                Refresh(counts, history, view);
                view.Counts = counts;
                view.RefreshAt = now + .75f;
            }
            bool input = GUI.enabled && !Find.WindowStack.AnyWindowAbsorbingAllInput && !Find.WindowStack.MouseObscuredNow;
            if (!input && Event.current.type != EventType.Repaint) return false;
            EnsureStyles();
            float top = 12f + (Prefs.DevMode ? 30f : 0f);
            float fullHeight = FullHeight(view, history);
            float height = Mathf.Min(Mathf.Max(60f, UI.screenHeight - 220f), Padding * 2f + fullHeight);
            Rect compact = new Rect(8f, top, CompactWidth, height);
            if (input)
            {
                bool hover = Mouse.IsOver(compact) || (view.Progress > 0f && Mouse.IsOver(view.LastPanel));
                if (hover) view.HoldUntil = now + HoverGrace;
                view.Target = hover || now < view.HoldUntil;
            }
            if (view.UpdatedFrame != Time.frameCount)
            {
                float elapsed = view.UpdatedAt < 0 ? 0f : Mathf.Min(.1f, Mathf.Max(0f, now - view.UpdatedAt));
                view.Progress = Mathf.MoveTowards(view.Progress, view.Target ? 1f : 0f, elapsed / Duration);
                view.UpdatedAt = now; view.UpdatedFrame = Time.frameCount;
            }
            float reveal = Mathf.SmoothStep(0f, 1f, view.Progress);
            Rect panel = new Rect(compact.x, compact.y, Mathf.Lerp(CompactWidth, ExpandedWidth, reveal), height);
            view.LastPanel = panel;
            // A compact/in-progress click only reveals. Fully visible items retain native access.
            if (input && view.Progress < .98f && LeftDown(panel)) Event.current.Use();
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = input;
                GUI.color = GUI.contentColor = Color.white;
                Theme.Panel(panel);
                Rect viewport = new Rect(panel.x + Padding, panel.y + Padding,
                    panel.width - Padding * 2f, panel.height - Padding * 2f);
                bool overflow = fullHeight > viewport.height + .01f;
                if (overflow) viewport.width -= 7f;
                view.Scroll.y = Mathf.Clamp(view.Scroll.y, 0f, Mathf.Max(0f, fullHeight - viewport.height));
                // Save root containment before BeginScrollView changes mouse coordinates.
                bool listInput = input && Mouse.IsOver(viewport);
                Rect content = new Rect(0f, 0f, viewport.width, fullHeight);
                view.Scroll = GUI.BeginScrollView(viewport, view.Scroll, content, false, false, GUIStyle.none, GUIStyle.none);
                try { DrawList(counts, history, view, content, viewport.height, listInput, reveal, overflow); }
                finally { GUI.EndScrollView(); }
                if (overflow)
                {
                    Rect track = new Rect(panel.xMax - 7f, viewport.y, 4f, viewport.height);
                    scrollThumb.fixedHeight = Mathf.Max(20f, viewport.height * viewport.height / fullHeight);
                    using (new Theme.GuiScope(true))
                    {
                        // A quiet compact hint remains, with full contrast when expanded.
                        GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(.35f, .8f, reveal));
                        view.Scroll.y = GUI.VerticalSlider(track, view.Scroll.y, 0f,
                            fullHeight - viewport.height, scrollTrack, scrollThumb);
                    }
                }
                if (input)
                {
                    if (LeftDown(panel)) Event.current.Use();
                    GenUI.AbsorbClicksInRect(panel);
                }
            }
            return false;
        }

        private static bool LeftDown(Rect rect) => Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 && Mouse.IsOver(rect);
        private static float FullHeight(View view, ResourceHistoryComponent history)
        {
            if (view.Groups.Count == 0) return HeaderHeight + RowHeight;
            float height = 0f;
            foreach (var group in view.Groups)
                height += HeaderHeight + GroupGap + (history.Closed(group.Id) ? 0f : group.Items.Count * RowHeight);
            return height - GroupGap;
        }

        private static void DrawList(Dictionary<ThingDef, int> counts, ResourceHistoryComponent history,
            View view, Rect content, float viewportHeight, bool input, float reveal, bool overflow)
        {
            if (view.Groups.Count == 0)
            {
                DrawHeading(new Rect(0, 0, content.width, HeaderHeight), "Materials", HeadingInk);
                Label(new Rect(0, HeaderHeight, content.width, RowHeight), "None stored", Theme.Muted);
                return;
            }
            float y = 0f;
            foreach (var group in view.Groups)
            {
                Rect header = new Rect(0f, y, content.width, HeaderHeight);
                bool closed = history.Closed(group.Id);
                if (header.yMax >= view.Scroll.y && header.y < view.Scroll.y + viewportHeight)
                {
                    DrawHeading(header, group.Name, HeadingInk);
                    Rect disclosure = new Rect(header.xMax - 12f, y, 12f, HeaderHeight);
                    if (reveal > .01f)
                    {
                        Icon(disclosure.ContractedBy(2f), closed ? Resolve(ref disclosureClosed, "Closed") :
                            Resolve(ref disclosureOpen, "Open"), HeadingInk, reveal);
                        if (input && view.Progress >= .98f)
                        {
                            TooltipHandler.TipRegion(disclosure, (closed ? "Expand " : "Collapse ") + group.Name + " category.");
                            if (LeftDown(disclosure)) { history.Toggle(group.Id); Event.current.Use(); }
                        }
                    }
                    if (input)
                    {
                        Rect title = new Rect(header.x, header.y, Mathf.Max(1f, header.width - (reveal > .01f ? 14f : 0f)), header.height);
                        TooltipHandler.TipRegion(title, group.Name + " — hover to expand; move away to collapse.");
                        if (LeftDown(title)) Event.current.Use();
                    }
                }
                y += HeaderHeight;
                // Keep this event's category geometry; changes take effect on the next draw.
                if (!closed) foreach (ThingDef def in group.Items)
                {
                    Rect row = new Rect(0f, y, content.width, RowHeight);
                    if (row.yMax >= view.Scroll.y && row.y < view.Scroll.y + viewportHeight)
                        DrawItem(counts, history, view, def, row, input, reveal, overflow);
                    y += RowHeight;
                }
                y += GroupGap;
            }
        }

        private static void DrawItem(Dictionary<ThingDef, int> counts, ResourceHistoryComponent history,
            View view, ThingDef def, Rect row, bool input, float reveal, bool overflow)
        {
            if (!counts.TryGetValue(def, out int count)) return;
            bool hover = input && Mouse.IsOver(row);
            if (hover) Theme.RoundRect(row, new Color(.45f, .55f, .62f, .10f), 2f);
            GUI.color = Color.white;
            Widgets.ThingIcon(new Rect(row.x, row.y + 2f, 12f, 12f), def);
            string quantity = count.ToString(CultureInfo.CurrentCulture);
            bool trend;
            long change;
            string reason;
            if (!MaterialsDiagnostics.TryTrend(def, count, out trend, out change, out reason))
                trend = history.Ledger.TryChange(def.defName, Find.TickManager.TicksGame, count, out change, out reason);
            string delta = trend ? (change > 0 ? "+" : "") + change.ToString(CultureInfo.CurrentCulture) : "Unavailable";
            float expandedInner = ExpandedWidth - Padding * 2f - (overflow ? 7f : 0f);
            float amountWidth = Mathf.Min(56f, Mathf.Ceil(body.CalcSize(new GUIContent(quantity)).x) + 1f);
            float finalQuantityX = expandedInner - 13f - amountWidth;
            float quantityX = Mathf.Lerp(18f, finalQuantityX, reveal);
            float detailsAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .95f, reveal));
            if (detailsAlpha > .001f)
            {
                // Reveal a fixed label through a growing clip, avoiding per-frame ellipsis changes.
                float labelWidth = Mathf.Max(0f, quantityX - 22f);
                GUI.BeginGroup(new Rect(18f, row.y, labelWidth, row.height));
                try { Label(new Rect(0f, 0f, Mathf.Max(1f, finalQuantityX - 22f), row.height), def.LabelCap,
                    new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, detailsAlpha)); }
                finally { GUI.EndGroup(); }
                Rect marker = new Rect(row.xMax - 9f, row.y + 4f, 8f, 8f);
                Texture2D texture = !trend ? Resolve(ref trendUnknown, "Unknown") : change == 0 ?
                    Resolve(ref trendFlat, "Flat") : change > 0 ? Resolve(ref trendUp, "Up") : Resolve(ref trendDown, "Down");
                Icon(marker, texture, !trend || change == 0 ? Theme.Muted : change > 0 ? Theme.Good : Theme.Danger, detailsAlpha);
            }
            float available = Mathf.Max(1f, row.width - quantityX - Mathf.Lerp(0f, 13f, reveal));
            Label(new Rect(quantityX, row.y, available, row.height), quantity, Theme.Ink);
            if (input)
            {
                TooltipHandler.TipRegion(row, def.LabelCap + ": " + quantity + "\n7-day quantity change: " + delta +
                    "\n" + reason + "\n\n" + def.description + (view.Progress >= .98f ? "\n\nClick for resource information." : "\n\nHover to reveal resource information."));
                if (view.Progress >= .98f && Widgets.ButtonInvisible(row))
                    Find.WindowStack.Add(new Dialog_InfoCard(def));
                else if (LeftDown(row)) Event.current.Use();
            }
        }

        private static void EnsureStyles()
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                GUIStyle native = Text.CurFontStyle;
                if (body == null || body.font != native.font)
                {
                    body = new GUIStyle(native) { fontSize = HudTypography.BodySize, alignment = TextAnchor.MiddleLeft, wordWrap = false,
                        richText = false, clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
                    body.normal.textColor = Color.white;
                }
                if (scrollTrack == null)
                {
                    scrollTrack = new GUIStyle { normal = { background = BaseContent.WhiteTex }, fixedWidth = 3f };
                    scrollThumb = new GUIStyle { normal = { background = BaseContent.WhiteTex }, fixedWidth = 3f };
                    scrollTrack.normal.textColor = Color.white;
                    // Transparent track; native drag behavior remains handled by GUI.VerticalSlider.
                    scrollTrack.normal.background = BaseContent.ClearTex;
                }
            }
        }
        private static void DrawHeading(Rect rect, string value, Color color)
            => HudTypography.Heading(rect, value, color);
        private static void Label(Rect rect, string text, Color color)
        {
            using (new Theme.GuiScope(true))
            {
                // Paint only: restore the blocked input state before any controls.
                GUI.enabled = true;
                GUI.contentColor = Color.white;
                GUI.color = color;
                if (body.CalcSize(new GUIContent(text)).x > rect.width)
                {
                    while (text.Length > 0 && body.CalcSize(new GUIContent(text + "…")).x > rect.width)
                        text = text.Substring(0, text.Length - 1);
                    text += "…";
                }
                GUI.Label(rect, text, body);
            }
        }
        private static Texture2D Resolve(ref Texture2D texture, string name)
        {
            if (texture == null) texture = ContentFinder<Texture2D>.Get("Reframed/Resources/" + name, true);
            return texture;
        }
        private static void Icon(Rect rect, Texture2D texture, Color tint, float alpha)
        {
            using (new Theme.GuiScope(true))
            {
                tint.a *= alpha; GUI.color = tint;
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            }
        }
        internal static Dictionary<string, object> AnimationAuditState()
        {
            var result = new Dictionary<string, object>();
            Map map = Find.CurrentMap;
            if (map == null || !Views.TryGetValue(map, out View view)) { result["active"] = false; return result; }
            result["active"] = true; result["progress"] = view.Progress; result["target"] = view.Target;
            result["interaction"] = "hover-only";
            result["scroll_y"] = view.Scroll.y; result["row_height"] = RowHeight;
            result["rect"] = new Dictionary<string, object> { ["x"] = view.LastPanel.x, ["y"] = view.LastPanel.y,
                ["width"] = view.LastPanel.width, ["height"] = view.LastPanel.height };
            var groups = new List<object>();
            foreach (var g in view.Groups)
            {
                var items = new List<object>();
                foreach (var def in g.Items) items.Add(new Dictionary<string, object> { ["def"] = def.defName,
                    ["label"] = def.LabelCap.ToString(), ["count"] = view.Counts.TryGetValue(def, out int n) ? n : -1 });
                groups.Add(new Dictionary<string, object> { ["name"] = g.Name, ["items"] = items });
            }
            result["groups"] = groups; return result;
        }

        private static void Refresh(Dictionary<ThingDef, int> counts, ResourceHistoryComponent history, View view)
        {
            view.Groups.Clear();
            foreach (var pair in counts)
            {
                ThingDef def = pair.Key;
                if (def == null || (pair.Value <= 0 && !def.resourceReadoutAlwaysShow && !history.Ledger.RecentlyPositive(def.defName))) continue;
                ThingCategoryDef category = NativeCategory(def);
                string id = category == null ? "other" : "category:" + category.defName;
                string legacyId = id;
                string name = category == null ? "Supplies" : category.LabelCap.ToString();
                int order = 3;
                if (HasAncestor(def, "Weapons")) { id = "family:weapons"; name = "Weapons"; order = 1; }
                else if (HasAncestor(def, "Foods")) { id = "family:food"; name = "Food"; order = 2; }
                else if (HasAncestor(def, "ResourcesRaw") || HasAncestor(def, "Manufactured"))
                    { id = "family:materials"; name = "Materials"; order = 0; }
                // Raw resources such as wood can be improvised weapons; their resource category wins.
                else if (def.IsWeapon) { id = "family:weapons"; name = "Weapons"; order = 1; }
                if (!Prefs.ResourceReadoutCategorized) { id = "uncategorized"; name = "Resources"; order = 3; }
                Group group = view.Groups.Find(item => item.Id == id);
                if (group == null)
                {
                    group = new Group { Id = id, Name = name, Order = order };
                    view.Groups.Add(group);
                }
                group.LegacyIds.Add(legacyId);
                group.Items.Add(def);
            }
            foreach (var group in view.Groups)
            {
                if (group.Order >= 3) continue;
                string migrated = group.Id + ":collapse-v1";
                if (history.Closed(migrated)) continue;
                bool allClosed = group.LegacyIds.Count > 0;
                foreach (string legacy in group.LegacyIds) allClosed &= history.Closed(legacy);
                // Keep old IDs intact. A merged family must not hide previously open rows.
                if (allClosed && !history.Closed(group.Id)) history.Toggle(group.Id);
                history.Toggle(migrated);
            }
            view.Groups.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) :
                string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            foreach (var group in view.Groups) group.Items.Sort((a, b) =>
            {
                int priority = b.resourceReadoutPriority.CompareTo(a.resourceReadoutPriority);
                return priority != 0 ? priority : string.Compare(a.label, b.label, StringComparison.CurrentCulture);
            });
        }

        private static bool HasAncestor(ThingDef def, string name)
        {
            if (def.thingCategories == null) return false;
            foreach (var leaf in def.thingCategories)
            {
                var seen = new HashSet<ThingCategoryDef>();
                for (var category = leaf; category != null && seen.Count < 64 && seen.Add(category); category = category.parent)
                    if (category.defName == name) return true;
            }
            return false;
        }

        private static ThingCategoryDef NativeCategory(ThingDef def)
        {
            if (def.thingCategories == null) return null;
            ThingCategoryDef semanticFallback = null;
            foreach (var leaf in def.thingCategories)
            {
                var seen = new HashSet<ThingCategoryDef>();
                for (var category = leaf; category != null && seen.Count < 64 && seen.Add(category); category = category.parent)
                {
                    if (category.resourceReadoutRoot) return category;
                    if (semanticFallback == null && category.parent != null && category.parent.parent == null)
                        semanticFallback = category;
                }
            }
            return semanticFallback;
        }
    }
}

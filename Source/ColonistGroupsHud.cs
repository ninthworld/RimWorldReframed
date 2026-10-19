using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Temporary concept headings; membership remains native roster order.
    [StaticConstructorOnStartup]
    internal static class ColonistGroupsHud
    {
        private static readonly string[] Headings = { "Warriors", "Engineers & builders", "Creative" };
        private static readonly List<SelectionOverlay> SelectionOverlays = new List<SelectionOverlay>();
        private static GUIStyle headingStyle;
        private static GUIStyle controlStyle, scrollTrack, scrollThumb;
        private static int firstRow, viewMap = -1;
        private static bool expanded;
        private const float AnimationDuration = .25f;
        private static float animationProgress, animationTime = -1f;
        private static int animationFrame = -1;
        private static bool animationTarget;
        private static Rect selectionViewport;
        private static Texture2D divider;
        private static int hookFrame = -1;
        private static bool foreignDrawerHook;
        internal static bool Drawing;
        // User-calibrated at UI 1.5: 25% larger than the first 60px trial,
        // while remaining smaller than the original 90px cards. Honor native UI scaling.
        internal static float UnitScale => 5f / 6f;

        private static bool HasForeignDrawerHook()
        {
            if (hookFrame == Time.frameCount) return foreignDrawerHook;
            hookFrame = Time.frameCount;
            foreignDrawerHook = false;
            foreach (string method in new[] { "DrawColonist", "DrawIcons" })
            {
                var patches = Harmony.GetPatchInfo(AccessTools.Method(typeof(ColonistBarColonistDrawer), method));
                if (patches == null) continue;
                foreach (string owner in patches.Owners)
                    if (owner != RuntimePatchBootstrap.OwnerId) { foreignDrawerHook = true; break; }
            }
            return foreignDrawerHook;
        }

        private static bool TryFrame(out Frame frame)
        {
            frame = null;
            if (!ColonistCards.Active || UI.screenWidth < 900 || UI.screenHeight < 600 ||
                (Find.CurrentMap == null && !WorldRendererUtility.WorldSelected) || Find.TilePicker.Active || !Find.PlaySettings.showColonistBar) return false;
            ColonistBar bar = Find.ColonistBar;
            List<ColonistBar.Entry> entries = bar.Entries;
            if (entries.Count == 0 || bar.drawer.GetType() != typeof(ColonistBarColonistDrawer) || HasForeignDrawerHook()) return false;
            bool weaponRow = false;
            var groupCounts = new List<int>();
            var groupNames = new List<string>();
            int lastGroup = int.MinValue;
            foreach (var entry in entries)
            {
                if (entry.pawn == null) return false;
                if (entry.group != lastGroup)
                {
                    lastGroup = entry.group;
                    groupCounts.Add(0);
                    groupNames.Add(entry.map == null ? entry.pawn.GetCaravan()?.LabelCap.ToString() ?? "Caravan".Translate().ToString() :
                        entry.map.Parent?.LabelCap.ToString() ?? "Map " + entry.map.uniqueID);
                }
                groupCounts[groupCounts.Count - 1]++;
                weaponRow |= ShowsWeapon(entry.pawn);
            }
            int currentView = WorldRendererUtility.WorldSelected ? int.MinValue : Find.CurrentMap?.uniqueID ?? -1;
            if (viewMap != currentView)
            {
                viewMap = currentView; firstRow = 0; expanded = false;
                animationProgress = 0; animationTime = Time.realtimeSinceStartup; animationFrame = -1;
            }
            float unit = UnitScale, available = UI.screenWidth - 400f;
            bool nativeGroups = groupCounts.Count > 1;
            PawnBarGeometry.Layout layout;
            if (!(nativeGroups ? PawnBarGeometry.TryBuild(groupCounts, available / unit, 1000000f, weaponRow ? 28f : 0f, out layout) :
                PawnBarGeometry.TryBuild(entries.Count, available / unit, 1000000f, weaponRow ? 28f : 0f, out layout))) return false;
            int fit = Mathf.Max(1, Mathf.FloorToInt((UI.screenHeight * .58f / unit - 44f + PawnBarGeometry.RowGap) / layout.RowPitch));
            int collapsedRows = Mathf.Clamp(ReframedMod.Settings.CollapsedPawnRows, 1, 3);
            bool overflow = layout.RowCount > Mathf.Min(collapsedRows, fit);
            if (overflow && !(nativeGroups ? PawnBarGeometry.TryBuild(groupCounts, available / unit - 12f, 1000000f, weaponRow ? 28f : 0f, out layout) :
                PawnBarGeometry.TryBuild(entries.Count, available / unit - 12f, 1000000f, weaponRow ? 28f : 0f, out layout))) return false;
            int closedRows = Mathf.Min(layout.RowCount, Mathf.Min(collapsedRows, fit));
            int openRows = Mathf.Min(layout.RowCount, Mathf.Min(4, fit));
            int rows = expanded ? openRows : closedRows;
            firstRow = Mathf.Clamp(firstRow, 0, layout.RowCount - rows);
            float closedContent = closedRows * layout.RowPitch - PawnBarGeometry.RowGap;
            float openContent = openRows * layout.RowPitch - PawnBarGeometry.RowGap;
            float progress = AdvanceAnimation(expanded && openRows > closedRows);
            float content = Mathf.Lerp(closedContent, openContent, Mathf.SmoothStep(0, 1, progress));
            float width = (layout.Width + (overflow ? 12f : 0f)) * unit;
            if (overflow) width = Mathf.Max(width, Mathf.Min(available, 200f * unit));
            float height = (content + 16f + (overflow ? 28f : 0f)) * unit;
            Rect bounds = new Rect(180f + (available - width) / 2f, 12f + (Prefs.DevMode ? 30f : 0f), width, height);
            frame = new Frame { Bounds = bounds, Unit = unit, Layout = layout, Entries = entries,
                FirstRow = firstRow, VisibleRows = rows, Overflow = overflow, ContentHeight = content * unit,
                CanExpand = Mathf.Min(4, fit) > Mathf.Min(collapsedRows, fit), CollapsedRows = collapsedRows,
                ClosedContentHeight = closedContent * unit, OpenContentHeight = openContent * unit,
                TargetContentHeight = (expanded ? openContent : closedContent) * unit,
                Headings = nativeGroups ? groupNames.ToArray() : Headings, NativeGroups = nativeGroups };
            return true;
        }
        private static float AdvanceAnimation(bool target)
        {
            animationTarget = target;
            // Real time keeps the transition moving while the game is paused.
            // All GUI passes and neighbor-bound queries share one sample per frame.
            if (animationFrame != Time.frameCount)
            {
                float now = Time.realtimeSinceStartup;
                float delta = animationTime < 0 ? 0 : Mathf.Max(0, now - animationTime);
                animationProgress = Mathf.MoveTowards(animationProgress, target ? 1f : 0f, delta / AnimationDuration);
                animationTime = now;
                animationFrame = Time.frameCount;
            }
            return animationProgress;
        }
        internal static bool TryGetOccupiedBounds(out Rect bounds)
        {
            bool found = TryFrame(out Frame frame);
            bounds = found ? frame.Bounds : default;
            return found;
        }
        private static Rect LocalCard(Frame frame, PawnBarGeometry.Slot card) =>
            new Rect(card.X * frame.Unit, (card.Y - frame.FirstRow * frame.Layout.RowPitch) * frame.Unit,
                PawnBarGeometry.CardWidth * frame.Unit, PawnBarGeometry.CardHeight * frame.Unit);
        private static bool Visible(Frame frame, float y)
        {
            float top = (y - frame.FirstRow * frame.Layout.RowPitch) * frame.Unit;
            return top >= -.01f && top < frame.ContentHeight;
        }
        private static Vector2 Origin(Frame frame) => frame.Bounds.position + Vector2.one * (PawnBarGeometry.Padding * frame.Unit);
        private static Rect Viewport(Frame frame) => new Rect(Origin(frame),
            new Vector2((frame.Layout.Width - 16f) * frame.Unit, frame.ContentHeight));
        private static Rect ScreenCard(Frame frame, PawnBarGeometry.Slot card)
        {
            Rect rect = LocalCard(frame, card); rect.position += Origin(frame); return rect;
        }
        internal static bool Draw(ColonistBar bar, List<Pawn> highlights, ref float nativeScale)
        {
            if (bar != Find.ColonistBar || !TryFrame(out Frame frame)) return false;
            if (Event.current.type == EventType.Layout) return true;
            bool blocked = Find.WindowStack.AnyWindowAbsorbingAllInput;
            if (blocked && Event.current.type != EventType.Repaint) return true;
            if (!blocked && frame.Overflow && Mouse.IsOver(frame.Bounds) && Event.current.type == EventType.ScrollWheel)
            {
                firstRow = Mathf.Clamp(firstRow + (Event.current.delta.y > 0 ? 1 : -1), 0, frame.Layout.RowCount - frame.VisibleRows);
                Event.current.Use();
                TryFrame(out frame);
            }
            float savedScale = nativeScale;
            nativeScale = frame.Unit;
            SelectionOverlays.Clear();
            selectionViewport = Viewport(frame);
            Drawing = true;
            try
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Color.white;
                    GUI.enabled &= !blocked;
                    // The reference container has no top-edge highlight.
                    Theme.RoundRect(frame.Bounds, Theme.PanelFill, 12f * frame.Unit);
                    bool contentInput = !blocked && Viewport(frame).Contains(UI.MousePositionOnUIInverted);
                    Widgets.BeginGroup(Viewport(frame));
                    try
                    {
                        foreach (var section in frame.Layout.Sections)
                        {
                            if (!Visible(frame, section.Y)) continue;
                            float sectionY = section.Y - frame.FirstRow * frame.Layout.RowPitch;
                            Rect header = new Rect(section.X * frame.Unit, sectionY * frame.Unit,
                                section.Width * frame.Unit, PawnBarGeometry.HeaderHeight * frame.Unit);
                            DrawHeading(header, frame.Headings[section.Group], frame.Unit);
                            if (frame.NativeGroups && contentInput && Event.current.type == EventType.MouseUp &&
                                Event.current.button == 0 && Mouse.IsOver(header) && !Find.Selector.dragBox.IsValidAndActive)
                            {
                                var groupEntry = frame.Entries[frame.Layout.Cards.Find(c => c.Group == section.Group).Index];
                                Find.Selector.dragBox.active = false;
                                Find.WorldSelector.dragBox.active = false;
                                if (groupEntry.map == null) CameraJumper.TryJumpAndSelect(groupEntry.pawn);
                                else
                                {
                                    if (!CameraJumper.TryHideWorld() && Find.CurrentMap != groupEntry.map)
                                        SoundDefOf.MapSelected.PlayOneShotOnCamera();
                                    Current.Game.CurrentMap = groupEntry.map;
                                }
                                Event.current.Use();
                            }
                            TooltipHandler.TipRegion(header, frame.Headings[section.Group] + (frame.NativeGroups ? "" :
                                "\nTemporary display section; native pawn order and work assignments are unchanged."));
                            if (section.DividerBefore)
                                DrawDivider(new Rect((section.X - PawnBarGeometry.GroupGap / 2) * frame.Unit,
                                    (sectionY + PawnBarGeometry.HeaderHeight) * frame.Unit, frame.Unit, PawnBarGeometry.CardHeight * frame.Unit));
                        }
                        foreach (var slot in frame.Layout.Cards)
                        {
                            if (!Visible(frame, slot.Y)) continue;
                            var entry = frame.Entries[slot.Index];
                            Pawn pawn = entry.pawn;
                            Rect rect = LocalCard(frame, slot);
                            if (contentInput && Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                            {
                                if (Event.current.button == 0 && Event.current.clickCount == 2)
                                { Event.current.Use(); CameraJumper.TryJump(pawn); }
                                else if (Event.current.button == 1) Event.current.Use();
                            }
                            ColonistCardRenderer.Draw(rect, pawn, entry.map, highlights.Contains(pawn), frame.Unit);
                            if (ShowsWeapon(pawn))
                            {
                                using (new Theme.GuiScope(true))
                                {
                                    GUI.color = new Color(1, 1, 1, ColonistCardRenderer.EntryAlpha(rect, entry.map));
                                    Rect weapon = new Rect(rect.center.x - 12f * frame.Unit, rect.yMax + 3f * frame.Unit, 24f * frame.Unit, 24f * frame.Unit);
                                    if (Event.current.type == EventType.Repaint)
                                        Widgets.ThingIcon(weapon, pawn.equipment.Primary, 1, null, stackOfOne: true);
                                    TooltipHandler.TipRegion(weapon, pawn.equipment.Primary.LabelCap);
                                }
                            }
                        }
                    }
                    finally { Widgets.EndGroup(); }
                    DrawSelectionOverlays();
                    if (frame.Overflow) DrawOverflow(frame);
                    if (!blocked && !TryHit(UI.MousePositionOnUIInverted, out _)) GenUI.AbsorbClicksInRect(frame.Bounds);
                }
            }
            finally { nativeScale = savedScale; Drawing = false; SelectionOverlays.Clear(); }
            if (Event.current.type == EventType.Repaint) highlights.Clear();
            return true;
        }
        private static void DrawDivider(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (divider == null)
            {
                divider = new Texture2D(1, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                Color[] pixels = new Color[64];
                for (int y = 0; y < 64; y++)
                { Color color = Theme.Border; color.a = Mathf.Sin(y / 63f * Mathf.PI) * .7f; pixels[y] = color; }
                divider.SetPixels(pixels); divider.Apply(false, true);
            }
            GUI.DrawTexture(rect, divider);
        }
        private static void DrawHeading(Rect rect, string label, float unit)
        {
            if (Event.current.type != EventType.Repaint) return;
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                int size = Mathf.Max(6, Mathf.RoundToInt(9f * unit));
                if (headingStyle == null || headingStyle.font != Text.CurFontStyle.font || headingStyle.fontSize != size)
                {
                    headingStyle = new GUIStyle(Text.CurFontStyle) { fontSize = size, fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false,
                        clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
                    headingStyle.normal.textColor = Color.white;
                }
                GUI.color = Theme.Muted;
                GUI.contentColor = Color.white;
                string text = label.ToUpperInvariant();
                float width = HeadingWidth(text, unit);
                if (width <= rect.width) HeadingLine(rect, text, unit);
                else if (text.Contains(" & "))
                {
                    string[] lines = text.Split(new[] { " & " }, StringSplitOptions.None);
                    HeadingLine(new Rect(rect.x, rect.y, rect.width, rect.height / 2), lines[0], unit);
                    HeadingLine(new Rect(rect.x, rect.center.y, rect.width, rect.height / 2), "& " + lines[1], unit);
                }
                else HeadingLine(rect, text, unit);
            }
        }
        private static float HeadingWidth(string text, float unit)
        {
            float width = 0;
            foreach (char glyph in text) width += headingStyle.CalcSize(new GUIContent(glyph.ToString())).x + .8f * unit;
            return Mathf.Max(0, width - .8f * unit);
        }
        private static void HeadingLine(Rect rect, string text, float unit)
        {
            if (HeadingWidth(text, unit) > rect.width)
            {
                int length = text.Length;
                while (length > 0 && HeadingWidth(text.Substring(0, length) + "…", unit) > rect.width) length--;
                text = text.Substring(0, length) + "…";
            }
            float x = rect.x + (rect.width - HeadingWidth(text, unit)) / 2;
            foreach (char glyph in text)
            {
                string value = glyph.ToString();
                float width = headingStyle.CalcSize(new GUIContent(value)).x;
                GUI.Label(new Rect(x, rect.y, width + unit, rect.height), value, headingStyle);
                x += width + .8f * unit;
            }
        }
        private static void DrawOverflow(Frame frame)
        {
            float s = frame.Unit;
            Text.Font = GameFont.Small;
            if (controlStyle == null)
            {
                controlStyle = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.RoundToInt(12f * s),
                    alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false,
                    padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
                controlStyle.normal.textColor = Color.white;
                scrollTrack = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
                scrollThumb = new GUIStyle { margin = new RectOffset(), padding = new RectOffset(), fixedWidth = 4f * s };
                scrollThumb.normal.background = scrollThumb.hover.background = scrollThumb.active.background = BaseContent.WhiteTex;
            }
            Vector2 origin = Origin(frame);
            Rect rail = new Rect(frame.Bounds.xMax - 12f * s, origin.y, 6f * s, frame.ContentHeight);
            Theme.RoundRect(rail, Theme.Surface, 3f * s);
            float revealedRows = (frame.ContentHeight / s + PawnBarGeometry.RowGap) / frame.Layout.RowPitch;
            scrollThumb.fixedHeight = Mathf.Max(20f * s, rail.height * revealedRows / frame.Layout.RowCount);
            GUI.color = Theme.Muted;
            firstRow = Mathf.RoundToInt(GUI.VerticalSlider(rail, firstRow, 0, frame.Layout.RowCount - frame.VisibleRows, scrollTrack, scrollThumb));
            GUI.color = Color.white;
            Rect footer = new Rect(origin.x, origin.y + frame.ContentHeight + 6f * s, frame.Bounds.width - 16f * s, 22f * s);
            int first = -1, last = -1, warnings = 0, warningRow = -1;
            foreach (var slot in frame.Layout.Cards)
            {
                if (Visible(frame, slot.Y)) { if (first < 0) first = slot.Index; last = slot.Index; }
                else if (NeedsAttention(frame.Entries[slot.Index].pawn))
                { warnings++; if (warningRow < 0) warningRow = Mathf.FloorToInt(slot.Y / frame.Layout.RowPitch); }
            }
            GUI.color = Theme.Muted;
            Rect counter = new Rect(footer.x, footer.y, 100f * s, footer.height);
            GUI.Label(counter,
                (first + 1) + "–" + (last + 1) + " / " + frame.Entries.Count, controlStyle);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(counter, "Scroll over the pawn bar or drag the scrollbar.");
            if (warnings > 0)
            {
                Rect alert = new Rect(footer.xMax - 82f * s, footer.y, 54f * s, footer.height);
                if (IconButton(alert, true, s, warnings)) firstRow = warningRow;
                TooltipHandler.TipRegion(alert, warnings + " offscreen pawns need attention. Show the first hidden pawn with injury, mood risk, mental state, burning, downed or dead status.");
            }
            Rect toggle = new Rect(footer.xMax - 22f * s, footer.y, 22f * s, footer.height);
            if (frame.CanExpand)
            {
                if (IconButton(toggle, false, s)) expanded = !expanded;
                TooltipHandler.TipRegion(toggle, expanded ? "Collapse pawn bar to " + frame.CollapsedRows +
                    (frame.CollapsedRows == 1 ? " row." : " rows.") : "Expand pawn bar to show up to four rows.");
            }
        }
        private static bool IconButton(Rect rect, bool alert, float s, int count = 0)
        {
            Theme.ButtonFace(rect, Mouse.IsOver(rect) && GUI.enabled ? Theme.Hover : Theme.Surface, !GUI.enabled);
            Color ink = alert ? Theme.Rgb(0xD9B16F) : Mouse.IsOver(rect) ? Theme.Ink : Theme.Muted;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = ink;
                GUI.DrawTexture(new Rect(rect.x + (alert ? 3f : 2f) * s, rect.center.y - 9f * s, 18f * s, 18f * s),
                    alert ? PawnBarIcons.Warning : expanded ? PawnBarIcons.Collapse : PawnBarIcons.Expand);
                if (alert)
                {
                    Rect badge = new Rect(rect.x + 25f * s, rect.center.y - 8f * s, rect.width - 28f * s, 16f * s);
                    GUI.color = Color.white;
                    Theme.RoundRect(badge, Theme.Rgb(0x141B20), 3f * s);
                    GUI.color = ink;
                    GUI.Label(badge, count.ToString(), controlStyle);
                }
            }
            GUI.color = Color.white;
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }
        private static bool NeedsAttention(Pawn pawn) => pawn.Dead || pawn.Downed || pawn.InMentalState || pawn.IsBurning() ||
            pawn.health.summaryHealth.SummaryHealthPercent < .999f ||
            Prefs.VisibleMood && pawn.needs?.mood != null && pawn.mindState?.mentalBreaker?.CanDoRandomMentalBreaks == true &&
            MoodThresholdExtensions.CurrentMoodThresholdFor(pawn) != MoodThreshold.None;
        internal static void SetAuditView(bool open, int row) { expanded = open; firstRow = Mathf.Max(0, row); }
        internal static bool Expanded => expanded;
        internal static int FirstRow => firstRow;
        internal static void QueueSelectionOverlay(Pawn pawn, Rect localRect)
        {
            if (pawn == null) return;
            float unit = Find.ColonistBar.Scale;
            // Both the card renderer and intercepted native selection call pass
            // the native bracket rect, expanded 2 units beyond the actual card.
            Rect card = localRect.ContractedBy(2f * unit);
            SelectionOverlays.Add(new SelectionOverlay
            {
                Rect = new Rect(UI.GUIToScreenPoint(card.position), card.size),
                Alpha = GUI.color.a, Unit = unit
            });
        }
        private static void DrawSelectionOverlays()
        {
            if (Event.current.type != EventType.Repaint) return;
            // Preserve queued root coordinates while clipping the inset treatment
            // with the same animated viewport used by cards and native hit queries.
            GUI.BeginClip(selectionViewport, -selectionViewport.position, Vector2.zero, false);
            try
            {
                foreach (var overlay in SelectionOverlays)
                    using (new Theme.GuiScope(true))
                    {
                        // Inferred selected state: a continuous rounded light edge,
                        // wholly inside the card. Border-only quads keep the mood,
                        // portrait, name and red health strip colors untouched.
                        GUI.color = Color.white;
                        float unit = overlay.Unit;
                        Rect border = overlay.Rect.ContractedBy(.6f * unit);
                        float radius = 2.4f * unit;
                        Color separator = Theme.Rgb(0x0B141A);
                        separator.a = .8f * overlay.Alpha;
                        GUI.DrawTexture(border, BaseContent.WhiteTex, ScaleMode.StretchToFill,
                            true, 0f, separator, 1.4f * unit, radius);
                        Color ink = Theme.Ink;
                        ink.a = .95f * overlay.Alpha;
                        GUI.DrawTexture(border, BaseContent.WhiteTex, ScaleMode.StretchToFill,
                            true, 0f, ink, 1f * unit, radius);
                    }
            }
            finally { GUI.EndClip(); }
        }
        private static bool ShowsWeapon(Pawn pawn)
        {
            ThingWithComps weapon = pawn.equipment?.Primary;
            return weapon != null && weapon.def.IsWeapon && (Prefs.ShowWeaponsUnderPortraitMode == ShowWeaponsUnderPortraitMode.Always ||
                (Prefs.ShowWeaponsUnderPortraitMode == ShowWeaponsUnderPortraitMode.WhileDrafted && pawn.Drafted));
        }
        internal static bool TryHit(Vector2 pos, out ColonistBar.Entry entry)
        {
            entry = default;
            if (!TryFrame(out Frame frame) || Find.WindowStack.AnyWindowAbsorbingAllInput || !Viewport(frame).Contains(pos)) return false;
            foreach (var slot in frame.Layout.Cards)
                if (Visible(frame, slot.Y) && ScreenCard(frame, slot).Contains(pos)) { entry = frame.Entries[slot.Index]; return true; }
            return false;
        }
        internal static List<Thing> InRect(Rect rect)
        {
            var result = new List<Thing>();
            if (!TryFrame(out Frame frame) || Find.WindowStack.AnyWindowAbsorbingAllInput) return result;
            Rect viewport = Viewport(frame);
            if (!viewport.Overlaps(rect)) return result;
            Rect clipped = Rect.MinMaxRect(Mathf.Max(rect.xMin, viewport.xMin), Mathf.Max(rect.yMin, viewport.yMin),
                Mathf.Min(rect.xMax, viewport.xMax), Mathf.Min(rect.yMax, viewport.yMax));
            var hits = frame.Layout.Cards.FindAll(slot => Visible(frame, slot.Y) && ScreenCard(frame, slot).Overlaps(clipped));
            if (WorldRendererUtility.WorldSelected && hits.Exists(slot => frame.Entries[slot.Index].map == null))
                hits.RemoveAll(slot => frame.Entries[slot.Index].map != null);
            else if (hits.Exists(slot => frame.Entries[slot.Index].map == Find.CurrentMap))
                hits.RemoveAll(slot => frame.Entries[slot.Index].map != Find.CurrentMap);
            foreach (var slot in hits)
            {
                Pawn pawn = frame.Entries[slot.Index].pawn;
                result.Add(pawn.Dead && pawn.Corpse != null && pawn.Corpse.SpawnedOrAnyParentSpawned ? (Thing)pawn.Corpse : pawn);
            }
            return result;
        }
        internal static Dictionary<string, object> AnimationAuditState()
        {
            bool ready = TryFrame(out Frame frame);
            var state = new Dictionary<string, object>
            {
                ["ready"] = ready, ["progress"] = animationProgress, ["target"] = animationTarget,
                ["expanded"] = expanded, ["durationSeconds"] = AnimationDuration,
                ["easedProgress"] = Mathf.SmoothStep(0, 1, animationProgress), ["firstRow"] = firstRow
            };
            if (!ready) return state;
            Rect viewport = Viewport(frame);
            state["height"] = frame.Bounds.height;
            state["contentHeight"] = frame.ContentHeight;
            state["targetContentHeight"] = frame.TargetContentHeight;
            state["closedContentHeight"] = frame.ClosedContentHeight;
            state["openContentHeight"] = frame.OpenContentHeight;
            state["targetHeight"] = frame.TargetContentHeight + (16f + (frame.Overflow ? 28f : 0f)) * frame.Unit;
            state["targetRows"] = frame.VisibleRows;
            state["revealedRows"] = (frame.ContentHeight / frame.Unit + PawnBarGeometry.RowGap) / frame.Layout.RowPitch;
            state["rowCount"] = frame.Layout.RowCount;
            state["rowPitch"] = frame.Layout.RowPitch * frame.Unit;
            state["bounds"] = new Dictionary<string, object> { ["x"] = frame.Bounds.x, ["y"] = frame.Bounds.y,
                ["width"] = frame.Bounds.width, ["height"] = frame.Bounds.height };
            state["viewport"] = new Dictionary<string, object> { ["x"] = viewport.x, ["y"] = viewport.y,
                ["width"] = viewport.width, ["height"] = viewport.height };
            return state;
        }
        private sealed class Frame
        {
            internal Rect Bounds;
            internal float Unit, ContentHeight;
            internal float ClosedContentHeight, OpenContentHeight, TargetContentHeight;
            internal int FirstRow, VisibleRows;
            internal bool Overflow;
            internal bool CanExpand;
            internal int CollapsedRows;
            internal bool NativeGroups;
            internal string[] Headings;
            internal PawnBarGeometry.Layout Layout;
            internal List<ColonistBar.Entry> Entries;
        }
        private sealed class SelectionOverlay
        { internal Rect Rect; internal float Alpha, Unit; }
    }

    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.GetEntryRectAlpha))]
    internal static class ColonistGroupAlpha
    {
        private static void Prefix(ref Rect rect)
        { if (ColonistGroupsHud.Drawing) rect.position = UI.GUIToScreenPoint(rect.position); }
    }
    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.TryGetEntryAt))]
    internal static class ColonistGroupHitTest
    {
        private static bool Prefix(Vector2 pos, out ColonistBar.Entry entry, ref bool __result)
        {
            entry = default;
            if (!ColonistGroupsHud.TryGetOccupiedBounds(out _)) return true;
            __result = ColonistGroupsHud.TryHit(pos, out entry);
            return false;
        }
    }
    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistsOrCorpsesInScreenRect))]
    internal static class ColonistGroupScreenRect
    {
        private static bool Prefix(Rect rect, ref List<Thing> __result)
        {
            if (!ColonistGroupsHud.TryGetOccupiedBounds(out _)) return true;
            __result = ColonistGroupsHud.InRect(rect);
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    internal static class ToolsPanel
    {
        private const float Width = 238f, Height = 490f, Padding = 12f, Row = 26f;
        private static readonly ToolsPanelMotion motion = new ToolsPanelMotion();
        private static readonly ToolsSearchService search = new ToolsSearchService();
        private static bool focusSearch, releaseFocus;
        private static string page = "main", outgoingPage = "main", lastReason = "initial", hoveredControl = "";
        private static string focusedControl = "";
        private static int keyboardControl;
        private static GUIStyle searchStyle, bodyStyle, headingStyle;
        private static Rect panel, shell, launcher;
        private static Vector2 searchScroll;
        private static Map lastMap;
        private static MainButtonDef lastTab;
        private static bool lastWorld;
        private static double Now => Time.realtimeSinceStartup;
        internal static bool IsOpen => motion.Open;
        internal static bool OwnSearchFocused => motion.Open && page == "search" && focusedControl == "Reframed.ToolsSearch" &&
            Current.ProgramState == ProgramState.Playing && !TutorSystem.TutorialMode &&
            !RimWorld.Planet.WorldRendererUtility.WorldSelected && Find.CurrentMap != null && Find.CurrentMap == lastMap &&
            Find.MainTabsRoot != null && Find.MainTabsRoot.OpenTab == lastTab &&
            Find.WindowStack != null && !Find.WindowStack.AnyWindowAbsorbingAllInput;
        private static bool Interactive => motion.Open && motion.Shell == 1f && motion.Settled;
        internal static bool OccludesMapPointer => EscMenuPanel.OccludesMapPointer || ReframedMod.Active && ReframedMod.Settings.CompactTools &&
            Current.ProgramState == ProgramState.Playing && !TutorSystem.TutorialMode &&
            (Find.CurrentMap != null || RimWorld.Planet.WorldRendererUtility.WorldSelected) &&
            (Mouse.IsOver(HudLayout.ToolsButtonBounds) || motion.Shell > 0f && Mouse.IsOver(shell));

        internal static void Prepare(bool eligible)
        {
            Map currentMap = Find.CurrentMap;
            bool world = RimWorld.Planet.WorldRendererUtility.WorldSelected;
            MainButtonDef currentTab = Find.MainTabsRoot?.OpenTab;
            bool obstructed = Find.WindowStack != null && Find.WindowStack.AnyWindowAbsorbingAllInput;
            if (eligible && currentMap == lastMap && !obstructed && currentTab != lastTab && currentTab != null)
                Open(false, "native-tab-opened");
            else if (!eligible || currentMap != lastMap || world != lastWorld || obstructed)
            {
                motion.Reset();
                search.Reset();
                focusSearch = false;
                focusedControl = "";
                keyboardControl = 0;
                page = outgoingPage = "main";
                searchScroll = Vector2.zero;
                // Only relinquish a focus that this surface owns.
                if (GUI.GetNameOfFocusedControl() == "Reframed.ToolsSearch") GUI.FocusControl(null);
                lastReason = "context-reset";
            }
            lastMap = currentMap;
            lastWorld = world;
            lastTab = currentTab;
        }

        private static void Open(bool value, string reason)
        {
            if (value && EscMenuPanel.BlocksTools) return;
            motion.SetOpen(value, Now);
            lastReason = reason;
            if (!value) releaseFocus = true;
            else if (motion.Shell == 0f) { page = outgoingPage = "main"; motion.ResetSubmenu(); }
        }

        private static void Navigate(string target)
        {
            if (target != "main" && target != "search" && target != "more") return;
            // Only main-to-submenu and back are needed; queueing a second page
            // behind moving controls would make their click identities unstable.
            if (!motion.Settled) return;
            if (target == page) return;
            outgoingPage = page;
            page = target;
            motion.SetSubmenu(target != "main", Now);
            focusSearch = target == "search";
            if (target != "search") releaseFocus = true;
            lastReason = "navigate:" + target;
        }

        internal static void Draw(WidgetRow nativeRow, bool worldView, bool hotkeyContext,
            KeyBindingDef shortcut, string shortcutLabel, ref float curBaseY)
        {
            if (Event.current.type == EventType.Layout) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                if (releaseFocus)
                {
                    if (GUI.GetNameOfFocusedControl() == "Reframed.ToolsSearch") GUI.FocusControl(null);
                    releaseFocus = false;
                }
                if (EscMenuPanel.BlocksTools)
                {
                    if (motion.Open) Open(false, "esc-covers-tools");
                    motion.Tick(Now);
                    hoveredControl = "";
                    // No glyph, tooltip, shortcut or hit target under the Esc panel.
                    return;
                }
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.MiddleLeft;
                launcher = HudLayout.ToolsButtonBounds;
                float panelHeight = worldView ? 382 : Height;
                panel = new Rect(launcher.xMax - Width, Mathf.Max(5f, launcher.yMax - panelHeight), Width,
                    Mathf.Min(panelHeight, launcher.yMax - 5f));
                bool menuOwnsLauncher = EscMenuPanel.OwnsWindow;
                bool blocked = Find.WindowStack.AnyWindowAbsorbingAllInput || menuOwnsLauncher;
                GUI.enabled = GUI.enabled && !blocked;
                motion.Tick(Now);
                bool overLauncher = Mouse.IsOver(launcher);
                if (!blocked)
                {
                    if (!worldView) ProcessNativeKeys();
                    else if (GUIUtility.keyboardControl == 0 && Event.current.type == EventType.KeyDown && KeyBindingDefOf.OpenMapSearch.JustPressed)
                    { Event.current.Use(); Open(false, "world-search-shortcut"); OpenWorldSearch(); }
                    if (hotkeyContext && shortcut != null && shortcut.KeyDownEvent)
                    { Open(!motion.Open, "shortcut"); Event.current.Use(); }
                    if (motion.Open && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        if (page != "main" && motion.Settled) Navigate("main");
                        else if (page == "main") Open(false, "escape");
                        Event.current.Use();
                    }
                }
                float growth = ToolsPanelMotion.Ease(motion.Shell);
                shell = new Rect(launcher.xMax - Mathf.Lerp(launcher.width, panel.width, growth),
                    launcher.yMax - Mathf.Lerp(launcher.height, panel.height, growth),
                    Mathf.Lerp(launcher.width, panel.width, growth), Mathf.Lerp(launcher.height, panel.height, growth));
                bool inside = Mouse.IsOver(shell) || overLauncher;
                // A small margin keeps clicks on the panel edge inside it.
                inside |= Mouse.IsOver(new Rect(shell.x - 5f, shell.y - 5f, shell.width + 10f, shell.height + 10f));
                if (!blocked && motion.Open && Event.current.type == EventType.MouseDown && !inside)
                {
                    Open(false, "outside-click");
                    // The sibling launcher gets this same click; map clicks still stop here.
                    if (!Mouse.IsOver(HudLayout.MenuButtonBounds)) Event.current.Use();
                }
                hoveredControl = "";
                if (motion.Shell > 0f)
                {
                    Theme.Panel(shell);
                    Rect body = new Rect(panel.x + Padding, panel.y + Padding, panel.width - Padding * 2,
                        panel.height - launcher.height - Padding * 2 - 3f);
                    // Content owns a bounded clip at its final position. The shell
                    // grows/retracts; contents are never squashed or click-active
                    // while either shell or page is moving.
                    Rect clip = Intersect(shell.ContractedBy(1f), body);
                    if (clip.width > 0f && clip.height > 0f)
                    {
                        GUI.BeginClip(clip);
                        try
                        {
                            Rect local = new Rect(body.x - clip.x, body.y - clip.y, body.width, body.height);
                            DrawPages(local, nativeRow, worldView);
                        }
                        finally { GUI.EndClip(); }
                    }
                    curBaseY = Mathf.Min(curBaseY, shell.y - 4f);
                }
                // Keep the toggle above the shell and outside the page-content
                // clip, so it can reverse either animation without hitting a row.
                HudControls.Face(launcher, !blocked && overLauncher, motion.Open, blocked);
                HudControls.ToolsGlyph(launcher, blocked, motion.Open);
                HudControls.Shortcut(launcher, shortcutLabel, !hotkeyContext);
                if (!menuOwnsLauncher) TooltipHandler.TipRegion(launcher, (motion.Open ? "Close tools" : "Open tools") +
                    (string.IsNullOrEmpty(shortcutLabel) ? "" : " (" + shortcutLabel +
                    ")\nShortcut available with no selection, open tab or active designator."));
                if (!blocked && Widgets.ButtonInvisible(launcher))
                { Open(!motion.Open, "launcher-click"); }
                if (!menuOwnsLauncher)
                {
                    if (motion.Shell > 0f) GenUI.AbsorbClicksInRect(shell);
                    GenUI.AbsorbClicksInRect(launcher);
                }
                curBaseY = Mathf.Min(curBaseY, launcher.y - 4f);
                // An outgoing TextField can reclaim its control during the Back
                // slide. Release only our own focus after that field stops drawing.
                if (page != "search" && motion.Settled && GUI.GetNameOfFocusedControl() == "Reframed.ToolsSearch")
                {
                    GUI.FocusControl(null);
                    GUIUtility.keyboardControl = 0;
                }
                // Audit requests also run from end-of-frame capture coroutines;
                // retain GUI-only focus data while actually inside OnGUI.
                keyboardControl = GUIUtility.keyboardControl;
                focusedControl = GUI.GetNameOfFocusedControl();
            }
        }

        private static Rect Intersect(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
            Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));

        private static void DrawPages(Rect body, WidgetRow nativeRow, bool worldView)
        {
            float slide = ToolsPanelMotion.Ease(motion.Slide);
            string sub = page != "main" ? page : outgoingPage;
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = GUI.enabled && Interactive;
                if (slide < 1f)
                {
                    Rect main = new Rect(body.x - body.width * slide, body.y, body.width, body.height);
                    if (worldView) DrawWorld(main); else DrawMain(main);
                }
                if (slide > 0f && sub != "main")
                {
                    Rect target = new Rect(body.x + body.width * (1f - slide), body.y, body.width, body.height);
                    if (sub == "search") DrawSearch(target);
                    else DrawMore(target, nativeRow, worldView);
                }
            }
        }

        private static void DrawMain(Rect body)
        {
            float x = body.x, y = body.y, w = body.width;
            HeadingLabel(new Rect(x + 2f, y, w, 20f), "TOOLS", Theme.Ink); y += 22f;
            Rect entry = new Rect(x, y, w, 26f);
            SearchFace(entry);
            BodyLabel(new Rect(x + 28f, y, w - 32f, 26f), "Search", Theme.Muted);
            TooltipHandler.TipRegion(entry, "Search revealed things on this map");
            if (GUI.enabled && Widgets.ButtonInvisible(entry)) Navigate("search");
            y += 34f;
            float half = (w - 4f) / 2;
            bool codex = ModsConfig.AnomalyActive && Find.Anomaly != null && Find.Anomaly.AnomalyStudyEnabled;
            if (Control(new Rect(x, y, half, Row), "codex", "Codex", false, codex,
                codex ? "Open the native entity codex" : "Entity codex requires Anomaly study to be available."))
            { Open(false, "codex"); Find.WindowStack.Add(new Dialog_EntityCodex(null)); }
            Control(new Rect(x + half + 4f, y, half, Row), "compare", "Compare", false, false,
                "Item comparison is not available yet.");
            y += Row + 12f;
            Heading(x, ref y, w, "GAMEPLAY");
            Toggle(new Rect(x, y, w, Row), "repair", "Rebuild destroyed buildings", ref Find.PlaySettings.autoRebuild,
                "Automatically rebuild destroyed player buildings."); y += Row + 4f;
            Toggle(new Rect(x, y, w, Row), "roof", "Automatic home area", ref Find.PlaySettings.autoHomeArea,
                "Automatically expand the home area around new buildings. Roof areas control roofing separately."); y += Row + 12f;
            Heading(x, ref y, w, "INTERFACE");
            bool colonists = Find.PlaySettings.showColonistBar;
            if (Control(new Rect(x, y, half, Row), "colonists", "Colonists", colonists, true, "Show the colonist bar"))
            { Find.PlaySettings.showColonistBar = !colonists; Find.ColonistBar.MarkColonistsDirty(); ToggleSound(!colonists); }
            Toggle(new Rect(x + half + 4f, y, half, Row), "helper", "Helper", ref Find.PlaySettings.showLearningHelper,
                "Show the learning helper even when no lessons are active. Active lessons remain visible."); y += Row + 4f;
            if (Control(new Rect(x, y, w, Row), "resources", "Categorized resources", Prefs.ResourceReadoutCategorized, true,
                "Group resources by category"))
            { Prefs.ResourceReadoutCategorized = !Prefs.ResourceReadoutCategorized; ToggleSound(Prefs.ResourceReadoutCategorized); }
            y += Row + 12f;
            Heading(x, ref y, w, "OVERLAYS");
            Toggle(new Rect(x, y, half, Row), "beauty", "Beauty", ref Find.PlaySettings.showBeauty, "Show beauty");
            Toggle(new Rect(x + half + 4f, y, half, Row), "heat", "Heat", ref Find.PlaySettings.showTemperatureOverlay, "Show temperature"); y += Row + 4f;
            Toggle(new Rect(x, y, half, Row), "pollution", "Pollution", ref Find.PlaySettings.showPollutionOverlay,
                ModsConfig.BiotechActive ? "Show pollution" : "Requires Biotech", ModsConfig.BiotechActive);
            Toggle(new Rect(x + half + 4f, y, half, Row), "terrain", "Terrain", ref Find.PlaySettings.showTerrainAffordanceOverlay, "Show terrain affordance"); y += Row + 4f;
            Toggle(new Rect(x, y, half, Row), "fertility", "Fertility", ref Find.PlaySettings.showFertilityOverlay, "Show fertility");
            Toggle(new Rect(x + half + 4f, y, half, Row), "zones", "Zones", ref Find.PlaySettings.showZones, "Show zones"); y += Row + 4f;
            Toggle(new Rect(x, y, half, Row), "roofs", "Roofs", ref Find.PlaySettings.showRoofOverlay, "Show roofs");
            Toggle(new Rect(x + half + 4f, y, half, Row), "roomstats", "Room stats", ref Find.PlaySettings.showRoomStats, "Show room statistics"); y += Row + 12f;
            if (Control(new Rect(x, y, w, 24f), "more", "More tools", false, true, "Additional map controls")) Navigate("more");
        }

        private static void OpenWorldSearch()
        {
            var window = new Dialog_WorldSearch();
            BottomMenuChildren.RegisterWorldTool(window);
            Find.WindowStack.Add(window);
        }
        private static void DrawWorld(Rect body)
        {
            float y = body.y, x = body.x, width = body.width;
            HeadingLabel(new Rect(x + 2, y, width, 20), "WORLD TOOLS", Theme.Ink); y += 26;
            if (Control(new Rect(x, y, width, Row), "search", "Search world", false, true, "Search locations on the world map"))
            { Open(false, "world-search"); OpenWorldSearch(); }
            y += 36;
            bool colonists = Find.PlaySettings.showColonistBar;
            if (Control(new Rect(x, y, width, Row), "colonists", "Colonists", colonists, true, "Show colonists and caravans"))
            { Find.PlaySettings.showColonistBar = !colonists; Find.ColonistBar.MarkColonistsDirty(); ToggleSound(!colonists); }
            y += Row + 4;
            bool north = Find.PlaySettings.lockNorthUp;
            Toggle(new Rect(x, y, width, Row), "terrain", "Keep north up", ref Find.PlaySettings.lockNorthUp, "Lock the world camera to north");
            if (north != Find.PlaySettings.lockNorthUp && Find.PlaySettings.lockNorthUp) Find.WorldCameraDriver.RotateSoNorthIsUp();
            y += Row + 4;
            Toggle(new Rect(x, y, width, Row), "zones", "Important locations", ref Find.PlaySettings.showImportantExpandingIcons, "Show important locations"); y += Row + 4;
            Toggle(new Rect(x, y, width, Row), "roofs", "Faction settlements", ref Find.PlaySettings.showBasesExpandingIcons, "Show faction settlements"); y += Row + 4;
            if (ModsConfig.OdysseyActive)
            { Toggle(new Rect(x, y, width, Row), "roomstats", "Landmarks", ref Find.PlaySettings.showExpandingLandmarks, "Show landmark icons"); y += Row + 4; }
            Toggle(new Rect(x, y, width, Row), "heat", "Day and night", ref Find.PlaySettings.usePlanetDayNightSystem, "Show the planet day and night cycle"); y += Row + 4;
            Toggle(new Rect(x, y, width, Row), "terrain", "World features", ref Find.PlaySettings.showWorldFeatures, "Show named world features"); y += Row + 4;
            if (Control(new Rect(x, y, width, 24), "more", "More tools", false, true, "Native world controls and compatible extensions")) Navigate("more");
        }

        private static void Heading(float x, ref float y, float width, string label)
        {
            HeadingLabel(new Rect(x + 2f, y, width, 16f), label, Theme.Muted);
            y += 16f;
        }

        private static void EnsureStyles()
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                if (bodyStyle != null && bodyStyle.font == Text.CurFontStyle.font) return;
                bodyStyle = new GUIStyle(Text.CurFontStyle) { fontSize = 12, alignment = TextAnchor.MiddleLeft,
                    wordWrap = false, richText = false, clipping = TextClipping.Clip,
                    padding = new RectOffset(), margin = new RectOffset() };
                bodyStyle.normal.textColor = Color.white;
                headingStyle = new GUIStyle(bodyStyle) { fontSize = 9, fontStyle = FontStyle.Bold };
            }
        }

        private static void BodyLabel(Rect rect, string value, Color color)
        {
            EnsureStyles();
            using (new Theme.GuiScope(true)) { GUI.color = color; GUI.Label(rect, value, bodyStyle); }
        }

        private static void HeadingLabel(Rect rect, string value, Color color)
        {
            EnsureStyles();
            using (new Theme.GuiScope(true))
            {
                GUI.color = color;
                float x = rect.x;
                foreach (char character in value)
                {
                    string glyph = character.ToString();
                    float width = headingStyle.CalcSize(new GUIContent(glyph)).x;
                    if (x + width > rect.xMax) break;
                    GUI.Label(new Rect(x, rect.y, width + 1f, rect.height), glyph, headingStyle);
                    x += width + .65f;
                }
            }
        }

        private static bool Control(Rect rect, string icon, string label, bool selected, bool available, string tip)
        {
            using (new Theme.GuiScope(true))
            {
                bool enabled = GUI.enabled && available;
                bool hover = enabled && Mouse.IsOver(rect);
                if (hover) hoveredControl = icon;
                if (selected)
                {
                    Theme.RoundRect(rect, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, enabled ? .8f : .35f), 3f);
                    Theme.RoundRect(rect.ContractedBy(.8f), Theme.Selected, 2.3f);
                }
                else Theme.ButtonFace(rect, hover ? Theme.Hover : Theme.Surface, !enabled, radius: 3f);
                GUI.color = enabled ? (selected ? Theme.Ink : Theme.Muted) : new Color(Theme.Muted.r, Theme.Muted.g, Theme.Muted.b, .45f);
                Texture2D texture = ToolsIcons.Get(icon);
                if (texture != null) GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + (rect.height - 15f) / 2f, 15f, 15f), texture);
                GUI.color = enabled ? Theme.Ink : new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, .45f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                if (label.Length > 0) BodyLabel(new Rect(rect.x + 27f, rect.y, rect.width - 30f, rect.height), label, GUI.color);
                TooltipHandler.TipRegion(rect, tip);
                GUI.enabled = enabled;
                return enabled && Widgets.ButtonInvisible(rect);
            }
        }

        private static void Toggle(Rect rect, string icon, string label, ref bool value, string tooltip, bool enabled = true)
        { if (Control(rect, icon, label, value, enabled, tooltip)) { value = !value; ToggleSound(value); } }

        private static void ToggleSound(bool value) => (value ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();

        private static void ProcessNativeKeys()
        {
            if (!ModsConfig.OdysseyActive || Find.CurrentMap == null || !Find.CurrentMap.Biome.inVacuum)
                Find.PlaySettings.showVacuumOverlay = false;
            if (GUIUtility.keyboardControl != 0 || Event.current.type != EventType.KeyDown) return;
            if (KeyBindingDefOf.ToggleBeautyDisplay.KeyDownEvent)
            {
                Find.PlaySettings.showBeauty = !Find.PlaySettings.showBeauty;
                ToggleSound(Find.PlaySettings.showBeauty); Event.current.Use();
            }
            else if (KeyBindingDefOf.ToggleRoomStatsDisplay.KeyDownEvent)
            {
                Find.PlaySettings.showRoomStats = !Find.PlaySettings.showRoomStats;
                ToggleSound(Find.PlaySettings.showRoomStats); Event.current.Use();
            }
            else if (KeyBindingDefOf.OpenMapSearch.JustPressed && Find.CurrentMap != null)
            {
                Event.current.Use(); Open(false, "native-search-shortcut");
                Find.WindowStack.Add(new Dialog_MapSearch(Find.CurrentMap));
            }
        }

        private static void SearchFace(Rect input)
        {
            Theme.RoundRect(input, Theme.Border, 3f);
            Theme.RoundRect(input.ContractedBy(1f), Theme.Rgb(DesignTokens.Deep), 2.3f);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Theme.Muted;
                Texture2D magnifier = ToolsIcons.Get("search");
                if (magnifier != null) GUI.DrawTexture(new Rect(input.x + 6f, input.y + (input.height - 15f) / 2f, 15f, 15f), magnifier);
            }
        }

        private static void SubHeader(Rect body, string label)
        {
            if (Control(new Rect(body.x, body.y, 28f, 24f), "back", "", false, true, "Back to tools (Escape)")) Navigate("main");
            HeadingLabel(new Rect(body.x + 35f, body.y, body.width - 35f, 24f), label, Theme.Ink);
        }

        private static void DrawSearch(Rect body)
        {
            SubHeader(body, "SEARCH");
            Rect input = new Rect(body.x, body.y + 31f, body.width, 29f);
            SearchFace(input);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (searchStyle == null || searchStyle.font != Text.CurFontStyle.font)
            {
                searchStyle = new GUIStyle(GUI.skin.textField) { font = Text.CurFontStyle.font,
                    fontSize = 12, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(), border = new RectOffset() };
                foreach (GUIStyleState state in new[] { searchStyle.normal, searchStyle.hover, searchStyle.active,
                    searchStyle.focused, searchStyle.onNormal, searchStyle.onHover, searchStyle.onActive, searchStyle.onFocused })
                { state.background = null; state.textColor = Theme.Ink; }
            }
            GUI.SetNextControlName("Reframed.ToolsSearch");
            string query = GUI.TextField(new Rect(input.x + 24f, input.y + 2f, input.width - 51f, input.height - 4f), search.Query, 128, searchStyle);
            if (query != search.Query) { search.SetQuery(query, Now); searchScroll = Vector2.zero; }
            if (Control(new Rect(input.xMax - 25f, input.y + 2f, 23f, input.height - 4f), "clear", "", false,
                search.Query.Length > 0, "Clear search"))
            { search.SetQuery("", Now); searchScroll = Vector2.zero; focusSearch = true; }
            if (focusSearch && Interactive && Event.current.type == EventType.Repaint)
            { GUI.FocusControl("Reframed.ToolsSearch"); focusSearch = false; }
            search.Refresh(Find.CurrentMap, Now);
            Rect count = new Rect(body.x + 2f, input.yMax + 3f, body.width - 4f, 19f);
            BodyLabel(count, search.Pending ? "Searching…" : search.Results.Count + " results", Theme.Muted);
            Rect viewport = new Rect(body.x, count.yMax + 2f, body.width, body.yMax - count.yMax - 3f);
            if (search.Query.Trim().Length == 0 || search.Results.Count == 0)
            {
                BodyLabel(new Rect(viewport.x + 5f, viewport.y + 8f, viewport.width - 10f, 26f),
                    search.Query.Trim().Length == 0 ? "Find items, buildings and pawns" : search.Pending ? "" : "No matching things", Theme.Muted);
                return;
            }
            const float resultHeight = 45f;
            float contentHeight = Math.Max(viewport.height, search.Results.Count * resultHeight);
            searchScroll = GUI.BeginScrollView(viewport, searchScroll, new Rect(0, 0, viewport.width - 12f, contentHeight),
                false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                int first = Math.Max(0, (int)(searchScroll.y / resultHeight));
                int last = Math.Min(search.Results.Count, (int)((searchScroll.y + viewport.height) / resultHeight) + 1);
                for (int i = first; i < last; i++)
                {
                    Thing thing = search.Results[i];
                    Rect rect = new Rect(0, i * resultHeight, viewport.width - 12f, resultHeight - 2f);
                    bool valid = ToolsSearchService.Available(thing, Find.CurrentMap);
                    bool fullRow = ToolsPanelMotion.RowInteractive(Interactive, rect.y, rect.yMax, searchScroll.y, searchScroll.y + viewport.height - 7f);
                    if (valid && fullRow && Mouse.IsOver(rect)) Theme.RoundRect(rect, Theme.Hover, 3f);
                    if (!valid) continue;
                    Widgets.ThingIcon(new Rect(rect.x + 3f, rect.y + 6f, 30f, 30f), thing);
                    using (new Theme.GuiScope(true))
                    {
                        GUI.color = Theme.Ink;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        BodyLabel(new Rect(39f, rect.y + 1f, rect.width - 42f, 22f), thing.LabelNoCount.CapitalizeFirst(), Theme.Ink);
                        BodyLabel(new Rect(39f, rect.y + 22f, rect.width - 42f, 19f), ToolsSearchService.Detail(thing),
                            thing.def.useHitPoints && thing.HitPoints < thing.MaxHitPoints ? Theme.Danger : Theme.Muted);
                    }
                    if (GUI.enabled && fullRow && Widgets.ButtonInvisible(rect))
                    {
                        // Validate the retained identity again immediately before
                        // native selection, including its current map and fog.
                        if (ToolsSearchService.Available(thing, Find.CurrentMap))
                        { Open(false, "search-select:" + thing.ThingID); CameraJumper.TryJumpAndSelect(thing); }
                    }
                }
            }
            finally { GUI.EndScrollView(); }
            if (contentHeight > viewport.height)
            {
                Rect rail = new Rect(viewport.xMax - 5f, viewport.y + 2f, 3f, viewport.height - 4f);
                Theme.RoundRect(rail, Theme.Surface, 1f);
                float thumb = Math.Max(18f, rail.height * viewport.height / contentHeight);
                float offset = Mathf.Clamp01(searchScroll.y / (contentHeight - viewport.height)) * (rail.height - thumb);
                Theme.RoundRect(new Rect(rail.x, rail.y + offset, rail.width, thumb), Theme.Muted, 1f);
            }
            // Quiet bottom fade is paint-only; the covered rows cannot activate.
            for (int i = 0; i < 7; i++)
            {
                Color fade = Theme.PanelFill; fade.a *= (i + 1f) / 7f;
                Theme.RoundRect(new Rect(viewport.x, viewport.yMax - 7f + i, viewport.width, 1f), fade, 0f);
            }
        }

        private static void DrawMore(Rect body, WidgetRow nativeRow, bool worldView)
        {
            SubHeader(body, "MORE TOOLS");
            BodyLabel(new Rect(body.x + 2f, body.y + 30f, body.width - 4f, 36f), worldView ? "Additional world controls" : "Additional map controls", Theme.Muted);
            // This preserves DLC/context/keyboard behaviors of the installed
            // native toolbar, including controls absent from the concept.
            nativeRow.Init(body.x + 5f, body.y + 76f, UIDirection.RightThenDown, body.width - 10f);
            // Native key handlers do not honor GUI.enabled. During a slide only
            // paint the toolbar; the central map dispatcher owns keyboard input.
            if (Interactive || Event.current.type == EventType.Repaint)
                Find.PlaySettings.DoPlaySettingsGlobalControls(nativeRow, worldView);
            BodyLabel(new Rect(body.x + 2f, body.y + 177f, body.width - 4f, 35f), "Hover an icon for details", Theme.Muted);
            if (Control(new Rect(body.x, body.y + 222f, body.width, 27f), "search", worldView ? "Search world" : "Advanced map search", false, worldView || Find.CurrentMap != null,
                "Open RimWorld's complete native search dialog"))
            { Open(false, "native-search"); if (worldView) OpenWorldSearch(); else Find.WindowStack.Add(new Dialog_MapSearch(Find.CurrentMap)); }
        }

        internal static Dictionary<string, object> AnimationAuditState() => new Dictionary<string, object>
        {
            ["shellProgress"] = motion.Shell, ["shellTarget"] = motion.Open ? 1f : 0f,
            ["slideProgress"] = motion.Slide, ["slideTarget"] = motion.Submenu ? 1f : 0f,
            ["shell"] = Bounds(shell), ["panel"] = Bounds(panel), ["launcher"] = Bounds(launcher),
            ["page"] = page, ["fromPage"] = outgoingPage, ["toPage"] = page,
            ["query"] = search.Query, ["resultCount"] = search.Results.Count,
            ["keyboardControl"] = keyboardControl, ["focusedControl"] = focusedControl,
            ["reason"] = lastReason, ["hoveredControl"] = hoveredControl, ["pinned"] = motion.Open,
            ["launcherVisible"] = true, ["launcherPressed"] = motion.Open, ["openingTrigger"] = "click-or-shortcut",
            ["interactive"] = Interactive, ["searchPending"] = search.Pending
        };
        private static float[] Bounds(Rect value) => new[] { value.x, value.y, value.width, value.height };
        internal static void DiagnosticSetOpen(bool value) => Open(value, "diagnostic");
        internal static void DiagnosticNavigate(string target) => Navigate(target);
        internal static void DiagnosticSetQuery(string value) { search.SetQuery(value, Now); searchScroll = Vector2.zero; }
    }
}

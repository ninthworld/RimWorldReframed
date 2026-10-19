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
using Verse.Sound;

namespace ReframedUI
{
    internal static class ArchitectSeams
    {
        internal static bool GridReady, FaceReady;
        internal static bool Ready => GridReady && FaceReady;
    }

    internal static class ArchitectPanel
    {
        private static readonly FieldInfo Panels = AccessTools.Field(typeof(MainTabWindow_Architect), "desPanelsCached");
        private static readonly FieldInfo Search = AccessTools.Field(typeof(MainTabWindow_Architect), "quickSearchWidget");
        private static readonly FieldInfo SearchControlName = AccessTools.Field(typeof(QuickSearchWidget), "controlName");
        private static readonly FieldInfo Forced = AccessTools.Field(typeof(MainTabWindow_Architect), "forceActivatedCommand");
        private static readonly FieldInfo InitialUnfocus = AccessTools.Field(typeof(MainTabWindow_Architect), "didInitialUnfocus");
        private static readonly MethodInfo Open = AccessTools.Method(typeof(MainTabWindow_Architect), "OpenTab");
        private static readonly MethodInfo Click = AccessTools.Method(typeof(MainTabWindow_Architect), "ClickedCategory");
        private static readonly MethodInfo Cache = AccessTools.Method(typeof(MainTabWindow_Architect), "CacheSearchState");
        private static readonly MethodInfo Highlight = AccessTools.Method(typeof(ArchitectCategoryTab), "ShouldHighLightGizmo");
        private static readonly MethodInfo Lowlight = AccessTools.Method(typeof(ArchitectCategoryTab), "ShouldLowLightGizmo");
        private static readonly FieldInfo HeightFrame = AccessTools.Field(typeof(GizmoGridDrawer), "heightDrawnFrame");
        private static readonly FieldInfo HeightDrawn = AccessTools.Field(typeof(GizmoGridDrawer), "heightDrawn");
        private static Vector2 categoryScroll, gridScroll, infoScroll;
        private static ArchitectCategoryTab lastCategory;
        private static Map lastMap;
        private static MainTabWindow_Architect lastWindow;
        private static string lastQuery = "", selectedLabel = "", hoveredLabel = "";
        private static Rect shell, categories, grid, information, searchRect;
        private static float contentHeight, drawHeight, minGridY, readoutHeight = 300f;
        private static Designator lastInformationDesignator;
        private static int categoryCount, designatorCount;
        private static bool focused, active;
        private static GUIStyle bodyStyle, headingStyle, tileLabelStyle, descriptionStyle, scrollTrack, scrollThumb, searchStyle;
        // True only inside our bounded native grid call. Unrelated gizmos retain native rendering.
        internal static bool DrawingGrid { get; private set; }
        internal static bool DrawingExtras { get; private set; }
        private static bool keyboardGridDispatched;
        private static DrawStyleCategoryDef drawStyleCategory;
        internal static float GridRight(float native) => DrawingGrid ? grid.width - 16f : native;
        internal static float GridBottom(float native) => DrawingGrid ? drawHeight - ArchitectLayout.NativeCardHeight - 12f : native;

        internal static bool SearchFocused(MainTabWindow_Architect window)
            => ((QuickSearchWidget)Search.GetValue(window))?.CurrentlyFocused() == true;

        internal static bool Eligible(Window window) => window != null && window.GetType() == typeof(MainTabWindow_Architect) &&
            ReframedMod.Active && ReframedMod.Settings.BuildMenu && !TutorSystem.TutorialMode &&
            ArchitectSeams.Ready &&
            Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null && !WorldRendererUtility.WorldSelected &&
            ArchitectLayout.Supported(UI.screenWidth, UI.screenHeight);

        internal static Rect Bounds => new Rect(HudLayout.DockLeft,
            ArchitectLayout.Bottom(UI.screenHeight) - ArchitectLayout.PanelHeight(UI.screenHeight),
            ArchitectLayout.Width, ArchitectLayout.PanelHeight(UI.screenHeight));

        internal static void Draw(MainTabWindow_Architect window, Rect rect)
        {
            if (Event.current.type == EventType.Layout) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                active = true; lastWindow = window; shell = window.windowRect;
                bool newMap = lastMap != Find.CurrentMap;
                if (newMap) { categoryScroll = gridScroll = infoScroll = Vector2.zero; lastCategory = null; }
                lastMap = Find.CurrentMap;
                Rect body = new Rect(0, 0, rect.width, rect.height).ContractedBy(ArchitectLayout.Padding);
                Color panelFill = Theme.PanelFill;
                panelFill.a = Math.Min(panelFill.a, .84f);
                if (!ArchitectTransition.Moving)
                    Theme.RoundRect(new Rect(0, 0, rect.width, rect.height), panelFill, 5f);
                categories = new Rect(body.x, body.y + ArchitectLayout.HeaderHeight,
                    ArchitectLayout.CategoryWidth, body.height - ArchitectLayout.HeaderHeight);
                float x = categories.xMax + 14f, width = body.xMax - x;
                searchRect = new Rect(x + 145f, body.y, width - 183f, 27f);
                grid = new Rect(x, categories.y, width, categories.height - ArchitectLayout.InfoHeight - 8f);
                information = new Rect(x, grid.yMax + 8f, width, ArchitectLayout.InfoHeight);
                Theme.RoundRect(categories, Theme.Rgb(DesignTokens.Deep).ToTransparent(.16f), 3f);
                HeadingLabel(new Rect(body.x + 2f, body.y + 2f, 130f, 22f), MainButtonDefOf.Architect.LabelCap.ToString().ToUpperInvariant(), Theme.Ink);
                var search = (QuickSearchWidget)Search.GetValue(window);
                var panels = (List<ArchitectCategoryTab>)Panels.GetValue(window);
                if (panels == null || search == null) return;
                categoryCount = panels.Count;
                ArchitectCategoryTab current = (ArchitectCategoryTab)Open.Invoke(window, null);
                if (KeyBindingDefOf.Accept.KeyDownEvent)
                {
                    if (search.filter.Active && current?.UniqueSearchMatch != null)
                    { Forced.SetValue(window, current.UniqueSearchMatch); Event.current.Use(); }
                    else if (!Verse.Steam.SteamDeck.IsSteamDeck) { window.Close(); Event.current.Use(); return; }
                }
                BodyLabel(new Rect(x + 2f, body.y + 2f, 140f, 22f), current?.def.LabelCap ?? MainButtonDefOf.Architect.LabelCap, Theme.Ink);
                DrawSearch(window, search);
                if (!(bool)InitialUnfocus.GetValue(window))
                { UI.UnfocusCurrentControl(); InitialUnfocus.SetValue(window, true); }
                focused = search.CurrentlyFocused();
                lastQuery = search.filter.Text;
                Rect close = new Rect(body.xMax - 24f, body.y, 24f, 26f);
                if (IconButton(close, "clear", "Close".Translate())) window.Close();
                current = (ArchitectCategoryTab)Open.Invoke(window, null);
                DrawCategories(window, panels, current, search.filter.Active);
                current = (ArchitectCategoryTab)Open.Invoke(window, null);
                DrawGrid(window, current);
                DrawInformation(current);
                DrawStyleButton(body);
                GenUI.AbsorbClicksInRect(new Rect(0, 0, rect.width, rect.height));
            }
        }

        private static bool IconButton(Rect rect, string icon, string tip)
        {
            Theme.ButtonFace(rect, Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface);
            var texture = ToolsIcons.Get(icon);
            if (texture != null) GUI.DrawTexture(rect.ContractedBy(5f), texture);
            TooltipHandler.TipRegion(rect, tip);
            return Widgets.ButtonInvisible(rect);
        }

        private static void DrawSearch(MainTabWindow_Architect window, QuickSearchWidget search)
        {
            EnsureStyles();
            // Keep the native widget's control identity, filter and cache callbacks.
            // Its focus remains visible to WindowStack and gameplay hotkey guards.
            if (search.CurrentlyFocused() && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { search.Unfocus(); Event.current.Use(); }
            if (OriginalEventUtility.EventType == EventType.MouseDown && !searchRect.Contains(Event.current.mousePosition))
                search.Unfocus();
            Theme.RoundRect(searchRect, search.CurrentlyFocused() ? Theme.Muted : Theme.Border, 3f);
            Theme.RoundRect(searchRect.ContractedBy(1f), Theme.Rgb(DesignTokens.Deep), 2.3f);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Theme.Muted;
                var magnifier = ToolsIcons.Get("search");
                if (magnifier != null)
                    GUI.DrawTexture(new Rect(searchRect.x + 6f, searchRect.y + 6f, 15f, 15f), magnifier);
            }
            const float clearWidth = 43f;
            bool hasText = !string.IsNullOrEmpty(search.filter.Text);
            Rect clear = new Rect(searchRect.xMax - clearWidth - 3f, searchRect.y + 2f, clearWidth, searchRect.height - 4f);
            Rect input = new Rect(searchRect.x + 26f, searchRect.y + 2f,
                searchRect.width - 32f - (hasText ? clearWidth + 4f : 0f), searchRect.height - 4f);
            string next;
            using (new Theme.GuiScope(true))
            {
                GUI.color = search.noResultsMatched && search.filter.Active ? ColorLibrary.RedReadable : Color.white;
                GUI.SetNextControlName((string)SearchControlName.GetValue(search));
                next = GUI.TextField(input, search.filter.Text, search.maxSearchTextLength, searchStyle);
            }
            if (next != search.filter.Text)
            { search.filter.Text = next; Cache.Invoke(window, null); }
            if (hasText)
            {
                BodyLabel(clear.ContractedBy(5f, 0f), "Clear", Mouse.IsOver(clear) ? Theme.Ink : Theme.Muted);
                TooltipHandler.TipRegion(clear, "Clear search");
                if (Widgets.ButtonInvisible(clear))
                {
                    search.filter.Text = "";
                    SoundDefOf.CancelMode.PlayOneShotOnCamera();
                    Cache.Invoke(window, null);
                    search.Focus();
                }
            }
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
                tileLabelStyle = new GUIStyle(bodyStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                descriptionStyle = new GUIStyle(bodyStyle) { fontSize = 11, alignment = TextAnchor.UpperLeft, wordWrap = true };
                searchStyle = new GUIStyle(GUI.skin.textField) { font = Text.CurFontStyle.font,
                    fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(),
                    margin = new RectOffset(), border = new RectOffset() };
                foreach (GUIStyleState state in new[] { searchStyle.normal, searchStyle.hover, searchStyle.active,
                    searchStyle.focused, searchStyle.onNormal, searchStyle.onHover, searchStyle.onActive, searchStyle.onFocused })
                { state.background = null; state.textColor = Theme.Ink; }
                scrollTrack = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
                scrollThumb = new GUIStyle { margin = new RectOffset(), padding = new RectOffset(), fixedWidth = 4f };
                scrollThumb.normal.background = scrollThumb.hover.background = scrollThumb.active.background = BaseContent.WhiteTex;
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

        private static void DrawCategories(MainTabWindow_Architect window, List<ArchitectCategoryTab> panels,
            ArchitectCategoryTab current, bool searching)
        {
            float height = Math.Max(categories.height, panels.Count * ArchitectLayout.CategoryRow);
            categoryScroll.y = ArchitectLayout.ClampScroll(categoryScroll.y, height, categories.height);
            categoryScroll = GUI.BeginScrollView(categories, categoryScroll,
                new Rect(0, 0, categories.width - 10f, height), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                for (int i = 0; i < panels.Count; i++)
                {
                    var tab = panels[i];
                    Rect row = new Rect(0, i * ArchitectLayout.CategoryRow, categories.width - 10f, ArchitectLayout.CategoryRow - 3f);
                    bool available = tab.Visible;
                    bool selected = tab == current;
                    Color face = selected ? Theme.Selected : Mouse.IsOver(row) ? Theme.Hover : Theme.Surface;
                    Theme.ButtonFace(row, face, !available);
                    Color ink = !available || searching && !tab.AnySearchMatches ? Theme.Muted : Theme.Ink;
                    BodyLabel(row.ContractedBy(6f, 0), tab.def.LabelCap, ink);
                    if (!string.IsNullOrWhiteSpace(tab.def.description))
                        TooltipHandler.TipRegion(row, tab.def.LabelCap + "\n\n" + tab.def.description);
                    // Native callback retains unavailable-category rejection and search selection state.
                    if (Widgets.ButtonInvisible(row)) { Click.Invoke(window, new object[] { tab }); infoScroll = Vector2.zero; }
                }
            }
            finally { GUI.EndScrollView(); }
            DrawScrollbar(categories, ref categoryScroll, height);
        }

        private static void DrawScrollbar(Rect viewport, ref Vector2 position, float height)
        {
            if (height <= viewport.height + .01f) return;
            EnsureStyles();
            using (new Theme.GuiScope(true))
            {
                Rect rail = new Rect(viewport.xMax - 5f, viewport.y, 4f, viewport.height);
                Theme.RoundRect(rail, Theme.Surface, 2f);
                scrollThumb.fixedHeight = Math.Min(viewport.height, Math.Max(20f, viewport.height * viewport.height / height));
                GUI.color = Mouse.IsOver(rail.ExpandedBy(3f)) ? Theme.Ink : Theme.Muted;
                position.y = GUI.VerticalSlider(rail, position.y, 0f, height - viewport.height, scrollTrack, scrollThumb);
            }
        }

        private static void DrawGrid(MainTabWindow_Architect window, ArchitectCategoryTab category)
        {
            // Native Architect dispatches hotkeys outside GUI.Window. A KeyDown
            // handled in Extras must never enter the dispatcher twice.
            if (keyboardGridDispatched) return;
            hoveredLabel = ""; hoveredDesignator = null;
            if (category == null)
            {
                lastCategory = null; designatorCount = 0;
                BodyLabel(new Rect(grid.x + 4f, grid.y + 12f, grid.width - 8f, 28f),
                    string.IsNullOrWhiteSpace(lastQuery) ? "Select a category" : "No matching designators", Theme.Muted);
                return;
            }
            List<Designator> designators = category.def.ResolvedAllowedDesignators.ToList();
            designatorCount = designators.Count(d => d.Visible);
            if (lastCategory != category)
            {
                // Deliberate upper bound; native grouping/shrinking may use less. Record exact paint bounds below.
                drawHeight = Math.Max(grid.height, designatorCount * 90f);
                contentHeight = drawHeight;
                gridScroll = new Vector2(0, contentHeight - grid.height);
                infoScroll = Vector2.zero; lastCategory = category;
            }
            if (designatorCount == 0) { drawHeight = 0f; contentHeight = grid.height; }
            minGridY = drawHeight;
            float previousHeight = contentHeight;
            gridScroll.y = ArchitectLayout.ClampScroll(gridScroll.y, contentHeight, grid.height);
            gridScroll = GUI.BeginScrollView(grid, gridScroll, new Rect(0, 0, grid.width - 10f, contentHeight),
                false, false, GUIStyle.none, GUIStyle.none);
            Gizmo hovered;
            int heightFrame = (int)HeightFrame.GetValue(null);
            float heightDrawn = (float)HeightDrawn.GetValue(null);
            try
            {
                DrawingGrid = true;
                var high = (Func<Gizmo, bool>)Delegate.CreateDelegate(typeof(Func<Gizmo, bool>), category, Highlight);
                var low = (Func<Gizmo, bool>)Delegate.CreateDelegate(typeof(Func<Gizmo, bool>), category, Lowlight);
                var forced = ArchitectTransition.Moving ? null : (Designator)Forced.GetValue(window);
                GizmoGridDrawer.DrawGizmoGrid(designators, 0f, out hovered,
                    forced == null ? (Func<Gizmo, bool>)null : g => g == forced, high, low, false);
                if (!ArchitectTransition.Moving) Forced.SetValue(window, null);
                if (hovered is Designator designator) hoveredDesignator = designator;
                else hoveredDesignator = null;
                hoveredLabel = hoveredDesignator?.Label ?? "";
            }
            finally
            {
                DrawingGrid = false;
                // The bounded Architect grid must not reserve global selector/HUD space.
                HeightFrame.SetValue(null, heightFrame); HeightDrawn.SetValue(null, heightDrawn);
                GUI.EndScrollView();
            }
            if (Event.current.type == EventType.Repaint && minGridY < drawHeight)
            {
                drawHeight = ArchitectLayout.MeasuredBlockHeight(drawHeight, minGridY);
                contentHeight = Math.Max(grid.height, drawHeight);
                gridScroll.y = ArchitectLayout.ReanchorScroll(gridScroll.y, previousHeight, contentHeight, grid.height);
            }
            DrawScrollbar(grid, ref gridScroll, contentHeight);
        }

        private static Designator hoveredDesignator;
        private static void DrawInformation(ArchitectCategoryTab category)
        {
            Designator designator = hoveredDesignator ?? Find.DesignatorManager.SelectedDesignator;
            selectedLabel = Find.DesignatorManager.SelectedDesignator?.Label ?? "";
            Theme.RoundRect(information, Theme.Rgb(DesignTokens.Deep).ToTransparent(.3f), 3f);
            if (category == null || designator == null) return;
            EnsureStyles();
            if (lastInformationDesignator != designator)
            { infoScroll = Vector2.zero; readoutHeight = 300f; lastInformationDesignator = designator; }
            Rect viewport = information.ContractedBy(7f);
            float costWidth = 180f, descWidth = viewport.width - 10f - costWidth - 12f;
            string desc = designator.Desc ?? "";
            string disabled = designator.Disabled ? designator.disabledReason ?? "" : "";
            float descriptionHeight = 24f + descriptionStyle.CalcHeight(new GUIContent(desc), descWidth) +
                (string.IsNullOrEmpty(disabled) ? 0f : 6f + descriptionStyle.CalcHeight(new GUIContent(disabled), descWidth)) + 6f;
            float height = Math.Max(viewport.height, Math.Max(descriptionHeight, readoutHeight));
            infoScroll.y = ArchitectLayout.ClampScroll(infoScroll.y, height, viewport.height);
            infoScroll = GUI.BeginScrollView(viewport, infoScroll, new Rect(0, 0, viewport.width - 10f, height),
                false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Theme.Ink;
                    Rect descRect = new Rect(costWidth + 12f, 0, descWidth, 26f);
                    BodyLabel(descRect, designator.LabelCap, Theme.Ink);
                    descRect.y += 24f; descRect.height = descriptionStyle.CalcHeight(new GUIContent(desc), descRect.width);
                    GUI.Label(descRect, desc, descriptionStyle);
                    if (!string.IsNullOrEmpty(disabled))
                    { GUI.color = Theme.Danger; GUI.Label(new Rect(descRect.x, descRect.yMax + 6f, descRect.width,
                        descriptionStyle.CalcHeight(new GUIContent(disabled), descRect.width)), disabled, descriptionStyle); }
                    GUI.color = Color.white;
                    float y = 32f; // Native information button occupies the first 24px row.
                    designator.DrawPanelReadout(ref y, costWidth);
                    DrawShapeControl();
                    if (Event.current.type == EventType.Repaint) readoutHeight = y + 12f;
                }
            }
            finally { GUI.EndScrollView(); }
            DrawScrollbar(viewport, ref infoScroll, height);
        }

        private static readonly CachedTexture StyleIcon = new CachedTexture("UI/Icons/ChangeSelectedStyles");
        private static void DrawStyleButton(Rect body)
        {
            if (!ModsConfig.IdeologyActive || !Find.IdeoManager.classicMode) return;
            Rect rect = new Rect(categories.xMax - 26f, body.y, 24f, 24f);
            if (Widgets.ButtonImage(rect.ContractedBy(2f), StyleIcon.Texture))
            {
                if (Find.WindowStack.IsOpen<Dialog_StyleSelection>()) Find.WindowStack.TryRemove(typeof(Dialog_StyleSelection));
                else Find.WindowStack.Add(new Dialog_StyleSelection());
            }
        }

        internal static void Extras(MainTabWindow_Architect window)
        {
            keyboardGridDispatched = false;
            drawStyleCategory = null;
            if (ArchitectTransition.Moving) return;
            DispatchKeyboard(window);
            if (Find.DesignatorManager.SelectedDesignator == null) return;
            try
            {
                DrawingExtras = true;
                Find.DesignatorManager.SelectedDesignator.DoExtraGuiControls(
                    Bounds.x + ArchitectLayout.Padding + ArchitectLayout.CategoryWidth + 14f,
                    Bounds.yMax - ArchitectLayout.Padding);
            }
            finally { DrawingExtras = false; }
        }

        private static void DispatchKeyboard(MainTabWindow_Architect window)
        {
            if (ArchitectTransition.Moving || Event.current.type != EventType.KeyDown || !Find.WindowStack.GetsInput(window) ||
                Find.WindowStack.AnyWindowAbsorbingAllInput || Find.WindowStack.IsOpen<FloatMenu>() ||
                Find.WindowStack.AnySearchWidgetFocused || SearchFocused(window) || GUIUtility.keyboardControl != 0 ||
                KeyBindingDefOf.Accept.KeyDownEvent || KeyBindingDefOf.Cancel.KeyDownEvent) return;
            var category = (ArchitectCategoryTab)Open.Invoke(window, null);
            if (category == null) return;
            keyboardGridDispatched = true;
            int heightFrame = (int)HeightFrame.GetValue(null);
            float heightDrawn = (float)HeightDrawn.GetValue(null);
            try
            {
                using (new Theme.GuiScope(true))
                {
                    DrawingGrid = true;
                    var high = (Func<Gizmo, bool>)Delegate.CreateDelegate(typeof(Func<Gizmo, bool>), category, Highlight);
                    var low = (Func<Gizmo, bool>)Delegate.CreateDelegate(typeof(Func<Gizmo, bool>), category, Lowlight);
                    GizmoGridDrawer.DrawGizmoGrid(category.def.ResolvedAllowedDesignators, 0f, out _, null, high, low, false);
                }
            }
            finally
            {
                DrawingGrid = false;
                HeightFrame.SetValue(null, heightFrame); HeightDrawn.SetValue(null, heightDrawn);
            }
        }

        internal static void CaptureDrawStyles(DrawStyleCategoryDef category) => drawStyleCategory = category;

        private static void DrawShapeControl()
        {
            if (drawStyleCategory?.styles == null || drawStyleCategory.styles.Count <= 1) return;
            Rect rect = new Rect(0f, 0f, 28f, 24f);
            Theme.ButtonFace(rect, Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface);
            var selected = Find.DesignatorManager.SelectedStyle;
            if (selected?.uiIcon != null) GUI.DrawTexture(rect.ContractedBy(4f), selected.uiIcon, ScaleMode.ScaleToFit);
            TooltipHandler.TipRegion(rect, selected?.LabelCap ?? "");
            if (Widgets.ButtonInvisible(rect))
            {
                var menu = new FloatMenu(drawStyleCategory.styles.Select(style =>
                    new FloatMenuOption(style.LabelCap, () => Find.DesignatorManager.SelectedStyle = style)).ToList());
                ArchitectPopup.RegisterOwned(menu);
                Find.WindowStack.Add(menu);
            }
        }

        internal static GizmoResult DrawNativeGizmo(Gizmo gizmo, Vector2 location, float maxWidth, GizmoRenderParms parms)
        {
            if (DrawingGrid) minGridY = Math.Min(minGridY, location.y);
            return gizmo.GizmoOnGUI(location, maxWidth, parms);
        }
        internal static GizmoResult DrawNativeShrunk(Command command, Vector2 location, float size, GizmoRenderParms parms)
        {
            if (DrawingGrid) minGridY = Math.Min(minGridY, location.y);
            return command.GizmoOnGUIShrunk(location, size, parms);
        }

        private static readonly FieldInfo CommandBackground = AccessTools.Field(typeof(Command), "BGTex");
        private static readonly FieldInfo CommandBackgroundShrunk = AccessTools.Field(typeof(Command), "BGTexShrunk");
        internal static void DrawCommandBackground(Rect rect, Texture texture, Material material, Rect texCoords)
        {
            if ((!DrawingGrid && !SelectionCommands.Drawing && !WorldMenus.CommandDrawing) || texture != (Texture)CommandBackground.GetValue(null) &&
                texture != (Texture)CommandBackgroundShrunk.GetValue(null))
            { GenUI.DrawTextureWithMaterial(rect, texture, material, texCoords); return; }
            using (new Theme.GuiScope(true))
            {
                // Native supplies grayscale material / dim tint for search mismatches and
                // disabled commands. Keep that distinction, with the shared flat surface.
                bool dim = material != null;
                GUI.color = Color.white;
                Theme.ButtonFace(rect, !dim && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface, dim, radius: 3f);
            }
        }

        internal static void DrawNativeLabelBackground(Rect rect, Texture texture)
        { if (!DrawingGrid && !SelectionCommands.Drawing && !WorldMenus.CommandDrawing) GUI.DrawTexture(rect, texture); }

        internal static void DrawTileIcon(Command command, Rect tile, Material material, GizmoRenderParms parms)
        {
            if ((!DrawingGrid && !SelectionCommands.Drawing && !WorldMenus.CommandDrawing) || command.GetType().Assembly != typeof(Command).Assembly)
            { command.DrawIcon(tile, material, parms); return; }
            Rect clip = tile.ContractedBy(1f);
            GUI.BeginClip(clip);
            try
            {
                Rect icon = new Rect(2f, 2f, clip.width - 4f, clip.height - 4f);
                if (command is Designator_Build build && build.PlacingDef is ThingDef thing)
                {
                    // Some native ground markers deliberately use world-sized icon
                    // multipliers. Fit those thumbnails to the tile without changing
                    // their definition, style/material choice or placement rendering.
                    float scale = Math.Max(1f, build.ThingStyleDefForPreview?.uiIconScale ?? GenUI.IconDrawScale(thing));
                    Vector2 center = icon.center;
                    icon.size /= scale;
                    icon.center = center;
                }
                command.DrawIcon(icon, material, parms);
            }
            finally { GUI.EndClip(); }
        }

        internal static void DrawTileLabel(Rect nativeRect, string label, Rect tile)
        {
            if (!DrawingGrid && !SelectionCommands.Drawing && !WorldMenus.CommandDrawing) { Widgets.Label(nativeRect, label); return; }
            EnsureStyles();
            using (new Theme.GuiScope(true))
            {
                Color ink = GUI.color; // Native lowlight/disabled label tint.
                ColonistCardRenderer.DrawNameBackdrop(new Rect(tile.x + 1f, tile.yMax - 29f,
                    tile.width - 2f, 28f), Theme.Surface, 2f);
                Rect labelRect = new Rect(tile.x + 3f, tile.yMax - 18f, tile.width - 6f, 16f);
                string text = label ?? "";
                if (tileLabelStyle.CalcSize(new GUIContent(text)).x > labelRect.width)
                {
                    int length = text.Length;
                    while (length > 0 && tileLabelStyle.CalcSize(new GUIContent(text.Substring(0, length) + "…")).x > labelRect.width) length--;
                    text = text.Substring(0, length) + "…";
                }
                GUI.color = ink;
                GUI.Label(labelRect, text, tileLabelStyle);
            }
        }

        internal static Dictionary<string, object> AnimationAuditState() => new Dictionary<string, object>
        {
            ["active"] = active && lastWindow != null && Eligible(lastWindow) && Find.MainTabsRoot.OpenTab == MainButtonDefOf.Architect,
            ["shell"] = RectData(shell), ["categories"] = RectData(categories), ["grid"] = RectData(grid),
            ["information"] = RectData(information), ["search"] = RectData(searchRect),
            ["categoryCount"] = categoryCount, ["designatorCount"] = designatorCount, ["category"] = lastCategory?.def.defName ?? "",
            ["query"] = lastQuery, ["searchFocused"] = focused, ["selectedDesignator"] = selectedLabel, ["hoveredDesignator"] = hoveredLabel,
            ["categoryScroll"] = categoryScroll.y, ["gridScroll"] = gridScroll.y, ["gridContentHeight"] = contentHeight,
            ["gridPaintHeight"] = drawHeight, ["readoutHeight"] = readoutHeight,
            ["infoScroll"] = infoScroll.y, ["motion"] = ArchitectTransition.Audit()
        };
        private static float[] RectData(Rect rect) => new[] { rect.x, rect.y, rect.width, rect.height };
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), nameof(MainTabWindow_Architect.DoWindowContents))]
    internal static class ArchitectContentsPatch
    {
        private static bool Prefix(MainTabWindow_Architect __instance, Rect __0)
        { if (!ArchitectPanel.Eligible(__instance)) return true; ArchitectTransition.DrawContents(__instance, __0); return false; }
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), nameof(MainTabWindow_Architect.ExtraOnGUI))]
    internal static class ArchitectExtrasPatch
    {
        private static bool Prefix(MainTabWindow_Architect __instance)
        {
            ArchitectWindowPatch.UpdateOwnership(__instance);
            if (!ArchitectPanel.Eligible(__instance)) return true;
            ArchitectTransition.DrawOpeningShell();
            ArchitectPanel.Extras(__instance); return false;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), "get_Margin")]
    internal static class ArchitectMarginPatch
    {
        private static bool Prefix(MainTabWindow_Architect __instance, ref float __result)
        { if (!ArchitectPanel.Eligible(__instance)) return true; __result = 0; return false; }
    }

    [HarmonyPatch(typeof(DesignatorUtility), nameof(DesignatorUtility.GUIDoRotationControls))]
    internal static class ArchitectRotationPatch
    {
        // Keyboard rotation is handled by native placement input, independently
        // of this purely visual helper. The user requested no Q/E buttons.
        private static bool Prefix() => !ArchitectPanel.DrawingExtras ||
            Find.DesignatorManager.SelectedDesignator?.GetType().Assembly != typeof(Designator).Assembly;
    }

    [HarmonyPatch(typeof(Designator), nameof(Designator.DoExtraGuiControls))]
    internal static class ArchitectDrawStylePatch
    {
        private static bool Prefix(Designator __instance)
        {
            if (!ArchitectPanel.DrawingExtras || __instance.GetType().Assembly != typeof(Designator).Assembly) return true;
            ArchitectPanel.CaptureDrawStyles(__instance.DrawStyleCategory);
            return false;
        }
    }

    [HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
    internal static class ArchitectWindowPatch
    {
        private static Window owner;
        private static Rect nativeRect;
        private static Rect lastCustomRect;
        private static readonly MethodInfo Reposition = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static bool nativeBackground, nativeShadow;
        private static void Prefix(Window __instance) => UpdateOwnership(__instance);

        internal static void UpdateOwnership(Window window)
        {
            if (window.GetType() != typeof(MainTabWindow_Architect)) return;
            if (!ArchitectPanel.Eligible(window))
            {
                if (owner == window)
                {
                    Release(window);
                    Reposition.Invoke(window, null);
                }
                return;
            }
            if (owner != window)
            {
                if (owner != null) Release(owner);
                nativeBackground = window.doWindowBackground; nativeShadow = window.drawShadow;
                nativeRect = window.windowRect; owner = window;
                ArchitectTransition.Open((MainTabWindow_Architect)window);
            }
            else if (window.windowRect != lastCustomRect) nativeRect = window.windowRect;
            window.windowRect = ArchitectPanel.Bounds;
            lastCustomRect = window.windowRect;
            // ExtraOnGUI precedes WindowStack's shadow pass. Keep these flags
            // suppressed through the whole owned interval, including GUI.Window.
            window.doWindowBackground = window.drawShadow = false;
        }

        internal static void Release(Window window)
        {
            if (owner != window) return;
            window.doWindowBackground = nativeBackground; window.drawShadow = nativeShadow;
            window.windowRect = nativeRect; owner = null;
        }
    }

    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class ArchitectClosePatch
    {
        private static void Prefix(Window __instance) => ArchitectTransition.Close(__instance);
        private static void Postfix(Window __instance) => ArchitectWindowPatch.Release(__instance);
    }

    // Preserve the complete native dispatcher (grouping, tutorial/highlight, hotkeys,
    // right-click, material menus and custom designators). Substitute only two initialized
    // layout bounds and wrap two paint calls for measured overflow. Pattern drift
    // disables this feature locally and retains the native Architect UI.
    [HarmonyPatch(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid))]
    internal static class ArchitectGridPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var source = instructions.ToList();
            FieldInfo screenWidth = AccessTools.Field(typeof(UI), nameof(UI.screenWidth));
            FieldInfo screenHeight = AccessTools.Field(typeof(UI), nameof(UI.screenHeight));
            FieldInfo spacing = AccessTools.Field(typeof(GizmoGridDrawer), "GizmoSpacing");
            FieldInfo y = AccessTools.Field(typeof(Vector2), nameof(Vector2.y));
            int rightIndex = -1, bottomIndex = -1, rightMatches = 0, bottomMatches = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (i >= 4 && source[i].opcode == OpCodes.Stloc_1 &&
                    source[i - 4].LoadsField(screenWidth) && source[i - 3].LoadsConstant(147) &&
                    source[i - 2].opcode == OpCodes.Sub && source[i - 1].opcode == OpCodes.Conv_R4)
                { rightIndex = i; rightMatches++; }
                if (i >= 9 && source[i].opcode == OpCodes.Stloc_2 &&
                    source[i - 9].LoadsField(screenHeight) && source[i - 8].LoadsConstant(35) &&
                    source[i - 7].opcode == OpCodes.Sub && source[i - 6].opcode == OpCodes.Conv_R4 &&
                    source[i - 5].opcode == OpCodes.Ldsflda && Equals(source[i - 5].operand, spacing) &&
                    source[i - 4].LoadsField(y) && source[i - 3].opcode == OpCodes.Sub &&
                    source[i - 2].LoadsConstant(75f) && source[i - 1].opcode == OpCodes.Sub)
                { bottomIndex = i; bottomMatches++; }
            }
            var result = new List<CodeInstruction>();
            int right = 0, bottom = 0, normal = 0, shrunk = 0;
            MethodInfo gizmo = AccessTools.Method(typeof(Gizmo), nameof(Gizmo.GizmoOnGUI));
            MethodInfo small = AccessTools.Method(typeof(Command), nameof(Command.GizmoOnGUIShrunk));
            if (rightMatches != 1 || bottomMatches != 1 || source.Count(c => c.Calls(gizmo)) != 1 || source.Count(c => c.Calls(small)) != 1)
            {
                ArchitectSeams.GridReady = false;
                Log.Warning("[Reframed UI] Native Architect grid IL changed; native Architect retained.");
                return source;
            }
            for (int i = 0; i < source.Count; i++)
            {
                CodeInstruction instruction = new CodeInstruction(source[i]);
                if (i == rightIndex)
                { result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.GridRight)))); right++; }
                if (i == bottomIndex)
                { result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.GridBottom)))); bottom++; }
                if (instruction.Calls(gizmo))
                { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawNativeGizmo)); normal++; }
                if (instruction.Calls(small))
                { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawNativeShrunk)); shrunk++; }
                result.Add(instruction);
            }
            ArchitectSeams.GridReady = right == 1 && bottom == 1 && normal == 1 && shrunk == 1;
            return result;
        }
    }

    [HarmonyPatch(typeof(Command), "GizmoOnGUIInt")]
    internal static class ArchitectCommandFacePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo native = AccessTools.Method(typeof(GenUI), nameof(GenUI.DrawTextureWithMaterial),
                new[] { typeof(Rect), typeof(Texture), typeof(Material), typeof(Rect) });
            MethodInfo labelGetter = AccessTools.PropertyGetter(typeof(Command), nameof(Command.LabelCap));
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            MethodInfo background = AccessTools.Method(typeof(GUI), nameof(GUI.DrawTexture), new[] { typeof(Rect), typeof(Texture) });
            MethodInfo icon = AccessTools.Method(typeof(Command), nameof(Command.DrawIcon), new[] { typeof(Rect), typeof(Material), typeof(GizmoRenderParms) });
            int replaced = 0;
            var source = instructions.ToList();
            int labelStart = source.FindIndex(c => c.Calls(labelGetter));
            int labelIndex = source.FindIndex(Math.Max(0, labelStart), c => c.Calls(label));
            int backgroundIndex = source.FindIndex(Math.Max(0, labelStart), c => c.Calls(background));
            if (native == null || source.Count(c => c.Calls(native)) != 1 || source.Count(c => c.Calls(icon)) != 1 ||
                source.Count(c => c.Calls(labelGetter)) != 1 || labelStart < 0 ||
                backgroundIndex <= labelStart || labelIndex <= backgroundIndex || labelIndex - backgroundIndex != 4 ||
                !source[backgroundIndex - 1].LoadsField(AccessTools.Field(typeof(TexUI), nameof(TexUI.GrayTextBG))))
            {
                ArchitectSeams.FaceReady = false;
                Log.Warning("[Reframed UI] Native command background IL changed; native Architect retained.");
                return source;
            }
            var result = new List<CodeInstruction>();
            for (int i = 0; i < source.Count; i++)
            {
                CodeInstruction instruction = new CodeInstruction(source[i]);
                if (instruction.Calls(native))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawCommandBackground));
                    replaced++;
                }
                if (i == backgroundIndex)
                    instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawNativeLabelBackground));
                if (instruction.Calls(icon))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawTileIcon));
                }
                if (i == labelIndex)
                {
                    var tileArgument = new CodeInstruction(OpCodes.Ldarg_1);
                    tileArgument.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    result.Add(tileArgument);
                    instruction.operand = AccessTools.Method(typeof(ArchitectPanel), nameof(ArchitectPanel.DrawTileLabel));
                }
                result.Add(instruction);
            }
            ArchitectSeams.FaceReady = replaced == 1;
            return result;
        }
    }
}

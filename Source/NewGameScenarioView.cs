using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Inferred companion to the observed storyteller design. The native page still
    // owns selection, configuration, editing and navigation; this is only its body.
    internal sealed class NewGameScenarioView
    {
        private static readonly AccessTools.FieldRef<Page_SelectScenario, Scenario> Selected =
            AccessTools.FieldRefAccess<Page_SelectScenario, Scenario>("curScen");
        private static readonly MethodInfo EnsureSelection = AccessTools.Method(typeof(Page_SelectScenario), "EnsureValidSelection");
        private static readonly MethodInfo PublishedId = AccessTools.Method(typeof(Scenario), "GetPublishedFileId");
        private static readonly MethodInfo OpenWorkshop = AccessTools.Method(typeof(SteamUtility), "OpenWorkshopPage", new[] { PublishedId.ReturnType });
        private static readonly MethodInfo Unsubscribe = AccessTools.TypeByName("Verse.Steam.Workshop")
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == "Unsubscribe" && method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(Scenario)));
        private static readonly object InvalidPublishedId = PublishedId.ReturnType.GetField("Invalid", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        private static readonly Color Ink = Theme.Rgb(0xF2F5F7), Muted = Theme.Rgb(0x9BAAB8), Border = Theme.Rgb(0x313B4A);
        private readonly List<Row> rows = new List<Row>();
        private Vector2 listScroll, infoScroll;
        private Scenario lastScenario;
        private LoadedLanguage styleLanguage;
        private GUIStyle heading, title, body, introduction, section, button, scrollTrack, scrollThumb;
        private float scale, styleScale = -1, opacity, listHeight;
        private int pageId = -1;
        private string information;
        private Texture2D warning, upload, workshop;

        private enum Kind { Scenario, Heading, Empty, Workshop }
        private sealed class Row
        {
            internal Kind Kind;
            internal Scenario Scenario;
            internal string Text, Summary;
            internal Rect Rect;
            internal float TitleHeight;
            internal object Published;
            internal bool HasPublished, CanUpload;
        }

        internal void Draw(Rect rect, Page_SelectScenario page, ref Scenario selected, float opacity = 1f)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            using (new Theme.GuiScope(true))
            {
                this.opacity = Mathf.Clamp01(opacity * GUI.color.a);
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                scale = rect.width / 582f;
                Styles();
                // Reopening after the native scenario editor can mutate the same
                // Scenario instance. WindowStack assigns a fresh ID on every Add.
                if (pageId != page.ID) { pageId = page.ID; lastScenario = null; }
                Label(At(rect, 17, 18, 237, 26), page.PageTitle, heading, Ink);
                string help = LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage
                    ? "Choose your colony's\nstarting story." : "ChooseScenario".Translate().ToString();
                Label(At(rect, 17, 49, 237, 54), help, introduction, Muted);
                BuildRows();
                DrawList(At(rect, 17, 115, 248, 457), page, ref selected);
                DrawInformation(At(rect, 289, 20, 284, 552), selected);
            }
        }

        private void BuildRows()
        {
            rows.Clear();
            listHeight = 0;
            AddScenarios(ScenarioCategory.FromDef);
            AddText(Kind.Heading, "ScenariosCustom".Translate().ToString().ToUpperInvariant(), 34);
            AddScenarios(ScenarioCategory.CustomLocal);
            AddText(Kind.Heading, "ScenariosSteamWorkshop".Translate().ToString().ToUpperInvariant(), 34);
            AddText(Kind.Workshop, "OpenSteamWorkshop".Translate(), 30);
            AddScenarios(ScenarioCategory.SteamWorkshop);
            listHeight = Mathf.Max(0, listHeight - 8 * scale);
        }

        private void AddScenarios(ScenarioCategory category)
        {
            bool any = false;
            foreach (Scenario scenario in ScenarioLister.ScenariosInCategory(category))
            {
                if (!scenario.showInUI) continue;
                any = true;
                object published = PublishedId.Invoke(scenario, null);
                bool hasPublished = !Equals(published, InvalidPublishedId);
                bool canUpload = hasPublished && scenario.CanToUploadToWorkshop();
                int actions = !scenario.enabled ? 0 : (CanDelete(scenario) ? 1 : 0) + (hasPublished ? 1 : 0) + (canUpload ? 1 : 0);
                if (!scenario.valid) actions++;
                float nameWidth = (217 - actions * 20) * scale;
                float nameHeight = Mathf.Max(21 * scale, Mathf.Ceil(title.CalcHeight(new GUIContent(scenario.name), nameWidth)));
                string summary = scenario.GetSummary();
                // Preserve every wrapped line. Longer cards extend the scrollable
                // list rather than clipping summaries at the previous two-line cap.
                float summaryHeight = Mathf.Ceil(body.CalcHeight(new GUIContent(summary), 217 * scale));
                float height = Mathf.Max(69 * scale, 18 * scale + nameHeight + summaryHeight);
                rows.Add(new Row { Kind = Kind.Scenario, Scenario = scenario, Text = scenario.name, Summary = summary,
                    Rect = new Rect(0, listHeight, 237 * scale, height), TitleHeight = nameHeight,
                    Published = published, HasPublished = hasPublished, CanUpload = canUpload });
                listHeight += height + 8 * scale;
            }
            if (!any) AddText(Kind.Empty, "(" + "NoneLower".Translate() + ")", 26);
        }

        private void AddText(Kind kind, string text, float height)
        {
            rows.Add(new Row { Kind = kind, Text = text, Rect = new Rect(0, listHeight, 237 * scale, height * scale) });
            listHeight += (height + 8) * scale;
        }

        private void DrawList(Rect viewport, Page_SelectScenario page, ref Scenario selected)
        {
            BeginScroll(viewport, ref listScroll, listHeight);
            try
            {
                foreach (Row row in rows)
                {
                    if (row.Rect.yMax < listScroll.y || row.Rect.y > listScroll.y + viewport.height) continue;
                    if (row.Kind == Kind.Scenario) DrawCard(row, page, ref selected);
                    else if (row.Kind == Kind.Workshop)
                    {
                        Face(row.Rect, false, 4);
                        Label(row.Rect.ContractedBy(8 * scale, 0), row.Text, button, Ink);
                        if (GUI.enabled) TooltipHandler.TipRegion(row.Rect, row.Text);
                        if (GUI.enabled && Widgets.ButtonInvisible(row.Rect)) SteamUtility.OpenSteamWorkshopPage();
                    }
                    else Label(row.Rect, row.Text, row.Kind == Kind.Heading ? section : body, row.Kind == Kind.Heading ? Ink : Muted);
                }
            }
            finally { GUI.EndScrollView(GUI.enabled); }
            Scrollbar(viewport, ref listScroll, listHeight);
        }

        private void DrawCard(Row row, Page_SelectScenario page, ref Scenario selected)
        {
            Scenario scenario = row.Scenario;
            Rect rect = row.Rect;
            bool available = GUI.enabled && scenario.enabled;
            Face(rect, selected == scenario, 8, available);
            float right = rect.xMax - 9 * scale;
            // Native list action order is right-to-left: delete, Workshop, upload, warning.
            bool action = false;
            if (scenario.enabled)
            {
                if (CanDelete(scenario))
                {
                    Rect icon = IconRect(ref right, rect.y + 8 * scale);
                    string tip = (scenario.Category == ScenarioCategory.CustomLocal ? "Delete" : "Unsubscribe").Translate();
                    if (Icon(icon, SaveBrowserIcons.Delete, tip, available)) { ConfirmRemoval(scenario, page); action = true; }
                }
                if (row.HasPublished)
                {
                    Rect icon = IconRect(ref right, rect.y + 8 * scale);
                    if (Icon(icon, workshop, "WorkshopPage".Translate(), available))
                    { OpenWorkshop.Invoke(null, new[] { row.Published }); action = true; }
                    if (row.CanUpload) Icon(IconRect(ref right, rect.y + 8 * scale), upload, "CanBeUpdatedOnWorkshop".Translate(), false);
                }
            }
            if (!scenario.valid)
            {
                Icon(IconRect(ref right, rect.y + 8 * scale), warning, "ScenPart_Error".Translate(), false);
                if (GUI.enabled) TooltipHandler.TipRegion(rect, "ScenPart_Error".Translate().Colorize(ColorLibrary.RedReadable));
            }
            Color textColor = scenario.enabled ? Ink : Muted;
            Label(new Rect(rect.x + 10 * scale, rect.y + 8 * scale, right - rect.x - 10 * scale, row.TitleHeight), row.Text, title, textColor);
            Label(new Rect(rect.x + 10 * scale, rect.y + 11 * scale + row.TitleHeight, rect.width - 20 * scale,
                rect.height - row.TitleHeight - 18 * scale), row.Summary, body, Muted);
            if (GUI.enabled && scenario.valid) TooltipHandler.TipRegion(rect, row.Text + "\n\n" + row.Summary);
            if (available) MouseoverSounds.DoRegion(rect);
            if (action || !available || selected == scenario || !Widgets.ButtonInvisible(rect)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            if (!scenario.valid)
                PreLoadUtility.CheckVersionAndLoad(scenario.File.FullName, ScribeMetaHeaderUtility.ScribeHeaderMode.Scenario,
                    () => Selected(page) = scenario);
            else selected = scenario;
        }

        private static bool CanDelete(Scenario scenario) => scenario.Category == ScenarioCategory.CustomLocal || scenario.Category == ScenarioCategory.SteamWorkshop;
        private static void ConfirmRemoval(Scenario scenario, Page_SelectScenario page)
        {
            if (scenario.Category == ScenarioCategory.CustomLocal)
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(scenario.File.Name), () =>
                {
                    scenario.File.Delete();
                    ScenarioLister.MarkDirty();
                }, destructive: true));
            else
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmUnsubscribeFrom".Translate(scenario.File.Name), () =>
                {
                    scenario.enabled = false;
                    if (Selected(page) == scenario)
                    {
                        Selected(page) = null;
                        EnsureSelection.Invoke(page, null);
                    }
                    Unsubscribe.Invoke(null, new object[] { scenario });
                }, destructive: true));
        }

        private void DrawInformation(Rect viewport, Scenario selected)
        {
            if (selected == null) return;
            if (lastScenario != selected)
            {
                lastScenario = selected;
                infoScroll = Vector2.zero;
                information = selected.GetFullInformationText();
            }
            float width = 276 * scale;
            float nameHeight = Mathf.Max(22 * scale, title.CalcHeight(new GUIContent(selected.name), width));
            float textHeight = body.CalcHeight(new GUIContent(information ?? string.Empty), width);
            float height = nameHeight + 12 * scale + textHeight + 6 * scale;
            BeginScroll(viewport, ref infoScroll, height);
            try
            {
                Label(new Rect(0, 0, width, nameHeight), selected.name, title, Ink);
                Label(new Rect(0, nameHeight + 12 * scale, width, textHeight), information, body, Muted);
            }
            finally { GUI.EndScrollView(GUI.enabled); }
            Scrollbar(viewport, ref infoScroll, height);
        }

        private Rect IconRect(ref float right, float y)
        {
            right -= 18 * scale;
            Rect rect = new Rect(right, y, 18 * scale, 18 * scale);
            right -= 2 * scale;
            return rect;
        }
        private bool Icon(Rect rect, Texture2D texture, string tooltip, bool clickable)
        {
            if (texture != null)
            {
                GUI.color = Alpha(clickable && Mouse.IsOver(rect) ? Ink : Muted);
                GUI.DrawTexture(rect.ContractedBy(2 * scale), texture, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
            if (GUI.enabled) TooltipHandler.TipRegion(rect, tooltip);
            return clickable && Widgets.ButtonInvisible(rect);
        }
        private void Face(Rect rect, bool selected, float radius, bool hover = true)
        {
            if (selected) Theme.RoundRect(rect, Alpha(Theme.Rgb(0x181F25), .96f), radius * scale);
            else if (hover && GUI.enabled && Mouse.IsOver(rect)) Theme.RoundRect(rect, Alpha(Theme.Rgb(0x181F25), .68f), radius * scale);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true, 0, Alpha(Border), scale, radius * scale);
        }
        private void BeginScroll(Rect viewport, ref Vector2 position, float height)
        {
            position.x = 0;
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, height - viewport.height));
            position = GUI.BeginScrollView(viewport, position, new Rect(0, 0, viewport.width, Mathf.Max(viewport.height, height)), false, false, GUIStyle.none, GUIStyle.none);
        }
        private void Scrollbar(Rect viewport, ref Vector2 position, float height)
        {
            if (height <= viewport.height + .5f) return;
            scrollThumb.fixedHeight = Mathf.Min(viewport.height, Mathf.Max(24 * scale, viewport.height * viewport.height / height));
            GUI.color = Alpha(Theme.Rgb(0x23323D));
            position.y = GUI.VerticalSlider(new Rect(viewport.xMax - 6 * scale, viewport.y, 6 * scale, viewport.height), position.y, 0,
                height - viewport.height, scrollTrack, scrollThumb);
            GUI.color = Color.white;
        }
        private void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            bool enabled = GUI.enabled;
            GUI.color = Alpha(color); GUI.enabled = true;
            try { GUI.Label(rect, text ?? string.Empty, style); }
            finally { GUI.color = Color.white; GUI.enabled = enabled; }
        }
        private Color Alpha(Color color, float amount = 1f) { color.a *= opacity * amount; return color; }
        private Rect At(Rect origin, float x, float y, float width, float height) => new Rect(origin.x + x * scale, origin.y + y * scale, width * scale, height * scale);
        private void Styles()
        {
            if (body != null && Mathf.Approximately(styleScale, scale) && body.font == Text.CurFontStyle.font && styleLanguage == LanguageDatabase.activeLanguage) return;
            styleScale = scale;
            styleLanguage = LanguageDatabase.activeLanguage;
            lastScenario = null;
            heading = Style(16, true, TextAnchor.UpperLeft);
            title = Style(14, true, TextAnchor.UpperLeft);
            body = Style(12, true, TextAnchor.UpperLeft);
            introduction = Style(14, true, TextAnchor.UpperLeft);
            section = Style(9, true, TextAnchor.LowerLeft, FontStyle.Bold);
            button = Style(12, false, TextAnchor.MiddleLeft);
            scrollTrack = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
            scrollThumb = new GUIStyle { margin = new RectOffset(), padding = new RectOffset(), fixedWidth = 3 * scale };
            scrollThumb.normal.background = scrollThumb.hover.background = scrollThumb.active.background = BaseContent.WhiteTex;
            warning = UnityEngine.Resources.Load<Texture2D>("Textures/UI/Widgets/YellowWarning");
            upload = ContentFinder<Texture2D>.Get("UI/Icons/ContentSources/CanUpload");
            workshop = ContentFinder<Texture2D>.Get("UI/Icons/ContentSources/SteamWorkshop");
        }
        private GUIStyle Style(int size, bool wrap, TextAnchor alignment, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale)), wordWrap = wrap,
                alignment = alignment, fontStyle = weight, richText = true, padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
            style.normal.textColor = Color.white;
            return style;
        }
    }
}

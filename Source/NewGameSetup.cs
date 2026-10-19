using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Keep real native pages, scenario configuration chains and validation.
    // Only pages linked to our explicit New colony action are registered.
    internal static class NewGameSetup
    {
        private static readonly ConditionalWeakTable<Page, View> Views = new ConditionalWeakTable<Page, View>();
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, StorytellerDef> Story = AccessTools.FieldRefAccess<Page_SelectStoryteller, StorytellerDef>("storyteller");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, DifficultyDef> DifficultyDef = AccessTools.FieldRefAccess<Page_SelectStoryteller, DifficultyDef>("difficulty");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, Difficulty> DifficultyValues = AccessTools.FieldRefAccess<Page_SelectStoryteller, Difficulty>("difficultyValues");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, Listing_Standard> Listing = AccessTools.FieldRefAccess<Page_SelectStoryteller, Listing_Standard>("selectedStorytellerInfoListing");
        private static readonly AccessTools.FieldRef<Page_SelectScenario, Scenario> Scenario = AccessTools.FieldRefAccess<Page_SelectScenario, Scenario>("curScen");
        private static readonly AccessTools.FieldRef<Page_SelectScenario, Vector2> InfoScroll = AccessTools.FieldRefAccess<Page_SelectScenario, Vector2>("infoScrollPosition");
        private static readonly MethodInfo ScenarioList = AccessTools.Method(typeof(Page_SelectScenario), "DoScenarioSelectionList");
        private static readonly MethodInfo ScenarioEditor = AccessTools.Method(typeof(Page_SelectScenario), "GoToScenarioEditor");
        private static readonly MethodInfo Position = AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition");
        private static bool failed;
        internal static int NativeEditorDepth;
        private static readonly Color Left = Theme.Rgb(0x060A0D), Right = Theme.Rgb(0x0E1317), Footer = Theme.Rgb(0x181F25);
        private static Texture2D nextIcon;
        internal static bool Enabled => !failed && EntryScene.Active && ReframedMod.Settings.StorytellerSetup && !TutorSystem.TutorialMode;
        internal static float FitScale => Mathf.Min(1f, (UI.screenHeight - 72f) / 652f, (UI.screenWidth - 32f) / 582f);
        private static bool Target(Page page) => page != null && (page.GetType() == typeof(Page_SelectScenario) || page.GetType() == typeof(Page_SelectStoryteller) || page.GetType() == typeof(Page_CreateWorldParams));
        internal static bool Owns(Window window) => window is Page page && Views.TryGetValue(page, out _) && Enabled;

        internal static void Open()
        {
            var page = new Page_SelectScenario();
            if (Enabled) Register(page);
            Find.WindowStack.Add(page);
            if (Owns(page)) EntryMenuTransition.Open(page, "new-game", Panel(Right), 16f * page.windowRect.width / 582f);
        }
        internal static void RegisterLinked(Window window)
        {
            if (!Enabled || !(window is Page page) || !Target(page)) return;
            if (Views.TryGetValue(page, out _) || (page.prev != null && Views.TryGetValue(page.prev, out _))) Register(page);
        }
        private static void Register(Page page) => Views.GetValue(page, ignored => new View(page));
        internal static void Size(Window window)
        {
            if (!Owns(window))
            {
                if (window is Page page && Views.TryGetValue(page, out View previous)) previous.Restore(page);
                return;
            }
            float scale = FitScale;
            float width = 582f * scale, height = 652f * scale;
            Rect menu = EntryScene.MenuBounds;
            window.windowRect = new Rect(Mathf.Clamp(menu.xMax - width, 16f, UI.screenWidth - width - 16f),
                Mathf.Clamp(menu.center.y - height / 2, 12f, UI.screenHeight - height - 52f), width, height);
            window.doWindowBackground = window.drawShadow = window.doCloseButton = window.doCloseX = false;
        }
        internal static void Update(Window window)
        {
            if (!Owns(window) || !EntryMenuTransition.ReadyToClose(window)) return;
            EntryMenuTransition.Forget(window);
            Invoke((Page)window, "DoBack");
        }
        internal static bool HandleCancel(Page page)
        {
            if (!Owns(page) || !Find.WindowStack.GetsInput(page)) return false;
            if ((bool)Invoke(page, "CanDoBack")) Back(page);
            Event.current.Use();
            return true;
        }
        private static void Back(Page page)
        {
            if (page is Page_SelectScenario && page.prev == null)
            {
                if (!EntryMenuTransition.Owns(page)) EntryMenuTransition.Open(page, "new-game", Panel(Right), 16f * page.windowRect.width / 582f, false);
                EntryMenuTransition.Back(page);
            }
            else Invoke(page, "DoBack");
        }
        internal static bool Draw(Page page, Rect rect)
        {
            if (!Owns(page)) return false;
            try
            {
                View view = Views.GetValue(page, ignored => new View(page));
                bool motion = EntryMenuTransition.Owns(page);
                bool interactive = !motion || !EntryMenuTransition.BlocksInput;
                float opacity = motion ? EntryMenuTransition.BrowserOpacity : 1f;
                if (!interactive && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
                {
                    if (page is Page_SelectScenario && page.prev == null && KeyBindingDefOf.Cancel.KeyDownEvent)
                        EntryMenuTransition.Back(page);
                    Event.current.Use(); return true;
                }
                if (opacity <= 0) return true;
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Color.white; GUI.contentColor = Color.white;
                    float scale = rect.width / 582f;
                    view.Styles(scale);
                    bool clip = motion && EntryMenuTransition.PaintsShell;
                    if (clip)
                    {
                        Rect shell = EntryMenuTransition.ShellBounds;
                        Rect local = new Rect(shell.x - page.windowRect.x, shell.y - page.windowRect.y, shell.width, shell.height);
                        GUI.BeginGroup(local);
                        GUI.BeginGroup(new Rect(-local.x, -local.y, rect.width, rect.height));
                    }
                    try
                    {
                        // During the last content-fade frames the root morph owns the shell.
                        // At rest the three disjoint regions keep the same .96 opacity.
                        if (!clip) DrawShell(rect, scale);
                        GUI.enabled &= interactive;
                        if (page is Page_SelectScenario scenario)
                            view.ScenarioView.Draw(new Rect(0, 0, rect.width, 588f * scale), scenario, ref Scenario(scenario), opacity);
                        else if (page is Page_SelectStoryteller story)
                            view.StoryView.Draw(new Rect(0, 0, rect.width, 588f * scale), ref Story(story), ref DifficultyDef(story), DifficultyValues(story),
                                () => Find.WindowStack.Add(new NativeDifficultyEditor(story)), opacity);
                        else if (page is Page_CreateWorldParams world)
                            view.WorldView.Draw(new Rect(0, 0, rect.width, 588f * scale), world, opacity);
                        DrawFooter(page, rect, scale, opacity, view);
                    }
                    finally { if (clip) { GUI.EndGroup(); GUI.EndGroup(); } }
                }
                return true;
            }
            catch (Exception error)
            {
                failed = true;
                EntryMenuTransition.Forget(page);
                foreach (Window window in Find.WindowStack.Windows)
                    if (window is Page known && Views.TryGetValue(known, out View old)) old.Restore(known);
                Position.Invoke(page, null);
                Log.Error("[Reframed UI] New-game presentation failed; native setup resumes. " + error);
                return true;
            }
        }

        private static Color Panel(Color color) { color.a = Theme.EntryPanelOpacity; return color; }
        internal static void DrawTransitionShell(Rect shell, float progress, float radius)
        {
            GUI.BeginGroup(shell);
            try
            {
                Rect whole = new Rect(0, 0, shell.width, shell.height);
                float split = Pixel(shell.width * 273f / 582f), footer = Pixel(shell.height * 588f / 652f);
                DrawShellRegion(whole, new Rect(0, 0, split, footer), Color.Lerp(EntryScene.PanelColor, Left, progress), radius / 16f);
                DrawShellRegion(whole, new Rect(split, 0, whole.width - split, footer), Color.Lerp(EntryScene.PanelColor, Right, progress), radius / 16f);
                DrawShellRegion(whole, new Rect(0, footer, whole.width, whole.height - footer), Color.Lerp(EntryScene.PanelColor, Footer, progress), radius / 16f);
            }
            finally { GUI.EndGroup(); }
        }
        private static void DrawShell(Rect rect, float scale)
        {
            float split = Pixel(273f * scale), footer = Pixel(588f * scale);
            DrawShellRegion(rect, new Rect(0, 0, split, footer), Left, scale);
            DrawShellRegion(rect, new Rect(split, 0, rect.width - split, footer), Right, scale);
            DrawShellRegion(rect, new Rect(0, footer, rect.width, rect.height - footer), Footer, scale);
        }
        private static float Pixel(float value) => Mathf.Round(value * Prefs.UIScale) / Prefs.UIScale;
        private static void DrawShellRegion(Rect whole, Rect region, Color color, float scale)
        {
            GUI.BeginGroup(region);
            try { Theme.RoundRect(new Rect(-region.x, -region.y, whole.width, whole.height), Panel(color), 16f * scale); }
            finally { GUI.EndGroup(); }
        }
        private static void DrawScenario(Page_SelectScenario page, Rect rect, float scale, float opacity, View view)
        {
            Ink(new Rect(17 * scale, 16 * scale, 239 * scale, 32 * scale), page.PageTitle, view.Heading, Color.white, opacity);
            Ink(new Rect(17 * scale, 49 * scale, 239 * scale, 54 * scale), "Choose your colony's starting story.", view.Description, Theme.Muted, opacity);
            // Native list preserves custom/Workshop grouping, disabled scenarios,
            // version checks and confirmed deletion/unsubscribe without copying that logic.
            using (new Theme.GuiScope(true))
            {
                GUI.color = new Color(1, 1, 1, opacity);
                ScenarioList.Invoke(page, new object[] { new Rect(12 * scale, 108 * scale, 249 * scale, 466 * scale) });
                ScenarioUI.DrawScenarioInfo(new Rect(289 * scale, 20 * scale, 276 * scale, 548 * scale), Scenario(page), ref InfoScroll(page));
            }
        }
        private static void DrawFooter(Page page, Rect rect, float scale, float opacity, View view)
        {
            GUI.color = new Color(1, 1, 1, opacity);
            Widgets.DrawBoxSolid(new Rect(0, 588 * scale, rect.width, scale), Theme.Rgb(0x313B4A));
            bool back = Button(new Rect(16 * scale, 604 * scale, 32 * scale, 32 * scale), null, SaveBrowserIcons.Back, false, view, opacity);
            // Native CanDoNext emits messages and mutates setup state. Only call it
            // on acceptance; inspect the actual choices for the visual/input gate.
            bool choicesComplete = !(page is Page_SelectStoryteller storyteller) ||
                (Story(storyteller) != null && DifficultyDef(storyteller) != null && Find.GameInitData?.permadeathChosen == true);
            bool worldPage = page is Page_CreateWorldParams;
            float nextWidth = worldPage ? 112f : 81f;
            bool next = Button(new Rect(rect.width - (nextWidth + 16) * scale, 604 * scale, nextWidth * scale, 32 * scale),
                (worldPage ? "WorldGenerate" : "Next").Translate(), null, true, view, opacity, choicesComplete);
            if (page is Page_SelectScenario scenario)
            {
                if (Button(new Rect(143 * scale, 604 * scale, 280 * scale, 32 * scale), "ScenarioEditor".Translate(), null, false, view, opacity))
                { ScenarioEditor.Invoke(scenario, null); return; }
            }
            else if (page is Page_CreateWorldParams world)
            {
                if (Button(new Rect(64 * scale, 604 * scale, 124 * scale, 32 * scale), "ResetAll".Translate(), null, false, view, opacity)) world.Reset();
            }
            else Ink(new Rect(60 * scale, 601 * scale, 410 * scale, 38 * scale), "CanChangeStorytellerSettingsDuringPlay".Translate(), view.Note, Theme.Muted, opacity);
            if (!GUI.enabled) return;
            back |= KeyBindingDefOf.Cancel.KeyDownEvent;
            bool accept = KeyBindingDefOf.Accept.KeyDownEvent;
            if (accept && !choicesComplete) Event.current.Use();
            next |= accept && choicesComplete;
            if (back && (bool)Invoke(page, "CanDoBack"))
            {
                Event.current.Use();
                Back(page);
            }
            else if (next && (bool)Invoke(page, "CanDoNext"))
            { Event.current.Use(); Invoke(page, "DoNext"); }
        }
        private static object Invoke(Page page, string method) => AccessTools.Method(page.GetType(), method).Invoke(page, null);
        private static void Ink(Rect rect, string text, GUIStyle style, Color color, float opacity)
        {
            using (new Theme.GuiScope(true)) { GUI.enabled = true; color.a *= opacity; GUI.color = color; GUI.Label(rect, text, style); }
        }
        private static bool Button(Rect rect, string text, Texture2D icon, bool positive, View view, float opacity, bool available = true)
        {
            using (new Theme.GuiScope(true))
            {
                bool interactive = GUI.enabled && available;
                // Drawing uses transition opacity; disabled input must not dim labels early.
                GUI.enabled = true;
                GUI.color = new Color(1, 1, 1, opacity);
                bool hover = interactive && Mouse.IsOver(rect);
                Theme.ButtonFace(rect, !available ? Theme.Surface : positive ? Theme.Rgb(hover ? 0x17673Au : 0x0D542Bu) : hover ? Theme.Hover : Theme.Surface, !available);
                GUI.color = available ? new Color(1, 1, 1, opacity) : new Color(.45f, .51f, .55f, opacity);
                if (icon != null)
                {
                    GUI.color = new Color(.73f, .81f, .85f, opacity);
                    GUI.DrawTexture(rect.ContractedBy(rect.height / 4), icon, ScaleMode.ScaleToFit);
                }
                else if (positive)
                {
                    GUI.Label(new Rect(rect.x, rect.y, rect.width * .73f, rect.height), text, view.Button);
                    if (nextIcon == null) nextIcon = ContentFinder<Texture2D>.Get("Reframed/NewGame/Next", true);
                    GUI.DrawTexture(new Rect(rect.xMax - rect.height * .7f, rect.y + rect.height * .28f, rect.height * .44f, rect.height * .44f), nextIcon, ScaleMode.ScaleToFit);
                }
                else GUI.Label(rect, text, view.Button);
                GUI.enabled = interactive;
                return interactive && Widgets.ButtonInvisible(rect);
            }
        }
        private sealed class View
        {
            internal readonly NewGameStorytellerView StoryView = new NewGameStorytellerView();
            internal readonly NewGameScenarioView ScenarioView = new NewGameScenarioView();
            internal readonly NewGameWorldView WorldView = new NewGameWorldView();
            private readonly bool background, shadow, close, closeX;
            private float lastScale;
            internal GUIStyle Heading, Description, Note, Button;
            internal View(Page page) { background = page.doWindowBackground; shadow = page.drawShadow; close = page.doCloseButton; closeX = page.doCloseX; }
            internal void Restore(Page page) { page.doWindowBackground = background; page.drawShadow = shadow; page.doCloseButton = close; page.doCloseX = closeX; }
            internal void Styles(float scale)
            {
                Text.Font = GameFont.Small;
                if (Heading != null && lastScale == scale && Heading.font == Text.CurFontStyle.font) return;
                lastScale = scale;
                Heading = Style(16, scale, TextAnchor.MiddleLeft, false);
                Description = Style(14, scale, TextAnchor.UpperLeft, true);
                Note = Style(12, scale, TextAnchor.MiddleCenter, true);
                Button = Style(14, scale, TextAnchor.MiddleCenter, false);
            }
            private static GUIStyle Style(int size, float scale, TextAnchor anchor, bool wrap)
            {
                var style = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.RoundToInt(size * scale), alignment = anchor, wordWrap = wrap,
                    padding = new RectOffset(), margin = new RectOffset() };
                style.normal.textColor = Color.white; return style;
            }
        }
        private sealed class NativeDifficultyEditor : Window
        {
            private readonly Page_SelectStoryteller page;
            internal NativeDifficultyEditor(Page_SelectStoryteller page) { this.page = page; doCloseButton = true; absorbInputAroundWindow = true; closeOnAccept = false; }
            public override Vector2 InitialSize => new Vector2(Mathf.Min(1020, UI.screenWidth - 24), Mathf.Min(764, UI.screenHeight - 24));
            public override void DoWindowContents(Rect rect)
            {
                NativeEditorDepth++;
                try { StorytellerUI.DrawStorytellerSelectionInterface(new Rect(0, 0, rect.width, rect.height - 55), ref Story(page), ref DifficultyDef(page), ref DifficultyValues(page), Listing(page)); }
                finally { NativeEditorDepth--; }
            }
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class NewGameRegisterPatch { private static void Prefix(Window __0) => NewGameSetup.RegisterLinked(__0); }
    [HarmonyPatch(typeof(Page), nameof(Page.OnCancelKeyPressed))]
    internal static class NewGameCancelPatch { private static bool Prefix(Page __instance) => !NewGameSetup.HandleCancel(__instance); }
    [HarmonyPatch(typeof(Window), "SetInitialSizeAndPosition")]
    internal static class NewGameSizePatch { private static void Postfix(Window __instance) => NewGameSetup.Size(__instance); }
    [HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
    internal static class NewGameLayoutPatch
    {
        private static void Prefix(Window __instance)
        {
            // Back adds the previous page before closing the world/site screen.
            // Its initial sizing therefore runs while WorldSelected is still true.
            // Reapply owned layout before GUI.Window computes its local rectangle
            // and paints native chrome, including the first frame after returning.
            if (NewGameSetup.Owns(__instance)) NewGameSetup.Size(__instance);
        }
    }
    [HarmonyPatch(typeof(Window), "get_Margin")]
    internal static class NewGameMarginPatch
    { private static bool Prefix(Window __instance, ref float __result) { if (!NewGameSetup.Owns(__instance)) return true; __result = 0; return false; } }
    [HarmonyPatch(typeof(Window), nameof(Window.WindowUpdate))]
    internal static class NewGameUpdatePatch { private static void Postfix(Window __instance) => NewGameSetup.Update(__instance); }
    [HarmonyPatch]
    internal static class NewGameContentsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        { yield return AccessTools.Method(typeof(Page_SelectScenario), "DoWindowContents"); yield return AccessTools.Method(typeof(Page_SelectStoryteller), "DoWindowContents"); yield return AccessTools.Method(typeof(Page_CreateWorldParams), "DoWindowContents"); }
        private static bool Prefix(Page __instance, Rect __0) => !NewGameSetup.Draw(__instance, __0);
    }
}

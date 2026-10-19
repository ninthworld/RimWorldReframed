using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace ReframedUI
{
    // Full-scene film coordinates, independent of the enlarged presentation board.
    [StaticConstructorOnStartup]
    internal static class EntryScene
    {
        private static readonly Texture2D Planet = ContentFinder<Texture2D>.Get("UI/HeroArt/BGPlanet");
        private static readonly Texture2D Wordmark = ContentFinder<Texture2D>.Get("UI/HeroArt/GameTitle");
        private static readonly Texture2D Publisher = ContentFinder<Texture2D>.Get("UI/HeroArt/LudeonLogoSmall");
        private static readonly Texture2D Steam = ContentFinder<Texture2D>.Get("UI/Icons/ContentSources/SteamWorkshop");
        private static GUIStyle footerStyle, languageStyle;
        private static int styleSize;
        internal static bool Failed;
        internal static bool Active => !Failed && ReframedMod.PatchesReady && ReframedMod.Settings != null &&
            ReframedMod.Settings.Enabled && ReframedMod.Settings.MainMenu &&
            Current.ProgramState == ProgramState.Entry && GenScene.InEntryScene &&
            Find.UIRoot != null && Find.WindowStack != null && !LongEventHandler.AnyEventNowOrWaiting &&
            !WorldRendererUtility.WorldSelected && UI.screenWidth >= 680 && UI.screenHeight >= 480;
        // The native entry root skips MainMenuOnGUI for Dialog-layer windows.
        // A language picker overlays the existing scene rather than replacing it;
        // other dialogs retain their native hiding behavior.
        internal static bool LanguageBackdropVisible
        {
            get
            {
                if (!Active) return false;
                bool languageOpen = false;
                WindowStack stack = Find.WindowStack;
                for (int i = 0; i < stack.Count; i++)
                {
                    Window window = stack[i];
                    if (window is EntryLanguagePicker) languageOpen = true;
                    else if (window.absorbInputAroundWindow ||
                        (window.layer == WindowLayer.Dialog && !window.IsDebug)) return false;
                }
                return languageOpen;
            }
        }
        private const float SceneAnchorX = 1156f, SceneAnchorY = 362.3333f;
        private const float FooterAnchorX = 18f, FooterAnchorY = 701f;
        private static float AnchorScale => Mathf.Min(UI.screenWidth / 1280f, UI.screenHeight / 720f);
        private static Vector2 SceneAnchor => new Vector2(UI.screenWidth - 124f * AnchorScale,
            UI.screenHeight * .5f + (SceneAnchorY - 360f) * AnchorScale);
        private static Vector2 FooterAnchor => new Vector2(FooterAnchorX * AnchorScale,
            UI.screenHeight - 19f * AnchorScale);

        // Reference geometry is calibrated at 1920x1080 / native UI scale 1.5.
        // Unity already applies Prefs.UIScale to these logical units. Only anchors
        // follow viewport size; scaling controls by resolution also would shrink
        // text below readable sizes on smaller supported displays.
        internal static float Scale
        {
            get
            {
                const float scale = 1f;
                Vector2 scene = SceneAnchor, footer = FooterAnchor;
                float margin = Mathf.Max(4f, 8f * AnchorScale);
                // Includes the user's 25% larger menu/logo and relocated strip.
                // Browser spans x525.5..1208, y128.5833..596.0833;
                // logo reaches x1210.5.
                // Cap only if those
                // related surfaces or the complete footer would leave the screen.
                float fit = Mathf.Min((scene.x - margin) / (SceneAnchorX - 525.5f),
                    (UI.screenWidth - margin - scene.x) / (1210.5f - SceneAnchorX));
                fit = Mathf.Min(fit, (scene.y - margin) / (SceneAnchorY - 128.5833f));
                fit = Mathf.Min(fit, (UI.screenHeight - margin - scene.y) / (596.0834f - SceneAnchorY));
                fit = Mathf.Min(fit, (UI.screenWidth - margin - footer.x) / (336f - FooterAnchorX));
                fit = Mathf.Min(fit, (footer.y - margin) / (FooterAnchorY - 668f));
                return Mathf.Min(scale, Mathf.Max(.01f, fit));
            }
        }
        internal static Rect Map(float x, float y, float width, float height)
        {
            float s = Scale;
            // Explicit authored groups: bottom-left publisher/account/language
            // footer (y>=650); right-center menu/logo/DLC/card/browser otherwise.
            // Transform each group about one anchor so its child gaps stay intact.
            bool footer = y >= 650f;
            Vector2 anchor = footer ? FooterAnchor : SceneAnchor;
            float designX = footer ? FooterAnchorX : SceneAnchorX;
            float designY = footer ? FooterAnchorY : SceneAnchorY;
            return new Rect(anchor.x + (x - designX) * s,
                anchor.y + (y - designY) * s, width * s, height * s);
        }
        internal static Color PanelColor { get { var color = Theme.Rgb(DesignTokens.Panel); color.a = Theme.EntryPanelOpacity; return color; } }
        internal static float MenuScale => Scale * 1.25f;
        // User-requested 50% increase over the already enlarged browser.
        internal static float BrowserScale => Scale * 1.875f;
        internal static Rect BrowserBounds => new Rect(MenuBounds.xMax - 364f * BrowserScale,
            MenuBounds.center.y - 124.66665f * BrowserScale, 364f * BrowserScale, 249.3333f * BrowserScale);
        internal static Rect MenuBounds
        {
            get
            {
                Rect original = Map(948, 289, 208, 146.6667f);
                float height = 146.6667f * MenuScale;
                return new Rect(original.x, original.center.y - height * .5f, 208f * MenuScale, height);
            }
        }
        internal static Rect DlcStripBounds => new Rect(MenuBounds.center.x - 78f * Scale,
            MenuBounds.yMax + 12.3333f * Scale, 156f * Scale, 34f * Scale);
        internal static Rect DlcCardBounds => new Rect(MenuBounds.x - 390f * Scale,
            DlcStripBounds.yMax - 270f * Scale, 380f * Scale, 270f * Scale);
        internal static void DrawBackgroundAndFooter(bool drawPlanet = true)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                if (drawPlanet)
                    GUI.DrawTexture(new Rect(0, 0, UI.screenWidth, UI.screenHeight), Planet, ScaleMode.ScaleAndCrop, true);
                EntryMenuTransition.DrawRoot();
                if (LanguageBackdropVisible) ReframedMainMenu.DrawLanguageBackdrop();
                DrawFooter();
            }
        }
        internal static void DrawWordmark(float opacity = 1f)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = new Color(1, 1, 1, .88f * Mathf.Clamp01(opacity));
                Rect menu = MenuBounds;
                GUI.DrawTexture(new Rect(menu.x, menu.y - 45f * MenuScale, 210f * MenuScale, 30f * MenuScale),
                    Wordmark, ScaleMode.ScaleToFit, true);
            }
        }
        private static void DrawFooter()
        {
            Text.Font = GameFont.Small;
            int size = Mathf.Max(7, Mathf.RoundToInt(8 * Scale));
            if (footerStyle == null || footerStyle.font != Text.CurFontStyle.font || styleSize != size)
            {
                styleSize = size;
                footerStyle = new GUIStyle(Text.CurFontStyle) { fontSize = size, fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip,
                    padding = new RectOffset(), margin = new RectOffset() };
                footerStyle.normal.textColor = Color.white;
                languageStyle = new GUIStyle(footerStyle) { fontSize = Mathf.Max(8, Mathf.RoundToInt(9 * Scale)) };
            }
            bool available = GUI.enabled && !EntryMenuTransition.BlocksInput && !Find.WindowStack.AnyWindowAbsorbingAllInput;
            Rect publisher = Map(18, 670, 100, 29);
            GUI.color = new Color(.75f, .84f, .89f, .7f);
            GUI.DrawTexture(publisher, Publisher, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
            if (available && ReframedMainMenu.Focused(8))
                Widgets.DrawBoxSolid(new Rect(publisher.x, publisher.yMax, publisher.width, Scale), Theme.Ink);
            TooltipHandler.TipRegion(publisher, "Ludeon Studios · Links");
            if (available && Widgets.ButtonInvisible(publisher)) ReframedMainMenu.Links();
            Color dark = Theme.Rgb(0x0D1318); dark.a = .9f;
            Theme.RoundRect(Map(128, 668, 208, 33), dark, 4 * Scale);
            Rect steam = Map(132, 675, 20, 20);
            Theme.RoundRect(steam, Theme.Rgb(0x174D7B), 10 * Scale);
            GUI.DrawTexture(steam, Steam, ScaleMode.ScaleToFit, true);
            string account = SteamManager.Initialized ? "Logged as " + SteamUtility.SteamPersonaName : "Steam offline";
            GUI.color = Theme.Rgb(0x94A2AB);
            GUI.Label(Map(158, 674, 104, 11), account, footerStyle);
            GUI.Label(Map(158, 685, 104, 11), "Version " + VersionControl.CurrentVersionString + (UnityData.Is64BitBuild ? " (64-bit)" : ""), footerStyle);
            TooltipHandler.TipRegion(Map(154, 670, 108, 30), account + "\n" + VersionControl.CurrentVersionStringWithRev);
            GUI.color = Color.white;
            Rect language = Map(266, 673, 65, 23);
            Theme.ButtonFace(language, available && Mouse.IsOver(language) ? Theme.Hover : Theme.Surface, radius: 3 * Scale);
            if (available && ReframedMainMenu.Focused(7))
                Widgets.DrawBoxSolid(new Rect(language.x + 3 * Scale, language.yMax - 2 * Scale, language.width - 6 * Scale, Scale), Theme.Ink);
            Texture2D flag = LanguageDatabase.activeLanguage.icon;
            if (flag != null) GUI.DrawTexture(Map(273, 679, 12, 10), flag, ScaleMode.ScaleToFit, true);
            GUI.Label(Map(290, 673, 39, 23), LanguageDatabase.activeLanguage.FriendlyNameNative, languageStyle);
            TooltipHandler.TipRegion(language, LanguageDatabase.activeLanguage.FriendlyNameNative);
            if (available && Widgets.ButtonInvisible(language)) ReframedMainMenu.Languages();
        }
    }

    [HarmonyPatch(typeof(UI_BackgroundMain), nameof(UI_BackgroundMain.BackgroundOnGUI))]
    internal static class EntryBackgroundPatch
    {
        private static bool Prefix(out bool __state)
        {
            __state = false;
            if (!EntryScene.Active) return true;
            // Let the game draw its selected/random expansion artwork, including
            // native background hooks. The postfix adds only our foreground.
            if (ReframedMod.Settings.UseOriginalMenuBackground) { __state = true; return true; }
            try { EntryScene.DrawBackgroundAndFooter(); return false; }
            catch (Exception error)
            {
                EntryScene.Failed = true;
                Log.Error("[Reframed UI] Entry composition failed; native composition resumes. " + error);
                return true;
            }
        }
        private static void Postfix(bool __state)
        {
            if (!__state || !EntryScene.Active) return;
            try { EntryScene.DrawBackgroundAndFooter(false); }
            catch (Exception error)
            {
                EntryScene.Failed = true;
                Log.Error("[Reframed UI] Entry foreground failed; native composition resumes. " + error);
            }
        }
    }

    internal sealed class EntryTranslationInfo : Window
    {
        public override Vector2 InitialSize => new Vector2(350, 470);
        internal EntryTranslationInfo() { doCloseX = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect rect) => MainMenuDrawer.DoTranslationInfoRect(rect);
    }
}

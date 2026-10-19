using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Entry-scene composition. EscMenuPanel separately presents the native colony menu.
    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    internal static class ReframedMainMenu
    {
        private static readonly Action Tutorial = (Action)Delegate.CreateDelegate(typeof(Action),
            AccessTools.Method(typeof(MainMenuDrawer), "InitLearnToPlay"));
        private static readonly string[] Icons = { "menu.plus", "menu.load", "menu.scroll", "menu.cog", "menu.briefcase", "menu.book", "menu.exit" };
        private static readonly string[] Keys = { "NewColony", "LoadGame", "Tutorial", "Options", "Mods", "Credits", "QuitToOS" };
        private static int keyboardFocus = -1;
        private static GUIStyle buttonLabel;
        private static Texture2D divider;
        private static bool failed;
        internal static bool Focused(int command) => keyboardFocus == command;

        private static bool Prefix()
        {
            if (failed || !EntryScene.Active) return true;
            try
            {
                // Keep native/foreign version-hook side effects (including mod
                // load error dialogs), while the footer owns their visible text.
                using (new Theme.GuiScope(true))
                {
                    GUI.BeginClip(new Rect(-10000, -10000, 1, 1));
                    try { VersionControl.DrawInfoInCorner(); }
                    finally { GUI.EndClip(); }
                }
                EntryScene.DrawWordmark();
                Draw();
                MainMenuDrawer.DoExpansionIcons();
                Verse.Steam.SteamDeck.ShowSteamDeckMainMenuControlsIfNotKnown();
            }
            catch (Exception error)
            {
                failed = true;
                EntryScene.Failed = true;
                Log.Error("[Reframed UI] Entry menu failed; native menu resumes next frame. " + error);
            }
            return false;
        }

        internal static void DrawTransitionContent(float opacity)
        {
            if (opacity > 0) Draw(opacity, false, false);
        }

        internal static void DrawLanguageBackdrop()
        {
            EntryScene.DrawWordmark();
            Draw(1f, false);
            // Preserve the existing expansion selection and card. The custom
            // panel also sees the modal input gate; native fallback must not
            // receive controls behind the picker either.
            using (new Theme.GuiScope(true))
            {
                GUI.enabled = false;
                MainMenuDrawer.DoExpansionIcons();
            }
        }

        private static void Draw(float opacity = 1f, bool interactive = true, bool shell = true)
        {
            int command = -1;
            bool available = false;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.MiddleLeft;
                if (buttonLabel == null || buttonLabel.font != Text.CurFontStyle.font)
                {
                    buttonLabel = new GUIStyle(Text.CurFontStyle) { fontSize = 14, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                    buttonLabel.normal.textColor = Color.white;
                }
                bool visuallyEnabled = !interactive || (GUI.enabled && !Find.WindowStack.AnyWindowAbsorbingAllInput);
                // Motion temporarily blocks input; it does not disable the commands visually.
                available = interactive && visuallyEnabled && !EntryMenuTransition.BlocksInput && !ReframedDlcPanel.Transitioning;
                if (!interactive) GUI.enabled = true; // Paint the outgoing enabled state without registering controls.
                // The full-scene film is 1280x720; the 312x220 board is an
                // enlarged component crop. The user requested a 25% larger
                // menu baseline; native UI scale still multiplies it normally.
                Rect bounds = EntryScene.MenuBounds;
                float scale = EntryScene.MenuScale * (2f / 3f);
                GUI.matrix *= Matrix4x4.TRS(new Vector3(bounds.x, bounds.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                const float x = 0, y = 0;
                if (shell) Theme.RoundRect(new Rect(0, 0, 312, 220), EntryScene.PanelColor, 16f);
                GUI.color = new Color(1, 1, 1, opacity);
                DrawDivider(new Rect(16, 115, 280, 1));
                GUI.color = Color.white;
                command = KeyboardCommand(available && !ReframedDlcPanel.CardOpen);
                for (int i = 0; i < Keys.Length; i++)
                {
                    Rect button;
                    if (i == 0) button = new Rect(x + 16, y + 16, 280, 44);
                    else
                    {
                        int pair = (i - 1) / 2;
                        button = new Rect(x + 16 + ((i - 1) % 2) * 144,
                            y + (pair == 0 ? 67 : 132 + (pair - 1) * 40), 136, 32);
                    }
                    string label = Keys[i].Translate();
                    if (LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage)
                    {
                        if (i == 1) label = "Load";
                        if (i == 6) label = "Quit";
                    }
                    if (Button(button, label, Icons[i], i == 0, i == 6, visuallyEnabled,
                        interactive && keyboardFocus == i, opacity, available))
                    {
                        keyboardFocus = -1;
                        command = i;
                    }
                }
            }
            // Native Window.PreOpen and page constructors run in the root GUI space.
            if (command >= 0 && available) Invoke(command);
        }

        private static void DrawDivider(Rect rect)
        {
            if (divider == null)
            {
                divider = new Texture2D(256, 1, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[256];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color ink = Theme.Rgb(0x27323B);
                    float edgeDistance = Mathf.Min(i, pixels.Length - 1 - i) / 127.5f;
                    ink.a = Mathf.SmoothStep(0, 1, edgeDistance);
                    pixels[i] = ink;
                }
                divider.SetPixels(pixels); divider.Apply(false, true);
            }
            GUI.DrawTexture(rect, divider);
        }

        private static bool Button(Rect rect, string label, string icon, bool centered, bool destructive, bool enabled, bool focused,
            float opacity = 1f, bool interactive = true)
        {
            bool hover = interactive && enabled && Mouse.IsOver(rect);
            Color face = destructive ? Theme.Rgb(hover ? 0xA0292Cu : DesignTokens.Destructive) : hover ? Theme.Hover : Theme.Surface;
            GUI.color = Color.white;
            Theme.ButtonFace(rect, face, !enabled, opacity: opacity);
            if (focused && enabled) Widgets.DrawBoxSolid(new Rect(rect.x + 4, rect.yMax - 2, rect.width - 8, 1), Theme.Rgb(DesignTokens.Secondary));
            float labelWidth = buttonLabel.CalcSize(new GUIContent(label)).x;
            float iconSpace = icon == null ? 0 : 26;
            float start = centered ? rect.x + Mathf.Max(8, (rect.width - labelWidth - iconSpace) / 2) : rect.x + 12;
            if (icon != null)
            {
                Texture2D texture = MainMenuIcons.Get(icon);
                Color ink = enabled ? Color.white : Theme.Muted; ink.a *= opacity; GUI.color = ink;
                if (texture != null) GUI.DrawTexture(new Rect(start, rect.center.y - 8, 16, 16), texture, ScaleMode.ScaleToFit, true);
            }
            Color text = enabled ? Color.white : Theme.Muted; text.a *= opacity; GUI.color = text;
            GUI.Label(new Rect(start + iconSpace, rect.y, Mathf.Max(1, rect.xMax - start - iconSpace - 6), rect.height), label, buttonLabel);
            GUI.color = Color.white;
            if (interactive && labelWidth > rect.xMax - start - iconSpace - 6) TooltipHandler.TipRegion(rect, label);
            return interactive && enabled && Widgets.ButtonInvisible(rect);
        }

        private static int KeyboardCommand(bool available)
        {
            var ev = Event.current;
            if (!available || ev.type != EventType.KeyDown) return -1;
            if (ev.keyCode == KeyCode.Tab || ev.keyCode == KeyCode.DownArrow || ev.keyCode == KeyCode.UpArrow)
            {
                int direction = ev.shift || ev.keyCode == KeyCode.UpArrow ? -1 : 1;
                int[] order = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
                int index = Array.IndexOf(order, keyboardFocus);
                keyboardFocus = order[index < 0 ? (direction > 0 ? 0 : 8) : (index + direction + 9) % 9];
                ev.Use();
            }
            else if (keyboardFocus >= 0 && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Space))
            {
                ev.Use();
                return keyboardFocus;
            }
            else if (ev.keyCode == KeyCode.Escape) { keyboardFocus = -1; ev.Use(); }
            return -1;
        }

        private static void Invoke(int command)
        {
            if (command != 7) ReframedDlcPanel.Close();
            if (Prefs.DevMode) Log.Message("[Reframed UI] Entry command: " + (command < Keys.Length ? Keys[command] : command == 7 ? "Language" : "Links"));
            switch (command)
            {
                case 0: NewGameSetup.Open(); break;
                case 1: ReframedSaveBrowser.Open(); break;
                case 2: Tutorial(); break;
                case 3: Find.WindowStack.Add(new Dialog_Options()); break;
                case 4: Find.WindowStack.Add(new Page_ModsConfig()); break;
                case 5: Find.WindowStack.Add(new Screen_Credits()); break;
                case 6: Root.Shutdown(); break;
                case 7: Languages(); break;
                case 8: Links(); break;
            }
        }

        internal static void Languages()
        {
            EntryLanguagePicker.Open();
        }

        internal static void Links()
        {
            ReframedDlcPanel.Close();
            var options = new List<FloatMenuOption>();
            Link(options, "FictionPrimer", "https://rimworldgame.com/backstory");
            Link(options, "LudeonBlog", "https://ludeon.com/blog");
            Link(options, "Subreddit", "https://www.reddit.com/r/RimWorld/");
            Link(options, "OfficialWiki", "https://rimworldwiki.com");
            Link(options, "TynansX", "https://x.com/TynanSylvester");
            Link(options, "TynansDesignBook", "https://tynansylvester.com/book");
            Link(options, "HelpTranslate", "https://rimworldgame.com/helptranslate");
            if (LanguageDatabase.activeLanguage != LanguageDatabase.defaultLanguage || DebugSettings.enableTranslationWindowInEnglish)
                options.Add(new FloatMenuOption("Translation information", () => Find.WindowStack.Add(new EntryTranslationInfo())));
            options.Add(new FloatMenuOption("BuySoundtrack".Translate(), Soundtracks));
            if (Prefs.DevMode) options.Add(new FloatMenuOption("DevQuickTest".Translate(), () =>
                LongEventHandler.QueueLongEvent(() => { Root_Play.SetupForQuickTestPlay(); PageUtility.InitGameStart(); },
                    "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Soundtracks()
        {
            var options = new List<FloatMenuOption>();
            Link(options, "BuySoundtrack_Classic", "https://store.steampowered.com/app/990430/RimWorld_Soundtrack/");
            Link(options, "BuySoundtrack_Royalty", "https://store.steampowered.com/app/1244270/RimWorld__Royalty_Soundtrack/");
            Link(options, "BuySoundtrack_Anomaly", "https://store.steampowered.com/app/2914900/RimWorld__Anomaly_Soundtrack/");
            Link(options, "BuySoundtrack_Odyssey", "https://store.steampowered.com/app/3689230/RimWorld__Odyssey_Soundtrack/");
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Link(List<FloatMenuOption> options, string key, string url) =>
            options.Add(new FloatMenuOption(key.Translate(), () => Application.OpenURL(url)));
    }
}

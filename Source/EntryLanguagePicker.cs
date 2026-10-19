using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Compact entry-scene language picker. Language names and flags come from
    // the loaded native LanguageDefs; no language-specific assets are copied.
    internal sealed class EntryLanguagePicker : Window
    {
        private const string SearchControl = "ReframedEntryLanguageSearch";
        private const float BaseWidth = 208f;
        private const float BaseHeight = 320f;
        private const float BaseRowHeight = 22f;
        private const float MotionDuration = .22f;
        private const uint ShellColor = 0x0D1318;
        private const uint HeaderColor = 0x080D11;
        private const uint RuleColor = 0x27323B;
        private const uint RowHoverColor = 0x17232B;
        private const uint RowSelectedColor = 0x1C2B34;
        private const uint ScrollTrackColor = 0x10171C;
        private const uint ScrollThumbColor = 0x23323D;

        private readonly List<LoadedLanguage> languages = new List<LoadedLanguage>();
        private readonly List<LoadedLanguage> visible = new List<LoadedLanguage>();
        private readonly Dictionary<LoadedLanguage, FlagInfo> flags = new Dictionary<LoadedLanguage, FlagInfo>();
        private string query = string.Empty;
        private string lastQuery;
        private Vector2 scroll;
        private GUIStyle titleStyle;
        private GUIStyle rowStyle;
        private GUIStyle searchStyle;
        private GUIStyle placeholderStyle;
        private static GUIStyle scrollTrack;
        private static GUIStyle scrollThumb;
        private static Texture2D trackTexture;
        private static Texture2D thumbTexture;
        private int styleFontSize = -1;
        private Font styleFont;
        private bool opening;
        private bool closing;
        private bool closeSound = true;
        private float motionProgress = 1f;
        private float motionStart;
        private float closeFrom;
        private LoadedLanguage pendingSelection;

        private sealed class FlagInfo
        {
            internal Texture2D Texture;
            internal string SourcePath;
            internal bool Owned;
        }

        internal static void Open()
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null || stack.IsOpen<EntryLanguagePicker>()) return;
            stack.Add(new EntryLanguagePicker());
        }

        private EntryLanguagePicker()
        {
            foreach (LoadedLanguage language in LanguageDatabase.AllLoadedLanguages)
                if (language != null) languages.Add(language);
            RebuildVisible();

            doCloseButton = false;
            doCloseX = false;
            doWindowBackground = false;
            drawShadow = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = false;
            draggable = false;
        }

        public override void PostClose()
        {
            foreach (FlagInfo flag in flags.Values)
                if (flag.Owned && flag.Texture != null && flag.Texture != BaseContent.BadTex)
                    UnityEngine.Object.Destroy(flag.Texture);
            flags.Clear();
            base.PostClose();
        }

        public override Vector2 InitialSize => new Vector2(PopupWidth(), PopupHeight());
        protected override float Margin => 0f;

        protected override void SetInitialSizeAndPosition()
        {
            Rect footer = FooterBounds();
            float height = PopupHeight();
            windowRect = new Rect(footer.xMin, footer.yMin - height, PopupWidth(), height);
        }

        public override void PostOpen()
        {
            motionProgress = 0f;
            opening = true;
            closing = false;
            motionStart = Time.unscaledTime;
            ApplyAnimatedBounds();
            base.PostOpen();
            EntryMotionEvidence.Start("language-open", MotionState);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!opening && !closing) return;

            float elapsed = Mathf.Clamp01((Time.unscaledTime - motionStart) / MotionDuration);
            float eased = elapsed * elapsed * (3f - 2f * elapsed);
            if (opening)
            {
                motionProgress = eased;
                if (elapsed >= 1f)
                {
                    motionProgress = 1f;
                    opening = false;
                }
                ApplyAnimatedBounds();
                return;
            }

            motionProgress = closeFrom * (1f - eased);
            if (elapsed >= 1f)
            {
                motionProgress = 0f;
                ApplyAnimatedBounds();
                LoadedLanguage selected = pendingSelection;
                pendingSelection = null;
                if (selected != null)
                {
                    try
                    {
                        LanguageDatabase.SelectLanguage(selected);
                        Prefs.Save();
                    }
                    catch (Exception error)
                    {
                        Log.Error("[Reframed UI] Could not select language: " + error);
                    }
                }
                base.Close(closeSound);
                return;
            }
            ApplyAnimatedBounds();
        }

        public override void OnCancelKeyPressed()
        {
            Event current = Event.current;
            if (current != null) current.Use();
            if (!closing) BeginClose(true, null);
        }

        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            if (!closing) BeginClose(true, null);
        }

        public override void Close(bool doCloseSound = true)
        {
            if (!IsOpen)
            {
                base.Close(doCloseSound);
                return;
            }
            BeginClose(doCloseSound, null);
        }

        private void BeginClose(bool playCloseSound, LoadedLanguage selection)
        {
            if (closing) return;
            opening = false;
            closing = true;
            closeSound = playCloseSound;
            pendingSelection = selection;
            closeFrom = motionProgress;
            motionStart = Time.unscaledTime;
            EntryMotionEvidence.Start("language-close", MotionState);
        }

        private string MotionState() => "progress=" + motionProgress + ";opening=" + opening + ";closing=" + closing + ";bounds=" + windowRect;

        private void ApplyAnimatedBounds()
        {
            Rect footer = FooterBounds();
            float height = PopupHeight() * Mathf.Clamp01(motionProgress);
            windowRect = new Rect(footer.xMin, footer.yMin - height, PopupWidth(), height);
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!IsOpen) return;

            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                EnsureStyles();
                bool controlsEnabled = GUI.enabled && !opening && !closing;
                GUI.enabled = controlsEnabled;

                Color shell = Theme.Rgb(ShellColor);
                shell.a = Theme.EntryPanelOpacity;
                float radius = 8f * SceneScale();
                if (rect.height > radius)
                {
                    GUI.BeginGroup(new Rect(rect.x, rect.y, rect.width, rect.height - radius));
                    try { Theme.RoundRect(new Rect(0f, 0f, rect.width, rect.height), shell, radius); }
                    finally { GUI.EndGroup(); }
                    Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - radius, rect.width, radius), shell);
                }
                else Theme.RoundRect(rect, shell, radius);

                float scale = SceneScale();
                float contentAlpha = Mathf.Clamp01(motionProgress);
                float inset = 12f * scale;
                Rect title = new Rect(inset, 8f * scale, rect.width - 2f * inset, 22f * scale);
                GUI.color = Alpha(Color.white, contentAlpha);
                GUI.Label(title, "Language", titleStyle);

                Rect search = new Rect(inset, 36f * scale, rect.width - 2f * inset, 30f * scale);
                Theme.RoundRect(search, Theme.Rgb(HeaderColor), 4f * scale);
                GUI.SetNextControlName(SearchControl);
                string next = GUI.TextField(search, query, 128, searchStyle);
                if (next != query)
                {
                    query = next;
                    RebuildVisible();
                    scroll = Vector2.zero;
                }
                if (string.IsNullOrEmpty(query) && GUI.GetNameOfFocusedControl() != SearchControl)
                {
                    GUI.color = Alpha(Theme.Muted, contentAlpha);
                    GUI.Label(new Rect(search.x, search.y, search.width, search.height),
                        "Search languages", placeholderStyle);
                }

                Widgets.DrawBoxSolid(new Rect(inset, 74f * scale, rect.width - 2f * inset, 1f * scale), Alpha(Theme.Rgb(RuleColor), contentAlpha));

                float listTop = 84f * scale;
                float bottomInset = 12f * scale;
                float scrollbarWidth = 10f * scale;
                Rect viewport = new Rect(inset, listTop,
                    Mathf.Max(1f, rect.width - 2f * inset - scrollbarWidth - 3f * scale),
                    Mathf.Max(1f, rect.height - listTop - bottomInset));
                float rowHeight = BaseRowHeight * scale;
                float contentHeight = visible.Count * rowHeight;
                bool overflow = contentHeight > viewport.height;

                scroll = GUI.BeginScrollView(viewport, scroll,
                    new Rect(0, 0, viewport.width, contentHeight), false, false, GUIStyle.none, GUIStyle.none);
                try
                {
                    for (int i = 0; i < visible.Count; i++)
                    {
                        float y = i * rowHeight;
                        if (y + rowHeight < scroll.y || y > scroll.y + viewport.height) continue;
                        LoadedLanguage language = visible[i];
                        Rect row = new Rect(0, y, viewport.width, rowHeight);
                        bool selected = language == LanguageDatabase.activeLanguage;
                        GUI.color = Alpha(Color.white, contentAlpha);
                        if (Mouse.IsOver(row)) Theme.RoundRect(row, Theme.Rgb(RowHoverColor), 3f * scale);
                        else if (selected) Theme.RoundRect(row, Theme.Rgb(RowSelectedColor), 3f * scale);

                        Texture2D flag = GetFlag(language)?.Texture;
                        float flagSize = 16f * scale;
                        float flagX = 5f * scale;
                        if (flag != null)
                        {
                            GUI.color = Alpha(Color.white, contentAlpha);
                            GUI.DrawTexture(new Rect(flagX, row.y + (row.height - flagSize * .66f) * .5f,
                                flagSize, flagSize * .66f), flag, ScaleMode.ScaleToFit, true);
                        }
                        GUI.color = Alpha(selected ? Theme.Rgb(0xD8E5EC) : Color.white, contentAlpha);
                        float labelX = flagX + flagSize + 8f * scale;
                        string label = language.DisplayName ?? language.FriendlyNameNative ?? string.Empty;
                        Rect labelRect = new Rect(labelX, row.y, Mathf.Max(1f, row.width - labelX - 4f * scale), row.height);
                        string fitted = FitLabel(label, rowStyle, labelRect.width);
                        GUI.Label(labelRect, fitted, rowStyle);
                        if (fitted != label)
                            TooltipHandler.TipRegion(labelRect, label);

                        if (Widgets.ButtonInvisible(row))
                        {
                            BeginClose(true, language);
                            return;
                        }
                    }
                }
                finally { GUI.EndScrollView(controlsEnabled); }

                if (overflow)
                {
                    Rect slider = new Rect(viewport.xMax, viewport.y, scrollbarWidth, viewport.height);
                    Rect track = new Rect(slider.x + 3f * scale, slider.y, 4f * scale, slider.height);
                    GUI.color = Alpha(Color.white, contentAlpha);
                    Theme.RoundRect(track, Theme.Rgb(ScrollTrackColor), 2f * scale);
                    scrollThumb.fixedHeight = Mathf.Max(22f * scale, viewport.height * viewport.height / contentHeight);
                    scroll.y = GUI.VerticalSlider(slider, scroll.y, 0f, contentHeight - viewport.height,
                        scrollTrack, scrollThumb);
                }

                if (visible.Count == 0)
                {
                    GUI.color = Alpha(Theme.Muted, contentAlpha);
                    GUI.Label(new Rect(viewport.x + 2f * scale, viewport.y + 4f * scale,
                        viewport.width - 4f * scale, 24f * scale), "No matching languages", rowStyle);
                }
            }
        }

        private void RebuildVisible()
        {
            if (lastQuery == query) return;
            lastQuery = query;
            visible.Clear();
            string needle = query.Trim();
            if (needle.Length == 0)
            {
                visible.AddRange(languages);
                return;
            }
            foreach (LoadedLanguage language in languages)
            {
                string display = language.DisplayName ?? string.Empty;
                string native = language.FriendlyNameNative ?? string.Empty;
                if (display.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    native.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    visible.Add(language);
            }
        }

        private FlagInfo GetFlag(LoadedLanguage language)
        {
            if (flags.TryGetValue(language, out FlagInfo cached))
            {
                Texture2D lateIcon = language.icon;
                if (cached.Texture == null && lateIcon != null && lateIcon != BaseContent.BadTex)
                {
                    cached = new FlagInfo { Texture = lateIcon, SourcePath = "LoadedLanguage.icon", Owned = false };
                    flags[language] = cached;
                }
                return cached;
            }

            Texture2D existing = language.icon;
            if (existing != null && existing != BaseContent.BadTex)
            {
                var borrowed = new FlagInfo { Texture = existing, SourcePath = "LoadedLanguage.icon", Owned = false };
                flags.Add(language, borrowed);
                return borrowed;
            }

            foreach (var directory in language.AllDirectories)
            {
                var file = directory.Item1.GetFile("LangIcon.png");
                if (!file.Exists) continue;
                LoadedContentItem<Texture2D> loaded = ModContentLoader<Texture2D>.LoadItem(file);
                Texture2D texture = loaded?.contentItem;
                if (texture == null || texture == BaseContent.BadTex) continue;
                var owned = new FlagInfo { Texture = texture, SourcePath = file.FullPath, Owned = true };
                flags.Add(language, owned);
                return owned;
            }

            var missing = new FlagInfo { SourcePath = null, Owned = false };
            flags.Add(language, missing);
            return missing;
        }

        private void EnsureStyles()
        {
            Font font = Text.CurFontStyle.font;
            int size = Mathf.Max(8, Mathf.RoundToInt(9.333f * SceneScale()));
            int titleSize = Mathf.Max(9, Mathf.RoundToInt(11f * SceneScale()));
            if (font != styleFont || size != styleFontSize || titleStyle == null || titleStyle.fontSize != titleSize)
            {
                styleFont = font;
                styleFontSize = size;
                titleStyle = MakeStyle(font, titleSize, FontStyle.Bold, TextAnchor.MiddleLeft);
                rowStyle = MakeStyle(font, size, FontStyle.Normal, TextAnchor.MiddleLeft);
                searchStyle = MakeStyle(font, size, FontStyle.Normal, TextAnchor.MiddleLeft);
                searchStyle.padding = new RectOffset(Mathf.RoundToInt(8f * SceneScale()), Mathf.RoundToInt(8f * SceneScale()), 0, 0);
                placeholderStyle = MakeStyle(font, size, FontStyle.Normal, TextAnchor.MiddleLeft);
                placeholderStyle.padding = new RectOffset();
            }
            if (scrollTrack == null)
            {
                trackTexture = Solid(Theme.Rgb(ScrollTrackColor));
                thumbTexture = Solid(Theme.Rgb(ScrollThumbColor));
                scrollTrack = ScrollStyle(trackTexture);
                scrollThumb = ScrollStyle(thumbTexture);
            }
            // Static styles survive picker instances and viewport changes.
            // Keep the thumb aligned with the track's current geometry.
            scrollThumb.fixedWidth = 4f * SceneScale();
            int scrollbarMargin = Mathf.RoundToInt(3f * SceneScale());
            if (scrollThumb.margin.left != scrollbarMargin || scrollThumb.margin.right != scrollbarMargin)
                scrollThumb.margin = new RectOffset(scrollbarMargin, scrollbarMargin, 0, 0);
        }

        private static GUIStyle MakeStyle(Font font, int size, FontStyle style, TextAnchor anchor)
        {
            Text.Font = GameFont.Small;
            var result = new GUIStyle(Text.CurFontStyle)
            {
                font = font,
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Clip,
                margin = new RectOffset(),
                padding = new RectOffset()
            };
            result.normal.textColor = Color.white;
            return result;
        }

        private static float SceneScale() => Mathf.Max(.5f, EntryScene.Scale);
        private static Rect FooterBounds() => EntryScene.Map(128f, 668f, 208f, 33f);
        private static float PopupWidth() => Mathf.Min(BaseWidth * SceneScale(), Mathf.Max(1f, UI.screenWidth - 16f));
        private static float PopupHeight() => Mathf.Min(BaseHeight * SceneScale(), Mathf.Max(1f, UI.screenHeight - 16f));

        private static Color Alpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private static string FitLabel(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;
            const string ellipsis = "…";
            int low = 0, high = text.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, middle) + ellipsis)).x <= width) low = middle;
                else high = middle - 1;
            }
            return text.Substring(0, low) + ellipsis;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static GUIStyle ScrollStyle(Texture2D texture)
        {
            var style = new GUIStyle { border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset() };
            style.normal.background = style.hover.background = style.active.background = style.focused.background = texture;
            return style;
        }
    }
}

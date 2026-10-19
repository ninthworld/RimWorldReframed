using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The page owns the shell, footer, navigation and native validation. This view changes
    // presentation and the same three selections as StorytellerUI, never incident mechanics.
    internal sealed class NewGameStorytellerView
    {
        private readonly StorytellerDef[] storytellers = DefDatabase<StorytellerDef>.AllDefs
            .Where(def => def.listVisible).OrderBy(def => def.listOrder).ToArray();
        private readonly DifficultyDef[] difficulties = DefDatabase<DifficultyDef>.AllDefs.ToArray();
        private readonly Dictionary<string, Texture2D> curves = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, string[]> sourceLines = new Dictionary<string, string[]>();
        private readonly List<Pill> pills = new List<Pill>();
        private Vector2 storytellerScroll, detailsScroll;
        private StorytellerDef lastStoryteller;
        private LoadedLanguage styleLanguage;
        private GUIStyle title, name, body, introduction, caption, section, pillLabel, scrollTrack, scrollThumb;
        private float scale, styleScale = -1f, opacity, pillsHeight;
        private static readonly string[] DifficultyLetters = "DIFFICULTY".Select(c => c.ToString()).ToArray();
        private static readonly string[] PlaystyleLetters = "PLAYSTYLE".Select(c => c.ToString()).ToArray();
        private static readonly Color Ink = Theme.Rgb(0xF2F5F7), Muted = Theme.Rgb(0x9BAAB8);
        private static readonly Color Border = Theme.Rgb(0x313B4A);
        private static readonly string[] NativeDifficultyOrder = { "Peaceful", "Easy", "Medium", "Rough", "Hard", "Extreme", "Custom" };
        private static readonly Rect[] NativePillBounds = {
            new Rect(0, 0, 82, 27), new Rect(89, 0, 154, 27),
            new Rect(0, 34, 132, 27), new Rect(139, 34, 131, 27),
            new Rect(0, 68, 126, 27), new Rect(133, 68, 110, 27), new Rect(0, 102, 77, 27)
        };
        private struct Pill { internal DifficultyDef Def; internal Rect Rect; }
        private sealed class PortraitCopy { internal Texture2D Texture; internal bool Failed; internal int LastFrame; }
        // Canonical portraits only: at most three owned GPU copies, shared by page
        // instances. Native resources and their filtering settings remain untouched.
        private static readonly Dictionary<Texture2D, PortraitCopy> PortraitCopies = new Dictionary<Texture2D, PortraitCopy>();
        private static readonly List<Texture2D> StalePortraits = new List<Texture2D>();

        internal void Draw(Rect rect, ref StorytellerDef chosen, ref DifficultyDef difficulty,
            Difficulty values, Action editCustom, float opacity = 1f)
        {
            if (rect.width <= 0 || rect.height <= 0 || values == null) return;
            using (new Theme.GuiScope(true))
            {
                this.opacity = Mathf.Clamp01(opacity * GUI.color.a);
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                scale = rect.width / 582f;
                EnsureStyles();
                if (lastStoryteller != chosen)
                {
                    lastStoryteller = chosen;
                    detailsScroll = Vector2.zero;
                }
                bool english = SourceEnglish;
                Label(At(rect, 17, 18, 237, 22), english ? "Choose AI Storyteller" : "ChooseAIStoryteller".Translate().ToString(), title, Ink);
                SourceLabel(At(rect, 17, 44, 237, 57), english
                    ? "Different narrators\nchange the rhythm and\nintensity of events."
                    : "HowStorytellersWork".Translate().ToString(), introduction, Muted, 19);
                DrawStorytellers(At(rect, 17, 115, 248, 457), ref chosen);
                if (chosen != null) DrawDetails(At(rect, 289, 20, 284, 554), chosen, ref difficulty, values, editCustom);
            }
        }

        private void DrawStorytellers(Rect viewport, ref StorytellerDef chosen)
        {
            float height = Mathf.Max(0, storytellers.Length * 77f - 8f) * scale;
            bool overflow = height > viewport.height + .5f;
            BeginScroll(viewport, ref storytellerScroll, Mathf.Max(viewport.height, height));
            try
            {
                for (int i = 0; i < storytellers.Length; i++)
                {
                    StorytellerDef option = storytellers[i];
                    Rect card = Local(0, i * 77, 237, 69);
                    if (card.yMax < storytellerScroll.y || card.y > storytellerScroll.y + viewport.height) continue;
                    bool selected = option == chosen;
                    ChoiceFace(card, selected, 8, 0x181F25);
                    DrawPortrait(Local(8, i * 77 + 10, 48, 48), option);
                    Label(Local(68, i * 77 + 10, 160, 21), option.LabelCap, name, Ink);
                    SourceLabel(Local(68, i * 77 + 30, 160, 36), ShortDescription(option), body, Muted, authored: KnownStory(option));
                    if (GUI.enabled) TooltipHandler.TipRegion(card, option.description ?? option.label);
                    if (GUI.enabled && Widgets.ButtonInvisible(card))
                    {
                        TutorSystem.Notify_Event("ChooseStoryteller");
                        chosen = option;
                        lastStoryteller = option;
                        detailsScroll = Vector2.zero;
                    }
                }
            }
            finally { GUI.EndScrollView(GUI.enabled); }
            if (overflow) DrawScrollBar(viewport, ref storytellerScroll, height);
        }

        private void DrawDetails(Rect viewport, StorytellerDef chosen, ref DifficultyDef difficulty,
            Difficulty values, Action editCustom)
        {
            float width = 276 * scale;
            string description = DetailDescription(chosen);
            float titleHeight = Mathf.Max(20 * scale, name.CalcHeight(new GUIContent(chosen.LabelCap), width));
            float descriptionHeight = Mathf.Max(64 * scale, body.CalcHeight(new GUIContent(description), width));
            float extra = (titleHeight - 20 * scale + descriptionHeight - 64 * scale) / scale;
            float difficultyY = 128 + extra;
            float graphCaptionY = difficultyY + pillsHeight + 20;
            float graphY = graphCaptionY + 18;
            float playstyleY = graphY + 92;
            float modeY = playstyleY + 20;
            string reloadTitle = SourceEnglish ? "Reload anytime" : "ReloadAnytimeMode".Translate().ToString();
            string commitmentTitle = SourceEnglish ? "Commitment mode" : "CommitmentMode".TranslateWithBackup("PermadeathMode").ToString();
            string reloadCopy = SourceEnglish ? "Freely save & reload. Great for\nlearning and experimenting." : "ReloadAnytimeModeInfo".Translate().ToString();
            string commitmentCopy = SourceEnglish ? "Ironman-style autosaves.\nYour choices matter." : "PermadeathModeInfo".Translate().ToString();
            float reloadHeight = Mathf.Max(68, 31 + body.CalcHeight(new GUIContent(reloadCopy), 238 * scale) / scale + 7);
            float commitmentHeight = Mathf.Max(69, 31 + body.CalcHeight(new GUIContent(commitmentCopy), 238 * scale) / scale + 7);
            float secondModeY = modeY + reloadHeight + 8;
            float end = secondModeY + commitmentHeight;
            float anomalyY = end + 16;
            if (ModsConfig.AnomalyActive) end = anomalyY + 31;
            // Leave two logical pixels beyond the final rounded stroke. The content
            // and viewport ending exactly on that stroke clips its antialiased edge.
            float height = Mathf.Max(viewport.height, (end + 2) * scale);
            BeginScroll(viewport, ref detailsScroll, height);
            try
            {
                Label(Local(0, -2, 276, titleHeight / scale), chosen.LabelCap, name, Ink);
                SourceLabel(new Rect(0, titleHeight + scale, width, descriptionHeight), description, body, Muted, authored: KnownStory(chosen));
                Section(Local(0, difficultyY - 18, 276, 13), DifficultyLetters);
                for (int i = 0; i < pills.Count; i++)
                {
                    Pill pill = pills[i];
                    Rect button = Local(pill.Rect.x, difficultyY + pill.Rect.y, pill.Rect.width, pill.Rect.height);
                    ChoiceFace(button, difficulty == pill.Def, 4, 0x272F3C);
                    DrawBox(new Rect(button.x + 8 * scale, button.y + 9 * scale, 2 * scale, 10 * scale), DifficultyColor(pill.Def));
                    Label(new Rect(button.x + 18 * scale, button.y, button.width - 23 * scale, button.height), pill.Def.LabelCap, pillLabel, Ink);
                    if (GUI.enabled) TooltipHandler.TipRegion(button, (pill.Def.description ?? string.Empty).ResolveTags());
                    if (GUI.enabled && Widgets.ButtonInvisible(button))
                    {
                        // Exactly the native initialization policy: revisiting the selected
                        // Custom entry must not discard the values edited in its native editor.
                        if (!pill.Def.isCustom) values.CopyFrom(pill.Def);
                        else if (difficulty != pill.Def) values.CopyFrom(DifficultyDefOf.Rough);
                        difficulty = pill.Def;
                        if (pill.Def.isCustom) editCustom?.Invoke();
                    }
                }
                Label(Local(0, graphCaptionY - 2, 150, 16), "Event frequency", body, Muted);
                Label(Local(146, graphCaptionY - 2, 130, 16), CurveCaption(chosen), caption, Muted);
                Rect graph = Local(0, graphY, 276, 65);
                ChoiceFace(graph, true, 4, 0x181F25, false);
                Texture2D curve = Curve(chosen);
                if (curve != null)
                {
                    GUI.color = Alpha(Color.white);
                    GUI.DrawTexture(graph, curve, ScaleMode.StretchToFill, true);
                    GUI.color = Color.white;
                }
                if (GUI.enabled) TooltipHandler.TipRegion(graph, "Conceptual storyteller rhythm. This is not a forecast of incident timing.");
                Section(Local(0, playstyleY, 276, 13), PlaystyleLetters);
                bool chosenMode = Find.GameInitData != null && Find.GameInitData.permadeathChosen;
                bool commitment = Find.GameInitData != null && Find.GameInitData.permadeath;
                if (Mode(Local(0, modeY, 276, reloadHeight), reloadTitle, reloadCopy, "ReloadAnytimeModeInfo".Translate().ToString().ResolveTags(),
                    chosenMode && !commitment, Theme.Rgb(0x00D654)) && Find.GameInitData != null)
                {
                    Find.GameInitData.permadeathChosen = true;
                    Find.GameInitData.permadeath = false;
                }
                if (Mode(Local(0, secondModeY, 276, commitmentHeight), commitmentTitle, commitmentCopy, "PermadeathModeInfo".Translate().ToString().ResolveTags(),
                    chosenMode && commitment, Theme.Rgb(0xD82034)) && Find.GameInitData != null)
                {
                    Find.GameInitData.permadeathChosen = true;
                    Find.GameInitData.permadeath = true;
                }
                if (ModsConfig.AnomalyActive)
                {
                    Rect anomaly = Local(0, anomalyY, 276, 27);
                    ChoiceFace(anomaly, false, 4, 0x181F25);
                    Label(Local(12, anomalyY, 252, 27), "AnomalySettings".Translate() + "…", pillLabel, Ink);
                    if (GUI.enabled && Widgets.ButtonInvisible(anomaly))
                    {
                        if (difficulty == null) Messages.Message("MustChooseDifficulty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                        else Find.WindowStack.Add(new Dialog_AnomalySettings(values));
                    }
                }
            }
            finally { GUI.EndScrollView(GUI.enabled); }
            if (height > viewport.height + .5f) DrawScrollBar(viewport, ref detailsScroll, height);
        }

        private bool Mode(Rect rect, string text, string description, string tooltip, bool selected, Color marker)
        {
            ChoiceFace(rect, selected, 8, 0x272F3C);
            DrawBox(new Rect(rect.x + 12 * scale, rect.y + 29 * scale, 2 * scale, 10 * scale), marker);
            Label(new Rect(rect.x + 26 * scale, rect.y + 8 * scale, rect.width - 38 * scale, 22 * scale), text, name, Ink);
            SourceLabel(new Rect(rect.x + 26 * scale, rect.y + 31 * scale, rect.width - 38 * scale, rect.height - 35 * scale), description, body, Muted);
            if (GUI.enabled) TooltipHandler.TipRegion(rect, text + "\n\n" + tooltip);
            return GUI.enabled && Widgets.ButtonInvisible(rect);
        }

        private void DrawPortrait(Rect rect, StorytellerDef storyteller)
        {
            // Native Tiny textures are tightly cropped faces. The concept shows whole
            // heads and accessories from the native 580x620 Large artwork instead.
            // Crop existing pixels through UVs; no copied game artwork is packaged.
            Rect crop;
            switch (storyteller.defName)
            {
                case "Cassandra": crop = new Rect(150, 15, 300, 300); break;
                case "Phoebe": crop = new Rect(210, 0, 280, 280); break;
                case "Randy": crop = new Rect(150, 0, 330, 330); break;
                default: crop = default; break;
            }
            Texture2D portrait = storyteller.portraitLargeTex;
            GUI.color = Alpha(Color.white);
            if (crop.width > 0 && portrait != null && portrait.width == 580 && portrait.height == 620)
            {
                Rect uv = new Rect(crop.x / portrait.width, 1f - crop.yMax / portrait.height,
                    crop.width / portrait.width, crop.height / portrait.height);
                GUI.DrawTextureWithTexCoords(rect, FilteredPortrait(portrait), uv, true);
            }
            else if (storyteller.portraitTinyTex != null)
                GUI.DrawTexture(rect, storyteller.portraitTinyTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
        }

        private static Texture2D FilteredPortrait(Texture2D source)
        {
            if (PortraitCopies.TryGetValue(source, out PortraitCopy cached))
            {
                cached.LastFrame = Time.frameCount;
                if (cached.Texture != null) return cached.Texture;
                if (cached.Failed) return source;
                PortraitCopies.Remove(source);
            }
            // GPU readback occurs once per source, during paint only. Layout/input
            // passes and a failed copy continue safely with the native texture.
            if (Event.current == null || Event.current.type != EventType.Repaint) return source;
            PrunePortraitCopies();
            RenderTexture previous = RenderTexture.active, temporary = null;
            Texture2D copy = null;
            var entry = new PortraitCopy { LastFrame = Time.frameCount };
            try
            {
                temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
                {
                    name = "Reframed storyteller filtered copy",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 1
                };
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                entry.Texture = copy;
            }
            catch (Exception error)
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                entry.Failed = true;
                Log.Warning("[Reframed UI] Storyteller filtering unavailable; native portrait retained. " + error.Message);
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
            PortraitCopies[source] = entry;
            return entry.Texture != null ? entry.Texture : source;
        }

        private static void PrunePortraitCopies()
        {
            StalePortraits.Clear();
            foreach (KeyValuePair<Texture2D, PortraitCopy> pair in PortraitCopies)
                if (pair.Key == null || !pair.Value.Failed && pair.Value.Texture == null) StalePortraits.Add(pair.Key);
            for (int i = 0; i < StalePortraits.Count; i++) ReleasePortraitCopy(StalePortraits[i]);
            // A live native texture can be replaced during a content reload. Bound
            // retained copies even when the old native object has not been destroyed.
            if (PortraitCopies.Count >= 3)
            {
                Texture2D oldest = null;
                int frame = int.MaxValue;
                foreach (KeyValuePair<Texture2D, PortraitCopy> pair in PortraitCopies)
                    if (pair.Value.LastFrame < frame) { oldest = pair.Key; frame = pair.Value.LastFrame; }
                if (!ReferenceEquals(oldest, null)) ReleasePortraitCopy(oldest);
            }
            StalePortraits.Clear();
        }

        private static void ReleasePortraitCopy(Texture2D source)
        {
            if (!PortraitCopies.TryGetValue(source, out PortraitCopy entry)) return;
            if (entry.Texture != null) UnityEngine.Object.Destroy(entry.Texture);
            PortraitCopies.Remove(source);
        }

        private void ChoiceFace(Rect rect, bool selected, float radius, uint selectedColor, bool hover = true)
        {
            if (selected) Theme.RoundRect(rect, Alpha(Theme.Rgb(selectedColor), .96f), radius * scale);
            else if (hover && GUI.enabled && Mouse.IsOver(rect)) Theme.RoundRect(rect, Alpha(Theme.Rgb(0x181F25), .68f), radius * scale);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true, 0, Alpha(Border), scale, radius * scale);
        }

        private void BeginScroll(Rect viewport, ref Vector2 position, float contentHeight)
        {
            position.x = 0;
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, contentHeight - viewport.height));
            position = GUI.BeginScrollView(viewport, position, new Rect(0, 0, viewport.width, contentHeight),
                false, false, GUIStyle.none, GUIStyle.none);
        }

        private void DrawScrollBar(Rect viewport, ref Vector2 position, float height)
        {
            scrollThumb.fixedHeight = Mathf.Min(viewport.height, Mathf.Max(24 * scale, viewport.height * viewport.height / height));
            GUI.color = Alpha(Theme.Rgb(0x23323D));
            position.y = GUI.VerticalSlider(new Rect(viewport.xMax - 6 * scale, viewport.y, 6 * scale, viewport.height),
                position.y, 0, height - viewport.height, scrollTrack, scrollThumb);
            GUI.color = Color.white;
        }

        private void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            bool interactive = GUI.enabled;
            GUI.color = Alpha(color);
            // A transition's input gate must not add Unity's disabled-text dimming
            // on top of the explicit fade. No input control is created in this scope.
            GUI.enabled = true;
            try { GUI.Label(rect, text ?? string.Empty, style); }
            finally { GUI.color = Color.white; GUI.enabled = interactive; }
        }

        private void SourceLabel(Rect rect, string text, GUIStyle style, Color color, float linePitch = 16, bool authored = true)
        {
            if (!authored || !SourceEnglish || text == null || text.IndexOf('\n') < 0)
            {
                Label(rect, text, style, color);
                return;
            }
            if (!sourceLines.TryGetValue(text, out string[] lines)) sourceLines[text] = lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                Label(new Rect(rect.x, rect.y + i * linePitch * scale, rect.width, linePitch * scale), lines[i], style, color);
        }

        private void Section(Rect rect, string[] letters)
        {
            float x = rect.x;
            for (int i = 0; i < letters.Length; i++)
            {
                float width = section.CalcSize(new GUIContent(letters[i])).x;
                Label(new Rect(x, rect.y, width + scale, rect.height), letters[i], section, Ink);
                x += width + .55f * scale;
            }
        }

        private void DrawBox(Rect rect, Color color) => Widgets.DrawBoxSolid(rect, Alpha(color));
        private Color Alpha(Color color, float amount = 1f) { color.a *= opacity * amount; return color; }
        private Rect Local(float x, float y, float w, float h) => new Rect(x * scale, y * scale, w * scale, h * scale);
        private Rect At(Rect origin, float x, float y, float w, float h) => new Rect(origin.x + x * scale, origin.y + y * scale, w * scale, h * scale);
        private static bool SourceEnglish => LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage;
        private static bool KnownStory(StorytellerDef def) => def.defName == "Cassandra" || def.defName == "Phoebe" || def.defName == "Randy";

        private void EnsureStyles()
        {
            if (body != null && Mathf.Approximately(styleScale, scale) && body.font == Text.CurFontStyle.font && styleLanguage == LanguageDatabase.activeLanguage) return;
            styleScale = scale;
            styleLanguage = LanguageDatabase.activeLanguage;
            title = Style(16, false, TextAnchor.UpperLeft);
            name = Style(14, true, TextAnchor.UpperLeft);
            body = Style(12, true, TextAnchor.UpperLeft);
            introduction = Style(14, true, TextAnchor.UpperLeft);
            caption = Style(12, false, TextAnchor.UpperRight);
            section = Style(9, false, TextAnchor.UpperLeft, FontStyle.Bold);
            pillLabel = Style(14, false, TextAnchor.MiddleLeft);
            scrollTrack = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
            scrollThumb = new GUIStyle { margin = new RectOffset(), padding = new RectOffset(), fixedWidth = 3 * scale };
            scrollThumb.normal.background = scrollThumb.hover.background = scrollThumb.active.background = BaseContent.WhiteTex;
            LayoutPills();
        }

        private GUIStyle Style(float size, bool wrap, TextAnchor anchor, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale)),
                alignment = anchor, fontStyle = weight, wordWrap = wrap, richText = false,
                padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Clip };
            style.normal.textColor = Color.white;
            return style;
        }

        private void LayoutPills()
        {
            pills.Clear();
            bool canonical = SourceEnglish && difficulties.Length == NativeDifficultyOrder.Length;
            for (int i = 0; canonical && i < difficulties.Length; i++) canonical = difficulties[i].defName == NativeDifficultyOrder[i];
            float x = 0, y = 0;
            for (int i = 0; i < difficulties.Length; i++)
            {
                Rect bounds;
                if (canonical) bounds = NativePillBounds[i];
                else
                {
                    float width = Mathf.Min(276, Mathf.Ceil(pillLabel.CalcSize(new GUIContent(difficulties[i].LabelCap)).x / scale) + 25);
                    if (x > 0 && x + width > 276) { x = 0; y += 34; }
                    bounds = new Rect(x, y, width, 27);
                    x += width + 7;
                }
                pills.Add(new Pill { Def = difficulties[i], Rect = bounds });
            }
            pillsHeight = pills.Count == 0 ? 0 : pills[pills.Count - 1].Rect.yMax;
        }

        private static Color DifficultyColor(DifficultyDef def)
        {
            switch (def.defName)
            {
                case "Peaceful": return Theme.Rgb(0x00D654);
                case "Easy": return Theme.Rgb(0x016A2F);
                case "Medium": return Theme.Rgb(0xF8D401);
                case "Rough": return Theme.Rgb(0x924F01);
                case "Hard": return Theme.Rgb(0xF96A6D);
                case "Extreme": return Theme.Rgb(0xA90611);
                default: return Theme.Rgb(0x8C9BA9);
            }
        }

        private static string ShortDescription(StorytellerDef def)
        {
            if (SourceEnglish)
            {
                switch (def.defName)
                {
                    case "Cassandra": return "Steady rise in challenge\nwith breathers.";
                    case "Phoebe": return "Fewer threats,\nmore time to build.";
                    case "Randy": return "Unpredictable chaos.\nAnything can happen.";
                }
            }
            return def.description ?? string.Empty;
        }

        private static string DetailDescription(StorytellerDef def)
        {
            if (SourceEnglish)
            {
                switch (def.defName)
                {
                    case "Cassandra": return "Creates a classic, steadily\nincreasing curve of tension\nwith short breathers between\nspikes.";
                    // Only Cassandra's expanded copy/curve is shown. These two are
                    // qualitative extensions of the native descriptions, not numeric forecasts.
                    case "Phoebe": return "Gives long, peaceful breaks\nbetween challenges, with time\nto build and recover.";
                    case "Randy": return "Unpredictable events, with\nlittle warning or reliable\nbreathing room.";
                }
            }
            return def.description ?? string.Empty;
        }

        private static string CurveCaption(StorytellerDef def)
        {
            switch (def.defName)
            {
                case "Cassandra": return "Moderate, rising";
                case "Phoebe": return "Long breaks";
                case "Randy": return "Unpredictable";
                default: return "Varies";
            }
        }

        private Texture2D Curve(StorytellerDef def)
        {
            if (def.defName != "Cassandra" && def.defName != "Phoebe" && def.defName != "Randy") return null;
            if (!curves.TryGetValue(def.defName, out Texture2D texture) || texture == null)
            {
                curves[def.defName] = texture = ContentFinder<Texture2D>.Get("Reframed/NewGame/Curve" + def.defName, true);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    /// <summary>Compact entry-scene expansion selector. The lead owns the native hook and fallback.</summary>
    [StaticConstructorOnStartup]
    internal static class ReframedDlcPanel
    {
        private static readonly string[] SourceOrder = { "Ideology", "Odyssey", "Royalty", "Anomaly", "Biotech" };
        private static readonly Dictionary<ExpansionDef, Texture2D> Previews = new Dictionary<ExpansionDef, Texture2D>();
        private static readonly Dictionary<string, ColumnCache> ColumnCopies = new Dictionary<string, ColumnCache>(StringComparer.OrdinalIgnoreCase);
        private static int selectedIndex = -1;
        private static string selectedLinkedMod, displayedLinkedMod, outgoingLinkedMod, queuedLinkedMod;
        private static bool hasQueuedSelection;
        private static float cardProgress, switchProgress, motionStart, motionFrom;
        private static MotionKind motion;
        private static Vector2 descriptionScroll;
        private static GUIStyle bodyStyle, sourceLineStyle, headingStyle, titleStyle, footerStyle, statusStyle, scrollTrack, scrollThumb;
        private static float styleScale = -1;
        private static Texture2D previewFade;
        private static bool failed;
        private const float MotionDuration = .24f;
        private const float SourceLinePitch = 10.667f;
        private enum MotionKind { None, Opening, Closing, Switching }
        internal static bool CardOpen => selectedLinkedMod != null || displayedLinkedMod != null;
        internal static bool Transitioning => motion != MotionKind.None;
        internal static void Close() => ResetCard();
        private static Color PanelColor => SurfaceColor(0x0D1318);
        private static Color SurfaceColor(uint rgb) { Color c = Theme.Rgb(rgb); c.a = EntryScene.PanelColor.a; return c; }
        private sealed class ColumnCopy { internal string Heading, Body; internal string[] SourceLines; }
        private sealed class ColumnCache { internal string Text; internal bool SourceCopy; internal ColumnCopy[] Columns; }
        private static Color WithOpacity(Color color, float opacity) { color.a *= Mathf.Clamp01(opacity); return color; }

        internal static bool Draw()
        {
            if (failed || Current.ProgramState != ProgramState.Entry || !GenScene.InEntryScene ||
                !EntryScene.Active || Find.WindowStack == null) { ResetCard(); return false; }
            bool modal = Find.WindowStack.AnyWindowAbsorbingAllInput;
            bool languageBackdrop = EntryScene.LanguageBackdropVisible;
            // Keep the DLC composition visible behind the language picker, but inert.
            if (modal && !languageBackdrop) return true;
            var expansions = OrderedExpansions();
            // Unknown extra expansions retain the native route instead of disappearing or shrinking the five marks.
            if (expansions.Count > SourceOrder.Length) { ResetCard(); return false; }
            try { DrawPanel(expansions, !modal); return true; }
            catch (Exception error)
            {
                ResetCard(); failed = true;
                Log.Error("[Reframed UI] DLC panel failed; native expansion controls resume next frame. " + error);
                return false;
            }
        }

        /// <summary>Paint-only browser transition: no card, tooltips, hit regions or selection changes.</summary>
        internal static void DrawTransitionStrip(float opacity, float upwardDesignOffset)
        {
            if (failed || Event.current == null || Event.current.type != EventType.Repaint || !EntryScene.Active ||
                ReframedMod.Settings?.DlcPanel != true || float.IsNaN(opacity) || float.IsInfinity(opacity) ||
                float.IsNaN(upwardDesignOffset) || float.IsInfinity(upwardDesignOffset)) return;
            opacity = Mathf.Clamp01(opacity);
            if (opacity <= 0) return;
            try
            {
                var expansions = OrderedExpansions();
                if (expansions.Count == 0 || expansions.Count > SourceOrder.Length) return;
                Rect bounds = EntryScene.DlcStripBounds;
                bounds.y -= upwardDesignOffset * EntryScene.Scale;
                DrawSelector(expansions, bounds, false, opacity, false);
            }
            catch (Exception error)
            {
                ResetCard(); failed = true;
                Log.Error("[Reframed UI] DLC transition strip failed; native expansion controls resume next frame. " + error);
            }
        }

        private static void DrawPanel(List<ExpansionDef> expansions, bool inputAvailable)
        {
            if (expansions.Count == 0) { ResetCard(); return; }
            UpdateMotion();
            if (selectedLinkedMod != null && FindExpansion(expansions, selectedLinkedMod) == null) ResetCard();
            if (displayedLinkedMod != null && FindExpansion(expansions, displayedLinkedMod) == null) ResetCard();
            selectedIndex = selectedLinkedMod == null ? -1 : expansions.FindIndex(e => string.Equals(e.linkedMod, selectedLinkedMod, StringComparison.OrdinalIgnoreCase));
            Rect selector = EntryScene.DlcStripBounds;
            Rect card = EntryScene.DlcCardBounds;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                bool available = GUI.enabled && inputAvailable;
                EnsureStyles();
                Event ev = Event.current;
                if (available && CardOpen)
                {
                    if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
                    { RequestClose(); ev.Use(); }
                    else if (ev.type == EventType.MouseDown && !Mouse.IsOver(selector) && !Mouse.IsOver(card))
                    { RequestClose(); ev.Use(); }
                }
                bool selectorInput = available;
                DrawSelector(expansions, selector, selectorInput, 1f, available, RequestSelection);
                DrawAnimatedCard(expansions, card, available);
                // The card is not a Window; consume unused pointer events over it to avoid underlying controls.
                if (CardOpen && (available || motion != MotionKind.None) && Mouse.IsOver(card) &&
                    (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp || ev.type == EventType.ScrollWheel)) ev.Use();
            }
        }

        private static void ResetCard()
        {
            selectedIndex = -1;
            selectedLinkedMod = displayedLinkedMod = outgoingLinkedMod = queuedLinkedMod = null;
            hasQueuedSelection = false;
            cardProgress = switchProgress = 0f;
            motion = MotionKind.None;
            descriptionScroll = Vector2.zero;
        }

        private static ExpansionDef FindExpansion(List<ExpansionDef> expansions, string linkedMod)
        {
            return expansions.Find(e => string.Equals(e.linkedMod, linkedMod, StringComparison.OrdinalIgnoreCase));
        }

        private static void RequestSelection(string linkedMod)
        {
            if (motion == MotionKind.Opening)
            {
                if (linkedMod == null) StartClose();
                else if (!string.Equals(linkedMod, displayedLinkedMod, StringComparison.OrdinalIgnoreCase)) QueueSelection(linkedMod);
                return;
            }
            if (motion == MotionKind.Closing)
            {
                if (string.Equals(linkedMod, displayedLinkedMod, StringComparison.OrdinalIgnoreCase)) StartOpen();
                else if (linkedMod != null) QueueSelection(linkedMod);
                return;
            }
            if (motion == MotionKind.Switching)
            {
                QueueSelection(string.Equals(linkedMod, selectedLinkedMod, StringComparison.OrdinalIgnoreCase) ? null : linkedMod);
                return;
            }
            ApplySelection(linkedMod);
        }

        private static void ApplySelection(string linkedMod)
        {
            if (linkedMod == null)
            {
                if (displayedLinkedMod != null) StartClose();
                else selectedLinkedMod = null;
                return;
            }
            if (string.Equals(linkedMod, displayedLinkedMod, StringComparison.OrdinalIgnoreCase))
            {
                selectedLinkedMod = displayedLinkedMod;
                if (cardProgress < 1f) StartOpen();
                return;
            }
            selectedLinkedMod = linkedMod;
            descriptionScroll = Vector2.zero;
            if (displayedLinkedMod == null)
            {
                displayedLinkedMod = linkedMod;
                cardProgress = 0f;
                motionFrom = 0f;
                BeginMotion(MotionKind.Opening);
            }
            else
            {
                outgoingLinkedMod = displayedLinkedMod;
                displayedLinkedMod = linkedMod;
                switchProgress = 0f;
                BeginMotion(MotionKind.Switching);
            }
        }

        private static void QueueSelection(string linkedMod)
        {
            queuedLinkedMod = linkedMod;
            hasQueuedSelection = true;
        }

        private static void RequestClose()
        {
            if (motion == MotionKind.Opening) StartClose();
            else if (motion == MotionKind.Switching)
            {
                ClearQueuedSelection();
                QueueSelection(null);
            }
            else if (motion == MotionKind.Closing) ClearQueuedSelection();
            else if (motion == MotionKind.None) ApplySelection(null);
        }

        private static void StartOpen()
        {
            if (displayedLinkedMod == null) return;
            ClearQueuedSelection();
            selectedLinkedMod = displayedLinkedMod;
            motionFrom = cardProgress;
            BeginMotion(MotionKind.Opening);
        }

        private static void StartClose()
        {
            if (displayedLinkedMod == null) return;
            ClearQueuedSelection();
            selectedLinkedMod = null;
            motionFrom = cardProgress;
            BeginMotion(MotionKind.Closing);
        }

        private static void ClearQueuedSelection()
        {
            queuedLinkedMod = null;
            hasQueuedSelection = false;
        }

        private static void BeginMotion(MotionKind kind)
        {
            motion = kind;
            motionStart = Time.unscaledTime;
            EntryMotionEvidence.Start("dlc-" + kind, () => "motion=" + motion + ";card=" + cardProgress + ";switch=" + switchProgress +
                ";selected=" + selectedLinkedMod + ";displayed=" + displayedLinkedMod + ";outgoing=" + outgoingLinkedMod +
                ";queued=" + queuedLinkedMod + ";hasQueue=" + hasQueuedSelection +
                ";reveal=" + CardRevealBounds(EntryScene.DlcCardBounds, cardProgress) + ";pivot=" + EntryScene.DlcCardBounds.max);
        }

        private static void UpdateMotion()
        {
            if (motion == MotionKind.None) return;
            float elapsed = Mathf.Clamp01((Time.unscaledTime - motionStart) / MotionDuration);
            float eased = elapsed * elapsed * (3f - 2f * elapsed);
            MotionKind completed = motion;
            if (motion == MotionKind.Opening) cardProgress = Mathf.Lerp(motionFrom, 1f, eased);
            else if (motion == MotionKind.Closing) cardProgress = Mathf.Lerp(motionFrom, 0f, eased);
            else switchProgress = eased;
            if (elapsed < 1f) return;

            motion = MotionKind.None;
            if (completed == MotionKind.Opening) { cardProgress = 1f; selectedLinkedMod = displayedLinkedMod; }
            else if (completed == MotionKind.Closing)
            {
                cardProgress = 0f;
                selectedLinkedMod = displayedLinkedMod = outgoingLinkedMod = null;
            }
            else { switchProgress = 1f; outgoingLinkedMod = null; }
            if (hasQueuedSelection)
            {
                string queued = queuedLinkedMod;
                queuedLinkedMod = null;
                hasQueuedSelection = false;
                ApplySelection(queued);
            }
        }

        private static void DrawAnimatedCard(List<ExpansionDef> expansions, Rect bounds, bool available)
        {
            if (displayedLinkedMod == null || motion == MotionKind.Closing && cardProgress <= 0f) return;
            ExpansionDef current = FindExpansion(expansions, displayedLinkedMod);
            if (current == null) return;
            float progress = motion == MotionKind.Closing || motion == MotionKind.Opening ? cardProgress : 1f;
            if (progress <= 0f) return;
            using (new Theme.GuiScope(true))
            {
                // Apply the fixed-pivot transform before opening the full-size group. Unity's
                // GUI matrix also transforms group origins and clipping bounds: scaling inside
                // a pre-shrunk group would move it toward screen zero and shrink its clip twice.
                Vector3 pivotOffset = new Vector3(bounds.xMax * (1f - progress), bounds.yMax * (1f - progress), 0f);
                GUI.matrix *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, new Vector3(progress, progress, 1f));
                GUI.BeginGroup(bounds);
                try
                {
                    Rect local = new Rect(0f, 0f, bounds.width, bounds.height);
                    DrawAnimatedCardShell(bounds.width, bounds.height, 0f);
                    float contentOpacity = MotionContentOpacity(progress);
                    if (motion == MotionKind.Switching && outgoingLinkedMod != null)
                    {
                        ExpansionDef previous = FindExpansion(expansions, outgoingLinkedMod);
                        DrawCardPreviews(previous, current, local, contentOpacity, switchProgress);
                        if (previous != null) DrawCardContents(previous, local, false, contentOpacity * (1f - switchProgress));
                        DrawCardContents(current, local, false, contentOpacity * switchProgress);
                    }
                    else
                    {
                        DrawCardPreviews(null, current, local, contentOpacity, 1f);
                        DrawCardContents(current, local, available && motion == MotionKind.None, contentOpacity);
                    }
                }
                finally { GUI.EndGroup(); }
            }
        }

        private static Rect CardRevealBounds(Rect bounds, float progress)
        {
            float fraction = Mathf.Clamp01(progress);
            float width = bounds.width * fraction, height = bounds.height * fraction;
            return new Rect(bounds.xMax - width, bounds.yMax - height, width, height);
        }

        private static float MotionContentOpacity(float progress)
        {
            float t = Mathf.Clamp01((progress - .65f) / .35f);
            return t * t * (3f - 2f * t);
        }

        private static void DrawSelector(List<ExpansionDef> expansions, Rect bounds, bool available, float opacity = 1f,
            bool interactive = true, Action<string> onSelection = null)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Color panel = PanelColor; panel.a *= opacity;
                Theme.RoundRect(bounds, panel, 10 * EntryScene.Scale);
                float slot = bounds.width / SourceOrder.Length;
                for (int i = 0; i < expansions.Count; i++)
                {
                    ExpansionDef expansion = expansions[i];
                    Rect hit = new Rect(bounds.x + i * slot, bounds.y, slot, bounds.height);
                    // Selection is the brighter native icon, without an opaque/double-painted tile over the translucent strip.
                    Texture2D icon = expansion.IconFromStatus;
                    if (icon != null)
                    {
                        float brightness = i == selectedIndex ? 1f : expansion.Status == ExpansionStatus.Active ? .7f : .5f;
                        GUI.color = new Color(brightness, brightness, brightness, opacity);
                        GUI.DrawTexture(new Rect(hit.center.x - 12 * EntryScene.Scale, hit.center.y - 12 * EntryScene.Scale,
                            24 * EntryScene.Scale, 24 * EntryScene.Scale), icon, ScaleMode.ScaleToFit, true);
                    }
                    GUI.color = Color.white;
                    if (interactive) TooltipHandler.TipRegion(hit, expansion.LabelCap + "\n" + expansion.StatusDescription);
                    if (interactive && available && Widgets.ButtonInvisible(hit))
                    {
                        if (onSelection != null) onSelection(selectedIndex == i ? null : expansion.linkedMod);
                    }
                }
            }
        }

        private static void DrawAnimatedCardShell(float width, float visibleHeight, float cardStartY)
        {
            float s = EntryScene.Scale;
            GUI.color = Color.white;
            float split = 228f * s;
            float bodyHeight = Mathf.Clamp(split - cardStartY, 0f, visibleHeight);
            float radius = 10f * s;
            // Keep a rounded moving top and a square reveal edge without painting translucent pixels twice.
            if (bodyHeight > 0f)
                DrawTopRoundedSquareBottom(new Rect(0, 0, width, bodyHeight), PanelColor, radius);
            if (bodyHeight < visibleHeight)
            {
                Rect footer = new Rect(0, bodyHeight, width, visibleHeight - bodyHeight);
                if (bodyHeight <= 0f)
                {
                    GUI.BeginGroup(footer);
                    try { Theme.RoundRect(new Rect(0, 0, footer.width, footer.height), SurfaceColor(0x182127), radius); }
                    finally { GUI.EndGroup(); }
                }
                else DrawSquareTopRoundedBottom(footer, SurfaceColor(0x182127), radius);
            }
            if (cardStartY <= split && cardStartY + visibleHeight >= split)
                Widgets.DrawBoxSolid(new Rect(0, split - cardStartY, width, Mathf.Max(.5f, .667f * s)), Theme.Rgb(0x303B43));
        }

        private static void DrawTopRoundedSquareBottom(Rect rect, Color color, float radius)
        {
            if (rect.height <= 0f) return;
            GUI.BeginGroup(rect);
            try { Theme.RoundRect(new Rect(0, 0, rect.width, rect.height + radius), color, radius); }
            finally { GUI.EndGroup(); }
        }

        private static void DrawSquareTopRoundedBottom(Rect rect, Color color, float radius)
        {
            if (rect.height <= 0f) return;
            float topBand = Mathf.Min(radius, rect.height);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, topBand), color);
            if (rect.height <= topBand) return;
            GUI.BeginGroup(new Rect(rect.x, rect.y + topBand, rect.width, rect.height - topBand));
            try { Theme.RoundRect(new Rect(0, -topBand, rect.width, rect.height), color, radius); }
            finally { GUI.EndGroup(); }
        }

        private static void DrawCardPreviews(ExpansionDef previous, ExpansionDef current, Rect bounds, float opacity, float blend)
        {
            if (opacity <= 0f) return;
            float s = EntryScene.Scale;
            GUI.BeginGroup(bounds);
            try
            {
                Texture2D outgoing = previous == null ? null : Preview(previous);
                Texture2D incoming = Preview(current);
                Rect image = new Rect(0, 0, bounds.width, 108 * s);
                // Blend the incoming image over a complete outgoing image, rather than
                // fading both against the shell and darkening the middle of the switch.
                GUI.color = Color.white;
                if (outgoing != null)
                    GUI.DrawTexture(image, outgoing, ScaleMode.ScaleAndCrop, true, 0,
                        WithOpacity(Color.white, opacity), 0, 10 * s);
                if (incoming != null)
                    GUI.DrawTexture(image, incoming, ScaleMode.ScaleAndCrop, true, 0,
                        WithOpacity(Color.white, opacity * (outgoing == null ? 1f : blend)), 0, 10 * s);
                // The spatial fade masks the combined image once. Attenuating this
                // mask with transition opacity leaves p*(1-p) of the image at its
                // bottom edge. Paint titles afterward so no preview covers them.
                if (outgoing != null || incoming != null)
                    GUI.DrawTexture(new Rect(0, 32 * s, bounds.width, 76 * s), previewFade);
            }
            finally { GUI.EndGroup(); }
        }

        private static void DrawCardContents(ExpansionDef expansion, Rect bounds, bool available, float opacity)
        {
            float s = EntryScene.Scale;
            GUI.color = Color.white;
            GUI.BeginGroup(bounds);
            try
            {
                float w = bounds.width;
                bool anomaly = string.Equals(expansion.defName, "Anomaly", StringComparison.OrdinalIgnoreCase);
                Color titleInk = anomaly ? Theme.Rgb(0xBFCF8B) : Theme.Ink;
                // The heading uses the full-color native brand; the selector/footer retain installation status.
                Texture2D icon = expansion.Icon;
                if (icon != null)
                {
                    GUI.color = WithOpacity(Color.white, opacity);
                    GUI.DrawTexture(new Rect(11 * s, 76 * s, 13.333f * s, 17 * s), icon, ScaleMode.ScaleToFit, true);
                }
                Label(new Rect(25 * s, 73 * s, w - 36 * s, 23 * s), expansion.LabelCap.ToString().ToUpperInvariant(), titleStyle, WithOpacity(titleInk, opacity));
                ColumnCopy[] copy = GetColumns(expansion);
                DrawColumns(new Rect(11 * s, 106 * s, w - 22 * s, 116 * s), copy, available, opacity);

                Rect cta = new Rect(11 * s, 238 * s, 118 * s, 22 * s);
                string url = expansion.StoreURL;
                string action = expansion.steamUrl.NullOrEmpty() ? "ClickForMoreInfo".Translate().ToString() : "See more on Steam";
                bool hasUrl = !url.NullOrEmpty();
                bool enabled = available && hasUrl;
                Color ctaFace = SurfaceColor(available && Mouse.IsOver(cta) && hasUrl ? 0x2A3742u : 0x202B34u);
                GUI.color = Color.white;
                Theme.ButtonFace(cta, ctaFace, !hasUrl, radius: 3 * s, opacity: opacity);
                Texture2D external = MainMenuIcons.Get("ui.external-link");
                if (external != null)
                {
                    GUI.color = WithOpacity(enabled ? Color.white : Theme.Muted, opacity);
                    GUI.DrawTexture(new Rect(cta.x + 8 * s, cta.center.y - 4.667f * s, 9.333f * s, 9.333f * s), external);
                    GUI.color = Color.white;
                }
                Label(new Rect(cta.x + 23 * s, cta.y, cta.width - 28 * s, cta.height), action, footerStyle, WithOpacity(hasUrl ? Color.white : Theme.Muted, opacity));
                if (available) TooltipHandler.TipRegion(cta, action);
                if (enabled && Widgets.ButtonInvisible(cta)) SteamUtility.OpenUrl(url);
                Rect status = new Rect(141 * s, 233 * s, w - 155 * s, 32 * s);
                Label(status, expansion.StatusDescription, statusStyle, WithOpacity(Theme.Rgb(0x93A2B0), opacity));
                if (available) TooltipHandler.TipRegion(status, expansion.StatusDescription);
            }
            finally { GUI.EndGroup(); }
        }

        private static ColumnCopy[] GetColumns(ExpansionDef expansion)
        {
            string key = expansion.linkedMod ?? expansion.defName;
            string text = expansion.description ?? string.Empty;
            bool anomaly = string.Equals(expansion.defName, "Anomaly", StringComparison.OrdinalIgnoreCase);
            bool sourceCopy = anomaly && LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage;
            if (ColumnCopies.TryGetValue(key, out ColumnCache cached) && cached.Text == text && cached.SourceCopy == sourceCopy)
                return cached.Columns;
            ColumnCopy[] columns = sourceCopy ? AnomalySourceColumns() : NativeCopyColumns(text);
            ColumnCopies[key] = new ColumnCache { Text = text, SourceCopy = sourceCopy, Columns = columns };
            return columns;
        }

        private static void DrawColumns(Rect viewport, ColumnCopy[] columns, bool interactive, float opacity)
        {
            float s = EntryScene.Scale, gap = 18 * s;
            float contentWidth = viewport.width;
            float columnWidth = Mathf.Max(1, (contentWidth - gap * 2) / 3);
            float height = ColumnHeight(columnWidth, s, columns);
            bool overflow = height > viewport.height + .5f;
            if (overflow)
            {
                contentWidth -= 8 * s;
                columnWidth = Mathf.Max(1, (contentWidth - gap * 2) / 3);
                height = ColumnHeight(columnWidth, s, columns);
            }
            height = Mathf.Max(viewport.height, height);
            Rect content = new Rect(viewport.x, viewport.y, contentWidth, viewport.height);
            descriptionScroll.x = 0;
            descriptionScroll.y = overflow ? Mathf.Clamp(descriptionScroll.y, 0, height - viewport.height) : 0;
            // Native scrollview owns wheel/trackpad input; its bulky chrome is disabled.
            descriptionScroll = GUI.BeginScrollView(content, descriptionScroll, new Rect(0, 0, contentWidth, height),
                false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = i * (columnWidth + gap), top = 0;
                    if (!string.IsNullOrEmpty(columns[i].Heading))
                    {
                        float headingHeight = headingStyle.CalcHeight(new GUIContent(columns[i].Heading), columnWidth);
                        Label(new Rect(x, 0, columnWidth, headingHeight), columns[i].Heading, headingStyle, WithOpacity(Color.white, opacity));
                        top = headingHeight + 5 * s;
                    }
                    Color bodyInk = WithOpacity(Theme.Rgb(0x95A4B2), opacity);
                    if (columns[i].SourceLines != null)
                    {
                        float pitch = SourceLinePitch * s;
                        for (int line = 0; line < columns[i].SourceLines.Length; line++)
                            if (!string.IsNullOrEmpty(columns[i].SourceLines[line]))
                                Label(new Rect(x, top + line * pitch, columnWidth, pitch), columns[i].SourceLines[line], sourceLineStyle, bodyInk);
                    }
                    else Label(new Rect(x, top, columnWidth, Mathf.Max(1, height - top)), columns[i].Body, bodyStyle, bodyInk);
                }
            }
            finally { GUI.EndScrollView(interactive); }
            if (overflow)
            {
                scrollThumb.fixedHeight = Mathf.Min(viewport.height, Mathf.Max(18.667f * s, viewport.height * viewport.height / height));
                GUI.color = WithOpacity(Color.white, opacity);
                bool oldEnabled = GUI.enabled;
                float sliderValue;
                try
                {
                    GUI.enabled = oldEnabled && interactive;
                    sliderValue = GUI.VerticalSlider(new Rect(viewport.xMax - 6.667f * s, viewport.y, 6.667f * s, viewport.height),
                        descriptionScroll.y, 0, height - viewport.height, scrollTrack, scrollThumb);
                }
                finally { GUI.enabled = oldEnabled; }
                if (interactive) descriptionScroll.y = sliderValue;
                GUI.color = Color.white;
            }
        }

        private static float ColumnHeight(float width, float scale, ColumnCopy[] columns)
        {
            float height = 0;
            for (int i = 0; i < 3; i++)
            {
                float heading = string.IsNullOrEmpty(columns[i].Heading) ? 0 : headingStyle.CalcHeight(new GUIContent(columns[i].Heading), width) + 5 * scale;
                float body = columns[i].SourceLines == null
                    ? bodyStyle.CalcHeight(new GUIContent(columns[i].Body), width)
                    : columns[i].SourceLines.Length * SourceLinePitch * scale;
                height = Mathf.Max(height, heading + body);
            }
            return height;
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = color;
                GUI.Label(rect, text ?? string.Empty, style);
            }
        }

        private static void EnsureStyles()
        {
            float s = EntryScene.Scale;
            if (bodyStyle != null && Mathf.Approximately(styleScale, s) && bodyStyle.font == Text.CurFontStyle.font) return;
            styleScale = s;
            bodyStyle = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(8 * s)),
                alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Normal, wordWrap = true, richText = true, padding = new RectOffset(), margin = new RectOffset() };
            bodyStyle.normal.textColor = Color.white;
            sourceLineStyle = new GUIStyle(bodyStyle) { wordWrap = false, richText = false, clipping = TextClipping.Clip };
            headingStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(9.333f * s)), fontStyle = FontStyle.Bold };
            footerStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(9.333f * s)), alignment = TextAnchor.MiddleLeft };
            statusStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(8 * s)), alignment = TextAnchor.MiddleRight };
            titleStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(13.333f * s)), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            if (scrollTrack == null)
            {
                scrollTrack = ScrollStyle(Theme.Rgb(0x10171C));
                scrollThumb = ScrollStyle(Theme.Rgb(0x23323D));
                scrollThumb.hover.background = scrollThumb.active.background = Solid(Theme.Rgb(0x3A4B58));
            }
            scrollThumb.fixedWidth = 2.667f * s;
            scrollThumb.margin = new RectOffset(Mathf.RoundToInt(2 * s), Mathf.RoundToInt(2 * s), 0, 0);
            if (previewFade == null)
            {
                previewFade = new Texture2D(1, 128, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var pixels = new Color[128]; Color card = Theme.Rgb(0x0D1318);
                for (int i = 0; i < pixels.Length; i++)
                {
                    float t = 1 - i / 127f;
                    Color c = card; c.a = t * t * (3 - 2 * t); pixels[i] = c;
                }
                previewFade.SetPixels(pixels); previewFade.Apply(false, true);
            }
        }

        private static GUIStyle ScrollStyle(Color color)
        {
            var style = new GUIStyle(); style.normal.background = Solid(color); return style;
        }
        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply(false, true); return texture;
        }

        // Source-proven English headings/copy from Docs/UI-Reworked/details/dlc.png; localized native copy is retained otherwise.
        private static ColumnCopy[] AnomalySourceColumns() => new[]
        {
            SourceColumn("Hunt the Unseen", "Track soul-stealing hunters,\nexpose hidden parasites,\nand stop infestations before\nthey devour the map.\n\nConfront reality-twisting\nobelisks and descend into\ncaverns to face ancient\nhorrors."),
            SourceColumn("Contain and Exploit", "Capture monsters in high-\nsecurity facilities and\nharvest their powers for\nvoid-charged weapons,\ngear, and serums.\n\nTurn your enemies’\nabominations into your\ngreatest assets."),
            SourceColumn("Defy the Cult", "Battle sinister cultists and\ntheir psychic rituals\nbefore they claim your\ncolonists.\n\nWield their dark magic to\nsummon the dead, spread\nmadness, and drain your\nfoes.")
        };
        // Only the source-proven default-English card has this authored line grid.
        // GetColumns caches these arrays; localized/native copy continues to wrap and scroll.
        private static ColumnCopy SourceColumn(string heading, string body) =>
            new ColumnCopy { Heading = heading, Body = body, SourceLines = body.Split('\n') };
        private static ColumnCopy[] NativeCopyColumns(string text)
        {
            string[] native = NativeColumns(text);
            return new[] { NativeColumn(native[0]), NativeColumn(native[1]), NativeColumn(native[2]) };
        }

        private static ColumnCopy NativeColumn(string text)
        {
            var copy = new ColumnCopy { Body = text };
            // Unshown DLC hierarchy: promote an existing short native sentence, never invent a marketing heading or cut markup.
            if (text.IndexOf('<') >= 0) return copy;
            int end = text.IndexOfAny(new[] { '.', '!', '?', '。', '！', '？' });
            if (end >= 12 && end <= 110)
            {
                copy.Heading = text.Substring(0, end + 1);
                copy.Body = text.Substring(end + 1).TrimStart();
            }
            return copy;
        }

        private static string[] NativeColumns(string description)
        {
            string[] paragraphs = description.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            // Plain unstructured copy can flow at whole-sentence boundaries. Never split an open rich-text span.
            if (paragraphs.Length == 1 && paragraphs[0].IndexOf('<') < 0)
                paragraphs = System.Text.RegularExpressions.Regex.Split(paragraphs[0], @"(?<=[.!?])\s+(?=\p{Lu})");
            var result = new[] { new StringBuilder(), new StringBuilder(), new StringBuilder() };
            int target = Mathf.Max(1, description.Length / 3), column = 0;
            for (int i = 0; i < paragraphs.Length; i++)
            {
                string paragraph = paragraphs[i].Trim();
                if (column < 2 && result[column].Length > 0 && result[column].Length + paragraph.Length > target && paragraphs.Length - i >= 2 - column) column++;
                if (result[column].Length > 0) result[column].Append("\n\n");
                result[column].Append(paragraph);
            }
            return new[] { result[0].ToString(), result[1].ToString(), result[2].ToString() };
        }

        private static List<ExpansionDef> OrderedExpansions()
        {
            var list = new List<ExpansionDef>();
            foreach (ExpansionDef expansion in ModLister.AllExpansions) if (!expansion.isCore) list.Add(expansion);
            list.Sort((a, b) =>
            {
                int x = Array.FindIndex(SourceOrder, name => string.Equals(name, a.defName, StringComparison.OrdinalIgnoreCase));
                int y = Array.FindIndex(SourceOrder, name => string.Equals(name, b.defName, StringComparison.OrdinalIgnoreCase));
                x = x < 0 ? SourceOrder.Length : x; y = y < 0 ? SourceOrder.Length : y;
                return x == y ? string.Compare(a.defName, b.defName, StringComparison.Ordinal) : x.CompareTo(y);
            });
            return list;
        }

        private static Texture2D Preview(ExpansionDef expansion)
        {
            if (Previews.TryGetValue(expansion, out Texture2D cached)) return cached;
            Texture2D result = null;
            try
            {
                var previews = expansion.PreviewImages;
                if (previews != null && previews.Count > 0) result = previews[0];
                if (result == null && !expansion.backgroundPath.NullOrEmpty()) result = expansion.BackgroundImage;
            }
            catch (Exception error) { Log.Warning("[Reframed UI] Expansion preview unavailable for " + expansion.defName + ": " + error.Message); }
            Previews[expansion] = result;
            return result;
        }
    }
}

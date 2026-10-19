using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Same data mutations as WorldFactionsUIUtility, with compact entry-scene
    // presentation. Native patches to that utility's drawing methods do not run here.
    internal sealed class NewGameWorldFactions
    {
        private List<FactionDef> currentFactions;
        private readonly List<int> visibleIndices = new List<int>();
        private Vector2 scroll;
        private AddFactionPopup popup;
        private GUIStyle label, small, countStyle, actionLabel, heading, warning, track, thumb;
        private float styleScale = -1, scale = 1, opacity = 1;
        private int lastDrawFrame;
        private static readonly AccessTools.FieldRef<Scenario, List<ScenPart>> ScenarioParts =
            AccessTools.FieldRefAccess<Scenario, List<ScenPart>>("parts");
        private static readonly Color Ink = Theme.Rgb(0xF2F5F7), Muted = Theme.Rgb(0x9BAAB8);

        internal void Draw(Rect rect, List<FactionDef> factions, float scale, float opacity, Action reset)
        {
            currentFactions = factions;
            lastDrawFrame = Time.frameCount;
            if (factions == null || rect.width <= 0 || rect.height <= 0) return;
            using (new Theme.GuiScope(true))
            {
                this.scale = Mathf.Max(.1f, scale);
                this.opacity = Mathf.Clamp01(opacity * GUI.color.a);
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small;
                EnsureStyles();
                Heading(new Rect(rect.x, rect.y, rect.width - 48 * scale, 20 * scale), "Factions".Translate());
                Label(new Rect(rect.xMax - 48 * scale, rect.y, 48 * scale, 20 * scale),
                    NonHiddenCount(factions) + "/12", countStyle, Muted);
                if (GUI.enabled)
                {
                    string special = string.Join(", ", factions.Where(def => def != null && def.hidden && def.displayInFactionSelection).Distinct().Select(def => def.LabelCap.ToString()));
                    string tip = "FactionSelectionDesc".Translate(12) + "\n\nThe counter tracks settlement factions. Special factions do not count toward this limit.";
                    if (!string.IsNullOrEmpty(special)) tip += "\n" + special;
                    TooltipHandler.TipRegion(new Rect(rect.x, rect.y, rect.width, 22 * scale), tip);
                }
                float buttonY = rect.yMax - 32 * scale;
                Rect viewport = new Rect(rect.x, rect.y + 28 * scale, rect.width,
                    Mathf.Max(20 * scale, buttonY - rect.y - 38 * scale));
                visibleIndices.Clear();
                for (int i = 0; i < factions.Count; i++)
                    if (factions[i] != null && factions[i].displayInFactionSelection) visibleIndices.Add(i);
                string warnings = Warnings(factions);
                float contentWidth = Mathf.Max(1, viewport.width - 9 * scale);
                float rowsHeight = visibleIndices.Count * 36 * scale;
                float warningHeight = string.IsNullOrEmpty(warnings) ? 0 : warning.CalcHeight(new GUIContent(warnings), contentWidth - 8 * scale) + 16 * scale;
                float contentHeight = Mathf.Max(viewport.height, rowsHeight + warningHeight + 2 * scale);
                BeginScroll(viewport, ref scroll, contentHeight);
                try
                {
                    for (int row = 0; row < visibleIndices.Count; row++)
                    {
                        int index = visibleIndices[row];
                        Rect rowRect = new Rect(0, row * 36 * scale, contentWidth, 34 * scale);
                        if (rowRect.yMax < scroll.y || rowRect.y > scroll.y + viewport.height) continue;
                        if (DrawRow(rowRect, factions[index], factions, index, row)) break;
                    }
                    if (warningHeight > 0)
                        Label(new Rect(4 * scale, rowsHeight + 10 * scale, contentWidth - 8 * scale, warningHeight), warnings, warning, Theme.Rgb(0xD4B568));
                }
                finally { GUI.EndScrollView(GUI.enabled); }
                if (contentHeight > viewport.height + .5f) Scrollbar(viewport, ref scroll, contentHeight);
                float half = (rect.width - 8 * scale) / 2;
                Rect add = new Rect(rect.x, buttonY, half, 32 * scale);
                Rect restore = new Rect(add.xMax + 8 * scale, buttonY, half, 32 * scale);
                if (Button(add, "Add".Translate().CapitalizeFirst() + "…") && TutorSystem.AllowAction("ConfiguringWorldFactions"))
                {
                    if (popup == null)
                    {
                        popup = new AddFactionPopup(this, UI.GUIToScreenRect(add), scale);
                        Find.WindowStack.Add(popup);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                if (Button(restore, "ResetFactions".Translate(), reset != null))
                {
                    popup?.Close();
                    reset?.Invoke();
                    // Native ResetFactionCounts replaces the list. Never leave an open
                    // popup or later callback pointing at the discarded collection.
                    currentFactions = null;
                    scroll = Vector2.zero;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                }
            }
        }

        private bool DrawRow(Rect rect, FactionDef def, List<FactionDef> factions, int index, int row)
        {
            bool protectedFaction = IsProtected(def);
            bool hover = GUI.enabled && Mouse.IsOver(rect);
            if (hover || row % 2 == 1)
                Theme.RoundRect(rect, Alpha(Theme.Rgb(0x181F25), hover ? .8f : .48f), 4 * scale);
            Icon(new Rect(rect.x + 5 * scale, rect.y + 5 * scale, 24 * scale, 24 * scale), def.FactionIcon, def.DefaultColor);
            float removeX = rect.xMax - 25 * scale, infoX = removeX - 27 * scale;
            Rect text = new Rect(rect.x + 35 * scale, rect.y, Mathf.Max(1, infoX - rect.x - 39 * scale), rect.height);
            Label(text, Ellipsis(def.LabelCap, label, text.width), label, Ink);
            if (GUI.enabled) TooltipHandler.TipRegion(rect, Tip(def, protectedFaction));
            Info(new Rect(infoX, rect.y + 5 * scale, 24 * scale, 24 * scale), def);
            Rect remove = new Rect(removeX, rect.y + 5 * scale, 24 * scale, 24 * scale);
            if (GUI.enabled && Mouse.IsOver(remove)) Theme.ButtonFace(remove, Theme.Surface, radius: 3 * scale, opacity: opacity);
            Icon(remove.ContractedBy(4 * scale), SaveBrowserIcons.Delete, protectedFaction ? new Color(1, 1, 1, .3f) : Color.white);
            if (!protectedFaction && GUI.enabled && Widgets.ButtonInvisible(remove) && TutorSystem.AllowAction("ConfiguringWorldFactions"))
            {
                // Index identity matters for repeated definitions. Do not Remove(def).
                if (index >= 0 && index < factions.Count && factions[index] == def)
                {
                    factions.RemoveAt(index);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    return true;
                }
            }
            return false;
        }

        private static bool IsProtected(FactionDef def)
        {
            Scenario scenario = Current.Game?.Scenario;
            if (scenario == null) return false;
            List<ScenPart> parts = ScenarioParts(scenario);
            if (parts == null) return false;
            foreach (ScenPart part in parts)
                if (part?.def?.preventRemovalOfFaction == def) return true;
            return false;
        }

        private static string Tip(FactionDef def, bool protectedFaction = false)
        {
            string text = def.LabelCap.AsTipTitle() + "\n" + def.Description;
            if (protectedFaction) text += "\n\n" + "FactionRemovalDisabled".Translate().Colorize(ColorLibrary.RedReadable);
            return text;
        }

        private static int NonHiddenCount(List<FactionDef> factions) => factions.Count(def => def != null && !def.hidden);
        private static AcceptanceReport CanAdd(FactionDef def, List<FactionDef> factions)
        {
            if (factions == null) return false;
            if (!def.hidden && NonHiddenCount(factions) >= 12)
                return "TotalFactionsAllowed".Translate(12).ToString().UncapitalizeFirst();
            if (factions.Count(item => item == def) >= def.maxConfigurableAtWorldCreation)
                return "MaxFactionsForType".Translate(def.maxConfigurableAtWorldCreation).ToString().UncapitalizeFirst();
            return true;
        }

        private static string Warnings(List<FactionDef> factions)
        {
            var text = new StringBuilder();
            if (NonHiddenCount(factions) == 0) text.AppendLine("FactionsDisabledWarning".Translate());
            else
            {
                if (ModsConfig.RoyaltyActive && !factions.Contains(FactionDefOf.Empire))
                    text.AppendLine("Warning".Translate() + ": " + "FactionDisabledContentWarning".Translate(FactionDefOf.Empire.label));
                if (!factions.Contains(FactionDefOf.Mechanoid))
                    text.AppendLine("Warning".Translate() + ": " + "MechanoidsDisabledContentWarning".Translate(FactionDefOf.Mechanoid.label));
                if (!factions.Contains(FactionDefOf.Insect))
                    text.AppendLine("Warning".Translate() + ": " + "InsectsDisabledContentWarning".Translate(FactionDefOf.Insect.label));
                if (ModsConfig.OdysseyActive)
                {
                    if (!factions.Contains(FactionDefOf.Mechanoid))
                        text.AppendLine("Warning".Translate() + ": " + "OdysseyFactionDisabledContentWarning".Translate(FactionDefOf.Mechanoid.label));
                    if (!factions.Contains(FactionDefOf.Insect))
                        text.AppendLine("Warning".Translate() + ": " + "OdysseyFactionDisabledContentWarning".Translate(FactionDefOf.Insect.label));
                }
            }
            return text.ToString().TrimEnd('\r', '\n');
        }

        private bool Button(Rect rect, string text, bool enabled = true)
        {
            bool available = enabled && GUI.enabled;
            Theme.ButtonFace(rect, available && Mouse.IsOver(rect) ? Theme.Hover : Theme.Surface,
                disabled: !enabled, radius: 4 * scale, opacity: opacity);
            Label(rect.ContractedBy(8 * scale, 0), Ellipsis(text, actionLabel, rect.width - 16 * scale), actionLabel, enabled ? Ink : Muted);
            if (GUI.enabled) TooltipHandler.TipRegion(rect, text);
            return available && Widgets.ButtonInvisible(rect);
        }

        private bool Info(Rect rect, FactionDef def)
        {
            Icon(rect.ContractedBy(4 * scale), TexButton.Info, Muted);
            if (!GUI.enabled || !Widgets.ButtonInvisible(rect)) return false;
            Find.WindowStack.Add(new Dialog_InfoCard(def));
            return true;
        }

        private void Icon(Rect rect, Texture2D icon, Color color)
        {
            if (icon == null) return;
            GUI.color = Alpha(color);
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
        }

        private void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = true;
            GUI.color = Alpha(color);
            try { GUI.Label(rect, text ?? string.Empty, style); }
            finally { GUI.color = Color.white; GUI.enabled = enabled; }
        }

        private void Heading(Rect rect, string text)
        {
            float x = rect.x;
            foreach (char letter in text.ToUpperInvariant())
            {
                string glyph = letter.ToString();
                float width = heading.CalcSize(new GUIContent(glyph)).x;
                Label(new Rect(x, rect.y, width + scale, rect.height), glyph, heading, Ink);
                x += width + .55f * scale;
            }
        }

        private void BeginScroll(Rect viewport, ref Vector2 position, float height)
        {
            position.x = 0;
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, height - viewport.height));
            position = GUI.BeginScrollView(viewport, position, new Rect(0, 0, viewport.width, height),
                false, false, GUIStyle.none, GUIStyle.none);
        }

        private void Scrollbar(Rect viewport, ref Vector2 position, float height)
        {
            thumb.fixedHeight = Mathf.Min(viewport.height, Mathf.Max(22 * scale, viewport.height * viewport.height / height));
            GUI.color = Alpha(Theme.Rgb(0x384957));
            position.y = GUI.VerticalSlider(new Rect(viewport.xMax - 6 * scale, viewport.y, 6 * scale, viewport.height),
                position.y, 0, height - viewport.height, track, thumb);
            GUI.color = Color.white;
        }

        private Color Alpha(Color color, float extra = 1) { color.a *= opacity * extra; return color; }
        private static string Ellipsis(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;
            const string tail = "…";
            for (int count = text.Length - 1; count > 0; count--)
            {
                string fitted = text.Substring(0, count) + tail;
                if (style.CalcSize(new GUIContent(fitted)).x <= width) return fitted;
            }
            return tail;
        }

        private void EnsureStyles()
        {
            if (label != null && Mathf.Approximately(styleScale, scale) && label.font == Text.CurFontStyle.font) return;
            styleScale = scale;
            label = Style(14, false, TextAnchor.MiddleLeft);
            small = Style(11, false, TextAnchor.MiddleLeft);
            countStyle = Style(11, false, TextAnchor.MiddleRight);
            actionLabel = Style(14, false, TextAnchor.MiddleCenter);
            heading = Style(9, false, TextAnchor.MiddleLeft, FontStyle.Bold);
            warning = Style(12, true, TextAnchor.UpperLeft);
            warning.richText = true;
            track = new GUIStyle { margin = new RectOffset(), padding = new RectOffset() };
            thumb = new GUIStyle { fixedWidth = 3 * scale, margin = new RectOffset(), padding = new RectOffset() };
            thumb.normal.background = thumb.hover.background = thumb.active.background = BaseContent.WhiteTex;
        }

        private GUIStyle Style(float size, bool wrap, TextAnchor anchor, FontStyle weight = FontStyle.Normal)
        {
            var result = new GUIStyle(Text.CurFontStyle) { fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale)),
                fontStyle = weight, alignment = anchor, wordWrap = wrap, richText = false,
                margin = new RectOffset(), padding = new RectOffset(), clipping = TextClipping.Clip };
            result.normal.textColor = Color.white;
            return result;
        }

        private sealed class AddFactionPopup : Window
        {
            private readonly NewGameWorldFactions owner;
            private readonly Rect anchor;
            private readonly float popupScale;
            private readonly FactionDef[] options;
            private Vector2 popupScroll;

            internal AddFactionPopup(NewGameWorldFactions owner, Rect anchor, float scale)
            {
                this.owner = owner;
                this.anchor = anchor;
                popupScale = scale;
                options = FactionGenerator.ConfigurableFactions.Where(def => def != null && def.displayInFactionSelection).ToArray();
                doWindowBackground = doCloseButton = doCloseX = drawShadow = false;
                closeOnAccept = false;
                closeOnCancel = closeOnClickedOutside = absorbInputAroundWindow = true;
                draggable = false;
                layer = WindowLayer.Super;
            }

            protected override float Margin => 0;
            public override Vector2 InitialSize => new Vector2(Mathf.Min(340 * popupScale, UI.screenWidth - 16), Mathf.Min(440 * popupScale, UI.screenHeight - 16));
            protected override void SetInitialSizeAndPosition()
            {
                Vector2 size = InitialSize;
                float x = Mathf.Clamp(anchor.x, 8, UI.screenWidth - size.x - 8);
                float y = anchor.y - size.y - 6 * popupScale;
                if (y < 8) y = Mathf.Clamp(anchor.yMax + 6 * popupScale, 8, UI.screenHeight - size.y - 8);
                windowRect = new Rect(x, y, size.x, size.y).Rounded();
            }

            public override void WindowUpdate()
            {
                base.WindowUpdate();
                if (owner.currentFactions == null || Time.frameCount - owner.lastDrawFrame > 2) Close();
            }

            public override void PostClose()
            {
                if (ReferenceEquals(owner.popup, this)) owner.popup = null;
                base.PostClose();
            }

            public override void DoWindowContents(Rect rect)
            {
                List<FactionDef> factions = owner.currentFactions;
                if (factions == null) return;
                using (new Theme.GuiScope(true))
                {
                    float oldScale = owner.scale, oldOpacity = owner.opacity;
                    owner.scale = popupScale;
                    owner.opacity = 1;
                    Text.Font = GameFont.Small;
                    GUI.color = GUI.contentColor = Color.white;
                    owner.EnsureStyles();
                    try
                    {
                        Color shell = Theme.Rgb(0x0D1318); shell.a = .96f;
                        Theme.RoundRect(rect, shell, 10 * popupScale);
                        Rect back = new Rect(10 * popupScale, 9 * popupScale, 24 * popupScale, 24 * popupScale);
                        owner.Icon(back.ContractedBy(4 * popupScale), SaveBrowserIcons.Back, Ink);
                        if (Widgets.ButtonInvisible(back)) { Close(); return; }
                        owner.Label(new Rect(43 * popupScale, 9 * popupScale, rect.width - 53 * popupScale, 24 * popupScale),
                            "Add".Translate().CapitalizeFirst() + " — " + "Factions".Translate(), owner.label, Ink);
                        Rect viewport = new Rect(10 * popupScale, 42 * popupScale, rect.width - 20 * popupScale, rect.height - 54 * popupScale);
                        float rowHeight = 48 * popupScale, height = Mathf.Max(viewport.height, options.Length * rowHeight + 2 * popupScale);
                        owner.BeginScroll(viewport, ref popupScroll, height);
                        try
                        {
                            for (int i = 0; i < options.Length; i++)
                            {
                                FactionDef def = options[i];
                                Rect row = new Rect(0, i * rowHeight, viewport.width - 9 * popupScale, rowHeight - 2 * popupScale);
                                if (row.yMax < popupScroll.y || row.y > popupScroll.y + viewport.height) continue;
                                AcceptanceReport report = CanAdd(def, factions);
                                bool allowed = report.Accepted;
                                if (Mouse.IsOver(row)) Theme.RoundRect(row, owner.Alpha(Theme.Rgb(0x202B34), .8f), 4 * popupScale);
                                owner.Icon(new Rect(5 * popupScale, row.y + 10 * popupScale, 24 * popupScale, 24 * popupScale),
                                    def.FactionIcon, allowed ? def.DefaultColor : def.DefaultColor * new Color(1, 1, 1, .5f));
                                int count = factions.Count(item => item == def);
                                string name = def.LabelCap + (count > 0 ? " (" + count + ")" : string.Empty);
                                Rect title = new Rect(36 * popupScale, row.y + 2 * popupScale, row.width - 68 * popupScale, 23 * popupScale);
                                owner.Label(title, Ellipsis(name, owner.label, title.width), owner.label, allowed ? Ink : Muted);
                                string detail = allowed ? "Add".Translate().CapitalizeFirst().ToString() : report.Reason;
                                Rect sub = new Rect(title.x, row.y + 24 * popupScale, title.width, 18 * popupScale);
                                owner.Label(sub, Ellipsis(detail, owner.small, sub.width), owner.small, allowed ? Muted : Theme.Rgb(0xD4B568));
                                TooltipHandler.TipRegion(row, Tip(def) + (!allowed && !string.IsNullOrEmpty(report.Reason) ? "\n\n" + report.Reason : string.Empty));
                                if (owner.Info(new Rect(row.xMax - 25 * popupScale, row.y + 10 * popupScale, 24 * popupScale, 24 * popupScale), def))
                                {
                                    // InfoCard is a native Dialog-layer window. Remove the
                                    // Super-layer picker so it cannot cover or block that card.
                                    Close(false);
                                    break;
                                }
                                Rect choose = new Rect(row.x, row.y, row.width - 29 * popupScale, row.height);
                                if (GUI.enabled && allowed && Widgets.ButtonInvisible(choose) && TutorSystem.AllowAction("ConfiguringWorldFactions"))
                                {
                                    // Recheck against the currently bound list at the action boundary.
                                    List<FactionDef> current = owner.currentFactions;
                                    if (current != null && CanAdd(def, current))
                                    {
                                        current.Add(def);
                                        SoundDefOf.Click.PlayOneShotOnCamera();
                                        Close(false);
                                        break;
                                    }
                                }
                            }
                        }
                        finally { GUI.EndScrollView(GUI.enabled); }
                        if (height > viewport.height + .5f) owner.Scrollbar(viewport, ref popupScroll, height);
                    }
                    finally { owner.scale = oldScale; owner.opacity = oldOpacity; }
                }
            }
        }
    }
}

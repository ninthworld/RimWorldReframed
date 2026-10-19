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
    internal static class NotificationCards
    {
        private static readonly MethodInfo LabelMethod = AccessTools.DeclaredMethod(typeof(Letter), "PostProcessedLabel", Type.EmptyTypes);
        private static readonly NotificationMotion<Letter> motion = new NotificationMotion<Letter>();
        private static readonly Dictionary<Letter, Presentation> presentations = new Dictionary<Letter, Presentation>(NotificationIdentityComparer<Letter>.Instance);
        private static readonly Dictionary<Type, bool> compatible = new Dictionary<Type, bool>();
        private static Texture2D accentFade;
        private static float width, limit;
        private static object game;
        private static Letter[] queueSnapshot = Array.Empty<Letter>();
        private sealed class Presentation
        {
            internal string Title, Tooltip, Icon;
            internal Texture NativeIcon;
            internal Color Color;
            internal int Arrival;
        }
        internal static bool Ready => LabelMethod?.ReturnType == typeof(string);
        internal static bool CanHandle(List<Letter> letters, float availableWidth, float maxHeight)
        {
            if (!NotificationPolicy.Supported(availableWidth, maxHeight) || !CanHandle(letters)) return false;
            return NotificationPolicy.Bundled(letters.Count, NotificationPolicy.Capacity(maxHeight)) == 0 ||
                CanHandle(new List<Letter> { Find.LetterStack.BundleLetter });
        }
        internal static bool CanHandle(List<Letter> letters)
        {
            if (!Ready || letters == null) return false;
            var seen = new HashSet<Letter>(NotificationIdentityComparer<Letter>.Instance);
            foreach (Letter letter in letters)
            {
                if (letter == null || letter.def == null || !seen.Add(letter)) return false;
                Type type = letter.GetType();
                if (!compatible.TryGetValue(type, out bool supported))
                {
                    supported = AccessTools.Method(type, nameof(Letter.DrawButtonAt), new[] { typeof(float) })?.DeclaringType == typeof(Letter) &&
                        AccessTools.Method(type, nameof(Letter.CheckForMouseOverTextAt), new[] { typeof(float) })?.DeclaringType == typeof(Letter);
                    compatible[type] = supported;
                }
                if (!supported) return false;
            }
            return true;
        }
        internal static void Reset()
        { motion.Reset(); presentations.Clear(); queueSnapshot = Array.Empty<Letter>(); game = null; width = limit = 0; }

        internal static float Prepare(List<Letter> letters, float availableWidth, float maxHeight, double now)
        {
            if (!ReferenceEquals(game, Current.Game)) { Reset(); game = Current.Game; }
            width = availableWidth; limit = Math.Max(0, maxHeight);
            int capacity = NotificationPolicy.Capacity(limit);
            if (!CanHandle(letters, width, limit) || capacity == 0)
            { motion.Reset(); presentations.Clear(); queueSnapshot = Array.Empty<Letter>(); return 0; }
            queueSnapshot = letters.ToArray();
            var visible = new List<Letter>();
            int bundled = NotificationPolicy.Bundled(letters.Count, capacity);
            if (bundled > 0)
            {
                BundleLetter bundle = Find.LetterStack.BundleLetter;
                bundle.SetLetters(letters.Take(bundled).ToList());
                visible.Add(bundle);
            }
            visible.AddRange(letters.Skip(bundled));
            foreach (Letter letter in visible) presentations[letter] = Describe(letter);
            float occupied = (float)motion.Prepare(visible, now);
            var retained = new HashSet<Letter>(motion.Nodes.Select(node => node.Key), NotificationIdentityComparer<Letter>.Instance);
            foreach (Letter stale in presentations.Keys.Where(key => !retained.Contains(key)).ToArray()) presentations.Remove(stale);
            return Math.Min(limit, occupied);
        }
        private static Presentation Describe(Letter letter)
        {
            var info = new Presentation { Title = (string)LabelMethod.Invoke(letter, null), Tooltip = ((IArchivable)letter).ArchivedTooltip,
                NativeIcon = letter.def.Icon, Color = letter.def.color, Arrival = letter.arrivalTick };
            var style = NotificationPolicy.Presentation(letter.def.defName, letter is NewQuestLetter || letter is ChoiceLetter choice && choice.quest != null);
            info.Icon = style.Icon;
            if (style.Tint.HasValue) info.Color = Theme.Rgb(style.Tint.Value);
            string contextIcon = NotificationContext.Icon(letter);
            if (contextIcon != null) info.Icon = contextIcon;
            if (letter is BundleLetter) { info.Icon = "envelope"; info.Arrival = -1; }
            return info;
        }
        internal static void Draw(Rect bounds, bool input)
        {
            if (width <= 0 || bounds.height <= 0) return;
            GUI.BeginClip(bounds);
            try
            {
                // All rows are inert during reflow. A disappearing event cannot
                // hand its MouseDown to a different event under the pointer.
                bool settled = motion.Settled;
                bool queueStable = Find.LetterStack.LettersListForReading.SequenceEqual(queueSnapshot, NotificationIdentityComparer<Letter>.Instance);
                foreach (var node in motion.Nodes.OrderBy(n => n.Live ? 1 : 0).ThenByDescending(n => n.Bottom))
                {
                    if (!presentations.TryGetValue(node.Key, out Presentation info)) continue;
                    float reveal = (float)NotificationPolicy.Ease(node.Progress);
                    Rect full = new Rect(Math.Max(0, bounds.width - width), bounds.height - (float)node.Bottom - NotificationPolicy.Row, width, NotificationPolicy.Row);
                    Rect shell = new Rect(full.xMax - full.width * reveal, full.yMax - full.height * reveal, full.width * reveal, full.height * reveal);
                    if (shell.width <= 0 || shell.height <= 0) continue;
                    using (new Theme.GuiScope(true))
                    {
                        GUI.color = GUI.contentColor = Color.white;
                        Theme.RoundRect(shell, Theme.PanelFill, 6);
                        PaintAccent(shell, info.Color);
                        GUI.BeginClip(shell);
                        Vector2 paintMouse = Event.current.mousePosition;
                        try
                        {
                            // Shared labels add truncation tips. Painting must
                            // never register hidden/moving control tooltips;
                            // the stable full-row region below owns them.
                            Event.current.mousePosition = new Vector2(-100000, -100000);
                            Rect contents = new Rect(full.x - shell.x, full.y - shell.y, full.width, full.height);
                            Rect icon = new Rect(contents.x + 8, contents.y + 8, 14, 14);
                            GUI.color = info.Color;
                            GUI.DrawTexture(icon, info.Icon == null ? info.NativeIcon : RightHudIcons.Get(info.Icon), ScaleMode.ScaleToFit);
                            GUI.color = Color.white;
                            string age = info.Arrival < 0 ? "" : RelativeTime(info.Arrival);
                            float timeWidth = Math.Min(58, PawnInspectorView.MeasureSmall(age));
                            PawnInspectorView.Label(new Rect(contents.x + 29, contents.y, Math.Max(0, contents.width - 39 - timeWidth), contents.height), info.Title, info.Color);
                            PawnInspectorView.SmallLabel(new Rect(contents.xMax - timeWidth - 8, contents.y, timeWidth, contents.height), age, Theme.Muted, right: true);
                        }
                        finally { Event.current.mousePosition = paintMouse; GUI.EndClip(); }
                        bool member = node.Key is BundleLetter ? Find.LetterStack.LettersListForReading.Count > 0 : Find.LetterStack.LettersListForReading.Any(letter => ReferenceEquals(letter, node.Key));
                        bool fullyVisible = full.y >= 0 && full.yMax <= bounds.height;
                        bool live = input && GUI.enabled && node.Live && node.Settled && settled && queueStable && member && fullyVisible;
                        if (!live || !Mouse.IsOver(full)) continue;
                        TooltipHandler.TipRegion(full, info.Title + "\n\n" + info.Tooltip);
                        if (Event.current.type == EventType.Repaint) Find.LetterStack.Notify_LetterMouseover(node.Key);
                        Event ev = Event.current;
                        if (!NotificationPolicy.CanActivate(input, GUI.enabled, node.Live, settled, member, ev.type == EventType.MouseDown, ev.button)) continue;
                        if (ev.button == 1)
                        {
                            if (node.Key.CanDismissWithRightClick)
                            { SoundDefOf.Click.PlayOneShotOnCamera(); Find.LetterStack.RemoveLetter(node.Key); }
                            ev.Use(); return;
                        }
                        node.Key.OpenLetter(); ev.Use(); return;
                    }
                }
            }
            finally { GUI.EndClip(); }
        }
        private static string RelativeTime(int arrival)
        {
            int ticks = NotificationPolicy.AgeTicks(Find.TickManager?.TicksGame ?? arrival, arrival);
            return ticks < 60 ? "Just now" : "TimeAgo".Translate(ticks.ToStringTicksToPeriod(allowSeconds: true, shortForm: true)).ToString();
        }
        internal static void PaintAccent(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (accentFade == null)
            {
                accentFade = new Texture2D(128, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[128];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 1, 1, .16f * (float)Math.Pow(1 - i / 127f, 2));
                accentFade.SetPixels(pixels); accentFade.Apply(false, true);
            }
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                GUI.DrawTexture(rect, accentFade, ScaleMode.StretchToFill, true, 0, color, 0, 6);
            }
        }
    }
}

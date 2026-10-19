using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Persistent native alerts use the notification presentation without becoming
    // letters. Their condition and original target action remain authoritative.
    internal static class CriticalIssueCards
    {
        private static readonly NotificationMotion<Alert> motion = new NotificationMotion<Alert>();
        private static readonly Dictionary<Alert, string> labels = new Dictionary<Alert, string>(NotificationIdentityComparer<Alert>.Instance);
        private static Alert[] snapshot = Array.Empty<Alert>();
        private static Vector2 scroll;
        private static float contentHeight;
        internal static bool Moving => !motion.Settled;
        internal static Alert HoveredAlert { get; private set; }
        internal static void Reset()
        { motion.Reset(); labels.Clear(); snapshot = Array.Empty<Alert>(); scroll = Vector2.zero; contentHeight = 0; HoveredAlert = null; }

        internal static float Prepare(List<Alert> active, float maximum, double now)
        {
            HoveredAlert = null;
            snapshot = active.Where(a => a.Active && ColonyIssuePolicy.SeparateAlert((int)a.Priority)).ToArray();
            foreach (Alert alert in snapshot) labels[alert] = alert.Label ?? "";
            contentHeight = (float)motion.Prepare(snapshot, now);
            foreach (Alert stale in labels.Keys.Where(a => !motion.Nodes.Any(n => ReferenceEquals(a, n.Key))).ToArray()) labels.Remove(stale);
            float height = Math.Min(Math.Max(0, maximum), contentHeight);
            scroll.y = Math.Min(scroll.y, Math.Max(0, contentHeight - height));
            return height;
        }

        internal static void Draw(Rect bounds, bool input, List<Alert> active)
        {
            HoveredAlert = null;
            if (bounds.height <= 0 || Event.current.type == EventType.Layout) return;
            if (!input && Event.current.type != EventType.Repaint) return;
            bool same = active.Where(a => a.Active && ColonyIssuePolicy.SeparateAlert((int)a.Priority))
                .SequenceEqual(snapshot, NotificationIdentityComparer<Alert>.Instance);
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                GUI.enabled &= input;
                GUI.BeginGroup(bounds);
                Rect viewport = new Rect(0, 0, bounds.width, bounds.height);
                try
                {
                    float width = PawnInspectorWidgets.BeginScroll(viewport, ref scroll, contentHeight);
                    try
                    {
                        foreach (var node in motion.Nodes.OrderBy(n => n.Live ? 1 : 0))
                        {
                            if (!labels.TryGetValue(node.Key, out string title)) continue;
                            float reveal = (float)NotificationPolicy.Ease(node.Progress);
                            Rect row = new Rect(0, contentHeight - (float)node.Bottom - NotificationPolicy.Row, width, NotificationPolicy.Row);
                            Rect shell = new Rect(row.xMax - row.width * reveal, row.yMax - row.height * reveal, row.width * reveal, row.height * reveal);
                            if (shell.width <= 0 || shell.height <= 0) continue;
                            Theme.RoundRect(shell, Theme.PanelFill, 6);
                            NotificationCards.PaintAccent(shell, Theme.Danger);
                            GUI.BeginClip(shell);
                            Vector2 mouse = Event.current.mousePosition;
                            try
                            {
                                Event.current.mousePosition = new Vector2(-100000, -100000);
                                Rect full = new Rect(row.x - shell.x, row.y - shell.y, row.width, row.height);
                                GUI.color = Theme.Danger;
                                GUI.DrawTexture(new Rect(full.x + 8, full.y + 8, 14, 14), RightHudIcons.Get(ColonyIssues.KindFor(node.Key).Icon));
                                GUI.color = Color.white;
                                PawnInspectorView.Label(new Rect(full.x + 29, full.y, full.width - 37, full.height), title, Theme.Danger);
                            }
                            finally { Event.current.mousePosition = mouse; GUI.EndClip(); }
                            bool clickable = input && GUI.enabled && same && motion.Settled && node.Settled &&
                                node.Key.Active && ColonyIssuePolicy.SeparateAlert((int)node.Key.Priority) &&
                                ColonyIssuePolicy.FullyVisible(row.y, row.yMax, scroll.y, scroll.y + bounds.height);
                            if (!clickable) continue;
                            TooltipHandler.TipRegion(row, title);
                            if (Mouse.IsOver(row)) HoveredAlert = node.Key;
                            if (Widgets.ButtonInvisible(row)) { ColonyIssues.Activate(node.Key); break; }
                        }
                    }
                    finally { PawnInspectorWidgets.EndScroll(viewport, ref scroll, contentHeight, 280083); }
                }
                finally { GUI.EndGroup(); }
            }
        }
    }
}

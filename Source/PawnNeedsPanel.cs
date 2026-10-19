using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnNeedsPanel
    {
        private sealed class KnownGroup
        {
            internal int Key, Stage;
            internal Thought Representative;
        }
        private sealed class PawnState
        {
            internal readonly List<KnownGroup> Groups = new List<KnownGroup>();
            internal readonly PawnNeedsNovelty Novelty = new PawnNeedsNovelty();
            internal int NextKey;
        }
        private sealed class Row
        {
            internal Thought Representative, Leading;
            internal List<Thought> Members;
            internal float Offset;
            internal int Key;
            internal string Label;
        }

        private static readonly Dictionary<Pawn, PawnState> history = new Dictionary<Pawn, PawnState>();
        private static readonly List<Thought> nativeGroups = new List<Thought>();
        private static readonly List<Thought> nativeMembers = new List<Thought>();
        private static readonly List<Row> rows = new List<Row>();
        private static readonly List<Need> needs = new List<Need>();
        private static readonly List<int> keys = new List<int>();
        private static Vector2 thoughtScroll, needScroll;
        private static Game session;
        private static readonly Color Positive = Color.Lerp(Theme.Muted, Theme.Good, .72f);
        private static readonly Color Negative = Color.Lerp(Theme.Muted, Theme.Danger, .75f);
        private static readonly Color Ideology = Theme.Rgb(0x22B8D0);

        // Selection changes reset only presentation/visibility bookkeeping. The
        // pawn's observed thought history survives switching away and back.
        internal static void Reset()
        {
            thoughtScroll = needScroll = Vector2.zero;
            foreach (PawnState state in history.Values) state.Novelty.StopShowing();
        }

        internal static void Draw(Rect rect, Pawn pawn)
        {
            if (pawn == null || rect.width < 80 || rect.height <= 0) return;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                if (session != Current.Game) { history.Clear(); session = Current.Game; Reset(); }
                if (!history.TryGetValue(pawn, out PawnState state)) history.Add(pawn, state = new PawnState());
                float split = rect.width * .54f;
                Rect left = new Rect(rect.x, rect.y, split - 8f, rect.height);
                Rect right = new Rect(rect.x + split + 8f, rect.y, rect.width - split - 8f, rect.height);
                PawnInspectorWidgets.Divider(new Rect(rect.x + split, rect.y + 1f, 1f, Math.Max(0, rect.height - 2f)));
                DrawThoughts(left, pawn, state);
                DrawNeeds(right, pawn);
            }
        }

        private static void ReadGroups(Pawn pawn, PawnState state)
        {
            rows.Clear(); keys.Clear(); nativeGroups.Clear();
            Need_Mood mood = pawn.needs?.mood;
            if (mood == null) { state.Novelty.Observe(keys); return; }
            PawnNeedsUIUtility.GetThoughtGroupsInDisplayOrder(mood, nativeGroups);
            foreach (Thought group in nativeGroups)
            {
                nativeMembers.Clear();
                mood.thoughts.GetMoodThoughts(group, nativeMembers);
                if (nativeMembers.Count == 0) continue;
                Thought leading = PawnNeedsUIUtility.GetLeadingThoughtInGroup(nativeMembers);
                if (leading == null || !leading.VisibleInNeedsTab) continue;
                KnownGroup known = state.Groups.FirstOrDefault(old => old.Representative.def == group.def &&
                    (old.Stage == group.CurStageIndex || group.def.stagesStack) &&
                    old.Representative.GroupsWith(group) && group.GroupsWith(old.Representative));
                if (known == null)
                {
                    known = new KnownGroup { Key = ++state.NextKey };
                    state.Groups.Add(known);
                }
                known.Representative = group; known.Stage = group.CurStageIndex;
                var members = new List<Thought>(nativeMembers);
                // Native tooltip order starts with the group's leading thought.
                members.Remove(leading); members.Insert(0, leading);
                rows.Add(new Row { Representative = group, Leading = leading, Members = members,
                    Offset = mood.thoughts.MoodOffsetOfGroup(group), Key = known.Key,
                    Label = leading.LabelCap + (members.Count > 1 ? " x" + members.Count : "") });
                keys.Add(known.Key);
            }
            state.Novelty.Observe(keys);
        }

        private static void DrawThoughts(Rect rect, Pawn pawn, PawnState state)
        {
            Need_Mood mood = pawn.needs?.mood;
            PawnInspectorView.Meter(new Rect(rect.x, rect.y, rect.width, 23f),
                mood?.LabelCap ?? "Mood".Translate(), mood?.CurLevelPercentage, Theme.Rgb(0x2B9B68), mood?.GetTipString());
            PawnInspectorWidgets.Divider(new Rect(rect.x, rect.y + 28f, rect.width, 1f));
            Rect viewport = new Rect(rect.x, rect.y + 36f, rect.width, Math.Max(0, rect.height - 36f));
            ReadGroups(pawn, state);
            bool polarity = false;
            for (int i = 1; i < rows.Count; i++) polarity |= PawnNeedsGeometry.PolarityDivider(rows[i - 1].Offset, rows[i].Offset);
            bool humanMeat = ModsConfig.IdeologyActive && pawn.Ideo != null && IdeoUtility.IdeoCausesHumanMeatCravings(pawn.Ideo);
            float height = PawnNeedsGeometry.ListHeight(Math.Max(1, rows.Count), polarity, PawnNeedsGeometry.ThoughtRow) + (humanMeat ? 29f : 0f);
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref thoughtScroll, height);
            try
            {
                float y = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    Row row = rows[i];
                    if (i > 0 && PawnNeedsGeometry.PolarityDivider(rows[i - 1].Offset, row.Offset))
                    { PawnInspectorWidgets.Divider(new Rect(0, y + 4f, width, 1f)); y += PawnNeedsGeometry.SectionGap; }
                    Rect line = new Rect(0, y, width, PawnNeedsGeometry.ThoughtRow);
                    bool visible = PawnNeedsGeometry.FullyVisible(line.y, line.yMax, thoughtScroll.y, viewport.height);
                    if (line.yMax > thoughtScroll.y && line.y < thoughtScroll.y + viewport.height)
                    {
                        if (Mouse.IsOver(line)) Theme.RoundRect(line, Theme.Hover.ToTransparent(.4f), 2f);
                        Rect label = new Rect(line.x, line.y, Math.Max(1, line.width - 76f), line.height);
                        PawnInspectorView.Label(label, row.Label, Theme.Ink);
                        Color color = row.Offset > 0 ? Positive : row.Offset < 0 ? Negative : Theme.Muted;
                        PawnInspectorView.Label(new Rect(line.xMax - 31f, line.y, 31f, line.height),
                            row.Offset.ToString("0.##", CultureInfo.CurrentCulture), color, right: true);
                        if (Mouse.IsOver(line)) TooltipHandler.TipRegion(line,
                            new TipSignal(ThoughtTip(row, pawn), unchecked(pawn.thingIDNumber * 397 ^ row.Key ^ 162603)));
                        if (state.Novelty.IsNew(row.Key))
                        {
                            Rect dot = new Rect(line.xMax - 66f, line.center.y - 2.5f, 5f, 5f);
                            Theme.RoundRect(dot, Theme.Muted.ToTransparent(.72f), 2.5f);
                            TooltipHandler.TipRegion(dot.ExpandedBy(3f), "Newly observed thought group this session. Clears after about 10 in-game minutes shown.");
                        }
                        if (ModsConfig.IdeologyActive && row.Leading.sourcePrecept?.ideo != null && !Find.IdeoManager.classicMode)
                            DrawIdeology(new Rect(line.xMax - 49f, line.y + 3f, 15f, 15f), row.Leading, visible);
                        if (visible && Event.current.type == EventType.Repaint)
                            state.Novelty.Shown(row.Key, Find.TickManager.TicksGame, Time.frameCount);
                    }
                    y += PawnNeedsGeometry.ThoughtRow;
                }
                if (rows.Count == 0)
                { PawnInspectorView.SmallLabel(new Rect(0, 0, width, 24f), "No mood thoughts", Theme.Muted); y += PawnNeedsGeometry.ThoughtRow; }
                if (humanMeat) DrawHumanMeat(new Rect(0, y + 7f, width, 22f), pawn);
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref thoughtScroll, height, 162601); }
        }

        private static void DrawIdeology(Rect rect, Thought thought, bool interactive)
        {
            using (new Theme.GuiScope(true))
            { GUI.color = Ideology; GUI.DrawTexture(rect, InspectorIcons.Get("ideology"), ScaleMode.ScaleToFit); }
            TooltipHandler.TipRegion(rect, thought.CausedByBeliefInPrecept);
            if (interactive && Widgets.ButtonInvisible(rect)) IdeoUIUtility.OpenIdeoInfo(thought.sourcePrecept.ideo);
        }

        private static void DrawNeeds(Rect rect, Pawn pawn)
        {
            needs.Clear();
            if (pawn.needs != null) needs.AddRange(pawn.needs.AllNeeds.Where(n => n != pawn.needs.mood && n.ShowOnNeedList));
            PawnNeedsUIUtility.SortInDisplayOrder(needs);
            // Food/Recreation are the demonstrated primary pair. Other native
            // visible needs (including Rest and Comfort) retain their native order.
            Need food = pawn.needs?.food, joy = pawn.needs?.joy;
            if (joy != null && needs.Remove(joy)) needs.Insert(0, joy);
            if (food != null && needs.Remove(food)) needs.Insert(0, food);
            int primary = needs.Count(n => n == food || n == joy);
            bool divider = primary > 0 && primary < needs.Count;
            float height = PawnNeedsGeometry.ListHeight(needs.Count, divider, PawnNeedsGeometry.NeedRow);
            float width = PawnInspectorWidgets.BeginScroll(rect, ref needScroll, height);
            try
            {
                float y = 0;
                for (int i = 0; i < needs.Count; i++)
                {
                    if (divider && i == primary)
                    { PawnInspectorWidgets.Divider(new Rect(0, y + 4f, width, 1f)); y += PawnNeedsGeometry.SectionGap; }
                    Need need = needs[i];
                    Color color = need == food ? Theme.Rgb(0x83AF7F) : need == joy ? Theme.Rgb(0x687BB7) : Theme.Rgb(0x536F7B);
                    PawnInspectorView.Meter(new Rect(0, y, width, 23f), need.LabelCap, need.CurLevelPercentage, color, need.GetTipString());
                    y += PawnNeedsGeometry.NeedRow;
                }
                if (needs.Count == 0) PawnInspectorView.SmallLabel(new Rect(0, 0, width, 24f), "No needs", Theme.Muted);
            }
            finally { PawnInspectorWidgets.EndScroll(rect, ref needScroll, height, 162602); }
        }

        private static void DrawHumanMeat(Rect rect, Pawn pawn)
        {
            int ticks = Find.TickManager.TicksGame - pawn.mindState.lastHumanMeatIngestedTick;
            string time = ticks.ToStringTicksToPeriod();
            PawnInspectorView.SmallLabel(rect, "LastHumanMeat".Translate() + ": " + "TimeAgo".Translate(time), Theme.Muted);
            TooltipHandler.TipRegion(rect, "LastHumanMeatDesc".Translate(pawn.Named("PAWN"), pawn.Ideo.Named("IDEO"), 8.Named("DURATION")));
        }

        private static string ThoughtTip(Row row, Pawn pawn)
        {
            Thought leading = row.Leading;
            var text = new StringBuilder(leading.LabelCap.AsTipTitle()).AppendLine().AppendLine();
            if (pawn.DevelopmentalStage.Baby())
                text.AppendLine(leading.BabyTalk).AppendLine().Append("Translation".Translate()).Append(": ").Append(leading.Description);
            else text.Append(leading.Description);
            int duration = row.Representative.DurationTicks;
            if (duration > 5 && leading is Thought_Memory)
            {
                var memories = row.Members.OfType<Thought_Memory>().ToList();
                text.AppendLine().AppendLine();
                if (memories.Count == 1)
                    text.Append("ThoughtExpiresIn".Translate((duration - memories[0].age).ToStringTicksToPeriod()));
                else if (memories.Count > 1)
                {
                    text.Append("ThoughtStartsExpiringIn".Translate((duration - memories.Max(t => t.age)).ToStringTicksToPeriod()));
                    text.AppendLine().Append("ThoughtFinishesExpiringIn".Translate((duration - memories.Min(t => t.age)).ToStringTicksToPeriod()));
                }
            }
            var labels = new HashSet<string> { leading.LabelCap };
            foreach (Thought member in row.Members)
                if (labels.Add(member.LabelCap)) text.AppendLine().Append("+ ").Append(member.LabelCap);
            // Thought.Description already appends native precept causes and
            // nullification messages. Do not duplicate those details here.
            return text.ToString();
        }
    }
}

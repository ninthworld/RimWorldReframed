using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ReframedUI
{
    // Original project coordinates and native callbacks; only links/cards/navigation are drawn here.
    internal static class KnowledgeMenusGraph
    {
        internal sealed class State
        {
            internal Vector2 Pan;
            internal float Zoom = 1;
            internal bool Focus = true;
            internal int PanControl, PanButton = -1;
            internal ResearchTabDef Tab;
            internal ResearchProjectDef Selected;
        }
        internal static void Release(State state)
        {
            if (state.PanControl != 0 && KnowledgeMenusPolicy.OwnsGraphPan(state.PanControl, GUIUtility.hotControl)) GUIUtility.hotControl = 0;
            state.PanControl = 0; state.PanButton = -1;
        }
        private sealed class Node
        {
            internal ResearchProjectDef Project;
            internal Rect Rect;
        }
        private static Rect Screen(Rect rect, State state, Vector2 origin)
            => new Rect((rect.x - origin.x) * state.Zoom - state.Pan.x, (rect.y - origin.y) * state.Zoom - state.Pan.y, rect.width * state.Zoom, rect.height * state.Zoom);
        private static void Zoom(State state, float value, Vector2 anchor)
        {
            float next = KnowledgeMenusPolicy.GraphZoom(value);
            state.Pan = new Vector2(KnowledgeMenusPolicy.ZoomAnchor(state.Pan.x, anchor.x, state.Zoom, next), KnowledgeMenusPolicy.ZoomAnchor(state.Pan.y, anchor.y, state.Zoom, next));
            state.Zoom = next;
        }
        internal static void Draw(MainTabWindow_Research owner, Rect rect, State state, List<ResearchProjectDef> all, HashSet<ResearchProjectDef> matches, bool searching)
        {
            int panId = GUIUtility.GetControlID(300131, FocusType.Passive);
            if (state.PanControl != 0 && (!GUI.enabled || state.PanControl != panId || !KnowledgeMenusPolicy.OwnsGraphPan(state.PanControl, GUIUtility.hotControl))) Release(state);
            Rect controls = new Rect(rect.x + 7, rect.y + 6, rect.width - 14, 24);
            if (PawnInspectorWidgets.Button(new Rect(controls.x, controls.y, 25, 23), "−")) Zoom(state, state.Zoom - .1f, new Vector2(rect.width / 2, rect.height / 2));
            if (PawnInspectorWidgets.Button(new Rect(controls.x + 29, controls.y, 25, 23), "+")) Zoom(state, state.Zoom + .1f, new Vector2(rect.width / 2, rect.height / 2));
            PawnInspectorView.SmallLabel(new Rect(controls.x + 60, controls.y, 42, 23), state.Zoom.ToStringPercent(), Theme.Muted);
            if (PawnInspectorWidgets.Button(new Rect(controls.x + 105, controls.y, 49, 23), "Focus")) state.Focus = true;
            TooltipHandler.TipRegion(controls, "Scroll to pan · Shift scroll sideways · Ctrl scroll to zoom · Middle/right drag to pan");
            Rect viewport = new Rect(rect.x + 6, rect.y + 36, rect.width - 18, Math.Max(1, rect.height - 57));
            ResearchProjectDef selected = KnowledgeMenusNative.Project(owner);
            if (state.Tab != owner.CurTab) { Release(state); state.Tab = owner.CurTab; state.Focus = true; state.Pan = Vector2.zero; }
            if (state.Selected != selected) { state.Selected = selected; state.Focus = true; }
            if (!Find.ResearchManager.TabInfoVisible(owner.CurTab))
            {
                Release(state);
                PawnInspectorView.Label(viewport.ContractedBy(8), "ResearchNotDiscovered".Translate(), Theme.Muted);
                return;
            }
            var nodes = new List<Node>();
            foreach (ResearchProjectDef project in all)
            {
                if (project.tab != owner.CurTab) continue;
                Vector2 position = KnowledgeMenusNative.ResearchPosition(owner, project);
                float x = position.x, y = position.y;
                if (!KnowledgeMenusPolicy.GraphFinite(x) || !KnowledgeMenusPolicy.GraphFinite(y)) continue;
                nodes.Add(new Node { Project = project, Rect = new Rect(x, y, KnowledgeMenusPolicy.GraphNodeWidth, KnowledgeMenusPolicy.GraphNodeHeight) });
            }
            if (nodes.Count == 0) { Release(state); PawnInspectorView.Label(viewport, "No research projects", Theme.Muted); return; }
            Vector2 origin = new Vector2(nodes.Min(n => n.Rect.x) - 20, nodes.Min(n => n.Rect.y) - 20);
            Vector2 extent = new Vector2(nodes.Max(n => n.Rect.xMax) - origin.x + 20, nodes.Max(n => n.Rect.yMax) - origin.y + 20);
            if (state.Focus)
            {
                Node target = nodes.FirstOrDefault(n => n.Project == selected) ?? nodes.FirstOrDefault(n => searching && matches.Contains(n.Project)) ?? nodes.First();
                state.Pan = new Vector2((target.Rect.center.x - origin.x) * state.Zoom - viewport.width / 2, (target.Rect.center.y - origin.y) * state.Zoom - viewport.height / 2);
                state.Focus = false;
            }
            Event ev = Event.current;
            if (GUI.enabled && Mouse.IsOver(viewport))
            {
                if (ev.type == EventType.ScrollWheel)
                {
                    if (ev.control) Zoom(state, state.Zoom - ev.delta.y * .05f, ev.mousePosition - viewport.position);
                    else if (ev.shift) state.Pan.x += ev.delta.y * 30;
                    else { state.Pan.x += ev.delta.x * 30; state.Pan.y += ev.delta.y * 30; }
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && (ev.button == 1 || ev.button == 2) && GUIUtility.hotControl == 0)
                { GUIUtility.hotControl = state.PanControl = panId; state.PanButton = ev.button; ev.Use(); }
            }
            if (KnowledgeMenusPolicy.GraphPanEvent(state.PanControl, GUIUtility.hotControl, state.PanButton, ev.button) && ev.type == EventType.MouseDrag)
            { state.Pan -= ev.delta; ev.Use(); }
            if (KnowledgeMenusPolicy.OwnsGraphPan(state.PanControl, GUIUtility.hotControl) &&
                (ev.type == EventType.Ignore || ev.rawType == EventType.MouseUp && ev.button == state.PanButton))
            { Release(state); if (ev.type == EventType.MouseUp) ev.Use(); }
            state.Pan.x = KnowledgeMenusPolicy.GraphPan(state.Pan.x, extent.x * state.Zoom, viewport.width);
            state.Pan.y = KnowledgeMenusPolicy.GraphPan(state.Pan.y, extent.y * state.Zoom, viewport.height);
            var byProject = nodes.ToDictionary(n => n.Project);
            bool pointer = Mouse.IsOver(viewport);
            GUI.BeginGroup(viewport);
            try
            {
                foreach (Node node in nodes)
                {
                    foreach (ResearchProjectDef prerequisite in node.Project.prerequisites ?? new List<ResearchProjectDef>())
                    {
                        if (prerequisite == null || !byProject.TryGetValue(prerequisite, out Node parent)) continue;
                        Rect childRect = Screen(node.Rect, state, origin), parentRect = Screen(parent.Rect, state, origin);
                        bool related = node.Project == selected || prerequisite == selected;
                        Color ink = related ? Theme.Accent.ToTransparent(.7f) : Theme.Muted.ToTransparent(.25f);
                        Vector2 from = new Vector2(parentRect.xMax, parentRect.center.y), to = new Vector2(childRect.x, childRect.center.y);
                        float mid = (from.x + to.x) / 2;
                        Widgets.DrawLine(from, new Vector2(mid, from.y), ink, related ? 1.4f : .8f);
                        Widgets.DrawLine(new Vector2(mid, from.y), new Vector2(mid, to.y), ink, related ? 1.4f : .8f);
                        Widgets.DrawLine(new Vector2(mid, to.y), to, ink, related ? 1.4f : .8f);
                    }
                }
                foreach (Node node in nodes)
                {
                    Rect card = Screen(node.Rect, state, origin);
                    if (!card.Overlaps(viewport.AtZero())) continue;
                    ResearchProjectDef project = node.Project;
                    bool hidden = project.IsHidden, active = !hidden && Find.ResearchManager.IsCurrentProject(project), finished = !hidden && project.IsFinished;
                    bool available = !hidden && !finished && !active && project.CanStartNow;
                    bool match = searching && matches.Contains(project), chosen = project == selected;
                    Color accent = active ? Theme.Rgb(0x58B7D4) : finished ? Theme.Good : available ? Theme.Ink : Theme.Muted;
                    using (new Theme.GuiScope(true))
                    {
                        GUI.color = searching && !match && !chosen ? Color.white.ToTransparent(.45f) : Color.white;
                        Theme.RoundRect(card, chosen ? Theme.Accent.ToTransparent(.85f) : match ? Theme.Ink.ToTransparent(.65f) : Theme.Border.ToTransparent(.6f), 5);
                        Theme.RoundRect(card.ContractedBy(.8f), Theme.Rgb(0x16212B).ToTransparent(.82f), 4);
                        Theme.RoundRect(new Rect(card.x + 1, card.y + 7, 2, card.height - 14), accent.ToTransparent(.8f), 1);
                        float iconWidth = !hidden && ModsConfig.AnomalyActive && project.knowledgeCategory != null ? 18 : 0;
                        if (iconWidth > 0)
                        {
                            GUI.color *= project.knowledgeCategory.color;
                            GUI.DrawTexture(new Rect(card.x + 7, card.y + 8, 13, 13), project.knowledgeCategory.Tex, ScaleMode.ScaleToFit);
                            GUI.color = searching && !match && !chosen ? Color.white.ToTransparent(.45f) : Color.white;
                        }
                        Rect label = new Rect(card.x + 8 + iconWidth, card.y + 3, card.width - 16 - iconWidth, 22);
                        PawnInspectorView.Label(label, hidden ? "UnknownResearch".Translate().ToString() : project.LabelCap.ToString(), hidden ? Theme.Muted : Theme.Ink, chosen);
                        string status = hidden ? "UnknownResearch".Translate().ToString() : active ? "InProgress".Translate().ToString() : finished ? "Finished".Translate().ToString() : available ? "Available" : "Locked".Translate().ToString();
                        PawnInspectorView.SmallLabel(new Rect(card.x + 8, card.y + 25, card.width - 16, 17), hidden ? status : status + " · " + project.techLevel.ToStringHuman(), accent);
                        if (!hidden) PawnInspectorView.Bar(new Rect(card.x + 8, card.yMax - 7, card.width - 16, 3), project.ProgressPercent, accent);
                    }
                    if (pointer && GUI.enabled)
                    {
                        TooltipHandler.TipRegion(card, hidden ? "UnknownResearch".Translate().ToString() : project.GetTip());
                        if (KnowledgeMenusPolicy.GraphNodeInput(GUI.enabled, hidden, all.Contains(project), pointer, ev.type == EventType.MouseDown, ev.button) && card.Contains(ev.mousePosition))
                        { owner.Select(project); SoundDefOf.Click.PlayOneShotOnCamera(); ev.Use(); }
                    }
                }
            }
            finally { GUI.EndGroup(); }
            if (extent.y * state.Zoom > viewport.height) PawnInspectorWidgets.Rail(new Rect(rect.xMax - 5, viewport.y, 4, viewport.height), ref state.Pan.y, extent.y * state.Zoom, viewport.height, false);
            if (extent.x * state.Zoom > viewport.width) PawnInspectorWidgets.Rail(new Rect(viewport.x, rect.yMax - 5, viewport.width, 4), ref state.Pan.x, extent.x * state.Zoom, viewport.width, true);
            PawnInspectorView.SmallLabel(new Rect(viewport.x, rect.yMax - 20, viewport.width, 14), searching ? matches.Count + " matches · context retained" : "Scroll / drag to explore", Theme.Muted);
        }
    }
}

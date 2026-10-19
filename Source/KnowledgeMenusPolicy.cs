using System;
using System.Collections.Generic;

namespace ReframedUI
{
    internal static class KnowledgeMenusPolicy
    {
        internal static List<T> Hierarchy<T>(IReadOnlyList<T> sorted, Func<T, T> parent, Func<T, IEnumerable<T>> children) where T : class
        {
            var candidates = new HashSet<T>(sorted); var visited = new HashSet<T>(); var result = new List<T>();
            Action<T> visit = null;
            visit = item =>
            {
                T ancestor = parent(item);
                if (visited.Contains(item) || ancestor != null && candidates.Contains(ancestor) && !visited.Contains(ancestor)) return;
                visited.Add(item); result.Add(item);
                foreach (T child in children(item)) if (candidates.Contains(child)) visit(child);
            };
            foreach (T item in sorted) visit(item);
            return result;
        }
        // Native UpdateSearchResults also changes selection/tab. Never invoke it as a passive timer.
        internal static bool RefreshSearch(string applied, string query) => applied != query;
        internal const float GraphNodeWidth = 150, GraphNodeHeight = 56;
        internal static bool OwnsGraphPan(int control, int hot) => control != 0 && control == hot;
        internal static bool GraphPanEvent(int control, int hot, int button, int eventButton) => OwnsGraphPan(control, hot) && button == eventButton && (button == 1 || button == 2);
        internal static bool GraphNodeInput(bool enabled, bool hidden, bool member, bool pointerInViewport, bool down, int button)
            => enabled && !hidden && member && pointerInViewport && down && button == 0;
        internal static bool GraphFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static float GraphZoom(float zoom) => Math.Max(.9f, Math.Min(1.5f, zoom));
        internal static float GraphPan(float pan, float total, float visible) => Math.Max(0, Math.Min(Math.Max(0, total - visible), pan));
        internal static float ZoomAnchor(float pan, float pointer, float oldZoom, float newZoom) => (pan + pointer) * newZoom / oldZoom - pointer;
        internal static bool Status(int filter, bool hidden, bool finished, bool current, bool startable)
            => filter == 0 || !hidden && (filter == 1 ? startable && !finished && !current : filter == 2 ? current : filter == 3 ? finished : !finished && !current && !startable);
        internal static float LeftWidth(float width) => Math.Max(180, Math.Min(280, width * .31f));
        internal static bool FullyVisible(float y, float height, float scroll, float viewport)
            => height > 0 && y >= scroll - .01f && y + height <= scroll + viewport + .01f;
        internal static bool CanStart(bool visible, bool hidden, bool current, bool startable)
            => visible && !hidden && !current && startable;
    }
}

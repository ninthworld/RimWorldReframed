using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Session-only observed novelty. The first snapshot is a baseline, never "new".
    // A dot expires after 417 unpaused game ticks (~10 in-game minutes) during
    // consecutive rendered frames in which its complete row is visible.
    internal sealed class PawnNeedsNovelty
    {
        internal const int ShownTicks = 417;
        private sealed class State
        {
            internal bool Present, New;
            internal int SeenTicks, LastTick, LastFrame = -1;
        }
        private readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private readonly HashSet<int> snapshot = new HashSet<int>();
        private bool initialized;

        internal void Observe(IEnumerable<int> keys)
        {
            snapshot.Clear();
            foreach (int key in keys) snapshot.Add(key);
            foreach (var pair in states)
                if (!snapshot.Contains(pair.Key)) { pair.Value.Present = false; pair.Value.LastFrame = -1; }
            foreach (int key in snapshot)
            {
                if (!states.TryGetValue(key, out State state))
                {
                    state = new State { Present = true, New = initialized };
                    states.Add(key, state);
                }
                else if (!state.Present)
                {
                    state.Present = true; state.New = initialized; state.SeenTicks = 0; state.LastFrame = -1;
                }
            }
            initialized = true;
        }

        internal bool IsNew(int key) => states.TryGetValue(key, out State state) && state.Present && state.New;

        internal void Shown(int key, int tick, int frame)
        {
            if (!states.TryGetValue(key, out State state) || !state.Present || !state.New) return;
            if (state.LastFrame >= 0 && (frame == state.LastFrame || frame == state.LastFrame + 1) && tick >= state.LastTick)
                state.SeenTicks = Math.Min(ShownTicks, state.SeenTicks + Math.Min(ShownTicks, tick - state.LastTick));
            state.LastTick = tick; state.LastFrame = frame;
            if (state.SeenTicks >= ShownTicks) state.New = false;
        }

        internal void StopShowing()
        {
            foreach (State state in states.Values) state.LastFrame = -1;
        }
    }

    internal static class PawnNeedsGeometry
    {
        internal const float ThoughtRow = 20f, NeedRow = 25f, SectionGap = 10f;
        internal static bool FullyVisible(float top, float bottom, float scroll, float viewport)
            => top >= scroll && bottom <= scroll + viewport;
        internal static bool PolarityDivider(float previous, float current) => previous >= 0 && current < 0;
        internal static float ListHeight(int count, bool divider, float row)
            => Math.Max(0, count) * row + (divider ? SectionGap : 0f);
    }
}

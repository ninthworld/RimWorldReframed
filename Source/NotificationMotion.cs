using System;
using System.Collections.Generic;

namespace ReframedUI
{
    // Identity is supplied by the caller, never by a title. Native removal is
    // immediate; an outgoing node is an inert visual retained for 220ms.
    internal sealed class NotificationMotion<T> where T : class
    {
        internal sealed class Node
        {
            internal readonly T Key;
            internal bool Live;
            internal double Progress, Bottom, TargetBottom, StartBottom, Reflow = 1;
            internal Node(T key) { Key = key; }
            internal bool Settled => Live && Progress == 1 && Reflow == 1;
        }
        private readonly List<Node> nodes = new List<Node>();
        private double previous = double.NaN;
        internal IReadOnlyList<Node> Nodes => nodes;
        internal void Reset() { nodes.Clear(); previous = double.NaN; }
        internal double Prepare(IReadOnlyList<T> oldestFirst, double now)
        {
            double dt = NotificationPolicy.Step(now, previous);
            if (!double.IsNaN(now) && !double.IsInfinity(now)) previous = double.IsNaN(previous) ? now : Math.Max(previous, now);
            foreach (Node node in nodes) node.Live = false;
            for (int i = oldestFirst.Count - 1; i >= 0; i--)
            {
                T key = oldestFirst[i]; Node node = nodes.Find(n => ReferenceEquals(n.Key, key));
                double target = (oldestFirst.Count - 1 - i) * (NotificationPolicy.Row + NotificationPolicy.Gap);
                if (node == null)
                { node = new Node(key) { Bottom = 0, StartBottom = 0, TargetBottom = target, Reflow = target == 0 ? 1 : 0 }; nodes.Add(node); }
                node.Live = true;
                if (node.TargetBottom != target)
                { node.StartBottom = node.Bottom; node.TargetBottom = target; node.Reflow = 0; }
            }
            double occupied = 0;
            foreach (Node node in nodes)
            {
                node.Progress = NotificationPolicy.Move(node.Progress, node.Live ? 1 : 0, dt / .22);
                node.Reflow = Math.Min(1, node.Reflow + dt / .18);
                node.Bottom = node.StartBottom + (node.TargetBottom - node.StartBottom) * NotificationPolicy.Ease(node.Reflow);
                if (node.Progress > 0) occupied = Math.Max(occupied, node.Bottom + NotificationPolicy.Row * NotificationPolicy.Ease(node.Progress));
            }
            nodes.RemoveAll(n => !n.Live && n.Progress == 0);
            return occupied;
        }
        internal bool Settled
        {
            get { foreach (Node node in nodes) if (!node.Settled) return false; return true; }
        }
    }
}

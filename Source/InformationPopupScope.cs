using System;

namespace ReframedUI
{
    // Additional windows opened by an information card keep its menu styling
    // without making their contents part of the card's native paint adapter.
    internal static class InformationPopupScope
    {
        private static int depth;
        internal static bool Active => depth > 0;
        internal static IDisposable Enter() => new Scope();
        private sealed class Scope : IDisposable
        {
            private bool disposed;
            internal Scope() { depth++; }
            public void Dispose() { if (!disposed) { disposed = true; depth--; } }
        }
    }
}

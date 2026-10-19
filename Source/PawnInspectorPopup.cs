using System;

namespace ReframedUI
{
    // A bounded origin scope, not a global FloatMenu skin. Menus opened by
    // inspector controls inherit this ownership even when BuildMenu is off.
    internal static class PawnInspectorPopup
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

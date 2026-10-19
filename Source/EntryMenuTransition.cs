#if !ENTRY_MOTION_CONTRACTS
using UnityEngine;
using Verse;
#endif

namespace ReframedUI
{
    internal static class EntryMotionCurve
    {
        internal const float Duration = .25f;
        internal static float Value(float from, float target, float elapsed)
        {
            float t = System.Math.Max(0f, System.Math.Min(1f, elapsed / Duration));
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            return from + (target - from) * ease;
        }
        internal static bool Moving(float from, float target, float elapsed) => from != target && elapsed < Duration;
        internal static float Reverse(float from, float elapsed)
        {
            float t = System.Math.Max(0f, System.Math.Min(1f, elapsed / Duration));
            return from * (1f - t * t * t);
        }
    }

#if !ENTRY_MOTION_CONTRACTS
    // Entry presentation only. Native Window/windowRect always retain their
    // final bounds; this root layer paints the shell between those bounds.
    internal static class EntryMenuTransition
    {
        private static Window owner;
        private static string kind = "browser";
        private static Color destinationColor;
        private static float destinationRadius;
        private static float started, from, target;
        private static int sampledFrame = -1;
        private static float sampledTime;
        internal static bool Closing { get; private set; }

        // Root painting and the later GUI.Window callback must agree on the
        // endpoint frame, or both could paint a translucent shell in one frame.
        private static float Now
        {
            get
            {
                if (sampledFrame != Time.frameCount)
                {
                    sampledFrame = Time.frameCount;
                    sampledTime = Time.realtimeSinceStartup;
                }
                return sampledTime;
            }
        }

        private static bool Valid
        {
            get
            {
                if (owner == null) return false;
                if (Find.WindowStack != null && owner.IsOpen && EntryScene.Active) return true;
                owner = null; Closing = false;
                return false;
            }
        }

        internal static float Progress
        {
            get
            {
                if (!Valid) return 0;
                return Closing ? EntryMotionCurve.Reverse(from, Now - started)
                    : EntryMotionCurve.Value(from, target, Now - started);
            }
        }
        internal static bool Moving => Valid && EntryMotionCurve.Moving(from, target, Now - started);
        internal static bool BlocksInput => Valid && (Closing || Moving);
        internal static bool PaintsShell => Valid && (Closing || Moving);
        // Reveal content only once the expanding shell reaches its content inset.
        // At the enlarged browser size, .96 leaves the moving edge inside
        // the first content inset before any labels become visible.
        // On Back this also fades content away before the shell contracts across it.
        internal static float BrowserOpacity => Mathf.Clamp01((Progress - .96f) / .04f);
        internal static Rect ShellBounds => RectLerp(EntryScene.MenuBounds, owner.windowRect, Progress);

        internal static bool Owns(Window browser) => Valid && owner == browser;
        internal static void Open(Window browser, string presentation = "browser", Color? shellColor = null, float? radius = null, bool animate = true)
        {
            kind = presentation;
            destinationColor = shellColor ?? ReframedSaveBrowser.TransitionShellColor;
            destinationRadius = radius ?? 16f * EntryScene.BrowserScale * (2f / 3f);
            owner = browser; Closing = false; from = animate ? 0 : 1; target = 1;
            started = Time.realtimeSinceStartup;
            sampledFrame = -1;
            ReframedDlcPanel.Close();
            if (animate) EntryMotionEvidence.Start(kind + "-open", MotionState);
        }
        internal static void Back(Window browser)
        {
            if (!Owns(browser) || Closing) return;
            from = Progress; target = 0; Closing = true;
            started = Now;
            EntryMotionEvidence.Start(kind + "-back", MotionState);
        }
        private static string MotionState() => "progress=" + Progress + ";closing=" + Closing + ";bounds=" + (owner == null ? default(Rect) : ShellBounds);
        internal static bool ReadyToClose(Window browser) => Owns(browser) && Closing && !Moving;
        internal static void Forget(Window browser)
        {
            if (owner != browser) return;
            owner = null; Closing = false;
        }

        internal static void DrawRoot()
        {
            if (!PaintsShell) return;
            float progress = Progress;
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                float radius = Mathf.Lerp(16f * EntryScene.MenuScale * (2f / 3f), destinationRadius, progress);
                if (kind == "new-game") NewGameSetup.DrawTransitionShell(ShellBounds, progress, radius);
                else Theme.RoundRect(ShellBounds, Color.Lerp(EntryScene.PanelColor, destinationColor, progress), radius);
                float outgoing = 1f - progress;
                EntryScene.DrawWordmark(outgoing);
                ReframedMainMenu.DrawTransitionContent(outgoing);
                ReframedDlcPanel.DrawTransitionStrip(outgoing, 16f * progress);
            }
        }

        private static Rect RectLerp(Rect a, Rect b, float t) => new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
    }
#endif
}

namespace ReframedUI
{
    // Same reversible real-time reveal as Architect and Tools; no gameplay clock.
    internal sealed class WorkPanelMotion
    {
        private readonly ToolsPanelMotion panel = new ToolsPanelMotion();
        private readonly ToolsPanelMotion editor = new ToolsPanelMotion();
        internal bool Open => panel.Open;
        internal bool EditorOpen => editor.Open;
        internal float Panel => ToolsPanelMotion.Ease(panel.Shell);
        internal float Editor => ToolsPanelMotion.Ease(editor.Shell);
        internal bool ReserveEditor => !editor.Open && Editor > 0;
        internal bool PanelInteractive => panel.Open && panel.Settled;
        internal bool EditorInteractive => PanelInteractive && editor.Open && editor.Settled;
        internal void SetOpen(bool value, double now)
        { panel.SetOpen(value, now); if (!value) editor.SetOpen(false, now); }
        internal void SetEditor(bool value, double now) => editor.SetOpen(value && panel.Open, now);
        internal void Tick(double now) { panel.Tick(now); editor.Tick(now); }
        internal void Reset() { panel.Reset(); editor.Reset(); }
    }
}

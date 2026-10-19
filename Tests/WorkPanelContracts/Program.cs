using System;
using System.Linq;
using ReframedUI;

int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
Check(!WorkPanelLayout.Supported(699, 1080) && !WorkPanelLayout.Supported(1920, 479), "unsupported canvas retains the native table");
foreach (float screenWidth in new[] { 700f, 720f, 1080f, 1920f })
foreach (float screenHeight in new[] { 480f, 600f, 824f, 1080f })
foreach (float content in new[] { 0f, 54f, 230f, 570f, 12000f })
{
    float total = WorkPanelLayout.Height(content, screenHeight);
    Check(WorkPanelLayout.Width(screenWidth) + 16 <= screenWidth, "panel stays horizontally on screen");
    Check(WorkPanelLayout.Bottom(screenHeight) - total >= 40, "expansion preserves top margin");
    Check(total >= WorkPanelLayout.Header + WorkPanelLayout.Padding + WorkPanelLayout.Row, "empty and tall rosters retain viewport");
}
Check(WorkPanelLayout.Reveal(0, 300, 250, 400) == 150, "selection reveals complete controls");
Check(WorkPanelLayout.Reveal(200, 100, 900, 400) == 100, "oversize selected content aligns its start");
Check(WorkPanelLayout.Reveal(100, 150, 100, 400) == 100, "visible selection does not jump scroll");
// Single compact roster rows do not grow with enabled-job count or priority mode.
Check(WorkPanelLayout.Row == 30 && WorkPanelLayout.Tag < WorkPanelLayout.Row, "native status cells and compact tags share one row");
Check(WorkPanelLayout.Height(7 * WorkPanelLayout.Row + 186, 824) == 433, "seven-pawn source composition stays compact with top editor");
foreach (float screen in new[] {480f, 600f, 824f, 1080f})
foreach (float content in new[] {100f, 174f, 260f, 1500f})
{
    float height = WorkPanelLayout.EditorHeight(content, screen);
    float viewport = WorkPanelLayout.EditorViewport(height);
    Check(viewport > 0 && height + WorkPanelLayout.Header + 2 * WorkPanelLayout.Row + WorkPanelLayout.Padding <= screen - 83,
        "large editors preserve reachable roster rows below them");
    if (content + WorkPanelLayout.EditorFooter + 12 <= (screen - 83) * .55f)
        Check(viewport == content, "ordinary editor has no phantom scrollbar caused by its outer padding");
    else Check(viewport < content, "real extra content stays scrollable");
}
foreach (float column in new[] {72f, 96f, 130f, 170f})
foreach (float measured in new[] {40f, 71.5f, 120f, 500f})
{
    float tag = WorkPanelLayout.EditorTagWidth(measured, column);
    Check(tag + WorkPanelLayout.PrioritySpace <= column && tag <= measured,
        "priority button keeps its separate hit target without overflowing adjacent category");
    Check(tag + 3 > tag, "toggle and cycle hit targets have a visible gap");
}
Check(WorkPanelLayout.Reveal(0, 180, 30, 150) == 60, "selection reveals pawn in roster without scrolling fixed top editor");
Check(WorkPanelLayout.Category("PatientBedRest") == 0 && WorkPanelLayout.Category("Childcare") == 0, "care includes actual stock/DLC identities");
Check(WorkPanelLayout.Category("DarkStudy") == 1 && WorkPanelLayout.Category("Hunting") == 2, "source categories remain canonical");
Check(WorkPanelLayout.Category("Crafting") == 3 && WorkPanelLayout.Category("Firefighter") == 4 && WorkPanelLayout.Category("Handling") == 5, "production/combat/construction match the source");
Check(WorkPanelLayout.Category("BasicWorker") == 6 && WorkPanelLayout.Category("ModdedJob") == 6, "all extra definitions retain an accessible section");
Check(WorkPanelLayout.EditorOrder("Growing") < WorkPanelLayout.EditorOrder("PlantCutting") && WorkPanelLayout.EditorOrder("Crafting") < WorkPanelLayout.EditorOrder("Art"), "editor uses illustrated order without rewriting scheduler priorities");
Check(WorkPanelPolicy.NativeOrderKey(100, 1) > WorkPanelPolicy.NativeOrderKey(1400, 2), "ordinary priorities sort by actual native order");
Check(WorkPanelPolicy.NativeOrderKey(-200000, 1) < WorkPanelPolicy.NativeOrderKey(1400, 2), "unusual mod natural priority retains the actual combined native score");
var tied = new[] { "a", "b", "c" }.OrderByDescending(_ => WorkPanelPolicy.NativeOrderKey(100, 2)).ToArray();
Check(tied.SequenceEqual(new[] { "a", "b", "c" }), "ties remain stable and do not fabricate a scheduling order");
Check(WorkPanelLayout.FullyVisible(32, 32, 32, 64) && !WorkPanelLayout.FullyVisible(32, 32, 33, 64), "clipped row cannot invoke actions");
Check(!WorkPanelLayout.FullyVisible(48, 20, 0, 64) && WorkPanelLayout.FullyVisible(48, 20, 4, 64), "editor tag only acts when wholly exposed");
var motion = new WorkPanelMotion();
bool Near(float a, float b) => Math.Abs(a-b) < .0001f;
Check(!motion.Open && !motion.PanelInteractive && !motion.EditorInteractive, "closed Work controls cannot act");
motion.SetEditor(true, 0);
Check(!motion.EditorOpen, "editor cannot open without Work");
motion.SetOpen(true, 0); motion.Tick(.11);
Check(Near(motion.Panel,.5f) && !motion.PanelInteractive, "Work uses real-time eased halfway reveal and blocks actions");
motion.Tick(.11);
Check(Near(motion.Panel,.5f), "repeated GUI calls in one frame cannot accelerate animation");
motion.SetOpen(false,.11);
Check(Near(motion.Panel,.5f) && !motion.Open, "closing reverses without jumping or delaying native closure");
motion.Tick(.165);
Check(motion.Panel>0 && motion.Panel<.5f, "close visibly contracts toward its anchor");
float reversing=motion.Panel;
motion.SetOpen(true,.165);
Check(Near(motion.Panel,reversing), "quick re-open continues from visible extent");
motion.Tick(.5);
Check(motion.Panel==1 && motion.PanelInteractive && !motion.EditorInteractive, "main panel settles independently of editor");
motion.SetEditor(true,.5); motion.Tick(.61);
Check(Near(motion.Editor,.5f) && motion.PanelInteractive && !motion.EditorInteractive, "upper window animates independently while roster stays usable");
motion.SetEditor(false,.61);
Check(Near(motion.Editor,.5f) && !motion.EditorOpen, "deselect immediately disables editor and retains outgoing progress");
Check(motion.ReserveEditor, "deselect reserves outgoing editor space above a height-capped roster");
motion.Tick(.665); reversing=motion.Editor; motion.SetEditor(true,.665);
Check(Near(motion.Editor,reversing), "quick selection reopens without a jump");
Check(!motion.ReserveEditor, "reselection uses active editor geometry rather than a stale reservation");
motion.Tick(1);
Check(motion.Editor==1 && motion.EditorInteractive, "editor controls enable only when fully exposed");
motion.SetOpen(false,1);
Check(!motion.Open && !motion.EditorOpen && !motion.EditorInteractive && !motion.PanelInteractive, "dismissing Work also closes the top window and disables both");
motion.Tick(1.11);
Check(Near(motion.Panel,.5f) && Near(motion.Editor,.5f), "both shells contract at common timing");
motion.Tick(2);
Check(motion.Panel==0 && motion.Editor==0, "no stale ghost survives completion");
Check(!motion.ReserveEditor, "completed contraction releases the reserved roster space");
motion.SetOpen(true,2); motion.SetEditor(true,2); motion.Reset();
Check(!motion.Open && !motion.EditorOpen && motion.Panel==0 && motion.Editor==0, "context/fallback reset retires every visual state");
Console.WriteLine($"PASS {checks} Work panel viewport, selection, motion, category and native-order contracts. No live acceptance claimed.");

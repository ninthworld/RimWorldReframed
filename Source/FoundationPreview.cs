using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Fixture-only data never enters a colony or the ordinary UI. Both the
    // settings entry and command-line entry require RimWorld development mode.
    internal sealed class FoundationPreview : Window
    {
        private int commands, captureNumber, captureDelay = -1;
        private bool stateProbePassed;
        private string message = "No checks run yet.";
        private Rect specimenPixels;
        private int page, candidate, iconVariant;
        private bool captureBusy, sweep, sweepStarted;
        private int sweepStep;
        private FoundationMetrics metrics;
        private string metricsPath;
        private readonly System.Collections.Generic.List<string> dispatches = new System.Collections.Generic.List<string>();
        internal static string OutputDirectory => Path.Combine(GenFilePaths.SaveDataFolderPath, "ReframedUI", "Foundation");
        public override Vector2 InitialSize => new Vector2(Mathf.Min(900, UI.screenWidth - 40), Mathf.Min(660, UI.screenHeight - 40));

        internal FoundationPreview()
        {
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            draggable = true;
        }

        internal static void Open()
        {
            var windows = Find.WindowStack;
            if (Prefs.DevMode && windows != null && !windows.IsOpen<FoundationPreview>()) windows.Add(new FoundationPreview());
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!Prefs.DevMode) { Close(); return; }
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(8, 0, inRect.width - 20, 40), "Reframed UI / foundation lab");
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.Label(new Rect(8, 44, inRect.width - 20, 62),
                    "Development fixture. Existing game fonts, reconstructed symbols and measured colors. Source fidelity is evaluated separately from runtime smoke checks.");
                if (inRect.width < 780 || inRect.height < 560)
                {
                    Widgets.Label(new Rect(8, 120, inRect.width - 20, 120), "The lab requires at least 820 x 600 UI pixels. Use a larger window or a lower UI scale for fixture captures. Normal gameplay is unaffected.");
                    return;
                }

                bool idle = !captureBusy && captureDelay < 0 && !sweep && (metrics == null || metrics.IsComplete);
                if (Theme.ActionButton(new Rect(20, 84, 138, 26), "Primitives", idle)) page = 0;
                if (Theme.ActionButton(new Rect(166, 84, 138, 26), "Game fonts", idle)) page = 1;
                if (Theme.ActionButton(new Rect(312, 84, 138, 26), "Icon atlas", idle)) page = 2;
                if (Theme.ActionButton(new Rect(476, 84, 90, 26), "Previous", idle && page != 0))
                {
                    if (page == 1) candidate = (candidate + TypographyCalibration.Count - 1) % TypographyCalibration.Count;
                    else iconVariant = (iconVariant + ReframedIcons.VariantCount - 1) % ReframedIcons.VariantCount;
                }
                if (Theme.ActionButton(new Rect(574, 84, 90, 26), "Next", idle && page != 0))
                {
                    if (page == 1) candidate = (candidate + 1) % TypographyCalibration.Count;
                    else iconVariant = (iconVariant + 1) % ReframedIcons.VariantCount;
                }
                if (page == 1)
                {
                    if (Theme.ActionButton(new Rect(676, 84, 150, 26), (candidate + 1) + " / " + TypographyCalibration.Count + " · Choose", idle))
                    {
                        var choices = new System.Collections.Generic.List<FloatMenuOption>();
                        for (int i = 0; i < TypographyCalibration.Count; i++)
                        {
                            int selectedCandidate = i;
                            choices.Add(new FloatMenuOption(TypographyCalibration.GetCandidateDescription(i), () => candidate = selectedCandidate));
                        }
                        Find.WindowStack.Add(new FloatMenu(choices));
                    }
                }
                else Theme.TextLine(new Rect(676, 84, 150, 26), page == 2 ? (iconVariant + 1) + " / " + ReframedIcons.VariantCount + " variants" : "Fixture v5", Theme.Muted);
                var specimen = new Rect(20, 116, 760, 286);
                bool sampled = metrics != null && Event.current.type == EventType.Repaint && metrics.BeginSample(Time.frameCount);
                bool drawSucceeded = false;
                try
                {
                    if (page == 1) TypographyCalibration.Draw(specimen, candidate);
                    else if (page == 2) ReframedIcons.DrawSpecimen(specimen, iconVariant);
                    else DrawSpecimen(specimen);
                    DrawFiducials(specimen);
                    drawSucceeded = true;
                }
                finally { if (sampled) metrics.EndSample(drawSucceeded); }
                if (metrics != null && metrics.IsComplete && metricsPath == null)
                {
                    Directory.CreateDirectory(OutputDirectory);
                    metricsPath = Path.Combine(OutputDirectory, "metrics-" + PageId + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".json");
                    try
                    {
                        metrics.WriteReport(metricsPath);
                        message = "Renderer scope sampling finished. Measurements are not a performance acceptance pass.";
                    }
                    catch (Exception ex)
                    {
                        message = "Metric export failed; inspect the log.";
                        Log.Warning("[Reframed UI] Could not export renderer measurements: " + ex.Message);
                    }
                }
                // GUIToScreenPoint follows the window/group transform; capture records
                // both this result and the physical/UI ratio for independent checking.
                Vector2 topLeft = GUIUtility.GUIToScreenPoint(new Vector2(specimen.x, specimen.y));
                Vector2 bottomRight = GUIUtility.GUIToScreenPoint(new Vector2(specimen.xMax, specimen.yMax));
                specimenPixels = Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);

                EventType inputEvent = Event.current.rawType;
                if (Theme.ActionButton(new Rect(20, 420, 156, 34), "Increment once", idle))
                {
                    commands++;
                    dispatches.Add("{\"sequence\":" + commands + ",\"command\":\"increment\",\"target\":\"foundation-counter\",\"frame\":" + Time.frameCount + ",\"event\":\"" + inputEvent + "\"}");
                }
                if (Theme.ActionButton(new Rect(188, 420, 156, 34), "Disabled action", false))
                {
                    // A regression must be observable; ignoring a disabled return
                    // value would make this fixture incapable of detecting it.
                    commands++;
                    dispatches.Add("{\"sequence\":" + commands + ",\"command\":\"disabled-action\",\"target\":\"foundation-counter\",\"frame\":" + Time.frameCount + ",\"event\":\"" + inputEvent + "\"}");
                    message = "FAIL: disabled control dispatched.";
                }
                if (Theme.ActionButton(new Rect(356, 420, 156, 34), "Probe GUI state", idle))
                {
                    stateProbePassed = ProbeState();
                    message = stateProbePassed ? "GUI state restored, including exception exit." : "GUI state probe FAILED.";
                }
                if (Theme.ActionButton(new Rect(524, 420, 156, 34), "Capture frame", idle))
                {
                    captureDelay = 3;
                    message = "Capture armed. Move the pointer outside the specimen.";
                }
                if (Theme.ActionButton(new Rect(20, 466, 220, 32), "Export runtime diagnostics", idle))
                {
                    string path = RuntimeDiagnostics.WriteReport(OutputDirectory);
                    message = path == null ? "Diagnostics failed; inspect the log." : "Diagnostics exported to the fixture folder.";
                }
                if (Theme.ActionButton(new Rect(252, 466, 156, 32), "Measure renderer", idle))
                {
                    metrics = new FoundationMetrics(Prefs.DevMode, 30, 180);
                    metricsPath = null;
                    message = "Sampling specimen Repaints only; 30 warmup + 180 measured frames.";
                }
                Widgets.Label(new Rect(420, 468, 420, 28), "Commands: " + commands + " | GUI: " + (stateProbePassed ? "PASS" : "not passed"));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(20, 514, inRect.width - 40, 26), message);
                Widgets.Label(new Rect(20, 540, inRect.width - 40, 40), "Output: " + OutputDirectory);
                if (Event.current.type == EventType.Repaint && !sweepStarted && Environment.GetCommandLineArgs().Contains("-reframed-calibration-capture"))
                {
                    sweepStarted = true; sweep = true; sweepStep = 0; page = 0; candidate = 0; captureDelay = 5;
                    message = "Development capture sweep running; controls resume after all candidates.";
                }
                if (captureDelay >= 0 && Event.current.type == EventType.Repaint && --captureDelay < 0) Capture();
            }
        }

        private string PageId => page == 1 ? "typography" : page == 2 ? "icons" : "primitives";

        private static void DrawFiducials(Rect r)
        {
            // These are fixture calibration marks, outside the content under comparison.
            // They deliberately change four corner patches relative to fixture v1.
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, 4, 4), Color.magenta);
            Widgets.DrawBoxSolid(new Rect(r.xMax - 4, r.y, 4, 4), Color.cyan);
            Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 4, 4, 4), new Color(1f, 1f, 0f, 1f));
            Widgets.DrawBoxSolid(new Rect(r.xMax - 4, r.yMax - 4, 4, 4), Color.white);
        }

        private static void DrawSpecimen(Rect rect)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(rect, Theme.Rgb(DesignTokens.Panel));
                uint[] palette = { DesignTokens.Deep, DesignTokens.Panel, DesignTokens.Content, DesignTokens.Button,
                    DesignTokens.Selected, DesignTokens.Divider, DesignTokens.Muted, DesignTokens.Secondary,
                    DesignTokens.Ink, DesignTokens.Best, DesignTokens.Destructive, DesignTokens.Positive };
                for (int i = 0; i < palette.Length; i++)
                {
                    float x = rect.x + 16 + i * 60;
                    Widgets.DrawBoxSolid(new Rect(x, rect.y + 16, 48, 32), Theme.Rgb(palette[i]));
                    Theme.TextLine(new Rect(x, rect.y + 50, 58, 18), palette[i].ToString("X6"), Theme.Muted);
                }
                string[] labels = { "Normal", "Hover (inferred)", "Pressed (inferred)", "Selected", "Disabled" };
                Color[] faces = { Theme.Surface, Theme.Hover, Theme.Rgb(DesignTokens.Pressed), Theme.Selected, Theme.Surface };
                for (int i = 0; i < labels.Length; i++)
                {
                    var r = new Rect(rect.x + 16 + 145*i, rect.y + 82, 133, 36);
                    Theme.ButtonFace(r, faces[i], i == 4);
                    Theme.Label(r, labels[i], i == 4 ? Theme.Muted : Theme.Ink);
                }
                Theme.TextLine(new Rect(rect.x + 16, rect.y + 138, 700, 26), "New colony   Load   Tutorial   Futa Healer   14  81%   0.5 kg", Theme.Ink);
                Theme.TextLine(new Rect(rect.x + 16, rect.y + 166, 700, 26), "Font: RimWorld Tiny fallback. Identity, glyph spacing and raster match remain open.", Theme.Muted);
                Theme.RoundRect(new Rect(rect.x + 16, rect.y + 208, 230, 56), Theme.Rgb(DesignTokens.Content), 4);
                Theme.RoundRect(new Rect(rect.x + 262, rect.y + 208, 230, 56), Theme.Rgb(DesignTokens.Content), 8);
                Theme.RoundRect(new Rect(rect.x + 508, rect.y + 208, 230, 56), Theme.Rgb(DesignTokens.Content), 12);
                Theme.Label(new Rect(rect.x + 16, rect.y + 208, 230, 56), "Provisional radius 4", Theme.Ink);
                Theme.Label(new Rect(rect.x + 262, rect.y + 208, 230, 56), "Provisional radius 8", Theme.Ink);
                Theme.Label(new Rect(rect.x + 508, rect.y + 208, 230, 56), "Provisional radius 12", Theme.Ink);
            }
        }

        private static bool ProbeState()
        {
            Color color = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor;
            bool enabled = GUI.enabled, wrap = Text.WordWrap;
            Matrix4x4 matrix = GUI.matrix;
            int depth = GUI.depth;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            try
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.color = Color.red; GUI.backgroundColor = Color.blue; GUI.contentColor = Color.green;
                    GUI.enabled = !enabled; GUI.matrix = Matrix4x4.Scale(new Vector3(2, 2, 1)); GUI.depth = depth + 1;
                    Text.Font = GameFont.Medium; Text.Anchor = TextAnchor.LowerRight; Text.WordWrap = !wrap;
                    throw new InvalidOperationException("Intentional local fixture exception");
                }
            }
            catch (InvalidOperationException) { }
            return GUI.color == color && GUI.backgroundColor == background && GUI.contentColor == content &&
                GUI.enabled == enabled && GUI.matrix == matrix && GUI.depth == depth && Text.Font == font &&
                Text.Anchor == anchor && Text.WordWrap == wrap;
        }

        private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        private static string Number(float number) => number.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private void Capture()
        {
            var root = UnityEngine.Object.FindObjectOfType<Root>();
            if (root == null) { sweep = false; message = "Capture unavailable: no game root."; return; }
            captureBusy = true;
            root.StartCoroutine(CaptureRenderedFrame(Time.frameCount));
        }

        private System.Collections.IEnumerator CaptureRenderedFrame(int requestedFrame)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = null;
            try
            {
                if (!Prefs.DevMode || Find.WindowStack == null || !Find.WindowStack.IsOpen<FoundationPreview>()) { sweep = false; yield break; }
                Directory.CreateDirectory(OutputDirectory);
                string stem = "foundation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + (++captureNumber);
                string png = Path.Combine(OutputDirectory, stem + ".png");
                int capturedFrame = Time.frameCount;
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                if (frame == null) throw new InvalidOperationException("No render-target texture was returned.");
                int width = frame.width, height = frame.height;
                File.WriteAllBytes(png, frame.EncodeToPNG());
                string typographyReport = page == 1 ? TypographyCalibration.WriteReport(OutputDirectory, candidate, capturedFrame) : null;
                string iconReport = page == 2 ? Path.Combine(OutputDirectory, stem + "-icons.json") : null;
                if (iconReport != null) File.WriteAllText(iconReport, ReframedIcons.MetadataJson(iconVariant), Encoding.UTF8);
                string metadataFiles = string.Join(",", new[] { typographyReport, iconReport }.Where(path => path != null)
                    .Select(path => "{\"path\":" + Quote(path) + ",\"sha256\":" + Quote(Hash(path)) + "}"));
                string currentReport = RuntimeDiagnostics.WriteReport(OutputDirectory);
                string reportSnapshot = null;
                if (currentReport != null)
                {
                    reportSnapshot = Path.Combine(OutputDirectory, stem + "-runtime.xml");
                    File.Copy(currentReport, reportSnapshot, false);
                }
                string manifest = "{\n  \"schema\":\"reframed-foundation-capture-v1\",\n  \"acceptance\":\"unreviewed development fixture; not a reference golden\",\n" +
                    "  \"capture\":" + Quote(png) + ",\n  \"unity_version\":" + Quote(Application.unityVersion) +
                    ",\n  \"assembly_version\":" + Quote(typeof(ReframedMod).Assembly.GetName().Version.ToString()) +
                    ",\n  \"mod_sha256\":" + Quote(Hash(typeof(ReframedMod).Assembly.Location)) +
                    ",\n  \"runtime_report\":" + (reportSnapshot == null ? "null" : Quote(reportSnapshot)) +
                    ",\n  \"runtime_report_sha256\":" + (reportSnapshot == null ? "null" : Quote(Hash(reportSnapshot))) +
                    ",\n  \"color_space\":" + Quote(QualitySettings.activeColorSpace.ToString()) +
                    ",\n  \"graphics_api\":" + Quote(SystemInfo.graphicsDeviceType.ToString()) +
                    ",\n  \"client_width\":" + Screen.width + ",\n  \"client_height\":" + Screen.height +
                    ",\n  \"ui_width\":" + UI.screenWidth + ",\n  \"ui_height\":" + UI.screenHeight +
                    ",\n  \"ui_scale\":" + Number(Prefs.UIScale) +
                    ",\n  \"frame_requested\":" + requestedFrame +
                    ",\n  \"frame_captured\":" + capturedFrame +
                    ",\n  \"capture_phase\":\"end_of_frame_render_target\",\n  \"png_sha256\":" + Quote(Hash(png)) +
                    ",\n  \"png_width\":" + width + ",\n  \"png_height\":" + height +
                    ",\n  \"specimen_version\":5,\n  \"specimen_page\":" + Quote(PageId) +
                    ",\n  \"candidate_id\":" + Quote(page == 1 ? TypographyCalibration.GetCandidateId(candidate) : page == 2 ? ReframedIcons.GetVariantId(iconVariant) : "primitives") +
                    ",\n  \"typography_report\":" + (typographyReport == null ? "null" : Quote(typographyReport)) +
                    ",\n  \"typography_report_sha256\":" + (typographyReport == null ? "null" : Quote(Hash(typographyReport))) +
                    ",\n  \"icon_report\":" + (iconReport == null ? "null" : Quote(iconReport)) +
                    ",\n  \"icon_report_sha256\":" + (iconReport == null ? "null" : Quote(Hash(iconReport))) +
                    ",\n  \"metadata_files\":[" + metadataFiles + "]" +
                    ",\n  \"specimen_gui_to_screen\":[" + Number(specimenPixels.x) + "," + Number(specimenPixels.y) + "," + Number(specimenPixels.width) + "," + Number(specimenPixels.height) + "]," +
                    "\n  \"specimen_coordinates_status\":\"GUIToScreenPoint; verify physical pixels before comparison\",\n" +
                    "  \"font\":\"existing game fonts only; see typography report\",\n  \"gui_state_probe_passed\":" + (stateProbePassed ? "true" : "false") +
                    ",\n  \"command_count\":" + commands + ",\n  \"dispatches\":[" + string.Join(",", dispatches) + "]\n}\n";
                File.WriteAllText(Path.Combine(OutputDirectory, stem + ".json"), manifest, Encoding.UTF8);
                message = "Captured rendered frame " + capturedFrame + ": " + stem;
                Log.Message("[Reframed UI] Foundation capture completed: " + png);
                if (sweep)
                {
                    sweepStep++;
                    if (sweepStep == 1) page = 0; // Independent primitive repeat.
                    else if (sweepStep < TypographyCalibration.Count + 2) { page = 1; candidate = sweepStep - 2; }
                    else if (sweepStep < TypographyCalibration.Count + 2 + 2 * ReframedIcons.VariantCount)
                    {
                        page = 2;
                        iconVariant = (sweepStep - TypographyCalibration.Count - 2) / 2; // Two independent frames per variant.
                    }
                    else
                    {
                        sweep = false; page = 0;
                        metrics = new FoundationMetrics(Prefs.DevMode, 30, 180); metricsPath = null;
                        message = "Capture sweep finished. Sampling primitive renderer scope.";
                    }
                    if (sweep) captureDelay = 5;
                }
            }
            catch (Exception ex)
            {
                message = "Capture failed; inspect the log.";
                Log.Warning("[Reframed UI] Could not save foundation capture: " + ex.Message);
                sweep = false;
            }
            finally { captureBusy = false; if (frame != null) UnityEngine.Object.Destroy(frame); }
        }
    }

    [StaticConstructorOnStartup]
    internal static class FoundationStartup
    {
        static FoundationStartup()
        {
            if (!Prefs.DevMode || !Environment.GetCommandLineArgs().Contains("-reframed-foundation-test")) return;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                RuntimeDiagnostics.WriteReport(FoundationPreview.OutputDirectory);
                var root = UnityEngine.Object.FindObjectOfType<Root>();
                if (root != null) root.StartCoroutine(OpenWhenInterfaceReady());
                else Log.Warning("[Reframed UI] Foundation test launch found no game root; use the development settings button.");
            });
        }

        private static System.Collections.IEnumerator OpenWhenInterfaceReady()
        {
            // ExecuteWhenFinished can precede Root's creation of its UIRoot. A
            // frame-bound wait avoids patching a global update just for this fixture.
            for (int frame = 0; frame < 600; frame++)
            {
                if (!Prefs.DevMode) yield break;
                if (!LongEventHandler.ShouldWaitForEvent && Find.WindowStack != null)
                {
                    FoundationPreview.Open();
                    yield break;
                }
                yield return null;
            }
            Log.Warning("[Reframed UI] Foundation test UI was not ready; open it from development settings.");
        }
    }
}



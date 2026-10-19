using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // Explicit development evidence only. No startup hook, Update, UI action, or
    // ordinary capture work; callers may invoke Start at transition boundaries.
    internal static class EntryMotionEvidence
    {
        private static readonly bool enabled = HasOptIn();
        private static bool busy, errorLogged;

        internal static void Start(string phase, Func<string> state)
        {
            if (!enabled || busy) return;
            try
            {
                if (!UnityData.IsInMainThread) throw new InvalidOperationException("Motion capture requires the Unity main thread.");
                var root = UnityEngine.Object.FindObjectOfType<Root>();
                if (root == null) throw new InvalidOperationException("No game root is available for motion capture.");
                busy = true;
                root.StartCoroutine(Capture(phase ?? "unspecified", state, Time.frameCount, Time.realtimeSinceStartup));
            }
            catch (Exception ex) { busy = false; Error(ex); }
        }

        private static bool HasOptIn()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, "-reframed-entry-motion-capture", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private sealed class Sample
        {
            internal Texture2D Texture;
            internal int Frame, ClientWidth, ClientHeight, UiWidth, UiHeight;
            internal float Realtime, Elapsed, UiScale;
            internal string State, Png, Sha256;
        }

        private static IEnumerator Capture(string phase, Func<string> state, int requestedFrame, float started)
        {
            var samples = new Sample[3];
            try
            {
                for (int index = 0; index < samples.Length; index++)
                {
                    float threshold = index == 0 ? 0f : index == 1 ? .09f : .28f;
                    do { yield return new WaitForEndOfFrame(); }
                    while (Time.realtimeSinceStartup - started < threshold ||
                        (index > 0 && Time.frameCount == samples[index - 1].Frame));
                    samples[index] = TryCapture(state, started);
                    if (samples[index] == null) yield break;
                }
                // Encoding, hashing and file writes intentionally start after the
                // last capture. Texture readback itself still affects frame timing.
                try { Write(phase, requestedFrame, started, samples); }
                catch (Exception ex) { Error(ex); }
            }
            finally
            {
                foreach (Sample sample in samples)
                    if (sample != null && sample.Texture != null) UnityEngine.Object.Destroy(sample.Texture);
                busy = false;
            }
        }

        private static Sample TryCapture(Func<string> state, float started)
        {
            Texture2D texture = null;
            try
            {
                var sample = new Sample
                {
                    Frame = Time.frameCount,
                    Realtime = Time.realtimeSinceStartup,
                    ClientWidth = Screen.width,
                    ClientHeight = Screen.height,
                    UiWidth = UI.screenWidth,
                    UiHeight = UI.screenHeight,
                    UiScale = Prefs.UIScale,
                    State = state == null ? null : state()
                };
                sample.Elapsed = sample.Realtime - started;
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("No end-of-frame render-target texture was returned.");
                sample.Texture = texture;
                return sample;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Error(ex);
                return null;
            }
        }

        private static void Write(string phase, int requestedFrame, float started, Sample[] samples)
        {
            string directory = Path.Combine(GenFilePaths.SaveDataFolderPath, "ReframedUI", "EntryMotion");
            Directory.CreateDirectory(directory);
            string stem = "entry-" + SafePhase(phase) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string assemblyPath = typeof(EntryMotionEvidence).Assembly.Location;
            string assemblyHash = HashFile(assemblyPath);
            foreach (Sample sample in samples)
            {
                sample.Png = Path.Combine(directory, stem + "-" + (Array.IndexOf(samples, sample) + 1) + ".png");
                byte[] png = sample.Texture.EncodeToPNG();
                if (png == null || png.Length == 0) throw new InvalidOperationException("PNG encoder returned no data.");
                File.WriteAllBytes(sample.Png, png);
                sample.Sha256 = HashFile(sample.Png);
            }
            var json = new StringBuilder();
            json.Append("{\n  \"schema\":\"reframed-entry-motion-v1\",\n  \"capture_phase\":\"end_of_frame_render_target\",\n");
            json.Append("  \"acceptance\":\"unreviewed development evidence; not a source golden or proof of a presented monitor frame\",\n");
            json.Append("  \"timing_limit\":\"texture readback affects timing; not a performance measurement\",\n");
            json.Append("  \"phase\":").Append(Quote(phase)).Append(",\n  \"frame_requested\":").Append(requestedFrame);
            json.Append(",\n  \"realtime_started_seconds\":").Append(Number(started));
            json.Append(",\n  \"mod_path\":").Append(Quote(assemblyPath)).Append(",\n  \"mod_sha256\":").Append(Quote(assemblyHash));
            json.Append(",\n  \"assembly_version\":").Append(Quote(typeof(EntryMotionEvidence).Assembly.GetName().Version.ToString()));
            json.Append(",\n  \"unity_version\":").Append(Quote(Application.unityVersion));
            json.Append(",\n  \"samples\":[\n");
            for (int index = 0; index < samples.Length; index++)
            {
                Sample s = samples[index];
                if (index > 0) json.Append(",\n");
                json.Append("    {\"index\":").Append(index + 1).Append(",\"minimum_elapsed_seconds\":").Append(index == 0 ? "0" : index == 1 ? "0.09" : "0.28");
                json.Append(",\"frame_captured\":").Append(s.Frame).Append(",\"realtime_seconds\":").Append(Number(s.Realtime));
                json.Append(",\"elapsed_seconds\":").Append(Number(s.Elapsed)).Append(",\"state\":").Append(Quote(s.State));
                json.Append(",\"client_width\":").Append(s.ClientWidth).Append(",\"client_height\":").Append(s.ClientHeight);
                json.Append(",\"ui_width\":").Append(s.UiWidth).Append(",\"ui_height\":").Append(s.UiHeight).Append(",\"ui_scale\":").Append(Number(s.UiScale));
                json.Append(",\"png_width\":").Append(s.Texture.width).Append(",\"png_height\":").Append(s.Texture.height);
                json.Append(",\"png\":").Append(Quote(s.Png)).Append(",\"png_sha256\":").Append(Quote(s.Sha256)).Append('}');
            }
            json.Append("\n  ]\n}\n");
            string receipt = Path.Combine(directory, stem + ".json");
            File.WriteAllText(receipt, json.ToString(), new UTF8Encoding(false));
            Log.Message("[Reframed UI] Entry motion evidence saved: " + receipt);
        }

        private static string SafePhase(string phase)
        {
            var result = new StringBuilder();
            foreach (char c in phase)
            {
                if (result.Length >= 48) break;
                result.Append(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_' ? c : '_');
            }
            return result.Length == 0 ? "unspecified" : result.ToString();
        }

        private static string Quote(string value)
        {
            if (value == null) return "null";
            var result = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') result.Append('\\').Append(c);
                else if (c < 32 || char.IsSurrogate(c)) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else result.Append(c);
            }
            return result.Append('"').ToString();
        }

        private static string Number(float value) => float.IsNaN(value) || float.IsInfinity(value) ? "null" : value.ToString("R", CultureInfo.InvariantCulture);

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void Error(Exception ex)
        {
            if (errorLogged) return;
            errorLogged = true;
            Log.Warning("[Reframed UI] Entry motion evidence failed: " + ex.Message);
        }
    }
}

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ReframedUI
{
    // Development fixture only. The caller must also check live DevMode/Repaint.
    // Measures one synchronous renderer scope, never whole-frame or GPU time.
    internal sealed class FoundationMetrics
    {
        private readonly bool developmentMode;
        private readonly int warmupTarget, sampleTarget, ownerThread;
        private readonly long frequency;
        private readonly Func<long> timestamp;
        private Func<long> allocationCounter;
        private readonly FoundationMetricSample[] samples;
        private string allocationReason;
        private int warmupObserved, warmupFailed, collected, lastFrame = -1, activeFrame;
        private long started, allocatedAtStart;
        private bool active, allocationAtStartAvailable;

        internal FoundationMetrics(bool developmentMode, int warmupSamples, int sampleCount)
            : this(developmentMode, warmupSamples, sampleCount, Stopwatch.GetTimestamp,
                  Stopwatch.Frequency, FindAllocationCounter(), null) { }

        // Dependency seam for deterministic contracts, not game-derived measurements.
        internal FoundationMetrics(bool developmentMode, int warmupSamples, int sampleCount,
            Func<long> timestamp, long frequency, Func<long> allocationCounter, string unavailableReason)
        {
            if (warmupSamples < 0 || warmupSamples > 1000000) throw new ArgumentOutOfRangeException(nameof(warmupSamples));
            if (sampleCount < 1 || sampleCount > 1000000) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (timestamp == null) throw new ArgumentNullException(nameof(timestamp));
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            this.developmentMode = developmentMode;
            warmupTarget = warmupSamples;
            sampleTarget = sampleCount;
            this.timestamp = timestamp;
            this.frequency = frequency;
            this.allocationCounter = allocationCounter;
            allocationReason = allocationCounter == null
                ? (unavailableReason ?? "GC.GetAllocatedBytesForCurrentThread unavailable on this runtime; no heap-size substitute")
                : "GC.GetAllocatedBytesForCurrentThread; synchronous current-thread allocation delta only";
            ownerThread = Thread.CurrentThread.ManagedThreadId;
            samples = developmentMode ? new FoundationMetricSample[sampleCount] : new FoundationMetricSample[0];
        }

        internal bool IsComplete => collected == sampleTarget;
        internal int CollectedSamples => collected;
        internal int WarmupObserved => warmupObserved;
        internal bool IsSampling => active;

        internal bool BeginSample(int unityFrame)
        {
            CheckThread();
            if (active) throw new InvalidOperationException("Nested measurement scope");
            if (!developmentMode || IsComplete) return false;
            if (unityFrame < 0 || unityFrame < lastFrame) throw new ArgumentOutOfRangeException(nameof(unityFrame));
            if (unityFrame == lastFrame) return false; // IMGUI may repaint more than once in a frame.
            lastFrame = activeFrame = unityFrame;
            allocationAtStartAvailable = TryAllocated(out allocatedAtStart);
            started = timestamp();
            active = true;
            return true;
        }

        internal void EndSample(bool succeeded = true)
        {
            CheckThread();
            if (!active) throw new InvalidOperationException("No active measurement scope");
            long ended = timestamp();
            long allocatedAtEnd;
            bool allocationAtEndAvailable = TryAllocated(out allocatedAtEnd);
            active = false;
            if (ended < started) throw new InvalidOperationException("Monotonic clock moved backwards");
            long? allocated = null;
            if (allocationAtStartAvailable && allocationAtEndAvailable)
            {
                if (allocatedAtEnd >= allocatedAtStart) allocated = allocatedAtEnd - allocatedAtStart;
                else DisableAllocation("Allocation counter moved backwards; affected sample unknown");
            }
            double elapsed = (ended - started) * 1000.0 / frequency;
            if (warmupObserved < warmupTarget)
            {
                warmupObserved++;
                if (!succeeded) warmupFailed++;
                return;
            }
            samples[collected++] = new FoundationMetricSample(activeFrame, elapsed, allocated, succeeded);
        }

        internal FoundationMetricSnapshot Snapshot()
        {
            CheckThread();
            if (active) throw new InvalidOperationException("Finish the scope before exporting or sorting samples");
            var copy = new FoundationMetricSample[collected];
            Array.Copy(samples, copy, collected);
            return new FoundationMetricSnapshot(developmentMode, warmupTarget, warmupObserved, sampleTarget,
                copy, allocationCounter != null, allocationReason, warmupFailed);
        }

        internal void WriteReport(string path) => Snapshot().WriteReport(path);

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Measurement and export must stay on the creating thread");
        }

        private bool TryAllocated(out long value)
        {
            value = 0;
            if (allocationCounter == null) return false;
            try
            {
                value = allocationCounter();
                if (value >= 0) return true;
                DisableAllocation("Allocation API returned a negative value; affected sample unknown");
            }
            catch (Exception ex)
            {
                DisableAllocation("Allocation API failed: " + ex.GetType().Name + "; affected sample unknown");
            }
            return false;
        }

        private void DisableAllocation(string reason)
        {
            allocationCounter = null;
            allocationReason = reason;
        }

        private static Func<long> FindAllocationCounter()
        {
            try
            {
                MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                return method == null || method.ReturnType != typeof(long) ? null
                    : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            }
            catch (Exception) { return null; }
        }
    }

    internal struct FoundationMetricSample
    {
        internal readonly int Frame;
        internal readonly double CpuMilliseconds;
        internal readonly long? AllocatedBytes;
        internal readonly bool Succeeded;
        internal FoundationMetricSample(int frame, double cpuMilliseconds, long? allocatedBytes, bool succeeded)
        {
            Frame = frame; CpuMilliseconds = cpuMilliseconds; AllocatedBytes = allocatedBytes; Succeeded = succeeded;
        }
    }

    internal sealed class FoundationMetricSnapshot
    {
        internal readonly bool DevelopmentEnabled, AllocationApiAvailable;
        internal readonly int WarmupTarget, WarmupObserved, SampleTarget, WarmupFailedScopes;
        internal readonly FoundationMetricSample[] Samples;
        internal readonly string AllocationReason;
        internal bool Complete => DevelopmentEnabled && WarmupObserved == WarmupTarget && Samples.Length == SampleTarget;
        internal int FailedScopes { get { int count = 0; foreach (var sample in Samples) if (!sample.Succeeded) count++; return count; } }

        internal FoundationMetricSnapshot(bool developmentEnabled, int warmupTarget, int warmupObserved,
            int sampleTarget, FoundationMetricSample[] samples, bool allocationApiAvailable, string allocationReason, int warmupFailedScopes)
        {
            DevelopmentEnabled = developmentEnabled; WarmupTarget = warmupTarget; WarmupObserved = warmupObserved;
            SampleTarget = sampleTarget; WarmupFailedScopes = warmupFailedScopes; Samples = samples; AllocationApiAvailable = allocationApiAvailable; AllocationReason = allocationReason;
        }

        // Nearest-rank quantiles. No fabricated percentile for an empty series.
        internal double? CpuPercentile(double percentile)
        {
            if (double.IsNaN(percentile) || percentile <= 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
            if (Samples.Length == 0) return null;
            var ordered = new double[Samples.Length];
            for (int i = 0; i < Samples.Length; i++) ordered[i] = Samples[i].CpuMilliseconds;
            Array.Sort(ordered);
            return ordered[(int)Math.Ceiling(percentile * ordered.Length) - 1];
        }

        internal string ToJson()
        {
            int allocatedCount = 0;
            foreach (var sample in Samples) if (sample.AllocatedBytes.HasValue) allocatedCount++;
            string allocationStatus = !AllocationApiAvailable && allocatedCount == 0 ? "unsupported"
                : allocatedCount == Samples.Length && Samples.Length > 0 ? "measured" : "partial_or_not_sampled";
            var json = new StringBuilder();
            json.Append("{\n  \"schema\":\"reframed-foundation-metrics-v1\",\n  \"measurement\":\"synchronous renderer-scope elapsed time (CPU-side timing)\",\n");
            json.Append("  \"cpu_clock\":\"Stopwatch monotonic elapsed wall time; includes synchronous waits; not process CPU counters\",\n");
            json.Append("  \"scope\":\"caller-selected renderer only; excludes export and quantile calculation\",\n");
            json.Append("  \"total_frame_ms\":null,\n  \"gpu_ms\":null,\n  \"performance_budget\":null,\n  \"performance_pass\":null,\n");
            json.Append("  \"development_enabled\":" + Bool(DevelopmentEnabled) + ",\n  \"complete\":" + Bool(Complete) + ",\n");
            json.Append("  \"warmup_target\":" + WarmupTarget + ",\n  \"warmup_observed\":" + WarmupObserved + ",\n  \"sample_target\":" + SampleTarget + ",\n  \"sample_count\":" + Samples.Length + ",\n");
            json.Append("  \"warmup_failed_scopes\":" + WarmupFailedScopes + ",\n");
            json.Append("  \"failed_scopes\":" + FailedScopes + ",\n  \"percentile_method\":\"nearest rank: sorted[ceil(p*n)-1]\",\n");
            json.Append("  \"renderer_scope_ms\":{\"p50\":" + Number(CpuPercentile(0.50)) + ",\"p95\":" + Number(CpuPercentile(0.95)) + ",\"p99\":" + Number(CpuPercentile(0.99)) + "},\n");
            json.Append("  \"allocation\":{\"status\":" + Quote(allocationStatus) + ",\"api_available\":" + Bool(AllocationApiAvailable) + ",\"reason\":" + Quote(AllocationReason) + ",\"samples_with_value\":" + allocatedCount + "},\n");
            json.Append("  \"samples\":[");
            for (int i = 0; i < Samples.Length; i++)
            {
                if (i > 0) json.Append(',');
                var sample = Samples[i];
                json.Append("{\"frame\":" + sample.Frame + ",\"renderer_scope_ms\":" + Number(sample.CpuMilliseconds)
                    + ",\"allocated_bytes\":" + (sample.AllocatedBytes.HasValue ? sample.AllocatedBytes.Value.ToString(CultureInfo.InvariantCulture) : "null")
                    + ",\"succeeded\":" + Bool(sample.Succeeded) + "}");
            }
            return json.Append("]\n}\n").ToString();
        }

        internal void WriteReport(string path)
        {
            if (!DevelopmentEnabled) throw new InvalidOperationException("Development measurement is disabled");
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(ToJson());
        }

        private static string Number(double? value) => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "null";
        private static string Bool(bool value) => value ? "true" : "false";
        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char ch in value)
            {
                if (ch == '\\' || ch == '"') text.Append('\\').Append(ch);
                else if (ch < 32) text.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(ch);
            }
            return text.Append('"').ToString();
        }
    }
}

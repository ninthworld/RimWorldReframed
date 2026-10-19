using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ReframedUI;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Throws<T>(Action action, string message) where T : Exception
{
    bool raised = false;
    try { action(); } catch (T) { raised = true; }
    Check(raised, message);
}

long clock = 0, allocated = 100;
var sampler = new FoundationMetrics(true, 2, 100, () => clock, 1000, () => allocated, null);
for (int i = 0; i < 102; i++)
{
    Check(sampler.BeginSample(i), "Fresh frame must start");
    clock += i < 2 ? 999 : 102 - i; // Measured 100..1; warmup must be excluded.
    allocated += i < 2 ? 9999 : 7;
    sampler.EndSample(i != 0 && i != 5);
    if (i == 0) Check(!sampler.BeginSample(i), "Duplicate IMGUI frame must not sample twice");
}
var snapshot = sampler.Snapshot();
Check(sampler.IsComplete && snapshot.Complete, "Target count must complete after warmup");
Check(sampler.CollectedSamples == 100 && sampler.WarmupObserved == 2, "Live progress properties");
Check(snapshot.CpuPercentile(.50) == 50, "Nearest-rank p50");
Check(snapshot.CpuPercentile(.95) == 95, "Nearest-rank p95");
Check(snapshot.CpuPercentile(.99) == 99, "Nearest-rank p99");
Check(snapshot.Samples[0].Frame == 2 && snapshot.Samples[0].CpuMilliseconds == 100, "Warmup frames must not leak into samples");
Check(snapshot.Samples.All(row => row.AllocatedBytes == 7), "Only measured current-thread deltas");
Check(snapshot.WarmupFailedScopes == 1, "Warmup render failures remain observable");
Check(snapshot.FailedScopes == 1, "Throwing render scope must remain observable");
Check(!sampler.BeginSample(200), "Finished run cannot silently keep sampling");
Throws<ArgumentOutOfRangeException>(() => snapshot.CpuPercentile(0), "Zero percentile invalid");
Throws<ArgumentOutOfRangeException>(() => snapshot.CpuPercentile(double.NaN), "NaN percentile invalid");
using (var json = JsonDocument.Parse(snapshot.ToJson()))
{
    Check(json.RootElement.GetProperty("sample_count").GetInt32() == 100, "Serialized actual count");
    Check(json.RootElement.GetProperty("renderer_scope_ms").GetProperty("p99").GetDouble() == 99, "Serialized quantile");
    foreach (string name in new[] { "total_frame_ms", "gpu_ms", "performance_budget", "performance_pass" })
        Check(json.RootElement.GetProperty(name).ValueKind == JsonValueKind.Null, "No invented whole-frame/GPU/budget/pass claim");
}
var noAllocation = new FoundationMetrics(true, 0, 1, () => clock, 1000, null, "Unsupported test runtime");
Check(noAllocation.Snapshot().CpuPercentile(.5) == null, "No percentile before samples");
Check(noAllocation.BeginSample(1), "Unsupported allocation still measures elapsed scope");
clock += 4;
noAllocation.EndSample();
Check(noAllocation.Snapshot().Samples[0].AllocatedBytes == null, "Unsupported allocation must remain null");
using (var json = JsonDocument.Parse(noAllocation.Snapshot().ToJson()))
{
    Check(json.RootElement.GetProperty("allocation").GetProperty("status").GetString() == "unsupported", "Unsupported reason serialized");
    Check(json.RootElement.GetProperty("samples")[0].GetProperty("allocated_bytes").ValueKind == JsonValueKind.Null, "No fabricated zero allocations");
}
var disabled = new FoundationMetrics(false, 0, 1, () => clock, 1000, null, "not used");
Check(!disabled.BeginSample(1), "Production-disabled sampler cannot start");
Check(!disabled.Snapshot().Complete, "Disabled sampler cannot claim completion");
Throws<InvalidOperationException>(() => disabled.WriteReport("should-not-write.json"), "Disabled sampler cannot write");
var lifecycle = new FoundationMetrics(true, 0, 2, () => clock, 1000, () => allocated, null);
Throws<InvalidOperationException>(() => lifecycle.EndSample(), "End without begin invalid");
Check(lifecycle.BeginSample(4), "Scope begins");
Throws<InvalidOperationException>(() => lifecycle.BeginSample(5), "Nested scope invalid");
Throws<InvalidOperationException>(() => lifecycle.Snapshot(), "Export while measuring invalid");
Exception foreign = null;
var thread = new Thread(() => { try { lifecycle.EndSample(); } catch (Exception ex) { foreign = ex; } });
thread.Start(); thread.Join();
Check(foreign is InvalidOperationException, "Cross-thread scope completion invalid");
clock += 1; lifecycle.EndSample();
Throws<ArgumentOutOfRangeException>(() => lifecycle.BeginSample(3), "Backward frame invalid");
Throws<ArgumentOutOfRangeException>(() => new FoundationMetrics(true, -1, 1), "Negative warmup invalid");
Throws<ArgumentOutOfRangeException>(() => new FoundationMetrics(true, 0, 0), "Empty sample target invalid");
int allocationCalls = 0;
var brokenCounter = new FoundationMetrics(true, 0, 1, () => clock, 1000,
    () => ++allocationCalls == 1 ? 10 : throw new NotSupportedException("not supported"), null);
brokenCounter.BeginSample(1); clock += 1; brokenCounter.EndSample();
Check(brokenCounter.Snapshot().Samples[0].AllocatedBytes == null, "Throwing allocation API stays unknown");
Check(!brokenCounter.Snapshot().AllocationApiAvailable, "Broken allocation API disabled");
var backwardCounter = new FoundationMetrics(true, 0, 1, () => clock, 1000, () => allocated, null);
allocated = 20; backwardCounter.BeginSample(1); allocated = 10; clock += 1; backwardCounter.EndSample();
Check(backwardCounter.Snapshot().Samples[0].AllocatedBytes == null, "Decreasing allocation counter stays unknown");
long badClock = 10;
var backwardClock = new FoundationMetrics(true, 0, 1, () => badClock, 1000, null, "unsupported");
backwardClock.BeginSample(1); badClock = 9;
Throws<InvalidOperationException>(() => backwardClock.EndSample(), "Backward monotonic time invalid");
Check(backwardClock.CollectedSamples == 0, "Clock failure cannot fabricate measurement");
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../work/capture-contracts"));
Directory.CreateDirectory(root);
string output = Path.Combine(root, "metrics-" + Guid.NewGuid().ToString("N") + ".json");
sampler.WriteReport(output);
Check(File.Exists(output), "Report written");
Throws<IOException>(() => sampler.WriteReport(output), "Existing report must not be overwritten");
Console.WriteLine($"PASS: {checks} capture/performance contracts (synthetic clocks/counters; not game measurements)");
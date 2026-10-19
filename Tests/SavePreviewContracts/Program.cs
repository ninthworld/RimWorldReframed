using System;
using System.IO;
using ReframedUI;

int passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    passed++;
}
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "ReframedPreviewContracts-" + Guid.NewGuid().ToString("N")));
string saves = Path.Combine(root, "Saves"), store = Path.Combine(root, "Previews");
Directory.CreateDirectory(saves);
SavePreviewStore.Configure(saves, store);
string path = Path.Combine(saves, "Colony.rws");
byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
SavePreviewRecord Record() => new SavePreviewRecord { Colonists = 7, Wealth = 12345.5, PreviewPng = png,
    Source = "saved", MapId = 12, SavedTick = 5000, CapturedTick = 5001, CapturedUtcTicks = DateTime.UtcNow.Ticks };
try
{
    Check(SavePreviewStore.Read(path) == null, "Missing saves do not create a preview");
    File.WriteAllText(path, "synthetic save revision A");
    var original = SavePreviewStore.Fingerprint(path);
    Check(original != null, "Fingerprint valid save");
    Check(SavePreviewStore.Write(original, Record()), "Write valid native snapshot shape");
    var read = SavePreviewStore.Read(path);
    Check(read != null && read.Colonists == 7 && read.Wealth == 12345.5 && read.PreviewPng.Length == png.Length
        && read.SavedTick == 5000 && read.CapturedTick == 5001 && read.SaveSha256 == original.Hash, "Roundtrip metadata/image/provenance");
    Check(File.ReadAllText(path) == "synthetic save revision A", "Save contents untouched");
    Check(new FileInfo(path).LastWriteTimeUtc.Ticks == original.Written, "Save timestamp untouched");
    string other = Path.Combine(saves, "Other.rws");
    File.Copy(path, other);
    Check(SavePreviewStore.Read(other) == null && SavePreviewStore.SidecarPath(path) != SavePreviewStore.SidecarPath(other), "Same contents different file identity");
    File.Copy(SavePreviewStore.SidecarPath(path), SavePreviewStore.SidecarPath(other));
    Check(SavePreviewStore.Read(other) == null, "Copied sidecar cannot describe renamed file");
    File.WriteAllText(path, "synthetic save revision B");
    File.SetLastWriteTimeUtc(path, new DateTime(original.Written, DateTimeKind.Utc));
    Check(new FileInfo(path).Length == original.Length && SavePreviewStore.Read(path) == null, "Hash rejects same-size same-time replacement");
    Check(!SavePreviewStore.Write(original, Record()), "Delayed old completion cannot bind to replacement");
    var current = SavePreviewStore.Fingerprint(path);
    Check(SavePreviewStore.Write(current, Record()) && SavePreviewStore.Read(path) != null, "New revision atomically replaces old record");
    var invalid = Record(); invalid.Wealth = double.NaN;
    Check(!SavePreviewStore.Write(current, invalid), "Reject nonfinite wealth");
    invalid = Record(); invalid.Colonists = -3;
    Check(!SavePreviewStore.Write(current, invalid), "Reject negative population");
    invalid = Record(); invalid.PreviewPng = new byte[] { 1, 2, 3 };
    Check(!SavePreviewStore.Write(current, invalid), "Reject malformed image");
    invalid = Record(); invalid.PreviewPng = (byte[])png.Clone(); invalid.PreviewPng[16] = 1;
    Check(!SavePreviewStore.Write(current, invalid), "Reject excessive decoded image dimensions");
    invalid = Record(); invalid.Source = "invented";
    Check(!SavePreviewStore.Write(current, invalid), "Reject unsupported provenance");
    var statistics = Record(); statistics.PreviewPng = null;
    Check(SavePreviewStore.Write(current, statistics) && SavePreviewStore.Read(path).PreviewPng == null, "Known statistics with unavailable image remain truthful");
    Check(SavePreviewStore.Fingerprint(Path.Combine(root, "outside.rws")) == null, "Reject save path outside native directory");
    Check(SavePreviewStore.Fingerprint(Path.Combine(saves, "Colony.rws.old")) == null, "Reject native backup as active save");
    File.WriteAllBytes(SavePreviewStore.SidecarPath(path), new byte[100]);
    Check(SavePreviewStore.Read(path) == null, "Corrupt sidecar fails closed");
    Check(SavePreviewStore.Write(current, Record()), "Restore after corruption");
    using (var stream = new FileStream(SavePreviewStore.SidecarPath(path), FileMode.Append)) stream.WriteByte(0);
    Check(SavePreviewStore.Read(path) == null, "Reject trailing payload");
    Check(Directory.GetFiles(store, "*.tmp").Length == 0, "No abandoned write temporaries");
    Console.WriteLine($"PASS {passed} save-preview disk contracts; no native game/rendering integration claimed.");
}
finally
{
    if (!root.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
        throw new Exception("Scratch cleanup escaped the test output directory");
    Directory.Delete(root, true);
}

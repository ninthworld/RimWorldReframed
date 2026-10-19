using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if !SAVE_PREVIEW_CONTRACTS
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;
#endif

namespace ReframedUI
{
    internal sealed class SavePreviewRecord
    {
        internal int? Colonists;
        internal double? Wealth;
        internal byte[] PreviewPng;
        internal string Source;
        internal int MapId;
        internal int SavedTick;
        internal int CapturedTick;
        internal string SaveSha256;
        internal long CapturedUtcTicks;
    }

    // The disk half has no Unity dependencies. Read is safe on the browser's metadata worker.
    internal static class SavePreviewStore
    {
        private const int Schema = 1;
        private const int MaxPng = 2 * 1024 * 1024;
        private const long MaxSave = 256L * 1024 * 1024;
        private static readonly object DiskLock = new object();
        private static string savesRoot, storeRoot;

        internal static void Configure(string saves, string store)
        {
            savesRoot = Path.GetFullPath(saves).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            storeRoot = Path.GetFullPath(store);
        }

        private static string Canonical(string path)
        {
            if (savesRoot == null || string.IsNullOrEmpty(path)) return null;
            string full = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(full), savesRoot, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(full), ".rws", StringComparison.OrdinalIgnoreCase)) return null;
            return full;
        }

        internal sealed class Identity
        {
            internal string Path, Hash;
            internal long Length, Written;
            internal bool Matches(Identity other) => other != null && Length == other.Length && Written == other.Written
                && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) && Hash == other.Hash;
        }

        internal static Identity Fingerprint(string path)
        {
            try
            {
                path = Canonical(path);
                if (path == null) return null;
                var before = new FileInfo(path);
                if (!before.Exists || before.Length < 1 || before.Length > MaxSave) return null;
                long length = before.Length, written = before.LastWriteTimeUtc.Ticks;
                string hash;
                var timer = Stopwatch.StartNew();
                // Deny concurrent writes and replacement while hashing this exact revision.
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var sha = SHA256.Create())
                {
                    if (input.Length != length) return null;
                    var buffer = new byte[65536];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (timer.ElapsedMilliseconds > 2000) return null;
                        sha.TransformBlock(buffer, 0, count, null, 0);
                    }
                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    hash = Hex(sha.Hash);
                }
                before.Refresh();
                if (!before.Exists || before.Length != length || before.LastWriteTimeUtc.Ticks != written) return null;
                return new Identity { Path = path, Length = length, Written = written, Hash = hash };
            }
            catch { return null; }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        internal static string SidecarPath(string path)
        {
            path = Canonical(path);
            if (path == null) return null;
            using (var sha = SHA256.Create())
                return Path.Combine(storeRoot, Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToUpperInvariant()))) + ".preview");
        }

        internal static SavePreviewRecord Read(string savePath)
        {
            try
            {
                string path = Canonical(savePath), sidecar = SidecarPath(savePath);
                if (path == null || sidecar == null) return null;
                byte[] data;
                using (var input = new FileStream(sidecar, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var payload = new BinaryReader(input))
                {
                    if (input.Length < 64 || input.Length > MaxPng + 16384) return null;
                    data = payload.ReadBytes((int)input.Length);
                    if (data.Length != input.Length) return null;
                }
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != 0x52575056 || reader.ReadInt32() != Schema) return null;
                    var id = new Identity { Path = reader.ReadString(), Length = reader.ReadInt64(), Written = reader.ReadInt64(), Hash = reader.ReadString() };
                    if (!string.Equals(path, id.Path, StringComparison.OrdinalIgnoreCase)) return null;
                    var file = new FileInfo(path);
                    if (!file.Exists || file.Length != id.Length || file.LastWriteTimeUtc.Ticks != id.Written) return null;
                    var record = new SavePreviewRecord { Source = reader.ReadString(), MapId = reader.ReadInt32(), SavedTick = reader.ReadInt32(),
                        CapturedTick = reader.ReadInt32(), CapturedUtcTicks = reader.ReadInt64(), SaveSha256 = id.Hash };
                    int count = reader.ReadInt32(); double wealth = reader.ReadDouble();
                    record.Colonists = count < 0 ? (int?)null : count;
                    record.Wealth = wealth < 0 ? (double?)null : wealth;
                    int pngLength = reader.ReadInt32();
                    if (pngLength < 0 || pngLength > MaxPng || stream.Length - stream.Position != pngLength) return null;
                    record.PreviewPng = pngLength == 0 ? null : reader.ReadBytes(pngLength);
                    if (!Valid(record) || !id.Matches(Fingerprint(path))) return null;
                    return record;
                }
            }
            catch { return null; }
        }

        internal static bool Write(Identity identity, SavePreviewRecord record)
        {
            if (identity == null || !Valid(record)) return false;
            string temporary = null;
            try
            {
                lock (DiskLock)
                {
                    if (!identity.Matches(Fingerprint(identity.Path))) return false;
                    Directory.CreateDirectory(storeRoot);
                    string target = SidecarPath(identity.Path);
                    temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new BinaryWriter(output, Encoding.UTF8))
                    {
                        writer.Write(0x52575056); writer.Write(Schema);
                        writer.Write(identity.Path); writer.Write(identity.Length); writer.Write(identity.Written); writer.Write(identity.Hash);
                        writer.Write(record.Source); writer.Write(record.MapId); writer.Write(record.SavedTick); writer.Write(record.CapturedTick);
                        writer.Write(record.CapturedUtcTicks); writer.Write(record.Colonists ?? -1); writer.Write(record.Wealth ?? -1);
                        writer.Write(record.PreviewPng?.Length ?? 0);
                        if (record.PreviewPng != null) writer.Write(record.PreviewPng);
                    }
                    // A queued completion cannot attach an old snapshot to an overwritten save.
                    if (!identity.Matches(Fingerprint(identity.Path))) return false;
                    if (File.Exists(target)) File.Replace(temporary, target, null);
                    else File.Move(temporary, target);
                    temporary = null;
                    return true;
                }
            }
            catch { return false; }
            finally { if (temporary != null) try { File.Delete(temporary); } catch { } }
        }

        private static bool Valid(SavePreviewRecord record)
        {
            if (record == null || (record.Source != "saved" && record.Source != "loaded") || record.Colonists < 0
                || record.Colonists > 100000 || (record.Wealth.HasValue && (double.IsNaN(record.Wealth.Value)
                    || double.IsInfinity(record.Wealth.Value) || record.Wealth.Value < 0))) return false;
            byte[] png = record.PreviewPng;
            if (png == null) return true;
            if (png.Length < 33 || png.Length > MaxPng) return false;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < 8; i++) if (png[i] != signature[i]) return false;
            if (png[12] != 73 || png[13] != 72 || png[14] != 68 || png[15] != 82) return false;
            uint width = BigEndian(png, 16), height = BigEndian(png, 20);
            return width > 0 && height > 0 && width <= 512 && height <= 512;
        }

        private static uint BigEndian(byte[] bytes, int offset) => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16)
            | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

#if !SAVE_PREVIEW_CONTRACTS
        private const string HarmonyId = "chris.reframedui.save-previews";
        private static readonly object PendingLock = new object();
        private static readonly List<CaptureRequest> Pending = new List<CaptureRequest>();
        private static bool initialized, rendering;

        private sealed class CaptureRequest
        {
            internal Identity Identity;
            internal SavePreviewRecord Record;
            internal Game Game;
            internal Map Map;
            internal DateTime Requested;
        }

        internal static void Initialize()
        {
            if (initialized) return;
            var harmony = new Harmony(HarmonyId);
            try
            {
                Configure(GenFilePaths.SavedGamesFolderPath, Path.Combine(GenFilePaths.SaveDataFolderPath, "ReframedUI", "SavePreviews"));
                MethodInfo save = AccessTools.DeclaredMethod(typeof(SafeSaver), "Save", new[] { typeof(string), typeof(string), typeof(Action), typeof(bool) });
                MethodInfo load = AccessTools.DeclaredMethod(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow", new[] { typeof(string) });
                MethodInfo update = AccessTools.DeclaredMethod(typeof(Root_Play), "Update", Type.EmptyTypes);
                if (save == null || !save.IsStatic || save.ReturnType != typeof(void) || load == null || !load.IsStatic || load.ReturnType != typeof(void)
                    || update == null || update.IsStatic || update.ReturnType != typeof(void))
                    throw new MissingMethodException("Native save preview hook signature changed.");
                harmony.Patch(save, postfix: new HarmonyMethod(typeof(SavePreviewStore), nameof(Saved)));
                harmony.Patch(load, postfix: new HarmonyMethod(typeof(SavePreviewStore), nameof(Loaded)));
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(SavePreviewStore), nameof(CapturePending)));
                initialized = true;
            }
            catch (Exception e)
            {
                harmony.UnpatchAll(HarmonyId);
                Log.Warning("[Reframed UI] Save previews unavailable: " + e.Message);
            }
        }

        private static void Saved(string path, string documentElementName)
        {
            if (documentElementName != "savegame") return;
            try { Queue(path, "saved"); } catch (Exception e) { Warn(e); }
        }

        private static void Loaded(string fileName)
        {
            try
            {
                // SavedGameLoaderNow constructs a fresh Game without InitData before
                // Game.LoadGame. Its own argument is the actual selected native file.
                if (!string.IsNullOrEmpty(fileName) && Current.ProgramState == ProgramState.Playing)
                    Queue(GenFilePaths.FilePathForSavedGame(fileName), "loaded");
            }
            catch (Exception e) { Warn(e); }
        }

        private static void Queue(string path, string source)
        {
            if (ReframedMod.Settings == null || !ReframedMod.Settings.Enabled || !ReframedMod.Settings.MainMenu) return;
            // Native save/load may run on a long-event thread. No statistics traversal or Unity work here.
            Game game = Current.Game;
            Map map = game?.CurrentMap;
            if (map == null || Current.ProgramState != ProgramState.Playing) return;
            Identity id = Fingerprint(path);
            if (id == null) return;
            int tick = game.tickManager.TicksGame;
            var record = new SavePreviewRecord { Source = source, MapId = map.uniqueID, SavedTick = tick,
                CapturedTick = -1, CapturedUtcTicks = DateTime.UtcNow.Ticks, SaveSha256 = id.Hash };
            var request = new CaptureRequest { Identity = id, Record = record, Game = game, Map = map, Requested = DateTime.UtcNow };
            lock (PendingLock)
            {
                Pending.RemoveAll(item => string.Equals(item.Identity.Path, id.Path, StringComparison.OrdinalIgnoreCase));
                if (Pending.Count >= 8) Pending.RemoveAt(0);
                Pending.Add(request);
            }
        }

        private static void CapturePending()
        {
            if (rendering || !initialized || !UnityData.IsInMainThread) return;
            if (ReframedMod.Settings == null || !ReframedMod.Settings.Enabled || !ReframedMod.Settings.MainMenu)
            { lock (PendingLock) Pending.Clear(); return; }
            try
            {
                List<CaptureRequest> ready = null;
                lock (PendingLock)
                {
                    if (Pending.Count == 0) return;
                    Pending.RemoveAll(item => item.Game != Current.Game || item.Map != Find.CurrentMap || (DateTime.UtcNow - item.Requested).TotalSeconds > 15
                        || item.Game.tickManager.TicksGame < item.Record.SavedTick || item.Game.tickManager.TicksGame - item.Record.SavedTick > 120);
                    if (Pending.Count == 0 || Find.Camera == null || LongEventHandler.ShouldWaitForEvent || !WorldRendererUtility.DrawingMap) return;
                    ready = new List<CaptureRequest>(Pending);
                    Pending.Clear();
                }
                // Root_Play.Update postfix runs on the main thread, outside an active camera render.
                byte[] png = null;
                try { png = Capture(Find.Camera); } catch (Exception e) { Warn(e); }
                foreach (var request in ready)
                {
                    int tick = request.Game.tickManager.TicksGame;
                    // Do not attach a much later colony image to a prior serialized state.
                    if (tick < request.Record.SavedTick || tick - request.Record.SavedTick > 120) continue;
                    request.Record.Colonists = request.Map.mapPawns.FreeColonistsCount;
                    request.Record.Wealth = request.Map.wealthWatcher.WealthTotal;
                    request.Record.PreviewPng = png;
                    request.Record.CapturedTick = tick;
                    request.Record.CapturedUtcTicks = DateTime.UtcNow.Ticks;
                    Task.Run(() => Write(request.Identity, request.Record));
                }
            }
            catch (Exception e) { Warn(e); }
        }

        private static byte[] Capture(Camera camera)
        {
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            float oldAspect = camera.aspect;
            Rect oldRect = camera.rect;
            rendering = true;
            try
            {
                // Keep the native projection/visible map bounds; crop to the card's 2:1 image.
                // Expanding the aspect could expose cells not submitted by MapDrawer this frame.
                if (oldAspect < 0.25f || oldAspect > 8f) throw new InvalidOperationException("Unsupported map camera aspect.");
                int width = Mathf.CeilToInt(Mathf.Max(512f, 256f * oldAspect));
                int height = Mathf.CeilToInt(width / oldAspect);
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = oldAspect;
                // Explicit map-camera rendering excludes the IMGUI/window overlays and does not move the camera.
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(512, 256, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect((width - 512) / 2, (height - 256) / 2, 512, 256), 0, 0, false);
                image.Apply(false, false);
                return image.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.rect = oldRect;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                if (image != null) UnityEngine.Object.Destroy(image);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                rendering = false;
            }
        }

        private static void Warn(Exception e) => Log.Warning("[Reframed UI] Optional save preview skipped: " + e.Message);
#endif
    }
}

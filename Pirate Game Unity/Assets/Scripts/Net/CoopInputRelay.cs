using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// ParrelSync shared input files so the host crank can apply every player's inputs
    /// without each wallet writing the delegated world account on MagicBlock ER.
    /// Each slot writes its own file to avoid cross-process lock contention on Windows.
    /// An in-process cache guarantees the crank always sees the latest local publish.
    /// </summary>
    public static class CoopInputRelay
    {
        public const int MaxSlots = 12;
        const string LegacySharedInputFileName = ".coop-input";
        const string SlotFilePrefix = ".coop-input-";
        const int IoRetryCount = 4;

        static readonly sbyte[] CachedThrottles = new sbyte[MaxSlots];
        static readonly sbyte[] CachedSteers = new sbyte[MaxSlots];

        public static void Publish(byte slot, sbyte throttle, sbyte steer)
        {
            if (slot >= MaxSlots) return;

            CachedThrottles[slot] = throttle;
            CachedSteers[slot] = steer;

            var path = SlotFilePath(slot);
            EnsureDirectory(path);
            WriteShared(path, FormatLine(slot, throttle, steer));
        }

        public static void ReadInto(sbyte[] throttles, sbyte[] steers, byte playerCount)
        {
            if (throttles == null || steers == null) return;

            var count = Math.Min(playerCount, (byte)Math.Min(MaxSlots, Math.Min(throttles.Length, steers.Length)));
            for (byte slot = 0; slot < count; slot++)
            {
                throttles[slot] = CachedThrottles[slot];
                steers[slot] = CachedSteers[slot];

                if (!TryReadSlot(slot, out var throttle, out var steer))
                    continue;

                throttles[slot] = throttle;
                steers[slot] = steer;
            }

            for (var slot = count; slot < throttles.Length; slot++)
                throttles[slot] = 0;
            for (var slot = count; slot < steers.Length; slot++)
                steers[slot] = 0;
        }

        public static void Clear()
        {
            Array.Clear(CachedThrottles, 0, CachedThrottles.Length);
            Array.Clear(CachedSteers, 0, CachedSteers.Length);

            for (byte slot = 0; slot < MaxSlots; slot++)
            {
                try
                {
                    var path = SlotFilePath(slot);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Coop] Failed to clear input relay slot={slot}: {ex.Message}");
                }
            }

            try
            {
                var legacy = LegacySharedInputFilePath;
                if (File.Exists(legacy))
                    File.Delete(legacy);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Coop] Failed to clear legacy input relay: {ex.Message}");
            }
        }

        static bool TryReadSlot(byte slot, out sbyte throttle, out sbyte steer)
        {
            throttle = 0;
            steer = 0;

            var path = SlotFilePath(slot);
            if (!File.Exists(path))
                return false;

            try
            {
                var line = ReadShared(path);
                if (string.IsNullOrWhiteSpace(line))
                    return false;

                var parts = line.Trim().Split(',');
                if (parts.Length < 3)
                    return false;
                if (!sbyte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out throttle))
                    return false;
                if (!sbyte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out steer))
                    return false;
                return true;
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Coop] Failed to read input relay slot={slot}: {ex.Message}");
                return false;
            }
        }

        static void WriteShared(string path, string contents)
        {
            for (var attempt = 0; attempt < IoRetryCount; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream, Encoding.UTF8);
                    writer.Write(contents);
                    return;
                }
                catch (IOException) when (attempt < IoRetryCount - 1)
                {
                    Thread.Sleep(1);
                }
                catch (IOException ex)
                {
                    Debug.LogWarning($"[Coop] Failed to write input relay: {ex.Message}");
                }
            }
        }

        static string ReadShared(string path)
        {
            for (var attempt = 0; attempt < IoRetryCount; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
                catch (IOException) when (attempt < IoRetryCount - 1)
                {
                    Thread.Sleep(1);
                }
            }

            return null;
        }

        static void EnsureDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        static string FormatLine(byte slot, sbyte throttle, sbyte steer) =>
            $"{slot},{throttle},{steer}";

        static string SlotFilePath(byte slot) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", $"{SlotFilePrefix}{slot}"));

        static string LegacySharedInputFilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", LegacySharedInputFileName));
    }
}

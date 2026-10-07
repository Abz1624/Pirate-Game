using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    public interface ISaveBackend
    {
        bool TryLoad(string key, out string json);
        void Save(string key, string json);
        void Delete(string key);
    }

    /// <summary>Deterministic in-memory backend for PlayMode tests (never touches PlayerPrefs).</summary>
    public sealed class MemorySaveBackend : ISaveBackend
    {
        readonly Dictionary<string, string> _store = new();

        public bool TryLoad(string key, out string json)
        {
            if (_store.TryGetValue(key, out json))
                return true;
            json = null;
            return false;
        }

        public void Save(string key, string json) => _store[key] = json ?? string.Empty;

        public void Delete(string key) => _store.Remove(key);
    }

    public sealed class PlayerPrefsSaveBackend : ISaveBackend
    {
        public bool TryLoad(string key, out string json)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                json = null;
                return false;
            }

            json = PlayerPrefs.GetString(key);
            return true;
        }

        public void Save(string key, string json)
        {
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public void Delete(string key) => PlayerPrefs.DeleteKey(key);
    }

    [Serializable]
    public class MetaSaveData
    {
        public int version = GameConstants.SaveVersion;
        public int doubloons;
        public int marks;
        public int completedQuests;
        public bool rerollUnlocked;
        public string selectedUnlockGoal = string.Empty;
        public string[] unlockedIds = Array.Empty<string>();
        public string[] purchasedIds = Array.Empty<string>();
        public string[] marksOwnedContentIds = Array.Empty<string>();
        public string[] settledRunReceiptIds = Array.Empty<string>();
        public int[] questProgress = Array.Empty<int>();
    }

    public sealed class SaveService
    {
        const string MetaKey = "highseas_meta_v1";
        readonly ISaveBackend _backend;

        public MetaSaveData Meta { get; private set; } = new();

        public SaveService(ISaveBackend backend = null)
        {
            _backend = backend ?? new PlayerPrefsSaveBackend();
            Load();
        }

        public void Load()
        {
            if (_backend.TryLoad(MetaKey, out var json))
            {
                var loaded = JsonUtility.FromJson<MetaSaveData>(json);
                Meta = Migrate(loaded);
            }
            else
            {
                Meta = new MetaSaveData();
            }
        }

        public void Save()
        {
            Meta.version = GameConstants.SaveVersion;
            KeepStoredOwnership();
            _backend.Save(MetaKey, JsonUtility.ToJson(Meta));
        }

        /// <summary>
        /// M16 (save safety): nothing in the game removes owned content or settled-run receipts, so a write that would
        /// drop any the stored save holds comes from a stale copy of the save (a player lost an unlock this way between
        /// two playtests). They are kept, and the writer is reported with its stack trace.
        /// </summary>
        void KeepStoredOwnership()
        {
            if (!_backend.TryLoad(MetaKey, out var json) || string.IsNullOrEmpty(json)) return;
            MetaSaveData stored;
            try { stored = Migrate(JsonUtility.FromJson<MetaSaveData>(json)); } // as Load reads it (a pre-v2 save has no owned ids)
            catch (ArgumentException) { return; }
            if (stored == null) return;
            var dropped = new List<string>();
            Meta.unlockedIds = KeepAll(stored.unlockedIds, Meta.unlockedIds, dropped);
            Meta.purchasedIds = KeepAll(stored.purchasedIds, Meta.purchasedIds, dropped);
            Meta.marksOwnedContentIds = KeepAll(stored.marksOwnedContentIds, Meta.marksOwnedContentIds, dropped);
            var receipts = new List<string>();
            Meta.settledRunReceiptIds = KeepAll(stored.settledRunReceiptIds, Meta.settledRunReceiptIds, receipts);
            if (dropped.Count > 0 || receipts.Count > 0)
                Debug.LogError($"SaveService: a save write would have dropped owned content [{string.Join(", ", dropped)}] and " +
                               $"{receipts.Count} settled-run receipt(s) held by the stored save; they were kept. Writer:\n{Environment.StackTrace}");
        }

        /// <summary>
        /// Writes a saved copy of the meta back (test and trace helpers restoring a snapshot) through the same guard, so a
        /// restore can bring back values but never remove owned content or receipts added since the snapshot.
        /// </summary>
        public static void RestoreSnapshot(string metaJson, ISaveBackend backend = null)
        {
            if (string.IsNullOrEmpty(metaJson)) return;
            try
            {
                var service = new SaveService(backend);
                service.Meta = Migrate(JsonUtility.FromJson<MetaSaveData>(metaJson));
                service.Save();
            }
            catch (ArgumentException e)
            {
                // A corrupt snapshot or stored save: leave the stored save as it is rather than stop the caller.
                Debug.LogError("SaveService: snapshot not restored (unreadable save data): " + e.Message);
            }
        }

        static string[] KeepAll(string[] stored, string[] current, List<string> dropped)
        {
            if (stored == null || stored.Length == 0) return current ?? Array.Empty<string>();
            var result = current;
            foreach (var id in stored)
            {
                if (string.IsNullOrEmpty(id) || ContainsId(result, id)) continue;
                dropped.Add(id);
                result = AddUnique(result, id);
            }
            return result ?? Array.Empty<string>();
        }

        public static MetaSaveData Migrate(MetaSaveData data)
        {
            if (data == null) return new MetaSaveData();
            data.unlockedIds ??= Array.Empty<string>();
            data.purchasedIds ??= Array.Empty<string>();
            data.questProgress ??= Array.Empty<int>();
            data.marksOwnedContentIds ??= Array.Empty<string>();
            data.settledRunReceiptIds ??= Array.Empty<string>();
            data.selectedUnlockGoal ??= string.Empty;

            if (data.version < 2)
            {
                data.marks = 0;
                data.selectedUnlockGoal = string.Empty;
                data.marksOwnedContentIds = Array.Empty<string>();
                data.settledRunReceiptIds = Array.Empty<string>();
                data.version = 2;
            }

            ResolveAliases(data); // V5 M8z (8 Oct, close)

            if (data.version >= GameConstants.SaveVersion)
                return data;

            data.version = GameConstants.SaveVersion;
            return data;
        }

        /// <summary>V5 M8z (8 Oct, close): owned ids and the unlock goal under their current ids (the config's content id
        /// aliases: a save that bought the Ramming Prow owns the Squid Ink Pots). Nothing is dropped: an old id becomes its
        /// new one.</summary>
        static void ResolveAliases(MetaSaveData data)
        {
            if (!ContentIdAliases.Any) return;
            data.unlockedIds = ContentIdAliases.ResolveAll(data.unlockedIds);
            data.purchasedIds = ContentIdAliases.ResolveAll(data.purchasedIds);
            data.marksOwnedContentIds = ContentIdAliases.ResolveAll(data.marksOwnedContentIds);
            data.selectedUnlockGoal = ContentIdAliases.Resolve(data.selectedUnlockGoal) ?? string.Empty;
        }

        /// <summary>V5 M8z (8 Oct, close): applies the aliases registered after this save loaded (the app's save loads before
        /// the config); written back on the next save.</summary>
        public void ApplyContentAliases()
        {
            if (Meta != null) ResolveAliases(Meta);
        }

        public bool TryPurchase(string id, int cost)
        {
            if (Meta.doubloons < cost) return false;
            Meta.doubloons -= cost;
            Save();
            return true;
        }

        public bool HasMarksOwned(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return false;
            return ContainsId(Meta.marksOwnedContentIds, contentId);
        }

        /// <summary>Marks ownership plus preserved legacy unlock/purchase arrays (Codex R4).</summary>
        public bool HasEffectiveContentOwned(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return false;
            contentId = ContentIdAliases.Resolve(contentId); // V5 M8z (8 Oct, close): an old id asks for its new one
            if (HasMarksOwned(contentId)) return true;
            if (ContainsId(Meta.unlockedIds, contentId)) return true;
            if (ContainsId(Meta.purchasedIds, contentId)) return true;

            // V5 M8z (8 Oct, close): data still under an old id (not yet resolved) owns its new one.
            foreach (var old in ContentIdAliases.OldIdsOf(contentId))
                if (HasMarksOwned(old) || ContainsId(Meta.unlockedIds, old) || ContainsId(Meta.purchasedIds, old)) return true;

            foreach (var alias in ContentOwnershipAliases(contentId))
            {
                if (HasMarksOwned(alias)) return true;
                if (ContainsId(Meta.unlockedIds, alias)) return true;
                if (ContainsId(Meta.purchasedIds, alias)) return true;
            }

            return false;
        }

        static IEnumerable<string> ContentOwnershipAliases(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) yield break;
            var lower = contentId.ToLowerInvariant();
            if (lower != contentId)
                yield return lower;
            if (contentId != lower)
                yield return contentId;
        }

        public bool TryPurchaseMarksUnlock(string contentId, int marksCost)
        {
            if (string.IsNullOrEmpty(contentId) || marksCost < 0) return false;
            if (HasEffectiveContentOwned(contentId)) return false;
            if (Meta.marks < marksCost) return false;

            Meta.marks -= marksCost;
            Meta.marksOwnedContentIds = AddUnique(Meta.marksOwnedContentIds, contentId);
            Save();
            return true;
        }

        public bool TrySettleRun(string runId, int marksAwarded)
        {
            if (string.IsNullOrEmpty(runId) || marksAwarded < 0)
                return false;

            if (ContainsId(Meta.settledRunReceiptIds, runId))
                return false;

            Meta.marks += marksAwarded;
            Meta.settledRunReceiptIds = AddUnique(Meta.settledRunReceiptIds, runId);
            Save();
            return true;
        }

        public void SetSelectedUnlockGoal(string contentId)
        {
            Meta.selectedUnlockGoal = contentId ?? string.Empty;
            Save();
        }

        static bool ContainsId(string[] ids, string id)
        {
            if (ids == null) return false;
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id) return true;
            }

            return false;
        }

        static string[] AddUnique(string[] ids, string id)
        {
            if (ContainsId(ids, id)) return ids;
            var list = ids != null ? new List<string>(ids) : new List<string>();
            list.Add(id);
            return list.ToArray();
        }
    }
}

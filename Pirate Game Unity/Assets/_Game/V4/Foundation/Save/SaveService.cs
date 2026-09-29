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
            _backend.Save(MetaKey, JsonUtility.ToJson(Meta));
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

            if (data.version >= GameConstants.SaveVersion)
                return data;

            data.version = GameConstants.SaveVersion;
            return data;
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
            if (HasMarksOwned(contentId)) return true;
            if (ContainsId(Meta.unlockedIds, contentId)) return true;
            if (ContainsId(Meta.purchasedIds, contentId)) return true;

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

using System;
using UnityEngine;

namespace HighSeas.Core
{
    public interface ISaveBackend
    {
        bool TryLoad(string key, out string json);
        void Save(string key, string json);
        void Delete(string key);
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
        public int completedQuests;
        public bool rerollUnlocked;
        public string[] unlockedIds = Array.Empty<string>();
        public string[] purchasedIds = Array.Empty<string>();
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

        static MetaSaveData Migrate(MetaSaveData data)
        {
            if (data == null) return new MetaSaveData();
            if (data.version >= GameConstants.SaveVersion) return data;
            data.version = GameConstants.SaveVersion;
            data.unlockedIds ??= Array.Empty<string>();
            data.purchasedIds ??= Array.Empty<string>();
            data.questProgress ??= Array.Empty<int>();
            return data;
        }

        public void AddDoubloons(int amount)
        {
            Meta.doubloons += amount;
            Save();
        }

        public bool TryPurchase(string id, int cost)
        {
            if (Meta.doubloons < cost) return false;
            Meta.doubloons -= cost;
            Save();
            return true;
        }
    }
}

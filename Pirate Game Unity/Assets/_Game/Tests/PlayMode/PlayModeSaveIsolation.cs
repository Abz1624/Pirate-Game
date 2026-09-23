using System;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Tests
{
    /// <summary>Isolates PlayMode tests from production PlayerPrefs (Codex F6).</summary>
    public sealed class PlayModeSaveIsolation : IDisposable
    {
        const string MetaKey = "highseas_meta_v1";

        readonly SaveService _restoredSave;
        readonly string _playerPrefsSnapshot;
        readonly bool _hadPlayerPrefs;

        PlayModeSaveIsolation(SaveService restoredSave, bool hadPlayerPrefs, string playerPrefsSnapshot)
        {
            _restoredSave = restoredSave;
            _hadPlayerPrefs = hadPlayerPrefs;
            _playerPrefsSnapshot = playerPrefsSnapshot;
        }

        public static PlayModeSaveIsolation Begin()
        {
            AppRoot.EnsureInitialized();
            var restored = AppRoot.Instance.Save;
            var hadKey = PlayerPrefs.HasKey(MetaKey);
            var snapshot = hadKey ? PlayerPrefs.GetString(MetaKey) : null;
            AppRoot.Instance.ReplaceSave(new SaveService(new MemorySaveBackend()));
            return new PlayModeSaveIsolation(restored, hadKey, snapshot);
        }

        public void Dispose()
        {
            if (AppRoot.Instance != null)
                AppRoot.Instance.ReplaceSave(_restoredSave);

            if (_hadPlayerPrefs)
            {
                var current = PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
                if (current != _playerPrefsSnapshot)
                {
                    PlayerPrefs.SetString(MetaKey, _playerPrefsSnapshot);
                    PlayerPrefs.Save();
                }
            }
        }

        public static string SnapshotProductionMetaJson() =>
            PlayerPrefs.HasKey(MetaKey) ? PlayerPrefs.GetString(MetaKey) : null;
    }
}

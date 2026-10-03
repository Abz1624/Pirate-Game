using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>One guest profile on this device. Registered once it has the API's token (the token is kept only here).</summary>
    [Serializable]
    public sealed class LocalProfile
    {
        public string localId;
        public string playerId;
        public string token;
        public string name;
        public string tag;
        public int rating;
        /// <summary>Renamed while offline (or not yet registered): the name is sent on the next online launch.</summary>
        public bool nameDirty;

        public bool Registered => !string.IsNullOrEmpty(token);
        /// <summary>"Anne Bonny#4821" (the name alone until the API gives the tag).</summary>
        public string Display => string.IsNullOrEmpty(tag) ? name : name + tag;
    }

    /// <summary>
    /// Playtest (3 Oct, owner): "guest profiles, no sign-in; give an option to edit the name so multiple players can use the
    /// same PC". The device's list of guest profiles and the active one, kept as JSON (PlayerPrefs in the game). Local
    /// only: the API calls are <see cref="OnlineService"/>'s.
    /// </summary>
    public sealed class ProfileStore
    {
        public const string PrefsKey = "rp.online.profiles";

        [Serializable]
        sealed class Saved
        {
            public List<LocalProfile> profiles = new List<LocalProfile>();
            public string activeLocalId;
        }

        readonly Func<string> _load;
        readonly Action<string> _save;
        Saved _data;

        public event Action Changed;

        /// <summary>A store over PlayerPrefs.</summary>
        public ProfileStore() : this(() => PlayerPrefs.GetString(PrefsKey, string.Empty), json => { PlayerPrefs.SetString(PrefsKey, json); PlayerPrefs.Save(); }) { }

        /// <summary>A store over any text storage (tests).</summary>
        public ProfileStore(Func<string> load, Action<string> save)
        {
            _load = load;
            _save = save;
            Reload();
        }

        public IReadOnlyList<LocalProfile> Profiles => _data.profiles;

        public LocalProfile Active
        {
            get
            {
                foreach (var p in _data.profiles) if (p.localId == _data.activeLocalId) return p;
                return _data.profiles.Count > 0 ? _data.profiles[0] : null;
            }
        }

        public void Reload()
        {
            Saved data = null;
            var json = _load?.Invoke();
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<Saved>(json); }
                catch (Exception e) { Debug.LogWarning("ProfileStore: unreadable profiles, starting fresh: " + e.Message); }
            }
            _data = data ?? new Saved();
            if (_data.profiles == null) _data.profiles = new List<LocalProfile>();
            _data.profiles.RemoveAll(p => p == null || string.IsNullOrEmpty(p.localId));
        }

        /// <summary>A new local profile (not registered yet), made active.</summary>
        public LocalProfile Add(string name)
        {
            var p = new LocalProfile { localId = Guid.NewGuid().ToString("N"), name = name, nameDirty = true };
            _data.profiles.Add(p);
            _data.activeLocalId = p.localId;
            Commit();
            return p;
        }

        public bool SetActive(string localId)
        {
            if (Find(localId) == null) return false;
            _data.activeLocalId = localId;
            Commit();
            return true;
        }

        /// <summary>Forgets a profile on this device (its token is dropped; the server keeps the profile). The next one
        /// becomes active.</summary>
        public bool Remove(string localId)
        {
            var p = Find(localId);
            if (p == null) return false;
            _data.profiles.Remove(p);
            if (_data.activeLocalId == localId) _data.activeLocalId = _data.profiles.Count > 0 ? _data.profiles[0].localId : null;
            Commit();
            return true;
        }

        /// <summary>A local rename; <paramref name="synced"/> false keeps it to send later.</summary>
        public void Rename(string localId, string name, bool synced)
        {
            var p = Find(localId);
            if (p == null) return;
            p.name = name;
            p.nameDirty = !synced;
            Commit();
        }

        /// <summary>What the API returned for a profile (registration or a /me read).</summary>
        public void Apply(string localId, string playerId, string token, string name, string tag, int? rating = null)
        {
            var p = Find(localId);
            if (p == null) return;
            if (!string.IsNullOrEmpty(playerId)) p.playerId = playerId;
            if (token != null) p.token = token;
            if (!string.IsNullOrEmpty(name)) p.name = name;
            if (tag != null) p.tag = tag;
            if (rating.HasValue) p.rating = rating.Value;
            Commit();
        }

        /// <summary>Drops the token (the server no longer knows it): the profile registers again on the next sync.</summary>
        public void Unregister(string localId)
        {
            var p = Find(localId);
            if (p == null) return;
            p.token = null;
            p.playerId = null;
            p.tag = null;
            p.nameDirty = true;
            Commit();
        }

        public LocalProfile Find(string localId)
        {
            if (string.IsNullOrEmpty(localId)) return null;
            foreach (var p in _data.profiles) if (p.localId == localId) return p;
            return null;
        }

        void Commit()
        {
            _save?.Invoke(JsonUtility.ToJson(_data));
            Changed?.Invoke();
        }
    }
}

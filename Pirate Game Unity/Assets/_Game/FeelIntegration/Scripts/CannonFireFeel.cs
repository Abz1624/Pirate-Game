using System.Collections.Generic;
using MoreMountains.Feedbacks;
using RoguePirates.V4;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Plays Feel feedbacks when the player's cannons fire. Listens to the weapon module's existing effects mirror
    /// (WeaponEffectsForwarder.Mirror, the same hook the 1v1 match uses, attached with += so nothing else is affected):
    /// 'f' = a weapon fired, 's' = a broadside salvo. No game code changed.
    /// Each weapon has its own strength (light swivels, heavy Long Nine). A hit on the ship takes priority: firing stays
    /// quiet for a moment after one. Throttled so fast guns and multi-barrel volleys don't stack (mobile friendly).
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Cannon Fire Feel")]
    public sealed class CannonFireFeel : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponProfile
        {
            [Tooltip("Weapon id, e.g. long_nine.")]
            public string WeaponId;
            [Tooltip("Feedback strength for this weapon (1 = full).")]
            [Range(0f, 1f)] public float Intensity = 1f;

            public WeaponProfile() { }
            public WeaponProfile(string id, float intensity) { WeaponId = id; Intensity = intensity; }
        }

        [Tooltip("Feel player for a cannon shot. Defaults to the MMF_Player on this object.")]
        public MMF_Player FireFeedback;
        [Tooltip("Optional Feel player for broadside salvos. Falls back to Fire Feedback when empty.")]
        public MMF_Player SalvoFeedback;

        [Tooltip("Which weapons count as cannons, and how strong each one feels. Weapons not listed play nothing.")]
        public List<WeaponProfile> Profiles = new List<WeaponProfile>
        {
            new WeaponProfile("swivel_guns", 0.35f),
            new WeaponProfile("deck_cannons", 0.6f),
            new WeaponProfile("chainshot", 0.6f),
            new WeaponProfile("mortar", 0.35f),
            new WeaponProfile("long_nine", 1f),
            new WeaponProfile("broadside_battery", 1f),
        };

        [Tooltip("Shortest time between two feedbacks (seconds). A stronger shot may still play inside it.")]
        public float MinInterval = 0.12f;
        [Tooltip("After a hit on the ship, firing feedback stays quiet this long (seconds) so the hit reads clearly.")]
        public float HitPriorityWindow = 0.3f;
        [Tooltip("How far out along the firing direction the feedback position is placed (drives the recoil side).")]
        public float SalvoSideOffset = 3f;

        readonly Dictionary<string, float> _intensity = new Dictionary<string, float>();
        WeaponEffectsForwarder _effects;
        V4Game _game;
        float _nextPlay, _lastIntensity, _nextSearch;

        void Awake()
        {
            if (FireFeedback == null) FireFeedback = GetComponent<MMF_Player>();
            RebuildProfiles();
            V4Game.RunStarted += OnRunStarted;
        }

        void OnValidate() => RebuildProfiles();

        void OnDestroy()
        {
            V4Game.RunStarted -= OnRunStarted;
            Unbind();
        }

        void OnDisable() => Unbind();

        void OnRunStarted(V4Game game) => _game = game;

        void RebuildProfiles()
        {
            _intensity.Clear();
            if (Profiles == null) return;
            foreach (var p in Profiles)
                if (p != null && !string.IsNullOrEmpty(p.WeaponId)) _intensity[p.WeaponId.Trim()] = p.Intensity;
        }

        void LateUpdate()
        {
            if (_game == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _game = FindFirstObjectByType<V4Game>();
            }

            var shell = _game != null ? _game.Shell : null;
            var effects = shell != null && shell.Weapons != null ? shell.Weapons.Effects : null;
            if (effects == _effects) return;
            Unbind();
            _effects = effects;
            if (_effects != null) _effects.Mirror += OnWeaponEffect;
        }

        void Unbind()
        {
            if (_effects != null) _effects.Mirror -= OnWeaponEffect;
            _effects = null;
        }

        void OnWeaponEffect(char kind, string weaponId, Vector3 point, Vector3 direction, float radius)
        {
            if (kind != 'f' && kind != 's') return;
            float intensity;
            if (weaponId == null || !_intensity.TryGetValue(weaponId, out intensity) || intensity <= 0f) return;
            if (FeelPriority.HitWithin(HitPriorityWindow)) return;
            var now = Time.unscaledTime;
            if (now < _nextPlay && intensity <= _lastIntensity) return;
            _nextPlay = now + MinInterval;
            _lastIntensity = intensity;

            var salvo = kind == 's';
            var player = salvo && SalvoFeedback != null ? SalvoFeedback : FireFeedback;
            if (player == null) return;
            // Place the feedback out along the firing direction, so the recoil goes away from where the shot went.
            var at = point + direction.normalized * SalvoSideOffset;
            player.PlayFeedbacks(at, intensity);
        }

        /// <summary>Editor/testing: play the fire feedback at full strength.</summary>
        [ContextMenu("Test Cannon Fire")]
        public void TestFire()
        {
            if (FireFeedback == null) FireFeedback = GetComponent<MMF_Player>();
            if (FireFeedback != null) FireFeedback.PlayFeedbacks(transform.position, 1f);
        }
    }
}

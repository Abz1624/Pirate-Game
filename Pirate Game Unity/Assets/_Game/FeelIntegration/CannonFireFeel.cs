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
    /// Throttled so fast-firing guns and multi-barrel volleys don't stack shakes (mobile friendly).
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Cannon Fire Feel")]
    public sealed class CannonFireFeel : MonoBehaviour
    {
        [Tooltip("Feel player for a single cannon shot. Defaults to the MMF_Player on this object.")]
        public MMF_Player FireFeedback;
        [Tooltip("Optional Feel player for broadside salvos. Falls back to Fire Feedback when empty.")]
        public MMF_Player SalvoFeedback;

        [Tooltip("Weapon ids that count as cannons.")]
        public string[] CannonWeaponIds =
        {
            "broadside_battery", "deck_cannons", "long_nine", "chainshot", "swivel_guns", "mortar"
        };

        [Tooltip("Shortest time between two feedbacks (seconds). Volleys fire several barrels in one frame.")]
        public float MinInterval = 0.12f;
        [Range(0f, 1f)] [Tooltip("Intensity of a single shot (a salvo plays at full).")]
        public float SingleShotIntensity = 0.6f;
        [Tooltip("How far out along the firing direction the feedback position is placed (drives the recoil lean side).")]
        public float SalvoSideOffset = 3f;

        readonly HashSet<string> _ids = new HashSet<string>();
        WeaponEffectsForwarder _effects;
        V4Game _game;
        float _nextPlay, _nextSearch;

        void Awake()
        {
            if (FireFeedback == null) FireFeedback = GetComponent<MMF_Player>();
            RebuildIds();
            V4Game.RunStarted += OnRunStarted;
        }

        void OnValidate() => RebuildIds();

        void OnDestroy()
        {
            V4Game.RunStarted -= OnRunStarted;
            Unbind();
        }

        void OnDisable() => Unbind();

        void OnRunStarted(V4Game game) => _game = game;

        void RebuildIds()
        {
            _ids.Clear();
            if (CannonWeaponIds == null) return;
            foreach (var id in CannonWeaponIds)
                if (!string.IsNullOrEmpty(id)) _ids.Add(id.Trim());
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
            if (weaponId == null || !_ids.Contains(weaponId)) return;
            if (Time.unscaledTime < _nextPlay) return;
            _nextPlay = Time.unscaledTime + MinInterval;

            var salvo = kind == 's';
            var player = salvo && SalvoFeedback != null ? SalvoFeedback : FireFeedback;
            if (player == null) return;
            // Place the feedback out along the firing direction, so the recoil lean goes away from where the shot went.
            var at = point + direction.normalized * SalvoSideOffset;
            player.PlayFeedbacks(at, salvo ? 1f : SingleShotIntensity);
        }

        /// <summary>Editor/testing: play the fire feedback as a single shot.</summary>
        [ContextMenu("Test Cannon Fire")]
        public void TestFire()
        {
            if (FireFeedback == null) FireFeedback = GetComponent<MMF_Player>();
            if (FireFeedback != null) FireFeedback.PlayFeedbacks(transform.position, SingleShotIntensity);
        }
    }
}

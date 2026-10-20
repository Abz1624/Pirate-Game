using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Plays a Feel MMF_Player when the player's ship takes a hit (DamageReceiver.Damaged, an existing event; no game
    /// code changed). Bigger hits (as a share of max hull) play stronger. Island bumps are skipped here because
    /// ShipImpactFeel already shakes for them. Throttled so many small hits don't turn into constant shaking.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Ship Damage Feel")]
    public sealed class ShipDamageFeel : MonoBehaviour
    {
        [Tooltip("Feel player for a hit on the ship. Defaults to the MMF_Player on this object.")]
        public MMF_Player DamageFeedback;

        [Header("Hit size (share of max hull) to intensity")]
        [Tooltip("Hits at or below this share of max hull play at Min Intensity.")]
        [Range(0f, 1f)] public float SmallHitShare = 0.02f;
        [Tooltip("Hits at or above this share of max hull play at full intensity.")]
        [Range(0f, 1f)] public float BigHitShare = 0.15f;
        [Range(0f, 1f)] public float MinIntensity = 0.5f;

        [Tooltip("Shortest time between two damage feedbacks (seconds).")]
        public float MinInterval = 0.15f;
        [Tooltip("Ignore damage this soon after an island bump (that bump has its own shake).")]
        public float IgnoreAfterIslandBump = 0.2f;

        PlayerShipController _ship;
        DamageReceiver _receiver;
        float _nextPlay, _lastIslandBump = -10f, _nextSearch;

        void Awake()
        {
            if (DamageFeedback == null) DamageFeedback = GetComponent<MMF_Player>();
        }

        void OnDisable() => Bind(null);

        void LateUpdate()
        {
            var followed = FollowedShip();
            if (followed != null)
            {
                if (followed != _ship) Bind(followed);
                return;
            }
            if (_ship != null || Time.unscaledTime < _nextSearch) return;
            _nextSearch = Time.unscaledTime + 0.5f;
            var found = FindFirstObjectByType<PlayerShipController>();
            if (found != null) Bind(found);
        }

        static PlayerShipController FollowedShip()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var follow = cam.GetComponent<FollowCamera>();
            return follow != null && follow.target != null ? follow.target.GetComponent<PlayerShipController>() : null;
        }

        void Bind(PlayerShipController ship)
        {
            if (_ship != null) _ship.IslandImpacted -= OnIslandBump;
            if (_receiver != null) _receiver.Damaged -= OnDamaged;
            _ship = ship;
            _receiver = ship != null ? ship.GetComponent<DamageReceiver>() : null;
            if (_ship != null) _ship.IslandImpacted += OnIslandBump;
            if (_receiver != null) _receiver.Damaged += OnDamaged;
        }

        void OnIslandBump(float _) => _lastIslandBump = Time.unscaledTime;

        void OnDamaged(float amount, bool isCrit)
        {
            if (amount <= 0f || DamageFeedback == null || _ship == null) return;
            var now = Time.unscaledTime;
            if (now - _lastIslandBump < IgnoreAfterIslandBump) return;
            if (now < _nextPlay) return;
            _nextPlay = now + MinInterval;

            var max = _receiver != null && _receiver.MaxHP > 0f ? _receiver.MaxHP : 100f;
            var t = Mathf.InverseLerp(SmallHitShare, BigHitShare, amount / max);
            var intensity = Mathf.Lerp(MinIntensity, 1f, t);
            if (isCrit) intensity = Mathf.Min(1f, intensity + 0.2f);
            DamageFeedback.PlayFeedbacks(_ship.transform.position, intensity);
        }

        /// <summary>Editor/testing: play the damage feedback at full intensity.</summary>
        [ContextMenu("Test Damage Shake")]
        public void TestDamage()
        {
            if (DamageFeedback == null) DamageFeedback = GetComponent<MMF_Player>();
            if (DamageFeedback != null) DamageFeedback.PlayFeedbacks(transform.position, 1f);
        }
    }
}

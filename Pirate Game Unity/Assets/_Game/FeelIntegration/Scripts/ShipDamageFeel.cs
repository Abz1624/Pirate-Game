using MoreMountains.Feedbacks;
using RoguePirates.V4;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Plays a Feel MMF_Player when the player's ship takes a hit (DamageReceiver.Damaged, an existing event; no game
    /// code changed). Bigger hits (as a share of max hull) play stronger. The feedback position is the hit's likely
    /// source (the nearest enemy), so the ship and camera recoil away from the side the hit came from.
    /// Island bumps are skipped (ShipImpactFeel shakes for them). Every hit marks FeelPriority, quieting firing feedback.
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

        [Header("Hit direction")]
        [Tooltip("Look for the enemy that likely dealt the hit within this distance (m). None found = no lean side.")]
        public float SourceSearchRange = 45f;

        [Tooltip("Shortest time between two damage feedbacks (seconds).")]
        public float MinInterval = 0.15f;
        [Tooltip("Ignore damage this soon after an island bump (that bump has its own shake).")]
        public float IgnoreAfterIslandBump = 0.2f;

        PlayerShipController _ship;
        DamageReceiver _receiver;
        V4Game _game;
        float _nextPlay, _lastIslandBump = -10f, _nextSearch;

        void Awake()
        {
            if (DamageFeedback == null) DamageFeedback = GetComponent<MMF_Player>();
            V4Game.RunStarted += OnRunStarted;
        }

        void OnDestroy() => V4Game.RunStarted -= OnRunStarted;

        void OnRunStarted(V4Game game) => _game = game;

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
            FeelPriority.MarkHit();
            if (now < _nextPlay) return;
            _nextPlay = now + MinInterval;

            var max = _receiver != null && _receiver.MaxHP > 0f ? _receiver.MaxHP : 100f;
            var t = Mathf.InverseLerp(SmallHitShare, BigHitShare, amount / max);
            var intensity = Mathf.Lerp(MinIntensity, 1f, t);
            if (isCrit) intensity = Mathf.Min(1f, intensity + 0.2f);
            DamageFeedback.PlayFeedbacks(HitSource(), intensity);
        }

        /// <summary>The nearest enemy's position (the likely source of the hit); the ship itself when there is none.</summary>
        Vector3 HitSource()
        {
            var center = _ship.transform.position;
            if (_game == null) _game = FindFirstObjectByType<V4Game>();
            var shell = _game != null ? _game.Shell : null;
            var enemies = shell != null && shell.Enemies != null ? shell.Enemies.Contracts as IEnemyQuery : null;
            int index;
            if (enemies != null && enemies.Exists && enemies.TryGetNearest(center, SourceSearchRange, out index, false))
            {
                var p = enemies.Position(index);
                p.y = center.y;
                return p;
            }
            return center;
        }

        /// <summary>Editor/testing: play the damage feedback at full intensity.</summary>
        [ContextMenu("Test Damage Shake")]
        public void TestDamage()
        {
            if (DamageFeedback == null) DamageFeedback = GetComponent<MMF_Player>();
            if (DamageFeedback != null) DamageFeedback.PlayFeedbacks(_ship != null ? HitSource() : transform.position, 1f);
        }
    }
}

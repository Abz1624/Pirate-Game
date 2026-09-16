using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Tick-based conditional item behaviors (doc §11).</summary>
    public sealed class V3RunItemEffects : MonoBehaviour
    {
        public static V3RunItemEffects Instance { get; private set; }

        public static V3RunItemEffects Ensure()
        {
            if (Instance != null) return Instance;
            var host = V3RunItemInventory.Instance != null
                ? V3RunItemInventory.Instance.gameObject
                : V3RunItemInventory.Ensure().gameObject;
            return host.GetComponent<V3RunItemEffects>() ?? host.AddComponent<V3RunItemEffects>();
        }

        const int BellKillThreshold = 12;
        const float HeadingChangeDegrees = 28f;
        const float GlovesBuffSeconds = 2.5f;
        const float LanternBuffSeconds = 2f;
        const float KeelSpeedThreshold = 4.2f;

        int _bellKillCounter;
        float _glovesTimer;
        float _lanternTimer;
        float _lastHeadingAngle;
        bool _headingInitialized;
        float _powderFuseCharge;
        float _distanceSinceLastHit;
        Vector3 _lastPos;

        public float ExplosionDamageMultiplier { get; private set; } = 1f;
        public float StatusDamageMultiplier { get; private set; } = 1f;
        public float ControlledTargetDamageMultiplier { get; private set; } = 1f;
        public int ExtraLightningJumps { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded)
                return;
            if (V3RunItemInventory.Instance == null) return;

            var dt = Time.deltaTime;
            _glovesTimer = Mathf.Max(0f, _glovesTimer - dt);
            _lanternTimer = Mathf.Max(0f, _lanternTimer - dt);

            TrackHeadingAndMovement(dt);
            RefreshDynamicMultipliers();
        }

        void TrackHeadingAndMovement(float dt)
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;

            var forward = player.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();
            var angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _lastHeadingAngle = angle;
            }
            else if (V3RunItemInventory.Instance.HasItem("gunners_gloves"))
            {
                var delta = Mathf.Abs(Mathf.DeltaAngle(_lastHeadingAngle, angle));
                if (delta >= HeadingChangeDegrees)
                {
                    _glovesTimer = GlovesBuffSeconds;
                    _lastHeadingAngle = angle;
                }
            }

            var pos = player.Position;
            if (_lastPos == Vector3.zero) _lastPos = pos;
            var moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_lastPos.x, 0f, _lastPos.z));
            _distanceSinceLastHit += moved;
            _lastPos = pos;

            if (V3RunItemInventory.Instance.HasItem("powder_fuse") && _distanceSinceLastHit > 18f)
            {
                _powderFuseCharge = 1f;
                _distanceSinceLastHit = 0f;
            }
        }

        void RefreshDynamicMultipliers()
        {
            ExplosionDamageMultiplier = 1f;
            StatusDamageMultiplier = 1f;
            ControlledTargetDamageMultiplier = 1f;
            ExtraLightningJumps = 0;

            if (V3RunItemInventory.Instance.HasItem("black_powder_keg"))
                ExplosionDamageMultiplier *= 1.12f;
            if (V3RunItemInventory.Instance.HasItem("sea_witchs_eye"))
                StatusDamageMultiplier *= 1.15f;
            if (V3RunItemInventory.Instance.HasItem("leviathan_tooth"))
                ControlledTargetDamageMultiplier *= 1.2f;
            if (V3RunItemInventory.Instance.HasItem("stormglass"))
                ExtraLightningJumps = 1;
            if (_powderFuseCharge > 0f)
                ExplosionDamageMultiplier *= 1.35f;
        }

        public void NotifyPlayerTookDamage()
        {
            _distanceSinceLastHit = 0f;
            _powderFuseCharge = 0f;
        }

        public void NotifyNearMissDodge()
        {
            if (V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.HasItem("ghost_lantern"))
                _lanternTimer = LanternBuffSeconds;
        }

        public void NotifyKill()
        {
            if (V3RunItemInventory.Instance == null || !V3RunItemInventory.Instance.HasItem("captains_bell"))
                return;
            _bellKillCounter++;
            if (_bellKillCounter < BellKillThreshold) return;
            _bellKillCounter = 0;
            FindFirstObjectByType<WeaponController>()?.CaptainFireAllWeapons();
        }

        public void NotifyExplosionResolved()
        {
            _powderFuseCharge = 0f;
        }

        public float GetIncomingDamageMultiplier(PlayerShipController player)
        {
            if (player == null || V3RunItemInventory.Instance == null) return 1f;
            if (!V3RunItemInventory.Instance.HasItem("reinforced_keel")) return 1f;
            var speed = new Vector3(player.Velocity.x, 0f, player.Velocity.z).magnitude;
            return speed >= KeelSpeedThreshold ? 0.88f : 1f;
        }

        public float GetAttackSpeedMultiplier()
        {
            if (_glovesTimer <= 0f) return 1f;
            return 1.12f;
        }

        public float GetCritChanceBonus()
        {
            if (_lanternTimer <= 0f) return 0f;
            return 0.08f;
        }

        public float GetGlobalDamageMultiplier(WeaponController weapons)
        {
            var mult = 1f;
            weapons ??= Object.FindFirstObjectByType<WeaponController>();
            if (V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.HasItem("admirals_ledger") && weapons != null)
            {
                var families = weapons.CountDistinctWeaponFamilies();
                mult *= 1f + Mathf.Min(0.18f, families * 0.03f);
            }

            if (V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.HasItem("golden_figurehead") && RunManager.Instance != null)
            {
                var gold = RunManager.Instance.Gold;
                mult *= 1f + Mathf.Min(0.15f, gold / 2000f);
            }

            return mult;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using HighSeas.Weapons;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{

    /// <summary>The player ship for weapons.</summary>
    [DeletedInPhase(14, "V4 ship snapshot")]
    public sealed class LegacyWeaponShip : IShipState
    {
        readonly Func<PlayerShipController> _ship;
        public LegacyWeaponShip(Func<PlayerShipController> ship) { _ship = ship; }

        PlayerShipController Ship => _ship?.Invoke();
        public bool Exists => Ship != null;
        public Vector3 Position => Ship.Position;
        public Vector3 Velocity => Ship.Velocity;
        public Vector3 MovementForward => Ship.MovementForward;
        public Vector3 TransformPoint(Vector3 local) => Ship.transform.TransformPoint(local);
    }

    /// <summary>
    /// Player stats (RunManager), captain passives and actives, and item effects, read at each use like V3's
    /// DamageSystem and WeaponController did. Multipliers of an absent controller are 1.
    /// </summary>
    [DeletedInPhase(9, "V4 stat resolver, captain and item modules")]
    public sealed class LegacyWeaponModifiers : IWeaponModifiers
    {
        static PlayerStats Stats => RunManager.Instance.Stats;
        public float AttackSpeed => Stats.AttackSpeed;
        public float ProjectileSpeed => Stats.ProjectileSpeed;
        public float Size => Stats.Size;
        public float Duration => Stats.Duration;
        public float Quantity => Stats.Get(StatId.Quantity);
        public float CritChance => Stats.CritChance;
        public float CritDamage => Stats.CritDamage;
        public float EliteDamage => Stats.EliteDamage;
        public float GlobalDamage => Stats.GlobalDamage;

        public float CaptainAdditiveDamage
        {
            get
            {
                var captain = V3CaptainController.Instance;
                var bonus = captain != null ? captain.GetAdditiveDamageBonus() : 0f;
                var active = V3CaptainActiveController.Instance;
                if (active != null) bonus += active.GetRichieActiveDamageBonus();
                return bonus;
            }
        }

        public float CaptainWeaponMultiplier(string weaponId)
        {
            var captain = V3CaptainController.Instance;
            return captain != null ? captain.GetWeaponDamageMultiplier(weaponId) : 1f;
        }

        public float ItemGlobalDamageMultiplier => V3RunItemEffects.Instance != null ? V3RunItemEffects.Instance.GetGlobalDamageMultiplier(null) : 1f;
        public float ItemCritChanceBonus => V3RunItemEffects.Instance != null ? V3RunItemEffects.Instance.GetCritChanceBonus() : 0f;
        public float ItemAttackSpeedMultiplier => V3RunItemEffects.Instance?.GetAttackSpeedMultiplier() ?? 1f;
        public float FlameRangeMultiplier => V3CaptainActiveController.Instance != null ? V3CaptainActiveController.Instance.FlameRangeMultiplier : 1f;
        public float BurnDamageMultiplier => V3CaptainActiveController.Instance != null ? V3CaptainActiveController.Instance.BurnDamageMultiplier : 1f;
        public float PowderKegBlastRadiusMultiplier => V3CaptainActiveController.Instance != null ? V3CaptainActiveController.Instance.PowderKegBlastRadiusMultiplier : 1f;
    }

    /// <summary>The run's combat random stream (RunManager.Rng, seeded per run).</summary>
    [DeletedInPhase(14, "V4 run random")]
    public sealed class LegacyCombatRandom : ICombatRandom
    {
        public float CombatFloat() => RunManager.Instance.Rng.CombatFloat();
    }

    /// <summary>Proc budget, the damage ledger, flame patches, and the ship-versus-ship hooks (inert in solo).</summary>
    [DeletedInPhase(12, "V4 kill path (M8), rewards and telemetry (M12)")]
    public sealed class LegacyWeaponHooks : IWeaponCombatHooks
    {
        readonly Func<PlayerShipController> _ship;
        readonly Func<string, WeaponDefinition> _definition;
        readonly RoguePirates.V4.Enemies.HellfirePatches _hellfire;

        public LegacyWeaponHooks(Func<PlayerShipController> ship, Func<string, WeaponDefinition> definition, RoguePirates.V4.Enemies.HellfirePatches hellfire)
        {
            _ship = ship;
            _definition = definition;
            _hellfire = hellfire;
        }

        public void BeginDamageResolution() => CombatProcRuntime.BeginWeaponDamageResolution();
        public bool TryRegisterProcEvent() => DamageProcPolicy.TryRegisterProcEvent();
        public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) => WeaponDamageLedger.RecordHit(weaponId, damage, isCrit, isElite, isBoss);
        public void RecordEmission(string weaponId) => WeaponDamageLedger.RecordEmission(weaponId);
        public void RecordConcurrentTargets(string weaponId, int count) => WeaponDamageLedger.RecordConcurrentTargets(weaponId, count);
        public void RecordControl(string weaponId, float seconds) => WeaponDamageLedger.RecordControl(weaponId, seconds);
        // M8: the V4 hellfire patches (V3's HellfirePatchWorld read V3's EnemyWorld, which left the V4 scenes).
        public void SpawnFlamePatch(Vector3 position, string weaponId) => _hellfire?.TrySpawnOnDirectFlameKill(position, weaponId);

        public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId) =>
            V3ShipCombatRegistry.ApplyRadiusDamage(_ship?.Invoke(), center, radius, damage, false, weaponId);

        public bool TryResolveShipAim(string weaponId, out Vector3 direction)
        {
            direction = Vector3.forward;
            var def = _definition?.Invoke(weaponId);
            var ship = _ship?.Invoke();
            if (def == null || ship == null) return false;
            return V3ShipCombatRegistry.TryResolveAim(ship, def, out _, out direction);
        }

        public bool ShipVersusShip => V3RunSession.AllowShipVsShipCombat;
        public bool IsPvP => V3RunSession.IsPvP;
        public bool AutoFireDisabled => V3PlaytestFlags.ShouldDisableShipAutoFire && V3RunContext.IsActive;
    }

    /// <summary>
    /// The switched-off WeaponController as the loadout record (register D3): the legacy level system still equips and
    /// upgrades weapons there until M9. V4 reads it every tick and writes its firing state back, so the HUD, the juice
    /// controller (orbit blades, flame stream) and the aim overlay keep showing live values.
    /// </summary>
    [DeletedInPhase(9, "V4 progression owns the loadout")]
    public sealed class LegacyWeaponRecord : IWeaponLoadout
    {
        readonly Func<WeaponController> _record;
        public LegacyWeaponRecord(Func<WeaponController> record) { _record = record; }

        IReadOnlyList<WeaponInstance> Weapons => _record?.Invoke()?.Weapons;

        public int Count => Weapons?.Count ?? 0;

        public LoadoutEntry Get(int index)
        {
            var w = Weapons[index];
            if (w?.def == null) return default;
            WeaponEvolutionRuntime.TryGetEvolutionId(w.def.id, out var evolutionId);
            return new LoadoutEntry
            {
                Key = w,
                WeaponId = w.def.id,
                EvolutionId = evolutionId,
                Level = w.level,
                DamageBonus = w.damageBonus,
                QuantityBonus = w.quantityBonus,
                CooldownMult = w.cooldownMult,
                SpeedMult = w.speedMult,
                SizeMult = w.sizeMult,
                DurationMult = w.durationMult,
                KnockbackMult = w.knockbackMult,
            };
        }

        public void Mirror(int index, in LoadoutMirror state)
        {
            var list = Weapons;
            if (list == null || index >= list.Count || list[index] == null) return;
            var w = list[index];
            w.cooldownTimer = state.CooldownTimer;
            w.swivelAwaitingSecond = state.AwaitingBurstShot;
            // V4's FlameStreamPresenter draws the stream; the juice controller's copy (1 particle a second) stays idle.
            w.flameStreamRemaining = 0f;
            w.flameRechargeRemaining = state.RechargeRemaining;
            w.flameTickTimer = state.StreamTickTimer;
        }
    }

    /// <summary>
    /// Captain actives and Captain's Bell. V3 fired them through the WeaponController, which is now a switched-off
    /// record with no owner, so its fire paths return at once (register D3). This probe notices an activation (the
    /// active's cooldown rising from zero) and Captain's Bell's twelfth kill, and V4 fires instead.
    /// </summary>
    [DeletedInPhase(9, "V4 captain and item modules")]
    public sealed class LegacyActiveProbe : IDisposable
    {
        const int BellKillThreshold = 12;
        static readonly FieldInfo CaptainField = typeof(V3CaptainActiveController).GetField("captainId", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly IWeaponManager _weapons;
        float _lastCooldown;
        int _bellKills;
        bool _bellPending;
        TriggerBus _bus;

        public LegacyActiveProbe(IWeaponManager weapons) { _weapons = weapons; }

        /// <summary>Once per frame, before the weapon tick (V3 fired inside the active controller's Update).</summary>
        public void Poll()
        {
            AttachBus();
            var active = V3CaptainActiveController.Instance;
            if (active == null) { _lastCooldown = 0f; return; }
            var cooldown = active.CooldownRemaining;
            // The cooldown only rises in TryActivate (it can expire and restart in one frame, so no "was zero" test).
            if (cooldown > _lastCooldown + 1e-4f) OnActivated(active);
            _lastCooldown = cooldown;
            if (_bellPending)
            {
                _bellPending = false;
                _weapons.FireAllOnce();
            }
        }

        void OnActivated(V3CaptainActiveController active)
        {
            var captain = CaptainField != null ? (V3CaptainId)CaptainField.GetValue(active) : V3CaptainId.Rook;
            switch (captain)
            {
                case V3CaptainId.Brine:
                case V3CaptainId.Richie:
                case V3CaptainId.Ember:
                    break; // no weapon effect (shield, speed, flame boost read through the modifiers)
                case V3CaptainId.Flint:
                    _weapons.ArmAllKegs();
                    break;
                case V3CaptainId.Roxie:
                    _weapons.FireLightningSalvo();
                    break;
                default:
                    _weapons.FireAllOnce();
                    break;
            }
        }

        void AttachBus()
        {
            if (_bus != null) return;
            _bus = TriggerBus.Instance;
            if (_bus != null) _bus.Kill += OnKill;
        }

        void OnKill(bool elite, bool boss, Vector3 position, string weaponId)
        {
            if (V3RunItemInventory.Instance == null || !V3RunItemInventory.Instance.HasItem("captains_bell")) return;
            _bellKills++;
            if (_bellKills < BellKillThreshold) return;
            _bellKills = 0;
            // V3 fired from NotifyKill after every kill subscriber had run; firing inside RaiseKill would nest the burst's
            // kills inside the outer kill for later subscribers. V4 fires on its next weapon tick (same or next frame).
            _bellPending = true;
        }

        public void Dispose()
        {
            if (_bus != null) _bus.Kill -= OnKill;
            _bus = null;
        }
    }
}

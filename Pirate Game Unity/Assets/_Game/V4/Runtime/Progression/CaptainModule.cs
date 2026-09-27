using System;
using HighSeas.Core;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>The ship as the Captain touches it (V3 found the PlayerShipController each time).</summary>
    public interface ICaptainShip
    {
        bool Exists { get; }
        /// <summary>Flat speed (x, z).</summary>
        float Speed { get; }
        /// <summary>V3 PlayerShipController.InitializeFromRun(hull).</summary>
        void InitializeHull(float baseHull);
        /// <summary>V3 PlayerHullSync.SyncMaxFromRunStats with the hull ratio kept (Hank's per-level resync).</summary>
        void SyncMaxHullKeepRatio();
        void ApplySlow(float multiplier, float seconds);
        void ApplyMoveEffect(float multiplier, float seconds);
    }

    /// <summary>What the Captain reads from the run.</summary>
    public interface ICaptainRun
    {
        bool Exists { get; }
        bool IsPaused { get; }
        bool IsRunEnded { get; }
        int Gold { get; }
        int PlayerLevel { get; }
    }

    /// <summary>
    /// The Captain (M9 step 9.5): port of V3's V3CaptainController (base hull and speed, passives, Running Fuse, Hank's
    /// per-level hull resync) and V3CaptainActiveController (actives with cooldowns and their timed effects), reading
    /// <see cref="CaptainConfig"/>. Actives only act when switched on (ExperimentConfig.captainActivesEnabled: V3 off,
    /// adopted decision on).
    /// </summary>
    public sealed class CaptainModule
    {
        readonly IStatModifierSink _stats;
        readonly ICaptainShip _ship;
        readonly ICaptainRun _run;
        readonly Func<bool> _activesEnabled;
        float _fastMoveTimer;
        bool _runningFuseActive;
        float _cooldown, _braceTimer, _kegRadiusTimer, _pickupTimer, _flameTimer, _nearMissTimer, _damageBuffTimer;

        public CaptainConfig Config { get; private set; }

        /// <summary>The active was used (telemetry and the "{name}!" toast).</summary>
        public event Action<CaptainConfig> ActiveUsed;
        public event Action FireAllWeapons;
        public event Action ArmPowderKegs;
        public event Action FireLightningSalvo;

        public CaptainModule(IStatModifierSink stats, ICaptainShip ship, ICaptainRun run, Func<bool> activesEnabled)
        {
            _stats = stats;
            _ship = ship;
            _run = run;
            _activesEnabled = activesEnabled;
        }

        CaptainActive A => Config != null ? Config.active : null;
        CaptainPassive P => Config != null ? Config.passive : null;

        public float CooldownRemaining => _cooldown;
        public float CooldownDuration => A != null ? A.cooldownSeconds : 0f;
        public bool IsReady => _cooldown <= 0f;

        /// <summary>V3 V3CaptainController.Initialize + ApplyBaseStats and the active controller's reset.</summary>
        public void BeginRun(CaptainConfig config)
        {
            Config = config;
            _fastMoveTimer = 0f;
            _runningFuseActive = false;
            _cooldown = _braceTimer = _kegRadiusTimer = _pickupTimer = _flameTimer = _nearMissTimer = _damageBuffTimer = 0f;
            if (config == null) return;
            _stats.SetBase((int)StatId.Hull, config.baseHull);
            _stats.SetBase((int)StatId.MoveSpeed, config.baseMoveSpeed);
            if (_ship != null && _ship.Exists)
            {
                _ship.InitializeHull(config.baseHull);
                OnLevelChanged();
            }
        }

        /// <summary>V3 OnLevelChanged → ApplyBrineArmor: Hank's max hull follows the run stats (ratio kept).</summary>
        public void OnLevelChanged()
        {
            if (P == null || P.armorPerLevel <= 0f) return;
            if (_ship == null || !_ship.Exists || !_run.Exists) return;
            _ship.SyncMaxHullKeepRatio(); // V3 also set the armor, which damage never reads (R-49)
        }

        /// <summary>V3 V3CaptainController.Update (Running Fuse) and V3CaptainActiveController.Update (timers).</summary>
        public void Tick(float dt, bool activatePressed)
        {
            if (Config == null || !_run.Exists) return;
            if (P.runningFuse && !_run.IsPaused && _ship != null && _ship.Exists)
            {
                if (_ship.Speed >= P.runningFuseSpeed) _fastMoveTimer += dt;
                else
                {
                    _fastMoveTimer = 0f;
                    _runningFuseActive = false;
                }
                if (!_runningFuseActive && _fastMoveTimer >= P.runningFuseSeconds) _runningFuseActive = true;
            }

            if (!_activesEnabled() || _run.IsPaused || _run.IsRunEnded) return;
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _braceTimer = Mathf.Max(0f, _braceTimer - dt);
            _kegRadiusTimer = Mathf.Max(0f, _kegRadiusTimer - dt);
            _pickupTimer = Mathf.Max(0f, _pickupTimer - dt);
            _flameTimer = Mathf.Max(0f, _flameTimer - dt);
            _nearMissTimer = Mathf.Max(0f, _nearMissTimer - dt);
            _damageBuffTimer = Mathf.Max(0f, _damageBuffTimer - dt);
            if (activatePressed) TryActivate();
        }

        /// <summary>V3 TryActivate.</summary>
        public bool TryActivate()
        {
            if (!_activesEnabled() || !IsReady || A == null) return false;
            var a = A;
            if (a.braceSeconds > 0f) _braceTimer = a.braceSeconds;
            if (a.slowSeconds > 0f) _ship?.ApplySlow(a.slowMultiplier, a.slowSeconds);
            if (a.armPowderKegs) ArmPowderKegs?.Invoke();
            if (a.kegRadiusSeconds > 0f) _kegRadiusTimer = a.kegRadiusSeconds;
            if (a.pickupRadiusSeconds > 0f) _pickupTimer = a.pickupRadiusSeconds;
            if (a.damageBuffSeconds > 0f) _damageBuffTimer = a.damageBuffSeconds;
            if (a.moveSeconds > 0f) _ship?.ApplyMoveEffect(a.moveMultiplier, a.moveSeconds);
            if (a.flameSeconds > 0f) _flameTimer = a.flameSeconds;
            if (a.nearMissWindowSeconds > 0f) _nearMissTimer = a.nearMissWindowSeconds;
            if (a.fireLightningSalvo) FireLightningSalvo?.Invoke();
            if (a.fireAllWeapons) FireAllWeapons?.Invoke();
            _cooldown = a.cooldownSeconds;
            ActiveUsed?.Invoke(Config);
            return true;
        }

        /// <summary>V3 NotifyNearMissDodge (Roxie; V3 never calls it).</summary>
        public void NotifyNearMiss()
        {
            if (A == null || _nearMissTimer <= 0f) return;
            _cooldown = Mathf.Max(0f, _cooldown - A.nearMissCooldownRefund);
        }

        // ---- Values the damage and weapon paths read -----------------------------------------------------------------

        static float PerLevel(int level, float perLevel, float cap) => Mathf.Min(cap, perLevel * Mathf.Max(0, level));

        /// <summary>V3 GetAdditiveDamageBonus + GetRichieActiveDamageBonus.</summary>
        public float AdditiveDamageBonus()
        {
            if (Config == null) return 0f;
            var p = P;
            var bonus = 0f;
            if (p.additiveDamageGoldDivisor > 0f) bonus = _run.Exists ? Mathf.Min(p.goldDamageCap, _run.Gold / p.additiveDamageGoldDivisor) : 0f;
            else if (p.additiveDamagePerLevel > 0f) bonus = PerLevel(_run.PlayerLevel, p.additiveDamagePerLevel, p.additiveDamageCap);
            if (_damageBuffTimer > 0f) bonus += A.damageBuffAdditive;
            return bonus;
        }

        /// <summary>V3 GetWeaponDamageMultiplier (Running Fuse).</summary>
        public float WeaponDamageMultiplier(string weaponId)
        {
            if (P == null || !P.runningFuse || weaponId != P.runningFuseWeaponId) return 1f;
            return _runningFuseActive ? P.runningFuseMultiplier : 1f;
        }

        /// <summary>Brace! and, with R-49's switch, Hank's armor (100 / (100 + armor), armor = per level up to the cap; V3 set it on the receiver, which damage never read).</summary>
        public float IncomingDamageMultiplier
        {
            get
            {
                var mult = _braceTimer > 0f ? A.braceIncomingDamageMultiplier : 1f;
                if (P != null && P.armorApplied && P.armorPerLevel > 0f && _run.Exists)
                    mult *= 100f / (100f + Mathf.Min(P.armorCap, P.armorPerLevel * Mathf.Max(0, _run.PlayerLevel)));
                return mult;
            }
        }

        /// <summary>R-49: Ember's passive burn ×1.12 (only with the switch; V3 never applied it).</summary>
        public float PassiveBurnMultiplier => P != null && P.elementMultipliersApplied ? P.burnDamageMultiplier : 1f;

        /// <summary>R-49: Roxie's passive lightning ×1.1 (only with the switch; V3 never applied it).</summary>
        public float LightningDamageMultiplier => P != null && P.elementMultipliersApplied ? P.lightningDamageMultiplier : 1f;
        public float PowderKegBlastRadiusMultiplier => _kegRadiusTimer > 0f ? A.kegRadiusMultiplier : 1f;
        public float FlameRangeMultiplier => _flameTimer > 0f ? A.flameRangeMultiplier : 1f;
        public float BurnDamageMultiplier => (_flameTimer > 0f ? A.burnDamageMultiplier : 1f) * PassiveBurnMultiplier;
        /// <summary>Cut and Run's pickup radius (V3 never applies it, R-49: only when pickupRadiusApplied).</summary>
        public float PickupRadiusMultiplier => _pickupTimer > 0f && A.pickupRadiusApplied ? A.pickupRadiusMultiplier : 1f;
    }
}

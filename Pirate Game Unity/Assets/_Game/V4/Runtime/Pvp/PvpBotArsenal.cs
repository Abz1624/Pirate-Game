using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Online;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Pvp
{
    /// <summary>
    /// Hackathon playtest (12 Oct, owner: "the AI captain was shooting only pink sphere volleys ... it should select weapons
    /// and logbooks from the weapon list that we already have in the game and nothing else"): the practice bot's guns are
    /// the game's own. Its loadout is its draft (the server's bot draft, else <see cref="DefaultWeapons"/> and
    /// <see cref="DefaultLogbooks"/>) kept to the weapon and Logbook definitions in the config set; the weapons fire
    /// through a second <see cref="WeaponManager"/> (the real behaviours, cooldowns, ranges, projectiles and effects) whose
    /// only target is the player's ship, and the Logbooks are the same stat modifiers a player's give. What a weapon deals
    /// to the ship is handed to <see cref="Hit"/> (the match sends it to the server as the bot's hit; the PvP rules apply
    /// there). Level-ups raise the next pick in draft order, as a player's auto level-ups do.
    /// </summary>
    public sealed class PvpBotArsenal : IEnemyQuery, IEnemyDamageSink, IShipState, IWeaponModifiers, IWeaponCombatHooks,
        ICombatRandom, IRunStatus
    {
        /// <summary>The bot's draft when the match names none (the server's practice bot draft).</summary>
        public static readonly string[] DefaultWeapons = { "deck_cannons", "mortar", "chainshot", "swivel_guns", "harpoon_gun", "long_nine" };
        public static readonly string[] DefaultLogbooks = { "gunnery_manual", "ironclad", "admiral", "carpenters_notes", "lookouts_glass", "oversized_shot" };

        readonly ConfigSetRuntime _config;
        readonly RunStream _rng;
        readonly PlayerStats _stats = new PlayerStats();
        readonly ProgressionLoadout _loadout;
        readonly PvpLevelOrder _order;
        readonly Dictionary<string, int> _logbookLevels = new Dictionary<string, int>();
        readonly List<string> _weaponIds = new List<string>();
        readonly List<string> _logbookIds = new List<string>();
        readonly ProgressionConfig _progression;

        /// <summary>The bot ship's pose (the match moves it).</summary>
        public Vector3 ShipPosition, ShipVelocity;
        public float ShipYaw;
        public bool ShipAlive = true;
        /// <summary>The player's ship, the weapons' only target.</summary>
        public Vector3 TargetPosition, TargetVelocity;
        public float TargetRadius = 1.6f;
        public bool TargetAlive;
        /// <summary>The match is on (started, not ended): the weapons fire only then.</summary>
        public bool Active;
        /// <summary>A weapon hit the ship: (raw damage, weapon id).</summary>
        public Action<float, string> Hit;
        /// <summary>A Hull Logbook raised the bot's hull by this much.</summary>
        public Action<float> HullAdded;

        public WeaponManager Weapons { get; }
        public IReadOnlyList<string> WeaponIds => _weaponIds;
        public IReadOnlyList<string> LogbookIds => _logbookIds;
        public IReadOnlyList<LoadoutWeapon> Loadout => _loadout.Weapons;
        public int LogbookLevel(string id) => _logbookLevels.TryGetValue(id, out var l) ? l : 0;

        public PvpBotArsenal(ConfigSetRuntime config, int seed, IReadOnlyList<string> weapons, IReadOnlyList<string> logbooks,
            IWeaponEffects effects, Func<IProjectileViews> projectileViews)
        {
            _config = config;
            _rng = new RunStream(seed ^ 0x5bd1e995);
            _progression = config?.Single<ProgressionConfig>();
            var weaponConfigs = new Dictionary<string, WeaponConfig>();
            var logbookConfigs = new Dictionary<string, LogbookConfig>();
            if (config != null)
            {
                foreach (var w in config.All<WeaponConfig>()) if (w != null && !string.IsNullOrEmpty(w.weaponId)) weaponConfigs[w.weaponId] = w;
                foreach (var l in config.All<LogbookConfig>()) if (l != null && !string.IsNullOrEmpty(l.logbookId)) logbookConfigs[l.logbookId] = l;
            }
            Pick(weapons, DefaultWeapons, id => weaponConfigs.TryGetValue(id, out var c) && Usable(c), _weaponIds);
            Pick(logbooks, DefaultLogbooks, logbookConfigs.ContainsKey, _logbookIds);
            _logbookConfigs = logbookConfigs;

            _loadout = new ProgressionLoadout(id => weaponConfigs.TryGetValue(id, out var c) ? c : null, null);
            _loadout.EnsureSlotCapacity(Mathf.Max(1, _weaponIds.Count));
            _loadout.Rebuild(_weaponIds);
            foreach (var id in _logbookIds) RaiseLogbook(id);
            _order = new PvpLevelOrder(_weaponIds, _logbookIds);

            var combat = new WeaponCombat
            {
                Enemies = this, Damage = this, Ship = this, Mods = this, Hooks = this, Effects = effects, Random = this,
                Calculator = new DamageCalculator(this, this),
                Targeting = new WeaponTargetingService(this, this),
                Projectiles = new PlayerProjectiles(64, this, this, this, effects) { Views = projectileViews ?? (() => null) },
                Rules = () => { if (_config != null) foreach (var r in _config.All<WeaponRulesConfig>()) return r; return null; },
            };
            Weapons = new WeaponManager(combat, _loadout, this, config);
        }

        readonly Dictionary<string, LogbookConfig> _logbookConfigs;

        /// <summary>A weapon the bot can carry: switched on, and drawn through the effects stream and the projectile views
        /// (orbit blades are drawn from the player's own weapons only; so are chained maces, so a bot Anchor Flail would hit
        /// with nothing drawn: left out too, 13 Oct review).</summary>
        public static bool Usable(WeaponConfig c) =>
            c != null && RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(c.weaponId)
            && c.behavior != ConfigEnums.WeaponBehavior.Orbit && c.behavior != ConfigEnums.WeaponBehavior.ChainedMace;

        static void Pick(IReadOnlyList<string> wanted, string[] fallback, Func<string, bool> known, List<string> into)
        {
            if (wanted != null) foreach (var id in wanted) if (!string.IsNullOrEmpty(id) && known(id) && !into.Contains(id)) into.Add(id);
            if (into.Count > 0) return;
            foreach (var id in fallback) if (known(id) && !into.Contains(id)) into.Add(id);
        }

        /// <summary>One frame: the weapons and their projectiles (only while the match is on and the bot afloat).</summary>
        public void Tick(float dt)
        {
            if (!Active || !ShipAlive)
            {
                // the match ended or the bot sank: nothing of its stays in the air
                if (_wasActive) Weapons.ClearInFlight(false);
                _wasActive = false;
                return;
            }
            _wasActive = true;
            ElapsedSeconds += dt;
            Weapons.TickOrbitPositions(ElapsedSeconds);
            Weapons.Tick(dt);
            Weapons.Projectiles.Tick(dt);
        }

        bool _wasActive;

        /// <summary>The match is torn down: in-flight state goes without effects.</summary>
        public void Dispose() => Weapons.ClearInFlight(false);

        /// <summary>A level-up: the next pick in draft order (a weapon's upgrade or a Logbook rank) that is not maxed.</summary>
        public string LevelUp()
        {
            var pick = _order.Next(p => IsMaxed(p.IsWeapon, p.Id));
            if (pick == null) return null;
            if (pick.Value.IsWeapon) RaiseWeapon(pick.Value.Id);
            else RaiseLogbook(pick.Value.Id);
            return pick.Value.Id;
        }

        bool IsMaxed(bool isWeapon, string id)
        {
            if (isWeapon)
            {
                var i = _loadout.IndexOf(id);
                return i < 0 || _loadout.Weapons[i].Level >= Mathf.Max(1, _loadout.Weapons[i].Config.maxLevel);
            }
            return !_logbookConfigs.TryGetValue(id, out var def) || LogbookLevel(id) >= Mathf.Max(1, def.maxLevel);
        }

        /// <summary>A weapon level: one upgrade line at the common rarity, the weapon's axes taken in turn.</summary>
        void RaiseWeapon(string id)
        {
            var i = _loadout.IndexOf(id);
            if (i < 0) return;
            var w = _loadout.Weapons[i];
            var axes = w.Config.upgradeAxes ?? Array.Empty<WeaponUpgradeAxis>();
            var lines = Array.Empty<UpgradeLine>();
            if (axes.Length > 0)
            {
                var axis = axes[(w.Level - 1) % axes.Length];
                var rarity = _progression != null && _progression.rarityMultipliers != null && _progression.rarityMultipliers.Length > 0 ? _progression.rarityMultipliers[0] : 1f;
                var power = _progression != null ? _progression.upgradePowerMultiplier : 1f;
                var value = axis.axis == ConfigEnums.UpgradeAxis.Quantity ? Mathf.Max(1f, Mathf.Round(axis.baseIncrement * rarity)) : axis.baseIncrement * rarity * power;
                lines = new[] { new UpgradeLine(axis.axis, value) };
            }
            _loadout.ApplyUpgradePackage(i, lines);
        }

        void RaiseLogbook(string id)
        {
            if (!_logbookConfigs.TryGetValue(id, out var book)) return;
            var rarity = _progression != null && _progression.rarityMultipliers != null && _progression.rarityMultipliers.Length > 0 ? _progression.rarityMultipliers[0] : 1f;
            _logbookLevels[id] = LogbookLevel(id) + 1;
            var def = book.randomStat ? RollRandomStat(book) : book; // Hackathon (13 Oct): a Chaos rank rolls its stat
            if (def == null || def.stat < 0 || def.stat > (int)StatId.MoveSpeed) return;
            _stats.AddModifier(new StatModifier((StatId)def.stat, def.perLevelAdditive * rarity, def.perLevelMultiplicative));
            if (def.stat != (int)StatId.Hull || def.perLevelAdditive <= 0f) return;
            HullGained += def.perLevelAdditive * rarity;
            HullAdded?.Invoke(def.perLevelAdditive * rarity);
        }

        /// <summary>Hackathon (13 Oct): the Chaos Logbook's rank, one of its listed Logbooks on the bot's own stream.</summary>
        LogbookConfig RollRandomStat(LogbookConfig chaos)
        {
            var candidates = new List<LogbookConfig>();
            foreach (var from in chaos.randomStatFrom ?? Array.Empty<string>())
                if (_logbookConfigs.TryGetValue(from, out var l) && !l.randomStat && l.stat >= 0 && !candidates.Contains(l)) candidates.Add(l);
            return candidates.Count == 0 ? null : candidates[Mathf.Min(candidates.Count - 1, (int)(_rng.NextDouble() * candidates.Count))];
        }

        /// <summary>Hull its Hull Logbooks added so far (the starting ranks are in it before the match hooks HullAdded).</summary>
        public float HullGained { get; private set; }

        /// <summary>The bot's stat (tests).</summary>
        public float Stat(StatId stat) => _stats.Get(stat);

        // ---- IEnemyQuery / IEnemyDamageSink: the player's ship is the only target (index 0) ----

        bool IEnemyQuery.Exists => TargetAlive;
        public int Capacity => 1;
        public bool IsActive(int index) => index == 0 && TargetAlive;
        public Vector3 Position(int index) => TargetPosition;
        public Vector3 Velocity(int index) => TargetVelocity;
        public float Radius(int index) => TargetRadius;
        public float Hp(int index) => 1f;
        public bool IsElite(int index) => false;
        public bool IsBoss(int index) => false;
        /// <summary>13 Oct (owner): the player's ship is the bot's rival captain, which Big Game Hunter (the admiral Logbook
        /// in the bot's default draft) counts like a boss; it did nothing before.</summary>
        public bool IsEliteDamageTarget(int index) => index == 0 && TargetAlive;

        public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false)
        {
            index = -1;
            if (!TargetAlive) return false;
            var d = TargetPosition - from;
            d.y = 0f;
            if (d.magnitude > range + TargetRadius) return false;
            index = 0;
            return true;
        }

        public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null)
        {
            if (index != 0 || amount <= 0f || !Active) return;
            Hit?.Invoke(amount, weaponId);
        }

        public void ApplyStatus(int index, WeaponStatus status, float strength) { }

        // ---- IShipState: the bot's ship ----

        bool IShipState.Exists => ShipAlive;
        Vector3 IShipState.Position => ShipPosition;
        Vector3 IShipState.Velocity => ShipVelocity;
        public Vector3 MovementForward => PvpRules.Forward(ShipYaw);
        public Vector3 TransformPoint(Vector3 local) => ShipPosition + Quaternion.Euler(0f, ShipYaw, 0f) * local;

        // ---- IWeaponModifiers: the bot's stats (its Logbooks) ----

        public float AttackSpeed => _stats.AttackSpeed;
        public float ProjectileSpeed => _stats.ProjectileSpeed;
        public float Size => _stats.Size;
        public float Duration => _stats.Duration;
        public float Quantity => _stats.Get(StatId.Quantity);
        public float CritChance => _stats.CritChance;
        public float CritDamage => _stats.CritDamage;
        public float EliteDamage => _stats.EliteDamage;
        public float GlobalDamage => _stats.GlobalDamage;
        public float CaptainAdditiveDamage => 0f;
        public float CaptainWeaponMultiplier(string weaponId) => 1f;
        public float ItemGlobalDamageMultiplier => 1f;
        public float ItemCritChanceBonus => 0f;
        public float ItemAttackSpeedMultiplier => 1f;
        public float FlameRangeMultiplier => 1f;
        public float BurnDamageMultiplier => 1f;
        public float PowderKegBlastRadiusMultiplier => 1f;
        public float ExplosionDamageMultiplier => 1f;
        public void ExplosionResolved() { }
        public int ExtraLightningJumps => 0;

        // ---- IWeaponCombatHooks ----

        int _procs;
        public void BeginDamageResolution() => _procs = 0;
        public bool TryRegisterProcEvent() => ++_procs <= 12;
        public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss) { }
        public void RecordEmission(string weaponId) { }
        public void RecordConcurrentTargets(string weaponId, int count) { }
        public void RecordControl(string weaponId, float seconds) { }
        public void SpawnFlamePatch(Vector3 position, string weaponId) { }

        public void ApplyShipRadiusDamage(Vector3 center, float radius, float damage, string weaponId)
        {
            if (!TargetAlive || damage <= 0f) return;
            var d = TargetPosition - center;
            d.y = 0f;
            if (d.magnitude <= radius + TargetRadius) ApplyDamage(0, damage, false, weaponId: weaponId);
        }

        public bool TryResolveShipAim(string weaponId, out Vector3 direction)
        {
            direction = TargetPosition - ShipPosition;
            direction.y = 0f;
            if (!TargetAlive || direction.sqrMagnitude < 0.01f) { direction = MovementForward; return false; }
            direction.Normalize();
            return true;
        }

        public bool ShipVersusShip => true;
        public bool IsPvP => true;
        public bool AutoFireDisabled => false;

        // ---- ICombatRandom / IRunStatus ----

        public float CombatFloat() => (float)_rng.NextDouble();

        bool IRunStatus.Exists => true;
        public float ElapsedSeconds { get; set; }
        public bool IsPaused => false;
        public bool IsRunEnded => !Active;
        public bool IsGameplayActive => Active;
        public bool IsBossFight => false;
        public int PlayerLevel => 1;
        public int Kills => 0;
    }

    /// <summary>The bot's weapon effects: everything the player's weapon presenter draws, except the aim overlay (that is
    /// the player's own weapons' aim).</summary>
    public sealed class PvpBotEffects : IWeaponEffects
    {
        readonly Func<IWeaponEffects> _target;
        public PvpBotEffects(Func<IWeaponEffects> target) { _target = target; }

        IWeaponEffects T
        {
            get
            {
                var t = _target?.Invoke();
                return t is UnityEngine.Object o && o == null ? null : t;
            }
        }

        public void Fired(string weaponId, Vector3 origin, Vector3 direction) => T?.Fired(weaponId, origin, direction);
        public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget) { }
        public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth) => T?.Beam(origin, direction, range, halfWidth);
        public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection) => T?.SideSalvo(weaponId, shipPosition, sideDirection);
        public int MineDropped(Vector3 position) => T?.MineDropped(position) ?? -1;
        public void MineRemoved(int handle) => T?.MineRemoved(handle);
        public void MineSunk(int handle, float seconds) => T?.MineSunk(handle, seconds);
        public void Explosion(Vector3 position, float radius) => T?.Explosion(position, radius);
        public void LightningStrike(Vector3 position, float radius) => T?.LightningStrike(position, radius);
        public void FlameBurst(Vector3 origin, Vector3 aim) => T?.FlameBurst(origin, aim);
        public void AreaTelegraph(Vector3 position, float radius) => T?.AreaTelegraph(position, radius);
        public void TimedRing(Vector3 position, float radius, float seconds) => T?.TimedRing(position, radius, seconds);
        public void AreaImpact(Vector3 position, float radius) => T?.AreaImpact(position, radius);
        public void AuraPulse(Vector3 center, float radius) => T?.AuraPulse(center, radius);
        public void WeaponPulse(string weaponId, Vector3 center, float radius) => T?.WeaponPulse(weaponId, center, radius);
        public void BurningPatch(Vector3 position, float radius) => T?.BurningPatch(position, radius);
        public void WaterSplash(Vector3 position) => T?.WaterSplash(position);
        public int SkiffSpawned(Vector3 position, Vector3 forward) => T?.SkiffSpawned(position, forward) ?? -1;
        public int SummonSpawned(string weaponId, Vector3 position, Vector3 forward) => T?.SummonSpawned(weaponId, position, forward) ?? -1;
        public void SkiffMoved(int handle, Vector3 position, Vector3 facing) => T?.SkiffMoved(handle, position, facing);
        public void SkiffRemoved(int handle) => T?.SkiffRemoved(handle);
    }
}

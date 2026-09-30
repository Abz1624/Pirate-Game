using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>How an enemy hurts the ship in the simulator.</summary>
    public enum SimAttack { Contact, Ranged, Suicide, Area, Boss, None }

    /// <summary>One enemy kind as the simulator sees it (archetype, with a variant's overrides).</summary>
    public sealed class SimEnemyDef
    {
        public string Id;
        public float BaseHp, BaseDamage, Speed, Cooldown;
        public int Xp, Gold, Shots = 1;
        public SimAttack Attack;
        public bool IsBoss;
        public float Radius;
        /// <summary>Where a ranged enemy stays (m).</summary>
        public float PreferredRange;
        /// <summary>Its melee hits are telegraphed rams or charges (dodged with dodge skill, not contact avoidance).</summary>
        public bool Telegraphed;
    }

    /// <summary>
    /// V5 M2: the configs one simulated run reads (a <see cref="ConfigSetRuntime"/>: the same copies a run uses, so the
    /// Tuning Console's live edits and the tests' changes reach the simulator), with lookups by id.
    /// </summary>
    public sealed class SimContent
    {
        readonly Dictionary<string, WeaponConfig> _weapons = new Dictionary<string, WeaponConfig>();
        readonly Dictionary<string, LogbookConfig> _logbooks = new Dictionary<string, LogbookConfig>();
        readonly Dictionary<string, ItemConfig> _items = new Dictionary<string, ItemConfig>();
        readonly Dictionary<string, WeaponEvolutionConfig> _evolutions = new Dictionary<string, WeaponEvolutionConfig>();
        readonly Dictionary<string, WeaponEvolutionConfig> _evolutionByWeapon = new Dictionary<string, WeaponEvolutionConfig>();
        readonly Dictionary<string, SimEnemyDef> _enemies = new Dictionary<string, SimEnemyDef>();

        public ConfigSetRuntime Config { get; }
        /// <summary>The config set's hash when this content was made (read on the main thread: the hash reads object names,
        /// so batches can then run on a worker thread).</summary>
        public string ConfigHash { get; }
        public ProgressionConfig Progression { get; }
        public OfferWeightsConfig OfferWeights { get; }
        public WaveTimelineConfig Timeline { get; }
        public DifficultyCurveConfig Curve { get; }
        public DirectorConfig Director { get; }
        public ExperimentConfig Experiment { get; }
        public StageContentConfig Stage { get; }
        public PickupConfig Pickups { get; }
        /// <summary>V5: the run config (the ship speed factor).</summary>
        public RunConfig Run { get; }
        /// <summary>V5: the ship's top speed and acceleration factor (RunConfig.shipSpeedMultiplier).</summary>
        public float ShipSpeedMultiplier => Run != null && Run.shipSpeedMultiplier > 0f ? Run.shipSpeedMultiplier : 1f;
        /// <summary>V5: the levelling pace (PickupConfig.xpPickupMultiplier).</summary>
        public float XpPickupMultiplier => Pickups != null ? Pickups.xpPickupMultiplier : 1f;
        public PowerupConfig Powerups { get; }
        public RewardConfig Rewards { get; }
        public UnlockConfig Unlocks { get; }
        public BossConfig Boss { get; }
        public BalanceTargetsConfig Targets { get; }
        public PlayerPersonalityConfig Personalities { get; }
        public IReadOnlyList<CaptainConfig> Captains { get; }
        public List<WeaponConfig> WeaponsInOrder { get; }
        public List<LogbookConfig> LogbooksInOrder { get; }
        public List<WeaponEvolutionConfig> Evolutions { get; }
        public List<ItemConfig> ChestPool { get; }
        /// <summary>Director mode for this batch (null = the config's).</summary>
        public DirectorMode? DirectorModeOverride { get; set; }
        /// <summary>Every weapon is unlocked (a regular player); off = the starter weapons only (a fresh save).</summary>
        public bool AllWeaponsUnlocked { get; set; } = true;

        public SimContent(ConfigSetRuntime config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            ConfigHash = config.Hash;
            Progression = config.Single<ProgressionConfig>();
            OfferWeights = config.Single<OfferWeightsConfig>();
            Timeline = config.Single<WaveTimelineConfig>();
            Curve = config.Single<DifficultyCurveConfig>();
            Director = config.Single<DirectorConfig>();
            Experiment = config.Single<ExperimentConfig>();
            Stage = config.Single<StageContentConfig>();
            Pickups = config.Single<PickupConfig>();
            Run = config.Single<RunConfig>();
            Powerups = config.Single<PowerupConfig>();
            Rewards = config.Single<RewardConfig>();
            Unlocks = config.Single<UnlockConfig>();
            Boss = config.Single<BossConfig>();
            Targets = config.Single<BalanceTargetsConfig>();
            Personalities = config.Single<PlayerPersonalityConfig>();
            Captains = config.All<CaptainConfig>().OrderBy(c => c.menuOrder).ToList();
            WeaponsInOrder = OfferGenerator.InDatabaseOrder(config.All<WeaponConfig>());
            LogbooksInOrder = OfferGenerator.InDatabaseOrder(config.All<LogbookConfig>());
            Evolutions = config.All<WeaponEvolutionConfig>().ToList();
            ChestPool = config.All<ItemConfig>().Where(i => i.inChestPool).OrderBy(i => i.chestPoolOrder).ToList();
            foreach (var w in config.All<WeaponConfig>()) if (!string.IsNullOrEmpty(w.weaponId)) _weapons[w.weaponId] = w;
            foreach (var l in config.All<LogbookConfig>()) if (!string.IsNullOrEmpty(l.logbookId)) _logbooks[l.logbookId] = l;
            foreach (var i in config.All<ItemConfig>()) if (!string.IsNullOrEmpty(i.itemId)) _items[i.itemId] = i;
            foreach (var e in Evolutions)
            {
                if (string.IsNullOrEmpty(e.evolutionId)) continue;
                _evolutions[e.evolutionId] = e;
                if (e.baseWeapon != null && !string.IsNullOrEmpty(e.baseWeapon.weaponId)) _evolutionByWeapon[e.baseWeapon.weaponId] = e;
            }
            foreach (var a in config.All<EnemyArchetypeConfig>()) if (!string.IsNullOrEmpty(a.enemyId)) _enemies[a.enemyId] = Def(a, null);
            foreach (var v in config.All<EnemyVariantConfig>())
                if (!string.IsNullOrEmpty(v.variantId) && v.baseArchetype != null) _enemies[v.variantId] = Def(v.baseArchetype, v);
        }

        /// <summary>
        /// Enemies with at most this base HP count as fodder for the time-to-kill target: the first band's ordinary pool's
        /// toughest enemy (the stage's opening swarm).
        /// </summary>
        public float FodderMaxBaseHp
        {
            get
            {
                var band = Timeline?.phases.Where(p => p.ratePerSecond > 0f && p.pools.Count > 0).OrderBy(p => p.trigger.timeSeconds).FirstOrDefault();
                var defs = band?.pools.SelectMany(p => p.entries).Select(Enemy).Where(d => d != null).ToList();
                return defs != null && defs.Count > 0 ? defs.Max(d => d.BaseHp) : 10f;
            }
        }

        public bool EvolutionsEnabled => Experiment == null || Experiment.weaponEvolutionsEnabled;
        public bool CaptainActivesEnabled => Experiment != null && Experiment.captainActivesEnabled;
        public int WeaponSlotCap => Experiment != null ? Experiment.weaponSlotCap : Progression.weaponSlots;
        public DirectorMode Mode => DirectorModeOverride ?? (Director != null ? Director.mode : DirectorMode.Off);

        public WeaponConfig Weapon(string id) => id != null && _weapons.TryGetValue(id, out var w) ? w : null;
        public LogbookConfig Logbook(string id) => id != null && _logbooks.TryGetValue(id, out var l) ? l : null;
        public ItemConfig Item(string id) => id != null && _items.TryGetValue(id, out var i) ? i : null;
        public WeaponEvolutionConfig Evolution(string id) => id != null && _evolutions.TryGetValue(id, out var e) ? e : null;
        public WeaponEvolutionConfig EvolutionFor(string weaponId) => weaponId != null && _evolutionByWeapon.TryGetValue(weaponId, out var e) ? e : null;
        public SimEnemyDef Enemy(string id) => id != null && _enemies.TryGetValue(id, out var d) ? d : null;
        public bool IsLightningWeapon(string id) => Weapon(id)?.behavior == ConfigEnums.WeaponBehavior.AreaStrike;

        public bool IsWeaponAvailable(string weaponId)
        {
            if (Weapon(weaponId) == null) return false;
            if (AllWeaponsUnlocked) return true;
            return Unlocks != null && Array.IndexOf(Unlocks.starterWeaponIds, weaponId) >= 0;
        }

        public CaptainConfig Captain(string key) => Captains.FirstOrDefault(c => c.captainKey == key) ?? Captains.FirstOrDefault();

        /// <summary>A regular player starts with the Captain's weapon; a fresh save with the First Conquest weapon.</summary>
        public string StartingWeapon(CaptainConfig captain)
        {
            if (AllWeaponsUnlocked && captain != null && Weapon(captain.startingWeaponId) != null) return captain.startingWeaponId;
            return Unlocks != null && Weapon(Unlocks.firstConquestWeaponId) != null ? Unlocks.firstConquestWeaponId : WeaponsInOrder.First().weaponId;
        }

        /// <summary>The enemy a pool entry spawns (a variant's id first, then the archetype's, then the legacy id).</summary>
        public SimEnemyDef Enemy(PoolEntry entry) =>
            entry == null ? null : Enemy(entry.variant != null ? entry.variant.variantId : entry.archetype != null ? entry.archetype.enemyId : entry.enemyId);

        static SimEnemyDef Def(EnemyArchetypeConfig a, EnemyVariantConfig v)
        {
            var combat = v != null ? v.combat : a.combat;
            var pattern = combat != null ? combat.attackPattern : ConfigEnums.EnemyAttackPattern.UseDefinition;
            if (pattern == ConfigEnums.EnemyAttackPattern.UseDefinition && a.combat != null) pattern = a.combat.attackPattern;
            var attack = SimAttack.Contact;
            switch (pattern)
            {
                case ConfigEnums.EnemyAttackPattern.SingleShot:
                case ConfigEnums.EnemyAttackPattern.BurstVolley:
                case ConfigEnums.EnemyAttackPattern.SpreadVolley: attack = SimAttack.Ranged; break;
                case ConfigEnums.EnemyAttackPattern.AreaDenial: attack = SimAttack.Area; break;
                case ConfigEnums.EnemyAttackPattern.SuicideExplosion: attack = SimAttack.Suicide; break;
                case ConfigEnums.EnemyAttackPattern.None: attack = SimAttack.None; break;
            }
            // Behaviours that keep their distance shoot even when the attack pattern is left to the definition.
            var behavior = v != null && v.behavior.enabled ? v.behavior.value : a.behavior;
            if (attack == SimAttack.Contact && pattern == ConfigEnums.EnemyAttackPattern.UseDefinition && behavior == ConfigEnums.EnemyBehavior.MaintainRange) attack = SimAttack.Ranged;
            var isBoss = a.isBoss;
            if (isBoss) attack = SimAttack.Boss;
            return new SimEnemyDef
            {
                Id = v != null ? v.variantId : a.enemyId,
                BaseHp = v != null && v.baseHP.enabled ? v.baseHP.value : a.stats.baseHP,
                BaseDamage = v != null && v.baseDamage.enabled ? v.baseDamage.value : a.stats.baseDamage,
                Speed = v != null && v.moveSpeed.enabled ? v.moveSpeed.value : a.stats.moveSpeed,
                Cooldown = combat != null && combat.attackCooldown > 0f ? combat.attackCooldown : Math.Max(0.3f, a.stats.attackCooldown),
                Xp = a.rewards.xpValue,
                Gold = a.rewards.goldValue,
                Shots = combat != null ? Math.Max(1, combat.projectileCount) : 1,
                Attack = attack,
                IsBoss = isBoss,
                Radius = a.stats.radius,
                Telegraphed = pattern == ConfigEnums.EnemyAttackPattern.TelegraphedCharge || behavior == ConfigEnums.EnemyBehavior.TelegraphedCharge,
                PreferredRange = combat != null && combat.preferredRange > 0f ? combat.preferredRange : a.stats.preferredRange,
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Audio;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.VFX;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// M9: the build and progression modules in a run — level-ups, Logbooks, items, the Captain, floor powerups and
    /// pickups — over the kept shared pieces (RunManager's stats and state, the ship, the level-up modal, the pickup
    /// and powerup views, toasts and telemetry). Replaces V3's LevelSystem, WeaponController record, PickupWorld,
    /// V3RunItemInventory/Effects, V3CaptainController/ActiveController and V3FloorPowerupWorld. The ticks sit where
    /// those components updated (see <see cref="LegacyUpdateSlot"/>).
    /// </summary>
    [DeletedInPhase(14, "Run wiring and the kept views move to V4 at switch-over")]
    public sealed class LegacyBuildWiring : IDisposable
    {
        static readonly MethodInfo VfxLevelChanged = typeof(CombatVfxController).GetMethod("OnLevelChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly ConfigSetRuntime _config;
        readonly Func<PlayerShipController> _ship;
        readonly Func<GameDatabase> _database;
        bool _bellPending;

        public StatResolver Stats { get; }
        public ProgressionModule Progression { get; }
        public ItemModule Items { get; }
        public CaptainModule Captain { get; }
        public PowerupModule Powerups { get; }
        public PickupModule Pickups { get; }
        public BuildWeaponModifiers WeaponModifiers { get; }
        /// <summary>Set once the weapon module exists (Captain actives and Captain's Bell fire it).</summary>
        public IWeaponManager Weapons { get; set; }

        public LegacyBuildWiring(ConfigSetRuntime config, Func<PlayerShipController> ship, Func<GameDatabase> database)
        {
            _config = config;
            _ship = ship;
            _database = database;
            if (config != null) config.ConfigChanged += _ => _items = null;
            var run = new BuildRun(this);
            var world = new BuildShip(this);
            Stats = new StatResolver(() => RunManager.Instance != null ? RunManager.Instance.Stats : null);
            var offers = new OfferGenerator(
                () => config.Single<ProgressionConfig>(),
                () => config.Single<OfferWeightsConfig>(),
                () => OfferGenerator.InDatabaseOrder(config.All<WeaponConfig>()),
                () => OfferGenerator.InDatabaseOrder(config.All<LogbookConfig>()),
                () => config.All<WeaponEvolutionConfig>().ToList(),
                () => Experiment?.weaponEvolutionsEnabled ?? true);
            Progression = new ProgressionModule(() => config.Single<ProgressionConfig>(), WeaponConfig, Logbook, offers,
                new RunStreams(), new OfferContext(this), Stats, Stats, run, world)
            {
                Presenter = new LegacyLevelUpPresenter(config, database),
            };
            Items = new ItemModule(ItemConfig, Stats, world);
            Captain = new CaptainModule(Stats, world, run, () => Experiment?.captainActivesEnabled ?? false);
            Powerups = new PowerupModule(() => config.Single<PowerupConfig>(), new RunStreams(), Stats, world, new PowerupViews());
            Pickups = new PickupModule(() => config.Single<PickupConfig>(), HighSeas.Platform.QualityProfile.Current.pickupCap, new PickupRewards(this),
                new LegacyPickupViews(PickupRoot(), null), () => config.Single<StageContentConfig>()?.magneticTideOpensBarrels ?? false,
                () => HighSeas.Platform.QualityProfile.Current.vfxDensity);
            WeaponModifiers = new BuildWeaponModifiers(() => RunManager.Instance.Stats, Captain, Items);

            // Level-ups: the Captain first (V3's controller subscribed at run start), then the level-up burst.
            Progression.LevelChanged += _ => Captain.OnLevelChanged();
            Progression.LevelChanged += PlayLevelUpBurst;
            Progression.OfferChosen += RecordOfferTelemetry;
            Items.Acquired += OnItemAcquired;
            Items.FireAllWeapons += () => _bellPending = true;
            Captain.ActiveUsed += OnActiveUsed;
            Captain.FireAllWeapons += () => Weapons?.FireAllOnce();
            Captain.ArmPowderKegs += () => Weapons?.ArmAllKegs();
            Captain.FireLightningSalvo += () => Weapons?.FireLightningSalvo();
            Powerups.Dropped += kind => V3ToastHud.Instance?.Show($"{V3FloorPowerupWorld.GetDisplayName((V3FloorPowerupKind)(int)kind)} dropped!", 2f);
            Powerups.Collected += kind => V3ToastHud.Instance?.Show(kind == PowerupKind.MagneticTide
                ? "Magnetic Tide — distant XP and gold pull toward you."
                : $"{V3FloorPowerupWorld.GetDisplayName((V3FloorPowerupKind)(int)kind)} collected.", 2.5f);
            Powerups.AttractXp += Pickups.AttractAllXp;
            Powerups.AttractLooseRewards += Pickups.AttractLooseRewards;
        }

        ExperimentConfig Experiment => _config?.Single<ExperimentConfig>();

        // Items are looked up on every damage roll and hit (ItemModule.Effect): a dictionary, rebuilt when configs change.
        Dictionary<string, ItemConfig> _items;

        ItemConfig ItemConfig(string id)
        {
            if (_items == null)
            {
                _items = new Dictionary<string, ItemConfig>();
                foreach (var item in _config.All<ItemConfig>()) if (!string.IsNullOrEmpty(item.itemId)) _items[item.itemId] = item;
            }
            return id != null && _items.TryGetValue(id, out var config) ? config : null;
        }
        WeaponConfig WeaponConfig(string id) => _config.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id);
        public LogbookConfig Logbook(string id) => _config.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id);
        PlayerShipController Ship => _ship();

        /// <summary>V3's pickups were children of PickupWorld on /Systems.</summary>
        static Transform PickupRoot()
        {
            var systems = GameObject.Find("Systems");
            var root = new GameObject("V4Pickups").transform;
            if (systems != null) root.SetParent(systems.transform, false);
            return root;
        }

        // ---- Run start, in V3RunBootstrap's order ------------------------------------------------------------------------

        /// <summary>V3 LevelSystem.CancelPendingLevelUps: nothing pending, the modal hidden, the level-up pause lifted.</summary>
        public void CancelPendingLevelUps()
        {
            Progression.CancelPendingLevelUps();
            LevelUpModal.Instance?.Hide();
            RunManager.Instance?.SetPause(PauseReason.None);
        }

        /// <summary>V3 ResetPowerupsAndItems (after RunManager.BeginRun made the run's stats).</summary>
        public void ResetForRun()
        {
            Stats.ResetForRun();
            Powerups.ResetForRun();
            Items.ResetForRun();
            Pickups.ClearAll();
            _bellPending = false;
        }

        /// <summary>The selected Captain (V3CaptainSelection, the menu's saved choice).</summary>
        public CaptainConfig SelectedCaptain()
        {
            var key = LegacyBuildMapping.CaptainKey(V3CaptainSelection.Selected);
            var all = _config.All<CaptainConfig>().ToList();
            return all.FirstOrDefault(c => c.captainKey == key) ?? all.OrderBy(c => c.menuOrder).FirstOrDefault();
        }

        /// <summary>V3 EnsureCaptain: the Captain controller and active controller added to /Systems (one slot here).</summary>
        public void BeginCaptain(CaptainConfig captain, Action createHellfireSlot)
        {
            Captain.BeginRun(captain);
            AddSlot(GameObject.Find("Systems"), "captain", dt =>
            {
                var keyboard = Keyboard.current;
                var pressed = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
                    || Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
                Captain.Tick(dt, pressed);
            });
            createHellfireSlot?.Invoke();
        }

        /// <summary>The starting weapon by the unlock rule (V3: Saltwind always starts with the First Conquest weapon).</summary>
        public string ResolveStartingWeapon(CaptainConfig captain)
        {
            var unlocks = _config.Single<UnlockConfig>();
            var firstConquest = unlocks != null ? unlocks.firstConquestWeaponId : null;
            var conquered = V3PlaytestFlags.UnlockAllContent || PlayerPrefs.HasKey(V3ContentUnlocks.FirstConquestKey);
            // V3 (ResolveStartingWeapon): Saltwind always starts with Swivel Guns unless the debug unlock-all flag is on;
            // then a conquered save starts with the Captain's V3 weapon.
            if (V3PlaytestFlags.SaltwindDebugUnlockAll)
                return conquered && captain != null && !string.IsNullOrEmpty(captain.v3StartingWeaponId) ? captain.v3StartingWeaponId : firstConquest;
            if (unlocks == null || unlocks.startingWeaponRule == StartingWeaponRule.V3AlwaysFirstConquestWeapon) return firstConquest;
            return conquered && captain != null && !string.IsNullOrEmpty(captain.startingWeaponId) ? captain.startingWeaponId : firstConquest;
        }

        /// <summary>V3 LevelSystem.PrepareForNewRun and the record's Initialize with its slot capacity.</summary>
        public void BeginProgression(string startingWeapon)
        {
            var slots = Experiment != null ? Experiment.weaponSlotCap : 6;
            Progression.BeginRun(startingWeapon, slots, _config.Single<ProgressionConfig>().newWeaponDamageBonus);
        }

        /// <summary>V3 EnsureItemSystems: V3RunItemEffects on its own new object (its Update's place).</summary>
        public void BeginItems() => AddSlot(new GameObject("V4ItemEffects"), "items", Items.Tick);

        /// <summary>Pickups then floor powerups, where V3RunBootstrap ticked PickupWorld and V3FloorPowerupWorld.</summary>
        public void TickWorld(float dt, Vector3 playerPos)
        {
            Pickups.Tick(dt, playerPos, RunManager.Instance.Stats.PickupRadius * Captain.PickupRadiusMultiplier); // × 1 unless R-49's switch is on
            Powerups.Tick(dt, playerPos);
        }

        /// <summary>Kill fan-out targets (V3 order: pickups, then the powerup roll; items after the kill event).</summary>
        public void SpawnKillDrops(Vector3 position, int xp, int gold) => Pickups.SpawnFromEnemy(position, xp, gold);
        public void RollPowerup(Vector3 position, bool isBoss, bool isElite) => Powerups.TryRollOnKill(position, isBoss, isElite);
        public void NotifyItemKill() => Items.NotifyKill();

        /// <summary>
        /// Before each weapon tick: Captain's Bell. V3 fired from NotifyKill after every kill subscriber had run; V4
        /// fires on the next weapon tick so the burst's kills do not nest inside the outer kill (as since M5).
        /// </summary>
        public void BeforeWeaponTick()
        {
            if (!_bellPending) return;
            _bellPending = false;
            Weapons?.FireAllOnce();
        }

        /// <summary>The weapon carries an evolution that leaves burning patches on direct flame kills (Hellfire Nozzle).</summary>
        public bool HasHellfire(string weaponId)
        {
            var evolution = Progression.EvolutionOf(weaponId);
            if (evolution == null) return false;
            var config = _config.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.evolutionId == evolution);
            return config != null && config.effect == ConfigEnums.EvolutionEffect.WideConeWithPatches && config.wideCone.patchesOnKill;
        }

        /// <summary>The build's incoming-damage multiplier on the ship (Brace!, Reinforced Keel).</summary>
        public float IncomingDamageMultiplier() => Captain.IncomingDamageMultiplier * Items.IncomingDamageMultiplier();

        /// <summary>V3 DamageReceiver.TakeDamage told the items after every hit it took.</summary>
        public void AttachShip(PlayerShipController ship)
        {
            if (ship?.Damage != null) ship.Damage.Damaged += OnShipDamaged;
        }

        void OnShipDamaged(float amount, bool crit) => Items.NotifyPlayerTookDamage();

        public void Dispose()
        {
            var ship = Ship;
            if (ship != null && ship.Damage != null) ship.Damage.Damaged -= OnShipDamaged;
            Pickups.ClearAll();
        }

        // ---- Side effects V3's components had (toasts, telemetry, the level-up burst) ---------------------------------

        void OnItemAcquired(string itemId)
        {
            RunCombatTelemetry.Instance?.RecordItemAcquired(itemId);
            V3ToastHud.Instance?.Show(V3ContentDescriptions.FormatAcquiredItem(itemId), 4f);
        }

        void OnActiveUsed(CaptainConfig captain)
        {
            RunCombatTelemetry.Instance?.Record("captain_active_used", new Dictionary<string, object> { ["captain"] = ((V3CaptainId)captain.legacyId).ToString() });
            V3ToastHud.Instance?.Show($"{captain.active.displayName}!", 2.5f);
        }

        void RecordOfferTelemetry(Offer offer)
        {
            if (offer.Kind == OfferKind.NewWeapon) RunCombatTelemetry.Instance?.RecordNewWeaponOfferAccepted();
            RunCombatTelemetry.Instance?.RecordWeaponCount(Progression.OwnedWeapons.Count, Progression.Level,
                RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f);
        }

        CombatVfxController _vfx;

        /// <summary>CombatVfxController's level-up burst (it listened to LevelSystem.LevelChanged).</summary>
        void PlayLevelUpBurst(int level)
        {
            if (_vfx == null) _vfx = Object.FindFirstObjectByType<CombatVfxController>();
            if (_vfx != null && _vfx.isActiveAndEnabled) VfxLevelChanged?.Invoke(_vfx, new object[] { level });
        }

        static void AddSlot(GameObject host, string role, Action<float> tick)
        {
            if (host == null) host = new GameObject("V4" + role + "Slot");
            var slot = host.AddComponent<LegacyUpdateSlot>();
            slot.role = role;
            slot.Tick = tick;
        }

        // ---- Adapters ------------------------------------------------------------------------------------------------

        sealed class RunStreams : IOfferRandom, IPowerupRandom
        {
            public float LootFloat() => RunManager.Instance.Rng.LootFloat();
            public float PowerupFloat() => RunManager.Instance.Rng.PowerupFloat();
        }

        /// <summary>
        /// V3 LevelOfferGenerator.IsWeaponOfferEligible: the weapon's feature flag and, in V3 runs (V3RunContext active,
        /// as every V4 run is), its unlock — a starter or owned in the save. A fresh save is never offered a locked weapon.
        /// </summary>
        public static bool IsWeaponOfferEligible(string weaponId) =>
            WeaponFeatureFlags.IsWeaponEnabled(weaponId) && (!V3RunContext.IsActive || V3ContentUnlocks.IsWeaponUnlocked(weaponId));

        sealed class OfferContext : IOfferContext
        {
            readonly LegacyBuildWiring _w;
            public OfferContext(LegacyBuildWiring w) => _w = w;
            public bool IsWeaponOfferable(string weaponId) => IsWeaponOfferEligible(weaponId);
            public bool IsWeaponUnlocked(string weaponId) => V3ContentUnlocks.IsWeaponUnlocked(weaponId);
            public bool HasItem(string itemId) => _w.Items.HasItem(itemId);
            public bool IsEvolved(string weaponId) => _w.Progression.EvolutionOf(weaponId) != null;
        }

        /// <summary>RunManager as the level-ups and the Captain see it.</summary>
        sealed class BuildRun : IProgressionRun, ICaptainRun
        {
            readonly LegacyBuildWiring _w;
            public BuildRun(LegacyBuildWiring w) => _w = w;
            static RunManager Run => RunManager.Instance;
            public bool Exists => Run != null;
            public bool IsRunEnded => Run != null && Run.IsRunEnded;
            public bool IsPaused => Run != null && Run.IsPaused;
            public int Gold => Run != null ? Run.Gold : 0;
            public int PlayerLevel => _w.Progression.Level;
            public void SetPause(PauseReason reason) => Run?.SetPause(reason);
            public void SetState(RunState state) => Run?.SetState(state);
            public void EnterPlaying() => Run?.EnterPlaying();
        }

        /// <summary>The ship (PlayerShipController, DamageReceiver) for level-ups, the Captain, items and powerups.</summary>
        sealed class BuildShip : IProgressionShip, ICaptainShip, IItemWorld, IPowerupWorld
        {
            readonly LegacyBuildWiring _w;
            public BuildShip(LegacyBuildWiring w) => _w = w;
            PlayerShipController Ship => _w.Ship;
            DamageReceiver Hull => Ship != null ? Ship.Damage : null;

            public bool Exists => Ship != null;
            public bool IsAlive => Hull != null && Hull.IsAlive;
            float IProgressionShip.Hull => Hull != null ? Hull.CurrentHP : 0f;
            public float MaxHull => Hull != null ? Hull.MaxHP : 0f;
            public void Heal(float amount) => Hull?.Heal(amount);
            public void SyncSpeed() => Ship?.SyncRunSpeedMultiplier();
            public void SyncMaxHull(float healFlat) { if (Ship != null) PlayerHullSync.SyncMaxFromRunStats(Ship, preserveRatio: false, healFlat: healFlat); }

            public float Speed { get { if (Ship == null) return 0f; var v = Ship.Velocity; return new Vector3(v.x, 0f, v.z).magnitude; } }
            public void InitializeHull(float baseHull) => Ship?.InitializeFromRun(baseHull);
            public void SyncMaxHullKeepRatio() { if (Ship != null) PlayerHullSync.SyncMaxFromRunStats(Ship); }
            public void ApplySlow(float multiplier, float seconds) => Ship?.ApplySlow(multiplier, seconds);
            public void ApplyMoveEffect(float multiplier, float seconds) => Ship?.ApplyMoveEffect(multiplier, seconds);

            public bool RunActive => RunManager.Instance != null && !RunManager.Instance.IsPaused && !RunManager.Instance.IsRunEnded;
            public bool ShipExists => Ship != null;
            public Vector3 ShipForward => Ship.MovementForward;
            public Vector3 ShipPosition => Ship.Position;
            public Vector3 ShipVelocity => Ship.Velocity;
            public int Gold => RunManager.Instance != null ? RunManager.Instance.Gold : 0;
            public IEnumerable<WeaponConfig> Weapons => _w.Progression.Loadout.Weapons.Select(l => l.Config);

            public bool IsFreeSpot(Vector3 p, float margin, float clearance) =>
                PlayBounds.IsInside(p, margin) && !(IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(p, clearance));
            public bool IsBossFight => RunManager.Instance != null && RunManager.Instance.IsBossFightState();
            public bool ShipAlive => IsAlive;
            public float ShipHull => Hull != null ? Hull.CurrentHP : 0f;
            public float ShipMaxHull => MaxHull;
            public void HealShip(float amount) => Hull?.Heal(amount);
            public void SyncShipSpeed() => SyncSpeed();
        }

        /// <summary>V3's floor powerup models (V3PlaceholderFactory), children of /Systems as V3FloorPowerupWorld's were.</summary>
        sealed class PowerupViews : IPowerupViews
        {
            public object Create(PowerupKind kind, Vector3 position)
            {
                var systems = GameObject.Find("Systems");
                return V3PlaceholderFactory.CreateFloorPowerup((V3FloorPowerupKind)(int)kind, position, systems != null ? systems.transform : null).transform;
            }
            public void Move(object view, Vector3 position) { if (view is Transform t && t != null) t.position = position; }
            public void Destroy(object view) { if (view is Transform t && t != null) Object.Destroy(t.gameObject); }
        }

        /// <summary>Where pickups pay: XP to the level-ups, gold through the stage economy, hull to the ship.</summary>
        sealed class PickupRewards : IPickupRewards
        {
            readonly LegacyBuildWiring _w;
            public PickupRewards(LegacyBuildWiring w) => _w = w;
            static RunManager Run => RunManager.Instance;
            public bool RunEnded => Run == null || Run.IsRunEnded;
            public float XpGain => Run != null ? Run.Stats.XPGain : 0f;
            public void GrantXp(int amount) => _w.Progression.AddXp(amount);
            public void GrantGold(int amount)
            {
                if (Run == null) return;
                Run.AddGold(SaltwindStageEconomyLogic.ApplyGoldGrant(amount, StageBalanceProfileContext.Export, Run.Stats.GoldGain));
            }
            public void HealShip(int amount)
            {
                var hull = _w.Ship != null ? _w.Ship.Damage : null;
                if (hull != null && hull.IsAlive) hull.Heal(Mathf.Min(amount, hull.MaxHP - hull.CurrentHP));
            }
            public bool RollHealthDrop(float chance) => Run != null && Run.Rng.DirectorFloat() < chance;
            public void PlaySound(string id) => SfxService.PlayPlaceholder(id);
        }
    }

    /// <summary>
    /// The kept V3 level-up modal showing V4 cards: each card gets the texts V3's LevelOfferGenerator wrote (weapon
    /// descriptions, upgrade lines, Logbook bonus lines from the V4 Logbook stat), and the modal's choice calls back
    /// into the progression module.
    /// </summary>
    [DeletedInPhase(14, "LevelUpPresenter on the authored panel")]
    public sealed class LegacyLevelUpPresenter : IOfferPresenter
    {
        readonly ConfigSetRuntime _config;
        readonly Func<GameDatabase> _database;

        public LegacyLevelUpPresenter(ConfigSetRuntime config, Func<GameDatabase> database)
        {
            _config = config;
            _database = database;
        }

        public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
        {
            var modal = LevelUpModal.Instance;
            if (modal == null)
            {
                Debug.LogWarning("Level-up UI unavailable; waiting for modal wiring.");
                return false;
            }
            var cards = offers.Select(ToCard).ToList();
            if (!modal.TryShow(cards, card => selected(offers[IndexOf(cards, card)]), skipped, refreshed, refreshesRemaining))
            {
                Debug.LogWarning("Level-up UI unavailable; waiting for modal wiring.");
                return false;
            }
            V3OnboardingDirector.Instance?.NotifyLevelUpShown();
            Debug.Log($"Level-up modal shown with {offers.Count} offers.");
            return true;
        }

        static int IndexOf(List<LevelOffer> cards, LevelOffer card)
        {
            for (var i = 0; i < cards.Count; i++)
                if (cards[i].kind == card.kind && cards[i].id == card.id && cards[i].evolutionId == card.evolutionId && cards[i].rarity == card.rarity)
                    return i;
            return 0;
        }

        LevelOffer ToCard(Offer o)
        {
            var db = _database();
            switch (o.Kind)
            {
                case OfferKind.Continue: return LevelOfferGenerator.BuildContinueOffer();
                case OfferKind.FallbackHeal: return LevelOfferGenerator.BuildFallbackHealOffer();
                case OfferKind.FallbackDamage: return LevelOfferGenerator.BuildFallbackDamageOffer();
                case OfferKind.FallbackMoveSpeed: return LevelOfferGenerator.BuildFallbackMoveSpeedOffer();
                case OfferKind.WeaponEvolution:
                {
                    var evo = _config.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.evolutionId == o.EvolutionId);
                    return new LevelOffer
                    {
                        kind = LevelOfferKind.WeaponEvolution, id = o.Id, evolutionId = o.EvolutionId,
                        title = evo != null ? evo.displayName : o.EvolutionId, categoryLabel = "WEAPON EVOLUTION",
                        description = evo != null ? evo.description : string.Empty, rarity = Rarity.Legendary,
                        currentLevel = o.CurrentLevel, nextLevel = o.NextLevel, isWeapon = true, isNew = false,
                    };
                }
                case OfferKind.NewWeapon:
                {
                    var def = db != null ? db.GetWeapon(o.Id) : null;
                    return def != null ? LevelOfferGenerator.BuildNewWeaponOffer(def) : Plain(o, "New Weapon");
                }
                case OfferKind.WeaponUpgrade:
                {
                    var def = db != null ? db.GetWeapon(o.Id) : null;
                    var weapon = _config.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == o.Id);
                    var lines = (o.Lines ?? Array.Empty<UpgradeLine>()).Select(l => new WeaponUpgradeLine
                    {
                        axis = (UpgradeAxis)(int)l.Axis,
                        value = l.Value,
                        displayText = new WeaponUpgradeAxisConfig
                        {
                            axis = (UpgradeAxis)(int)l.Axis,
                            displayFormat = weapon?.upgradeAxes?.FirstOrDefault(a => a.axis == l.Axis)?.displayFormat,
                        }.FormatValue(l.Value),
                    }).ToArray();
                    var upgradeText = string.Join("\n", lines.Select(l => l.displayText));
                    var card = Plain(o, "Weapon Upgrade");
                    if (def != null) card.title = def.displayName;
                    card.description = def != null ? $"{V3ContentDescriptions.GetWeaponDescription(def)}\n{upgradeText}" : upgradeText;
                    card.upgradeLines = lines;
                    return card;
                }
                case OfferKind.Logbook:
                {
                    var logbook = _config.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == o.Id);
                    var mult = _config.Single<ProgressionConfig>().rarityMultipliers[o.Rarity];
                    var line = logbook != null && StatResolver.IsStat(logbook.stat) ? StatDisplay.FormatLogbookBonus((StatId)logbook.stat, logbook.perLevelAdditive * mult) : string.Empty;
                    if (logbook != null && logbook.stat == (int)StatId.Hull) line += " (also heals)";
                    var card = Plain(o, o.CurrentLevel > 0 ? "Logbook Upgrade" : "New Logbook");
                    card.title = logbook != null ? logbook.displayName : o.Id;
                    card.description = line;
                    card.effectLines = new[] { line };
                    card.upgradeLines = Array.Empty<WeaponUpgradeLine>();
                    return card;
                }
                default: return Plain(o, string.Empty);
            }
        }

        LevelOffer Plain(Offer o, string category)
        {
            var weapon = o.IsWeapon ? _config.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == o.Id) : null;
            return new LevelOffer
            {
                kind = (LevelOfferKind)(int)o.Kind, id = o.Id, evolutionId = o.EvolutionId,
                title = weapon != null ? weapon.displayName : o.Id, categoryLabel = category, rarity = (Rarity)o.Rarity,
                currentLevel = o.CurrentLevel, nextLevel = o.NextLevel, isWeapon = o.IsWeapon, isNew = o.IsNew,
                upgradeLines = Array.Empty<WeaponUpgradeLine>(),
            };
        }
    }
}

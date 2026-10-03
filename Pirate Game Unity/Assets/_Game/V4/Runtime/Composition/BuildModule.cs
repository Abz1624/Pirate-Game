using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoguePirates.V4.Foundation.Audio;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using RoguePirates.V4.Ports;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// M9: the build and progression modules in a run — level-ups, Logbooks, items, the Captain, floor powerups and
    /// pickups — over the kept shared pieces (RunManager's stats and state, the ship, the level-up modal, the pickup
    /// and powerup views, toasts and telemetry). Replaces V3's LevelSystem, WeaponController record, PickupWorld,
    /// V3RunItemInventory/Effects, V3CaptainController/ActiveController and V3FloorPowerupWorld. The ticks sit where
    /// those components updated (frame steps since M14.14).
    /// </summary>
    public sealed class BuildModule : IDisposable
    {

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
        /// <summary>The enemy world (for "is anything in range" before a weapons-only active).</summary>
        public IEnemyQuery Enemies { get; set; }

        /// <summary>PlayerPrefs key of the gold earned across all runs (Richie's unlock, M9 playtest).</summary>
        public const string LifetimeGoldKey = RoguePirates.V4.Rewards.LifetimeGold.Key;
        bool _goldRecorded;

        public BuildModule(ConfigSetRuntime config, Func<PlayerShipController> ship, Func<GameDatabase> database)
        {
            _config = config;
            _ship = ship;
            _database = database;
            if (config != null) config.ConfigChanged += _ => { _items = null; _lightning = null; };
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
            // V5 M5: Logbooks bought with Rum are offered only once owned.
            offers.LogbookUnlocked = id => IsBought(config, id);
            Progression = new ProgressionModule(() => config.Single<ProgressionConfig>(), WeaponConfig, Logbook, offers,
                new RunStreams(), new OfferContext(this), Stats, Stats, run, world)
            {
                Presenter = new OfferPresenterForwarder(() => OfferPresenterTarget?.Invoke()), // M14.11 (was LegacyLevelUpPresenter)
                // R-84/R-102: the stage switch decides (the v3-parity preset turns it off).
                ResumeToBossFight = () => config.Single<StageContentConfig>()?.bossFightSurvivesLevelUp ?? false,
            };
            Items = new ItemModule(ItemConfig, Stats, world);
            Captain = new CaptainModule(Stats, world, run, () => Experiment?.captainActivesEnabled ?? false);
            Powerups = new PowerupModule(() => config.Single<PowerupConfig>(), new RunStreams(), Stats, world, new RoguePirates.V4.Views.PowerupViewsForwarder(() => PowerupViewsTarget?.Invoke()));
            Pickups = new PickupModule(() => config.Single<PickupConfig>(), RoguePirates.V4.Foundation.Platform.QualityProfile.Current.pickupCap, new PickupRewards(this),
                new RoguePirates.V4.Views.PickupViewsForwarder(() => PickupViewsTarget?.Invoke()), () => config.Single<StageContentConfig>()?.magneticTideOpensBarrels ?? false,
                () => RoguePirates.V4.Foundation.Platform.QualityProfile.Current.vfxDensity);
            WeaponModifiers = new BuildWeaponModifiers(() => RunManager.Instance.Stats, Captain, Items, IsLightningWeapon);

            // Level-ups: the Captain first (V3's controller subscribed at run start), then the level-up burst.
            Progression.LevelChanged += _ => Captain.OnLevelChanged();
            Progression.OfferChosen += RecordOfferTelemetry;
            Items.Acquired += OnItemAcquired;
            Items.FireAllWeapons += () => _bellPending = true;
            Items.PulseRequested += (id, damage, radius, telegraph) => Weapons?.ItemPulse(id, damage, radius, telegraph); // V5 M5 Leviathan Heart
            Items.CheatedDeath += id => Toasts?.Show($"{ItemConfig(id)?.displayName ?? id}: back from the brink!", 3f);
            Captain.ActiveUsed += OnActiveUsed;
            Captain.HoldWithoutTarget = () => Experiment?.captainActiveHoldsWithoutTarget ?? false;
            Captain.HasWeaponTarget = AnyEnemyInWeaponRange;
            Captain.HeldWithoutTarget += c => Toasts?.Show($"{c.active.displayName}: no enemies in range", 2f);
            Captain.FireAllWeapons += () => Weapons?.FireAllOnce();
            Captain.ArmPowderKegs += () => Weapons?.ArmAllKegs();
            Captain.FireLightningSalvo += () => Weapons?.FireLightningSalvo();
            // M14.14: the names from the powerup config (were V3FloorPowerupWorld's).
            Powerups.Dropped += kind => Toasts?.Show($"{PowerupName(kind)} dropped!", 2f);
            Powerups.Collected += kind => Toasts?.Show(kind == PowerupKind.MagneticTide
                ? "Magnetic Tide: distant XP and gold pull toward you."
                : $"{PowerupName(kind)} collected.", 2.5f);
            Powerups.AttractXp += Pickups.AttractAllXp;
            Powerups.AttractLooseRewards += Pickups.AttractLooseRewards;
        }

        ExperimentConfig Experiment => _config?.Single<ExperimentConfig>();

        /// <summary>V5 M5: free content, or bought with Rum and owned in the save.</summary>
        public static bool IsBought(ConfigSetRuntime config, string contentId)
        {
            var unlocks = config?.Single<UnlockConfig>();
            return unlocks == null || unlocks.RumCost(contentId) <= 0 || Ports.ContentPort.IsContentOwned(contentId);
        }
        /// <summary>The active's key for the HUD: the gamepad button while a gamepad is connected (R-49: RB, V3: A), else SPACE.</summary>
        public string ActiveKeyHint() => Gamepad.current == null ? "SPACE" : Experiment != null && !Experiment.captainActiveOnGamepadSouth ? "RB" : "A";

        /// <summary>Captain actives switched on (ExperimentConfig; off under the v3-parity preset).</summary>
        public bool ActivesEnabled() => Experiment?.captainActivesEnabled ?? false;

        // Items are looked up on every damage roll and hit (ItemModule.Effect): a dictionary, rebuilt when configs change.
        Dictionary<string, ItemConfig> _items;
        Dictionary<string, bool> _lightning;

        /// <summary>R-49: Roxie's lightning multiplier applies to area-strike weapons (by config behavior, no ids).</summary>
        bool IsLightningWeapon(string weaponId)
        {
            if (weaponId == null) return false;
            if (_lightning == null)
            {
                _lightning = new Dictionary<string, bool>();
                foreach (var w in _config.All<WeaponConfig>()) if (!string.IsNullOrEmpty(w.weaponId)) _lightning[w.weaponId] = w.behavior == ConfigEnums.WeaponBehavior.AreaStrike;
            }
            return _lightning.TryGetValue(weaponId, out var yes) && yes;
        }

        /// <summary>R-40/R-49: burn and Hellfire ticks × this (Sea Witch's Eye, Ember's passive burn).</summary>
        public float StatusDamageMultiplier() => Items.StatusDamageMultiplier() * Captain.PassiveBurnMultiplier;

        /// <summary>R-40: a near-miss dodge reaches Ghost Lantern and Roxie's active.</summary>
        public void NotifyNearMiss()
        {
            Items.NotifyNearMiss();
            Captain.NotifyNearMiss();
        }

        public float NearMissMargin() => Experiment != null ? Mathf.Max(0f, Experiment.nearMissMargin) : 0f;

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
        /// <summary>An item's config (the loadout view's item names).</summary>
        public ItemConfig ItemConfigFor(string id) => ItemConfig(id);

        public LogbookConfig Logbook(string id) => _config.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id);
        PlayerShipController Ship => _ship();

        /// <summary>M14.14: the run's frame (ticks register here instead of legacy update slots).</summary>
        public FramePipeline Frame { get; set; }
        /// <summary>M14.9b: the pickup and powerup presenters the presentation root registers (were V3's PickupViewPool
        /// and PlaceholderFactory's floor powerups).</summary>
        public Func<IPickupViews> PickupViewsTarget;
        /// <summary>M14.11: the level-up presenter (was LegacyLevelUpPresenter over V3's LevelUpModal).</summary>
        public Func<IOfferPresenter> OfferPresenterTarget;
        public Func<IPowerupViews> PowerupViewsTarget;

        // ---- Run start, in V3RunBootstrap's order ------------------------------------------------------------------------

        /// <summary>V3 LevelSystem.CancelPendingLevelUps: nothing pending, the modal hidden, the level-up pause lifted.</summary>
        public void CancelPendingLevelUps()
        {
            Progression.CancelPendingLevelUps();
            Progression.Presenter?.Hide();
            RunManager.Instance?.SetPause(PauseReason.None);
        }

        /// <summary>V3 ResetPowerupsAndItems (after RunManager.BeginRun made the run's stats).</summary>
        public void ResetForRun()
        {
            Stats.ResetForRun();
            _goldRecorded = false;
            Powerups.ResetForRun();
            Items.ResetForRun();
            Pickups.ClearAll();
            Pickups.Seed(RunManager.Instance != null ? RunManager.Instance.RunSeed : 0); // M14: the pickup stream (after RunManager.BeginRun)
            _bellPending = false;
        }

        string PowerupName(PowerupKind kind) => _config.Single<PowerupConfig>()?.DisplayName(kind) ?? kind.ToString();

        /// <summary>
        /// The selected Captain (the menu's saved choice, by the Captain config's V3 id). M14.13 review fix: a saved Captain
        /// the lock rule now locks (a choice from before the V4 locks, or a direct reload) falls back to the first open one.
        /// </summary>
        public CaptainConfig SelectedCaptain()
        {
            var id = CaptainChoice.LegacyId;
            var all = _config.All<CaptainConfig>().OrderBy(c => c.menuOrder).ToList();
            var chosen = all.FirstOrDefault(c => c.legacyId == id);
            var locks = MenuServices.CreateLocks(() => _config);
            // Playtest (3 Oct, owner): in a 1v1 both players sail the same ship whatever their Captain: the first one.
            if (PvpWeapons != null) return all.FirstOrDefault();
            var allowLocked = RunRequest.ConsumeAllowLockedCaptain(); // the console's "restart as" (dev tools)
            if (chosen != null && (allowLocked || !locks.IsLocked(chosen))) return chosen;
            return all.FirstOrDefault(c => !locks.IsLocked(c)) ?? all.FirstOrDefault();
        }

        /// <summary>V3 EnsureCaptain: the Captain's tick (its controllers' Updates) in the frame's Captain step.</summary>
        public void BeginCaptain(CaptainConfig captain, Action createHellfireSlot)
        {
            Captain.BeginRun(captain);
            Frame?.Register(FrameStep.Captain, dt =>
            {
                var keyboard = Keyboard.current;
                var pad = Gamepad.current;
                // R-49: V3 used gamepad South, which is also the boost button; with the fix the right shoulder uses the active.
                var padButton = pad == null ? null : Experiment != null && !Experiment.captainActiveOnGamepadSouth ? pad.rightShoulder : pad.buttonSouth;
                var keyPressed = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame);
                var padPressed = padButton != null && padButton.wasPressedThisFrame;
                // South is also the boost button: a boost press must not raise "no enemies in range" every time, so a
                // weapons-only active with nothing in range ignores a South press (keyboard presses still say so).
                if (padPressed && !keyPressed && padButton == pad.buttonSouth && Captain.Config?.active != null
                    && CaptainModule.OnlyFiresWeapons(Captain.Config.active) && (Experiment?.captainActiveHoldsWithoutTarget ?? false) && !AnyEnemyInWeaponRange())
                    padPressed = false;
                // V5: the round touch button (touch devices).
                var touchPressed = RoguePirates.V4.Foundation.Platform.TouchAbilityButton.ConsumePress();
                Captain.Tick(dt, keyPressed || padPressed || touchPressed);
            });
            createHellfireSlot?.Invoke();
        }

        /// <summary>The starting weapon by the unlock rule (V3: Saltwind always starts with the First Conquest weapon).</summary>
        public string ResolveStartingWeapon(CaptainConfig captain)
        {
            var weapon = ResolveConfiguredStartingWeapon(captain);
            // Playtest (3 Oct, owner, round 7): a switched-off weapon (Ember's flamethrower) starts as the First Conquest weapon.
            if (RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(weapon)) return weapon;
            var unlocks = _config.Single<UnlockConfig>();
            return unlocks != null ? unlocks.firstConquestWeaponId : weapon;
        }

        string ResolveConfiguredStartingWeapon(CaptainConfig captain)
        {
            var unlocks = _config.Single<UnlockConfig>();
            var firstConquest = unlocks != null ? unlocks.firstConquestWeaponId : null;
            var conquered = ContentPort.ConqueredForStartingWeapon;
            // V3 (ResolveStartingWeapon): Saltwind always starts with Swivel Guns unless the debug unlock-all flag is on;
            // then a conquered save starts with the Captain's V3 weapon.
            if (ContentPort.SaltwindDebugUnlockAll)
                return conquered && captain != null && !string.IsNullOrEmpty(captain.v3StartingWeaponId) ? captain.v3StartingWeaponId : firstConquest;
            if (unlocks == null || unlocks.startingWeaponRule == StartingWeaponRule.V3AlwaysFirstConquestWeapon) return firstConquest;
            return conquered && captain != null && !string.IsNullOrEmpty(captain.startingWeaponId) ? captain.startingWeaponId : firstConquest;
        }

        /// <summary>V3 LevelSystem.PrepareForNewRun and the record's Initialize with its slot capacity.</summary>
        public void BeginProgression(string startingWeapon)
        {
            var slots = Experiment != null ? Experiment.weaponSlotCap : 6;
            if (PvpWeapons != null && PvpWeapons.Count > 0) startingWeapon = PvpWeapons[0];
            Progression.BeginRun(startingWeapon, slots, _config.Single<ProgressionConfig>().newWeaponDamageBonus);
            if (PvpWeapons != null) Progression.BeginPvp(PvpWeapons, PvpLogbooks);
        }

        /// <summary>Playtest (3 Oct, owner): the 1v1 draft (six weapons and six Logbooks in priority order); null in a voyage.
        /// Set, the run starts with it and the first Captain.</summary>
        public IReadOnlyList<string> PvpWeapons { get; set; }
        public IReadOnlyList<string> PvpLogbooks { get; set; }

        /// <summary>V3 EnsureItemSystems: the item triggers (V3RunItemEffects.Update) in the frame's Items step.</summary>
        public void BeginItems() => Frame?.Register(FrameStep.Items, Items.Tick);

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

        const float TargetMargin = 1.5f; // enemies are measured centre to centre; a big hull's edge can be in reach first

        /// <summary>Some enemy is within the range of a carried weapon (a weapons-only active waits for one).</summary>
        public bool AnyEnemyInWeaponRange()
        {
            var ship = Ship;
            if (ship == null || Enemies == null || Weapons == null) return true; // nothing to check against: let it fire
            var range = 0f;
            var size = RunManager.Instance != null ? RunManager.Instance.Stats.Size : 1f;
            foreach (var slot in Weapons.Slots) if (slot?.Config != null) range = Mathf.Max(range, slot.Config.range * slot.SizeMult * size);
            return range > 0f && Enemies.TryGetNearest(ship.Position, range + TargetMargin, out _);
        }

        /// <summary>Adds the run's gold to the lifetime total once (run end; Richie unlocks at 5,000, enforced by the V4 menu in M14).</summary>
        public void RecordRunGold()
        {
            if (_goldRecorded || RunManager.Instance == null) return;
            _goldRecorded = true;
            RoguePirates.V4.Rewards.LifetimeGold.Add(RunManager.Instance.GrossGoldEarned);
        }

        public static int LifetimeGold => RoguePirates.V4.Rewards.LifetimeGold.Total;

        /// <summary>The build's incoming-damage multiplier on the ship (Brace!, Reinforced Keel, Armor).</summary>
        public float IncomingDamageMultiplier() => Captain.IncomingDamageMultiplier * Items.IncomingDamageMultiplier() * ArmorMultiplier();

        /// <summary>Playtest (3 Oct, owner: "Armor does nothing"): Armor cuts every hit with diminishing returns, damage x
        /// 20 / (20 + Armor): 4 Armor (Reef Carapace) takes 17% off, 15 (Ironclad at rank 5) about 43%.</summary>
        public const float ArmorHalfPoint = 20f;
        float ArmorMultiplier()
        {
            var armor = Mathf.Max(0f, Stats.Get((int)StatId.Armor));
            return ArmorHalfPoint / (ArmorHalfPoint + armor);
        }

        /// <summary>V3 DamageReceiver.TakeDamage told the items after every hit it took.</summary>
        public void AttachShip(PlayerShipController ship)
        {
            if (ship?.Damage != null)
            {
                ship.Damage.Damaged += OnShipDamaged;
                ship.Damage.PreventDeath = Items.TryCheatDeath; // V5 M5 Captain's Last Rum
            }
        }

        void OnShipDamaged(float amount, bool crit) => Items.NotifyPlayerTookDamage();

        public void Dispose()
        {
            var ship = Ship;
            if (ship != null && ship.Damage != null)
            {
                ship.Damage.Damaged -= OnShipDamaged;
                ship.Damage.PreventDeath = null;
            }
            Pickups.ClearAll();
        }

        // ---- Side effects V3's components had (toasts, telemetry, the level-up burst) ---------------------------------

        /// <summary>M14: the run's toasts (was V3ToastHud).</summary>
        public RoguePirates.V4.Views.IToastSink Toasts;
        /// <summary>M14: the run's V4 telemetry (was V3's static RunCombatTelemetry).</summary>
        public RoguePirates.V4.Telemetry.RunTelemetry Telemetry;

        void OnItemAcquired(string itemId)
        {
            Telemetry?.RecordItemAcquired(itemId);
            // M14.14: the item config's name and description (were V3ContentDescriptions', same format).
            var item = ItemConfig(itemId);
            Toasts?.Show(item != null ? $"{item.displayName}\n<size=80%>{item.description}</size>" : itemId, 4f);
        }

        void OnActiveUsed(CaptainConfig captain)
        {
            // The V3 Captain name telemetry recorded (Rook, Brine, Flint, Richie, Ember, Roxie) from the config key.
            var name = string.IsNullOrEmpty(captain.captainKey) ? string.Empty : char.ToUpperInvariant(captain.captainKey[0]) + captain.captainKey.Substring(1);
            Telemetry?.Record("captain_active_used", new Dictionary<string, object> { ["captain"] = name });
            Toasts?.Show($"{captain.active.displayName}!", 2.5f);
        }

        void RecordOfferTelemetry(Offer offer)
        {
            if (offer.Kind == OfferKind.NewWeapon) Telemetry?.RecordNewWeaponOfferAccepted();
            Telemetry?.RecordWeaponCount(Progression.OwnedWeapons.Count, Progression.Level,
                RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f);
        }

        // (M14: the level-up burst is the combat feedback presenter's, from the LevelUp event.)

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
        public static bool IsWeaponOfferEligible(string weaponId) => ContentPort.IsWeaponOfferEligible(weaponId);

        sealed class OfferContext : IOfferContext
        {
            readonly BuildModule _w;
            public OfferContext(BuildModule w) => _w = w;
            public bool IsWeaponOfferable(string weaponId) => IsWeaponOfferEligible(weaponId);
            public bool IsWeaponUnlocked(string weaponId) => ContentPort.IsWeaponUnlocked(weaponId);
            public bool HasItem(string itemId) => _w.Items.HasItem(itemId);
            public bool IsEvolved(string weaponId) => _w.Progression.EvolutionOf(weaponId) != null;
        }

        /// <summary>RunManager as the level-ups and the Captain see it.</summary>
        sealed class BuildRun : IProgressionRun, ICaptainRun
        {
            readonly BuildModule _w;
            public BuildRun(BuildModule w) => _w = w;
            static RunManager Run => RunManager.Instance;
            public bool Exists => Run != null;
            public bool IsRunEnded => Run != null && Run.IsRunEnded;
            public bool IsPaused => Run != null && Run.IsPaused;
            public RunState State => Run != null ? Run.State : RunState.Playing;
            public int Gold => Run != null ? Run.Gold : 0;
            public int PlayerLevel => _w.Progression.Level;
            public void SetPause(PauseReason reason) => Run?.SetPause(reason);
            public void SetState(RunState state) => Run?.SetState(state);
            public void EnterPlaying() => Run?.EnterPlaying();
        }

        /// <summary>The ship (PlayerShipController, DamageReceiver) for level-ups, the Captain, items and powerups.</summary>
        sealed class BuildShip : IProgressionShip, ICaptainShip, IItemWorld, IPowerupWorld
        {
            readonly BuildModule _w;
            public BuildShip(BuildModule w) => _w = w;
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

            public int EnemiesNear(float radius)
            {
                var enemies = _w.Enemies;
                if (enemies == null || Ship == null) return 0;
                var p = Ship.Position;
                var count = 0;
                for (var i = 0; i < enemies.Capacity; i++)
                    if (enemies.IsActive(i) && WeaponCombat.WithinHorizontalRadius(p, enemies.Position(i), radius)) count++;
                return count;
            }

            public bool IsFreeSpot(Vector3 p, float margin, float clearance) =>
                PlayBounds.IsInside(p, margin) && !(IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(p, clearance));
            public bool IsBossFight => RunManager.Instance != null && RunManager.Instance.IsBossFightState();
            public bool ShipAlive => IsAlive;
            public float ShipHull => Hull != null ? Hull.CurrentHP : 0f;
            public float ShipMaxHull => MaxHull;
            public void HealShip(float amount) => Hull?.Heal(amount);
            public void SyncShipSpeed() => SyncSpeed();
        }

        /// <summary>V3's floor powerup models (PlaceholderFactory), children of /Systems as V3FloorPowerupWorld's were.</summary>
        /// <summary>Where pickups pay: XP to the level-ups, gold through the stage economy, hull to the ship.</summary>
        sealed class PickupRewards : IPickupRewards
        {
            readonly BuildModule _w;
            public PickupRewards(BuildModule w) => _w = w;
            static RunManager Run => RunManager.Instance;
            public bool RunEnded => Run == null || Run.IsRunEnded;
            public float XpGain => Run != null ? Run.Stats.XPGain : 0f;
            public void GrantXp(int amount) => _w.Progression.AddXp(amount);
            public void GrantGold(int amount)
            {
                if (Run == null) return;
                Run.AddGold(StageProfilePort.ApplyGoldGrant(amount, Run.Stats.GoldGain));
            }
            public void HealShip(int amount)
            {
                var hull = _w.Ship != null ? _w.Ship.Damage : null;
                if (hull != null && hull.IsAlive) hull.Heal(Mathf.Min(amount, hull.MaxHP - hull.CurrentHP));
            }
            public bool RollHealthDrop(float chance) => Run != null && Run.Rng.DirectorFloat() < chance;
            public float RunSeconds => Run != null ? Run.ElapsedSeconds : 0f;
            public float MaxHull => _w.Ship != null && _w.Ship.Damage != null ? _w.Ship.Damage.MaxHP : 100f;
            public float HullFraction
            {
                get
                {
                    var hull = _w.Ship != null ? _w.Ship.Damage : null;
                    return hull != null && hull.MaxHP > 0f ? hull.CurrentHP / hull.MaxHP : 1f;
                }
            }
            public void PlaySound(string id) { } // V3's SfxService only logged placeholder ids; V4 has no pickup sounds yet
        }
    }
}

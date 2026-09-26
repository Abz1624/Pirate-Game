using System.Collections.Generic;
using System.Text;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.PlayerSystems;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.World;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    // M6: the read-only views presentation reads, implemented over the legacy hubs until each hub has a V4 module.
    // They only read (and the command sink forwards button presses the way V3's HUD and results screen did). Hub
    // lookups are cached and retried at most every 30 frames when missing, instead of V3's per-frame
    // FindFirstObjectByType (R-63).

    /// <summary>Caches a scene object found by type; a missing one is looked up again at most every 30 frames.</summary>
    [DeletedInPhase(14, "Used only by the legacy views")]
    struct CachedLookup<T> where T : Object
    {
        T _value;
        int _nextLookupFrame;

        public T Get()
        {
            if (_value != null) return _value;
            if (Time.frameCount < _nextLookupFrame) return null;
            _value = Object.FindFirstObjectByType<T>();
            if (_value == null) _nextLookupFrame = Time.frameCount + 30;
            return _value;
        }
    }

    [DeletedInPhase(14, "RunManager is read through the V4 run lifecycle after switch-over")]
    public sealed class LegacyRunStatusView : IRunStatusView
    {
        static RunManager Run => RunManager.Instance;
        public bool Exists => Run != null;
        public float ElapsedSeconds => Run != null ? Run.ElapsedSeconds : 0f;
        public bool IsOvertime => Run != null && Run.IsOvertime;
        public int Gold => Run != null ? Run.Gold : 0;
        public int Kills => Run != null ? Run.KillCount : 0;
        public bool IsRunEnded => Run != null && Run.IsRunEnded;
        public bool IsPaused => Run != null && Run.IsPaused;
        public bool IsRunComplete => Run != null && Run.State == RunState.RunComplete;
        public bool ObjectiveSecured => Run != null && Run.ObjectiveSecured;
        public bool EndedByDeath => Run != null && Run.LastRunEndedByDeath;
        public bool WeaponPlaytestActive => HighSeas.Playtest.WeaponEnemyPlaytestSession.Active;
    }

    [DeletedInPhase(14, "The ship stays shared infrastructure; its view moves with the V4 world module")]
    public sealed class LegacyShipView : IShipView
    {
        PlayerShipController _player;
        ShipBoostController _boost;
        CachedLookup<PlayerShipController> _lookup;

        public LegacyShipView(PlayerShipController player) => _player = player;

        public PlayerShipController Player
        {
            get
            {
                if (_player == null) _player = _lookup.Get();
                return _player;
            }
        }

        public bool Exists => Player != null;
        public Vector3 Position => Player != null ? Player.Position : Vector3.zero;
        public Vector3 Velocity => Player != null ? Player.Velocity : Vector3.zero;
        public Vector3 MovementForward => Player != null ? Player.MovementForward : Vector3.forward;
        public float Hp => Player != null && Player.Damage != null ? Player.Damage.CurrentHP : 0f;
        public float MaxHp => Player != null && Player.Damage != null ? Player.Damage.MaxHP : 0f;

        public ShipBoostController Boost
        {
            get
            {
                if (_boost == null && Player != null) _boost = Player.GetComponent<ShipBoostController>();
                return _boost;
            }
        }

        public float BoostNormalized => Boost != null ? Boost.BoostNormalized : 0f;
    }

    [DeletedInPhase(9, "CaptainModule and CaptainConfig")]
    public sealed class LegacyCaptainView : ICaptainView
    {
        static V3CaptainId Current => V3RunContext.IsActive ? V3RunContext.CaptainId : V3CaptainSelection.Selected;
        public int CaptainId => (int)Current;
        public string DisplayTitle => V3CaptainCatalog.GetDisplayTitle(Current);
    }

    [DeletedInPhase(9, "ProgressionModule replaces LevelSystem")]
    public sealed class LegacyProgressionView : IProgressionView
    {
        GameDatabase _database;

        static LevelSystem Levels => LevelSystem.Instance;
        // V3's HUD read logbook names from the loaded database asset (not the run copy); same names either way.
        GameDatabase Database => _database != null ? _database : (_database = GameDatabaseLoader.Load());

        public bool Exists => Levels != null;
        public int Level => Levels != null ? Levels.Level : 1;
        public float CurrentXp => Levels != null ? Levels.CurrentXP : 0f;
        public float XpToNext => Levels != null ? Levels.XPToNext : 1f;
        public int LogbookCount => Levels != null ? Levels.OwnedLogbooks.Count : 0;
        public string LogbookId(int index) => Levels.OwnedLogbooks[index];

        public string LogbookName(int index)
        {
            var id = LogbookId(index);
            var def = Database != null ? Database.GetLogbook(id) : null;
            return def != null ? def.displayName : id;
        }

        public int LogbookRank(int index) => Levels.GetLogbookLevel(LogbookId(index));
    }

    /// <summary>
    /// Weapons come from the V4 Weapon Manager (the slots it fires; they mirror the loadout record in order, so this is
    /// what V3's HUD showed from WeaponController), names from the V4 weapon and evolution configs; items from the
    /// legacy inventory until M9.
    /// </summary>
    [DeletedInPhase(9, "ItemModule replaces the legacy item inventory")]
    public sealed class LegacyLoadoutView : ILoadoutView
    {
        readonly IWeaponManager _weapons;

        public LegacyLoadoutView(IWeaponManager weapons) => _weapons = weapons;

        IReadOnlyList<WeaponSlot> Slots => _weapons != null ? _weapons.Slots : null;
        static V3RunItemInventory Items => V3RunItemInventory.Instance;

        public int WeaponSlotCap => CombatExperimentContext.Active.weaponSlotCap;
        public int WeaponCount => Slots != null ? Slots.Count : 0;
        public string WeaponId(int index) => Slots[index].Id;

        public string WeaponName(int index)
        {
            var slot = Slots[index];
            if (slot.Evolution != null && !string.IsNullOrEmpty(slot.Evolution.displayName)) return slot.Evolution.displayName;
            return WeaponIconName(index);
        }

        public string WeaponIconName(int index)
        {
            var config = Slots[index].Config;
            return config != null && !string.IsNullOrEmpty(config.displayName) ? config.displayName : Slots[index].Id;
        }

        public int WeaponLevel(int index) => Slots[index].Level;
        public int ItemCount => Items != null ? Items.Items.Count : 0;
        public string ItemId(int index) => Items.Items[index];
        public string ItemName(int index) => V3ContentDescriptions.GetItemName(ItemId(index));
        public int ItemStacks(int index) => Items.GetStackCount(ItemId(index));
    }

    [DeletedInPhase(7, "StageContentModule and BeaconDiscovery")]
    public sealed class LegacyStageView : IStageView
    {
        CachedLookup<POIManager> _poi;
        StageLayout _lastLayout;
        int _layoutVersion;

        static StageLayout Layout => V3RunContext.Layout;
        static V3DiscoverySystem Discovery => V3DiscoverySystem.Instance;

        public bool Exists => Layout != null;

        public int LayoutVersion
        {
            get
            {
                if (!ReferenceEquals(Layout, _lastLayout)) { _lastLayout = Layout; _layoutVersion++; }
                return _layoutVersion;
            }
        }

        public int PoiCount => Layout?.pois != null ? Layout.pois.Count : 0;

        public PoiMarker Poi(int index)
        {
            var slot = Layout.pois[index];
            return new PoiMarker { type = slot.type, position = slot.position, used = slot.used };
        }

        public bool HasDiscovery => Discovery != null;
        public BeaconReveal Beacon => Discovery != null ? (BeaconReveal)(int)Discovery.BeaconState : BeaconReveal.Hidden;
        public Vector3? BeaconPosition => Discovery != null ? Discovery.GetBeaconPosition() : null;
        public bool IsPoiVisible(int index) => Discovery != null && Discovery.IsPoiVisible(index);
        public bool IsCellExplored(Vector3 world) => Discovery != null && Discovery.IsCellExplored(world);
        public int ExplorationRevision => Discovery != null ? Discovery.ExplorationRevision : 0;
        public float ExploreCellSize => V3DiscoverySystem.ExploreCellSize;
        public float DiscoveryRadius => V3DiscoverySystem.DiscoveryRadius;

        public bool BossSummoned
        {
            get { var poi = _poi.Get(); return poi != null && poi.BossSummoned; }
        }

        public bool BossDefeated
        {
            get { var poi = _poi.Get(); return poi != null && poi.BossDefeated; }
        }
    }

    [DeletedInPhase(8, "The V4 Enemy World")]
    public sealed class LegacyEnemyView : IEnemyView
    {
        CachedLookup<EnemyWorld> _world;

        public void CollectThreats(List<ThreatMarker> into)
        {
            into.Clear();
            var world = _world.Get();
            if (world == null) return;
            var defs = world.Definitions;
            if (defs == null || defs.Length == 0) return;
            var agents = world.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (!agent.active) continue;
                if (agent.definitionIndex < 0 || agent.definitionIndex >= defs.Length) continue;
                var def = defs[agent.definitionIndex];
                if (def == null || (!def.isBoss && !def.isMiniboss)) continue;
                into.Add(new ThreatMarker { position = agent.position, isBoss = def.isBoss });
            }
        }

        public bool IsKnownDefinition(int definitionIndex)
        {
            var defs = _world.Get()?.Definitions;
            return defs != null && definitionIndex >= 0 && definitionIndex < defs.Length && defs[definitionIndex] != null;
        }

        public EnemyTier TierOf(int definitionIndex) =>
            IsKnownDefinition(definitionIndex) ? EnemyTierLogic.ResolveTier(_world.Get().Definitions[definitionIndex]) : EnemyTier.Soldier;
    }

    /// <summary>The results data V3RunResultsScreen.BuildSummaryText read, same sources and formatting helpers.</summary>
    [DeletedInPhase(12, "RewardModule and the V4 results view")]
    public sealed class LegacyResultsView : IResultsView
    {
        static RunManager Run => RunManager.Instance;

        public bool ObjectiveSecured => Run != null && Run.ObjectiveSecured;
        public bool EndedByDeath => Run != null && Run.LastRunEndedByDeath;

        public string ProfileFooter => Run != null
            ? SaltwindRunResultsLogic.FormatProfileFooter(StageBalanceProfileContext.Export, Run.RunSeed, Run.ElapsedSeconds)
            : string.Empty;

        public int GoldEarned => Run != null ? Run.GrossGoldEarned : 0;
        public int GoldSpent => Run != null ? Run.GrossGoldSpent : 0;
        public int GoldRemaining => Run != null ? Run.Gold : 0;
        public bool HasRewardSection => StageBalanceProfileContext.IsActive;

        public string RewardSection
        {
            get
            {
                if (!StageBalanceProfileContext.IsActive) return string.Empty;
                var save = AppRoot.Instance?.Save;
                var treasury = save?.Meta.marks ?? 0;
                var awarded = SaltwindRunSettlement.LastMarksAwarded;
                if (SaltwindRunSettlement.LastSettlementApplied || awarded > 0 || SaltwindRunSettlement.LastBreakdown.TotalMarks > 0)
                    return SaltwindRunResultsLogic.FormatMarksBreakdownForPlayer(SaltwindRunSettlement.LastBreakdown, awarded, treasury);
                return null;
            }
        }

        public string GoalProgress
        {
            get
            {
                if (!StageBalanceProfileContext.IsActive) return string.Empty;
                var save = AppRoot.Instance?.Save;
                return save != null ? SaltwindMarksUnlockLogic.FormatSelectedGoalProgress(save, StageBalanceProfileContext.Export) : string.Empty;
            }
        }

        public void CollectFinalBuild(List<NamedValue> into)
        {
            into.Clear();
            var levels = LevelSystem.Instance;
            if (levels == null) return;
            for (var i = 0; i < levels.OwnedWeapons.Count; i++)
            {
                var id = levels.OwnedWeapons[i];
                into.Add(new NamedValue { name = V3WeaponDisplayNames.GetWeaponName(id), value = levels.GetWeaponLevel(id) });
            }
        }

        public void CollectContributions(List<NamedValue> into)
        {
            into.Clear();
            var lines = SaltwindRunResultsLogic.BuildWeaponContributionLines(WeaponDamageLedger.Snapshot());
            for (var i = 0; i < lines.Count; i++)
                into.Add(new NamedValue { name = lines[i].DisplayName, value = lines[i].DamageSharePercent });
        }
    }

    [DeletedInPhase(14, "ToastPresenter replaces the kept V3 toast view")]
    public sealed class LegacyToastSink : IToastSink
    {
        public void Show(string message, float seconds) => V3ToastHud.Instance?.Show(message, seconds);
    }

    /// <summary>Button presses from the HUD and results screen, forwarded as V3 handled them (restart and menu go to V4).</summary>
    [DeletedInPhase(12, "The reward/unlock actions move to the V4 unlock service")]
    public sealed class LegacyHudCommands : IHudCommands
    {
        readonly LegacyShipView _ship;
        readonly System.Action _retry;
        readonly System.Action _menu;

        public LegacyHudCommands(LegacyShipView ship, System.Action retry, System.Action menu)
        {
            _ship = ship;
            _retry = retry;
            _menu = menu;
        }

        public void RequestBoost() => _ship?.Boost?.RequestBoost();
        public void Retry() => _retry?.Invoke();
        public void GoToMenu() => _menu?.Invoke();

        public string SelectNextUnlockGoal()
        {
            var save = AppRoot.Instance?.Save;
            var export = StageBalanceProfileContext.Export;
            if (save == null || export == null) return null;
            var next = SaltwindMarksUnlockLogic.GetNextLockedMarksWeapon(export, save);
            if (string.IsNullOrEmpty(next)) return "All Marks weapons owned.";
            SaltwindMarksUnlockLogic.TrySetUnlockGoal(save, next);
            return $"Unlock goal: {V3WeaponDisplayNames.GetWeaponName(next)}";
        }

        public string PurchaseUnlockGoal()
        {
            var save = AppRoot.Instance?.Save;
            var export = StageBalanceProfileContext.Export;
            if (save == null || export == null) return null;
            var goal = save.Meta.selectedUnlockGoal;
            if (string.IsNullOrEmpty(goal)) return "Select an unlock goal first.";
            if (SaltwindMarksUnlockLogic.TryPurchaseUnlock(save, export, goal))
                return $"Unlocked {V3WeaponDisplayNames.GetWeaponName(goal)}!";
            return "Not enough Marks or already owned.";
        }
    }

    /// <summary>
    /// V3's results screen also recorded "results shown" in the run telemetry and wrote the telemetry export, inside the
    /// run's RunComplete state change (before the death record that follows it). That is not presentation, so it stays
    /// here, at the same moment. V3 had two results screens and did it twice (R-63); once now.
    /// </summary>
    [DeletedInPhase(12, "V4 rewards and telemetry")]
    public sealed class LegacyResultsTelemetry : System.IDisposable
    {
        RunManager _run;

        public void Attach()
        {
            Dispose();
            _run = RunManager.Instance;
            if (_run != null) _run.StateChanged += OnStateChanged;
        }

        public void Dispose()
        {
            if (_run != null) _run.StateChanged -= OnStateChanged;
            _run = null;
        }

        void OnStateChanged(RunState state)
        {
            if (state != RunState.RunComplete || _run == null) return;
            RunCombatTelemetry.Instance?.RecordRunResultsShown(_run.ObjectiveSecured, SaltwindRunSettlement.LastMarksAwarded);
            RunCombatTelemetry.Instance?.WriteExportFile();
        }
    }

    /// <summary>Builds the M6 view set over the legacy hubs (and the V4 weapon module's aim view).</summary>
    [DeletedInPhase(14, "Every view has a V4 implementation after switch-over")]
    public static class LegacyViewFactory
    {
        public static RunViews Create(PlayerShipController player, WeaponManager weapons, System.Action retry, System.Action menu)
        {
            var ship = new LegacyShipView(player);
            return new RunViews
            {
                Run = new LegacyRunStatusView(),
                Ship = ship,
                Captain = new LegacyCaptainView(),
                Progression = new LegacyProgressionView(),
                Loadout = new LegacyLoadoutView(weapons),
                Stage = new LegacyStageView(),
                Enemies = new LegacyEnemyView(),
                Results = new LegacyResultsView(),
                WeaponAim = weapons != null ? new WeaponAimView(weapons.Combat) : null,
                Toasts = new LegacyToastSink(),
                Commands = new LegacyHudCommands(ship, retry, menu)
            };
        }
    }
}

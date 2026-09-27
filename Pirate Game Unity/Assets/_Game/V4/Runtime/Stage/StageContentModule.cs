using System;
using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// Chests, landmarks and the Treasury Beacon (M7): port of the V3 paths of POIManager and POIInteractable, driven by
    /// the stage layout's POIs (placed by the shared StageBuilder). Same rules, texts, random draws and order:
    /// chests (price quote, loot plan on the loot stream, item, refund when full), Repair Wreck, Powder Cache (proximity
    /// ambush through the V4 encounter executor, then item + gold), Signal Buoy (charge in the circle, reward on the map
    /// stream), Treasure Chart, Lighthouse of Fortune, and the Beacon (hold, telegraph, boss summon) with boss victory.
    /// Ticks where V3 ticked: <see cref="TickManager"/> in POIManager's scene slot, <see cref="TickPoi"/> on each marker
    /// (POIInteractable's place). V3's non-profile paths (relic chests, spawner chest prices) are not ported: V4 always
    /// runs with a stage profile.
    /// </summary>
    public sealed class StageContentModule
    {
        public const string PromptOpenChest = "Open Salvage Chest";

        readonly Func<StageContentConfig> _config;
        readonly IStageRun _run;
        readonly IStageShip _ship;
        readonly IStageEconomy _economy;
        readonly IStageRewards _rewards;
        readonly IStageToasts _toasts;
        readonly IEncounterTracker _encounters;
        readonly IEncounterExecutor _executor;
        IPoiMarkers _markers;

        StageLayout _layout;
        bool _enabled = true;
        bool _bossSummoned, _bossDefeated;
        int _paidChestCount;
        int _powderCacheEncounterCount, _powderCacheTrackerId, _bossEncounterId;
        bool _powderCacheCleared;
        readonly HashSet<int> _powderCacheAmbushStarted = new HashSet<int>();
        readonly HashSet<int> _signalBuoyChargeAnnounced = new HashSet<int>();
        readonly Dictionary<int, float> _signalBuoyCharges = new Dictionary<int, float>();
        readonly HashSet<int> _signalBuoysCompleted = new HashSet<int>();
        readonly List<int> _pendingChestPoiIndices = new List<int>();
        int _chestProgressSpawnPaidIndex = -1;

        // POIInteractable state, per POI index.
        bool[] _used, _hidden, _telegraphing, _awaitingBossSummon;
        float[] _telegraphTimer;
        readonly List<int> _markerOrder = new List<int>(); // marker creation order (V3's interactable lookup order)

        public BeaconDiscovery Discovery { get; }

        /// <summary>
        /// Native CombatEvents (M7): ChestOpened, LandmarkUsed (A = POI index, Tag = type), BeaconState (A = reveal state),
        /// BossState (A = 1 summoned, 2 defeated). Set by the composition root.
        /// </summary>
        public Action<CombatEventKind, int, int, Vector3, string> Publish;

        public StageContentModule(Func<StageContentConfig> config, IStageRun run, IStageShip ship, IStageEconomy economy,
            IStageRewards rewards, IStageToasts toasts, IEncounterTracker encounters, IEncounterExecutor executor)
        {
            _config = config;
            _run = run;
            _ship = ship;
            _economy = economy;
            _rewards = rewards;
            _toasts = toasts;
            _encounters = encounters;
            _executor = executor;
            Discovery = new BeaconDiscovery(config, () => _ship != null && _ship.Exists ? _ship.Position : (Vector3?)null);
            Discovery.BeaconSpotted += () => _toasts?.Show("Treasury Beacon spotted — follow the guide.", 3.5f);
            Discovery.BeaconChanged += state => Publish?.Invoke(CombatEventKind.BeaconState, (int)state, 0, default, null);
        }

        StageContentConfig C => _config?.Invoke();
        public StageContentConfig Config => C;
        public StageLayout Layout => _layout;
        public bool Enabled => _enabled;
        public bool BossSummoned => _bossSummoned;
        public bool BossDefeated => _bossDefeated;
        public int PaidChestCount => _paidChestCount;
        public int PowderCacheEncounterId => _powderCacheTrackerId;
        public IReadOnlyList<int> MarkerOrder => _markerOrder;
        float SignalBuoyChargeRequired => C != null ? C.signalBuoyChargeSeconds : 8f;
        float SignalBuoyRadius => C != null ? C.signalBuoyRadius : 4f;

        /// <summary>POIManager.SetV3Options + Initialize: resets the run's stage state and creates the markers.</summary>
        public void Initialize(StageLayout layout, IPoiMarkers markers, bool enabled = true)
        {
            _markers?.Clear();
            _markers = markers;
            _enabled = enabled;
            ResetState();
            _layout = layout;
            var n = layout?.pois != null ? layout.pois.Count : 0;
            _used = new bool[n];
            _hidden = new bool[n];
            _telegraphing = new bool[n];
            _awaitingBossSummon = new bool[n];
            _telegraphTimer = new float[n];
            if (!_enabled) return; // PvP scenes: V3 switched POIManager off before initializing it
            BuildMarkers();
        }

        /// <summary>Off: no chest spawns, ambushes, buoy charge, interactions or Beacon.</summary>
        public void SetEnabled(bool enabled) => _enabled = enabled;

        void ResetState()
        {
            _bossSummoned = false;
            _bossDefeated = false;
            _paidChestCount = 0;
            _powderCacheEncounterCount = 0;
            _powderCacheTrackerId = 0;
            _bossEncounterId = 0;
            _encounters?.ResetForRun();
            _powderCacheCleared = false;
            _powderCacheAmbushStarted.Clear();
            _signalBuoyChargeAnnounced.Clear();
            _signalBuoyCharges.Clear();
            _signalBuoysCompleted.Clear();
            _pendingChestPoiIndices.Clear();
            _chestProgressSpawnPaidIndex = -1;
            _markerOrder.Clear();
        }

        bool SpawnAllChestsAtStart => C == null || C.disableGoldTriggeredChestSpawn;

        void BuildMarkers()
        {
            if (_layout == null) return;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_layout.pois[i].type == POIType.SalvageChest && !SpawnAllChestsAtStart)
                {
                    _pendingChestPoiIndices.Add(i);
                    continue;
                }
                SpawnMarker(i);
            }
            TrySpawnNextPendingChest();
        }

        void SpawnMarker(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count || _markers == null) return;
            var poi = _layout.pois[index];
            _markers.Spawn(index, poi.type, poi.position, TickPoi);
            _markerOrder.Add(index);
        }

        // ---- POIManager.Update --------------------------------------------------------------------------------

        public void TickManager(float dt)
        {
            if (!_enabled) return;
            RestoreBossFightAfterResume();
            TickProgressiveChestSpawns();
            if (!_powderCacheCleared && _powderCacheEncounterCount > 0 && IsPowderCacheEncounterClear())
            {
                _powderCacheCleared = true;
                // R-97 fix (off = V3): the hidden cache could never be used again, so V3 never paid it out.
                if (C != null && C.powderCachePaysWhenCleared) PayClearedPowderCaches();
            }
        }

        void TickProgressiveChestSpawns()
        {
            if (SpawnAllChestsAtStart || _pendingChestPoiIndices.Count == 0) return;
            if (!_run.Exists || _run.IsRunEnded) return;
            if (CountActiveSalvageChestMarkers() == 0)
            {
                TrySpawnNextPendingChest();
                return;
            }
            if (_chestProgressSpawnPaidIndex == _paidChestCount) return;
            var threshold = _economy.ProgressChestPrice(_paidChestCount) * (C != null ? C.chestSpawnGoldFraction : 0.8f);
            if (_run.Gold < threshold) return;
            _chestProgressSpawnPaidIndex = _paidChestCount;
            TrySpawnNextPendingChest();
        }

        int CountActiveSalvageChestMarkers()
        {
            if (_layout == null) return 0;
            var count = 0;
            for (var i = 0; i < _layout.pois.Count; i++)
                if (_layout.pois[i].type == POIType.SalvageChest && !_layout.pois[i].used && _markers.IsSpawned(i)) count++;
            return count;
        }

        void TrySpawnNextPendingChest()
        {
            if (_pendingChestPoiIndices.Count == 0) return;
            var index = _pendingChestPoiIndices[0];
            _pendingChestPoiIndices.RemoveAt(0);
            SpawnMarker(index);
        }

        // ---- POIInteractable (per marker) ---------------------------------------------------------------------

        public bool IsMarkerSpawned(int index) => _markers != null && _markers.IsSpawned(index);
        public bool CanInteract(int index) => _enabled && InRange(index) && !_used[index] && !_hidden[index];
        public bool IsConsumed(int index) => InRange(index) && _used[index];
        public bool IsHidden(int index) => InRange(index) && _hidden[index];
        public bool IsTelegraphing(int index) => InRange(index) && _telegraphing[index];
        public Vector3 MarkerPosition(int index) => _markers != null ? _markers.Position(index) : Poi(index).position;
        public float DistanceToShip(int index) => _ship != null && _ship.Exists ? Vector3.Distance(_ship.Position, MarkerPosition(index)) : float.MaxValue;
        public bool IsPlayerInRange(int index) => DistanceToShip(index) <= InteractRange(index);
        bool InRange(int index) => _used != null && index >= 0 && index < _used.Length;
        POISlot Poi(int index) => _layout.pois[index];

        public float InteractRange(int index)
        {
            var c = C;
            switch (Poi(index).type)
            {
                case POIType.TrialBuoy: return c != null ? c.interactRange : 4f;
                case POIType.BossBeacon: return c != null ? c.beaconInteractRange : 6f;
                case POIType.SalvageChest: return c != null ? c.chestInteractRange : 6f;
                default: return c != null ? c.interactRange : 4f;
            }
        }

        public float RequiredHoldSeconds(int index) =>
            Poi(index).type == POIType.BossBeacon ? (C != null ? C.beaconHoldSeconds : 0.7f) : 0f;

        public bool UsesChargeInteraction(int index) => Poi(index).type == POIType.TrialBuoy;
        public bool UsesProximityAmbush(int index) => Poi(index).type == POIType.MerchantSkiff;
        public bool ShowsSalvageChestPrice(int index) => Poi(index).type == POIType.SalvageChest;

        public string PromptLabel(int index) => Poi(index).type switch
        {
            POIType.SalvageChest => SalvageChestPromptLabel,
            POIType.GildedWreck => "Repair Wreck",
            POIType.MerchantSkiff => "Powder Cache",
            POIType.TrialBuoy => "Signal Buoy",
            POIType.CursedIdol => "Treasure Chart",
            POIType.BossBeacon => "Activate Treasury Beacon",
            _ => Poi(index).type.ToString()
        };

        /// <summary>POIInteractable.Update: buoy charge and ring, Powder Cache proximity, the Beacon's telegraph countdown.</summary>
        public void TickPoi(int index, float dt)
        {
            if (!_enabled || !InRange(index) || _used[index]) return;
            if (_run.Exists && _run.IsPaused) return;
            var type = Poi(index).type;
            if (type == POIType.TrialBuoy)
            {
                TickSignalBuoyCharge(index, dt);
                _markers.SetChargeRing(index, SignalBuoyCharge01(index), SignalBuoyRadius);
                if (Poi(index).used) Consume(index);
            }
            if (type == POIType.MerchantSkiff) TickPowderCacheProximity(index);
            if (_awaitingBossSummon[index])
            {
                _telegraphTimer[index] -= dt;
                _markers.PulseTelegraph(index);
                if (_telegraphTimer[index] > 0f) return;
                _awaitingBossSummon[index] = false;
                if (Interact(type, index)) Consume(index);
                else if (C != null && C.beaconRetryAfterFailedSummon)
                {
                    // R-96 fix: V3 left the telegraph flag set after a failed summon, so the Beacon never worked again.
                    _telegraphing[index] = false;
                    _markers.HideTelegraph(index);
                }
            }
        }

        /// <summary>POIInteractable.TryCompleteInteraction (the interaction controller calls it).</summary>
        public void TryCompleteInteraction(int index)
        {
            if (!InRange(index) || _used[index]) return;
            if (Poi(index).type == POIType.BossBeacon)
            {
                // V3 quirk kept: if the summon fails, the telegraph flag stays set and the Beacon can't be used again.
                if (!_telegraphing[index])
                {
                    _telegraphing[index] = true;
                    _awaitingBossSummon[index] = true;
                    _telegraphTimer[index] = C != null ? C.bossTelegraphSeconds : 0.7f;
                    _markers.ShowTelegraph(index);
                }
                return;
            }
            if (Interact(Poi(index).type, index)) Consume(index);
        }

        void Consume(int index)
        {
            _used[index] = true;
            _telegraphing[index] = false;
            _telegraphTimer[index] = 0f;
            _markers.Consume(index);
        }

        void HideMarker(int index)
        {
            _hidden[index] = true;
            _telegraphing[index] = false; // V3 SetMarkerHidden hid the rings (HideTelegraph resets the telegraph)
            _telegraphTimer[index] = 0f;
            _markers.Hide(index);
        }

        // ---- POIManager.Interact ------------------------------------------------------------------------------

        public bool Interact(POIType type, int index)
        {
            if (_run.Exists && _run.IsRunEnded) return false;
            if (_ship == null || !_ship.Exists) return false;
            var c = C;
            switch (type)
            {
                case POIType.SalvageChest:
                    if (!TryOpenSalvageChest(index)) return false;
                    ConsumePoiMarker(index);
                    return true;
                case POIType.LighthouseOfFortune:
                    _run.AddStatModifier(5 /* StatId.Damage */, 0f, c != null ? c.lighthouseDamageMultiplier : 1.08f);
                    break;
                case POIType.TrialBuoy:
                    return TryCompleteSignalBuoy(index);
                case POIType.GildedWreck:
                    if (_ship.CurrentHp >= _run.MaxHull - 0.5f)
                    {
                        _toasts?.Show("Hull already full — wreck unchanged.", 2f);
                        return false;
                    }
                    var missing = _run.MaxHull - _ship.CurrentHp;
                    _ship.Heal(Mathf.Min(_run.MaxHull * (c != null ? c.repairWreckHealFraction : 0.25f), missing));
                    _toasts?.Show("Repair Wreck — hull restored 25%.", 2.5f);
                    break;
                case POIType.MerchantSkiff:
                    return TryCompletePowderCache(index);
                case POIType.CursedIdol:
                    Discovery.RevealNearestChests(c != null ? c.treasureChartRevealCount : 3, _ship.Position);
                    var bearing = Discovery.BeaconBearingDegrees(_ship.Position);
                    _toasts?.Show($"Treasure Chart — chests revealed; beacon bearing {bearing:0}°.", 3f);
                    break;
                case POIType.BossBeacon:
                    return TrySummonBossAtBeacon();
                default:
                    return false;
            }

            MarkPoiUsed(index);
            _rewards?.PoiUsedTelemetry(type);
            return true;
        }

        public int QuoteChestPrice() => _economy.QuoteChestPrice();
        public bool CanAffordSalvageChest() => _run.Exists && _run.Gold >= QuoteChestPrice();
        public string SalvageChestPromptLabel => $"{PromptOpenChest} — {QuoteChestPrice()} gold";

        bool TryOpenSalvageChest(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count || _layout.pois[index].used) return false;
            if (!_run.Exists) return false;
            // R-47: V3 looked for the equipped weapons on the ship object, never found them, and passed none.
            var owned = C != null && C.chestPlanUsesLoadout ? _economy.EquippedWeaponIds : null;
            var plan = _economy.PlanChest(owned);
            if (!plan.Valid)
            {
                if (plan.Error == ChestPlan.InsufficientGold) _toasts?.Show($"Need {plan.Price} run gold to open chest.", 2f);
                else if (plan.Error == ChestPlan.NoEligibleLoot) _toasts?.Show("Chest unavailable — no eligible reward.", 2f);
                return false;
            }
            if (!_run.TrySpendGold(plan.Price))
            {
                _toasts?.Show($"Need {plan.Price} run gold to open chest.", 2f);
                return false;
            }
            _economy.RegisterChestPurchase();
            _paidChestCount++;
            if (!_economy.TryAddItem(plan.ItemId))
            {
                _run.AddGold(plan.Price);
                _toasts?.Show("Inventory full — chest unchanged.", 2.5f);
                return false;
            }
            _toasts?.Show($"Salvage Chest — gained {_economy.ItemName(plan.ItemId)}.", 2.5f);
            _rewards?.ChestOpened();
            Publish?.Invoke(CombatEventKind.ChestOpened, index, 0, Poi(index).position, null);
            _rewards?.PoiUsedTelemetry(POIType.SalvageChest);
            return true;
        }

        // ---- Powder Cache ------------------------------------------------------------------------------------------

        void TickPowderCacheProximity(int index)
        {
            if (_powderCacheAmbushStarted.Contains(index) || _ship == null || !_ship.Exists) return;
            if (Vector3.Distance(_ship.Position, Poi(index).position) > (C != null ? C.powderCacheTriggerRadius : 8f)) return;
            if (!SpawnPowderCacheFight())
            {
                _toasts?.Show("Powder Cache — ambush failed to spawn, retrying...", 2f);
                return;
            }
            _powderCacheAmbushStarted.Add(index);
            _powderCacheEncounterCount++;
            HideMarker(index);
            _toasts?.Show("Powder Cache — ambush incoming!", 3.5f);
        }

        bool TryCompletePowderCache(int index)
        {
            if (!_powderCacheAmbushStarted.Contains(index))
            {
                TickPowderCacheProximity(index);
                return false;
            }
            if (!IsPowderCacheEncounterClear())
            {
                if (_powderCacheTrackerId > 0 && _encounters.Exists(_powderCacheTrackerId) && _encounters.State(_powderCacheTrackerId) == EncounterState.Failed)
                {
                    if (SpawnPowderCacheFight()) _toasts?.Show("Powder Cache — ambush retry!", 2.5f);
                    else _toasts?.Show("Powder Cache — could not spawn reinforcements.", 2f);
                }
                return false;
            }
            var itemId = _economy.RollChestPoolItem();
            _economy.TryAddItem(itemId);
            _run.AddGold(C != null ? C.powderCacheBonusGold : 20);
            MarkPoiUsed(index);
            _rewards?.PoiUsedTelemetry(POIType.MerchantSkiff);
            return true;
        }

        void PayClearedPowderCaches()
        {
            foreach (var index in new List<int>(_powderCacheAmbushStarted))
                if (InRange(index) && !Poi(index).used && TryCompletePowderCache(index)) Consume(index);
        }

        /// <summary>The ambush is clear once its encounter completed (V3's other fallbacks never apply in V4 runs).</summary>
        bool IsPowderCacheEncounterClear() =>
            _powderCacheTrackerId > 0 && _encounters.Exists(_powderCacheTrackerId) && _encounters.State(_powderCacheTrackerId) == EncounterState.Completed;

        bool SpawnPowderCacheFight()
        {
            var spawner = C != null ? C.powderCacheSpawner : null;
            if (spawner == null || _executor == null)
            {
                Debug.LogWarning("V4 stage: StageContentConfig.powderCacheSpawner is not set; the Powder Cache can't spawn its ambush.");
                return false;
            }
            var count = Mathf.Max(1, C.powderCacheAmbushCount);
            _powderCacheTrackerId = _encounters.Request(EncounterKind.PowderCache, count);
            _executor.Trigger(spawner, count, _powderCacheTrackerId);
            return true;
        }

        // ---- Signal Buoy -----------------------------------------------------------------------------------------

        public float SignalBuoyCharge(int index) => _signalBuoyCharges.TryGetValue(index, out var charge) ? charge : 0f;
        public float SignalBuoyCharge01(int index) => Mathf.Clamp01(SignalBuoyCharge(index) / SignalBuoyChargeRequired);

        void TickSignalBuoyCharge(int index, float dt)
        {
            if (_run.Exists && _run.IsPaused) return;
            if (_signalBuoysCompleted.Contains(index) || _ship == null || !_ship.Exists) return;
            if (!_signalBuoyCharges.ContainsKey(index)) _signalBuoyCharges[index] = 0f;
            if (Vector3.Distance(_ship.Position, Poi(index).position) > SignalBuoyRadius) return;
            if (_signalBuoyCharges[index] <= 0.05f && _signalBuoyChargeAnnounced.Add(index))
                _toasts?.Show("Signal Buoy charging — stay in the circle!", 3f);
            _signalBuoyCharges[index] += dt;
            if (_signalBuoyCharges[index] >= SignalBuoyChargeRequired) TryCompleteSignalBuoy(index);
        }

        bool TryCompleteSignalBuoy(int index)
        {
            if (_signalBuoysCompleted.Contains(index) || _ship == null || !_ship.Exists) return false;
            if (Vector3.Distance(_ship.Position, Poi(index).position) > SignalBuoyRadius) return false;
            if (SignalBuoyCharge(index) < SignalBuoyChargeRequired) return false;
            ApplySignalBuoyReward();
            _signalBuoysCompleted.Add(index);
            MarkPoiUsed(index);
            _rewards?.PoiUsedTelemetry(POIType.TrialBuoy);
            return true;
        }

        void ApplySignalBuoyReward()
        {
            var rewards = C != null ? C.signalBuoyRewards : null;
            if (rewards == null || rewards.Length == 0) return;
            var pick = rewards[_economy.MapInt(rewards.Length)];
            _run.AddStatModifier(pick.stat, pick.add, pick.multiply);
            _toasts?.Show($"Signal Buoy charged!\n<size=80%>{pick.label}</size>", 5f);
        }

        // ---- POIs used ---------------------------------------------------------------------------------------------

        void MarkPoiUsed(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count) return;
            var p = _layout.pois[index];
            p.used = true;
            _layout.pois[index] = p;
            _rewards?.RegisterSpecialPoi(index, p.type);
            if (p.type != POIType.SalvageChest && p.type != POIType.BossBeacon)
                Publish?.Invoke(CombatEventKind.LandmarkUsed, index, 0, p.position, p.type.ToString());
        }

        void ConsumePoiMarker(int index)
        {
            MarkPoiUsed(index);
            if (InRange(index) && _markers != null && _markers.IsSpawned(index)) Consume(index);
        }

        // ---- Beacon and boss ---------------------------------------------------------------------------------------

        /// <summary>The boss summon without the Beacon (console, dev-build B key, trace harness).</summary>
        public bool TrySummonBoss()
        {
            if (!_enabled)
            {
                _toasts?.Show("Stage content is off in this scene: no boss.", 2f);
                return false;
            }
            if (_run.Exists && _run.IsRunEnded)
            {
                _toasts?.Show("The run has ended.", 2f);
                return false;
            }
            if (_bossDefeated)
            {
                _toasts?.Show("Objective already secured.", 2f);
                return false;
            }
            if (_bossSummoned)
            {
                _toasts?.Show("Boss already summoned.", 2f);
                return false;
            }
            if (!TrySummonBossAtBeacon()) return false;
            _toasts?.Show("Treasury Beacon — boss incoming! (B)", 3f);
            return true;
        }

        bool TrySummonBossAtBeacon()
        {
            if (_bossSummoned)
            {
                _toasts?.Show("Boss already summoned.", 2f);
                return true;
            }
            if (_bossDefeated)
            {
                _toasts?.Show("Objective already secured.", 2f);
                return false;
            }
            SummonBoss();
            if (_bossSummoned) return true;
            _toasts?.Show("Beacon failed — clear space near the beacon and try again.", 3f);
            return false;
        }

        void SummonBoss()
        {
            if (_bossSummoned || _bossDefeated) return;
            var spawner = C != null ? C.bossSpawner : null;
            if (spawner == null || _executor == null)
            {
                Debug.LogWarning("V4 stage: StageContentConfig.bossSpawner is not set; the Beacon can't summon the boss.");
                return;
            }

            if (_encounters.TryGetOpenBossEncounter(out var open))
            {
                _bossEncounterId = open;
                _executor.ProcessNow();
                if (_encounters.Exists(open) && _encounters.SpawnedCount(open) > 0 && _encounters.State(open) != EncounterState.Failed)
                {
                    _bossSummoned = true;
                    ConsumeBossBeaconMarker();
                    _run.SetState(RunState.BossFight);
                    _rewards?.BossSummonedTelemetry(_bossEncounterId);
                    Publish?.Invoke(CombatEventKind.BossState, 1, _bossEncounterId, default, null);
                }
                return;
            }

            var encounter = _encounters.Request(EncounterKind.Boss, 1);
            _bossEncounterId = encounter;
            _executor.TriggerImmediate(spawner, 1, encounter);
            if (!_encounters.Exists(encounter) || _encounters.State(encounter) == EncounterState.Failed)
            {
                _executor.Cancel(_bossEncounterId);
                _bossEncounterId = 0;
                return;
            }
            if (_encounters.SpawnedCount(encounter) <= 0) return;

            _bossSummoned = true;
            _rewards?.BossSummonedTelemetry(_bossEncounterId);
            ConsumeBossBeaconMarker();
            _run.SetState(RunState.BossFight);
            Publish?.Invoke(CombatEventKind.BossState, 1, _bossEncounterId, default, null);
        }

        void ConsumeBossBeaconMarker()
        {
            if (_layout == null) return;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_layout.pois[i].type != POIType.BossBeacon || _layout.pois[i].used) continue;
                ConsumePoiMarker(i);
                return;
            }
        }

        /// <summary>
        /// R-102 fix (off = V3): V3's level-up resume always set the run to Playing, ending the boss fight while the boss
        /// lived (ambient spawns lost the boss-fight rate and elite beats were no longer skipped). Put it back to
        /// BossFight on the next stage tick while the summoned boss is alive.
        /// </summary>
        void RestoreBossFightAfterResume()
        {
            if (C == null || !C.bossFightSurvivesLevelUp) return;
            if (!_bossSummoned || IsBossDead()) return;
            if (!_run.Exists || _run.IsPaused || _run.IsRunEnded || _run.State != RunState.Playing) return;
            _run.SetState(RunState.BossFight);
        }

        public bool IsBossDead() => _bossDefeated || (_bossSummoned && _encounters.BossObjectiveSecured);

        /// <summary>
        /// Module Lab: a boss victory there is not saved (no home-seed lock and no boss-victory Marks), so the lab cannot
        /// change later runs.
        /// </summary>
        public bool LabMode { get; set; }

        /// <summary>
        /// Module Lab "Clear all" removed the summoned boss without killing it: forget the summon (its encounter fails) so
        /// the run leaves the boss fight and the boss can be summoned again.
        /// </summary>
        public void ForgetDespawnedBoss()
        {
            if (!_bossSummoned || _bossDefeated) return;
            _executor?.Cancel(_bossEncounterId);
            if (_bossEncounterId > 0) _encounters.MarkFailed(_bossEncounterId);
            _bossEncounterId = 0;
            _bossSummoned = false;
            if (_run.Exists && _run.State == RunState.BossFight) _run.SetState(RunState.Playing);
        }

        /// <summary>Same-tick boss and player death counts as a win (V3 DEC-024).</summary>
        public void TrySecureIfBossDead()
        {
            if (_bossDefeated || !IsBossDead()) return;
            NotifyBossDefeated();
        }

        /// <summary>
        /// A boss died. V3's kill path called POIManager.NotifyBossDefeated when the boss encounter completed, right
        /// before the kill event; V4 hears the kill event (see LegacyBossKillHook) and checks the same condition.
        /// </summary>
        public void OnBossKilled()
        {
            if (_encounters.BossObjectiveSecured) NotifyBossDefeated();
        }

        public void NotifyBossDefeated()
        {
            if (_bossDefeated) return;
            _bossDefeated = true;
            if (!LabMode) _rewards?.RegisterBossVictory();
            _rewards?.BossDefeatedTelemetry(_bossEncounterId);
            _run.SecureObjective();
            Publish?.Invoke(CombatEventKind.BossState, 2, _bossEncounterId, default, null);
            var continueAfter = C == null || C.continueAfterBossVictory;
            if (continueAfter && _run.Exists && !LabMode) _rewards?.LockHomeSeed(_run.RunSeed);
            _toasts?.Show("TERRITORY OBJECTIVE SECURED — survive or keep farming.", 5f);
            if (continueAfter && _run.Exists && !_run.IsRunEnded)
            {
                _run.SetState(RunState.Playing);
                return;
            }
            if (_run.Exists && _run.IsRunEnded) return;
            _run.SetState(RunState.BossDefeated);
            _run.CompleteRun(true);
        }
    }
}

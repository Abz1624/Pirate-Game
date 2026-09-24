using System;
using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.Save;
using HighSeas.V3;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.Serialization;

namespace HighSeas.POI
{
    public sealed class POIManager : MonoBehaviour
    {
        public const int V3SalvageChestCount = GameConstants.V3SalvageChestPoolSize;

        [SerializeField] GameDatabase database;
        [SerializeField] LootService loot;
        [FormerlySerializedAs("director")]
        [SerializeField] Enemies.EncounterSpawner encounterSpawner;
        [SerializeField] DemoJuiceSettings juiceSettings;
        [SerializeField] DemoPacingSettings pacingSettings;
        [SerializeField] Transform poiRoot;

        World.StageLayout _layout;
        bool _bossSummoned;
        bool _bossDefeated;
        int _paidChestCount;
        bool _continueAfterBossVictory;
        bool _usePlaceholderVisuals;
        bool _useV3ChestPricing;
        bool _spawnAllChestsAtStart;
        float _signalBuoyChargeRequired = SignalBuoyChargeSeconds;
        int _powderCacheEncounterId;
        int _powderCacheTrackerEncounterId;
        int _bossEncounterId;
        bool _powderCacheCleared;
        readonly HashSet<int> _powderCacheAmbushStarted = new();
        readonly Dictionary<int, POIInteractable> _interactables = new();
        readonly HashSet<int> _signalBuoyChargeAnnounced = new();
        readonly List<int> _powderCacheEnemySlots = new();
        readonly List<int> _pendingChestPoiIndices = new();
        int _chestProgressSpawnPaidIndex = -1;
        const int PowderCacheAmbushCount = 3;
        const float PowderCacheTriggerRadius = 8f;
        readonly Dictionary<int, float> _signalBuoyCharges = new();
        readonly HashSet<int> _signalBuoysCompleted = new();
        const float SignalBuoyChargeSeconds = 8f;
        const float SignalBuoyRadius = 4f;

        public int PaidChestCount => _paidChestCount;
        public bool BossSummoned => _bossSummoned;
        public bool IsPowderCacheAmbushStarted(int index) => _powderCacheAmbushStarted.Contains(index);
        public bool BossDefeated => _bossDefeated;

        /// <summary>PlayMode regression: active powder-cache encounter id (0 if none).</summary>
        public int DebugPowderCacheEncounterId => _powderCacheTrackerEncounterId;

#if UNITY_INCLUDE_TESTS
        /// <summary>PlayMode: boss summoned flag without full beacon interaction.</summary>
        public void ConfigureTrackedBossEncounterForTests(int encounterId)
        {
            _bossSummoned = true;
            _bossDefeated = false;
            _bossEncounterId = encounterId;
        }
#endif

        public bool IsPoiConsumed(int index) =>
            _layout != null && index >= 0 && index < _layout.pois.Count && _layout.pois[index].used;

        public void WriteSnapshotFields(RunSnapshotData data)
        {
            if (data == null) return;
            data.paidChestCount = _paidChestCount;
            data.bossSummoned = _bossSummoned;
            data.bossDefeated = _bossDefeated;
            if (_layout == null)
            {
                data.consumedPoiIndices = Array.Empty<int>();
                return;
            }

            var consumed = new List<int>();
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_layout.pois[i].used)
                    consumed.Add(i);
            }

            data.consumedPoiIndices = consumed.ToArray();

            if (_powderCacheTrackerEncounterId > 0)
            {
                var enc = RunEncounterTracker.Get(_powderCacheTrackerEncounterId);
                if (enc != null)
                {
                    data.activePowderCacheEncounterId = enc.EncounterId;
                    data.activePowderCacheState = (int)enc.State;
                    data.activePowderCacheSpawnedCount = enc.SpawnedCount;
                }
            }
        }

        public void SetV3Options(bool continueAfterBossVictory, bool usePlaceholderVisuals, bool useV3ChestPricing = true)
        {
            _continueAfterBossVictory = continueAfterBossVictory;
            _usePlaceholderVisuals = usePlaceholderVisuals;
            _useV3ChestPricing = useV3ChestPricing;
            _spawnAllChestsAtStart = StageBalanceProfileContext.IsActive &&
                                      StageBalanceProfileContext.Export?.Exploration?.DisableGoldTriggeredChestSpawn == true;
            _signalBuoyChargeRequired = StageBalanceProfileContext.IsActive &&
                                        StageBalanceProfileContext.Authoring != null
                ? StageBalanceProfileContext.Authoring.signalBuoyOccupancySeconds
                : SignalBuoyChargeSeconds;
        }

        public void Initialize(World.StageLayout layout, GameDatabase db)
        {
            ClearPoiMarkers();
            ResetEncounterState();
            _layout = layout;
            database = db;
            if (loot == null)
                loot = FindFirstObjectByType<LootService>() ?? gameObject.AddComponent<LootService>();
            if (juiceSettings == null)
                juiceSettings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (pacingSettings == null)
                pacingSettings = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            BuildMarkers();
        }

        void ClearPoiMarkers()
        {
            var root = poiRoot != null ? poiRoot : transform;
            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        void ResetEncounterState()
        {
            _bossSummoned = false;
            _bossDefeated = false;
            _paidChestCount = 0;
            _powderCacheEncounterId = 0;
            _powderCacheTrackerEncounterId = 0;
            _bossEncounterId = 0;
            RunEncounterTracker.ResetForNewRun();
            _powderCacheCleared = false;
            _powderCacheAmbushStarted.Clear();
            _signalBuoyChargeAnnounced.Clear();
            _powderCacheEnemySlots.Clear();
            _signalBuoyCharges.Clear();
            _signalBuoysCompleted.Clear();
            _pendingChestPoiIndices.Clear();
            _chestProgressSpawnPaidIndex = -1;
            _interactables.Clear();
        }

        void BuildMarkers()
        {
            if (_layout == null) return;
            var root = poiRoot != null ? poiRoot : transform;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                var poi = _layout.pois[i];
                if (_useV3ChestPricing && poi.type == POIType.SalvageChest && !_spawnAllChestsAtStart)
                {
                    _pendingChestPoiIndices.Add(i);
                    continue;
                }

                SpawnPoiMarker(i, root);
            }

            if (_useV3ChestPricing)
                TrySpawnNextPendingChest();
        }

        void SpawnPoiMarker(int poiIndex, Transform root)
        {
            if (_layout == null || poiIndex < 0 || poiIndex >= _layout.pois.Count) return;
            if (root == null)
                root = poiRoot != null ? poiRoot : transform;

            var poi = _layout.pois[poiIndex];
            var marker = CreateMarker(poi.type, poi.position, root);
            var trigger = marker.AddComponent<POIInteractable>();
            trigger.Initialize(this, poi.type, poiIndex, juiceSettings, pacingSettings);
            _interactables[poiIndex] = trigger;
        }

        public void HidePoiMarkerVisual(int index)
        {
            if (_interactables.TryGetValue(index, out var interactable) && interactable != null)
                interactable.SetMarkerHidden(true);
        }

        void ConsumePoiMarker(int index)
        {
            MarkPoiUsed(index);
            if (_interactables.TryGetValue(index, out var interactable) && interactable != null)
                interactable.ForceConsume();
        }

        void TickProgressiveChestSpawns()
        {
            if (_spawnAllChestsAtStart) return;
            if (!_useV3ChestPricing || _pendingChestPoiIndices.Count == 0) return;
            var rm = RunManager.Instance;
            if (rm == null || rm.IsRunEnded) return;

            if (_pendingChestPoiIndices.Count > 0 &&
                CountActiveSalvageChestMarkers() == 0)
            {
                TrySpawnNextPendingChest();
                return;
            }

            if (_chestProgressSpawnPaidIndex == _paidChestCount)
                return;

            var nextChestPrice = V3Economy.GetChestPrice(_paidChestCount);
            var threshold = nextChestPrice * GameConstants.V3ChestSpawnGoldFraction;
            if (rm.Gold < threshold)
                return;

            _chestProgressSpawnPaidIndex = _paidChestCount;
            TrySpawnNextPendingChest();
        }

        int CountActiveSalvageChestMarkers()
        {
            if (_layout == null) return 0;
            var count = 0;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_layout.pois[i].type != POIType.SalvageChest || _layout.pois[i].used)
                    continue;
                if (IsChestMarkerSpawned(i))
                    count++;
            }

            return count;
        }

        bool IsChestMarkerSpawned(int poiIndex)
        {
            var root = poiRoot != null ? poiRoot : transform;
            var triggers = root.GetComponentsInChildren<POIInteractable>(true);
            for (var i = 0; i < triggers.Length; i++)
            {
                if (triggers[i].Index == poiIndex)
                    return true;
            }

            return false;
        }

        void TrySpawnNextPendingChest()
        {
            if (_pendingChestPoiIndices.Count == 0) return;
            var poiIndex = _pendingChestPoiIndices[0];
            _pendingChestPoiIndices.RemoveAt(0);
            SpawnPoiMarker(poiIndex, null);
        }

        GameObject CreateMarker(POIType type, Vector3 position, Transform root)
        {
            var spawnPos = position + Vector3.up * 0.15f;
            var prefab = GetPrefab(type);
            if (prefab != null && (!_usePlaceholderVisuals || type == POIType.SalvageChest))
                return CreatePrefabMarker(type, prefab, position, spawnPos, root);

            if (_usePlaceholderVisuals)
            {
                var marker = V3PlaceholderFactory.CreatePoiMarker(type, spawnPos, root);
                if (type == POIType.SalvageChest)
                    SalvageChestWaterFloat.Attach(marker, juiceSettings);
                return marker;
            }

            GameObject go;
            if (prefab != null)
                return CreatePrefabMarker(type, prefab, position, spawnPos, root);

            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = type.ToString();
            go.transform.SetParent(root, false);
            go.transform.position = spawnPos + Vector3.up * 0.35f;
            go.transform.localScale = GetFallbackScale(type);
            var rend = go.GetComponent<Renderer>();
            rend.material.color = type switch
            {
                POIType.SalvageChest => new Color(0.8f, 0.6f, 0.2f),
                POIType.LighthouseOfFortune => Color.white,
                POIType.TrialBuoy => Color.red,
                POIType.BossBeacon => Color.magenta,
                _ => Color.gray
            };

            StripColliders(go);
            if (type == POIType.SalvageChest)
                SalvageChestWaterFloat.Attach(go, juiceSettings);
            return go;
        }

        GameObject CreatePrefabMarker(POIType type, GameObject prefab, Vector3 waterPos, Vector3 spawnPos, Transform root)
        {
            var go = Instantiate(prefab, spawnPos, Quaternion.identity, root);
            go.name = type.ToString();
            go.transform.localScale *= GetScale(type);

            if (type == POIType.SalvageChest)
            {
                SalvageChestWaterFloat.Attach(go, juiceSettings);
                SalvageChestTreasureGlow.Attach(go, juiceSettings);
            }

            StripColliders(go);
            return go;
        }

        static void StripColliders(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);
        }

        GameObject GetPrefab(POIType type)
        {
            if (juiceSettings == null) return null;
            return type switch
            {
                POIType.SalvageChest => juiceSettings.salvageChestPrefab,
                POIType.LighthouseOfFortune => juiceSettings.lighthousePrefab,
                POIType.TrialBuoy => juiceSettings.trialBuoyPrefab,
                POIType.BossBeacon => juiceSettings.bossBeaconPrefab,
                _ => null
            };
        }

        float GetScale(POIType type)
        {
            if (juiceSettings == null) return 1f;
            return type switch
            {
                POIType.SalvageChest => juiceSettings.salvageChestScale,
                POIType.LighthouseOfFortune => juiceSettings.lighthouseScale,
                POIType.TrialBuoy => juiceSettings.trialBuoyScale,
                POIType.BossBeacon => juiceSettings.bossBeaconScale,
                _ => 1f
            };
        }

        static Vector3 GetFallbackScale(POIType type) =>
            type switch
            {
                POIType.SalvageChest => new Vector3(1.6f, 0.8f, 1.6f),
                POIType.LighthouseOfFortune => new Vector3(1.2f, 2f, 1.2f),
                POIType.TrialBuoy => new Vector3(1f, 1.2f, 1f),
                POIType.BossBeacon => new Vector3(2.2f, 0.2f, 2.2f),
                _ => new Vector3(2f, 1f, 2f)
            };

        public bool Interact(POIType type, int index)
        {
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return false;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return false;

            switch (type)
            {
                case POIType.SalvageChest:
                    if (!TryOpenSalvageChest(player, index))
                        return false;
                    ConsumePoiMarker(index);
                    return true;
                case POIType.LighthouseOfFortune:
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Damage, 0f, 1.08f));
                    break;
                case POIType.TrialBuoy:
                    return TryCompleteSignalBuoy(player, index);
                case POIType.GildedWreck:
                    if (player.Damage.CurrentHP >= RunManager.Instance.Stats.MaxHull - 0.5f)
                    {
                        V3ToastHud.Instance?.Show("Hull already full — wreck unchanged.", 2f);
                        return false;
                    }

                    var missingHull = RunManager.Instance.Stats.MaxHull - player.Damage.CurrentHP;
                    player.Damage.Heal(Mathf.Min(RunManager.Instance.Stats.MaxHull * 0.25f, missingHull));
                    V3ToastHud.Instance?.Show("Repair Wreck — hull restored 25%.", 2.5f);
                    break;
                case POIType.MerchantSkiff:
                    return TryCompletePowderCache(player, index);
                case POIType.CursedIdol:
                    V3DiscoverySystem.Instance?.RevealNearestChests(3);
                    var bearing = V3DiscoverySystem.Instance?.GetBeaconBearingDegrees(player.Position) ?? 0f;
                    V3ToastHud.Instance?.Show(
                        $"Treasure Chart — chests revealed; beacon bearing {bearing:0}°.",
                        3f);
                    break;
                case POIType.BossBeacon:
                    return TrySummonBossAtBeacon(player);
                default:
                    return false;
            }

            MarkPoiUsed(index);
            RunCombatTelemetry.Instance?.RecordPoiInteraction(type.ToString());
            return true;
        }

        Enemies.EncounterSpawner ResolveEncounterSpawner() =>
            encounterSpawner != null ? encounterSpawner : FindFirstObjectByType<Enemies.EncounterSpawner>();

        /// <summary>PlayMode harness: bind the runtime spawner created alongside POIManager.</summary>
        public void BindEncounterSpawner(Enemies.EncounterSpawner spawner) => encounterSpawner = spawner;

        public int GetSalvageChestQuotePrice()
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                return SaltwindStageEconomyLogic.QuoteChestPrice(
                    StageBalanceProfileContext.Export,
                    StageBalanceProfileContext.Runtime.SuccessfulChestPurchases);
            }

            return V3Economy.GetChestPrice(_paidChestCount);
        }

        public bool CanAffordSalvageChest()
        {
            var rm = RunManager.Instance;
            return rm != null && rm.Gold >= GetSalvageChestQuotePrice();
        }

        public string GetSalvageChestPromptLabel() =>
            $"Open Salvage Chest — {GetSalvageChestQuotePrice()} gold";

        static IReadOnlyList<string> CollectEquippedWeaponIds(PlayerShipController player)
        {
            if (player == null) return null;
            var controller = player.GetComponent<WeaponController>();
            if (controller == null || controller.Weapons == null || controller.Weapons.Count == 0)
                return null;

            var ids = new List<string>(controller.Weapons.Count);
            for (var i = 0; i < controller.Weapons.Count; i++)
            {
                var def = controller.Weapons[i].def;
                if (def != null && !string.IsNullOrEmpty(def.id))
                    ids.Add(def.id);
            }

            return ids.Count > 0 ? ids : null;
        }

        bool TryOpenSalvageChest(PlayerShipController player, int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count || _layout.pois[index].used)
                return false;

            var rm = RunManager.Instance;
            if (rm == null) return false;

            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
            {
                var ownedWeapons = CollectEquippedWeaponIds(player);
                var plan = SaltwindStageEconomyLogic.TryPlanChestPurchase(
                    StageBalanceProfileContext.Export,
                    StageBalanceProfileContext.Runtime.SuccessfulChestPurchases,
                    rm.Gold,
                    rm.Rng,
                    rm.Stats.Luck,
                    AppRoot.Instance?.Save,
                    ownedWeapons);
                if (!plan.IsValid)
                {
                    if (plan.Error == "insufficient gold")
                        V3ToastHud.Instance?.Show($"Need {plan.Price} run gold to open chest.", 2f);
                    else if (plan.Error == "no eligible loot")
                        V3ToastHud.Instance?.Show("Chest unavailable — no eligible reward.", 2f);
                    return false;
                }

                if (!rm.TrySpendGold(plan.Price))
                {
                    V3ToastHud.Instance?.Show($"Need {plan.Price} run gold to open chest.", 2f);
                    return false;
                }

                StageBalanceProfileContext.Runtime.RegisterSuccessfulChestPurchase();
                _paidChestCount++;
                if (!V3RunItemInventory.Instance.TryAddItem(plan.ItemId))
                {
                    rm.AddGold(plan.Price);
                    V3ToastHud.Instance?.Show("Inventory full — chest unchanged.", 2.5f);
                    return false;
                }

                V3ToastHud.Instance?.Show($"Salvage Chest — gained {V3ItemCatalog.GetDisplayName(plan.ItemId)}.", 2.5f);
                TriggerBus.Instance?.RaiseChestOpen();
                RunCombatTelemetry.Instance?.RecordPoiInteraction(POIType.SalvageChest.ToString());
                return true;
            }

            var spawner = ResolveEncounterSpawner();
            var price = _useV3ChestPricing
                ? V3Economy.GetChestPrice(_paidChestCount)
                : spawner != null ? spawner.GetChestPrice() : 25;
            if (!_useV3ChestPricing && _paidChestCount == 0 && pacingSettings != null)
                price = Mathf.RoundToInt(price * (1f - pacingSettings.firstChestDiscount));

            if (!rm.TrySpendGold(price))
            {
                V3ToastHud.Instance?.Show($"Need {price} run gold to open chest.", 2f);
                return false;
            }

            _paidChestCount++;
            if (_useV3ChestPricing || V3RunContext.IsActive)
            {
                var itemId = V3ChestLoot.RollSalvageItem(rm.Rng, rm.Stats.Luck);
                V3RunItemInventory.Instance?.TryAddItem(itemId);
            }
            else
            {
                var relic = loot != null
                    ? loot.RollRelic(rm.Rng, rm.Stats.Luck)
                    : null;
                if (relic != null)
                    TriggerBus.Instance?.AddRelic(relic);
            }

            TriggerBus.Instance?.RaiseChestOpen();
            RunCombatTelemetry.Instance?.RecordPoiInteraction(POIType.SalvageChest.ToString());
            return true;
        }

        public void TickPowderCacheProximity(PlayerShipController player, int index)
        {
            if (player == null) return;
            TickPowderCacheProximityAt(player.Position, index);
        }

        /// <summary>PlayMode: avoid spinning up full ship colliders when only position matters.</summary>
        public void TickPowderCacheProximityAt(Vector3 playerPosition, int index)
        {
            if (_powderCacheAmbushStarted.Contains(index))
                return;

            if (Vector3.Distance(playerPosition, GetPoiPosition(index)) > PowderCacheTriggerRadius)
                return;

            if (!SpawnPowderCacheFight(GetPoiPosition(index)))
            {
                V3ToastHud.Instance?.Show("Powder Cache — ambush failed to spawn, retrying...", 2f);
                return;
            }

            _powderCacheAmbushStarted.Add(index);
            _powderCacheEncounterId++;
            HidePoiMarkerVisual(index);
            V3ToastHud.Instance?.Show("Powder Cache — ambush incoming!", 3.5f);
        }

        bool TryCompletePowderCache(PlayerShipController player, int index)
        {
            if (!_powderCacheAmbushStarted.Contains(index))
            {
                TickPowderCacheProximity(player, index);
                return false;
            }

            if (!IsPowderCacheEncounterClear())
            {
                var record = _powderCacheTrackerEncounterId > 0
                    ? RunEncounterTracker.Get(_powderCacheTrackerEncounterId)
                    : null;
                if (record != null && record.State == RunEncounterState.Failed)
                {
                    if (SpawnPowderCacheFight(GetPoiPosition(index)))
                        V3ToastHud.Instance?.Show("Powder Cache — ambush retry!", 2.5f);
                    else
                        V3ToastHud.Instance?.Show("Powder Cache — could not spawn reinforcements.", 2f);
                }

                return false;
            }

            var itemId = RollPowderCacheItem();
            V3RunItemInventory.Instance?.TryAddItem(itemId);
            RunManager.Instance.AddGold(
                StageBalanceProfileContext.Export?.Rewards?.PowderCacheBonusGold ?? 20);
            MarkPoiUsed(index);
            RunCombatTelemetry.Instance?.RecordPoiInteraction(POIType.MerchantSkiff.ToString());
            return true;
        }

        bool IsPowderCacheEncounterClear()
        {
            if (_powderCacheTrackerEncounterId > 0)
            {
                var record = RunEncounterTracker.Get(_powderCacheTrackerEncounterId);
                return record != null && record.State == RunEncounterState.Completed;
            }

            var spawner = ResolveEncounterSpawner();
            var profile = spawner?.GetScheduleProfile("powder_cache");
            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null) return true;

            if (profile != null)
            {
                for (var i = 0; i < world.Agents.Length; i++)
                {
                    var agent = world.Agents[i];
                    if (agent.active && agent.spawnProfile == profile)
                        return false;
                }

                return true;
            }

            if (_powderCacheEnemySlots.Count == 0)
                return false;

            for (var i = 0; i < _powderCacheEnemySlots.Count; i++)
            {
                var slot = _powderCacheEnemySlots[i];
                if (slot < 0 || slot >= world.Agents.Length) continue;
                if (world.Agents[slot].active)
                    return false;
            }

            return true;
        }

        public float GetSignalBuoyCharge(int index) =>
            _signalBuoyCharges.TryGetValue(index, out var charge) ? charge : 0f;

        public float GetSignalBuoyCharge01(int index) =>
            Mathf.Clamp01(GetSignalBuoyCharge(index) / _signalBuoyChargeRequired);

        public float SignalBuoyRadiusMeters => SignalBuoyRadius;

        bool TryCompleteSignalBuoy(PlayerShipController player, int index)
        {
            if (_signalBuoysCompleted.Contains(index))
                return false;

            if (Vector3.Distance(player.Position, GetPoiPosition(index)) > SignalBuoyRadius)
                return false;

            if (GetSignalBuoyCharge(index) < _signalBuoyChargeRequired)
                return false;

            ApplySignalBuoyReward();
            _signalBuoysCompleted.Add(index);
            MarkPoiUsed(index);
            RunCombatTelemetry.Instance?.RecordPoiInteraction(POIType.TrialBuoy.ToString());
            return true;
        }

        void ApplySignalBuoyReward()
        {
            var choices = new (StatModifier mod, string label)[]
            {
                (new StatModifier(StatId.Damage, 0f, 1.1f), "+10% damage for this run"),
                (new StatModifier(StatId.MoveSpeed, 0.5f), "+0.5 move speed for this run"),
                (new StatModifier(StatId.PickupRadius, 0.8f), "+0.8 pickup radius for this run")
            };
            var pick = choices[RunManager.Instance.Rng.MapInt(0, choices.Length)];
            RunManager.Instance.Stats.AddModifier(pick.mod);
            V3ToastHud.Instance?.Show($"Signal Buoy charged!\n<size=80%>{pick.label}</size>", 5f);
        }

        public void AnnounceSignalBuoyCharge(int index)
        {
            if (_signalBuoyChargeAnnounced.Contains(index))
                return;

            _signalBuoyChargeAnnounced.Add(index);
            V3ToastHud.Instance?.Show("Signal Buoy charging — stay in the circle!", 3f);
        }

        static string RollPowderCacheItem()
        {
            var pool = V3ItemCatalog.ChestPool;
            var idx = RunManager.Instance.Rng.MapInt(0, pool.Count);
            return pool[idx].id;
        }

        Vector3 GetPoiPosition(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count)
                return Vector3.zero;
            return _layout.pois[index].position;
        }

        void MarkPoiUsed(int index)
        {
            if (index < 0 || index >= _layout.pois.Count) return;
            var p = _layout.pois[index];
            p.used = true;
            _layout.pois[index] = p;
            RunMarksTracker.TryRegisterSpecialPoi(index, p.type);
        }

        bool SpawnPowderCacheFight(Vector3 poiPosition)
        {
            var spawner = ResolveEncounterSpawner();
            var profile = spawner?.GetScheduleProfile("powder_cache");
            if (spawner == null || profile == null)
            {
                Debug.LogWarning(
                    "POIManager: Assign powderCacheAmbush on EncounterSpawnSchedule to spawn powder cache fights.");
                return false;
            }

            _powderCacheEnemySlots.Clear();
            var encounter = RunEncounterTracker.Request(RunEncounterKind.PowderCache, PowderCacheAmbushCount);
            _powderCacheTrackerEncounterId = encounter.EncounterId;
            spawner.TriggerProfile(profile, PowderCacheAmbushCount, encounter.EncounterId);
            return true;
        }

        public void TickSignalBuoyCharge(PlayerShipController player, int index)
        {
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            if (_signalBuoysCompleted.Contains(index) || player == null) return;
            if (!_signalBuoyCharges.ContainsKey(index))
                _signalBuoyCharges[index] = 0f;

            if (Vector3.Distance(player.Position, GetPoiPosition(index)) > SignalBuoyRadius)
                return;

            if (_signalBuoyCharges[index] <= 0.05f)
                AnnounceSignalBuoyCharge(index);

            _signalBuoyCharges[index] += Time.deltaTime;
            if (_signalBuoyCharges[index] >= _signalBuoyChargeRequired)
                TryCompleteSignalBuoy(player, index);
        }

        /// <summary>Summon the treasury boss (e.g. hotkey when no beacon POI is in the world).</summary>
        public bool TrySummonBoss()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded)
                return false;
            if (_bossDefeated)
            {
                V3ToastHud.Instance?.Show("Objective already secured.", 2f);
                return false;
            }

            if (_bossSummoned)
            {
                V3ToastHud.Instance?.Show("Boss already summoned.", 2f);
                return false;
            }

            var player = FindFirstObjectByType<PlayerShipController>();
            if (!TrySummonBossAtBeacon(player))
                return false;

            V3ToastHud.Instance?.Show("Treasury Beacon — boss incoming! (B)", 3f);
            return true;
        }

        bool TrySummonBossAtBeacon(PlayerShipController player)
        {
            if (_bossSummoned)
            {
                V3ToastHud.Instance?.Show("Boss already summoned.", 2f);
                return true;
            }

            if (_bossDefeated)
            {
                V3ToastHud.Instance?.Show("Objective already secured.", 2f);
                return false;
            }

            SummonBoss(player);
            if (_bossSummoned)
                return true;

            V3ToastHud.Instance?.Show("Beacon failed — clear space near the beacon and try again.", 3f);
            return false;
        }

        void SummonBoss(PlayerShipController player)
        {
            if (_bossSummoned || _bossDefeated) return;

            var spawner = ResolveEncounterSpawner();
            var profile = spawner?.GetScheduleProfile("boss");
            if (spawner == null || profile == null)
            {
                Debug.LogWarning(
                    "POIManager: Assign bossSummon on EncounterSpawnSchedule to spawn the treasury boss.");
                return;
            }

            if (RunEncounterTracker.TryGetOpenBossEncounter(out var open))
            {
                _bossEncounterId = open.EncounterId;
                spawner.ProcessPendingEncountersNow();
                open = RunEncounterTracker.Get(open.EncounterId);
                if (open != null && open.SpawnedCount > 0 && open.State != RunEncounterState.Failed)
                {
                    _bossSummoned = true;
                    ConsumeBossBeaconMarker();
                    RunManager.Instance.SetState(RunState.BossFight);
                    RunCombatTelemetry.Instance?.RecordBossSummoned(_bossEncounterId);
                }

                return;
            }

            var encounter = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            _bossEncounterId = encounter.EncounterId;
            spawner.TriggerProfileImmediate(profile, 1, encounter.EncounterId);
            encounter = RunEncounterTracker.Get(encounter.EncounterId);
            if (encounter == null || encounter.State == RunEncounterState.Failed)
            {
                spawner.CancelPendingEncounter(_bossEncounterId);
                _bossEncounterId = 0;
                return;
            }

            if (encounter.SpawnedCount <= 0)
                return;

            _bossSummoned = true;
            RunCombatTelemetry.Instance?.RecordBossSummoned(_bossEncounterId);
            ConsumeBossBeaconMarker();
            RunManager.Instance.SetState(RunState.BossFight);
        }

        void ConsumeBossBeaconMarker()
        {
            if (_layout == null) return;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_layout.pois[i].type != POIType.BossBeacon || _layout.pois[i].used)
                    continue;
                ConsumePoiMarker(i);
                return;
            }
        }

        public bool IsBossDead()
        {
            if (_bossDefeated) return true;
            if (!_bossSummoned) return false;
            return RunEncounterTracker.BossObjectiveSecured;
        }

        /// <summary>DEC-024 / M2-031: secure boss objective even when player dies same simulation tick.</summary>
        public void TrySecureIfBossDead()
        {
            if (_bossDefeated) return;
            if (!IsBossDead()) return;
            NotifyBossDefeated();
        }

        public void NotifyBossDefeated()
        {
            if (_bossDefeated) return;
            _bossDefeated = true;
            RunMarksTracker.TryRegisterBossVictory();
            RunCombatTelemetry.Instance?.RecordBossDefeated(_bossEncounterId);
            RunManager.Instance.SecureObjective();
            if (_continueAfterBossVictory && RunManager.Instance != null)
                V3HomeSeed.TryLockFromRun(RunManager.Instance.RunSeed);
            V3.V3ToastHud.Instance?.Show("TERRITORY OBJECTIVE SECURED — survive or keep farming.", 5f);
            if (_continueAfterBossVictory && RunManager.Instance != null && !RunManager.Instance.IsRunEnded)
            {
                RunManager.Instance.SetState(RunState.Playing);
                return;
            }

            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded)
                return;

            RunManager.Instance.SetState(RunState.BossDefeated);
            RunManager.Instance.CompleteRun(true);
        }

        void Update()
        {
            TickProgressiveChestSpawns();

            if (!_powderCacheCleared && _powderCacheEncounterId > 0 && IsPowderCacheEncounterClear())
                _powderCacheCleared = true;
        }
    }

    public sealed class POIInteractable : MonoBehaviour
    {
        POIManager _manager;
        POIType _type;
        int _index;
        bool _used;
        bool _markerHidden;
        DemoJuiceSettings _juice;
        DemoPacingSettings _pacing;
        bool _telegraphing;
        bool _awaitingBossSummon;
        float _telegraphTimer;
        Transform _telegraphRing;
        Transform _chargeRing;

        public bool CanInteract => !_used && !_markerHidden && _manager != null;
        public bool IsPlayerInRange => GetPlayerDistance() <= InteractRange;
        public bool UsesChargeInteraction => _type == POIType.TrialBuoy;
        public bool UsesProximityAmbush => _type == POIType.MerchantSkiff;
        public bool ShowsSalvageChestPrice => _type == POIType.SalvageChest;
        public int Index => _index;
        public float InteractRange => _type switch
        {
            POIType.TrialBuoy => 4f,
            POIType.BossBeacon => 6f,
            POIType.SalvageChest => 6f,
            _ => V3InteractionController.DefaultRange
        };
        public Vector3 WorldPosition => transform.position;
        public float RequiredHoldSeconds => _type switch
        {
            POIType.SalvageChest => 0f,
            POIType.BossBeacon => V3InteractionController.BossHoldSeconds,
            POIType.TrialBuoy => 0f,
            _ => 0f
        };

        public string PromptLabel => _type switch
        {
            POIType.SalvageChest => _manager?.GetSalvageChestPromptLabel() ?? "Open Salvage Chest",
            POIType.GildedWreck => "Repair Wreck",
            POIType.MerchantSkiff => "Powder Cache",
            POIType.TrialBuoy => "Signal Buoy",
            POIType.CursedIdol => "Treasure Chart",
            POIType.BossBeacon => "Activate Treasury Beacon",
            _ => _type.ToString()
        };

        public void Initialize(
            POIManager manager,
            POIType type,
            int index,
            DemoJuiceSettings juice,
            DemoPacingSettings pacing)
        {
            _manager = manager;
            _type = type;
            _index = index;
            _juice = juice;
            _pacing = pacing;
        }

        void Update()
        {
            if (_used) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            if (_type == POIType.TrialBuoy)
            {
                var player = FindFirstObjectByType<PlayerShipController>();
                _manager?.TickSignalBuoyCharge(player, _index);
                UpdateChargeRing();
                if (_manager != null && _manager.IsPoiConsumed(_index))
                    Consume();
            }

            if (_type == POIType.MerchantSkiff)
            {
                var player = FindFirstObjectByType<PlayerShipController>();
                _manager?.TickPowderCacheProximity(player, _index);
            }

            if (_awaitingBossSummon)
            {
                _telegraphTimer -= Time.deltaTime;
                PulseTelegraph();
                if (_telegraphTimer > 0f) return;
                _awaitingBossSummon = false;
                if (_manager != null && _manager.Interact(_type, _index))
                    Consume();
            }
        }

        public void TryCompleteInteraction()
        {
            if (_used || _manager == null) return;

            if (_type == POIType.BossBeacon)
            {
                if (!_telegraphing)
                {
                    _telegraphing = true;
                    _awaitingBossSummon = true;
                    _telegraphTimer = _pacing != null ? _pacing.bossTelegraphSeconds : 0.7f;
                    ShowTelegraph();
                }

                return;
            }

            if (_manager.Interact(_type, _index))
                Consume();
        }

        void Consume()
        {
            _used = true;
            HideChargeRing();
            HideTelegraph();
            gameObject.SetActive(false);
        }

        public void ForceConsume() => Consume();

        public void SetMarkerHidden(bool hidden)
        {
            _markerHidden = hidden;
            if (!hidden) return;
            HideChargeRing();
            HideTelegraph();
            for (var i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(false);
        }

        float GetPlayerDistance()
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return float.MaxValue;
            return Vector3.Distance(player.Position, transform.position);
        }

        void ShowTelegraph()
        {
            if (_telegraphRing != null) return;
            var scale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
            GameObject ring;
            if (_juice?.bossTelegraphRingPrefab != null)
            {
                ring = Instantiate(_juice.bossTelegraphRingPrefab, transform.position, Quaternion.identity, transform);
            }
            else
            {
                ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                var rend = ring.GetComponent<Renderer>();
                rend.material.color = new Color(1f, 0.2f, 0.9f, 0.65f);
                var col = ring.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            ring.name = "BossTelegraph";
            ring.transform.localScale = new Vector3(scale, 0.05f, scale);
            _telegraphRing = ring.transform;
        }

        void PulseTelegraph()
        {
            if (_telegraphRing == null) return;
            var pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.08f;
            var baseScale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
            _telegraphRing.localScale = new Vector3(baseScale * pulse, 0.05f, baseScale * pulse);
        }

        void HideTelegraph()
        {
            if (_telegraphRing == null) return;
            Destroy(_telegraphRing.gameObject);
            _telegraphRing = null;
            _telegraphing = false;
            _telegraphTimer = 0f;
        }

        void UpdateChargeRing()
        {
            if (_manager == null) return;
            var charge01 = _manager.GetSignalBuoyCharge01(_index);
            if (_chargeRing == null)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "SignalBuoyChargeRing";
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                var rend = ring.GetComponent<Renderer>();
                rend.material.color = new Color(0.2f, 0.75f, 1f, 0.45f);
                var col = ring.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _chargeRing = ring.transform;
            }

            var radius = _manager.SignalBuoyRadiusMeters * 2f;
            var pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
            _chargeRing.localScale = new Vector3(radius * pulse, 0.04f, radius * pulse);
            if (_chargeRing.TryGetComponent(out Renderer renderer))
            {
                var alpha = Mathf.Lerp(0.2f, 0.75f, charge01);
                renderer.material.color = new Color(0.2f, 0.75f, 1f, alpha);
            }
        }

        void HideChargeRing()
        {
            if (_chargeRing == null) return;
            Destroy(_chargeRing.gameObject);
            _chargeRing = null;
        }
    }
}

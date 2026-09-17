using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Meta;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.POI
{
    public sealed class POIManager : MonoBehaviour
    {
        public const int V3SalvageChestCount = 12;

        [SerializeField] GameDatabase database;
        [SerializeField] LootService loot;
        [SerializeField] Enemies.SpawnDirector director;
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
        int _powderCacheEncounterId;
        bool _powderCacheCleared;
        readonly HashSet<int> _powderCacheAmbushStarted = new();
        readonly HashSet<int> _signalBuoyChargeAnnounced = new();
        readonly List<int> _powderCacheEnemySlots = new();
        const int PowderCacheAmbushCount = 3;
        const int PowderCacheMaxThreat = 4;
        const float PowderCacheTriggerRadius = 8f;
        readonly Dictionary<int, float> _signalBuoyCharges = new();
        readonly HashSet<int> _signalBuoysCompleted = new();
        const float SignalBuoyChargeSeconds = 8f;
        const float SignalBuoyRadius = 4f;

        public int PaidChestCount => _paidChestCount;
        public bool BossSummoned => _bossSummoned;
        public bool BossDefeated => _bossDefeated;

        public bool IsPoiConsumed(int index) =>
            _layout != null && index >= 0 && index < _layout.pois.Count && _layout.pois[index].used;

        public void SetV3Options(bool continueAfterBossVictory, bool usePlaceholderVisuals, bool useV3ChestPricing = true)
        {
            _continueAfterBossVictory = continueAfterBossVictory;
            _usePlaceholderVisuals = usePlaceholderVisuals;
            _useV3ChestPricing = useV3ChestPricing;
        }

        public void Initialize(World.StageLayout layout, GameDatabase db)
        {
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

        void BuildMarkers()
        {
            if (_layout == null) return;
            var root = poiRoot != null ? poiRoot : transform;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                var poi = _layout.pois[i];
                if (poi.type != POIType.SalvageChest)
                    continue;
                var marker = CreateMarker(poi.type, poi.position, root);
                var trigger = marker.AddComponent<POIInteractable>();
                trigger.Initialize(this, poi.type, i, juiceSettings, pacingSettings);
            }
        }

        GameObject CreateMarker(POIType type, Vector3 position, Transform root)
        {
            var spawnPos = position + Vector3.up * 0.15f;
            var prefab = GetPrefab(type);
            if (prefab != null && (!_usePlaceholderVisuals || type == POIType.SalvageChest))
                return CreatePrefabMarker(type, prefab, position, spawnPos, root);

            if (_usePlaceholderVisuals)
                return V3PlaceholderFactory.CreatePoiMarker(type, spawnPos, root);

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
            return go;
        }

        GameObject CreatePrefabMarker(POIType type, GameObject prefab, Vector3 waterPos, Vector3 spawnPos, Transform root)
        {
            var go = Instantiate(prefab, spawnPos, Quaternion.identity, root);
            go.name = type.ToString();
            go.transform.localScale *= GetScale(type);

            if (type == POIType.SalvageChest)
            {
                AlignBottomToWater(go, waterPos.y + 0.05f);
                SalvageChestTreasureGlow.Attach(go, juiceSettings);
            }

            StripColliders(go);
            return go;
        }

        static void AlignBottomToWater(GameObject go, float waterY)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            go.transform.position += Vector3.up * (waterY - bounds.min.y);
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
                    return TryOpenSalvageChest(player, index);
                case POIType.LighthouseOfFortune:
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Damage, 0f, 1.08f));
                    break;
                case POIType.TrialBuoy:
                    return TryCompleteSignalBuoy(player, index);
                case POIType.GildedWreck:
                    player.Damage.Heal(RunManager.Instance.Stats.MaxHull * 0.25f);
                    V3ToastHud.Instance?.Show("Repair Wreck — hull restored 25%.", 2.5f);
                    break;
                case POIType.MerchantSkiff:
                    return TryCompletePowderCache(player, index);
                case POIType.CursedIdol:
                    V3DiscoverySystem.Instance?.RevealNearestChests(3);
                    V3ToastHud.Instance?.Show("Treasure Chart — nearest chests revealed.", 3f);
                    break;
                case POIType.BossBeacon:
                    SummonBoss(player);
                    break;
                default:
                    return false;
            }

            MarkPoiUsed(index);
            return true;
        }

        bool TryOpenSalvageChest(PlayerShipController player, int index)
        {
            var price = _useV3ChestPricing
                ? V3Economy.GetChestPrice(_paidChestCount)
                : director != null ? director.GetChestPrice() : 25;
            if (!_useV3ChestPricing && _paidChestCount == 0 && pacingSettings != null)
                price = Mathf.RoundToInt(price * (1f - pacingSettings.firstChestDiscount));

            if (!RunManager.Instance.TrySpendGold(price))
            {
                V3ToastHud.Instance?.Show($"Need {price} run gold to open chest.", 2f);
                return false;
            }

            _paidChestCount++;
            if (_useV3ChestPricing || V3RunContext.IsActive)
            {
                var itemId = V3ChestLoot.RollSalvageItem(
                    RunManager.Instance.Rng,
                    RunManager.Instance.Stats.Luck);
                V3RunItemInventory.Instance?.TryAddItem(itemId);
            }
            else
            {
                var relic = loot != null
                    ? loot.RollRelic(RunManager.Instance.Rng, RunManager.Instance.Stats.Luck)
                    : null;
                if (relic != null)
                    TriggerBus.Instance?.AddRelic(relic);
            }

            TriggerBus.Instance?.RaiseChestOpen();
            MarkPoiUsed(index);
            return true;
        }

        public void TickPowderCacheProximity(PlayerShipController player, int index)
        {
            if (player == null || _powderCacheAmbushStarted.Contains(index))
                return;

            if (Vector3.Distance(player.Position, GetPoiPosition(index)) > PowderCacheTriggerRadius)
                return;

            if (!SpawnPowderCacheFight(GetPoiPosition(index)))
            {
                V3ToastHud.Instance?.Show("Powder Cache — ambush failed to spawn, retrying...", 2f);
                return;
            }

            _powderCacheAmbushStarted.Add(index);
            _powderCacheEncounterId++;
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
                V3ToastHud.Instance?.Show("Powder Cache — defeat the ambush first.", 2f);
                return false;
            }

            var itemId = RollPowderCacheItem();
            V3RunItemInventory.Instance?.TryAddItem(itemId);
            RunManager.Instance.AddGold(20);
            MarkPoiUsed(index);
            return true;
        }

        bool IsPowderCacheEncounterClear()
        {
            if (_powderCacheEnemySlots.Count == 0)
                return false;

            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null) return true;

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
            Mathf.Clamp01(GetSignalBuoyCharge(index) / SignalBuoyChargeSeconds);

        public float SignalBuoyRadiusMeters => SignalBuoyRadius;

        bool TryCompleteSignalBuoy(PlayerShipController player, int index)
        {
            if (_signalBuoysCompleted.Contains(index))
                return false;

            if (Vector3.Distance(player.Position, GetPoiPosition(index)) > SignalBuoyRadius)
                return false;

            if (GetSignalBuoyCharge(index) < SignalBuoyChargeSeconds)
                return false;

            ApplySignalBuoyReward();
            _signalBuoysCompleted.Add(index);
            MarkPoiUsed(index);
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
        }

        bool SpawnPowderCacheFight(Vector3 poiPosition)
        {
            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null || world.Definitions == null) return false;

            _powderCacheEnemySlots.Clear();
            var spawnPos = World.PlayBounds.Clamp(poiPosition, 3f);
            var candidates = new List<int>();
            for (var i = 0; i < world.Definitions.Length; i++)
            {
                var def = world.Definitions[i];
                if (def.isBoss || def.isMiniboss) continue;
                if (def.threatCost > PowderCacheMaxThreat) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < world.Definitions.Length; i++)
                {
                    if (!world.Definitions[i].isBoss && !world.Definitions[i].isMiniboss)
                        candidates.Add(i);
                }
            }

            var rng = RunManager.Instance?.Rng;
            var spawned = 0;
            while (spawned < PowderCacheAmbushCount && candidates.Count > 0)
            {
                var pick = rng != null ? rng.MapInt(0, candidates.Count) : spawned % candidates.Count;
                var defIndex = candidates[pick];
                var offset = Quaternion.Euler(0f, spawned * (360f / PowderCacheAmbushCount), 0f) * Vector3.forward * 9f;
                var pos = World.PlayBounds.Clamp(spawnPos + offset, 2f);
                if (!World.PlayBounds.IsOpenWater(pos, 2f))
                    pos = spawnPos + offset.normalized * 6f;
                var slot = world.Spawn(defIndex, pos);
                if (slot >= 0)
                    _powderCacheEnemySlots.Add(slot);
                spawned++;
            }

            return _powderCacheEnemySlots.Count > 0;
        }

        public void TickSignalBuoyCharge(PlayerShipController player, int index)
        {
            if (_signalBuoysCompleted.Contains(index) || player == null) return;
            if (!_signalBuoyCharges.ContainsKey(index))
                _signalBuoyCharges[index] = 0f;

            if (Vector3.Distance(player.Position, GetPoiPosition(index)) > SignalBuoyRadius)
            {
                _signalBuoyCharges[index] = Mathf.Max(0f, _signalBuoyCharges[index] - Time.deltaTime * 0.5f);
                return;
            }

            if (_signalBuoyCharges[index] <= 0.05f)
                AnnounceSignalBuoyCharge(index);

            _signalBuoyCharges[index] += Time.deltaTime;
            if (_signalBuoyCharges[index] >= SignalBuoyChargeSeconds)
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
            SummonBoss(player);
            V3ToastHud.Instance?.Show("Treasury Beacon — boss incoming! (B)", 3f);
            return true;
        }

        void SummonBoss(PlayerShipController player)
        {
            if (_bossSummoned) return;
            _bossSummoned = true;
            RunManager.Instance.SetState(RunState.BossFight);
            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null || world.Definitions == null) return;
            for (var i = 0; i < world.Definitions.Length; i++)
            {
                if (!world.Definitions[i].isBoss) continue;
                var pos = player != null ? player.Position + player.MovementForward * 12f : Vector3.forward * 12f;
                world.Spawn(i, pos, EliteAffix.Armored);
                break;
            }
        }

        public bool IsBossDead()
        {
            if (_bossDefeated) return true;
            if (!_bossSummoned) return false;

            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null) return false;
            for (var i = 0; i < world.Agents.Length; i++)
            {
                if (!world.Agents[i].active) continue;
                if (world.Agents[i].isBoss) return false;
            }

            return true;
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
            if (!_powderCacheCleared && _powderCacheEncounterId > 0 && IsPowderCacheEncounterClear())
                _powderCacheCleared = true;

            if (!_bossSummoned || _bossDefeated) return;
            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null) return;
            var bossAlive = false;
            for (var i = 0; i < world.Agents.Length; i++)
            {
                if (!world.Agents[i].active) continue;
                if (world.Agents[i].isBoss) { bossAlive = true; break; }
            }
            if (!bossAlive) NotifyBossDefeated();
        }
    }

    public sealed class POIInteractable : MonoBehaviour
    {
        POIManager _manager;
        POIType _type;
        int _index;
        bool _used;
        DemoJuiceSettings _juice;
        DemoPacingSettings _pacing;
        bool _telegraphing;
        bool _awaitingBossSummon;
        float _telegraphTimer;
        Transform _telegraphRing;
        Transform _chargeRing;

        public bool CanInteract => !_used && _manager != null;
        public bool IsPlayerInRange => GetPlayerDistance() <= InteractRange;
        public bool UsesChargeInteraction => _type == POIType.TrialBuoy;
        public bool UsesProximityAmbush => _type == POIType.MerchantSkiff;
        public int Index => _index;
        public float InteractRange => _type switch
        {
            POIType.TrialBuoy => 4f,
            POIType.BossBeacon => 6f,
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
            POIType.SalvageChest => "Open Salvage Chest",
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

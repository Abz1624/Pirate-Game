using System;
using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.V3;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    // M7: what the V4 stage content module calls in systems that are still legacy. Each adapter does exactly the call
    // POIManager made, so behaviour and random draws match V3.

    [DeletedInPhase(14, "RunManager is read through the V4 run lifecycle after switch-over")]
    public sealed class LegacyStageRun : IStageRun
    {
        static RunManager Run => RunManager.Instance;
        public bool Exists => Run != null;
        public bool IsRunEnded => Run != null && Run.IsRunEnded;
        public bool IsPaused => Run != null && Run.IsPaused;
        public PauseReason PauseReason => Run != null ? Run.PauseReason : PauseReason.None;
        public RunState State => Run != null ? Run.State : RunState.StageIntro;
        public float ElapsedSeconds => Run != null ? Run.ElapsedSeconds : 0f;
        public int Gold => Run != null ? Run.Gold : 0;
        public int RunSeed => Run != null ? Run.RunSeed : 0;
        public float MaxHull => Run != null ? Run.Stats.MaxHull : 0f;
        public float Luck => Run != null ? Run.Stats.Luck : 0f;
        public void SetState(RunState state) => Run?.SetState(state);
        public void SetPause(PauseReason reason) => Run?.SetPause(reason);
        public void AddGold(int amount) => Run?.AddGold(amount);
        public bool TrySpendGold(int amount) => Run != null && Run.TrySpendGold(amount);
        public void SecureObjective() => Run?.SecureObjective();
        public void CompleteRun(bool victory) => Run?.CompleteRun(victory);
        public void AddStatModifier(int stat, float add, float multiply) => Run?.Stats.AddModifier(new StatModifier((StatId)stat, add, multiply));
    }

    [DeletedInPhase(14, "The ship stays shared infrastructure; its V4 view comes with the world module")]
    public sealed class LegacyStageShip : IStageShip
    {
        PlayerShipController _player;
        readonly Func<PlayerShipController> _find;

        public LegacyStageShip(Func<PlayerShipController> find) => _find = find;

        PlayerShipController Player
        {
            get
            {
                if (_player == null) _player = _find?.Invoke();
                if (_player == null) _player = Object.FindFirstObjectByType<PlayerShipController>();
                return _player;
            }
        }

        public bool Exists => Player != null;
        public Vector3 Position => Player != null ? Player.Position : Vector3.zero;
        public float CurrentHp => Player != null && Player.Damage != null ? Player.Damage.CurrentHP : 0f;
        public void Heal(float amount) => Player?.Damage?.Heal(amount);
    }

    /// <summary>Chest prices and loot (stage profile economy), items (legacy inventory), the map random stream.</summary>
    [DeletedInPhase(9, "ItemModule and the V4 chest economy")]
    public sealed class LegacyStageEconomy : IStageEconomy
    {
        readonly Func<IWeaponManager> _weapons;
        readonly List<string> _equipped = new List<string>();

        public LegacyStageEconomy(Func<IWeaponManager> weapons) => _weapons = weapons;

        public int QuoteChestPrice() =>
            StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null
                ? SaltwindStageEconomyLogic.QuoteChestPrice(StageBalanceProfileContext.Export, StageBalanceProfileContext.Runtime.SuccessfulChestPurchases)
                : V3Economy.GetChestPrice(0);

        public int ProgressChestPrice(int paidChests) => V3Economy.GetChestPrice(paidChests);

        public ChestPlan PlanChest(IReadOnlyList<string> equippedWeaponIds)
        {
            var rm = RunManager.Instance;
            if (rm == null || !StageBalanceProfileContext.IsActive || StageBalanceProfileContext.Export == null)
                return new ChestPlan(false, 0, null, "missing export");
            var plan = SaltwindStageEconomyLogic.TryPlanChestPurchase(StageBalanceProfileContext.Export,
                StageBalanceProfileContext.Runtime.SuccessfulChestPurchases, rm.Gold, rm.Rng, rm.Stats.Luck, AppRoot.Instance?.Save,
                equippedWeaponIds);
            return new ChestPlan(plan.IsValid, plan.Price, plan.ItemId, plan.Error);
        }

        public void RegisterChestPurchase() => StageBalanceProfileContext.Runtime?.RegisterSuccessfulChestPurchase();
        public bool TryAddItem(string itemId) => V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.TryAddItem(itemId);
        public string ItemName(string itemId) => V3ItemCatalog.GetDisplayName(itemId);

        public string RollChestPoolItem()
        {
            var pool = V3ItemCatalog.ChestPool;
            return pool[RunManager.Instance.Rng.MapInt(0, pool.Count)].id;
        }

        public int MapInt(int maxExclusive) => RunManager.Instance.Rng.MapInt(0, maxExclusive);

        public IReadOnlyList<string> EquippedWeaponIds
        {
            get
            {
                _equipped.Clear();
                var weapons = _weapons?.Invoke();
                if (weapons != null)
                    foreach (var slot in weapons.Slots)
                        if (!string.IsNullOrEmpty(slot.Id)) _equipped.Add(slot.Id);
                return _equipped.Count > 0 ? _equipped : null;
            }
        }
    }

    [DeletedInPhase(12, "RewardModule and V4 telemetry")]
    public sealed class LegacyStageRewards : IStageRewards
    {
        public void RegisterSpecialPoi(int poiIndex, POIType type) => RunMarksTracker.TryRegisterSpecialPoi(poiIndex, type);
        public void RegisterBossVictory() => RunMarksTracker.TryRegisterBossVictory();
        public void LockHomeSeed(int runSeed) => V3HomeSeed.TryLockFromRun(runSeed);
        public void ChestOpened() => TriggerBus.Instance?.RaiseChestOpen();
        public void PoiUsedTelemetry(POIType type) => RunCombatTelemetry.Instance?.RecordPoiInteraction(type.ToString());
        public void BossSummonedTelemetry(int encounterId) => RunCombatTelemetry.Instance?.RecordBossSummoned(encounterId);
        public void BossDefeatedTelemetry(int encounterId) => RunCombatTelemetry.Instance?.RecordBossDefeated(encounterId);
    }

    [DeletedInPhase(14, "ToastPresenter")]
    public sealed class LegacyStageToasts : IStageToasts
    {
        public void Show(string text, float seconds) => V3ToastHud.Instance?.Show(text, seconds);
    }

    /// <summary>
    /// The static RunEncounterTracker as V4's encounter records: the legacy enemy world's kill path completes the boss
    /// and Powder Cache encounters there (RegisterAgentKill) until the V4 enemy world (M8).
    /// </summary>
    [DeletedInPhase(8, "The V4 enemy world completes encounters in the V4 tracker")]
    public sealed class LegacyEncounterTracker : IEncounterTracker
    {
        public void ResetForRun() => RunEncounterTracker.ResetForNewRun();

        public int Request(EncounterKind kind, int expectedSpawnCount) =>
            RunEncounterTracker.Request(kind == EncounterKind.Boss ? RunEncounterKind.Boss : RunEncounterKind.PowderCache, expectedSpawnCount).EncounterId;

        public bool Exists(int encounterId) => RunEncounterTracker.Get(encounterId) != null;

        public EncounterState State(int encounterId)
        {
            var record = RunEncounterTracker.Get(encounterId);
            return record == null ? EncounterState.Failed : (EncounterState)(int)record.State;
        }

        public int SpawnedCount(int encounterId) => RunEncounterTracker.Get(encounterId)?.SpawnedCount ?? 0;
        public bool IsTerminal(int encounterId) => RunEncounterTracker.Get(encounterId)?.IsTerminal ?? true;
        public void RegisterSpawned(int encounterId, int unitId) => RunEncounterTracker.RegisterSpawnedAgent(encounterId, unitId);
        public void MarkFailed(int encounterId) => RunEncounterTracker.MarkFailed(encounterId);

        public bool TryGetOpenBossEncounter(out int encounterId)
        {
            var open = RunEncounterTracker.TryGetOpenBossEncounter(out var record);
            encounterId = open ? record.EncounterId : 0;
            return open;
        }

        public bool BossObjectiveSecured => RunEncounterTracker.BossObjectiveSecured;
    }

    /// <summary>
    /// The legacy kill path told POIManager about a boss kill right before raising TriggerBus.Kill; V4 listens to that
    /// event (subscribed first, at run start) and gives the stage module the same moment.
    /// </summary>
    [DeletedInPhase(8, "The V4 kill fanout calls the stage module directly")]
    public sealed class LegacyBossKillHook : IDisposable
    {
        TriggerBus _bus;
        Action _onBossKilled;

        public void Attach(Action onBossKilled)
        {
            Dispose();
            _onBossKilled = onBossKilled;
            _bus = TriggerBus.Instance;
            if (_bus != null) _bus.Kill += OnKill;
        }

        void OnKill(bool elite, bool boss, Vector3 position, string weaponId)
        {
            if (boss) _onBossKilled?.Invoke();
        }

        public void Dispose()
        {
            if (_bus != null) _bus.Kill -= OnKill;
            _bus = null;
        }
    }

    /// <summary>
    /// POI marker objects, made exactly as POIManager made them (prefab or placeholder, chest bobbing and glow, colliders
    /// stripped) at the same moment of the run start, with the per-POI V4 tick on the marker in POIInteractable's place,
    /// plus the Signal Buoy charge ring and the Beacon telegraph ring POIInteractable drew. Created at run start rather
    /// than by the presentation root because the chest bobbing draws from Unity's global random state (which also drives
    /// pickups) and moves the chest that interaction range is measured to (R-95).
    /// </summary>
    [DeletedInPhase(14, "Marker presenters over the V4 stage view (with R-95 fixed)")]
    public sealed class LegacyPoiMarkers : IPoiMarkers
    {
        sealed class Marker
        {
            public GameObject Go;
            public POIType Type;
            public Transform ChargeRing;
            public Transform TelegraphRing;
        }

        readonly DemoJuiceSettings _juice;
        readonly bool _usePlaceholderVisuals;
        readonly Dictionary<int, Marker> _markers = new Dictionary<int, Marker>();
        Transform _root;

        public LegacyPoiMarkers(bool usePlaceholderVisuals)
        {
            _usePlaceholderVisuals = usePlaceholderVisuals;
            _juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
        }

        /// <summary>
        /// V3's POIManager.ClearPoiMarkers destroyed every child of its object, /Systems, at run start, which in the V4
        /// scenes is VFXRoot (the combat VFX broker is then configured with it after it was destroyed, so V3's effects
        /// have no parent). Kept so the V4 run matches V3 (R-98).
        /// </summary>
        public void ClearRootLikeV3()
        {
            var root = Root;
            if (root == null || root.name != "Systems") return;
            for (var i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
        }

        public void Clear()
        {
            foreach (var m in _markers.Values)
                if (m.Go != null) Object.Destroy(m.Go);
            _markers.Clear();
        }

        Transform Root
        {
            get
            {
                // V3 parented the markers to POIManager's object, /Systems (its poiRoot was unset).
                if (_root == null)
                {
                    var systems = GameObject.Find("Systems");
                    _root = systems != null ? systems.transform : new GameObject("V4PoiMarkers").transform;
                }
                return _root;
            }
        }

        public void Spawn(int index, POIType type, Vector3 position, Action<int, float> tick)
        {
            var go = CreateMarker(type, position, Root);
            var slot = go.AddComponent<LegacyUpdateSlot>(); // POIInteractable's place
            slot.role = "poi";
            slot.Tick = dt => tick(index, dt);
            _markers[index] = new Marker { Go = go, Type = type };
        }

        public bool IsSpawned(int index) => _markers.ContainsKey(index);

        public Vector3 Position(int index) =>
            _markers.TryGetValue(index, out var m) && m.Go != null ? m.Go.transform.position : Vector3.zero;

        public void Hide(int index)
        {
            if (!_markers.TryGetValue(index, out var m) || m.Go == null) return;
            HideRings(m);
            for (var i = 0; i < m.Go.transform.childCount; i++) m.Go.transform.GetChild(i).gameObject.SetActive(false);
        }

        public void Consume(int index)
        {
            if (!_markers.TryGetValue(index, out var m) || m.Go == null) return;
            HideRings(m);
            m.Go.SetActive(false);
        }

        static void HideRings(Marker m)
        {
            if (m.ChargeRing != null) { Object.Destroy(m.ChargeRing.gameObject); m.ChargeRing = null; }
            if (m.TelegraphRing != null) { Object.Destroy(m.TelegraphRing.gameObject); m.TelegraphRing = null; }
        }

        /// <summary>POIInteractable.UpdateChargeRing: a pulsing blue disc, twice the circle radius, brighter as it charges.</summary>
        public void SetChargeRing(int index, float charge01, float radius)
        {
            if (!_markers.TryGetValue(index, out var m) || m.Go == null) return;
            if (m.ChargeRing == null)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "SignalBuoyChargeRing";
                ring.transform.SetParent(m.Go.transform, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                ring.GetComponent<Renderer>().material.color = new Color(0.2f, 0.75f, 1f, 0.45f);
                var col = ring.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                m.ChargeRing = ring.transform;
            }
            var size = radius * 2f;
            var pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
            m.ChargeRing.localScale = new Vector3(size * pulse, 0.04f, size * pulse);
            if (m.ChargeRing.TryGetComponent(out Renderer renderer))
                renderer.material.color = new Color(0.2f, 0.75f, 1f, Mathf.Lerp(0.2f, 0.75f, charge01));
        }

        public void ShowTelegraph(int index)
        {
            if (!_markers.TryGetValue(index, out var m) || m.Go == null || m.TelegraphRing != null) return;
            var scale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
            GameObject ring;
            if (_juice != null && _juice.bossTelegraphRingPrefab != null)
                ring = Object.Instantiate(_juice.bossTelegraphRingPrefab, m.Go.transform.position, Quaternion.identity, m.Go.transform);
            else
            {
                ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.transform.SetParent(m.Go.transform, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                ring.GetComponent<Renderer>().material.color = new Color(1f, 0.2f, 0.9f, 0.65f);
                var col = ring.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            ring.name = "BossTelegraph";
            ring.transform.localScale = new Vector3(scale, 0.05f, scale);
            m.TelegraphRing = ring.transform;
        }

        public void PulseTelegraph(int index)
        {
            if (!_markers.TryGetValue(index, out var m) || m.TelegraphRing == null) return;
            var pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.08f;
            var baseScale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
            m.TelegraphRing.localScale = new Vector3(baseScale * pulse, 0.05f, baseScale * pulse);
        }

        public void HideTelegraph(int index)
        {
            if (!_markers.TryGetValue(index, out var m) || m.TelegraphRing == null) return;
            Object.Destroy(m.TelegraphRing.gameObject);
            m.TelegraphRing = null;
        }

        // ---- POIManager.CreateMarker, as V3 ------------------------------------------------------------------------

        GameObject CreateMarker(POIType type, Vector3 position, Transform root)
        {
            var spawnPos = position + Vector3.up * 0.15f;
            var prefab = GetPrefab(type);
            if (prefab != null && (!_usePlaceholderVisuals || type == POIType.SalvageChest))
                return CreatePrefabMarker(type, prefab, spawnPos, root);

            if (_usePlaceholderVisuals)
            {
                var marker = V3PlaceholderFactory.CreatePoiMarker(type, spawnPos, root);
                if (type == POIType.SalvageChest) SalvageChestWaterFloat.Attach(marker, _juice);
                return marker;
            }

            if (prefab != null) return CreatePrefabMarker(type, prefab, spawnPos, root);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = type.ToString();
            go.transform.SetParent(root, false);
            go.transform.position = spawnPos + Vector3.up * 0.35f;
            go.transform.localScale = FallbackScale(type);
            go.GetComponent<Renderer>().material.color = type switch
            {
                POIType.SalvageChest => new Color(0.8f, 0.6f, 0.2f),
                POIType.LighthouseOfFortune => Color.white,
                POIType.TrialBuoy => Color.red,
                POIType.BossBeacon => Color.magenta,
                _ => Color.gray
            };
            StripColliders(go);
            if (type == POIType.SalvageChest) SalvageChestWaterFloat.Attach(go, _juice);
            return go;
        }

        GameObject CreatePrefabMarker(POIType type, GameObject prefab, Vector3 spawnPos, Transform root)
        {
            var go = Object.Instantiate(prefab, spawnPos, Quaternion.identity, root);
            go.name = type.ToString();
            go.transform.localScale *= Scale(type);
            if (type == POIType.SalvageChest)
            {
                SalvageChestWaterFloat.Attach(go, _juice);
                SalvageChestTreasureGlow.Attach(go, _juice);
            }
            StripColliders(go);
            return go;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }

        GameObject GetPrefab(POIType type)
        {
            if (_juice == null) return null;
            return type switch
            {
                POIType.SalvageChest => _juice.salvageChestPrefab,
                POIType.LighthouseOfFortune => _juice.lighthousePrefab,
                POIType.TrialBuoy => _juice.trialBuoyPrefab,
                POIType.BossBeacon => _juice.bossBeaconPrefab,
                _ => null
            };
        }

        float Scale(POIType type)
        {
            if (_juice == null) return 1f;
            return type switch
            {
                POIType.SalvageChest => _juice.salvageChestScale,
                POIType.LighthouseOfFortune => _juice.lighthouseScale,
                POIType.TrialBuoy => _juice.trialBuoyScale,
                POIType.BossBeacon => _juice.bossBeaconScale,
                _ => 1f
            };
        }

        static Vector3 FallbackScale(POIType type) => type switch
        {
            POIType.SalvageChest => new Vector3(1.6f, 0.8f, 1.6f),
            POIType.LighthouseOfFortune => new Vector3(1.2f, 2f, 1.2f),
            POIType.TrialBuoy => new Vector3(1f, 1.2f, 1f),
            POIType.BossBeacon => new Vector3(2.2f, 0.2f, 2.2f),
            _ => new Vector3(2f, 1f, 2f)
        };
    }
}

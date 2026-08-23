using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Meta;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.POI
{
    public sealed class POIManager : MonoBehaviour
    {
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

        public void Initialize(World.StageLayout layout, GameDatabase db)
        {
            _layout = layout;
            database = db;
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
                var marker = CreateMarker(poi.type, poi.position, root);
                var trigger = marker.AddComponent<POIInteractable>();
                trigger.Initialize(this, poi.type, i, juiceSettings, pacingSettings);
            }
        }

        GameObject CreateMarker(POIType type, Vector3 position, Transform root)
        {
            var prefab = GetPrefab(type);
            GameObject go;
            var spawnPos = position + Vector3.up * 0.15f;

            if (prefab != null)
            {
                go = Instantiate(prefab, spawnPos, Quaternion.identity, root);
                go.name = type.ToString();
                go.transform.localScale = Vector3.one * GetScale(type);
            }
            else
            {
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
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
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

        public void Interact(POIType type, int index)
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            switch (type)
            {
                case POIType.SalvageChest:
                    var price = director != null ? director.GetChestPrice() : 25;
                    if (_paidChestCount == 0 && pacingSettings != null)
                        price = Mathf.RoundToInt(price * (1f - pacingSettings.firstChestDiscount));
                    _paidChestCount++;
                    if (!RunManager.Instance.TrySpendGold(price)) return;
                    var relic = loot.RollRelic(RunManager.Instance.Rng, RunManager.Instance.Stats.Luck);
                    TriggerBus.Instance?.AddRelic(relic);
                    TriggerBus.Instance?.RaiseChestOpen();
                    break;
                case POIType.LighthouseOfFortune:
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Damage, 0f, 1.08f));
                    break;
                case POIType.TrialBuoy:
                    RunManager.Instance.AddGold(40);
                    break;
                case POIType.BossBeacon:
                    SummonBoss(player);
                    break;
            }

            if (index >= 0 && index < _layout.pois.Count)
            {
                var p = _layout.pois[index];
                p.used = true;
                _layout.pois[index] = p;
            }
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

        public void NotifyBossDefeated()
        {
            if (_bossDefeated) return;
            _bossDefeated = true;
            RunManager.Instance.SetState(RunState.BossDefeated);
            RunManager.Instance.CompleteRun(true);
        }

        void Update()
        {
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
        float _telegraphTimer;
        Transform _telegraphRing;

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
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            if (Vector3.Distance(player.Position, transform.position) > 3f)
            {
                CancelTelegraph();
                return;
            }

            if (_type == POIType.BossBeacon)
            {
                var minSeconds = _pacing != null ? _pacing.bossBeaconMinSeconds : 180f;
                if (RunManager.Instance.ElapsedSeconds < minSeconds)
                    return;

                if (!_telegraphing)
                {
                    _telegraphing = true;
                    _telegraphTimer = _pacing != null ? _pacing.bossTelegraphSeconds : 0.7f;
                    ShowTelegraph();
                }

                _telegraphTimer -= Time.deltaTime;
                PulseTelegraph();
                if (_telegraphTimer > 0f) return;

                _manager?.Interact(_type, _index);
                _used = true;
                HideTelegraph();
                return;
            }

            _manager?.Interact(_type, _index);
            _used = true;
            gameObject.SetActive(false);
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

        void CancelTelegraph()
        {
            if (!_telegraphing) return;
            _telegraphing = false;
            _telegraphTimer = 0f;
            HideTelegraph();
        }

        void HideTelegraph()
        {
            if (_telegraphRing == null) return;
            Destroy(_telegraphRing.gameObject);
            _telegraphRing = null;
        }
    }
}

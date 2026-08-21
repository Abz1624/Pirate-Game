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

        World.StageLayout _layout;
        bool _bossSummoned;
        bool _bossDefeated;

        public void Initialize(World.StageLayout layout, GameDatabase db)
        {
            _layout = layout;
            database = db;
            BuildMarkers();
        }

        void BuildMarkers()
        {
            if (_layout == null) return;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                var poi = _layout.pois[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = poi.type.ToString();
                go.transform.position = poi.position + Vector3.up * 0.5f;
                go.transform.localScale = new Vector3(2f, 1f, 2f);
                Destroy(go.GetComponent<Collider>());
                var rend = go.GetComponent<Renderer>();
                rend.material.color = poi.type switch
                {
                    POIType.SalvageChest => new Color(0.8f, 0.6f, 0.2f),
                    POIType.LighthouseOfFortune => Color.white,
                    POIType.TrialBuoy => Color.red,
                    POIType.BossBeacon => Color.magenta,
                    _ => Color.gray
                };
                var trigger = go.AddComponent<POIInteractable>();
                trigger.Initialize(this, poi.type, i);
            }
        }

        public void Interact(POIType type, int index)
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            switch (type)
            {
                case POIType.SalvageChest:
                    var price = director != null ? director.GetChestPrice() : 25;
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
                    SummonBoss();
                    break;
            }
            if (index >= 0 && index < _layout.pois.Count)
            {
                var p = _layout.pois[index];
                p.used = true;
                _layout.pois[index] = p;
            }
        }

        void SummonBoss()
        {
            if (_bossSummoned) return;
            _bossSummoned = true;
            RunManager.Instance.SetState(RunState.BossFight);
            var world = FindFirstObjectByType<Enemies.EnemyWorld>();
            if (world == null || world.Definitions == null) return;
            for (var i = 0; i < world.Definitions.Length; i++)
            {
                if (!world.Definitions[i].isBoss) continue;
                var player = FindFirstObjectByType<PlayerShipController>();
                var pos = player != null ? player.Position + player.transform.forward * 12f : Vector3.forward * 12f;
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

        public void Initialize(POIManager manager, POIType type, int index)
        {
            _manager = manager;
            _type = type;
            _index = index;
        }

        void Update()
        {
            if (_used) return;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            if (Vector3.Distance(player.Position, transform.position) > 3f) return;
            if (_type == POIType.BossBeacon && RunManager.Instance.ElapsedSeconds < 60f) return;
            _manager?.Interact(_type, _index);
            _used = _type != POIType.BossBeacon;
            if (_used) gameObject.SetActive(false);
        }
    }
}

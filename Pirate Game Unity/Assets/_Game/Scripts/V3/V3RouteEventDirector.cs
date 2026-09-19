using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Prototype route-risk events (doc §13.1): treasure chase, cursed chest, bounty flag, powder depot.</summary>
    public sealed class V3RouteEventDirector : MonoBehaviour
    {
        public static V3RouteEventDirector Instance { get; private set; }

        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] Transform player;

        float _nextEventTime = 75f;
        int _eventsTriggered;
        float _bountyDensityTimer;
        bool _cursedChestActive;

        public static void Ensure()
        {
            if (Instance != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<V3RouteEventDirector>() == null)
                systems.AddComponent<V3RouteEventDirector>();
        }

        void Awake()
        {
            Instance = this;
            if (enemyWorld == null) enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (player == null)
            {
                var ship = FindFirstObjectByType<PlayerShipController>();
                if (ship != null) player = ship.transform;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!V3RunContext.IsActive || RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (_bountyDensityTimer > 0f)
            {
                _bountyDensityTimer -= Time.deltaTime;
                return;
            }

            if (elapsed < _nextEventTime || _eventsTriggered >= 4) return;
            _nextEventTime = elapsed + Mathf.Lerp(90f, 150f, _eventsTriggered / 4f);
            _eventsTriggered++;
            TriggerEvent(_eventsTriggered % 4, elapsed);
        }

        void TriggerEvent(int kind, float elapsed)
        {
            switch (kind)
            {
                case 0:
                    SpawnTreasureRaider();
                    V3ToastHud.Instance?.Show("Treasure Raider spotted — chase for bonus gold!", 4f);
                    break;
                case 1:
                    _cursedChestActive = true;
                    V3ToastHud.Instance?.Show("Cursed Chest revealed — elites inbound!", 4f);
                    TriggerScheduleProfile("route_swordfish", 3);
                    TriggerScheduleProfile("route_cannon", 2);
                    break;
                case 2:
                    _bountyDensityTimer = 25f;
                    FindFirstObjectByType<EncounterSpawner>()?.SetSpawnRateMultiplier(1.35f);
                    V3ToastHud.Instance?.Show("Bounty Flag raised — higher enemy density!", 4f);
                    break;
                default:
                    V3ToastHud.Instance?.Show("Powder Depot armed — chain clears enabled!", 4f);
                    break;
            }

            RunCombatTelemetry.Instance?.Record("route_event", new System.Collections.Generic.Dictionary<string, object>
            {
                ["kind"] = kind,
                ["elapsed"] = elapsed
            });
        }

        void SpawnTreasureRaider()
        {
            if (TriggerScheduleProfile("route_raider", 1))
                return;

            if (enemyWorld == null || player == null) return;
            var index = FindDefinitionIndex("treasure_raider");
            if (index < 0) index = FindDefinitionIndex("deckhand_dinghy");
            if (index < 0) return;
            var offset = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * player.forward * 18f;
            enemyWorld.Spawn(index, player.position + offset);
        }

        bool TriggerScheduleProfile(string key, int count)
        {
            var spawner = EncounterSpawner.Instance ?? FindFirstObjectByType<EncounterSpawner>();
            var profile = spawner?.GetScheduleProfile(key);
            if (spawner == null || profile == null)
                return false;
            spawner.TriggerProfile(profile, count);
            return true;
        }

        void SpawnEnemyRing(string enemyId, int count)
        {
            var index = FindDefinitionIndex(enemyId);
            if (index < 0) return;
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                var pos = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 16f;
                enemyWorld.Spawn(index, pos);
            }
        }

        int FindDefinitionIndex(string id)
        {
            var defs = enemyWorld.Definitions;
            if (defs == null) return -1;
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] != null && defs[i].id == id) return i;
            }
            return -1;
        }
    }
}

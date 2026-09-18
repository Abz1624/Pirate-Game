using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// Temporary floor powerups: 1% non-boss kill roll, max six per stage, 30s lifetime.
    /// RP-CONT-014 / M2-024.
    /// </summary>
    public sealed class V3FloorPowerupWorld : MonoBehaviour
    {
        public const float DropChance = 0.01f;
        public const int MaxPerStage = 6;
        public const float CollectRadius = 1.5f;
        public const float LifetimeSeconds = 30f;
        public const float BattleSignalDuration = 8f;
        public const float TailwindDuration = 6f;
        public const float BattleSignalDamageMultiplier = 1.25f;
        public const float TailwindSpeedMultiplier = 1.2f;

        struct Agent
        {
            public bool active;
            public V3FloorPowerupKind kind;
            public Vector3 position;
            public float lifetime;
            public Transform view;
        }

        public static V3FloorPowerupWorld Instance { get; private set; }

        Agent[] _agents = new Agent[MaxPerStage];
        int _spawnedThisStage;
        float _battleSignalTimer;
        float _tailwindTimer;
        StatModifier _damageModifier;
        StatModifier _speedModifier;
        bool _damageBuffActive;
        bool _speedBuffActive;
        float _bobTime;

        public int SpawnedThisStage => _spawnedThisStage;

        public static void Ensure()
        {
            if (Instance != null) return;
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<V3FloorPowerupWorld>() == null)
                systems.AddComponent<V3FloorPowerupWorld>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            ClearBuffs();
            if (Instance == this)
                Instance = null;
        }

        public void ResetForRun()
        {
            for (var i = 0; i < _agents.Length; i++)
                Despawn(i);
            _spawnedThisStage = 0;
            ClearBuffs();
        }

        public void TryRollOnKill(Vector3 position, bool isBoss)
        {
            if (!V3RunContext.IsActive || isBoss) return;
            if (_spawnedThisStage >= MaxPerStage) return;

            var rng = RunManager.Instance?.Rng;
            if (rng == null || rng.PowerupFloat() >= DropChance) return;

            var kind = V3PowerupCatalog.RollKind(rng, V3PowerupCatalog.UseExpandedPool());
            if (!TrySpawn(kind, position))
                return;

            V3ToastHud.Instance?.Show($"{GetDisplayName(kind)} dropped!", 2f);
        }

        public static V3FloorPowerupKind RollKind(RunRng rng)
        {
            var roll = rng.PowerupFloat() * 100f;
            if (roll < 35f) return V3FloorPowerupKind.RepairSupplies;
            if (roll < 70f) return V3FloorPowerupKind.BattleSignal;
            if (roll < 90f) return V3FloorPowerupKind.Tailwind;
            return V3FloorPowerupKind.SalvageChime;
        }

        bool TrySpawn(V3FloorPowerupKind kind, Vector3 origin)
        {
            var slot = FindFreeSlot();
            if (slot < 0) return false;

            if (!TryFindSpawnPosition(origin, out var spawnPos))
                return false;

            var view = V3PlaceholderFactory.CreateFloorPowerup(kind, spawnPos, transform);
            _agents[slot] = new Agent
            {
                active = true,
                kind = kind,
                position = spawnPos,
                lifetime = LifetimeSeconds,
                view = view.transform
            };
            _spawnedThisStage++;
            return true;
        }

        static bool TryFindSpawnPosition(Vector3 origin, out Vector3 position)
        {
            position = origin;
            position.y = 0.35f;

            if (IsValidSpawn(position))
                return true;

            for (var ring = 1; ring <= 4; ring++)
            {
                for (var i = 0; i < 8; i++)
                {
                    var angle = (i / 8f) * Mathf.PI * 2f;
                    var candidate = origin + new Vector3(Mathf.Cos(angle) * ring * 1.5f, 0f, Mathf.Sin(angle) * ring * 1.5f);
                    candidate.y = 0.35f;
                    if (!IsValidSpawn(candidate)) continue;
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        static bool IsValidSpawn(Vector3 position)
        {
            if (!PlayBounds.IsInside(position, 1f))
                return false;
            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, 0.45f))
                return false;
            return true;
        }

        int FindFreeSlot()
        {
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active)
                    return i;
            }

            return -1;
        }

        public void Tick(float dt, Vector3 playerPos)
        {
            if (!V3RunContext.IsActive) return;
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;

            _bobTime += dt;
            TickBuffs(dt);

            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;

                ref var agent = ref _agents[i];
                agent.lifetime -= dt;
                if (agent.lifetime <= 0f)
                {
                    Despawn(i);
                    continue;
                }

                var bob = Mathf.Sin(_bobTime * 4f + i) * 0.1f;
                var displayPos = agent.position + Vector3.up * bob;
                if (agent.view != null)
                    agent.view.position = displayPos;

                var flat = playerPos - agent.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > CollectRadius * CollectRadius)
                    continue;

                ApplyPickup(agent.kind, playerPos);
                Despawn(i);
            }
        }

        void ApplyPickup(V3FloorPowerupKind kind, Vector3 playerPos)
        {
            switch (kind)
            {
                case V3FloorPowerupKind.RepairSupplies:
                {
                    var player = FindFirstObjectByType<PlayerShipController>();
                    if (player?.Damage != null && player.Damage.IsAlive)
                    {
                        var heal = player.Damage.MaxHP * 0.15f;
                        player.Damage.Heal(heal);
                    }
                    break;
                }
                case V3FloorPowerupKind.BattleSignal:
                    _battleSignalTimer = BattleSignalDuration;
                    ApplyDamageBuff();
                    break;
                case V3FloorPowerupKind.Tailwind:
                    _tailwindTimer = TailwindDuration;
                    ApplySpeedBuff();
                    break;
                case V3FloorPowerupKind.SalvageChime:
                    Pickups.PickupWorld.Instance?.AttractAllInfamy();
                    break;
                case V3FloorPowerupKind.FullSail:
                    _tailwindTimer = 8f;
                    ApplySpeedBuff(1.45f);
                    break;
                case V3FloorPowerupKind.PowderRush:
                    FindFirstObjectByType<Weapons.WeaponController>()?.CaptainFireAllWeapons();
                    _battleSignalTimer = 4f;
                    ApplyDamageBuff(1.15f);
                    break;
                case V3FloorPowerupKind.MagneticTide:
                    Pickups.PickupWorld.Instance?.AttractAllInfamy();
                    break;
                case V3FloorPowerupKind.DeadCalm:
                    FindFirstObjectByType<Enemies.SpawnDirector>()?.SetSpawnRateMultiplier(0.55f);
                    break;
            }

            V3ToastHud.Instance?.Show($"{GetDisplayName(kind)} collected.", 2f);
        }

        void TickBuffs(float dt)
        {
            if (_battleSignalTimer > 0f)
            {
                _battleSignalTimer -= dt;
                if (_battleSignalTimer <= 0f)
                    RemoveDamageBuff();
            }

            if (_tailwindTimer > 0f)
            {
                _tailwindTimer -= dt;
                if (_tailwindTimer <= 0f)
                    RemoveSpeedBuff();
            }
        }

        void ApplyDamageBuff(float multiplier = BattleSignalDamageMultiplier)
        {
            var stats = RunManager.Instance?.Stats;
            if (stats == null) return;
            RemoveDamageBuff();
            _damageModifier = new StatModifier(StatId.Damage, 0f, multiplier);
            stats.AddModifier(_damageModifier);
            _damageBuffActive = true;
        }

        void RemoveDamageBuff()
        {
            if (!_damageBuffActive) return;
            var stats = RunManager.Instance?.Stats;
            if (stats != null)
                stats.RemoveModifier(_damageModifier);
            _damageModifier = default;
            _damageBuffActive = false;
        }

        void ApplySpeedBuff(float multiplier = TailwindSpeedMultiplier)
        {
            var stats = RunManager.Instance?.Stats;
            if (stats == null) return;
            RemoveSpeedBuff();
            _speedModifier = new StatModifier(StatId.MoveSpeed, 0f, multiplier);
            stats.AddModifier(_speedModifier);
            _speedBuffActive = true;
            FindFirstObjectByType<PlayerShipController>()?.SyncRunSpeedMultiplier();
        }

        void RemoveSpeedBuff()
        {
            if (!_speedBuffActive) return;
            var stats = RunManager.Instance?.Stats;
            if (stats != null)
                stats.RemoveModifier(_speedModifier);
            _speedModifier = default;
            _speedBuffActive = false;
            FindFirstObjectByType<PlayerShipController>()?.SyncRunSpeedMultiplier();
        }

        void ClearBuffs()
        {
            _battleSignalTimer = 0f;
            _tailwindTimer = 0f;
            RemoveDamageBuff();
            RemoveSpeedBuff();
        }

        void Despawn(int index)
        {
            ref var agent = ref _agents[index];
            if (!agent.active) return;
            if (agent.view != null)
                Destroy(agent.view.gameObject);
            agent = default;
        }

        public static string GetDisplayName(V3FloorPowerupKind kind) =>
            kind switch
            {
                V3FloorPowerupKind.RepairSupplies => "Repair Supplies",
                V3FloorPowerupKind.BattleSignal => "Battle Signal",
                V3FloorPowerupKind.Tailwind => "Tailwind",
                V3FloorPowerupKind.SalvageChime => "Salvage Chime",
                _ => kind.ToString()
            };
    }
}

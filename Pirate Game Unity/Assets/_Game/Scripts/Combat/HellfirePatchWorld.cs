using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Hellfire Nozzle flame patches — bounded hazard, no generic proc chains.</summary>
    public sealed class HellfirePatchWorld : MonoBehaviour
    {
        public static HellfirePatchWorld Instance { get; private set; }

        const int MaxPatches = 8;
        const float PatchLifetime = 2.4f;
        const float PatchRadius = 2.1f;
        const float TickInterval = 0.35f;

        struct Patch
        {
            public bool active;
            public Vector3 position;
            public float life;
            public float tickTimer;
        }

        readonly Patch[] _patches = new Patch[MaxPatches];
        EnemyWorld _enemies;

        public static HellfirePatchWorld Ensure()
        {
            if (Instance != null) return Instance;
            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("HellfirePatchWorld");
            return host.GetComponent<HellfirePatchWorld>() ?? host.AddComponent<HellfirePatchWorld>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void TrySpawnOnDirectFlameKill(Vector3 position, string weaponId)
        {
            if (WeaponEvolutionCatalog.GetBehaviorModifier(weaponId) != "hellfire_nozzle") return;
            Ensure()?.AddPatch(position);
        }

        void AddPatch(Vector3 position)
        {
            position.y = 0.2f;
            var slot = -1;
            for (var i = 0; i < _patches.Length; i++)
            {
                if (!_patches[i].active) { slot = i; break; }
            }

            if (slot < 0)
            {
                var oldest = 0;
                var oldestLife = float.MaxValue;
                for (var i = 0; i < _patches.Length; i++)
                {
                    if (!_patches[i].active || _patches[i].life >= oldestLife) continue;
                    oldestLife = _patches[i].life;
                    oldest = i;
                }
                slot = oldest;
            }

            _patches[slot] = new Patch
            {
                active = true,
                position = position,
                life = PatchLifetime,
                tickTimer = 0f
            };

            var juice = FindFirstObjectByType<WeaponJuiceController>();
            juice?.OnBurningOilDropped(position, PatchRadius * 0.85f);
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded) return;
            if (_enemies == null) _enemies = FindFirstObjectByType<EnemyWorld>();
            if (_enemies == null) return;

            var dt = Time.deltaTime;
            var rng = RunManager.Instance.Rng;
            var stats = RunManager.Instance.Stats;
            for (var i = 0; i < _patches.Length; i++)
            {
                ref var patch = ref _patches[i];
                if (!patch.active) continue;
                patch.life -= dt;
                if (patch.life <= 0f)
                {
                    patch.active = false;
                    continue;
                }

                patch.tickTimer -= dt;
                if (patch.tickTimer > 0f) continue;
                patch.tickTimer = TickInterval;

                var agents = _enemies.Agents;
                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (Vector3.Distance(agents[ai].position, patch.position) > PatchRadius) continue;
                    var dmg = DamageSystem.RollDamage(
                        4f,
                        1f,
                        stats,
                        agents[ai].isElite,
                        0f,
                        rng,
                        0f,
                        "flamethrower");
                    _enemies.ApplyDamage(ai, dmg, false, showHitFeedback: false);
                    StatusController.Instance?.ApplyStatus(ai, StatusType.Burn, 0.45f);
                }
            }
        }
    }
}

using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Pickups
{
    public struct PickupAgent
    {
        public bool active;
        public bool homingToPlayer;
        public PickupType type;
        public int value;
        public Vector3 position;
        public Vector3 basePosition;
        public float bobPhase;
        public float flyDelay;
        public Transform view;
    }

    public sealed class PickupWorld : MonoBehaviour
    {
        public static PickupWorld Instance { get; private set; }

        const float KillDropSpreadRadius = 1.6f;

        [SerializeField] Transform root;
        [SerializeField] DemoJuiceSettings juiceSettings;
        [SerializeField] DemoPacingSettings pacingSettings;

        PickupAgent[] _pool;
        PickupViewPool _viewPool;
        int _active;
        float _time;

        void Awake()
        {
            Instance = this;
            _pool = new PickupAgent[Platform.QualityProfile.Current.pickupCap];
            if (juiceSettings == null)
                juiceSettings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (pacingSettings == null)
                pacingSettings = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            _viewPool = new PickupViewPool(root != null ? root : transform, juiceSettings);
        }

        public void SpawnFromEnemy(Vector3 pos, int xp, int gold, bool rich)
        {
            var xpMult = pacingSettings != null ? pacingSettings.xpDropMultiplier : 1f;
            var healthChance = pacingSettings != null ? pacingSettings.healthDropChance : 0.12f;
            var basePos = pos;
            basePos.y = 0f;

            var spawnHealth = RunManager.Instance != null && RunManager.Instance.Rng.DirectorFloat() < healthChance;
            var dropCount = 0;
            if (xp > 0) dropCount++;
            if (gold > 0) dropCount++;
            if (spawnHealth) dropCount++;

            var dropIndex = 0;
            if (xp > 0)
            {
                Spawn(
                    PickupType.Infamy,
                    Mathf.Max(1, Mathf.RoundToInt(xp * xpMult)),
                    basePos + GetKillDropOffset(dropIndex++, dropCount));
            }

            if (gold > 0)
            {
                var goldValue = rich ? gold * 2 : gold;
                Spawn(PickupType.GoldBarrel, goldValue, basePos + GetKillDropOffset(dropIndex++, dropCount));
            }

            if (spawnHealth)
            {
                Spawn(PickupType.Powerup, 25, basePos + GetKillDropOffset(dropIndex, dropCount));
            }
        }

        static Vector3 GetKillDropOffset(int index, int count)
        {
            if (count <= 1)
                return Vector3.zero;

            var angle = (index * (360f / count) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            var radius = KillDropSpreadRadius + Random.Range(-0.15f, 0.15f);
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        public void Spawn(PickupType type, int value, Vector3 pos)
        {
            if (_active >= _pool.Length) MergeNearby(type, value, pos);
            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) { slot = i; break; }
            }
            if (slot < 0) return;

            pos.y = GetPickupHeight(type);
            var view = _viewPool.Acquire(type, pos, value);
            _pool[slot] = new PickupAgent
            {
                active = true,
                homingToPlayer = false,
                type = type,
                value = value,
                position = pos,
                basePosition = pos,
                bobPhase = Random.Range(0f, Mathf.PI * 2f),
                view = view
            };
            _active++;
        }

        static float GetPickupHeight(PickupType type) => type switch
        {
            PickupType.GoldBarrel => 2f,
            PickupType.Powerup => 2f,
            PickupType.FlyingGold or PickupType.FlyingHealth => 0.45f,
            _ => 0.35f
        };

        void MergeNearby(PickupType type, int value, Vector3 pos)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active || _pool[i].type != type) continue;
                if (Vector3.Distance(_pool[i].position, pos) > 2f) continue;
                var p = _pool[i];
                p.value += value;
                _pool[i] = p;
                return;
            }
        }

        public void Tick(float dt, Vector3 playerPos, float pickupRadius)
        {
            _time += dt;
            var xpPickupRadius = pacingSettings != null ? pacingSettings.xpAutoPickupRadius : 4.25f;
            var goldPickupRadius = pacingSettings != null ? pacingSettings.goldAutoPickupRadius : 26f;
            var barrelTouchRadius = pacingSettings != null ? pacingSettings.barrelTouchRadius : 5f;

            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                ref var p = ref _pool[i];
                var toPlayer = playerPos - p.position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;

                if (p.homingToPlayer || p.type == PickupType.FlyingGold || p.type == PickupType.FlyingHealth)
                {
                    if (p.flyDelay > 0f)
                    {
                        p.flyDelay -= dt;
                    }
                    else
                    {
                        var flySpeed = Mathf.Lerp(10f, 28f, 1f - Mathf.Clamp01(dist / Mathf.Max(goldPickupRadius, 1f)));
                        p.position += toPlayer.normalized * flySpeed * dt;
                        p.basePosition = p.position;
                        if (dist <= 0.75f)
                            CompleteHomingPickup(ref p);
                    }
                }
                else if (p.type == PickupType.Infamy && dist <= xpPickupRadius)
                {
                    BeginHomingPickup(ref p);
                }
                else if ((p.type == PickupType.Gold || p.type == PickupType.FlyingGold) && dist <= goldPickupRadius)
                {
                    BeginHomingPickup(ref p);
                }
                else if ((p.type == PickupType.GoldBarrel || p.type == PickupType.Powerup) && dist <= barrelTouchRadius)
                {
                    Collect(i);
                    continue;
                }
                else
                {
                    var bob = Mathf.Sin(_time * 3.5f + p.bobPhase) * 0.12f;
                    p.position = p.basePosition + Vector3.up * bob;
                }

                if (p.view != null)
                {
                    p.view.position = p.position;
                    if (!p.homingToPlayer && p.type != PickupType.GoldBarrel && p.type != PickupType.Powerup)
                        p.view.Rotate(0f, 180f * dt, 0f, Space.World);
                }
            }
        }

        void BeginHomingPickup(ref PickupAgent p)
        {
            p.homingToPlayer = true;
            p.flyDelay = 0f;
            if ((p.type == PickupType.Gold || p.type == PickupType.FlyingGold) && p.view != null && juiceSettings != null)
                p.view.localScale = Vector3.one * juiceSettings.goldFlyingCoinScale;
        }

        void CompleteHomingPickup(ref PickupAgent p)
        {
            switch (p.type)
            {
                case PickupType.Infamy:
                    UI.LevelSystem.Instance?.AddXP(Mathf.RoundToInt(p.value * (RunManager.Instance?.Stats.XPGain ?? 1f)));
                    break;
                case PickupType.Gold:
                case PickupType.FlyingGold:
                    GrantGold(p.value);
                    break;
                case PickupType.FlyingHealth:
                {
                    var player = Object.FindFirstObjectByType<Player.PlayerShipController>();
                    player?.Damage.Heal(p.value);
                    break;
                }
            }

            ReleasePickup(ref p);
        }

        void Collect(int i)
        {
            ref var p = ref _pool[i];
            switch (p.type)
            {
                case PickupType.GoldBarrel:
                    BurstGoldCoins(p.position, p.value);
                    ReleasePickup(ref p);
                    break;
                case PickupType.Powerup:
                    BurstHealthPluses(p.position, p.value);
                    ReleasePickup(ref p);
                    break;
            }
        }

        void BurstGoldCoins(Vector3 origin, int totalGold)
        {
            var coinCount = Random.Range(10, 21);
            var perCoin = Mathf.Max(1, totalGold / coinCount);
            var remainder = totalGold - perCoin * coinCount;

            for (var c = 0; c < coinCount; c++)
            {
                if (_active >= _pool.Length) break;
                var slot = -1;
                for (var i = 0; i < _pool.Length; i++)
                {
                    if (!_pool[i].active) { slot = i; break; }
                }
                if (slot < 0) break;

                var scatter = Random.insideUnitSphere * 0.8f;
                scatter.y = 0f;
                var pos = origin + scatter;
                pos.y = 0.45f;
                var value = perCoin + (c == 0 ? remainder : 0);
                var view = _viewPool.Acquire(PickupType.FlyingGold, pos, value);
                _pool[slot] = new PickupAgent
                {
                    active = true,
                    homingToPlayer = true,
                    type = PickupType.FlyingGold,
                    value = value,
                    position = pos,
                    basePosition = pos,
                    bobPhase = Random.Range(0f, Mathf.PI * 2f),
                    flyDelay = Random.Range(0.05f, 0.25f) + c * 0.02f,
                    view = view
                };
                _active++;
            }
        }

        void BurstHealthPluses(Vector3 origin, int totalHeal)
        {
            var plusCount = Random.Range(10, 21);
            var perPlus = Mathf.Max(1, totalHeal / plusCount);
            var remainder = totalHeal - perPlus * plusCount;

            for (var c = 0; c < plusCount; c++)
            {
                if (_active >= _pool.Length) break;
                var slot = -1;
                for (var i = 0; i < _pool.Length; i++)
                {
                    if (!_pool[i].active) { slot = i; break; }
                }
                if (slot < 0) break;

                var scatter = Random.insideUnitSphere * 0.8f;
                scatter.y = 0f;
                var pos = origin + scatter;
                pos.y = 0.45f;
                var value = perPlus + (c == 0 ? remainder : 0);
                var view = _viewPool.Acquire(PickupType.FlyingHealth, pos, value);
                _pool[slot] = new PickupAgent
                {
                    active = true,
                    homingToPlayer = true,
                    type = PickupType.FlyingHealth,
                    value = value,
                    position = pos,
                    basePosition = pos,
                    bobPhase = Random.Range(0f, Mathf.PI * 2f),
                    flyDelay = Random.Range(0.05f, 0.25f) + c * 0.02f,
                    view = view
                };
                _active++;
            }
        }

        static void GrantGold(int value) =>
            RunManager.Instance?.AddGold(Mathf.RoundToInt(value * (RunManager.Instance?.Stats.GoldGain ?? 1f)));

        void ReleasePickup(ref PickupAgent p)
        {
            if (p.view != null)
                _viewPool.Release(p.view, p.type, p.value);
            p.active = false;
            p.homingToPlayer = false;
            p.view = null;
            _active--;
        }
    }
}

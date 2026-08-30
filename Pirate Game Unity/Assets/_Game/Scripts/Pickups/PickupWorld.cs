using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Pickups
{
    public struct PickupAgent
    {
        public bool active;
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
            var player = FindPlayerPosition();
            var xpMult = pacingSettings != null ? pacingSettings.xpDropMultiplier : 1f;
            var healthChance = pacingSettings != null ? pacingSettings.healthDropChance : 0.12f;

            if (xp > 0)
            {
                var xpPos = ScatterPickupPosition(pos, player);
                Spawn(PickupType.Infamy, Mathf.Max(1, Mathf.RoundToInt(xp * xpMult)), xpPos);
            }

            if (gold > 0)
            {
                var goldValue = rich ? gold * 2 : gold;
                var goldPos = ScatterPickupPosition(pos, player, 1.2f);
                Spawn(PickupType.GoldBarrel, goldValue, goldPos);
            }

            if (RunManager.Instance != null && RunManager.Instance.Rng.DirectorFloat() < healthChance)
            {
                var healthPos = ScatterPickupPosition(pos, player, 0.8f);
                Spawn(PickupType.Powerup, 25, healthPos);
            }
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
            PickupType.GoldBarrel => 0.2f,
            PickupType.Powerup => 0.2f,
            PickupType.FlyingGold => 0.45f,
            _ => 0.35f
        };

        static Vector3 ScatterPickupPosition(Vector3 origin, Vector3 playerPos, float spreadScale = 1f)
        {
            origin.y = 0f;
            playerPos.y = 0f;
            var minDist = 2.5f * spreadScale;
            var maxDist = 5.5f * spreadScale;
            var minPlayerDist = 3.5f;

            for (var attempt = 0; attempt < 12; attempt++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var dist = Random.Range(minDist, maxDist);
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                var candidate = origin + offset;
                if (Vector3.Distance(candidate, playerPos) >= minPlayerDist)
                    return candidate;
            }

            var away = origin - playerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();
            return origin + away * Mathf.Max(minDist, minPlayerDist);
        }

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
            var autoPickupRadius = pacingSettings != null ? pacingSettings.goldXpAutoPickupRadius : 4.5f;
            var touchRadius = pacingSettings != null ? pacingSettings.powerupTouchRadius : 1.2f;

            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                ref var p = ref _pool[i];
                var toPlayer = playerPos - p.position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;

                if (p.type == PickupType.FlyingGold)
                {
                    if (p.flyDelay > 0f)
                    {
                        p.flyDelay -= dt;
                    }
                    else
                    {
                        var flySpeed = Mathf.Lerp(8f, 22f, 1f - Mathf.Clamp01(dist / 8f));
                        p.position += toPlayer.normalized * flySpeed * dt;
                        p.basePosition = p.position;
                        if (dist <= 0.6f)
                        {
                            GrantGold(p.value);
                            ReleasePickup(ref p);
                        }
                    }
                }
                else if (IsAutoPickupType(p.type) && dist <= autoPickupRadius)
                {
                    Collect(i);
                    continue;
                }
                else if ((p.type == PickupType.GoldBarrel || p.type == PickupType.Powerup) && dist <= touchRadius)
                {
                    Collect(i);
                    continue;
                }
                else if (p.type == PickupType.Gold && dist < pickupRadius)
                {
                    var speed = Mathf.Lerp(4f, 18f, 1f - dist / pickupRadius);
                    p.position += toPlayer.normalized * speed * dt;
                    p.basePosition = p.position;
                }
                else if (p.type != PickupType.FlyingGold)
                {
                    var bob = Mathf.Sin(_time * 3.5f + p.bobPhase) * 0.12f;
                    p.position = p.basePosition + Vector3.up * bob;
                }

                if (p.view != null)
                {
                    p.view.position = p.position;
                    if (p.type != PickupType.GoldBarrel && p.type != PickupType.Powerup)
                        p.view.Rotate(0f, 180f * dt, 0f, Space.World);
                }
            }
        }

        static bool IsAutoPickupType(PickupType type) =>
            type == PickupType.Infamy || type == PickupType.Gold;

        void Collect(int i)
        {
            ref var p = ref _pool[i];
            switch (p.type)
            {
                case PickupType.Infamy:
                    UI.LevelSystem.Instance?.AddXP(Mathf.RoundToInt(p.value * (RunManager.Instance?.Stats.XPGain ?? 1f)));
                    ReleasePickup(ref p);
                    break;
                case PickupType.Gold:
                    GrantGold(p.value);
                    ReleasePickup(ref p);
                    break;
                case PickupType.GoldBarrel:
                    BurstGoldCoins(p.position, p.value);
                    ReleasePickup(ref p);
                    break;
                case PickupType.Powerup:
                {
                    var player = Object.FindFirstObjectByType<Player.PlayerShipController>();
                    player?.Damage.Heal(p.value);
                    ReleasePickup(ref p);
                    break;
                }
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

        static void GrantGold(int value) =>
            RunManager.Instance?.AddGold(Mathf.RoundToInt(value * (RunManager.Instance?.Stats.GoldGain ?? 1f)));

        void ReleasePickup(ref PickupAgent p)
        {
            if (p.view != null)
                _viewPool.Release(p.view, p.type, p.value);
            p.active = false;
            p.view = null;
            _active--;
        }

        static Vector3 FindPlayerPosition()
        {
            var player = Object.FindFirstObjectByType<Player.PlayerShipController>();
            return player != null ? player.Position : Vector3.zero;
        }
    }
}

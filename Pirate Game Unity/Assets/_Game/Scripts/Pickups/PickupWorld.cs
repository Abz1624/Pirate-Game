using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
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

    struct PendingFlyingPickup
    {
        public PickupType type;
        public Vector3 position;
        public int value;
        public float flyDelay;
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
        readonly Queue<PendingFlyingPickup> _pendingFlyingPickups = new();
        Player.PlayerShipController _playerShip;
        int _active;
        int _pendingXpGrant;
        int _pendingGoldGrant;
        int _pendingHealGrant;
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
                    PickupType.XpBarrel,
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
            ApplyUniformPickupScale(type, view);
            if (IsBarrelPickup(type))
                pos = AlignBarrelSubmerged(view);
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

        static bool IsBarrelPickup(PickupType type) =>
            type is PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup;

        static float GetPickupHeight(PickupType type) => type switch
        {
            PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup => 0f,
            PickupType.FlyingGold or PickupType.FlyingHealth or PickupType.FlyingXp => 0.45f,
            _ => 0.35f
        };

        Vector3 AlignBarrelSubmerged(Transform view)
        {
            var submerged = juiceSettings != null ? juiceSettings.barrelSubmergedFraction : 0.15f;
            var waterY = juiceSettings != null ? juiceSettings.pickupWaterSurfaceY : 0f;

            if (!TryGetBarrelVisualBounds(view, out var bounds))
                return view.position;

            var height = bounds.size.y;
            var targetMinY = waterY - submerged * height;
            var aligned = view.position + Vector3.up * (targetMinY - bounds.min.y);
            view.position = aligned;
            return aligned;
        }

        static bool TryGetBarrelVisualBounds(Transform view, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            var root = view.Find("Visuals");
            if (root == null)
                root = view;

            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < meshRenderers.Length; i++)
            {
                var rendererBounds = meshRenderers[i].bounds;
                if (rendererBounds.size.sqrMagnitude < 0.0001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(rendererBounds);
            }

            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var i = 0; i < skinnedRenderers.Length; i++)
            {
                var rendererBounds = skinnedRenderers[i].bounds;
                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(rendererBounds);
            }

            if (hasBounds)
                return true;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var mesh = meshFilters[i].sharedMesh;
                if (mesh == null)
                    continue;

                var meshBounds = TransformMeshBounds(meshFilters[i].transform, mesh.bounds);
                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(meshBounds);
            }

            return hasBounds;
        }

        static Bounds TransformMeshBounds(Transform transform, Bounds localBounds)
        {
            var center = transform.TransformPoint(localBounds.center);
            var extents = localBounds.extents;
            var axisX = transform.TransformVector(extents.x, 0f, 0f);
            var axisY = transform.TransformVector(0f, extents.y, 0f);
            var axisZ = transform.TransformVector(0f, 0f, extents.z);
            var worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, worldExtents * 2f);
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

        public void AttractAllInfamy()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active || _pool[i].type != PickupType.Infamy) continue;
                BeginHomingPickup(ref _pool[i]);
            }
        }

        public void ClearAllActive()
        {
            _pendingFlyingPickups.Clear();
            _pendingXpGrant = 0;
            _pendingGoldGrant = 0;
            _pendingHealGrant = 0;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                var p = _pool[i];
                ReleasePickup(ref p);
                _pool[i] = p;
            }
        }

        public void Tick(float dt, Vector3 playerPos, float pickupRadius)
        {
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;
            ProcessPendingFlyingPickups();
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

                if (p.homingToPlayer || p.type == PickupType.FlyingGold || p.type == PickupType.FlyingHealth || p.type == PickupType.FlyingXp)
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
                else if ((p.type == PickupType.GoldBarrel || p.type == PickupType.XpBarrel || p.type == PickupType.Powerup) && dist <= barrelTouchRadius)
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
                    if (p.type is PickupType.Infamy or PickupType.FlyingXp)
                        ApplyUniformPickupScale(p.type, p.view);
                    if (!p.homingToPlayer && p.type != PickupType.GoldBarrel && p.type != PickupType.XpBarrel && p.type != PickupType.Powerup)
                        p.view.Rotate(0f, 180f * dt, 0f, Space.World);
                }
            }

            FlushPendingGrants();
        }

        void ApplyUniformPickupScale(PickupType type, Transform view)
        {
            if (view == null || _viewPool == null) return;
            if (type is PickupType.Infamy or PickupType.FlyingXp)
                view.localScale = Vector3.one * _viewPool.GetXpCrystalVisualScale();
        }

        void BeginHomingPickup(ref PickupAgent p)
        {
            p.homingToPlayer = true;
            p.flyDelay = 0f;
            if ((p.type == PickupType.Gold || p.type == PickupType.FlyingGold || p.type == PickupType.FlyingXp) && p.view != null && juiceSettings != null)
                p.view.localScale = Vector3.one * (p.type == PickupType.FlyingXp
                    ? _viewPool.GetXpFlyingGemScale()
                    : juiceSettings.goldFlyingCoinScale);
            else if (p.type == PickupType.Infamy && p.view != null)
                ApplyUniformPickupScale(PickupType.Infamy, p.view);
        }

        void CompleteHomingPickup(ref PickupAgent p)
        {
            if (RunManager.Instance == null || RunManager.Instance.IsRunEnded)
            {
                ReleasePickup(ref p);
                return;
            }

            switch (p.type)
            {
                case PickupType.FlyingXp:
                case PickupType.Infamy:
                    _pendingXpGrant += ComputeXpGrant(p.value);
                    break;
                case PickupType.Gold:
                case PickupType.FlyingGold:
                    _pendingGoldGrant += p.value;
                    break;
                case PickupType.FlyingHealth:
                    _pendingHealGrant += p.value;
                    break;
            }

            ReleasePickup(ref p);
        }

        int ComputeXpGrant(int value)
        {
            if (RunManager.Instance == null)
                return 0;

            var xp = value * RunManager.Instance.Stats.XPGain;
            if (V3RunContext.IsActive)
                xp *= 0.35f;
            return Mathf.Max(1, Mathf.RoundToInt(xp));
        }

        void FlushPendingGrants()
        {
            if (_pendingXpGrant > 0)
            {
                UI.LevelSystem.Instance?.AddXP(_pendingXpGrant);
                _pendingXpGrant = 0;
            }

            if (_pendingGoldGrant > 0)
            {
                GrantGold(_pendingGoldGrant);
                _pendingGoldGrant = 0;
            }

            if (_pendingHealGrant > 0)
            {
                EnsurePlayerShip();
                if (_playerShip?.Damage != null && _playerShip.Damage.IsAlive)
                    _playerShip.Damage.Heal(_pendingHealGrant);
                _pendingHealGrant = 0;
            }
        }

        void EnsurePlayerShip()
        {
            if (_playerShip != null && _playerShip.Damage != null && _playerShip.Damage.IsAlive)
                return;
            _playerShip = Object.FindFirstObjectByType<Player.PlayerShipController>();
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
                case PickupType.XpBarrel:
                    BurstXpCrystals(p.position, p.value);
                    ReleasePickup(ref p);
                    break;
                case PickupType.Powerup:
                    BurstHealthPluses(p.position, p.value);
                    ReleasePickup(ref p);
                    break;
            }
        }

        void BurstXpCrystals(Vector3 origin, int totalXp) =>
            EnqueueBarrelBurst(PickupType.FlyingXp, origin, totalXp);

        void BurstGoldCoins(Vector3 origin, int totalGold) =>
            EnqueueBarrelBurst(PickupType.FlyingGold, origin, totalGold);

        void BurstHealthPluses(Vector3 origin, int totalHeal) =>
            EnqueueBarrelBurst(PickupType.FlyingHealth, origin, totalHeal);

        void EnqueueBarrelBurst(PickupType flyingType, Vector3 origin, int totalValue)
        {
            var visualCount = GetBurstVisualCount();
            var perPiece = Mathf.Max(1, totalValue / visualCount);
            var remainder = totalValue - perPiece * visualCount;

            for (var c = 0; c < visualCount; c++)
            {
                var scatter = Random.insideUnitSphere * 0.8f;
                scatter.y = 0f;
                var pos = origin + scatter;
                pos.y = 0.45f;
                var value = perPiece + (c == 0 ? remainder : 0);
                _pendingFlyingPickups.Enqueue(new PendingFlyingPickup
                {
                    type = flyingType,
                    position = pos,
                    value = value,
                    flyDelay = Random.Range(0.05f, 0.25f) + c * 0.02f
                });
            }
        }

        int GetBurstVisualCount()
        {
            var min = pacingSettings != null ? pacingSettings.barrelBurstVisualMin : 6;
            var max = pacingSettings != null ? pacingSettings.barrelBurstVisualMax : 10;
            if (max < min)
                max = min;

            var count = Random.Range(min, max + 1);
            var density = Platform.QualityProfile.Current.vfxDensity;
            return Mathf.Max(3, Mathf.RoundToInt(count * density));
        }

        void ProcessPendingFlyingPickups()
        {
            var budget = pacingSettings != null ? pacingSettings.maxFlyingPickupSpawnsPerFrame : 18;
            while (budget-- > 0 && _pendingFlyingPickups.Count > 0)
            {
                var pending = _pendingFlyingPickups.Dequeue();
                if (!TrySpawnFlyingPickup(pending))
                    GrantFlyingPickupValue(pending.type, pending.value);
            }
        }

        bool TrySpawnFlyingPickup(PendingFlyingPickup pending)
        {
            if (_active >= _pool.Length)
                return false;

            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) { slot = i; break; }
            }

            if (slot < 0)
                return false;

            var view = _viewPool.Acquire(pending.type, pending.position, pending.value);
            if (pending.type == PickupType.FlyingXp)
                ApplyUniformPickupScale(PickupType.FlyingXp, view);

            _pool[slot] = new PickupAgent
            {
                active = true,
                homingToPlayer = true,
                type = pending.type,
                value = pending.value,
                position = pending.position,
                basePosition = pending.position,
                bobPhase = Random.Range(0f, Mathf.PI * 2f),
                flyDelay = pending.flyDelay,
                view = view
            };
            _active++;
            return true;
        }

        void GrantFlyingPickupValue(PickupType type, int value)
        {
            switch (type)
            {
                case PickupType.FlyingXp:
                    _pendingXpGrant += ComputeXpGrant(value);
                    break;
                case PickupType.FlyingGold:
                    _pendingGoldGrant += value;
                    break;
                case PickupType.FlyingHealth:
                    _pendingHealGrant += value;
                    break;
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

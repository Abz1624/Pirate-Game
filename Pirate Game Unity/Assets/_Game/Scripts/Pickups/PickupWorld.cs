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
            var xpMult = pacingSettings != null ? pacingSettings.xpDropMultiplier : 1f;
            Spawn(PickupType.Infamy, Mathf.Max(1, Mathf.RoundToInt(xp * xpMult)), pos + Random.insideUnitSphere * 0.5f);
            if (gold > 0)
                Spawn(PickupType.Gold, rich ? gold * 2 : gold, pos + Random.insideUnitSphere * 0.5f);
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

            pos.y = 0.35f;
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
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                ref var p = ref _pool[i];
                var toPlayer = playerPos - p.position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;
                if (dist < pickupRadius)
                {
                    var speed = Mathf.Lerp(4f, 18f, 1f - dist / pickupRadius);
                    p.position += toPlayer.normalized * speed * dt;
                    p.basePosition = p.position;
                }
                else
                {
                    var bob = Mathf.Sin(_time * 3.5f + p.bobPhase) * 0.12f;
                    p.position = p.basePosition + Vector3.up * bob;
                }

                if (p.view != null)
                {
                    p.view.position = p.position;
                    p.view.Rotate(0f, 180f * dt, 0f, Space.World);
                }

                if (dist < 0.6f) Collect(i);
            }
        }

        void Collect(int i)
        {
            ref var p = ref _pool[i];
            if (p.type == PickupType.Infamy)
                UI.LevelSystem.Instance?.AddXP(Mathf.RoundToInt(p.value * (RunManager.Instance?.Stats.XPGain ?? 1f)));
            else if (p.type == PickupType.Gold)
                RunManager.Instance?.AddGold(Mathf.RoundToInt(p.value * (RunManager.Instance?.Stats.GoldGain ?? 1f)));
            else if (p.type == PickupType.Powerup)
            {
                var player = Object.FindFirstObjectByType<Player.PlayerShipController>();
                player?.Damage.Heal(p.value);
            }

            if (p.view != null)
                _viewPool.Release(p.view, p.type, p.value);
            p.active = false;
            _active--;
        }
    }
}

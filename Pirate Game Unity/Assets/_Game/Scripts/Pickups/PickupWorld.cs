using System;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Pickups
{
    public struct PickupAgent
    {
        public bool active;
        public PickupType type;
        public int value;
        public Vector3 position;
        public Transform view;
    }

    public sealed class PickupWorld : MonoBehaviour
    {
        public static PickupWorld Instance { get; private set; }

        [SerializeField] Transform root;
        PickupAgent[] _pool;
        int _active;

        void Awake()
        {
            Instance = this;
            _pool = new PickupAgent[Platform.QualityProfile.Current.pickupCap];
        }

        public void SpawnFromEnemy(Vector3 pos, int xp, int gold, bool rich)
        {
            Spawn(PickupType.Infamy, Mathf.Max(1, xp), pos + UnityEngine.Random.insideUnitSphere * 0.5f);
            if (gold > 0)
                Spawn(PickupType.Gold, rich ? gold * 2 : gold, pos + UnityEngine.Random.insideUnitSphere * 0.5f);
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

            var color = type == PickupType.Gold ? Color.yellow : Color.cyan;
            var view = CreateView(color);
            view.position = pos;
            _pool[slot] = new PickupAgent
            {
                active = true,
                type = type,
                value = value,
                position = pos,
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

        Transform CreateView(Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(root != null ? root : transform, false);
            go.transform.localScale = Vector3.one * 0.4f;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = color;
            return go.transform;
        }

        public void Tick(float dt, Vector3 playerPos, float pickupRadius)
        {
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
                }
                if (p.view != null) p.view.position = p.position;
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

            if (p.view != null) Destroy(p.view.gameObject);
            p.active = false;
            _active--;
        }
    }
}

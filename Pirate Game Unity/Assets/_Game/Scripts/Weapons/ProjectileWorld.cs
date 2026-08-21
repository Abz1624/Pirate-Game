using System;
using HighSeas.Core;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.Weapons
{
    public struct Projectile
    {
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float damage;
        public float life;
        public float radius;
        public int pierceLeft;
        public bool isCrit;
        public Transform view;
    }

    public sealed class ProjectileWorld : MonoBehaviour
    {
        public static ProjectileWorld Instance { get; private set; }

        [SerializeField] Transform root;
        Projectile[] _pool;
        int _active;

        void Awake()
        {
            Instance = this;
            _pool = new Projectile[Platform.QualityProfile.Current.projectileCap];
        }

        public bool TrySpawn(Vector3 pos, Vector3 dir, float speed, float damage, float life, float radius, int pierce, bool isCrit, Color color)
        {
            if (_active >= _pool.Length) return false;
            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) { slot = i; break; }
            }
            if (slot < 0) return false;

            var view = CreateView(color, radius);
            view.position = pos;
            _pool[slot] = new Projectile
            {
                active = true,
                position = pos,
                velocity = dir.normalized * speed,
                damage = damage,
                life = life,
                radius = radius,
                pierceLeft = pierce,
                isCrit = isCrit,
                view = view
            };
            _active++;
            return true;
        }

        Transform CreateView(Color color, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(root != null ? root : transform, false);
            go.transform.localScale = Vector3.one * radius * 2f;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.GetComponent<Renderer>().material.color = color;
            return go.transform;
        }

        public void Tick(float dt, EnemyWorld enemies)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].active) continue;
                ref var p = ref _pool[i];
                p.position += p.velocity * dt;
                p.life -= dt;
                if (p.view != null) p.view.position = p.position;

                if (enemies != null)
                {
                    var list = enemies.Agents;
                    for (var ei = 0; ei < list.Length; ei++)
                    {
                        if (!list[ei].active) continue;
                        var d = Vector3.Distance(p.position, list[ei].position);
                        if (d > p.radius + list[ei].radius) continue;
                        enemies.ApplyDamage(ei, p.damage, p.isCrit);
                        Meta.TriggerBus.Instance?.RaiseHit(p.damage, p.isCrit);
                        p.pierceLeft--;
                        if (p.pierceLeft < 0) break;
                    }
                }

                if (p.life <= 0f || p.pierceLeft < 0)
                {
                    Despawn(i);
                }
            }
        }

        void Despawn(int i)
        {
            if (!_pool[i].active) return;
            if (_pool[i].view != null) Destroy(_pool[i].view.gameObject);
            _pool[i].active = false;
            _active--;
        }
    }
}

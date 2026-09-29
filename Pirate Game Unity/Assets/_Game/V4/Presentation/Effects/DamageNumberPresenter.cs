using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Floating damage numbers (M14; port of V3's FloatingDamageNumbers, which created and destroyed a TextMesh object per
    /// number): enemy numbers on hits (at most 14 a frame) and player numbers above the ship, same sizes, colours, rise and
    /// fade, from a prewarmed pool.
    /// </summary>
    public sealed class DamageNumberPresenter : MonoBehaviour
    {
        public const string PoolKey = "fx/damage_number";
        const float EnemyLifetime = 0.95f;
        const float PlayerLifetime = 1.05f;
        const float RiseSpeed = 2.8f;
        const int EnemyNumbersPerFrame = 14;

        struct Floater
        {
            public GameObject Go;
            public TextMesh Text;
            public float Life, MaxLife;
            public Vector3 Velocity;
        }

        readonly List<Floater> _floaters = new List<Floater>(96);
        PoolService _pools;
        Func<Vector3?> _shipPosition;
        GameObject _template;
        int _enemyNumbersThisFrame;
        int _frame = -1;

        public int Showing => _floaters.Count;

        public void Bind(PoolService pools, Func<Vector3?> shipPosition, int prewarm = 48)
        {
            _pools = pools;
            _shipPosition = shipPosition;
            if (_pools == null || _pools.Has(PoolKey)) return;
            _template = new GameObject("DamageNumberTemplate");
            _template.SetActive(false);
            _template.transform.SetParent(transform, false);
            var text = _template.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                var mesh = _template.GetComponent<MeshRenderer>();
                if (mesh != null) { mesh.sharedMaterial = font.material; mesh.sortingOrder = 250; }
            }
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            _pools.Register(new PoolDefinition(PoolKey, _template, prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "damage numbers"));
            _pools.Prewarm(PoolKey);
        }

        public void ShowEnemyDamage(Vector3 worldPosition, float damage, bool crit)
        {
            if (damage <= 0f) return;
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _enemyNumbersThisFrame = 0; }
            if (_enemyNumbersThisFrame >= EnemyNumbersPerFrame) return;
            _enemyNumbersThisFrame++;
            Spawn(worldPosition + Vector3.up * (1.35f + UnityEngine.Random.Range(0f, 0.45f)), damage, crit, false);
        }

        public void ShowPlayerDamage(float amount)
        {
            if (amount <= 0f) return;
            var ship = _shipPosition?.Invoke();
            if (!ship.HasValue) return;
            Spawn(ship.Value + Vector3.up * 3.75f, amount, false, true);
        }

        void Spawn(Vector3 position, float damage, bool crit, bool onPlayer)
        {
            var go = _pools != null ? _pools.Acquire(PoolKey, position, Quaternion.identity) : null;
            if (go == null) return;
            var text = go.GetComponent<TextMesh>();
            text.text = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            text.fontSize = crit ? 72 : onPlayer ? 64 : 60;
            text.characterSize = crit ? 0.26f : onPlayer ? 0.24f : 0.22f;
            text.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
            text.color = onPlayer ? new Color(1f, 0.35f, 0.3f, 1f) : crit ? new Color(1f, 0.88f, 0.25f, 1f) : new Color(1f, 0.97f, 0.92f, 1f);
            FaceCamera(go.transform);
            var lifetime = onPlayer ? PlayerLifetime : EnemyLifetime;
            _floaters.Add(new Floater
            {
                Go = go, Text = text, Life = lifetime, MaxLife = lifetime,
                Velocity = Vector3.up * RiseSpeed + UnityEngine.Random.insideUnitSphere * 0.4f,
            });
        }

        void LateUpdate()
        {
            for (var i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                if (f.Go == null) { _floaters.RemoveAt(i); continue; }
                f.Life -= Time.deltaTime;
                f.Go.transform.position += f.Velocity * Time.deltaTime;
                FaceCamera(f.Go.transform);
                if (f.Text != null && f.MaxLife > 0f)
                {
                    var c = f.Text.color;
                    c.a = Mathf.Clamp01(f.Life / f.MaxLife);
                    f.Text.color = c;
                }
                if (f.Life <= 0f)
                {
                    _pools.Release(f.Go);
                    _floaters.RemoveAt(i);
                    continue;
                }
                _floaters[i] = f;
            }
        }

        static void FaceCamera(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return;
            t.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }

        void OnDestroy()
        {
            foreach (var f in _floaters) if (f.Go != null) _pools?.Release(f.Go);
            _floaters.Clear();
        }
    }
}

using System.Collections.Generic;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.V3.MultiShip;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class FloatingDamageNumbers : MonoBehaviour
    {
        public static FloatingDamageNumbers Instance { get; private set; }

        struct Floater
        {
            public Transform transform;
            public TextMesh text;
            public float life;
            public float maxLife;
            public Vector3 velocity;
        }

        readonly List<Floater> _floaters = new(96);
        int _enemyNumbersThisFrame;
        Transform _root;
        Font _font;
        bool _subscribed;

        const float EnemyLifetime = 0.95f;
        const float PlayerLifetime = 1.05f;
        const float RiseSpeed = 2.8f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            var go = new GameObject("DamageNumberRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable() => TrySubscribe();
        void OnDisable() => Unsubscribe();

        void Start() => TrySubscribe();

        void Update()
        {
            _enemyNumbersThisFrame = 0;
            if (!_subscribed)
                TrySubscribe();
        }

        void TrySubscribe()
        {
            if (_subscribed || TriggerBus.Instance == null) return;
            TriggerBus.Instance.DamageTaken -= OnPlayerDamaged;
            TriggerBus.Instance.DamageTaken += OnPlayerDamaged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || TriggerBus.Instance == null) return;
            TriggerBus.Instance.DamageTaken -= OnPlayerDamaged;
            _subscribed = false;
        }

        public void Rebind() => TrySubscribe();

        public void ShowEnemyDamage(Vector3 worldPosition, float damage, bool crit)
        {
            if (damage <= 0f) return;
            if (_enemyNumbersThisFrame >= 14) return;
            _enemyNumbersThisFrame++;
            var spawn = worldPosition + Vector3.up * (1.35f + Random.Range(0f, 0.45f));
            Spawn(spawn, damage, crit, false);
        }

        void OnPlayerDamaged(float amount)
        {
            if (amount <= 0f) return;
            var ship = V3MultiShipWeaponLoadouts.FindLocalHumanShip();
            if (ship == null)
                ship = FindFirstObjectByType<PlayerShipController>();
            if (ship == null) return;
            Spawn(ship.Position + Vector3.up * 3.75f, amount, false, true);
        }

        void Spawn(Vector3 position, float damage, bool crit, bool onPlayer)
        {
            var go = new GameObject(onPlayer ? "PlayerDmg" : crit ? "EnemyDmg_Crit" : "EnemyDmg");
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            var text = go.AddComponent<TextMesh>();
            if (_font != null)
                text.font = _font;
            text.text = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            text.fontSize = crit ? 72 : onPlayer ? 64 : 60;
            text.characterSize = crit ? 0.26f : onPlayer ? 0.24f : 0.22f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
            text.color = onPlayer
                ? new Color(1f, 0.35f, 0.3f, 1f)
                : crit
                    ? new Color(1f, 0.88f, 0.25f, 1f)
                    : new Color(1f, 0.97f, 0.92f, 1f);
            var mesh = go.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.sortingOrder = 250;
            FaceCamera(go.transform);
            var lifetime = onPlayer ? PlayerLifetime : EnemyLifetime;
            _floaters.Add(new Floater
            {
                transform = go.transform,
                text = text,
                life = lifetime,
                maxLife = lifetime,
                velocity = Vector3.up * RiseSpeed + Random.insideUnitSphere * 0.4f
            });
        }

        void LateUpdate()
        {
            for (var i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                if (f.transform == null)
                {
                    _floaters.RemoveAt(i);
                    continue;
                }

                f.life -= Time.deltaTime;
                f.transform.position += f.velocity * Time.deltaTime;
                FaceCamera(f.transform);

                if (f.text != null && f.maxLife > 0f)
                {
                    var c = f.text.color;
                    c.a = Mathf.Clamp01(f.life / f.maxLife);
                    f.text.color = c;
                }

                if (f.life <= 0f)
                {
                    Destroy(f.transform.gameObject);
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
    }
}

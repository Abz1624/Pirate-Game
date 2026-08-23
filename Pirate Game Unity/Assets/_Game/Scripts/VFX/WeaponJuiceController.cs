using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class WeaponJuiceController : MonoBehaviour
    {
        [SerializeField] DemoJuiceSettings settings;
        [SerializeField] Transform effectRoot;

        WeaponController _weapons;
        PlayerShipController _player;
        JuiceVfxBroker _broker;
        Transform _root;

        readonly Dictionary<string, Transform> _cutlasses = new();
        readonly List<Transform> _activeMines = new();
        Transform _burningTrail;
        Transform _flamethrower;
        float _burningTimer;
        float _flameTimer;

        void Awake()
        {
            if (settings == null)
                settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _root = effectRoot != null ? effectRoot : transform;
            _weapons = FindFirstObjectByType<WeaponController>();
            _player = FindFirstObjectByType<PlayerShipController>();
            _broker = JuiceVfxBroker.Instance;
        }

        void Update()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            if (_player == null || _weapons == null || settings == null) return;

            SyncCutlasses();
            TickBurningTrail();
            TickFlamethrower();
            CleanupMines();
        }

        public void OnProjectileFired(Vector3 origin, Vector3 direction)
        {
            if (settings.shootingVfxPrefab == null) return;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            _broker?.Play(settings.shootingVfxPrefab, origin, rotation, settings.shootingVfxScale, 0.35f);
        }

        public void OnMineDropped(Vector3 position)
        {
            if (settings.mineBarrelPrefab == null) return;
            var mine = Instantiate(settings.mineBarrelPrefab, position, Quaternion.identity, _root);
            mine.transform.localScale = Vector3.one * settings.mineBarrelScale;
            StripPhysics(mine);
            _activeMines.Add(mine.transform);
        }

        public void OnLightningStrike(Vector3 position)
        {
            _broker?.Play(
                settings.lightningStrikePrefab,
                position + Vector3.up * 0.2f,
                Quaternion.identity,
                settings.lightningStrikeScale);
        }

        void SyncCutlasses()
        {
            var hasCutlasses = false;
            var count = 0;
            for (var i = 0; i < _weapons.Weapons.Count; i++)
            {
                if (_weapons.Weapons[i].def.id != "flying_cutlasses") continue;
                hasCutlasses = true;
                count = 2 + _weapons.Weapons[i].quantityBonus;
                break;
            }

            if (!hasCutlasses)
            {
                foreach (var kv in _cutlasses)
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                _cutlasses.Clear();
                return;
            }

            while (_cutlasses.Count < count)
            {
                var key = $"cutlass_{_cutlasses.Count}";
                Transform view;
                if (settings.cutlassPrefab != null)
                {
                    var instance = Instantiate(settings.cutlassPrefab, _root);
                    instance.transform.localScale = Vector3.one * settings.cutlassScale;
                    StripPhysics(instance);
                    view = instance.transform;
                }
                else
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    view.SetParent(_root, false);
                    view.localScale = Vector3.one * 0.3f;
                    Destroy(view.GetComponent<Collider>());
                }

                _cutlasses[key] = view;
            }

            while (_cutlasses.Count > count)
            {
                string lastKey = null;
                foreach (var key in _cutlasses.Keys)
                    lastKey = key;
                if (lastKey == null) break;
                if (_cutlasses[lastKey] != null)
                    Destroy(_cutlasses[lastKey].gameObject);
                _cutlasses.Remove(lastKey);
            }

            var index = 0;
            var orbitRadius = 2.5f;
            foreach (var kv in _cutlasses)
            {
                var angle = (Time.time * 180f + index * (360f / Mathf.Max(1, count))) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(angle), 0.35f, Mathf.Cos(angle)) * orbitRadius;
                kv.Value.position = _player.Position + offset;
                kv.Value.rotation = Quaternion.LookRotation(_player.MovementForward, Vector3.up);
                index++;
            }
        }

        void TickBurningTrail()
        {
            var hasBurningOil = false;
            for (var i = 0; i < _weapons.Weapons.Count; i++)
            {
                if (_weapons.Weapons[i].def.id != "burning_oil") continue;
                hasBurningOil = true;
                break;
            }

            if (!hasBurningOil)
            {
                if (_burningTrail != null)
                {
                    Destroy(_burningTrail.gameObject);
                    _burningTrail = null;
                }

                return;
            }

            if (_player.Velocity.sqrMagnitude < 0.5f) return;

            _burningTimer -= Time.deltaTime;
            if (_burningTimer > 0f) return;
            _burningTimer = 0.18f;

            var trailPos = _player.Position - _player.MovementForward * 1.2f;
            trailPos.y = 0.2f;
            if (settings.burningTrailPrefab != null)
                _broker?.Play(settings.burningTrailPrefab, trailPos, Quaternion.identity, settings.burningTrailScale, 1.2f);
        }

        void TickFlamethrower()
        {
            var hasBurningOil = false;
            for (var i = 0; i < _weapons.Weapons.Count; i++)
            {
                if (_weapons.Weapons[i].def.id != "burning_oil") continue;
                hasBurningOil = true;
                break;
            }

            if (!hasBurningOil)
            {
                if (_flamethrower != null)
                {
                    Destroy(_flamethrower.gameObject);
                    _flamethrower = null;
                }

                return;
            }

            if (_flamethrower == null && settings.flamethrowerPrefab != null)
            {
                var instance = Instantiate(settings.flamethrowerPrefab, _root);
                instance.transform.localScale = Vector3.one * settings.flamethrowerScale;
                StripPhysics(instance);
                _flamethrower = instance.transform;
            }

            if (_flamethrower == null) return;

            _flamethrower.position = _player.Position + Vector3.up * 0.6f;
            _flamethrower.rotation = Quaternion.LookRotation(_player.MovementForward, Vector3.up);
            _flameTimer -= Time.deltaTime;
            if (_flameTimer > 0f) return;
            _flameTimer = 0.12f;

            var particles = _flamethrower.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }

        void CleanupMines()
        {
            for (var i = _activeMines.Count - 1; i >= 0; i--)
            {
                if (_activeMines[i] == null)
                    _activeMines.RemoveAt(i);
            }

            while (_activeMines.Count > 8)
            {
                var oldest = _activeMines[0];
                _activeMines.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest.gameObject);
            }
        }

        static void StripPhysics(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);

            var obstacles = root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
            for (var i = 0; i < obstacles.Length; i++)
                Destroy(obstacles[i]);
        }
    }
}

using System.Collections.Generic;
using HighSeas.Combat;
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
        readonly List<CutlassHitSphere> _cutlassHits = new();
        Transform _flamethrowerPort;
        float _flameTimer;

        const float FlamethrowerForwardOffset = 1.65f;
        const float FlamethrowerVisualScaleMult = 1.35f;

        public struct CutlassHitSphere
        {
            public Vector3 position;
            public float radius;
        }

        public IReadOnlyList<CutlassHitSphere> CutlassHits => _cutlassHits;

        const float CutlassOrbitRadius = 5f;
        const float CutlassHitRadius = 2.75f;

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
            TickFlamethrower();
        }

        public void OnProjectileFired(Vector3 origin, Vector3 direction)
        {
            if (settings.shootingVfxPrefab == null) return;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            _broker?.Play(settings.shootingVfxPrefab, origin, rotation, settings.shootingVfxScale, 0.35f);
        }

        public Transform OnMineDropped(Vector3 position)
        {
            if (settings.mineBarrelPrefab == null) return null;
            var mine = Instantiate(settings.mineBarrelPrefab, position, Quaternion.identity, _root);
            mine.transform.localScale = Vector3.one * settings.mineBarrelScale;
            StripPhysics(mine);
            return mine.transform;
        }

        public void OnMineExploded(Vector3 position, float blastRadius)
        {
            var prefab = settings.eliteKillExplosionPrefab != null
                ? settings.eliteKillExplosionPrefab
                : settings.enemyKillPrefab;
            if (prefab == null) return;

            var scale = settings.powderKegExplosionScale * Mathf.Clamp(blastRadius / 2.8f, 0.85f, 1.4f);
            PlayVfx(prefab, position + Vector3.up * 0.35f, Quaternion.identity, scale, 2f);
        }

        public void OnLightningStrike(Vector3 position, float areaRadius)
        {
            if (settings.lightningStrikePrefab == null) return;
            var strikePos = position;
            strikePos.y = 0.05f;
            var scale = settings.lightningStrikeScale * Mathf.Clamp(areaRadius / 3.5f, 0.85f, 1.35f);
            PlayLightningStrike(settings.lightningStrikePrefab, strikePos, scale, 1.15f);
        }

        void PlayLightningStrike(GameObject prefab, Vector3 position, float scale, float lifetime)
        {
            _broker ??= JuiceVfxBroker.Instance;
            if (_broker != null)
            {
                var instance = _broker.Pool.AcquirePublic(prefab);
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                instance.transform.localScale = Vector3.one * scale;
                PrepareLightningParticles(instance);
                var autoReturn = instance.GetComponent<VfxAutoReturn>();
                if (autoReturn == null)
                    autoReturn = instance.AddComponent<VfxAutoReturn>();
                autoReturn.Play(lifetime, go => _broker.Pool.ReleasePublic(prefab, go));
                return;
            }

            var fallback = Instantiate(prefab, position, Quaternion.identity, _root);
            fallback.transform.localScale = Vector3.one * scale;
            PrepareLightningParticles(fallback);
            Destroy(fallback, lifetime);
        }

        static void PrepareLightningParticles(GameObject instance)
        {
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.startDelay = 0f;
                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }

        void PlayVfx(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetime)
        {
            _broker ??= JuiceVfxBroker.Instance;
            if (_broker != null)
            {
                _broker.Play(prefab, position, rotation, scale, lifetime);
                return;
            }

            var instance = Instantiate(prefab, position, rotation, _root);
            instance.transform.localScale = Vector3.one * scale;
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }

            Destroy(instance, lifetime);
        }

        public void OnFlamethrowerBurst(Vector3 origin, Vector3 aim)
        {
            if (_flamethrowerPort == null) return;
            _flamethrowerPort.position = origin + Vector3.up * 0.45f;
            _flamethrowerPort.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
            PlayFlamethrowerParticles(_flamethrowerPort);
        }

        static void PlayFlamethrowerParticles(Transform mount)
        {
            if (mount == null) return;
            var particles = mount.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }

        public void OnMortarTelegraph(Vector3 position, float radius)
        {
            if (settings.burningTrailPrefab == null) return;
            position.y = 0.2f;
            var scale = settings.burningTrailScale * Mathf.Clamp(radius / 4.5f, 0.85f, 1.45f);
            _broker?.Play(settings.burningTrailPrefab, position, Quaternion.identity, scale, 1.15f);
        }

        public void OnBroadsideSalvo(Vector3 shipPosition, Vector3 sideDirection)
        {
            if (settings.shootingVfxPrefab == null) return;
            sideDirection.y = 0f;
            if (sideDirection.sqrMagnitude < 0.0001f) return;
            sideDirection.Normalize();
            var forward = Vector3.Cross(Vector3.up, sideDirection).normalized;
            for (var i = -1; i <= 1; i++)
            {
                var origin = shipPosition + sideDirection * 1.1f + forward * (i * 0.55f) + Vector3.up * 0.35f;
                var rotation = Quaternion.LookRotation(sideDirection, Vector3.up);
                _broker?.Play(settings.shootingVfxPrefab, origin, rotation, settings.shootingVfxScale * 1.15f, 0.4f);
            }
        }

        public void OnBurningOilDropped(Vector3 position, float radius)
        {
            if (settings.burningTrailPrefab == null) return;
            var scale = settings.burningTrailScale * Mathf.Clamp(radius / 2.8f, 0.75f, 1.6f);
            _broker?.Play(settings.burningTrailPrefab, position, Quaternion.identity, scale, 1.6f);
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
                _cutlassHits.Clear();
                return;
            }

            _cutlassHits.Clear();

            while (_cutlasses.Count < count)
            {
                var key = $"cutlass_{_cutlasses.Count}";
                Transform view;
                if (settings.cutlassPrefab != null)
                {
                    var instance = Instantiate(settings.cutlassPrefab, _root);
                    instance.transform.localScale = Vector3.one * settings.cutlassScale;
                    StripPhysics(instance);
                    EnsureCutlassCollider(instance, settings.cutlassScale);
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

            var orbitRadius = CutlassOrbitRadius;
            if (WeaponEvolutionCatalog.GetBehaviorModifier("flying_cutlasses") == "bladed_tide")
                orbitRadius *= 0.72f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.75f));

            var index = 0;
            var flatRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var kv in _cutlasses)
            {
                var angle = (Time.time * 180f + index * (360f / Mathf.Max(1, count))) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(angle), 0.35f, Mathf.Cos(angle)) * orbitRadius;
                var worldPos = _player.Position + offset;
                kv.Value.position = worldPos;
                kv.Value.rotation = Quaternion.LookRotation(_player.MovementForward, Vector3.up) * flatRotation;
                _cutlassHits.Add(new CutlassHitSphere
                {
                    position = worldPos,
                    radius = CutlassHitRadius
                });
                index++;
            }
        }

        static void EnsureCutlassCollider(GameObject root, float visualScale)
        {
            var sphere = root.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = CutlassHitRadius / Mathf.Max(visualScale, 0.01f);
            sphere.center = Vector3.zero;
        }

        void TickFlamethrower()
        {
            var hasFlamethrower = false;
            for (var i = 0; i < _weapons.Weapons.Count; i++)
            {
                var id = _weapons.Weapons[i].def.id;
                if (id != "flamethrower" && id != "burning_oil") continue;
                hasFlamethrower = true;
                break;
            }

            if (!hasFlamethrower)
            {
                DestroyFlamethrowerMount(ref _flamethrowerPort);
                return;
            }

            EnsureFlamethrowerMount(ref _flamethrowerPort);
            if (_flamethrowerPort == null) return;

            var forward = _player.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = _player.transform.forward;
            forward.Normalize();
            SetFlamethrowerPose(_flamethrowerPort, forward);

            _flameTimer -= Time.deltaTime;
            if (_flameTimer > 0f) return;
            _flameTimer = 0.35f;

            PlayFlamethrowerParticles(_flamethrowerPort);
        }

        void EnsureFlamethrowerMount(ref Transform mount)
        {
            if (mount != null || settings.flamethrowerPrefab == null) return;
            var instance = Instantiate(settings.flamethrowerPrefab, _root);
            instance.transform.localScale = Vector3.one * settings.flamethrowerScale * FlamethrowerVisualScaleMult;
            StripPhysics(instance);
            mount = instance.transform;
        }

        static void DestroyFlamethrowerMount(ref Transform mount)
        {
            if (mount == null) return;
            Destroy(mount.gameObject);
            mount = null;
        }

        void SetFlamethrowerPose(Transform mount, Vector3 forward)
        {
            if (mount == null) return;
            mount.position = _player.Position + forward * FlamethrowerForwardOffset + Vector3.up * 0.45f;
            mount.rotation = Quaternion.LookRotation(forward, Vector3.up);
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

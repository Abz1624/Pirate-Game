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
        bool _flamethrowerEmissionActive;

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
        const float MortarReticleDiameterPerScale = 12.5f;
        const float MortarWindupSeconds = 1.1f;

        public static float MortarTelegraphScaleForRadius(float blastRadius, float telegraphScaleSetting)
        {
            var desiredDiameter = blastRadius * 2f;
            return telegraphScaleSetting * (desiredDiameter / MortarReticleDiameterPerScale);
        }
        static readonly Color CutlassAuraTint = new(1f, 0.9f, 0.32f, 1f);
        const float DefaultLongNineBeamHalfWidth = 0.32f;

        public float LongNineBeamHalfWidthBase =>
            settings != null ? settings.longNineBeamHalfWidth : DefaultLongNineBeamHalfWidth;

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
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_player == null || _weapons == null || settings == null) return;

            SyncCutlasses();
            TickFlamethrower();
        }

        public void OnProjectileFired(Vector3 origin, Vector3 direction) =>
            OnWeaponFired(null, origin, direction);

        public void OnWeaponFired(string weaponId, Vector3 origin, Vector3 direction)
        {
            var prefab = ResolveMuzzlePrefab(weaponId);
            if (prefab == null) return;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            var scale = settings.shootingVfxScale;
            if (weaponId is "broadside_battery")
                scale *= 1.12f;

            if (TryPlayGunChildBurst(prefab, "CannonSmoke", origin, rotation, scale, 0.35f, null))
                return;

            _broker?.Play(prefab, origin, rotation, scale, 0.35f);
        }

        public void OnLongNineBeam(Vector3 origin, Vector3 direction, float range, float halfWidth)
        {
            if (settings.laserBeamPrefab == null) return;
            _broker ??= JuiceVfxBroker.Instance;
            if (_broker == null) return;

            var instance = _broker.Pool.AcquirePublic(settings.laserBeamPrefab);
            var beam = instance.GetComponent<LongNineBeamVisual>();
            if (beam == null)
                beam = instance.AddComponent<LongNineBeamVisual>();
            beam.Configure(origin, direction, range, halfWidth);

            var autoReturn = instance.GetComponent<VfxAutoReturn>();
            if (autoReturn == null)
                autoReturn = instance.AddComponent<VfxAutoReturn>();
            autoReturn.Play(settings.laserBeamDuration, go => _broker.Pool.ReleasePublic(settings.laserBeamPrefab, go));
        }

        GameObject ResolveMuzzlePrefab(string weaponId)
        {
            if (settings == null) return null;
            if (settings.cannonMuzzleVfxPrefab != null)
                return settings.cannonMuzzleVfxPrefab;
            if (weaponId is "long_nine" or "deck_cannons" or "broadside_battery" or "swivel_guns" or "grapeshot" or "mortar")
                return settings.shootingVfxPrefab;
            return settings.shootingVfxPrefab;
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

        static void PrepareLightningParticles(GameObject instance) => VfxParticleUtil.RestartParticles(instance);

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
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            aim.Normalize();
            _flamethrowerPort.position = origin + Vector3.up * 0.45f;
            _flamethrowerPort.rotation = Quaternion.LookRotation(aim, Vector3.up);
        }

        static void SetFlamethrowerEmission(Transform mount, bool active)
        {
            if (mount == null) return;
            var particles = mount.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                if (active)
                {
                    if (!particles[i].isPlaying)
                        particles[i].Play(true);
                }
                else if (particles[i].isPlaying)
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void OnMortarTelegraph(Vector3 position, float radius)
        {
            position.y = 0.15f;
            var scale = MortarTelegraphScaleForRadius(radius, settings.mortarTelegraphScale);
            var lifetime = MortarWindupSeconds + 0.12f;
            var template = settings.mortarTelegraphPrefab != null
                ? settings.mortarTelegraphPrefab
                : settings.lightningStrikePrefab;
            if (template != null && TryPlayMortarReticle(template, position, scale, lifetime))
                return;

            if (settings.bossTelegraphRingPrefab != null)
            {
                _broker ??= JuiceVfxBroker.Instance;
                _broker?.Play(settings.bossTelegraphRingPrefab, position, Quaternion.identity, scale * 0.55f, lifetime);
                return;
            }

            if (settings.burningTrailPrefab == null) return;
            scale = settings.burningTrailScale * Mathf.Clamp(radius / 8f, 0.55f, 0.95f);
            _broker?.Play(settings.burningTrailPrefab, position, Quaternion.identity, scale, lifetime);
        }

        bool TryPlayMortarReticle(GameObject sourcePrefab, Vector3 position, float scale, float lifetime)
        {
            var reticle = sourcePrefab.transform.Find("Visuals/WaterReticle");
            if (reticle == null) return false;

            var instance = Instantiate(reticle.gameObject);
            instance.name = "MortarTelegraph";
            instance.SetActive(true);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scale;
            if (_root != null)
                instance.transform.SetParent(_root, true);

            VfxParticleUtil.SetRenderersEnabled(instance, true);
            var tint = settings.mortarTelegraphColor;
            VfxParticleUtil.ApplyTint(
                instance,
                tint,
                settings.mortarTelegraphTintSaturation,
                settings.mortarTelegraphTintAlpha);
            VfxParticleUtil.RestartParticles(instance);
            Destroy(instance, lifetime);
            return true;
        }

        public void OnMortarImpact(Vector3 position, float radius, RunRng rng)
        {
            if (settings.weaponExplosionPrefab == null)
            {
                OnMineExploded(position, radius);
                return;
            }

            var burstCount = rng != null
                ? 4 + Mathf.FloorToInt(rng.CombatFloat() * 3f)
                : 4;
            var smallScale = settings.weaponExplosionScale * Mathf.Clamp(radius / 11f, 0.18f, 0.34f);
            // Keep bursts inside the visible telegraph ring (and away from the edge by VFX size).
            var ringRadius = MortarTelegraphScaleForRadius(radius, settings.mortarTelegraphScale)
                * MortarReticleDiameterPerScale * 0.5f;
            var vfxPad = smallScale * 1.6f;
            var maxSpawnRadius = Mathf.Max(0.2f, ringRadius * 0.72f - vfxPad);
            for (var i = 0; i < burstCount; i++)
            {
                var angle = rng != null ? rng.CombatFloat() * Mathf.PI * 2f : i * 1.05f;
                var dist = maxSpawnRadius * (rng != null
                    ? Mathf.Sqrt(rng.CombatFloat())
                    : (i + 1f) / (burstCount + 1f));
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                var burstPos = position + offset + Vector3.up * 0.3f;
                PlayVfx(settings.weaponExplosionPrefab, burstPos, Quaternion.identity, smallScale, 1.05f);
            }
        }

        void PlayMortarTelegraph(GameObject prefab, Vector3 position, float scale, float lifetime)
        {
            var tint = new Color(1f, 0.34f, 0.12f, 1f);
            if (TryPlayGunChildBurst(prefab, "Visuals/WaterReticle", position, Quaternion.identity, scale, lifetime, tint))
                return;

            _broker ??= JuiceVfxBroker.Instance;
            if (_broker != null)
            {
                var instance = _broker.Pool.AcquirePublic(prefab);
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                instance.transform.localScale = Vector3.one * scale;
                VfxParticleUtil.ApplyTint(instance, tint);
                VfxParticleUtil.RestartParticles(instance);
                var autoReturn = instance.GetComponent<VfxAutoReturn>();
                if (autoReturn == null)
                    autoReturn = instance.AddComponent<VfxAutoReturn>();
                autoReturn.Play(lifetime, go => _broker.Pool.ReleasePublic(prefab, go));
                return;
            }

            var fallback = Instantiate(prefab, position, Quaternion.identity, _root);
            fallback.transform.localScale = Vector3.one * scale;
            VfxParticleUtil.ApplyTint(fallback, tint);
            VfxParticleUtil.RestartParticles(fallback);
            Destroy(fallback, lifetime);
        }

        bool TryPlayGunChildBurst(
            GameObject gunPrefab,
            string childPath,
            Vector3 position,
            Quaternion rotation,
            float scale,
            float lifetime,
            Color? tint)
        {
            if (gunPrefab == null || string.IsNullOrEmpty(childPath)) return false;
            var child = gunPrefab.transform.Find(childPath);
            if (child == null) return false;

            var temp = Instantiate(gunPrefab);
            temp.name = $"{gunPrefab.name}_{childPath.Replace('/', '_')}_Burst";
            temp.transform.SetPositionAndRotation(position, rotation);
            temp.transform.localScale = Vector3.one * scale;
            if (_root != null)
                temp.transform.SetParent(_root, true);
            var liveChild = temp.transform.Find(childPath);
            if (liveChild == null)
            {
                Destroy(temp);
                return false;
            }

            DisableAllExcept(temp.transform, liveChild);
            if (tint.HasValue)
                VfxParticleUtil.ApplyTint(liveChild.gameObject, tint.Value);
            VfxParticleUtil.RestartParticles(liveChild.gameObject);
            Destroy(temp, lifetime);
            return true;
        }

        static void DisableAllExcept(Transform root, Transform keep)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].enabled = renderers[i].transform.IsChildOf(keep) || renderers[i].transform == keep;
            }

            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var t = particles[i].transform;
                var enabled = t == keep || t.IsChildOf(keep);
                if (enabled) continue;
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                t.gameObject.SetActive(false);
            }
        }

        public void OnBroadsideSalvo(Vector3 shipPosition, Vector3 sideDirection)
        {
            var prefab = ResolveMuzzlePrefab("broadside_battery");
            if (prefab == null) return;
            sideDirection.y = 0f;
            if (sideDirection.sqrMagnitude < 0.0001f) return;
            sideDirection.Normalize();
            var forward = Vector3.Cross(Vector3.up, sideDirection).normalized;
            for (var i = -1; i <= 1; i++)
            {
                var origin = shipPosition + sideDirection * 1.1f + forward * (i * 0.55f) + Vector3.up * 0.35f;
                var rotation = Quaternion.LookRotation(sideDirection, Vector3.up);
                var scale = settings.shootingVfxScale * 1.15f;
                if (!TryPlayGunChildBurst(prefab, "CannonSmoke", origin, rotation, scale, 0.4f, null))
                    _broker?.Play(prefab, origin, rotation, scale, 0.4f);
            }
        }

        public void OnBurningOilDropped(Vector3 position, float radius)
        {
            if (settings.burningTrailPrefab == null) return;
            var scale = settings.burningTrailScale * Mathf.Clamp(radius / 2.8f, 0.75f, 1.6f);
            _broker?.Play(settings.burningTrailPrefab, position, Quaternion.identity, scale, 1.6f);
        }

        Transform GetCutlassOrbitAnchor() => _player != null ? _player.transform : _root;

        void SyncCutlasses()
        {
            var hasCutlasses = false;
            var count = 0;
            var sizeMult = 1f;
            for (var i = 0; i < _weapons.Weapons.Count; i++)
            {
                if (_weapons.Weapons[i].def.id != "flying_cutlasses") continue;
                hasCutlasses = true;
                count = 2 + _weapons.Weapons[i].quantityBonus;
                sizeMult = _weapons.Weapons[i].sizeMult;
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
            var orbitAnchor = GetCutlassOrbitAnchor();

            while (_cutlasses.Count < count)
            {
                var key = $"cutlass_{_cutlasses.Count}";
                Transform view;
                if (settings.cutlassPrefab != null)
                {
                    var instance = Instantiate(settings.cutlassPrefab, orbitAnchor);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one * settings.cutlassScale;
                    ActivateCutlassVisuals(instance);
                    HideCutlassRootMesh(instance);
                    VfxParticleUtil.ApplyTint(instance, CutlassAuraTint, 0.72f);
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

            var orbitRadius = CutlassOrbitRadius * sizeMult;
            if (WeaponEvolutionCatalog.GetBehaviorModifier("flying_cutlasses") == "bladed_tide")
                orbitRadius *= 0.72f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.75f));

            var index = 0;
            var flatRotation = Quaternion.Euler(90f, 0f, 0f);
            var shipForward = _player.MovementForward;
            shipForward.y = 0f;
            if (shipForward.sqrMagnitude < 0.0001f)
                shipForward = orbitAnchor.forward;
            shipForward.Normalize();
            foreach (var kv in _cutlasses)
            {
                if (kv.Value == null) continue;
                if (kv.Value.parent != orbitAnchor)
                    kv.Value.SetParent(orbitAnchor, false);

                var angle = (Time.time * 180f + index * (360f / Mathf.Max(1, count))) * Mathf.Deg2Rad;
                var localOffset = new Vector3(Mathf.Sin(angle), 0.35f, Mathf.Cos(angle)) * orbitRadius;
                kv.Value.localPosition = localOffset;
                kv.Value.rotation = Quaternion.LookRotation(shipForward, Vector3.up) * flatRotation;
                VfxParticleUtil.ApplyTint(kv.Value.gameObject, CutlassAuraTint, 0.55f);
                _cutlassHits.Add(new CutlassHitSphere
                {
                    position = kv.Value.position,
                    radius = CutlassHitRadius
                });
                index++;
            }
        }

        static void ActivateCutlassVisuals(GameObject root)
        {
            if (root == null) return;
            var blade = root.transform.Find("Blade");
            if (blade != null)
                blade.gameObject.SetActive(true);
            var trails = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < trails.Length; i++)
                trails[i].gameObject.SetActive(true);
        }

        static void HideCutlassRootMesh(GameObject root)
        {
            if (root == null) return;
            var mesh = root.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = false;
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
                if (_flamethrowerEmissionActive && _flamethrowerPort != null)
                    SetFlamethrowerEmission(_flamethrowerPort, false);
                _flamethrowerEmissionActive = false;
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

            if (!_flamethrowerEmissionActive)
            {
                SetFlamethrowerEmission(_flamethrowerPort, true);
                _flamethrowerEmissionActive = true;
            }
        }

        void EnsureFlamethrowerMount(ref Transform mount)
        {
            if (mount != null || settings.flamethrowerPrefab == null) return;
            var instance = Instantiate(settings.flamethrowerPrefab, _root);
            instance.transform.localScale = Vector3.one * settings.flamethrowerScale * FlamethrowerVisualScaleMult;
            StripPhysics(instance);
            mount = instance.transform;
            SetFlamethrowerEmission(mount, true);
            _flamethrowerEmissionActive = true;
        }

        static void DestroyFlamethrowerMount(ref Transform mount)
        {
            if (mount == null) return;
            SetFlamethrowerEmission(mount, false);
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
            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
                Destroy(rigidbodies[i]);

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);

            var obstacles = root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
            for (var i = 0; i < obstacles.Length; i++)
                Destroy(obstacles[i]);
        }
    }
}

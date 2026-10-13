using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Player projectile views through the V4 view pool (M5; V3's ProjectileViewPool). The model comes from the firing
    /// weapon's config (projectileVisual) or the default projectile model; scale, tint and trails follow V3. Views are
    /// presentation only: the simulation hands out an int handle and never touches the objects.
    /// </summary>
    public sealed class ProjectileViewPresenter : IProjectileViews
    {
        public const string KeyPrefix = "projectile/";

        readonly PoolService _pools;
        readonly ConfigSetRuntime _config;
        readonly DemoJuiceSettings _settings;
        readonly Dictionary<int, GameObject> _live = new Dictionary<int, GameObject>();
        readonly HashSet<int> _prepared = new HashSet<int>();
        readonly Dictionary<string, WeaponConfig> _weapons = new Dictionary<string, WeaponConfig>();
        int _next;

        public ProjectileViewPresenter(PoolService pools, ConfigSetRuntime config, DemoJuiceSettings settings)
        {
            _pools = pools;
            _config = config;
            _settings = settings;
        }

        public int LiveCount => _live.Count;
        /// <summary>Demo: the artists' per-weapon VFX profiles (projectile model and trail).</summary>
        public Effects.VfxProfilePlayer Profiles { get; set; }
        /// <summary>Hackathon (13 Oct): the weapon prefab catalog (a weapon's projectile prefab; null: as before).</summary>
        public WeaponVisualCatalog Visuals { get; set; }

        readonly Dictionary<int, string> _liveWeapon = new Dictionary<int, string>();
        // The shots that ended this frame (whose splash a splash at their spot is): a small fixed ring, no allocation.
        const int EndedSlots = 16;
        readonly Vector3[] _endedAt = new Vector3[EndedSlots];
        readonly string[] _endedWeapon = new string[EndedSlots];
        readonly int[] _endedFrame = new int[EndedSlots];
        int _nextEnded;

        /// <summary>The weapon of a shot that ended this frame within 1.5 m of <paramref name="position"/> (null: none).</summary>
        public string EndedNear(Vector3 position)
        {
            for (var i = 0; i < EndedSlots; i++)
            {
                if (_endedFrame[i] != Time.frameCount || _endedWeapon[i] == null) continue;
                var dx = _endedAt[i].x - position.x;
                var dz = _endedAt[i].z - position.z;
                if (dx * dx + dz * dz <= 1.5f * 1.5f) return _endedWeapon[i];
            }
            return null;
        }

        /// <summary>The pool key for a model (the catalog derives these from the weapons, so they are prewarmed).</summary>
        public static string KeyFor(GameObject prefab) => prefab != null ? KeyPrefix + prefab.name : null;

        public int Acquire(string weaponId, Vector3 position, Color color, float radius)
        {
            var visual = VisualOf(weaponId);
            // Playtest (3 Oct, owner): a weapon that is not a cannon and has no projectile model of its own (the Monkey
            // Gunners' shots) gets its coloured placeholder, not the default cannonball.
            // V5 M8z (7 Oct): which placeholder is the weapon's data (visual.placeholderLook).
            var look = ConfigOf(weaponId)?.visual?.placeholderLook ?? WeaponPlaceholderLook.None;
            var placeholder = visual != null && visual.prefab != null ? null : WeaponPlaceholderModels.Projectile(look, _pools != null ? _pools.Staging : null);
            var prefab = placeholder != null ? placeholder : visual != null && visual.prefab != null ? visual.prefab : _settings != null ? _settings.projectilePrefab : null;
            var scaleMult = visual != null ? visual.scaleMultiplier : 1f;
            var visualRadius = radius * (visual != null ? visual.radiusMultiplier : 1f);
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var scale = Mathf.Max(0.2f, visualRadius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f) * 2f * sizeMult * scaleMult);
            var profile = Profiles != null ? Profiles.Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Projectile) : null;
            if (profile != null && profile.enabled)
            {
                if (profile.prefab != null) prefab = profile.prefab;
                scale *= profile.ScaleOrOne;
                if (profile.useTint) color = profile.tint;
            }
            // Hackathon art pass (12 Oct): the art team's projectile model for this weapon (the chainshot), in its own
            // colours; prewarmed by the pool catalog with the weapon's other projectile views.
            var art = ArtProjectile(_settings, weaponId);
            if (art != null)
            {
                prefab = art.projectileModel;
                scale *= art.projectileScale;
                placeholder = art.projectileModel;
            }

            // Hackathon (13 Oct): the weapon's projectile prefab (a copy of the model above, in its folder) is drawn
            // instead, prepared as the model it copies (harpoon, cannonball trails); its tuning scales it and says whether
            // it keeps its own colours (else it is tinted with the weapon's colour, as the cannonballs are).
            var source = prefab;
            var own = Visuals != null && (profile == null || !profile.enabled || profile.prefab == null) ? Visuals.Get(weaponId, WeaponVisualSlot.Projectile) : null;
            var tint = placeholder == null;
            if (own != null)
            {
                prefab = own;
                scale *= WeaponVisualTuning.Scale(own);
                var tuning = WeaponVisualTuning.Of(own);
                if (tuning != null) tint = !tuning.keepOwnColours;
            }

            var key = KeyFor(prefab);
            if (key == null) return -1;
            if (!_pools.Has(key)) _pools.Register(new PoolDefinition(key, prefab, 0, 0, ConfigEnums.PoolOverflowPolicy.Grow, "projectile (on demand)"));
            var go = _pools.Acquire(key, position, Quaternion.identity);
            if (go == null) return -1;

            var isHarpoon = _settings != null && _settings.harpoonProjectilePrefab != null && source == _settings.harpoonProjectilePrefab;
            var cannonballTrail = VfxParticleUtil.UsesCannonballTrail(source, _settings);
            if (_prepared.Add(go.GetInstanceID()))
            {
                StripPhysics(go);
                if (isHarpoon) VfxParticleUtil.ConfigureHarpoonProjectile(go);
            }
            else if (cannonballTrail)
            {
                VfxParticleUtil.ClearMotionTrails(go);
                VfxParticleUtil.SetTrailEmitting(go, true);
            }

            if (isHarpoon) VfxParticleUtil.SetTrailEmitting(go, false);
            go.transform.localScale = Vector3.one * scale;
            if (cannonballTrail) VfxParticleUtil.BoostCannonballTrails(go);
            if (isHarpoon)
            {
                VfxParticleUtil.ClearMotionTrails(go);
                VfxParticleUtil.SetTrailEmitting(go, true);
            }
            if (tint) Tint(go.transform, color); // a placeholder keeps its own colours (shared materials)

            Profiles?.Attach(Profiles.Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Trail), go.transform);
            var handle = _next++;
            _live[handle] = go;
            _liveWeapon[handle] = weaponId;
            return handle;
        }

        public void Move(int handle, Vector3 position, Vector3 velocity)
        {
            if (!_live.TryGetValue(handle, out var go) || go == null) return;
            go.transform.position = position;
            if (velocity.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        public void Release(int handle)
        {
            if (!_live.TryGetValue(handle, out var go)) return;
            _live.Remove(handle);
            _liveWeapon.TryGetValue(handle, out var weaponId);
            _liveWeapon.Remove(handle);
            if (go == null) return;
            _endedAt[_nextEnded] = go.transform.position;
            _endedWeapon[_nextEnded] = weaponId;
            _endedFrame[_nextEnded] = Time.frameCount;
            _nextEnded = (_nextEnded + 1) % EndedSlots;
            Profiles?.Detach(go.transform);
            VfxParticleUtil.SetTrailEmitting(go, false);
            VfxParticleUtil.ClearMotionTrails(go);
            _pools.Release(go);
        }

        ProjectileVisual VisualOf(string weaponId) => ConfigOf(weaponId)?.projectileVisual;

        /// <summary>Hackathon (13 Oct): a weapon's projectile prefab in the weapon prefab catalog (null: none).</summary>
        public static GameObject CatalogProjectile(WeaponVisualCatalog visuals, string weaponId) =>
            visuals != null ? visuals.Get(weaponId, WeaponVisualSlot.Projectile) : null;

        /// <summary>Hackathon art pass: the art team's projectile model for a weapon (null: none).</summary>
        public static ArtWeaponModel ArtProjectile(DemoJuiceSettings settings, string weaponId)
        {
            var art = settings != null ? settings.WeaponArt(weaponId) : null;
            return art != null && art.projectileModel != null ? art : null;
        }

        WeaponConfig ConfigOf(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || _config == null) return null;
            if (!_weapons.TryGetValue(weaponId, out var config))
            {
                foreach (var w in _config.All<WeaponConfig>())
                    if (w.weaponId == weaponId) { config = w; break; }
                _weapons[weaponId] = config;
            }
            return config;
        }

        static void Tint(Transform view, Color color)
        {
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.material;
                if (material.HasProperty("_Color")) material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.65f);
                }
            }
        }

        static void StripPhysics(GameObject root)
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(body);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
        }
    }
}

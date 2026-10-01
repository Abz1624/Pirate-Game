using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Plays the artists' VFX profiles (<see cref="VfxProfileLibrary"/>, demo 30 Sep 2026) for the presenters: one-shot
    /// effects through the V4 pool, trails attached to projectiles, enemies and the ship, and the enemy ids behind the
    /// event stream's unit ids (Spawned carries the id, Killed only the unit). The library is read at the moment each
    /// effect plays, so edits made in Play mode (the VFX editor) show at once.
    /// </summary>
    public sealed class VfxProfilePlayer : MonoBehaviour
    {
        PooledEffects _fx;
        Pooling.PoolService _pools;
        System.Func<Transform> _ship;
        readonly Dictionary<int, string> _enemyOfUnit = new Dictionary<int, string>();
        readonly Dictionary<Transform, string> _enemyOfView = new Dictionary<Transform, string>();
        readonly Dictionary<Transform, (GameObject go, GameObject prefab)> _trails = new Dictionary<Transform, (GameObject, GameObject)>();
        readonly List<Transform> _scratch = new List<Transform>();
        Transform _shipTrailOn;
        bool _shipSpawned;

        public VfxProfileLibrary Library { get; set; }
        public int Played { get; private set; }

        public void Bind(Pooling.PoolService pools, VfxProfileLibrary library, System.Func<Transform> ship)
        {
            Clear();
            _pools = pools;
            _fx = new PooledEffects(pools, "vfxprofile-fx/");
            Library = library;
            _ship = ship;
            _shipSpawned = false;
            PrewarmLibrary();
        }

        /// <summary>Instances of each profile effect made at bind (V5 M3e: they were made on first use, mid-fight).</summary>
        public const int PrewarmPerEffect = 2;

        void PrewarmLibrary()
        {
            if (Library == null || Library.profiles == null || _fx == null || _pools == null) return;
            foreach (var profile in Library.profiles)
            {
                if (profile == null) continue;
                foreach (var kind in new[] { VfxSlotKind.Muzzle, VfxSlotKind.Hit, VfxSlotKind.Death, VfxSlotKind.Spawn })
                {
                    var slot = profile.Slot(kind);
                    if (slot == null || !slot.HasPrefab) continue;
                    var prefab = NeedsFallback(slot.prefab, out var fallback, out _) ? fallback : slot.prefab;
                    if (prefab != null) _fx.Register(prefab, PrewarmPerEffect);
                }
                foreach (var kind in new[] { VfxSlotKind.Trail, VfxSlotKind.Projectile })
                {
                    var slot = profile.Slot(kind);
                    if (slot == null || !slot.HasPrefab) continue;
                    var key = "vfxprofile/" + slot.prefab.name;
                    if (!_pools.Has(key)) _pools.Register(new Pooling.PoolDefinition(key, slot.prefab, PrewarmPerEffect, 0, RoguePirates.V4.Config.ConfigEnums.PoolOverflowPolicy.Grow, "vfx profile trails"));
                }
            }
            _pools.PrewarmAll();
        }

        public VfxSlot Slot(VfxSubject subject, string id, VfxSlotKind kind)
        {
            if (Library == null) return null;
            var slot = Library.Slot(subject, id, kind);
            // An elite variant without its own profile uses its base enemy's ("elite_x" → "x").
            if (slot == null && subject == VfxSubject.Enemy && id != null && id.StartsWith(ElitePrefix, System.StringComparison.Ordinal))
                slot = Library.Slot(subject, id.Substring(ElitePrefix.Length), kind);
            return slot;
        }

        const string ElitePrefix = "elite_";

        /// <summary>Plays the slot's prefab at the pose (offset in the pose's frame); false when the slot has none.</summary>
        public bool Play(VfxSlot slot, Vector3 position, Quaternion rotation, float baseScale = 1f, Transform parent = null)
        {
            if (slot == null || !slot.HasPrefab || _fx == null) return false;
            var prefab = slot.prefab;
            var scale = baseScale * slot.ScaleOrOne;
            if (NeedsFallback(prefab, out var fallback, out var fallbackScale)) { prefab = fallback; scale *= fallbackScale; }
            if (prefab == null) return false;
            _fx.Register(prefab, 2);
            var go = _fx.Play(prefab, position + rotation * slot.offset, rotation, scale, slot.lifetime, parent);
            if (go == null) return false;
            Restart(go);
            VfxTint.Apply(go, slot.useTint, slot.tint);
            Played++;
            return true;
        }

        /// <summary>Attaches the slot's prefab to a target until <see cref="Detach"/> (projectile, enemy or ship trails).</summary>
        public void Attach(VfxSlot slot, Transform target)
        {
            if (target == null) return;
            Detach(target);
            if (slot == null || !slot.HasPrefab || _pools == null) return;
            var key = "vfxprofile/" + slot.prefab.name;
            if (!_pools.Has(key)) _pools.Register(new Pooling.PoolDefinition(key, slot.prefab, 0, 0, RoguePirates.V4.Config.ConfigEnums.PoolOverflowPolicy.Grow, "vfx profile trails"));
            var go = _pools.Acquire(key, target.position, target.rotation);
            if (go == null) return;
            go.transform.SetParent(target, false);
            go.transform.localPosition = slot.offset;
            go.transform.localRotation = Quaternion.identity;
            var parentScale = target.lossyScale.x > 0.0001f ? target.lossyScale.x : 1f;
            go.transform.localScale = Vector3.one * (slot.ScaleOrOne / parentScale);
            foreach (var t in go.GetComponentsInChildren<TrailRenderer>(true)) t.Clear();
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) { p.Clear(true); p.Play(true); }
            Restart(go);
            VfxTint.Apply(go, slot.useTint, slot.tint);
            _trails[target] = (go, slot.prefab);
        }

        public void Detach(Transform target)
        {
            if (ReferenceEquals(target, null) || !_trails.TryGetValue(target, out var trail)) return;
            _trails.Remove(target);
            if (trail.go == null) return;
            trail.go.transform.SetParent(null, true);
            if (_pools == null || !_pools.Release(trail.go))
            {
                if (Application.isPlaying) Destroy(trail.go); else DestroyImmediate(trail.go); // play-mode exit tears down in edit mode
            }
        }

        readonly Dictionary<GameObject, bool> _usesVfxGraph = new Dictionary<GameObject, bool>();

        /// <summary>
        /// A VFX Graph effect (the whirlpool) on a device without compute shaders (WebGL) draws nothing: the demo settings'
        /// particle stand-in plays instead.
        /// </summary>
        bool NeedsFallback(GameObject prefab, out GameObject fallback, out float scale)
        {
            fallback = null;
            scale = 1f;
            if (SystemInfo.supportsComputeShaders || prefab == null) return false;
            if (!_usesVfxGraph.TryGetValue(prefab, out var graph))
                _usesVfxGraph[prefab] = graph = prefab.GetComponentInChildren<UnityEngine.VFX.VisualEffect>(true) != null;
            if (!graph) return false;
            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            fallback = settings != null ? settings.vfxGraphFallbackPrefab : null;
            scale = settings != null ? settings.vfxGraphFallbackScale : 1f;
            return true;
        }

        /// <summary>Effects that do not restart with their particle systems: VFX Graph effects (the whirlpool) and the
        /// art team's tornado, which plays through its own call.</summary>
        static void Restart(GameObject go)
        {
            foreach (var vfx in go.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true)) { vfx.Reinit(); vfx.Play(); }
            go.BroadcastMessage("PlayTornado", SendMessageOptions.DontRequireReceiver);
        }

        // ---- Enemies ------------------------------------------------------------------------------------------------

        public void Spawned(int unitId, string enemyId, Vector3 position)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            _enemyOfUnit[unitId] = enemyId; // the spawn effect plays when the model appears (EnemyViewCreated), where it is
        }

        /// <summary>An enemy died: its own death effect, else the killing weapon's. True when the default kill effect should not play.</summary>
        public bool Killed(int unitId, string weaponId, Vector3 position)
        {
            _enemyOfUnit.TryGetValue(unitId, out var enemyId);
            _enemyOfUnit.Remove(unitId);
            var slot = Slot(VfxSubject.Enemy, enemyId, VfxSlotKind.Death);
            if (slot == null || !slot.HasPrefab) slot = Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Death);
            Play(slot, position, Quaternion.identity);
            return slot != null && slot.SuppressesDefault;
        }

        /// <summary>The default kill effect's scale for this enemy/weapon (the slot's scale when it has no prefab).</summary>
        public float KillScale(string weaponId)
        {
            var slot = Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Death);
            return slot != null && !slot.HasPrefab ? slot.ScaleOrOne : 1f;
        }

        public void EnemyViewCreated(string enemyId, Transform view)
        {
            if (view == null || string.IsNullOrEmpty(enemyId)) return;
            _enemyOfView[view] = enemyId;
            Play(Slot(VfxSubject.Enemy, enemyId, VfxSlotKind.Spawn), new Vector3(view.position.x, 0f, view.position.z), Quaternion.identity);
            Attach(Slot(VfxSubject.Enemy, enemyId, VfxSlotKind.Trail), view);
        }

        public void EnemyViewReleased(Transform view)
        {
            if (view == null) return;
            _enemyOfView.Remove(view);
            Detach(view);
        }

        public void EnemyHit(Transform view)
        {
            if (view == null || !_enemyOfView.TryGetValue(view, out var id)) return;
            Play(Slot(VfxSubject.Enemy, id, VfxSlotKind.Hit), view.position, view.rotation);
        }

        // ---- Ship ---------------------------------------------------------------------------------------------------

        public void ShipDamaged()
        {
            var ship = _ship?.Invoke();
            if (ship != null) Play(Slot(VfxSubject.PlayerShip, VfxProfileLibrary.PlayerShipId, VfxSlotKind.Hit), ship.position, ship.rotation);
        }

        public void ShipDied()
        {
            var ship = _ship?.Invoke();
            if (ship != null) Play(Slot(VfxSubject.PlayerShip, VfxProfileLibrary.PlayerShipId, VfxSlotKind.Death), ship.position, ship.rotation);
        }

        /// <summary>The VFX editor's preview: plays a slot at the ship (5 m ahead for weapons and enemies).</summary>
        public void Preview(VfxSubject subject, string id, VfxSlotKind kind)
        {
            var ship = _ship?.Invoke();
            if (ship == null) return;
            var slot = Slot(subject, id, kind);
            var pose = subject == VfxSubject.PlayerShip ? ship.position : ship.position + ship.forward * 5f + Vector3.up * 0.5f;
            if (kind == VfxSlotKind.Trail || kind == VfxSlotKind.Projectile)
            {
                if (subject == VfxSubject.PlayerShip) { _shipTrailOn = null; return; } // re-attached next frame
                PlayProjectilePreview(slot, ship);
                return;
            }
            Play(slot, pose, ship.rotation);
        }

        /// <summary>A projectile or trail preview: the prefab flies 12 m ahead of the ship over one second.</summary>
        void PlayProjectilePreview(VfxSlot slot, Transform ship)
        {
            if (slot == null || !slot.HasPrefab) return;
            var go = Instantiate(slot.prefab, ship.position + Vector3.up, ship.rotation);
            go.transform.localScale *= slot.ScaleOrOne;
            Restart(go);
            VfxTint.Apply(go, slot.useTint, slot.tint);
            go.AddComponent<PreviewFlight>().Velocity = ship.forward * 12f;
            Destroy(go, 1.2f);
        }

        sealed class PreviewFlight : MonoBehaviour
        {
            public Vector3 Velocity;
            void Update() => transform.position += Velocity * Time.deltaTime;
        }

        void LateUpdate()
        {
            _fx?.Tick();
            var ship = _ship?.Invoke();
            if (ship != null && ship.gameObject.activeInHierarchy)
            {
                if (!_shipSpawned)
                {
                    _shipSpawned = true;
                    Play(Slot(VfxSubject.PlayerShip, VfxProfileLibrary.PlayerShipId, VfxSlotKind.Spawn), ship.position, ship.rotation);
                }
                // The ship's trail follows the profile live (an edit in Play mode re-attaches it).
                var slot = Slot(VfxSubject.PlayerShip, VfxProfileLibrary.PlayerShipId, VfxSlotKind.Trail);
                var wanted = slot != null && slot.HasPrefab ? slot.prefab : null;
                var has = _trails.TryGetValue(ship, out var current) ? current.prefab : null;
                if (_shipTrailOn != ship || wanted != has)
                {
                    _shipTrailOn = ship;
                    Attach(slot, ship);
                }
            }
            // Targets that went away (pooled projectiles released elsewhere) give their trails back.
            _scratch.Clear();
            foreach (var pair in _trails) if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy) _scratch.Add(pair.Key);
            // (Detach removes destroyed targets too: it checks the reference, not Unity's null.)
            foreach (var t in _scratch) Detach(t);
        }

        public void Clear()
        {
            _scratch.Clear();
            _scratch.AddRange(_trails.Keys);
            foreach (var t in _scratch) Detach(t);
            _trails.Clear();
            _fx?.ReleaseAll();
            _enemyOfUnit.Clear();
            _enemyOfView.Clear();
            _shipTrailOn = null;
        }

        void OnDestroy() => Clear();
    }

    /// <summary>A profile's tint on a pooled effect: particle start colours and material colours, restored when untinted.</summary>
    public sealed class VfxTint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        ParticleSystem[] _particles;
        ParticleSystem.MinMaxGradient[] _base;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;

        public static void Apply(GameObject go, bool useTint, Color tint)
        {
            if (go == null) return;
            var t = go.GetComponent<VfxTint>();
            if (t == null)
            {
                if (!useTint) return;
                t = go.AddComponent<VfxTint>();
            }
            t.Set(useTint, tint);
        }

        void Set(bool on, Color tint)
        {
            if (_particles == null)
            {
                _particles = GetComponentsInChildren<ParticleSystem>(true);
                _base = new ParticleSystem.MinMaxGradient[_particles.Length];
                for (var i = 0; i < _particles.Length; i++) _base[i] = _particles[i].main.startColor;
                _renderers = GetComponentsInChildren<Renderer>(true);
                _block = new MaterialPropertyBlock();
            }
            for (var i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;
                var main = _particles[i].main;
                var g = _base[i];
                if (on)
                {
                    if (g.mode == ParticleSystemGradientMode.Color) g = new ParticleSystem.MinMaxGradient(g.color * tint);
                    else if (g.mode == ParticleSystemGradientMode.TwoColors) g = new ParticleSystem.MinMaxGradient(g.colorMin * tint, g.colorMax * tint);
                    else g = new ParticleSystem.MinMaxGradient(tint);
                }
                main.startColor = g;
            }
            foreach (var r in _renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!on) { r.SetPropertyBlock(null); continue; }
                var m = r.sharedMaterial;
                if (m == null) continue;
                r.GetPropertyBlock(_block);
                if (m.HasProperty(BaseColorId)) _block.SetColor(BaseColorId, m.GetColor(BaseColorId) * tint);
                if (m.HasProperty(ColorId)) _block.SetColor(ColorId, m.GetColor(ColorId) * tint);
                if (m.HasProperty(TintColorId)) _block.SetColor(TintColorId, m.GetColor(TintColorId) * tint);
                r.SetPropertyBlock(_block);
            }
        }
    }
}

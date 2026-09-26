using System;
using System.Collections.Generic;
using System.Linq;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// The per-run GameDatabase the legacy run uses from M2. Its enemy array has exactly the layout of the loaded
    /// database after V3's catalogs (null slots and appended specialist aliases included, so definition indices match),
    /// but every enemy is a per-run copy written from its archetype or variant config. Weapons, logbooks, relics and
    /// ships still reference the loaded database's assets (legacy HUD and meta code reads them directly; weapons move
    /// to V4 in M5). Config edits rewrite the copies in place; the enemy world indexes into the same array.
    /// </summary>
    [DeletedInPhase(9, "Enemies (M8) and the rest of the build data (M9) are V4 modules")]
    public sealed class V4RuntimeDatabase : IDisposable
    {
        readonly ConfigSetRuntime _config;
        readonly List<Object> _created = new List<Object>();
        readonly Dictionary<string, EnemyDefinition> _byId = new Dictionary<string, EnemyDefinition>();

        public GameDatabase Database { get; private set; }
        /// <summary>Enemy ids in the database that have no V4 config (kept as copies of the V3 definition).</summary>
        public IReadOnlyList<string> Unconfigured { get; private set; } = Array.Empty<string>();

        public V4RuntimeDatabase(ConfigSetRuntime config) { _config = config; }

        /// <summary>Builds the runtime database from the loaded one (call after the catalogs ran, like V3).</summary>
        public GameDatabase Build(GameDatabase loaded)
        {
            Database = Track(Object.Instantiate(loaded));
            Database.name = loaded.name;
            var unconfigured = new List<string>();
            Database.enemies = new EnemyDefinition[loaded.enemies.Length];
            for (var i = 0; i < loaded.enemies.Length; i++)
            {
                var source = loaded.enemies[i];
                if (source == null) continue;
                var copy = Track(Object.Instantiate(source));
                copy.name = source.name;
                Database.enemies[i] = copy;
                _byId[copy.id] = copy;
                if (!Write(copy.id)) unconfigured.Add(copy.id);
            }
            Unconfigured = unconfigured;
            if (_config != null) _config.ConfigChanged += OnConfigChanged;
            return Database;
        }

        /// <summary>Resolves an enemy id from config and writes it into its runtime definition. False when no config.</summary>
        bool Write(string id)
        {
            if (_config == null || !_byId.TryGetValue(id, out var def)) return false;
            var archetype = _config.All<EnemyArchetypeConfig>().FirstOrDefault(a => a.enemyId == id);
            var variant = _config.All<EnemyVariantConfig>().FirstOrDefault(v => v.variantId == id && v.kind == VariantKind.DatabaseAlias);
            if (archetype == null && variant == null) return false;
            Apply(archetype != null ? EnemyStatResolver.Resolve(archetype) : EnemyStatResolver.Resolve(variant), def);
            // R-13 stand-in models: the template ships carry colliders on the Island layer (register R-81), which V3's
            // spawn placement and island checks treat as obstacles. V3's specialist cubes have none, so the specialist
            // gets a copy of the model without colliders and plays exactly as before.
            if (variant != null && variant.inheritBaseVisual) def.visualPrefab = ColliderFreeCopy(def.visualPrefab);
            return true;
        }

        readonly Dictionary<GameObject, GameObject> _colliderFreeCopies = new Dictionary<GameObject, GameObject>();
        GameObject _copyHolder;

        GameObject ColliderFreeCopy(GameObject prefab)
        {
            if (prefab == null) return null;
            if (_colliderFreeCopies.TryGetValue(prefab, out var existing) && existing != null) return existing;
            if (_copyHolder == null)
            {
                // Inactive parent: the copy never wakes up or collides; the enemy view pool instantiates from it.
                _copyHolder = Track(new GameObject("V4 stand-in enemy models"));
                _copyHolder.hideFlags = HideFlags.HideAndDontSave;
                _copyHolder.SetActive(false);
            }
            var copy = Object.Instantiate(prefab, _copyHolder.transform);
            copy.name = prefab.name;
            copy.hideFlags = HideFlags.HideAndDontSave;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            return _colliderFreeCopies[prefab] = copy;
        }

        /// <summary>Resolved values onto a legacy EnemyDefinition (every EnemyDefinition field).</summary>
        public static void Apply(ResolvedEnemyStats r, EnemyDefinition d)
        {
            d.id = r.id;
            d.displayName = r.displayName;
            d.category = (EnemyCategory)(int)r.category;
            d.tier = (EnemyTier)(int)r.tier;
            d.visualPrefab = r.visualPrefab;
            d.visualScale = r.visualScale;
            d.visualYawOffset = r.visualYawOffset;
            d.biomeTag = r.biomeTag;
            d.baseHP = r.baseHP;
            d.baseDamage = r.baseDamage;
            d.moveSpeed = r.moveSpeed;
            d.radius = r.radius;
            d.visualHeightOffset = r.visualHeightOffset;
            d.threatCost = r.threatCost;
            d.xpValue = r.xpValue;
            d.goldValue = r.goldValue;
            d.behavior = (EnemyBehavior)(int)r.behavior;
            d.attackCooldown = r.attackCooldown;
            d.preferredRange = r.preferredRange;
            d.tags = r.tags != null ? (string[])r.tags.Clone() : null;
            d.placeholderColor = r.placeholderColor;
            d.isBoss = r.isBoss;
            d.isMiniboss = r.isMiniboss;
        }

        void OnConfigChanged(ConfigChange change)
        {
            switch (_config.Get<V4ConfigAsset>(change.configId))
            {
                case EnemyArchetypeConfig archetype:
                    Write(archetype.enemyId);
                    // Aliases copy fields from their base, so rewrite those too.
                    foreach (var v in _config.All<EnemyVariantConfig>().Where(v => v.baseArchetype == archetype && v.kind == VariantKind.DatabaseAlias))
                        Write(v.variantId);
                    break;
                case EnemyVariantConfig variant when variant.kind == VariantKind.DatabaseAlias:
                    Write(variant.variantId);
                    break;
            }
        }

        T Track<T>(T obj) where T : Object
        {
            obj.hideFlags = HideFlags.DontSave;
            _created.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            foreach (var o in _created) if (o != null) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
            _created.Clear();
            _colliderFreeCopies.Clear();
        }
    }

    /// <summary>
    /// Writes the archetype and variant combat blocks into V3's static combat-profile registry (M2), so the legacy
    /// enemy world uses config values. The registry is shared with V3 in the same Editor session and V3 code mutates
    /// it at runtime (SaltwindCombatBehaviorSystem sets the elite hammerhead flag), so originals are snapshotted first
    /// and restored when the V4 run ends. Registry values are copied into agents at spawn: edits reach new spawns.
    /// </summary>
    [DeletedInPhase(8, "V4 Enemy World reads archetype combat blocks directly")]
    public sealed class LegacyCombatProfileOverride : IDisposable
    {
        readonly ConfigSetRuntime _config;
        readonly Dictionary<string, (EnemyCombatProfile live, EnemyCombatProfile snapshot)> _touched = new Dictionary<string, (EnemyCombatProfile, EnemyCombatProfile)>();

        public LegacyCombatProfileOverride(ConfigSetRuntime config) { _config = config; }

        public void ApplyAll()
        {
            if (_config == null) return;
            foreach (var a in _config.All<EnemyArchetypeConfig>()) Apply(a.enemyId, a.combat);
            foreach (var v in _config.All<EnemyVariantConfig>()) Apply(v.variantId, v.combat);
            _config.ConfigChanged += OnConfigChanged;
        }

        void Apply(string registryId, ArchetypeCombat block)
        {
            if (block == null || !block.registered || !EnemyCombatProfileRegistry.TryGet(registryId, out var profile)) return;
            if (!_touched.ContainsKey(registryId))
            {
                var snapshot = new EnemyCombatProfile();
                LegacyConfigMapping.CopyFields(profile, snapshot);
                _touched[registryId] = (profile, snapshot);
            }
            LegacyConfigMapping.CopyFields(block, profile);
        }

        void OnConfigChanged(ConfigChange change)
        {
            switch (_config.Get<V4ConfigAsset>(change.configId))
            {
                case EnemyArchetypeConfig a: Apply(a.enemyId, a.combat); break;
                case EnemyVariantConfig v: Apply(v.variantId, v.combat); break;
            }
        }

        /// <summary>Puts every touched registry profile back as it was before the run.</summary>
        public void Dispose()
        {
            if (_config != null) _config.ConfigChanged -= OnConfigChanged;
            foreach (var (live, snapshot) in _touched.Values) LegacyConfigMapping.CopyFields(snapshot, live);
            _touched.Clear();
        }
    }
}

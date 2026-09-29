using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Ports
{
    /// <summary>
    /// The per-run GameDatabase the run uses (since M2). Its enemy array is the loaded database's layout (null slots
    /// included), then the specialist aliases in their configured order (M16: created here; V3's catalog appended them),
    /// then the V4-only enemies; every enemy is a per-run copy written from its archetype or variant config. Weapons, logbooks, relics and
    /// ships still reference the loaded database's assets (legacy HUD and meta code reads them directly; weapons move
    /// to V4 in M5). Config edits rewrite the copies in place; the enemy world indexes into the same array.
    /// </summary>
    public sealed class V4RuntimeDatabase : IDisposable
    {
        readonly ConfigSetRuntime _config;
        readonly List<Object> _created = new List<Object>();
        readonly Dictionary<string, EnemyDefinition> _byId = new Dictionary<string, EnemyDefinition>();

        public GameDatabase Database { get; private set; }
        /// <summary>Enemy ids in the database that have no V4 config (kept as copies of the V3 definition).</summary>
        public IReadOnlyList<string> Unconfigured { get; private set; } = Array.Empty<string>();

        public V4RuntimeDatabase(ConfigSetRuntime config) { _config = config; }

        /// <summary>Builds the runtime database from the loaded one (its slots, then the aliases, then the V4-only enemies).</summary>
        public GameDatabase Build(GameDatabase loaded)
        {
            Dispose(); // safe to build again: drops the previous copies, lookups and subscription
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
            AppendDatabaseAliases();
            AppendV4OnlyEnemies();
            Unconfigured = unconfigured;
            if (_config != null) _config.ConfigChanged += OnConfigChanged;
            return Database;
        }

        /// <summary>
        /// The database aliases (V3's specialists: an id with its own stats on a template enemy's model), appended after
        /// the loaded slots in their configured slot order — what V3's specialist catalog appended (M16: the catalog is
        /// gone). An alias is skipped when the loaded database already has its id or lacks its template, as the catalog did.
        /// </summary>
        void AppendDatabaseAliases()
        {
            if (_config == null) return;
            var aliases = _config.All<EnemyVariantConfig>()
                .Where(v => v.kind == VariantKind.DatabaseAlias && v.baseArchetype != null && !string.IsNullOrEmpty(v.variantId))
                .Where(v => !_byId.ContainsKey(v.variantId) && _byId.ContainsKey(v.baseArchetype.enemyId))
                .OrderBy(v => v.databaseSlotOrder).ThenBy(v => v.variantId, StringComparer.Ordinal).ToList();
            if (aliases.Count == 0) return;
            var list = new List<EnemyDefinition>(Database.enemies);
            foreach (var v in aliases)
            {
                var def = Track(ScriptableObject.CreateInstance<EnemyDefinition>());
                def.name = string.Empty; // as the catalog's definitions
                def.id = v.variantId;
                _byId[def.id] = def;
                Write(def.id);
                list.Add(def);
            }
            Database.enemies = list.ToArray();
        }

        /// <summary>Ids of enemies V3's database does not have (appended after its slots, so no index moves).</summary>
        readonly HashSet<string> _v4Only = new HashSet<string>();
        public IReadOnlyCollection<string> V4OnlyIds => _v4Only;

        /// <summary>
        /// Enemies only V4 spawns: archetypes V3's live database lacks (the Drowned enemies, R-05) and the registry
        /// elites (R-03: their own definition, so the elite combat profile applies to that unit only).
        /// </summary>
        void AppendV4OnlyEnemies()
        {
            if (_config == null) return;
            var ids = _config.All<EnemyArchetypeConfig>().Where(a => !a.inLegacyDatabase).Select(a => a.enemyId)
                .Concat(_config.All<EnemyVariantConfig>().Where(v => v.kind == VariantKind.RegistryElite && v.baseArchetype != null).Select(v => v.variantId))
                .Where(id => !string.IsNullOrEmpty(id) && !_byId.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (ids.Count == 0) return;
            var list = new List<EnemyDefinition>(Database.enemies);
            foreach (var id in ids)
            {
                var def = Track(ScriptableObject.CreateInstance<EnemyDefinition>());
                def.name = id;
                def.id = id;
                _byId[id] = def;
                _v4Only.Add(id);
                Write(id);
                list.Add(def);
            }
            Database.enemies = list.ToArray();
        }

        /// <summary>Resolves an enemy id from config and writes it into its runtime definition. False when no config.</summary>
        bool Write(string id)
        {
            if (_config == null || !_byId.TryGetValue(id, out var def)) return false;
            var archetype = _config.All<EnemyArchetypeConfig>().FirstOrDefault(a => a.enemyId == id);
            var variant = _config.All<EnemyVariantConfig>().FirstOrDefault(v => v.variantId == id && v.baseArchetype != null);
            if (archetype == null && variant == null) return false;
            Apply(archetype != null ? EnemyStatResolver.Resolve(archetype) : EnemyStatResolver.Resolve(variant), def);
            // Stand-in models (R-13 specialists, V4-only enemies): the template ships carry colliders on the Island layer
            // (register R-81), which V3's spawn placement and island checks treat as obstacles. V3's placeholder cubes
            // have none, so these enemies get a copy of the model without colliders.
            if ((variant != null && (variant.inheritBaseVisual || variant.kind == VariantKind.RegistryElite)) || _v4Only.Contains(id))
                def.visualPrefab = ColliderFreeCopy(def.visualPrefab);
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
                    // Variants copy fields from their base, so rewrite those too.
                    foreach (var v in _config.All<EnemyVariantConfig>().Where(v => v.baseArchetype == archetype))
                        Write(v.variantId);
                    break;
                case EnemyVariantConfig variant:
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
            _byId.Clear();
            _v4Only.Clear();
        }
    }

}

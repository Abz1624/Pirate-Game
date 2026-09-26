using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Pooling
{
    /// <summary>
    /// Builds the run's pool definitions from <see cref="PoolCatalogConfig"/>: its explicit entries, then one entry per
    /// enemy model the wave timeline can spawn (spawner anchors, pool entries, focus enemies), keyed "enemy/&lt;model&gt;".
    /// Enemies sharing a model share its pool; the prewarm is the largest any of them asks for.
    /// </summary>
    public static class PoolCatalog
    {
        public const string EnemyKeyPrefix = "enemy/";

        public static List<PoolDefinition> Build(ConfigSetRuntime config, bool moduleLab)
        {
            var result = new List<PoolDefinition>();
            var catalog = config?.All<PoolCatalogConfig>().FirstOrDefault();
            if (catalog == null) return result;
            foreach (var e in catalog.entries)
            {
                if (e == null || e.prefab == null || string.IsNullOrEmpty(e.key)) continue;
                if (e.moduleLabOnly && !moduleLab) continue;
                result.Add(new PoolDefinition(e.key, e.prefab, e.prewarm, e.cap, e.overflow, e.moduleLabOnly ? "catalog (Module Lab)" : "catalog"));
            }
            if (catalog.deriveEnemyViews)
            {
                // Keys come from model names: two different prefabs with the same name (or a catalog entry already
                // using the key) get a numbered key instead of silently sharing one pool.
                var used = new HashSet<string>(result.Select(d => d.Key));
                foreach (var d in DeriveEnemyViews(config, catalog))
                {
                    var key = d.Key;
                    for (var n = 2; !used.Add(key); n++) key = d.Key + "#" + n;
                    result.Add(key == d.Key ? d : new PoolDefinition(key, d.Prefab, d.Prewarm, d.Cap, d.Overflow, d.Source));
                }
            }
            return result;
        }

        /// <summary>Enemies the timeline can spawn, by id, with the model each renders with.</summary>
        public static List<(string id, GameObject model, int prewarm)> ReachableEnemies(ConfigSetRuntime config, PoolCatalogConfig catalog)
        {
            var found = new List<(string, GameObject, int)>();
            var seen = new HashSet<string>();
            var timeline = config.All<WaveTimelineConfig>().FirstOrDefault();
            if (timeline == null) return found;

            void AddArchetype(EnemyArchetypeConfig a)
            {
                if (a == null || !seen.Add(a.enemyId)) return;
                var r = EnemyStatResolver.Resolve(a);
                found.Add((a.enemyId, r.visualPrefab, Prewarm(a, catalog)));
            }
            void AddVariant(EnemyVariantConfig v)
            {
                if (v == null || v.baseArchetype == null || !seen.Add(v.variantId)) return;
                var r = EnemyStatResolver.Resolve(v);
                found.Add((v.variantId, r.visualPrefab, Prewarm(v.baseArchetype, catalog)));
            }

            foreach (var phase in timeline.phases)
            {
                if (phase == null) continue;
                AddArchetype(phase.spawner != null ? phase.spawner.anchor : null);
                AddArchetype(phase.alternateSpawner != null ? phase.alternateSpawner.anchor : null);
                foreach (var pool in phase.pools)
                foreach (var entry in pool.entries)
                {
                    if (entry.variant != null) AddVariant(entry.variant);
                    else AddArchetype(entry.archetype);
                }
                if (phase.focus != null && phase.focus.enabled)
                {
                    AddVariant(phase.focus.variant);
                    AddArchetype(phase.focus.archetype);
                }
            }
            return found;
        }

        static int Prewarm(EnemyArchetypeConfig a, PoolCatalogConfig catalog) =>
            a.visual != null && a.visual.poolPrewarm > 0 ? a.visual.poolPrewarm
            : a.isBoss || a.isMiniboss ? catalog.bossViewPrewarm : catalog.enemyViewPrewarm;

        static IEnumerable<PoolDefinition> DeriveEnemyViews(ConfigSetRuntime config, PoolCatalogConfig catalog)
        {
            var byModel = new Dictionary<GameObject, (int prewarm, List<string> ids)>();
            var order = new List<GameObject>();
            foreach (var (id, model, prewarm) in ReachableEnemies(config, catalog))
            {
                if (model == null) continue; // V3's placeholder cube: no model to pool
                if (!byModel.TryGetValue(model, out var entry)) { entry = (0, new List<string>()); order.Add(model); }
                entry.prewarm = Mathf.Max(entry.prewarm, prewarm);
                entry.ids.Add(id);
                byModel[model] = entry;
            }
            foreach (var model in order)
            {
                var (prewarm, ids) = byModel[model];
                yield return new PoolDefinition(EnemyKeyPrefix + model.name, model, catalog.prewarmEnemyViews ? prewarm : 0,
                    catalog.enemyViewCap, catalog.enemyViewOverflow, "derived: " + string.Join(", ", ids));
            }
        }
    }
}

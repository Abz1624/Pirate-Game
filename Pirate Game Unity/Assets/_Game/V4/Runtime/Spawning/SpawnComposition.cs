using System.Collections.Generic;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Picks which enemy an ambient spawn becomes, from the phase's pools (port of SaltwindAmbientCompositionLogic):
    /// roll the secondary (specialist) pools against the first pool's share, try a pool from a random start and walk
    /// it, skipping enemies whose tier is not unlocked yet or whose concurrency tag is at its cap; fall back to the
    /// first pool, then its first entry. Same RNG calls in the same order as V3 when pool weights are equal.
    /// </summary>
    internal sealed class SpawnComposition
    {
        readonly SpawnServices _s;
        readonly Dictionary<string, EnemyInfo> _info = new Dictionary<string, EnemyInfo>();
        bool _infoValid;

        struct EnemyInfo
        {
            public string[] Tags;
            public ConfigEnums.EnemyTier Tier;
        }

        public SpawnComposition(SpawnServices services) { _s = services; }

        /// <summary>Config edits can change tags and tiers; rebuild the lookup on next use.</summary>
        public void Invalidate() => _infoValid = false;

        public string Pick(TimelinePhase phase, float elapsed, int playerLevel)
        {
            var pools = phase.pools;
            if (pools == null || pools.Count == 0) return null;
            var basePool = pools[0];

            var hasSecondary = false;
            for (var i = 1; i < pools.Count; i++) if (pools[i].entries.Count > 0) { hasSecondary = true; break; }
            if (hasSecondary)
            {
                var roll = _s.Random.DirectorFloat();
                if (roll > basePool.share)
                {
                    var secondary = PickSecondary(pools, roll, basePool.share);
                    if (secondary != null && TryPickFromPool(secondary, true, elapsed, playerLevel, out var special)) return special;
                }
            }

            if (TryPickFromPool(basePool, false, elapsed, playerLevel, out var ordinary)) return ordinary;

            if (basePool.entries.Count > 0)
            {
                var fallback = IdOf(basePool.entries[0]);
                if (WithinCaps(fallback, false) && TierUnlocked(fallback, elapsed, playerLevel)) return fallback;
            }
            return null;
        }

        /// <summary>One secondary pool: the only one, else by cumulative share above the first pool's share.</summary>
        static SpawnPool PickSecondary(List<SpawnPool> pools, float roll, float baseShare)
        {
            SpawnPool last = null;
            var threshold = baseShare;
            for (var i = 1; i < pools.Count; i++)
            {
                if (pools[i].entries.Count == 0) continue;
                last = pools[i];
                threshold += pools[i].share;
                if (roll <= threshold) return pools[i];
            }
            return last;
        }

        bool TryPickFromPool(SpawnPool pool, bool secondaryPool, float elapsed, int playerLevel, out string enemyId)
        {
            enemyId = null;
            var n = pool.entries.Count;
            if (n == 0) return false;
            var start = StartIndex(pool);
            for (var offset = 0; offset < n; offset++)
            {
                var candidate = IdOf(pool.entries[(start + offset) % n]);
                if (string.IsNullOrEmpty(candidate)) continue;
                if (!TierUnlocked(candidate, elapsed, playerLevel)) continue;
                if (!WithinCaps(candidate, secondaryPool)) continue;
                enemyId = candidate;
                return true;
            }
            return false;
        }

        /// <summary>Equal weights: a uniform start (V3's DirectorInt call). Otherwise a weighted start.</summary>
        int StartIndex(SpawnPool pool)
        {
            var entries = pool.entries;
            var equal = true;
            var total = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                total += entries[i].weight;
                if (i > 0 && !UnityEngine.Mathf.Approximately(entries[i].weight, entries[0].weight)) equal = false;
            }
            if (equal || total <= 0f) return _s.Random.DirectorInt(0, entries.Count);
            var r = _s.Random.DirectorFloat() * total;
            for (var i = 0; i < entries.Count; i++)
            {
                r -= entries[i].weight;
                if (r < 0f) return i;
            }
            return entries.Count - 1;
        }

        public static string IdOf(PoolEntry entry)
        {
            if (entry == null) return null;
            if (entry.variant != null) return entry.variant.variantId;
            if (entry.archetype != null) return entry.archetype.enemyId;
            return entry.enemyId;
        }

        /// <summary>Concurrency caps by tag (e.g. ranged_gunner). SpecialistPool caps only apply to secondary-pool picks.</summary>
        bool WithinCaps(string enemyId, bool secondaryPool)
        {
            var timeline = _s.Timeline;
            if (timeline == null || !Info(enemyId, out var info) || info.Tags == null) return true;
            foreach (var tag in info.Tags)
            {
                var cap = timeline.Cap(tag);
                if (cap == null) continue;
                if (cap.scope == TagCapScope.SpecialistPool && !secondaryPool) continue;
                if (CountTagged(tag) >= cap.max) return false;
            }
            return true;
        }

        int CountTagged(string tag)
        {
            EnsureInfo();
            var count = 0;
            foreach (var pair in _info)
            {
                if (pair.Value.Tags == null || System.Array.IndexOf(pair.Value.Tags, tag) < 0) continue;
                var index = _s.Sink.FindDefinition(pair.Key);
                if (index >= 0) count += _s.Sink.CountActive(index);
            }
            return count;
        }

        bool TierUnlocked(string enemyId, float elapsed, int playerLevel)
        {
            var d = _s.Difficulty;
            if (d == null || !Info(enemyId, out var info)) return true;
            foreach (var rule in d.tierUnlocks)
            {
                if (rule.tier != info.Tier) continue;
                var minutes = elapsed / 60f;
                var timeOk = minutes >= rule.minMinutes;
                var levelOk = playerLevel >= rule.minPlayerLevel;
                return rule.requireBoth ? timeOk && levelOk : timeOk || levelOk;
            }
            return true;
        }

        bool Info(string enemyId, out EnemyInfo info)
        {
            EnsureInfo();
            return _info.TryGetValue(enemyId ?? string.Empty, out info);
        }

        void EnsureInfo()
        {
            if (_infoValid) return;
            _info.Clear();
            var config = _s.Config;
            if (config != null)
            {
                foreach (var a in config.All<EnemyArchetypeConfig>())
                    _info[a.enemyId] = new EnemyInfo { Tags = a.concurrencyTags, Tier = a.spawnTier };
                foreach (var v in config.All<EnemyVariantConfig>())
                    _info[v.variantId] = new EnemyInfo { Tags = v.concurrencyTags, Tier = v.spawnTier }; // elites count toward tag caps too
            }
            _infoValid = true;
        }
    }
}

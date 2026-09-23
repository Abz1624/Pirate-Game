using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Enemies
{
    /// <summary>Proxy meshes for specialist ship enemies until dedicated art ships.</summary>
    public static class EnemyVisualFallbackResolver
    {
        const string PrefabRoot = "Assets/GameObject/";
        static readonly (string enemyId, string prefabPath, float scale)[] Proxies =
        {
            ("cutthroat_skiff", "SmuggleBoat.prefab", 0.92f),
            ("deckhand_dinghy", "SmuggleBoat.prefab", 0.82f),
            ("gunner_cog", "CannonboatNavy.prefab", 1f),
            ("bomb_boat", "FireBoat.prefab", 1f),
            ("rammer_raft", "RamShip.prefab", 1f),
            ("boarding_barge", "RamShip.prefab", 1.12f),
            ("drowned_pursuer", "SmuggleBoat.prefab", 1.05f),
            ("drowned_harrier", "CannonboatNavy.prefab", 0.95f),
        };

        static GameObject[] _prefabs;
        static float[] _scales;

        public static GameObject ResolvePrefab(EnemyDefinition def)
        {
            if (def == null || def.visualPrefab != null) return def?.visualPrefab;
            EnsureLoaded();
            for (var i = 0; i < Proxies.Length; i++)
            {
                if (Proxies[i].enemyId != def.id) continue;
                return _prefabs[i];
            }

            return null;
        }

        public static float ResolveVisualScale(EnemyDefinition def)
        {
            if (def == null) return 1f;
            if (def.visualPrefab != null) return def.visualScale;
            EnsureLoaded();
            for (var i = 0; i < Proxies.Length; i++)
            {
                if (Proxies[i].enemyId != def.id) continue;
                return Proxies[i].scale;
            }

            return def.visualScale > 0.01f ? def.visualScale : 1f;
        }

        static void EnsureLoaded()
        {
            if (_prefabs != null) return;
            _prefabs = new GameObject[Proxies.Length];
            _scales = new float[Proxies.Length];
            for (var i = 0; i < Proxies.Length; i++)
            {
                _scales[i] = Proxies[i].scale;
#if UNITY_EDITOR
                _prefabs[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + Proxies[i].prefabPath);
#else
                _prefabs[i] = null;
#endif
            }
        }
    }
}

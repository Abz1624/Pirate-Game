using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    /// <summary>
    /// An enemy's model and model scale. M16: V3's proxy table (models for eight ship enemies keyed by id, used only when a
    /// definition had no model) is gone — every archetype config carries its model and scale (the same proxies), which a
    /// test guards; a definition without a model gets the placeholder cube.
    /// </summary>
    public static class EnemyVisualFallbackResolver
    {
        public static GameObject ResolvePrefab(EnemyDefinition def) => def != null ? def.visualPrefab : null;

        public static float ResolveVisualScale(EnemyDefinition def)
        {
            if (def == null) return 1f;
            if (def.visualPrefab != null) return def.visualScale;
            return def.visualScale > 0.01f ? def.visualScale : 1f;
        }
    }
}

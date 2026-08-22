using UnityEngine;

namespace HighSeas.World
{
    public static class IslandVisualUtility
    {
        public static float MeasureInstanceXZExtent(GameObject instance)
        {
            if (instance == null) return 10f;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 10f;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        public static float GetPrefabXZExtent(GameObject prefab)
        {
            if (prefab == null) return 10f;
            var temp = Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                return MeasureInstanceXZExtent(temp);
            }
            finally
            {
                Object.Destroy(temp);
            }
        }

        public static float ComputeGroundUniformScale(GameObject groundPrefab, float targetDiameter, float libraryMultiplier)
        {
            var extent = GetPrefabXZExtent(groundPrefab);
            if (extent < 0.01f) return libraryMultiplier;
            return targetDiameter / extent * libraryMultiplier;
        }
    }
}

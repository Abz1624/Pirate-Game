using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    public static class IslandVisualBuilder
    {
        public static void BuildAtRuntime(
            Transform root,
            IslandVisualLibrary library,
            float islandDiameterMeters,
            int decorationSeed)
        {
            Build(root, library, islandDiameterMeters, decorationSeed, InstantiateRuntime);
        }

#if UNITY_EDITOR
        public static void BuildInEditor(
            Transform root,
            IslandVisualLibrary library,
            float islandDiameterMeters,
            int decorationSeed)
        {
            Build(root, library, islandDiameterMeters, decorationSeed, InstantiateEditor);
        }

        static GameObject InstantiateEditor(GameObject prefab, Transform parent) =>
            (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif

        static void Build(
            Transform root,
            IslandVisualLibrary library,
            float islandDiameterMeters,
            int decorationSeed,
            Func<GameObject, Transform, GameObject> instantiate)
        {
            if (library == null || !library.HasPrefabs || root == null)
                return;

            var cellRng = new RunRng(unchecked(decorationSeed));

            var ground = instantiate(library.groundTile, root);
            ground.name = "Ground";
            ground.transform.localPosition = new Vector3(0f, library.groundYOffset, 0f);
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = Vector3.one;

            var extent = IslandVisualUtility.MeasureInstanceXZExtent(ground);
            var uniform = extent > 0.01f
                ? islandDiameterMeters / extent * library.groundScale
                : library.groundScale;
            ground.transform.localScale = Vector3.one * uniform;

            var rockCount = cellRng.MapInt(1, 4);
            for (var i = 0; i < rockCount; i++)
                PlaceScatterProp(library.rocks, root, cellRng, islandDiameterMeters, instantiate, $"Rock_{i}");

            if (cellRng.MapFloat() < 0.42f)
                PlaceScatterProp(library.trees, root, cellRng, islandDiameterMeters, instantiate, "Tree");

            IslandColliderUtility.ConfigureIslandCollision(root.gameObject, islandDiameterMeters * 0.5f);
        }

        static void PlaceScatterProp(
            GameObject[] pool,
            Transform root,
            RunRng rng,
            float islandDiameter,
            Func<GameObject, Transform, GameObject> instantiate,
            string name)
        {
            if (pool == null || pool.Length == 0) return;

            var prefab = pool[rng.MapInt(0, pool.Length)];
            if (prefab == null) return;

            var prop = instantiate(prefab, root);
            prop.name = name;
            var spread = islandDiameter * 0.22f;
            prop.transform.localPosition = new Vector3(
                (rng.MapFloat() - 0.5f) * spread,
                0f,
                (rng.MapFloat() - 0.5f) * spread);
            prop.transform.localRotation = Quaternion.Euler(0f, rng.MapFloat() * 360f, 0f);
            var scale = 0.85f + rng.MapFloat() * 0.35f;
            prop.transform.localScale = Vector3.one * scale;
            IslandColliderUtility.DisableChildColliders(prop);
        }

        static GameObject InstantiateRuntime(GameObject prefab, Transform parent) =>
            UnityEngine.Object.Instantiate(prefab, parent);
    }
}

using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class StageBuilder : MonoBehaviour
    {
        [SerializeField] Transform islandRoot;
        [SerializeField] IslandVisualLibrary visuals;
        [SerializeField] Material islandMaterial;
        [SerializeField] bool useVisualPrefabs = true;

        [Header("Demo layout")]
        [SerializeField] bool usePredefinedLayout;
        [SerializeField] IslandLayoutDefinition predefinedLayout;

        [Header("Procedural islands")]
        [SerializeField] ProceduralIslandSettings proceduralIslands = ProceduralIslandSettings.Default;
        [SerializeField] float islandDensityMultiplier = 1f;

        bool _useV3PoiLayout;

        public float IslandDensityMultiplier => islandDensityMultiplier;

        public void SetIslandDensityMultiplier(float multiplier) =>
            islandDensityMultiplier = Mathf.Max(1f, multiplier);

        public void MultiplyIslandDensity(float factor) =>
            islandDensityMultiplier = Mathf.Max(1f, islandDensityMultiplier * factor);

        public void ConfigureV3Layout(IslandLayoutDefinition layout)
        {
            usePredefinedLayout = layout != null && layout.HasIslands;
            predefinedLayout = layout;
            useVisualPrefabs = false;
            _useV3PoiLayout = true;
        }

        public void ConfigureProceduralMode(bool visualPrefabs = true)
        {
            usePredefinedLayout = false;
            predefinedLayout = null;
            _useV3PoiLayout = false;
            useVisualPrefabs = visualPrefabs;
        }

        public ProceduralIslandSettings GetProceduralIslandSettings() => proceduralIslands.Clamped();

        public void SetProceduralIslandSettings(ProceduralIslandSettings settings) =>
            proceduralIslands = settings.Clamped();

        public void MultiplyIslandCounts(float factor)
        {
            if (factor <= 0f)
                return;

            var s = proceduralIslands.Clamped();
            s.countMin = Mathf.Max(1, Mathf.RoundToInt(s.countMin * factor));
            s.countMax = Mathf.Max(s.countMin, Mathf.RoundToInt(s.countMax * factor));
            proceduralIslands = s;
        }

        void OnValidate() => proceduralIslands = proceduralIslands.Clamped();

        public StageLayout Build(int seed, int stageIndex)
        {
            EnsureIslandRoot();
            PurgeGeneratedIslandContent();
            ClearIslands();

            var islandSettings = ScaleSettingsForDensity(proceduralIslands.Clamped());
            var gen = new StageGenerator();
            var layout = gen.Generate(seed, stageIndex, islandSettings);

            var demoLayout = usePredefinedLayout ? IslandLayoutDefinition.LoadAssignedOrDefault(predefinedLayout) : null;
            if (demoLayout != null && demoLayout.HasIslands)
            {
                var layoutCount = IslandLayoutSpawner.Spawn(demoLayout, islandRoot, seed, _useV3PoiLayout);
                var layoutBaseline = Mathf.Max(layoutCount, demoLayout.CountSpawnableEntries());
                var desiredTotal = Mathf.Max(
                    layoutCount,
                    Mathf.RoundToInt(layoutBaseline * islandDensityMultiplier));
                var extraTarget = Mathf.Max(0, desiredTotal - layoutCount);
                var extraSpawned = 0;
                if (extraTarget > 0)
                {
                    var supplementalVisuals = IslandVisualLibrary.Resolve(visuals);
                    extraSpawned = SpawnSupplementalIslands(seed, islandSettings, extraTarget, supplementalVisuals, layoutCount);
                    if (extraSpawned < extraTarget)
                        Debug.LogWarning(
                            $"StageBuilder: placed {extraSpawned}/{extraTarget} supplemental islands (density x{islandDensityMultiplier:F2}). Try + again or lower min separation on StageBuilder.");
                }

                PlayBounds.ConfigureFromExclusionRegistry();
                if (_useV3PoiLayout)
                    V3StagePoiPlacer.Place(layout, new RunRng(seed + 404), demoLayout);
                else if (demoLayout.overridePlayerSpawn)
                    layout.playerSpawn = demoLayout.playerSpawn;

                var total = layoutCount + extraSpawned;
                Debug.Log(
                    $"StageBuilder: spawned {total} islands (seed={seed}, layout {layoutCount}, target +{extraTarget} supplemental, placed +{extraSpawned}, density x{islandDensityMultiplier:F2}).");
                return layout;
            }

            var resolvedVisuals = useVisualPrefabs ? IslandVisualLibrary.Resolve(visuals) : null;
            var islandCount = 0;

            if (layout.islandCenters != null && layout.islandCenters.Count > 0)
            {
                for (var i = 0; i < layout.islandCenters.Count; i++)
                {
                    BuildIslandAt(layout.islandCenters[i], i, seed, islandSettings, resolvedVisuals);
                    islandCount++;
                }
            }
            else
            {
                for (var x = 0; x < GameConstants.GridCells; x++)
                {
                    for (var z = 0; z < GameConstants.GridCells; z++)
                    {
                        if (!layout.occupancy[x, z]) continue;
                        BuildIslandAt(StageGenerator.CellToWorld(x, z), islandCount, seed, islandSettings, resolvedVisuals);
                        islandCount++;
                    }
                }
            }

            var layoutTarget = layout.islandCenters?.Count ?? islandCount;
            Debug.Log(
                $"StageBuilder: built {islandCount} procedural islands (seed={seed}, layout had {layoutTarget}, target {islandSettings.countMin}-{islandSettings.countMax}, density x{islandDensityMultiplier:F2}, sep {islandSettings.minSeparationMeters:F0}m, dia {islandSettings.islandDiameterMeters:F0}m).");
            return layout;
        }

        ProceduralIslandSettings ScaleSettingsForDensity(ProceduralIslandSettings settings)
        {
            if (islandDensityMultiplier <= 1.001f)
                return settings;

            settings.countMin = Mathf.Max(1, Mathf.RoundToInt(settings.countMin * islandDensityMultiplier));
            settings.countMax = Mathf.Max(settings.countMin, Mathf.RoundToInt(settings.countMax * islandDensityMultiplier));
            return settings.Clamped();
        }

        int SpawnSupplementalIslands(
            int seed,
            ProceduralIslandSettings settings,
            int targetCount,
            IslandVisualLibrary resolvedVisuals,
            int indexOffset)
        {
            if (targetCount <= 0 || islandRoot == null)
                return 0;

            var rng = new RunRng(unchecked(seed + 90210));
            var radius = settings.islandDiameterMeters * 0.5f;
            var margin = radius + 2f;
            var mapReach = GameConstants.MapSizeMeters * 0.5f * Mathf.Max(1.35f, islandDensityMultiplier + 0.35f);
            var halfMap = mapReach - margin;
            var attemptsPerIsland = Mathf.Max(200, settings.maxPlacementAttempts / Mathf.Max(1, targetCount));

            var occupied = new List<Vector3>(islandRoot.childCount + targetCount);
            for (var i = 0; i < islandRoot.childCount; i++)
                occupied.Add(islandRoot.GetChild(i).position);

            var spawned = 0;
            for (var n = 0; n < targetCount; n++)
            {
                var spacing = Mathf.Max(
                    settings.islandDiameterMeters * 1.05f,
                    settings.minSeparationMeters * Mathf.Lerp(0.45f, 0.75f, n / (float)Mathf.Max(1, targetCount - 1)));
                var minDistSq = spacing * spacing;

                for (var attempt = 0; attempt < attemptsPerIsland; attempt++)
                {
                    var pos = new Vector3(
                        (rng.MapFloat() * 2f - 1f) * halfMap,
                        0f,
                        (rng.MapFloat() * 2f - 1f) * halfMap);

                    if (!IsFarEnough(pos, occupied, minDistSq))
                        continue;

                    var index = indexOffset + spawned;
                    BuildIslandAt(pos, index, seed, settings, resolvedVisuals);
                    occupied.Add(pos);
                    spawned++;
                    break;
                }
            }

            return spawned;
        }

        static bool IsFarEnough(Vector3 candidate, List<Vector3> placed, float minDistSq)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                var delta = candidate - placed[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistSq)
                    return false;
            }

            return true;
        }

        void EnsureIslandRoot()
        {
            if (islandRoot != null) return;

            var existing = GameObject.Find("IslandRoot");
            if (existing != null)
            {
                islandRoot = existing.transform;
                return;
            }

            var go = new GameObject("IslandRoot");
            islandRoot = go.transform;
        }

        public void PurgeGeneratedIslandContent()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null || t.gameObject.scene.name == null)
                    continue;

                if (t.name == "IslandRoot")
                {
                    for (var c = t.childCount - 1; c >= 0; c--)
                        DestroyImmediate(t.GetChild(c).gameObject);
                    continue;
                }

                if (t.parent != null && t.parent.name == "IslandRoot")
                    DestroyImmediate(t.gameObject);
            }
#endif
        }

        void ClearIslands()
        {
            IslandSpawnExclusionRegistry.Clear();
            PlayBounds.ResetToDefaults();
            EnsureIslandRoot();
            if (islandRoot == null) return;
            for (var i = islandRoot.childCount - 1; i >= 0; i--)
            {
                var child = islandRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
                    Destroy(child);
#else
                Destroy(child);
#endif
            }
        }

        void BuildIslandAt(
            Vector3 center,
            int index,
            int seed,
            ProceduralIslandSettings settings,
            IslandVisualLibrary resolvedVisuals)
        {
            var root = new GameObject($"Island_{index}");
            root.transform.SetParent(islandRoot, false);
            root.transform.position = center;

            var radius = settings.islandDiameterMeters * 0.5f;

            if (resolvedVisuals != null && resolvedVisuals.HasPrefabs)
                BuildPrefabIsland(root.transform, index, seed, settings, resolvedVisuals);
            else
                BuildFallbackDisc(root.transform, settings.islandDiameterMeters, radius);
        }

        void BuildPrefabIsland(
            Transform root,
            int index,
            int seed,
            ProceduralIslandSettings settings,
            IslandVisualLibrary library)
        {
            var decorationSeed = unchecked(seed + index * 73856093 + 19349663);
            IslandVisualBuilder.BuildAtRuntime(root, library, settings.islandDiameterMeters, decorationSeed);
        }

        void BuildFallbackDisc(Transform root, float diameter, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "FallbackMesh";
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.transform.localScale = new Vector3(diameter * 0.9f, 0.3f, diameter * 0.9f);
            if (islandMaterial != null)
                go.GetComponent<Renderer>().material = islandMaterial;

            IslandColliderUtility.ConfigureIslandCollision(root.gameObject, radius * 0.92f, 0.6f);
        }
    }
}

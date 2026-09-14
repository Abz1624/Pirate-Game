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

        bool _useV3PoiLayout;

        public void ConfigureV3Layout(IslandLayoutDefinition layout)
        {
            usePredefinedLayout = layout != null && layout.HasIslands;
            predefinedLayout = layout;
            useVisualPrefabs = false;
            _useV3PoiLayout = true;
        }

        void OnValidate() => proceduralIslands = proceduralIslands.Clamped();

        public StageLayout Build(int seed, int stageIndex)
        {
            EnsureIslandRoot();
            ClearIslands();

            var islandSettings = proceduralIslands.Clamped();
            var gen = new StageGenerator();
            var layout = gen.Generate(seed, stageIndex, islandSettings);

            var demoLayout = usePredefinedLayout ? IslandLayoutDefinition.LoadAssignedOrDefault(predefinedLayout) : null;
            if (demoLayout != null && demoLayout.HasIslands)
            {
                var count = IslandLayoutSpawner.Spawn(demoLayout, islandRoot, seed);
                PlayBounds.ConfigureFromExclusionRegistry();
                if (_useV3PoiLayout)
                    V3StagePoiPlacer.Place(layout, new RunRng(seed + 404), demoLayout);
                else if (demoLayout.overridePlayerSpawn)
                    layout.playerSpawn = demoLayout.playerSpawn;

                Debug.Log($"StageBuilder: spawned {count} demo islands from layout (seed={seed}).");
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
                $"StageBuilder: built {islandCount} procedural islands (seed={seed}, layout had {layoutTarget}, target {islandSettings.countMin}-{islandSettings.countMax}, sep {islandSettings.minSeparationMeters:F0}m, dia {islandSettings.islandDiameterMeters:F0}m).");
            return layout;
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

        void ClearIslands()
        {
            IslandSpawnExclusionRegistry.Clear();
            PlayBounds.ResetToDefaults();
            if (islandRoot == null) return;
            for (var i = islandRoot.childCount - 1; i >= 0; i--)
            {
                var child = islandRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                DestroyImmediate(child);
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

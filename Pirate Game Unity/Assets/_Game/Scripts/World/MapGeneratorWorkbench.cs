using HighSeas.Core;

using HighSeas.Data;

using HighSeas.V3;

using UnityEngine;



namespace HighSeas.World

{

    public enum MapGeneratorSeedMode

    {

        Hold,

        Advance,

        Random,

        GameplayResolver

    }



    /// <summary>Edit-mode island map preview. Map Generator scene — M / + in Scene view (editor).</summary>

    public sealed class MapGeneratorWorkbench : MonoBehaviour

    {

        [SerializeField] StageBuilder stageBuilder;

        [SerializeField] int seed = GameConstants.DemoRunSeed;

        [SerializeField] int lastBuildSeed;

        [SerializeField] float islandDensityMultiplier = 1f;

        [SerializeField] bool proceduralOnly;

        [SerializeField] bool useV3SaltwindLayout = true;

        [SerializeField] IslandLayoutDefinition v3IslandLayout;



        public int Seed => seed;

        public int LastBuildSeed => lastBuildSeed;

        public float IslandDensityMultiplier => islandDensityMultiplier;



        public void SetSeed(int value)

        {

            seed = value;

            lastBuildSeed = value;

#if UNITY_EDITOR

            UnityEditor.EditorUtility.SetDirty(this);

#endif

        }



        public void SyncSeedFromGameplay()

        {

            var bootstrap = FindFirstObjectByType<V3RunBootstrap>();

            if (bootstrap != null)

                SetSeed(bootstrap.EditorRunSeed);

            else

                SetSeed(V3HomeSeed.ResolveRunSeed());

        }



        public StageLayout Regenerate(MapGeneratorSeedMode seedMode = MapGeneratorSeedMode.Advance)

        {

            if (stageBuilder == null)

                stageBuilder = GetComponent<StageBuilder>();

            if (stageBuilder == null)

            {

                Debug.LogError("MapGeneratorWorkbench: assign a StageBuilder.");

                return null;

            }



            ApplyGenerationMode();

            stageBuilder.SetIslandDensityMultiplier(islandDensityMultiplier);



            var buildSeed = ResolveBuildSeed(seedMode);

            seed = buildSeed;

            lastBuildSeed = buildSeed;



            var result = stageBuilder.Build(buildSeed, 0);

            var settings = stageBuilder.GetProceduralIslandSettings();

            var islandRoot = GameObject.Find("IslandRoot");

            var totalIslands = islandRoot != null ? islandRoot.transform.childCount : 0;

            Debug.Log(

                $"MapGeneratorWorkbench: built seed={buildSeed}, mode={seedMode}, islands in scene={totalIslands}, density x{islandDensityMultiplier:F2}, procedural target {settings.countMin}-{settings.countMax}.");

#if UNITY_EDITOR

            UnityEditor.EditorUtility.SetDirty(this);

            UnityEditor.EditorUtility.SetDirty(stageBuilder);

#endif

            return result;

        }



        public void MultiplyIslandCounts(float factor)

        {

            if (stageBuilder == null)

                stageBuilder = GetComponent<StageBuilder>();

            if (stageBuilder == null)

                return;



            islandDensityMultiplier = Mathf.Max(1f, islandDensityMultiplier * factor);

            stageBuilder.SetIslandDensityMultiplier(islandDensityMultiplier);



            if (proceduralOnly)

                stageBuilder.MultiplyIslandCounts(factor);



            Debug.Log($"MapGeneratorWorkbench: island density multiplier is now x{islandDensityMultiplier:F2} (+{Mathf.RoundToInt((factor - 1f) * 100f)}% step).");

#if UNITY_EDITOR

            UnityEditor.EditorUtility.SetDirty(this);

            UnityEditor.EditorUtility.SetDirty(stageBuilder);

#endif

        }



        public void ResetProceduralDefaults()

        {

            if (stageBuilder == null)

                stageBuilder = GetComponent<StageBuilder>();

            if (stageBuilder == null)

                return;



            islandDensityMultiplier = 1f;

            stageBuilder.SetIslandDensityMultiplier(1f);

            ApplyGenerationMode();

            stageBuilder.SetProceduralIslandSettings(ProceduralIslandSettings.Default);

        }



        void ApplyGenerationMode()

        {

            if (useV3SaltwindLayout && !proceduralOnly)

            {

                var layout = IslandLayoutDefinition.LoadAssignedOrDefault(v3IslandLayout);

                if (layout != null && layout.HasIslands)

                {

                    stageBuilder.ConfigureV3Layout(layout);

                    return;

                }



                Debug.LogWarning("MapGeneratorWorkbench: V3 Saltwind layout asset missing — falling back to procedural islands.");

            }



            stageBuilder.ConfigureProceduralMode();

        }



        int ResolveBuildSeed(MapGeneratorSeedMode seedMode)

        {

            switch (seedMode)

            {

                case MapGeneratorSeedMode.Hold:

                    return lastBuildSeed != 0 ? lastBuildSeed : seed;

                case MapGeneratorSeedMode.Advance:

                    return unchecked((lastBuildSeed != 0 ? lastBuildSeed : seed) + 1);

                case MapGeneratorSeedMode.Random:

                    return Random.Range(1, int.MaxValue);

                case MapGeneratorSeedMode.GameplayResolver:

                    return V3HomeSeed.ResolveRunSeed();

                default:

                    return seed;

            }

        }



        void OnValidate()

        {

            if (lastBuildSeed == 0 && seed != 0)

                lastBuildSeed = seed;

        }

    }

}



using System;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "DemoIslandLayout", menuName = "HighSeas/Demo Island Layout")]
    public sealed class IslandLayoutDefinition : ScriptableObject
    {
        public bool overridePlayerSpawn;
        public Vector3 playerSpawn = new(10f, 0f, -70f);
        public IslandClusterDefinition[] clusters;

        public bool HasIslands => clusters != null && clusters.Length > 0;

        public static IslandLayoutDefinition LoadAssignedOrDefault(IslandLayoutDefinition assigned)
        {
            if (assigned != null && assigned.HasIslands)
                return assigned;
            return Resources.Load<IslandLayoutDefinition>("DemoIslandLayout");
        }
    }

    [Serializable]
    public sealed class IslandClusterDefinition
    {
        public int clusterId;
        public Vector3 anchorPosition;
        public float clusterJitterRadius = 6f;
        public IslandPrefabEntry[] islands;
    }

    [Serializable]
    public sealed class IslandPrefabEntry
    {
        public GameObject prefab;
        public Vector3 offsetFromAnchor;
        public float yRotation;
        public Vector3 scale = Vector3.one;
        public float positionJitter = 2.5f;
        public float rotationJitter = 12f;
        [Tooltip("When true, scale is computed from prefab bounds to match target footprint.")]
        public bool autoScaleToFootprint = true;
        [Tooltip("Target XZ footprint diameter in meters when auto-scaling.")]
        public float targetFootprintMeters = 20f;
    }
}

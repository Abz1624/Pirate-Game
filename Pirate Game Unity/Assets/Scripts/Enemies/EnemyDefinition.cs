using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Reusable enemy type: assign a model prefab and base health in the Inspector.
    /// Create via Assets → Create → Pirate Game → Enemy Definition.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Pirate Game/Enemy Definition", order = 0)]
    public class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Label used in the Inspector and debug logs.")]
        public string displayName = "Enemy";

        [Tooltip("Enemy model prefab. Root should have Health + EnemySetup (Pirate Game → Combat → Setup Selected As Enemy).")]
        public GameObject prefab;

        [Min(1f)]
        [Tooltip("Base hit points applied when this enemy is spawned.")]
        public float maxHealth = 30f;

        [Min(0f)]
        [Tooltip("Chase speed in units per second.")]
        public float moveSpeed = 2f;

        [Min(0f)]
        [Tooltip("Relative chance to be picked when the spawner chooses a random type.")]
        public float spawnWeight = 1f;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }
#endif
    }
}

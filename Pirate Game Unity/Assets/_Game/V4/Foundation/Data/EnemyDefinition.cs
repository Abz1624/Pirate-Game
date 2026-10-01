using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "RoguePirates/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public EnemyCategory category = EnemyCategory.Creature;
        public EnemyTier tier = EnemyTier.Soldier;
        public GameObject visualPrefab;
        [Tooltip("Uniform scale applied to the visual prefab root.")]
        public float visualScale = 1f;
        [Tooltip("Extra yaw applied so the model faces its movement direction.")]
        public float visualYawOffset;
        public string biomeTag = "emerald";
        public float baseHP = 20f;
        public float baseDamage = 8f;
        public float moveSpeed = 4f;
        public float radius = 1f;
        public float visualHeightOffset;
        public int threatCost = 1;
        public int xpValue = 2;
        public int goldValue = 1;
        public EnemyBehavior behavior;
        public float attackCooldown = 1.5f;
        public float preferredRange = 8f;
        public string[] tags;
        public Color placeholderColor = Color.red;
        [Tooltip("V5 M3d: the view's colours × this (white = as authored).")] public Color visualTint = Color.white;
        [Tooltip("V5 M3d: the drawn size and the gameplay radius × this.")] public float viewSize = 1f;
        public bool isBoss;
        public bool isMiniboss;
    }
}

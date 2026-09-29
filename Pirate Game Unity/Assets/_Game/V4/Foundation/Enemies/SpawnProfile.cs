using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    [CreateAssetMenu(fileName = "SpawnProfile", menuName = "RoguePirates/Spawn Profile")]
    public sealed class SpawnProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId;

        [Header("Enemy")]
        public EnemyDefinition enemy;

        [Header("Spawn")]
        public SpawnMode spawnMode = SpawnMode.Single;
        public SpawnGroupLayout groupLayout = SpawnGroupLayout.Scatter;
        public SpawnTimingSettings timing = new();
        public SpawnPlacementSettings placement = new();

        [Header("Elite")]
        public SpawnEliteSettings elite = new();

        [Header("Movement")]
        public SpawnMovementOverrides movement = new();

        [Header("Attack")]
        public SpawnAttackOverrides attack = new();

        [Header("Special")]
        public EnemySpecialBehavior specialBehavior = EnemySpecialBehavior.None;
        public FormationBehaviorSettings formation = new();
        public SummonBehaviorSettings summon = new();
        public TetherBeamSettings tether = new();
        public SupportAuraSettings supportAura = new();
        public PhaseDashBehaviorSettings phaseDash = new();
        public TerrainAmbushSettings terrainAmbush = new();
        public AdaptiveCounterSettings adaptive = new();

        public string ResolvedId => string.IsNullOrEmpty(profileId) ? name : profileId;
    }
}

using UnityEngine;

namespace PirateGame.Progression
{
    public enum UpgradeArcFamily
    {
        Passive,
        Broadside,
        Chaser,
        Deck
    }

    public enum UpgradeEffect
    {
        DamagePercent,
        FireRatePercent,
        CannonCount,
        Pierce,
        RangePercent,
        MaxHealthPercent,
        MoveSpeedPercent,
        TurnRatePercent,
        HealthRegen,
        PickupRadiusPercent
    }

    [System.Serializable]
    public struct UpgradeCardData
    {
        public string id;
        public string displayName;
        public UpgradeArcFamily family;
        public UpgradeEffect effect;
        [Tooltip("Stack value — e.g. 0.15 = +15% damage.")]
        public float magnitude;
        public Sprite icon;
    }
}

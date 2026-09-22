using HighSeas.Balance;
using HighSeas.Core;

namespace HighSeas.V3
{
    public readonly struct V3StagePoiPlacementOptions
    {
        public int SalvageChestCount { get; }
        public float FirstChestMaxTravelSeconds { get; }
        public float RepresentativeCruiseSpeed { get; }
        public float NavClearance { get; }
        public int ExplosiveBarrelCount { get; }

        public V3StagePoiPlacementOptions(
            int salvageChestCount,
            float firstChestMaxTravelSeconds,
            float representativeCruiseSpeed,
            float navClearance,
            int explosiveBarrelCount)
        {
            SalvageChestCount = salvageChestCount;
            FirstChestMaxTravelSeconds = firstChestMaxTravelSeconds;
            RepresentativeCruiseSpeed = representativeCruiseSpeed;
            NavClearance = navClearance;
            ExplosiveBarrelCount = explosiveBarrelCount;
        }

        public static V3StagePoiPlacementOptions LegacyV3() => new(
            GameConstants.V3SalvageChestPoolSize,
            999f,
            SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
            3.15f,
            0);

        public static V3StagePoiPlacementOptions SaltwindSolo() => new(
            12,
            15f,
            SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
            SaltwindPoiLayoutLogic.DefaultNavClearance,
            2);

        public static V3StagePoiPlacementOptions ResolveActive()
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Authoring != null)
            {
                var profile = StageBalanceProfileContext.Authoring;
                return new V3StagePoiPlacementOptions(
                    profile.salvageChestCount,
                    15f,
                    SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
                    SaltwindPoiLayoutLogic.DefaultNavClearance,
                    profile.explosiveBarrelCount);
            }

            return LegacyV3();
        }
    }
}

using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;

namespace RoguePirates.V4.Foundation.World
{
    public readonly struct StagePoiPlacementOptions
    {
        public int SalvageChestCount { get; }
        public float FirstChestMaxTravelSeconds { get; }
        public float RepresentativeCruiseSpeed { get; }
        public float NavClearance { get; }
        public int ExplosiveBarrelCount { get; }

        public StagePoiPlacementOptions(
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

        public static StagePoiPlacementOptions WithoutProfile() => new(
            GameConstants.SalvageChestPoolSize,
            999f,
            SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
            3.15f,
            0);

        public static StagePoiPlacementOptions SaltwindSolo() => new(
            12,
            15f,
            SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
            SaltwindPoiLayoutLogic.DefaultNavClearance,
            2);

        public static StagePoiPlacementOptions ResolveActive()
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Authoring != null)
            {
                var profile = StageBalanceProfileContext.Authoring;
                return new StagePoiPlacementOptions(
                    profile.salvageChestCount,
                    15f,
                    SaltwindPoiLayoutLogic.DefaultSaltwindCruiseSpeed,
                    SaltwindPoiLayoutLogic.DefaultNavClearance,
                    profile.explosiveBarrelCount);
            }

            return WithoutProfile();
        }
    }
}

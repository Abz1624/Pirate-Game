using RoguePirates.V4.Foundation.Core;

namespace RoguePirates.V4.Foundation.Platform
{
    public static class QualityProfile
    {
        public struct Caps
        {
            public int enemyCap;
            public int projectileCap;
            public int pickupCap;
            public float vfxDensity;
        }

        public static Caps Current
        {
            get
            {
                // V5 M7: the gameplay caps (enemies, projectiles, pickups) are Mid's on every device, so a tier never changes
                // combat; the tier sets only the effect density (and, on phones, the render scale).
                var caps = Get(QualityTier.Mid);
                caps.vfxDensity = Get(AppRoot.Instance?.Config.Quality ?? QualityTier.Mid).vfxDensity;
                if (GameSettings.Current.reducedEffects) caps.vfxDensity *= 0.5f; // Reduced effects
                return caps;
            }
        }

        public static Caps Get(QualityTier tier) => tier switch
        {
            QualityTier.Low => new Caps { enemyCap = 160, projectileCap = 280, pickupCap = 180, vfxDensity = 0.45f },
            QualityTier.High => new Caps { enemyCap = 420, projectileCap = 900, pickupCap = 180, vfxDensity = 1f },
            _ => new Caps { enemyCap = 260, projectileCap = 500, pickupCap = 180, vfxDensity = 0.75f }
        };
    }
}

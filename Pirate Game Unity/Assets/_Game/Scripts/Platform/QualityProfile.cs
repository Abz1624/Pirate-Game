using HighSeas.Core;

namespace HighSeas.Platform
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

        public static Caps Current => Get(AppRoot.Instance?.Config.Quality ?? QualityTier.Mid);

        public static Caps Get(QualityTier tier) => tier switch
        {
            QualityTier.Low => new Caps { enemyCap = 160, projectileCap = 280, pickupCap = 180, vfxDensity = 0.45f },
            QualityTier.High => new Caps { enemyCap = 420, projectileCap = 900, pickupCap = 180, vfxDensity = 1f },
            _ => new Caps { enemyCap = 260, projectileCap = 500, pickupCap = 180, vfxDensity = 0.75f }
        };
    }
}

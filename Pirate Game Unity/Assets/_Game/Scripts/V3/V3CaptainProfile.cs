using HighSeas.Core;

namespace HighSeas.V3
{
    public readonly struct V3CaptainProfile
    {
        public V3CaptainId Id { get; }
        public string DisplayName { get; }
        public string StartingWeaponId { get; }
        public float BaseHull { get; }
        public float BaseMoveSpeed { get; }

        V3CaptainProfile(
            V3CaptainId id,
            string displayName,
            string startingWeaponId,
            float baseHull,
            float baseMoveSpeed)
        {
            Id = id;
            DisplayName = displayName;
            StartingWeaponId = startingWeaponId;
            BaseHull = baseHull;
            BaseMoveSpeed = baseMoveSpeed;
        }

        public static V3CaptainProfile Get(V3CaptainId id) =>
            id switch
            {
                V3CaptainId.Brine => new V3CaptainProfile(
                    V3CaptainId.Brine, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Brine), "swivel_guns",
                    GameConstants.ScaleHull(110f), 5.7f),
                V3CaptainId.Flint => new V3CaptainProfile(
                    V3CaptainId.Flint, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Flint), "powder_kegs",
                    GameConstants.ScaleHull(90f), 6.3f),
                V3CaptainId.Richie => new V3CaptainProfile(
                    V3CaptainId.Richie, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Richie), "swivel_guns",
                    GameConstants.ScaleHull(95f), 6.1f),
                V3CaptainId.Ember => new V3CaptainProfile(
                    V3CaptainId.Ember, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Ember), "flamethrower",
                    GameConstants.ScaleHull(92f), 5.9f),
                V3CaptainId.Roxie => new V3CaptainProfile(
                    V3CaptainId.Roxie, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Roxie), "swivel_guns",
                    GameConstants.ScaleHull(88f), 6.5f),
                _ => new V3CaptainProfile(
                    V3CaptainId.Rook, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Rook), "swivel_guns",
                    GameConstants.DefaultHull, GameConstants.DefaultMoveSpeed)
            };
    }
}

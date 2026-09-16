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
                    V3CaptainId.Brine, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Brine), "deck_cannons", 110f, 5.7f),
                V3CaptainId.Flint => new V3CaptainProfile(
                    V3CaptainId.Flint, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Flint), "powder_kegs", 90f, 6.3f),
                V3CaptainId.Richie => new V3CaptainProfile(
                    V3CaptainId.Richie, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Richie), "deck_cannons", 95f, 6.1f),
                V3CaptainId.Ember => new V3CaptainProfile(
                    V3CaptainId.Ember, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Ember), "flamethrower", 92f, 5.9f),
                V3CaptainId.Roxie => new V3CaptainProfile(
                    V3CaptainId.Roxie, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Roxie), "deck_cannons", 88f, 6.5f),
                _ => new V3CaptainProfile(
                    V3CaptainId.Rook, V3CaptainCatalog.GetDisplayTitle(V3CaptainId.Rook), "deck_cannons",
                    GameConstants.DefaultHull, GameConstants.DefaultMoveSpeed)
            };
    }
}

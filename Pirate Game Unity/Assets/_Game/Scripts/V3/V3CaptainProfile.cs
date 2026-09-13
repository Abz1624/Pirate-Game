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
                    V3CaptainId.Brine, "Captain Brine", "deck_cannons", 110f, 5.7f),
                V3CaptainId.Flint => new V3CaptainProfile(
                    V3CaptainId.Flint, "Captain Flint", "powder_kegs", 90f, 6.3f),
                _ => new V3CaptainProfile(
                    V3CaptainId.Rook, "Captain Rook", "deck_cannons", GameConstants.DefaultHull, GameConstants.DefaultMoveSpeed)
            };
    }
}

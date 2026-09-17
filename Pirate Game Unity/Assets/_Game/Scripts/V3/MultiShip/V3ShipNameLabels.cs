using HighSeas.Core;
using HighSeas.Player;

namespace HighSeas.V3.MultiShip
{
    public static class V3ShipNameLabels
    {
        public static void Attach(PlayerShipController ship, string displayName)
        {
            if (ship == null || string.IsNullOrEmpty(displayName))
                return;

            var tag = ship.GetComponent<ShipPlayerNameTag>();
            if (tag == null)
                tag = ship.gameObject.AddComponent<ShipPlayerNameTag>();
            tag.SetDisplayName(displayName);
        }

        public static void TagShips(PlayerShipController human, params PlayerShipController[] others)
        {
            var rng = RunManager.Instance?.Rng;
            V3RandomPlayerNames.ResetSession();

            if (human != null)
                Attach(human, V3RandomPlayerNames.RollUnique(rng));

            if (others == null)
                return;

            for (var i = 0; i < others.Length; i++)
            {
                if (others[i] != null)
                    Attach(others[i], V3RandomPlayerNames.RollUnique(rng));
            }
        }
    }
}

using HighSeas.Platform;
using HighSeas.Player;

namespace HighSeas.V3.MultiShip
{
    public static class V3LocalShipInput
    {
        public static void Bind(PlayerShipController ship, ShipKeyboardBinding binding)
        {
            if (ship == null)
                return;
            ship.ConfigureDedicatedSteerInput(new BoundShipKeyboardInput(binding));
        }
    }
}

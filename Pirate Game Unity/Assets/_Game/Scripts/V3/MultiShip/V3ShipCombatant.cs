using HighSeas.Combat;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Registers a ship for PvP damage and bot coordination.</summary>
    [DisallowMultipleComponent]
    public sealed class V3ShipCombatant : MonoBehaviour
    {
        [SerializeField] V3ShipTeam team = V3ShipTeam.Human;
        [SerializeField] bool isLocalHuman;

        PlayerShipController _ship;
        DamageReceiver _damage;

        public V3ShipTeam Team => team;
        public bool IsLocalHuman => isLocalHuman;
        public PlayerShipController Ship => _ship;
        public DamageReceiver Damage => _damage;

        public void Configure(V3ShipTeam shipTeam, bool localHuman)
        {
            team = shipTeam;
            isLocalHuman = localHuman;
        }

        void Awake()
        {
            _ship = GetComponent<PlayerShipController>();
            _damage = GetComponent<DamageReceiver>();
        }

        void OnEnable() => V3ShipCombatRegistry.Register(this);
        void OnDisable() => V3ShipCombatRegistry.Unregister(this);

        public bool IsHostileTo(V3ShipCombatant other)
        {
            if (other == null || other == this) return false;
            if (!V3RunSession.IsPvP) return false;
            return team != other.team;
        }
    }
}

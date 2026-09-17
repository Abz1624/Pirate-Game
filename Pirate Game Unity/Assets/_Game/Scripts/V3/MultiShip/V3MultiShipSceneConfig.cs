using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Place on the Systems object in PvP / Co-op prototype scenes.</summary>
    public sealed class V3MultiShipSceneConfig : MonoBehaviour
    {
        [SerializeField] V3RunSessionKind sessionKind = V3RunSessionKind.SinglePlayer;
        [SerializeField] float shipSpacing = 22f;

        public V3RunSessionKind SessionKind => sessionKind;
        public float ShipSpacing => shipSpacing;

        void Awake() => V3RunSession.Configure(sessionKind);
    }
}

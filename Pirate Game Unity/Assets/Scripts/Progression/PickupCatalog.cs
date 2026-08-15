using UnityEngine;

namespace PirateGame.Progression
{
    [CreateAssetMenu(fileName = "PickupCatalog", menuName = "Pirate Game/Pickup Catalog", order = 2)]
    public class PickupCatalog : ScriptableObject
    {
        public GameObject[] prefabs = System.Array.Empty<GameObject>();
    }
}

using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "ShipDefinition", menuName = "HighSeas/Ship Definition")]
    public sealed class ShipDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public GameObject visualPrefab;
        public float baseHull = 100f;
        public float baseMoveSpeed = 6.2f;
        public float baseArmor;
        public float baseEvasion;
        public string startingWeaponId = "broadside_battery";
        public string passiveId;
        public string unlockQuestId;
        public int unlockDoubloonCost;
    }
}

using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [CreateAssetMenu(fileName = "RelicDefinition", menuName = "RoguePirates/Relic Definition")]
    public sealed class RelicDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Rarity rarity;
        public string[] tags;
        public bool unique;
        public int maxStacks = 99;
        public StatModifier[] passiveModifiers;
        public TriggerEvent triggerEvent;
        public string effectId;
        public float effectValue;
        public string unlockQuestId;
        public int unlockDoubloonCost;
    }
}

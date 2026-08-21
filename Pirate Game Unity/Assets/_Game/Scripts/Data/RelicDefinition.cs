using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "RelicDefinition", menuName = "HighSeas/Relic Definition")]
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

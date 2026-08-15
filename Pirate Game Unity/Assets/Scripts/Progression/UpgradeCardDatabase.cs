using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Progression
{
    [CreateAssetMenu(fileName = "UpgradeCardDatabase", menuName = "Pirate Game/Upgrade Card Database")]
    public class UpgradeCardDatabase : ScriptableObject
    {
        public List<UpgradeCardData> cards = new();

        public IReadOnlyList<UpgradeCardData> Cards => cards;

        public UpgradeCardData? Find(string id)
        {
            foreach (var card in cards)
            {
                if (card.id == id)
                    return card;
            }
            return null;
        }
    }
}

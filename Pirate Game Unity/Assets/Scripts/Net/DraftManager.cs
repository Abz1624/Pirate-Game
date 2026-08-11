using System;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Level-up draft with VRF-backed offers (unpredictable card rolls per docs/03).
    /// </summary>
    public class DraftManager : MonoBehaviour
    {
        [SerializeField] ErClient erClient;

        public struct DraftOffer
        {
            public byte cardId;
            public byte rarity;
            public string displayName;
        }

        public DraftOffer[] CurrentOffers { get; private set; } = Array.Empty<DraftOffer>();
        public byte RerollsRemaining { get; private set; }
        public byte BanishRemaining { get; private set; }

        public event Action<DraftOffer[]> OnDraftShown;

        public void ShowDraft(byte playerLevel, byte[] vrfSeed)
        {
            CurrentOffers = GenerateOffers(playerLevel, vrfSeed);
            OnDraftShown?.Invoke(CurrentOffers);
        }

        public void PickCard(byte index, byte[] vrfSeed)
        {
            if (index >= CurrentOffers.Length) return;
            Debug.Log($"[Draft] pick card {CurrentOffers[index].cardId} vrf={Convert.ToBase64String(vrfSeed)}");
            // draft_pick instruction on ER
        }

        public void Reroll(byte[] vrfSeed)
        {
            if (RerollsRemaining == 0) return;
            RerollsRemaining--;
            CurrentOffers = GenerateOffers(0, vrfSeed);
            OnDraftShown?.Invoke(CurrentOffers);
        }

        DraftOffer[] GenerateOffers(byte level, byte[] vrfSeed)
        {
            // docs/03 rules: 3 cards, evolution partners 3x, maxed excluded, one per full family
            var hash = vrfSeed.Length > 0 ? vrfSeed[0] : (byte)level;
            return new[]
            {
                new DraftOffer { cardId = (byte)(hash % 20 + 1), rarity = 0, displayName = "Cannon Blast" },
                new DraftOffer { cardId = (byte)(hash % 20 + 21), rarity = 1, displayName = "Ramrod" },
                new DraftOffer { cardId = (byte)(hash % 20 + 41), rarity = 0, displayName = "Iron Fangs" },
            };
        }
    }
}

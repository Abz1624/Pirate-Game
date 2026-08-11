using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Default game config tables from docs/03 and docs/04.
    /// Serialized for update_config instruction; balance passes need no redeploy.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfigTables", menuName = "Pirate Game/Game Config Tables")]
    public class GameConfigTables : ScriptableObject
    {
        [Header("Lobby")]
        [Range(1, 12)] public int defaultLobbyCapacity = 4;
        [Range(5, 20)] public int tickHz = 10;

        [Header("Convoy (docs/04)")]
        public float convoyRadius = 15f;
        public float convoyBuffPerAlly = 0.08f;
        public float convoyBuffCap = 0.24f;

        [Header("Party scaling (docs/04) — extended past 4 for future modes")]
        public PartyScaleEntry[] partyScaling = {
            new() { players = 1, density = 1.0f, hp = 1.0f, bossHp = 1.0f, rewardPct = 1.0f },
            new() { players = 2, density = 1.75f, hp = 1.1f, bossHp = 1.9f, rewardPct = 0.95f },
            new() { players = 3, density = 2.45f, hp = 1.2f, bossHp = 2.75f, rewardPct = 0.92f },
            new() { players = 4, density = 3.1f, hp = 1.3f, bossHp = 3.55f, rewardPct = 0.90f },
            new() { players = 8, density = 5.0f, hp = 1.5f, bossHp = 5.0f, rewardPct = 0.85f },
            new() { players = 12, density = 7.0f, hp = 1.7f, bossHp = 7.0f, rewardPct = 0.80f },
        };

        [Header("Draft rarity weights (docs/03)")]
        public float commonWeight = 0.60f;
        public float uncommonWeight = 0.25f;
        public float rareWeight = 0.12f;
        public float legendaryWeight = 0.03f;
        public float evolutionPartnerWeight = 3f;

        [Header("Weapons (sample rows — full set in onchain config blob)")]
        public WeaponTableEntry[] weapons;

        [System.Serializable]
        public struct PartyScaleEntry
        {
            public int players;
            public float density;
            public float hp;
            public float bossHp;
            public float rewardPct;
        }

        [System.Serializable]
        public struct WeaponTableEntry
        {
            public string name;
            public string arcFamily;
            public string executor;
            public int damage;
            public float fireRate;
            public int pierce;
            public float range;
            public string element;
        }

        public byte[] ToConfigBlob()
        {
            // Compact binary blob for update_config — extend as needed
            return System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        }
    }
}

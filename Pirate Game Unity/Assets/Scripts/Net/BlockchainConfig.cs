using UnityEngine;

namespace PirateGame.Net
{
    [CreateAssetMenu(fileName = "BlockchainConfig", menuName = "Pirate Game/Blockchain Config")]
    public class BlockchainConfig : ScriptableObject
    {
        public enum NetworkMode
        {
            Local,
            Devnet
        }

        [Header("Mode")]
        public NetworkMode mode = NetworkMode.Local;

        [Header("Local stack")]
        public string localBaseRpc = "http://localhost:8899";
        public string localBaseWs = "ws://localhost:8900";
        public string localErRpc = "http://localhost:7799";
        public string localErWs = "ws://localhost:7800";
        public string localErValidator = "mAGicPQYBMvcYveUZA5F5UNNwyHvfYh5xkLS2Fr1mev";

        [Header("Devnet")]
        public string devnetBaseRpc = "https://api.devnet.solana.com";
        public string devnetErRpc = "https://devnet-as.magicblock.app";
        public string devnetErWs = "wss://devnet-as.magicblock.app";
        public string devnetErValidator = "MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57";

        [Header("Program")]
        public string programId = "PirCoop11111111111111111111111111111111111";

        [Header("Gameplay")]
        [Range(1, 12)] public int defaultLobbyCapacity = 4;
        [Range(5, 20)] public int tickHz = 10;
        public float predictionSmoothTime = 0.15f;

        public string BaseRpc => mode == NetworkMode.Local ? localBaseRpc : devnetBaseRpc;
        public string ErRpc => mode == NetworkMode.Local ? localErRpc : devnetErRpc;
        public string ErWs => mode == NetworkMode.Local ? localErWs : devnetErWs;
        public string ErValidator => mode == NetworkMode.Local ? localErValidator : devnetErValidator;
    }
}

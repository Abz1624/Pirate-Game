using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using PirateGame.Net;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;

namespace PirateGame.View
{
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text goldText;
        [SerializeField] Text scoreText;
        [SerializeField] Text killsText;
        [SerializeField] Text placementText;
        [SerializeField] Button continueButton;

        public void Show(uint gold, uint contribution, uint kills, byte placement)
        {
            if (panel) panel.SetActive(true);
            if (goldText) goldText.text = $"Gold earned: {gold}";
            if (scoreText) scoreText.text = $"Bank (contribution): {contribution}";
            if (killsText) killsText.text = $"Player kills: {kills}";
            if (placementText) placementText.text = placement > 0 ? $"Placement: #{placement}" : "";
        }

        public void Hide()
        {
            if (panel) panel.SetActive(false);
        }
    }

    public class SettlementController : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] ResultsScreen resultsScreen;

        IRpcClient baseRpc;
        Account payer;
        bool settling;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            if (erClient == null) erClient = FindFirstObjectByType<ErClient>();
            if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
            baseRpc = SolanaTx.Client(config.BaseRpc);
        }

        void OnEnable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (snap == null || snap.matchTimerSec < 8 * 60 - 5) return;
            ShowLocalResults(snap);
        }

        void ShowLocalResults(MatchSnapshot snap)
        {
            if (lobbyManager == null || snap.players == null) return;
            var slot = lobbyManager.LocalPlayerSlot;
            if (slot >= snap.players.Length) return;
            var p = snap.players[slot];
            resultsScreen?.Show(p.bank, p.bank, p.kills, 0);
        }

        public async void CreditProfile()
        {
            if (settling || erClient == null || !erClient.HasBoundMatch) return;
            settling = true;
            try
            {
                payer ??= SolanaTx.LoadPayer(config);
                var profile = PirateCoopProgram.ProfilePda(payer.PublicKey);
                var ix = PirateCoopProgram.CreditProfile(payer.PublicKey, erClient.MatchPda, profile);
                await SolanaTx.SendAsync(baseRpc, payer, ix);
                Debug.Log("[Settlement] credit_profile confirmed on base layer");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Settlement] credit_profile failed: {ex.Message}");
            }
            finally
            {
                settling = false;
            }
        }
    }
}

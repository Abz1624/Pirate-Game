using System.Threading.Tasks;
using UnityEngine;
using PirateGame.Net;
using PirateGame.Net.Generated;

namespace PirateGame.UI
{
    /// <summary>On-chain level-up draft overlay (sends draft_pick to world PDA).</summary>
    public class PvPDraftUI : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;

        bool draftOpen;
        uint draftTick;
        readonly string[] offerLabels = { "Weapon upgrade", "Passive boost", "Hull repair" };
        GUIStyle titleStyle;
        GUIStyle buttonStyle;
        bool stylesReady;
        bool sending;

        void Awake()
        {
            if (erClient == null) erClient = FindFirstObjectByType<ErClient>();
            if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
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
            if (snap?.events == null || lobbyManager == null) return;
            foreach (var ev in snap.events)
            {
                if (ev.kind != GameEventKind.LevelUp) continue;
                if (ev.actor != lobbyManager.LocalPlayerSlot) continue;
                if (ev.tick <= draftTick) continue;
                draftTick = ev.tick;
                draftOpen = true;
            }
        }

        void OnGUI()
        {
            if (!draftOpen || erClient == null || !erClient.MatchLive) return;
            EnsureStyles();

            var w = Screen.width;
            var h = Screen.height;
            GUI.Box(new Rect(0, 0, w, h), GUIContent.none);
            GUI.Label(new Rect(0, h * 0.2f, w, 40), "Level up — pick a card", titleStyle);

            var cardW = Mathf.Min(220f, (w - 80f) / offerLabels.Length);
            var totalW = offerLabels.Length * cardW + (offerLabels.Length - 1) * 16f;
            var startX = (w - totalW) * 0.5f;
            var y = h * 0.45f;

            for (var i = 0; i < offerLabels.Length; i++)
            {
                var rect = new Rect(startX + i * (cardW + 16f), y, cardW, 80f);
                GUI.enabled = !sending;
                if (GUI.Button(rect, offerLabels[i], buttonStyle))
                    _ = PickCardAsync((byte)i);
                GUI.enabled = true;
            }
        }

        async Task PickCardAsync(byte index)
        {
            if (sending || erClient == null) return;
            sending = true;
            try
            {
                var vrf = new byte[32];
                var ix = PirateCoopProgram.DraftPick(
                    erClient.SessionAccount.PublicKey,
                    erClient.MatchPda,
                    erClient.WorldPda,
                    index,
                    vrf);
                await SolanaTx.SendAsync(SolanaTx.Client(
                    Resources.Load<BlockchainConfig>("BlockchainConfig").ErRpc),
                    erClient.SessionAccount, ix);
                draftOpen = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Draft] pick failed: {ex.Message}");
            }
            finally
            {
                sending = false;
            }
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            stylesReady = true;
        }
    }
}

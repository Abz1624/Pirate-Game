using UnityEngine;

namespace PirateGame.View
{
    /// <summary>
    /// Wires ErClient snapshots to entity views and event pump.
    /// </summary>
    public class MatchViewController : MonoBehaviour
    {
        [SerializeField] Net.ErClient erClient;
        [SerializeField] Net.LobbyManager lobbyManager;
        [SerializeField] SnapshotInterpolator interpolator;
        [SerializeField] EntityViewPool entityPool;
        [SerializeField] GameEventPump eventPump;

        byte localPlayerSlot;

        void OnEnable()
        {
            if (erClient == null)
                erClient = FindFirstObjectByType<Net.ErClient>();
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<Net.LobbyManager>();
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
            if (lobbyManager != null)
                localPlayerSlot = lobbyManager.LocalPlayerSlot;
        }

        void OnDisable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void OnSnapshot(Net.MatchSnapshot snap)
        {
            interpolator?.PushSnapshot(snap);
            float t = 1f;
            Net.MatchSnapshot from = snap;
            Net.MatchSnapshot to = snap;
            if (interpolator != null && interpolator.TryGetBlend(out from, out to, out t))
                entityPool?.SyncFromSnapshot(to, localPlayerSlot, interpolator, t, from);
            else
                entityPool?.SyncFromSnapshot(snap, localPlayerSlot, interpolator);

            if (eventPump != null && snap.events != null)
                eventPump.ProcessEvents(snap.events);
        }
    }
}

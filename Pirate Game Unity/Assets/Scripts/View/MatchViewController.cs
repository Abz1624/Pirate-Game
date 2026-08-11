using UnityEngine;

namespace PirateGame.View
{
    /// <summary>
    /// Wires ErClient snapshots to entity views and event pump.
    /// </summary>
    public class MatchViewController : MonoBehaviour
    {
        [SerializeField] Net.ErClient erClient;
        [SerializeField] SnapshotInterpolator interpolator;
        [SerializeField] EntityViewPool entityPool;
        [SerializeField] GameEventPump eventPump;
        [SerializeField] byte localPlayerSlot;

        void OnEnable()
        {
            if (erClient == null)
                erClient = FindFirstObjectByType<Net.ErClient>();
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void OnSnapshot(Net.MatchSnapshot snap)
        {
            interpolator?.PushSnapshot(snap);
            entityPool?.SyncFromSnapshot(snap, localPlayerSlot, interpolator);
            if (eventPump != null && snap.events != null)
                eventPump.ProcessEvents(snap.events);
        }
    }
}

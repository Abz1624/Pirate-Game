using UnityEngine;

namespace HighSeas.Balance
{
    /// <summary>Scene-authored binding for solo stage balance (explicit, not scene-name heuristics).</summary>
    public sealed class StageBalanceProfileHost : MonoBehaviour
    {
        [SerializeField] StageBalanceProfile profile;
        [SerializeField] bool requireForSinglePlayerRun = true;

        public StageBalanceProfile Profile => profile;
        public bool RequireForSinglePlayerRun => requireForSinglePlayerRun;
    }
}

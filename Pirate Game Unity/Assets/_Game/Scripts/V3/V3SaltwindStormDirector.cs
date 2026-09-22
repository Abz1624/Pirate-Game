using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Profile-driven storm safe-region contraction and warning (Saltwind C05).</summary>
    public sealed class V3SaltwindStormDirector : MonoBehaviour
    {
        bool _stormWarned;
        bool _stormActiveAnnounced;

        public static V3SaltwindStormDirector Ensure()
        {
            var existing = FindFirstObjectByType<V3SaltwindStormDirector>();
            if (existing != null) return existing;
            var systems = GameObject.Find("Systems");
            var go = systems != null ? systems : new GameObject("V3SaltwindStormDirector");
            if (systems == null)
                go.name = "V3SaltwindStormDirector";
            return go.GetComponent<V3SaltwindStormDirector>() ??
                   go.AddComponent<V3SaltwindStormDirector>();
        }

        public void ResetForRun()
        {
            _stormWarned = false;
            _stormActiveAnnounced = false;
            PlayBounds.ResetStormSafeRegion();
            V3SaltwindStormRingVisual.Disable();
        }

        void Update()
        {
            if (!StageBalanceProfileContext.IsActive) return;
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded)
                return;

            var export = StageBalanceProfileContext.Export;
            if (export == null) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (SaltwindFinaleLogic.ShouldShowStormWarning(elapsed, export, _stormWarned))
            {
                _stormWarned = true;
                V3ToastHud.Instance?.Show("DREAD TIDE — storm boundary in 10 seconds!", 4f);
            }

            if (!SaltwindFinaleLogic.IsStormDamageActive(elapsed, export))
                return;

            if (!_stormActiveAnnounced)
            {
                _stormActiveAnnounced = true;
                RunCombatTelemetry.Instance?.Record("storm_started", null);
                V3ToastHud.Instance?.Show("DREAD TIDE — stay inside the safe water!", 4f);
                V3SaltwindStormRingVisual.Ensure();
            }

            var scale = SaltwindFinaleLogic.GetStormSafeRegionScale(elapsed, export);
            PlayBounds.SetProfileStormSafeScale(scale);
            V3SaltwindStormRingVisual.Instance?.Refresh();
        }
    }
}

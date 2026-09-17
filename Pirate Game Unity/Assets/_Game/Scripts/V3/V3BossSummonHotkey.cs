using HighSeas.Core;
using HighSeas.POI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.V3
{
    /// <summary>Summons the run boss with <c>B</c> when the treasury beacon POI is not placed.</summary>
    public sealed class V3BossSummonHotkey : MonoBehaviour
    {
        POIManager _poi;

        public static void Ensure()
        {
            if (FindFirstObjectByType<V3BossSummonHotkey>() != null)
                return;
            var go = new GameObject("V3BossSummonHotkey");
            go.AddComponent<V3BossSummonHotkey>();
        }

        void Update()
        {
            if (!V3RunContext.IsActive)
                return;
            if (RunManager.Instance == null || RunManager.Instance.IsRunEnded || RunManager.Instance.IsPaused)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.bKey.wasPressedThisFrame)
                return;

            if (_poi == null)
                _poi = FindFirstObjectByType<POIManager>();
            _poi?.TrySummonBoss();
        }
    }
}

using System;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>A legacy update slot at order 50: where V3's juice controller computed orbit blade positions.</summary>
    [DeletedInPhase(14, "V4 owns the frame once the run clock is V4")]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("")]
    public sealed class LegacyOrder50Slot : MonoBehaviour
    {
        public Action<float> Tick;
        void Update() => Tick?.Invoke(Time.time);
    }
}

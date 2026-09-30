using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>
    /// V5 (owner, 1 Oct 2026): the Captain's special ability on touch devices, a round pad on the right of the screen (the
    /// floating stick owns the left half). A press is taken on pointer down (no drag threshold) and read once by the
    /// Captain's frame step through <see cref="ConsumePress"/>, so the run's pause and cooldown rules still decide.
    /// Stops taking touches while the run is paused (level-up cards and results sit under it).
    /// </summary>
    public sealed class TouchAbilityButton : MonoBehaviour, IPointerDownHandler
    {
        static bool _pressed;
        Graphic _hit;

        /// <summary>True once per press (then cleared).</summary>
        public static bool ConsumePress()
        {
            var p = _pressed;
            _pressed = false;
            return p;
        }

        /// <summary>A press as the touch would make it (tests and the playable check).</summary>
        public static void Press() => _pressed = true;

        public int Presses { get; private set; }

        void Awake() => _hit = GetComponentInChildren<Graphic>();

        void OnDisable() => _pressed = false;

        void Update()
        {
            if (_hit == null) return;
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            var live = Time.timeScale > 0f && (run == null || !run.IsPaused);
            if (_hit.raycastTarget != live) _hit.raycastTarget = live;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Presses++;
            _pressed = true;
        }
    }
}

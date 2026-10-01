using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>V5 M7: what a touch button asks for.</summary>
    public enum TouchAction { Boost = 0, Pause = 1 }

    /// <summary>
    /// V5 M7: a touch button (boost, pause) taken on pointer down and read once by the system it drives through
    /// <see cref="Consume"/>, as the Captain's <see cref="TouchAbilityButton"/>. The boost button stops taking touches while
    /// the run is paused; the pause button stays live (it also resumes).
    /// </summary>
    public sealed class TouchActionButton : MonoBehaviour, IPointerDownHandler
    {
        static readonly bool[] Pressed = new bool[2];

        public TouchAction action;
        Graphic _hit;

        /// <summary>True once per press of that action (then cleared).</summary>
        public static bool Consume(TouchAction action)
        {
            var i = (int)action;
            var p = Pressed[i];
            Pressed[i] = false;
            return p;
        }

        /// <summary>A press as the touch would make it (tests and the playable check).</summary>
        public static void Press(TouchAction action) => Pressed[(int)action] = true;

        void Awake() => _hit = GetComponentInChildren<Graphic>();

        void OnDisable() => Pressed[(int)action] = false;

        void Update()
        {
            if (_hit == null || action == TouchAction.Pause) return;
            var run = Core.RunManager.Instance;
            var live = Time.timeScale > 0f && (run == null || !run.IsPaused);
            if (_hit.raycastTarget != live) _hit.raycastTarget = live;
        }

        public void OnPointerDown(PointerEventData eventData) => Pressed[(int)action] = true;
    }
}

using UnityEngine;

namespace RoguePirates.V4.Presentation.Stage
{
    /// <summary>
    /// V5 M8z (7 Oct, playtest: "the boss beacon is invisible, but can be triggered when the player is near it"). A point
    /// of interest marker whose model is a one-shot effect (the Treasury Beacon's whirlpool, FlexUnit's WaterSpell: its
    /// animator plays "start", then about 7 s later "finish", which fades the whirlpool out and stops its particles for
    /// good) would vanish a few seconds after it appears, long before the player reaches it. Each time the marker is shown
    /// its animators play their first state again and hold its last frame, so the marker stays drawn until it is used.
    /// Animators whose state loops are left alone.
    /// </summary>
    public sealed class PoiMarkerAnimationHold : MonoBehaviour
    {
        Animator[] _animators;
        int[] _firstState;
        bool[] _held;

        /// <summary>True while every non-looping animator on the marker is held at the end of its first state (tests).</summary>
        public bool Holding
        {
            get
            {
                if (_held == null) return false;
                foreach (var h in _held) if (!h) return false;
                return true;
            }
        }

        /// <summary>Adds the hold to a marker that has animators (none otherwise).</summary>
        public static void AttachIfAnimated(GameObject marker)
        {
            if (marker != null && marker.GetComponentInChildren<Animator>(true) != null && marker.GetComponent<PoiMarkerAnimationHold>() == null)
                marker.AddComponent<PoiMarkerAnimationHold>();
        }

        void OnEnable()
        {
            if (_animators == null)
            {
                _animators = GetComponentsInChildren<Animator>(true);
                _firstState = new int[_animators.Length];
                _held = new bool[_animators.Length];
            }
            for (var i = 0; i < _animators.Length; i++)
            {
                var a = _animators[i];
                _held[i] = false;
                if (a == null || a.runtimeAnimatorController == null) { _held[i] = true; continue; }
                a.speed = 1f;
                if (_firstState[i] != 0) a.Play(_firstState[i], 0, 0f);
            }
        }

        void Update()
        {
            for (var i = 0; i < _animators.Length; i++)
            {
                if (_held[i]) continue;
                var a = _animators[i];
                if (a == null || !a.isActiveAndEnabled || a.IsInTransition(0)) continue;
                var info = a.GetCurrentAnimatorStateInfo(0);
                if (_firstState[i] == 0) _firstState[i] = info.fullPathHash; // the default state, seen on its first frame
                if (info.loop) { _held[i] = true; continue; } // a looping idle: nothing to hold
                if (info.fullPathHash != _firstState[i]) { a.Play(_firstState[i], 0, 1f); continue; } // went on: back to the first state's end
                if (info.normalizedTime < 1f) continue;
                a.Play(_firstState[i], 0, 1f);
                a.speed = 0f; // its last frame (the whirlpool fully open), never reaching the "finish" transition
                _held[i] = true;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (13 Oct, owner): the numbers the code applies to a weapon visual, on the visual's prefab (root), so they
    /// can be tweaked in the Editor without code. Every value is a multiplier or offset on what the code computes (scale
    /// from the Size stat, lifetime from the effect, the spin and arc of thrown models, line widths), so 1 (and 0 for the
    /// offset) is the code's look. The presenters read these from the prefab asset each time they use it, so an edit
    /// during Play mode shows on the next shot. A prefab without this component uses the code's values. What each
    /// value does per visual is in plan/hackathon/WEAPON-PREFABS.md. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponVisualTuning : MonoBehaviour
    {
        [Tooltip("Times the scale the code gives it (from the shot's size, the blast radius, the juice settings' scale).")]
        [Min(0f)] public float scale = 1f;
        [Tooltip("Times how long the code shows it (a burst's seconds, a pulse's sweep; not a shot or a summon, which the simulation ends).")]
        [Min(0.05f)] public float lifetime = 1f;
        [Tooltip("Times its spin speed (orbit models 540 deg/s, coins 1080 deg/s in flight, the anchor's roll 420 deg/s, the whirlpool 90 deg/s).")]
        public float spin = 1f;
        [Tooltip("Times the height of its arc (a lobbed shell or ink pot, a thrown coin).")]
        [Min(0f)] public float arc = 1f;
        [Tooltip("Times its line width (pulse rings, landing rings, patch rings, the chain line).")]
        [Min(0f)] public float width = 1f;
        [Tooltip("Metres added to its height above where the code puts it.")]
        public float heightOffset;

        [Header("Colour")]
        [Tooltip("Ring and pulse lines: use this colour instead of the code's (the weapon's aim colour, the landing ring's orange). Alpha still fades as the code fades it.")]
        public bool overrideColor;
        public Color color = Color.white;
        [Tooltip("Shots: keep the prefab's own material colours. Off: the code tints every material with the weapon's projectile colour (as the cannonballs always were).")]
        public bool keepOwnColours = true;

        [Header("Set by the prefab builder")]
        [Tooltip("A flat model the code sizes to a radius (aura, whirlpool): its radius in metres at scale 1.")]
        [Min(0.01f)] public float baseRadius = 1f;
        [Tooltip("Turned to face the camera every frame (the Cursed Doubloons' coin sprite).")]
        public bool faceCamera;

        static readonly Dictionary<int, WeaponVisualTuning> Cache = new Dictionary<int, WeaponVisualTuning>();

        /// <summary>The tuning on a prefab's root (null: the code's values). Looked up once per prefab.</summary>
        public static WeaponVisualTuning Of(GameObject prefab)
        {
            if (prefab == null) return null;
            var id = prefab.GetInstanceID();
            if (!Cache.TryGetValue(id, out var tuning) || (tuning == null && !ReferenceEquals(tuning, null)))
            {
                tuning = prefab.GetComponent<WeaponVisualTuning>();
                Cache[id] = tuning;
            }
            return tuning;
        }

        public static float Scale(GameObject prefab) => Of(prefab) is { } t ? t.scale : 1f;
        public static float Lifetime(GameObject prefab) => Of(prefab) is { } t ? t.lifetime : 1f;
        public static float Spin(GameObject prefab) => Of(prefab) is { } t ? t.spin : 1f;
        public static float Arc(GameObject prefab) => Of(prefab) is { } t ? t.arc : 1f;
        public static float Width(GameObject prefab) => Of(prefab) is { } t ? t.width : 1f;
        public static float Height(GameObject prefab) => Of(prefab) is { } t ? t.heightOffset : 0f;

        /// <summary>The tuning's colour when it overrides the code's (alpha times the code's alpha), else <paramref name="code"/>.</summary>
        public static Color Colour(GameObject prefab, Color code)
        {
            var t = Of(prefab);
            if (t == null || !t.overrideColor) return code;
            var c = t.color;
            c.a *= code.a;
            return c;
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoguePirates.UI
{
    /// <summary>
    /// Hackathon UI skin (13 Oct, the art team's UI pack): one asset (Resources/HackSkin) holding the kit's sprites, the
    /// font and the ink colours of the parchment screens. V5 M9i: in the shared UI assembly (RoguePirates.UI, below V4
    /// Presentation and the hackathon assembly), read by <see cref="UiKit"/>. Swap art by replacing a PNG in Assets/_Game/Hackathon/Art/UI
    /// (same file name keeps the reference) or by dragging another sprite into the list; sprites are looked up by name
    /// (for example "Btn_Gold", "Panel_Card", "Icon_Close").
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Pirates/Hackathon UI Skin", fileName = "HackSkin")]
    public sealed class HackSkin : ScriptableObject
    {
        [Tooltip("Font of every hackathon screen (titles, buttons and text). Empty: TMP's default.")]
        public TMP_FontAsset font;

        [Header("Ink on parchment")]
        public Color ink = new Color(0.24f, 0.14f, 0.06f, 1f);
        public Color inkDim = new Color(0.48f, 0.35f, 0.23f, 1f);
        public Color inkGold = new Color(0.55f, 0.3f, 0.02f, 1f);
        public Color inkGood = new Color(0.17f, 0.45f, 0.14f, 1f);
        public Color inkBad = new Color(0.66f, 0.16f, 0.09f, 1f);

        [Header("Text on wood (header titles)")]
        public Color onWood = new Color(1f, 0.94f, 0.8f, 1f);

        [Tooltip("The kit's sprites, found by name.")]
        public Sprite[] sprites;

        static HackSkin _instance;
        static Dictionary<string, Sprite> _byName;

        /// <summary>The skin (Resources/HackSkin), or a default one with no sprites so the screens still draw.</summary>
        public static HackSkin I
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<HackSkin>("HackSkin");
                if (_instance == null) _instance = CreateInstance<HackSkin>();
                _byName = new Dictionary<string, Sprite>();
                if (_instance.sprites != null)
                    foreach (var s in _instance.sprites)
                        if (s != null) _byName[s.name] = s;
                return _instance;
            }
        }

        /// <summary>The kit sprite called <paramref name="name"/> (null when the skin lacks it).</summary>
        public static Sprite S(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var _ = I;
            return _byName != null && _byName.TryGetValue(name, out var s) ? s : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _byName = null;
        }
    }
}

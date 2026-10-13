using RoguePirates.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct polish): keeps a status strip readable without changing its callers. The panels set the text and
    /// its colour; this tints the strip to match (errors red, success green, work in progress amber) and animates the
    /// dots of a message ending in "..." so a loading or waiting state is visibly alive.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class HackStatus : MonoBehaviour
    {
        public Image Background;
        /// <summary>13 Oct: on a parchment strip (kit screens) the text is in the skin's inks; the tint follows those.</summary>
        public bool Parchment;
        TextMeshProUGUI _text;
        string _base;
        Color _lastColor;
        int _dots = -1;

        void Awake() => _text = GetComponent<TextMeshProUGUI>();

        void LateUpdate()
        {
            if (_text == null) return;
            var shown = _text.text ?? string.Empty;
            // A new message from the panel: remember it (without our dots) and its colour.
            if (_base == null || (shown != Animated(_base, _dots) && shown != _base))
            {
                _base = shown;
                _dots = -1;
            }
            if (_text.color != _lastColor || Background != null && Background.color == default)
            {
                _lastColor = _text.color;
                if (Background != null) Background.color = Parchment ? ParchmentTint(_lastColor) : HackUi.StatusTint(_lastColor);
            }
            if (Background != null) Background.enabled = !string.IsNullOrEmpty(_base);
            if (!_base.EndsWith("...")) return;
            var dots = (int)(Time.unscaledTime * 3f) % 4;
            if (dots == _dots) return;
            _dots = dots;
            _text.text = Animated(_base, dots);
        }

        /// <summary>The tint of a parchment status strip for an ink colour (red, green, amber or none).</summary>
        public static Color ParchmentTint(Color ink)
        {
            var skin = HackSkin.I;
            if (Near(ink, skin.inkBad)) return new Color(0.75f, 0.18f, 0.12f, 0.18f);
            if (Near(ink, skin.inkGood)) return new Color(0.2f, 0.6f, 0.25f, 0.18f);
            if (Near(ink, skin.inkGold)) return new Color(1f, 0.7f, 0.2f, 0.2f);
            return new Color(0f, 0f, 0f, 0f);
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        /// <summary>"Loading..." with 0 to 3 dots (spaces keep the width steady).</summary>
        public static string Animated(string message, int dots)
        {
            if (string.IsNullOrEmpty(message) || !message.EndsWith("...") || dots < 0) return message;
            var stem = message.Substring(0, message.Length - 3);
            return stem + new string('.', dots) + "<color=#00000000>" + new string('.', 3 - dots) + "</color>";
        }
    }
}

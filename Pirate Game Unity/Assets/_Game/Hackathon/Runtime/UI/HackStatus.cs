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
                if (Background != null) Background.color = HackUi.StatusTint(_lastColor);
            }
            if (Background != null) Background.enabled = !string.IsNullOrEmpty(_base);
            if (!_base.EndsWith("...")) return;
            var dots = (int)(Time.unscaledTime * 3f) % 4;
            if (dots == _dots) return;
            _dots = dots;
            _text.text = Animated(_base, dots);
        }

        /// <summary>"Loading..." with 0 to 3 dots (spaces keep the width steady).</summary>
        public static string Animated(string message, int dots)
        {
            if (string.IsNullOrEmpty(message) || !message.EndsWith("...") || dots < 0) return message;
            var stem = message.Substring(0, message.Length - 3);
            return stem + new string('.', dots) + "<color=#00000000>" + new string('.', 3 - dots) + "</color>";
        }
    }
}

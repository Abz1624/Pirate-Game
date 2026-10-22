using TMPro;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// A satisfying pop on the HUD's coin counter whenever the gold goes up: the counter punches bigger and springs back,
    /// the coin icon wobbles, and the number flashes warm gold. Watches the counter's own number (the HUD rewrites it
    /// when gold changes), so no game code is involved. Finds the counter at runtime by path (works with the SaltWind
    /// HUD; change the path for another HUD). The HUD's layout ignores scale, so nothing else moves.
    /// Mobile friendly: a few numbers per frame, idle when nothing is popping; all changes are restored.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Coin Counter Pop Feel")]
    public sealed class CoinCounterPopFeel : MonoBehaviour
    {
        [Header("Where the counter is")]
        [Tooltip("Path of the counter under a canvas (the part that pops).")]
        public string CounterPath = "HUD_SaltWind/SafeArea/TopRight/CurrencyBar/Gold";
        [Tooltip("Path of the number text, under the counter.")]
        public string AmountPath = "Pill/Amount";
        [Tooltip("Path of the coin icon, under the counter (wobbles). Leave empty for none.")]
        public string IconPath = "Icon";

        [Header("Pop")]
        [Tooltip("Pop size for a small gain (0.18 = 18% bigger at the peak).")]
        public float SmallPop = 0.18f;
        [Tooltip("Pop size for a big gain.")]
        public float BigPop = 0.32f;
        [Tooltip("A gain this large or larger gets the big pop.")]
        public int BigGain = 50;
        [Tooltip("How long the pop takes to settle (seconds).")]
        public float Duration = 0.35f;
        [Tooltip("Wobbles per second (higher = snappier).")]
        public float Frequency = 4.5f;
        [Tooltip("Coin icon wobble (degrees).")]
        public float IconWobble = 14f;

        [Header("Number flash")]
        public Color FlashColor = new Color(1f, 0.86f, 0.3f, 1f);
        [Tooltip("How long the number stays tinted (seconds).")]
        public float FlashDuration = 0.3f;

        RectTransform _counter, _icon;
        TMP_Text _amount;
        Color _baseColor;
        Quaternion _iconBase;
        int _lastValue = int.MinValue;
        float _value, _velocity, _omega = 28f, _zeta = 0.3f, _flash, _wobbleTime = -1f, _wobbleAmp, _nextSearch;
        bool _active;

        void LateUpdate()
        {
            if (_counter == null || _amount == null)
            {
                if (Time.unscaledTime < _nextSearch) return;
                _nextSearch = Time.unscaledTime + 1f;
                if (!Find()) return;
            }
            if (!_counter.gameObject.activeInHierarchy) return;

            int value;
            if (TryParse(_amount.text, out value))
            {
                if (_lastValue != int.MinValue && value > _lastValue) Pop(value - _lastValue);
                _lastValue = value;
            }

            if (!_active) return;
            var dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

            // Springy scale punch.
            for (var i = 0; i < 2; i++)
            {
                var h = dt * 0.5f;
                _velocity += (-_omega * _omega * _value - 2f * _zeta * _omega * _velocity) * h;
                _value += _velocity * h;
            }
            _counter.localScale = Vector3.one * (1f + _value);

            // Icon wobble that fades out.
            if (_icon != null && _wobbleTime >= 0f)
            {
                _wobbleTime += dt;
                var fade = Mathf.Clamp01(1f - _wobbleTime / Duration);
                _icon.localRotation = _iconBase * Quaternion.Euler(0f, 0f, Mathf.Sin(_wobbleTime * 38f) * _wobbleAmp * fade);
                if (fade <= 0f) { _icon.localRotation = _iconBase; _wobbleTime = -1f; }
            }

            // Number tint back to its own colour.
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt);
                _amount.color = Color.Lerp(_baseColor, FlashColor, _flash / Mathf.Max(0.01f, FlashDuration));
            }

            if (Mathf.Abs(_value) < 0.0005f && Mathf.Abs(_velocity) < 0.01f && _flash <= 0f && _wobbleTime < 0f)
            {
                _value = _velocity = 0f;
                _counter.localScale = Vector3.one;
                _amount.color = _baseColor;
                _active = false;
            }
        }

        void Pop(int gain)
        {
            var t = Mathf.Clamp01(Mathf.Log(1f + gain) / Mathf.Log(1f + Mathf.Max(2, BigGain)));
            var amount = Mathf.Lerp(SmallPop, BigPop, t);
            _omega = Mathf.Max(0.5f, Frequency) * Mathf.PI * 2f;
            _zeta = Mathf.Clamp(4f / (_omega * Duration), 0.05f, 0.95f);
            // Kick so the first peak is about 'amount'; quick pickups add on (capped so it never balloons).
            var root = Mathf.Sqrt(1f - _zeta * _zeta);
            var wd = _omega * root;
            var tPeak = Mathf.Atan2(root, _zeta) / wd;
            var peakPerVelocity = Mathf.Exp(-_zeta * _omega * tPeak) * Mathf.Sin(wd * tPeak) / wd;
            _velocity = Mathf.Min(_velocity + amount / Mathf.Max(1e-4f, peakPerVelocity), BigPop * 1.3f / Mathf.Max(1e-4f, peakPerVelocity));
            if (_icon != null) { _wobbleTime = 0f; _wobbleAmp = IconWobble * Mathf.Lerp(0.7f, 1.2f, t); }
            _flash = FlashDuration;
            _active = true;
        }

        bool Find()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                var c = canvas.transform.Find(CounterPath) as RectTransform;
                if (c == null) continue;
                var a = c.Find(AmountPath);
                var text = a != null ? a.GetComponent<TMP_Text>() : null;
                if (text == null) continue;
                _counter = c;
                _amount = text;
                _baseColor = text.color;
                _icon = string.IsNullOrEmpty(IconPath) ? null : c.Find(IconPath) as RectTransform;
                if (_icon != null) _iconBase = _icon.localRotation;
                _lastValue = int.MinValue;
                return true;
            }
            return false;
        }

        static bool TryParse(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            var any = false;
            long v = 0;
            for (var i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                if (ch >= '0' && ch <= '9') { v = v * 10 + (ch - '0'); any = true; if (v > int.MaxValue) return false; }
            }
            value = (int)v;
            return any;
        }

        void OnDisable()
        {
            if (_counter != null) _counter.localScale = Vector3.one;
            if (_amount != null && _active) _amount.color = _baseColor;
            if (_icon != null) _icon.localRotation = _iconBase;
            _active = false;
            _value = _velocity = 0f;
        }
    }
}

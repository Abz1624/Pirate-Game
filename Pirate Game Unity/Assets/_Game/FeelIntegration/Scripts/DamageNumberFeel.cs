using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Makes the game's floating damage numbers read better, on top of DamageNumberPresenter (no game code changed):
    ///  - a quick pop-in punch when a number appears (bigger, with a small wobble, on crits);
    ///  - a soft dark drop shadow so numbers stay readable over bright water, flashes and explosions.
    /// The presenter keeps owning the text, colour, rise and fade; this only adds scale/rotation after it each frame
    /// and a shadow copy of the text. Finds the pooled number objects itself (a cheap scan once a second) and does
    /// no allocations per frame (mobile friendly).
    /// </summary>
    [DefaultExecutionOrder(300)] // after DamageNumberPresenter moves and faces the numbers
    [AddComponentMenu("Rogue Pirates/Feel/Damage Number Feel")]
    public sealed class DamageNumberFeel : MonoBehaviour
    {
        [Header("Pop-in")]
        [Tooltip("Peak size of a normal hit's number as it appears (1.4 = 40% bigger), then it settles to normal.")]
        public float PopScale = 1.4f;
        [Tooltip("Peak size of a crit's number.")]
        public float CritPopScale = 1.8f;
        [Tooltip("How long the pop lasts (seconds).")]
        public float PopDuration = 0.2f;
        [Tooltip("Crit wobble (degrees), fading out.")]
        public float CritWobble = 10f;

        [Header("Drop shadow")]
        public bool Shadow = true;
        [Tooltip("Shadow offset in the number's own space (right, down).")]
        public Vector2 ShadowOffset = new Vector2(0.035f, -0.035f);
        [Range(0f, 1f)] public float ShadowOpacity = 0.6f;
        public Color ShadowColor = new Color(0.08f, 0.05f, 0.03f, 1f);

        [Tooltip("Names of the game's damage-number objects (the pool renames its copies).")]
        public string[] NumberObjectNames = { "fx/damage_number", "DamageNumberTemplate" };

        class Tracked
        {
            public TextMesh Text;
            public TextMesh ShadowText;
            public bool WasActive;
            public string LastText;
            public float Age = -1f;
            public bool Crit;
        }

        readonly List<Tracked> _tracked = new List<Tracked>(64);
        readonly HashSet<TextMesh> _known = new HashSet<TextMesh>();
        float _nextScan;

        void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                Scan();
            }
            var dt = Time.deltaTime;
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var t = _tracked[i];
                if (t.Text == null) { _tracked.RemoveAt(i); continue; }
                var go = t.Text.gameObject;
                var active = go.activeInHierarchy;
                if (!active) { t.WasActive = false; continue; }

                // A number just appeared (or a pooled one was reused with new text).
                if (!t.WasActive || t.Text.text != t.LastText)
                {
                    t.Age = 0f;
                    t.LastText = t.Text.text;
                    t.Crit = t.Text.fontStyle == FontStyle.Bold && t.Text.fontSize >= 70; // the presenter's crit style
                }
                t.WasActive = true;

                // Pop: fast rise to the peak, then ease back to normal size.
                var tr = go.transform;
                if (t.Age >= 0f)
                {
                    t.Age += dt;
                    var n = Mathf.Clamp01(t.Age / Mathf.Max(0.02f, PopDuration));
                    var peak = t.Crit ? CritPopScale : PopScale;
                    float s;
                    if (n < 0.3f) s = Mathf.Lerp(0.6f, peak, EaseOut(n / 0.3f));
                    else s = Mathf.Lerp(peak, 1f, EaseOut((n - 0.3f) / 0.7f));
                    tr.localScale = Vector3.one * s;
                    if (t.Crit && CritWobble > 0f)
                        tr.rotation *= Quaternion.AngleAxis(Mathf.Sin(t.Age * 40f) * CritWobble * (1f - n), Vector3.forward);
                    if (n >= 1f) { t.Age = -1f; tr.localScale = Vector3.one; }
                }

                if (Shadow) UpdateShadow(t);
                else if (t.ShadowText != null && t.ShadowText.gameObject.activeSelf) t.ShadowText.gameObject.SetActive(false);
            }
        }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        void UpdateShadow(Tracked t)
        {
            if (t.ShadowText == null)
            {
                var go = new GameObject("Shadow");
                go.transform.SetParent(t.Text.transform, false);
                t.ShadowText = go.AddComponent<TextMesh>();
                var mr = go.GetComponent<MeshRenderer>();
                var src = t.Text.GetComponent<MeshRenderer>();
                if (mr != null && src != null)
                {
                    mr.sharedMaterial = src.sharedMaterial;
                    mr.sortingLayerID = src.sortingLayerID;
                    mr.sortingOrder = src.sortingOrder - 1; // just behind the number
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }
            var s = t.ShadowText;
            if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
            if (s.text != t.Text.text) s.text = t.Text.text;
            s.font = t.Text.font;
            s.fontSize = t.Text.fontSize;
            s.characterSize = t.Text.characterSize;
            s.fontStyle = t.Text.fontStyle;
            s.anchor = t.Text.anchor;
            s.alignment = t.Text.alignment;
            var c = ShadowColor;
            c.a = ShadowOpacity * t.Text.color.a; // fades with the number
            s.color = c;
            s.transform.localPosition = new Vector3(ShadowOffset.x, ShadowOffset.y, 0.02f);
            s.transform.localRotation = Quaternion.identity;
            s.transform.localScale = Vector3.one;
        }

        void Scan()
        {
            foreach (var tm in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tm == null || _known.Contains(tm)) continue;
                if (!IsNumberName(tm.gameObject.name)) continue; // (our own shadows are named "Shadow", so they never match)
                if (!tm.gameObject.scene.IsValid()) continue; // the hidden template's prefab-less copy is fine; assets are not
                _known.Add(tm);
                _tracked.Add(new Tracked { Text = tm, WasActive = false });
            }
        }

        bool IsNumberName(string name)
        {
            if (NumberObjectNames == null) return false;
            for (var i = 0; i < NumberObjectNames.Length; i++)
                if (!string.IsNullOrEmpty(NumberObjectNames[i]) && name.StartsWith(NumberObjectNames[i])) return true;
            return false;
        }

        void OnDisable()
        {
            foreach (var t in _tracked)
            {
                if (t.Text != null) t.Text.transform.localScale = Vector3.one;
                if (t.ShadowText != null) t.ShadowText.gameObject.SetActive(false);
            }
        }
    }
}

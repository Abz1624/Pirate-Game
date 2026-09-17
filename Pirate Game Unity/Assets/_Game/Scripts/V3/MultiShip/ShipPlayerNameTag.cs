using HighSeas.Player;
using TMPro;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Billboard player name above a ship (PvP / co-op).</summary>
    [DisallowMultipleComponent]
    public sealed class ShipPlayerNameTag : MonoBehaviour
    {
        const float HeightOffset = 7.8f;

        [SerializeField] float heightOffset = HeightOffset;

        TextMeshPro _label;
        Transform _anchor;
        string _displayName;

        public void SetDisplayName(string displayName)
        {
            _displayName = displayName ?? "Sailor";
            EnsureLabel();
            if (_label != null)
                _label.text = _displayName;
        }

        void Start()
        {
            var ship = GetComponent<PlayerShipController>();
            _anchor = ship != null && ship.VisualModel != null ? ship.VisualModel : transform;
            EnsureLabel();
        }

        void EnsureLabel()
        {
            if (_label != null)
                return;

            var go = new GameObject("PlayerNameLabel");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshPro>();
            _label.text = _displayName ?? "Sailor";
            _label.font = TMP_Settings.defaultFontAsset;
            _label.fontSize = 3.2f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.98f, 0.96f, 0.9f, 1f);
            _label.outlineWidth = 0.22f;
            _label.outlineColor = new Color(0.05f, 0.08f, 0.14f, 0.95f);
            _label.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;
        }

        void LateUpdate()
        {
            if (_label == null)
                return;

            if (_anchor == null)
            {
                var ship = GetComponent<PlayerShipController>();
                _anchor = ship != null && ship.VisualModel != null ? ship.VisualModel : transform;
            }

            if (_anchor == null)
                return;

            var pos = _anchor.position + Vector3.up * heightOffset;
            _label.transform.position = pos;

            var cam = Camera.main;
            if (cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }
    }
}

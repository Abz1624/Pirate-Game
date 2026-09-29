using TMPro;
using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>World-space name label for placeholder POIs and objects.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldBillboardLabel : MonoBehaviour
    {
        [SerializeField] string displayName = "Placeholder";
        [SerializeField] string statusText = "";

        TextMeshPro _label;
        Camera _camera;

        public void Configure(string name, string status = "")
        {
            displayName = name;
            statusText = status;
            if (_label != null)
                RefreshText();
        }

        void Start()
        {
            _camera = Camera.main;
            EnsureLabel();
            RefreshText();
        }

        void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null) return;

            transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);
        }

        void EnsureLabel()
        {
            if (_label != null) return;

            var go = new GameObject("BillboardLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.2f;
            _label = go.AddComponent<TextMeshPro>();
            _label.fontSize = 3.2f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = PlaceholderFactory.UiAccent;
            _label.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
        }

        void RefreshText()
        {
            if (_label == null) return;
            _label.text = string.IsNullOrEmpty(statusText)
                ? displayName
                : $"{displayName}\n<size=70%>{statusText}</size>";
        }

        public static void Attach(GameObject target, string name, string status = "")
        {
            if (target == null) return;
            var label = target.GetComponent<WorldBillboardLabel>();
            if (label == null)
                label = target.AddComponent<WorldBillboardLabel>();
            label.Configure(name, status);
        }
    }
}

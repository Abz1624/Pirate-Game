using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>What the V4 menu controller drives: the generated carousel or the art team's panels.</summary>
    public interface ICaptainMenuView
    {
        event Action<int> CaptainChanged;
        event Action PlayRequested;
        int SelectedIndex { get; }
        void Show(IReadOnlyList<CaptainMenuEntry> entries, int selected, string hint);
        void Refresh(IReadOnlyList<CaptainMenuEntry> entries);
        void SetPlayButton(string label, bool enabled);
    }

    /// <summary>
    /// The art team's main menu and Captain select (demo, 30 Sep 2026), bound to the V4 menu: Set Sail opens Captain
    /// select; each Captain slot (CharacterSlot_&lt;name&gt;, matched to the Captain's display name) focuses that Captain,
    /// shows its details panel (ability text from the Captain config) and its lock; the Set Sail button there starts the
    /// run, or buys a Captain whose only missing piece is Rum. Buttons with no V4 feature yet (leaderboard, ledger, raid,
    /// settings, profile) are shown but not interactive.
    /// </summary>
    public sealed class ArtMenuView : MonoBehaviour, ICaptainMenuView
    {
        public event Action<int> CaptainChanged;
        public event Action PlayRequested;

        ArtMenuSettings _settings;
        GameObject _main, _select, _mainBackdrop, _selectBackdrop;
        Camera _menuCamera;
        Func<int> _rum;
        string _currency = "RUM";
        TextMeshProUGUI _rumLabel;
        Button _setSail;
        Image _setSailArt;
        Sprite _setSailSprite;
        TextMeshProUGUI _setSailText;
        readonly List<Slot> _slots = new List<Slot>();
        IReadOnlyList<CaptainMenuEntry> _entries = Array.Empty<CaptainMenuEntry>();
        int _selected;
        bool _binding;

        sealed class Slot
        {
            public string Name;
            public Toggle Toggle;
            public GameObject LockOverlay;
            public TextMeshProUGUI LockText;
            public GameObject Details;
            public GameObject Boat;
            public int Entry = -1;
        }

        public int SelectedIndex => _selected;
        /// <summary>The Rum name from the reward config (the controller knows it once its services are up).</summary>
        public void SetCurrency(string currency) { if (!string.IsNullOrEmpty(currency)) _currency = currency; UpdateRum(); }
        public bool OnCaptainSelect => _select != null && _select.activeSelf;

        /// <summary>The art menu on the canvas, or null when its settings are missing (the generated carousel is used then).</summary>
        public static ArtMenuView TryCreate(Transform canvas, Func<int> rum, string currency)
        {
            var settings = ArtMenuSettings.Load();
            if (settings == null || !settings.Usable || canvas == null) return null;
            var view = canvas.gameObject.AddComponent<ArtMenuView>();
            view.Build(settings, canvas, rum, currency);
            return view;
        }

        void Build(ArtMenuSettings settings, Transform canvas, Func<int> rum, string currency)
        {
            _settings = settings;
            _rum = rum;
            _currency = string.IsNullOrEmpty(currency) ? "RUM" : currency;
            _menuCamera = Camera.main;
            if (settings.mainMenuBackdrop != null) _mainBackdrop = Instantiate(settings.mainMenuBackdrop);
            if (settings.characterSelectBackdrop != null) _selectBackdrop = Instantiate(settings.characterSelectBackdrop);
            // The panels' roots are zero-size in their prefabs (the art scene stretched them); stretch them over the canvas.
            _main = Stretch(Instantiate(settings.mainMenuPanel, canvas, false));
            _select = Stretch(Instantiate(settings.characterSelectionPanel, canvas, false));

            // Main menu.
            OnClick(_main.transform, "SetSail", () => ShowSelect(true));
            foreach (var name in new[] { "Leaderboard", "Ledger", "Raid", "Settings", "Profile" })
            {
                var b = FindDeep(_main.transform, name)?.GetComponent<Button>();
                if (b != null) b.interactable = false;
            }
            _rumLabel = FindDeep(_main.transform, "RumBg")?.GetComponentInChildren<TextMeshProUGUI>(true);

            // Captain select.
            OnClick(_select.transform, "BackButton", () => ShowSelect(false));
            _setSail = FindDeep(_select.transform, "SetSailButton")?.GetComponent<Button>();
            if (_setSail != null)
            {
                _setSail.onClick.AddListener(() => PlayRequested?.Invoke());
                _setSailArt = FindDeep(_setSail.transform, "Art")?.GetComponent<Image>();
                if (_setSailArt != null) _setSailSprite = _setSailArt.sprite;
                var textGo = new GameObject("V4Label", typeof(RectTransform));
                textGo.transform.SetParent(_setSail.transform, false);
                Stretch(textGo);
                _setSailText = textGo.AddComponent<TextMeshProUGUI>();
                _setSailText.alignment = TextAlignmentOptions.Center;
                _setSailText.enableAutoSizing = true;
                _setSailText.fontSizeMin = 14f;
                _setSailText.fontSizeMax = 40f;
                _setSailText.color = new Color(0.25f, 0.13f, 0.05f);
                _setSailText.raycastTarget = false;
                if (_settings.font != null) _setSailText.font = _settings.font;
                textGo.SetActive(false);
            }
            foreach (var toggle in _select.GetComponentsInChildren<Toggle>(true))
            {
                if (!toggle.name.StartsWith("CharacterSlot_", StringComparison.Ordinal)) continue;
                var slot = new Slot { Name = toggle.name.Substring("CharacterSlot_".Length), Toggle = toggle };
                slot.LockOverlay = FindDeep(toggle.transform, "LockOverlay")?.gameObject;
                slot.Details = FindDeep(_select.transform, "CharacterDetails_" + slot.Name)?.gameObject;
                if (_selectBackdrop != null) slot.Boat = FindDeep(_selectBackdrop.transform, slot.Name + "_Boat")?.gameObject;
                if (slot.LockOverlay != null) slot.LockText = AddLockText(slot.LockOverlay.transform);
                var captured = slot;
                toggle.onValueChanged.AddListener(on => { if (on && !_binding) Focus(captured); });
                _slots.Add(slot);
            }
            ShowSelect(false);
        }

        public void Show(IReadOnlyList<CaptainMenuEntry> entries, int selected, string hint)
        {
            Refresh(entries);
            _selected = selected;
            var slot = _slots.Find(s => s.Entry == selected);
            if (slot != null) Focus(slot, notify: false);
        }

        public void Refresh(IReadOnlyList<CaptainMenuEntry> entries)
        {
            _entries = entries ?? Array.Empty<CaptainMenuEntry>();
            foreach (var slot in _slots)
            {
                slot.Entry = -1;
                for (var i = 0; i < _entries.Count; i++)
                    if (string.Equals(_entries[i].Label, slot.Name, StringComparison.OrdinalIgnoreCase)) slot.Entry = i;
                slot.Toggle.gameObject.SetActive(slot.Entry >= 0);
                if (slot.Entry < 0) continue;
                var e = _entries[slot.Entry];
                slot.Toggle.interactable = true; // locked Captains stay focusable (and buyable)
                if (slot.LockOverlay != null) slot.LockOverlay.SetActive(e.Locked);
                if (slot.LockText != null) slot.LockText.text = e.Locked ? e.LockReason : "";
                if (slot.Details != null) FillDetails(slot.Details.transform, e);
            }
            UpdateRum();
        }

        public void SetPlayButton(string label, bool enabled)
        {
            if (_setSail == null) return;
            _setSail.interactable = enabled;
            var plain = label == "SET SAIL";
            if (_setSailArt != null) _setSailArt.sprite = plain || _settings.setSailNoText == null ? _setSailSprite : _settings.setSailNoText;
            if (_setSailText != null)
            {
                _setSailText.gameObject.SetActive(!plain);
                _setSailText.text = label;
            }
        }

        void Focus(Slot slot, bool notify = true)
        {
            if (slot == null || slot.Entry < 0) return;
            _selected = slot.Entry;
            _binding = true;
            foreach (var s in _slots)
            {
                if (s.Details != null) s.Details.SetActive(s == slot);
                if (s.Boat != null) s.Boat.SetActive(s == slot);
                if (s == slot && !s.Toggle.isOn) s.Toggle.isOn = true;
            }
            _binding = false;
            if (notify) CaptainChanged?.Invoke(slot.Entry);
        }

        void ShowSelect(bool select)
        {
            _main.SetActive(!select);
            _select.SetActive(select);
            if (_mainBackdrop != null) _mainBackdrop.SetActive(!select);
            if (_selectBackdrop != null) _selectBackdrop.SetActive(select);
            // A backdrop brings its own camera; the menu camera shows only when the active screen has none.
            var backdrop = select ? _selectBackdrop : _mainBackdrop;
            if (_menuCamera != null) _menuCamera.enabled = backdrop == null || backdrop.GetComponentInChildren<Camera>(true) == null;
            if (select)
            {
                var slot = _slots.Find(s => s.Entry == _selected);
                if (slot != null) Focus(slot, notify: false);
            }
            UpdateRum();
        }

        void UpdateRum()
        {
            if (_rumLabel != null && _rum != null) _rumLabel.text = _rum() + " " + _currency;
        }

        static void FillDetails(Transform details, CaptainMenuEntry e)
        {
            var name = FindDeep(details, "CharacterName")?.GetComponent<TextMeshProUGUI>();
            if (name != null && !string.IsNullOrEmpty(e.Label)) name.text = e.Label.ToUpperInvariant();
            var ability = FindDeep(details, "AbilityName")?.GetComponent<TextMeshProUGUI>();
            if (ability != null && !string.IsNullOrEmpty(e.ActiveName)) ability.text = e.ActiveName.ToUpperInvariant();
            var description = FindDeep(details, "AbilityDescription")?.GetComponent<TextMeshProUGUI>();
            if (description != null && !string.IsNullOrEmpty(e.ActiveSummary))
                description.text = string.IsNullOrEmpty(e.PassiveSummary) ? e.ActiveSummary : e.ActiveSummary + "\n<size=80%>" + e.PassiveSummary + "</size>";
        }

        TextMeshProUGUI AddLockText(Transform overlay)
        {
            var go = new GameObject("V4LockReason", typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.3f);
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, 0f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 10f;
            t.fontSizeMax = 20f;
            t.color = new Color(1f, 0.92f, 0.75f);
            t.raycastTarget = false;
            if (_settings.font != null) t.font = _settings.font;
            return t;
        }

        static void OnClick(Transform root, string name, Action action)
        {
            var b = FindDeep(root, name)?.GetComponent<Button>();
            if (b != null) b.onClick.AddListener(() => action());
        }

        static GameObject Stretch(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return go;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return go;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        void OnDestroy()
        {
            if (_mainBackdrop != null) Destroy(_mainBackdrop);
            if (_selectBackdrop != null) Destroy(_selectBackdrop);
        }
    }
}

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
        /// <summary>V5 M7/M8: a hub screen asked for ("Settings", "Ledger" for the shop, "Profile").</summary>
        public event System.Action<string> HubRequested;

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
        bool _binding, _ordered, _ownsBackdrops;
        TextMeshProUGUI _lockReason;

        sealed class Slot
        {
            public string Name;
            public Toggle Toggle;
            public GameObject LockOverlay;
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
            if (canvas == null) return null;
            var settings = ArtMenuSettings.Load();
            var inScene = FindDeep(canvas, "MainMenuPanel") != null && FindDeep(canvas, "CharacterSelectionPanel") != null;
            if (!inScene && (settings == null || !settings.Usable)) return null;
            var view = canvas.gameObject.AddComponent<ArtMenuView>();
            view.Build(settings, canvas, rum, currency);
            return view;
        }

        /// <summary>Playtest (3 Oct, owner): a backdrop made under an inactive holder and stripped of its NavMesh components
        /// before it wakes (a plain Instantiate enabled the boats' agents, which logged "Failed to create agent because
        /// there is no valid NavMesh" before the strip below ran).</summary>
        static GameObject InstantiateScenery(GameObject prefab)
        {
            var holder = new GameObject("BackdropHolder");
            holder.SetActive(false);
            var go = Instantiate(prefab, holder.transform, false);
            go.name = prefab.name;
            foreach (var agent in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(agent);
            foreach (var obstacle in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) DestroyImmediate(obstacle);
            go.transform.SetParent(null, true);
            Destroy(holder);
            return go;
        }

        static GameObject SceneRoot(string name)
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        void Build(ArtMenuSettings settings, Transform canvas, Func<int> rum, string currency)
        {
            _settings = settings;
            _rum = rum;
            _currency = string.IsNullOrEmpty(currency) ? "RUM" : currency;
            // The menu scene holds the art team's panels and 3D sets (Tools > Rogue Pirates > Demo > Build Demo Scenes);
            // without them, the prefabs from the settings are used.
            // Wide phones (20:9): scale by height so the top bar and Set Sail stay on screen; 16:9 and narrower by width.
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.matchWidthOrHeight = Screen.width / (float)Mathf.Max(1, Screen.height) > scaler.referenceResolution.x / scaler.referenceResolution.y + 0.01f ? 1f : 0f;
            _main = FindDeep(canvas, "MainMenuPanel")?.gameObject;
            _select = FindDeep(canvas, "CharacterSelectionPanel")?.gameObject;
            _mainBackdrop = SceneRoot("MainMenuSetup");
            _selectBackdrop = SceneRoot("CharacterSelection Setup");
            _ownsBackdrops = _mainBackdrop == null && _selectBackdrop == null;
            if (_ownsBackdrops && settings != null)
            {
                if (settings.mainMenuBackdrop != null) _mainBackdrop = InstantiateScenery(settings.mainMenuBackdrop);
                if (settings.characterSelectBackdrop != null) _selectBackdrop = InstantiateScenery(settings.characterSelectBackdrop);
            }
            // Playtest (3 Oct): the captains' boats carry NavMeshAgents from the art scene and the menu has no NavMesh
            // ("Failed to create agent because there is no valid NavMesh"): the backdrops are scenery, strip them.
            foreach (var backdrop in new[] { _mainBackdrop, _selectBackdrop })
            {
                if (backdrop == null) continue;
                foreach (var agent in backdrop.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(agent);
                foreach (var obstacle in backdrop.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) DestroyImmediate(obstacle);
            }
            // The panels' roots are zero-size in their prefabs (the art scene stretched them); stretch them over the canvas.
            if (_main == null) _main = Stretch(Instantiate(settings.mainMenuPanel, canvas, false));
            if (_select == null) _select = Stretch(Instantiate(settings.characterSelectionPanel, canvas, false));
            // The menu camera stays on only when it is not one of the sets' cameras and the showing set has none.
            _menuCamera = Camera.main;
            if (_menuCamera != null && ((_mainBackdrop != null && _menuCamera.transform.IsChildOf(_mainBackdrop.transform)) ||
                                        (_selectBackdrop != null && _menuCamera.transform.IsChildOf(_selectBackdrop.transform)))) _menuCamera = null;
            if (!_ownsBackdrops) _menuCamera = null; // the scene's cameras are the artists' (as in their UI scene)

            // Main menu. Its Background keeps a 16:9 shape (an aspect fitter that fills the screen), which pushes the top bar
            // and Set Sail off wider phones (20:9): it is stretched over the screen instead, so the buttons sit in its corners.
            var background = _main.transform.Find("Background") as RectTransform;
            if (background != null)
            {
                var fitter = background.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.enabled = false;
                background.anchorMin = Vector2.zero;
                background.anchorMax = Vector2.one;
                background.offsetMin = Vector2.zero;
                background.offsetMax = Vector2.zero;
            }
            // Main menu.
            // Playtest (4 Oct, owner): Set Sail goes straight to the Captain select (no mode panel); PvP has its own button.
            OnClick(_main.transform, "SetSail", () => ShowSelect(true));
            // V5 M7/M8: Settings, the shop (the Ledger) and the Profile open the menu hub's screens; Raids and the
            // leaderboard come later.
            foreach (var name in new[] { "Leaderboard", "Ledger", "Raid", "Settings", "Profile" })
            {
                var b = FindDeep(_main.transform, name)?.GetComponent<Button>();
                if (b == null) continue;
                var open = name == "Settings" || name == "Ledger" || name == "Profile" || name == "Leaderboard"; // Playtest (3 Oct, owner): the weekly leaderboard
                b.interactable = open;
                if (open) { var n = name; b.onClick.AddListener(() => HubRequested?.Invoke(n)); }
            }
            FitSideButtons(_main.transform);
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
                if (_settings != null && _settings.font != null) _setSailText.font = _settings.font;
                textGo.SetActive(false);
            }
            // What unlocks the focused Captain, above Set Sail ("LockReason" in the scene, else made here).
            _lockReason = FindDeep(_select.transform, "LockReason")?.GetComponent<TextMeshProUGUI>();
            if (_lockReason == null && _setSail != null) _lockReason = AddLockText(_setSail.transform);
            if (_lockReason != null) _lockReason.gameObject.SetActive(false);
            foreach (var toggle in _select.GetComponentsInChildren<Toggle>(true))
            {
                if (!toggle.name.StartsWith("CharacterSlot_", StringComparison.Ordinal)) continue;
                var slot = new Slot { Name = toggle.name.Substring("CharacterSlot_".Length), Toggle = toggle };
                slot.LockOverlay = FindDeep(toggle.transform, "LockOverlay")?.gameObject;
                slot.Details = FindDeep(_select.transform, "CharacterDetails_" + slot.Name)?.gameObject;
                if (_selectBackdrop != null) slot.Boat = FindDeep(_selectBackdrop.transform, slot.Name + "_Boat")?.gameObject;
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
                if (slot.Details != null) FillDetails(slot.Details.transform, e);
            }
            if (!_ordered) OrderSlots();
            UpdateRum();
        }

        /// <summary>The slots in the Captains' menu order (Rook, Hank, Richie, Sparky, Ember, Roxie), filling the art's
        /// groups (pages) in turn.</summary>
        void OrderSlots()
        {
            _ordered = true;
            var groups = new List<Transform>();
            foreach (var slot in _slots)
                if (slot.Toggle.transform.parent != null && !groups.Contains(slot.Toggle.transform.parent)) groups.Add(slot.Toggle.transform.parent);
            if (groups.Count == 0) return;
            groups.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var ordered = new List<Slot>(_slots);
            ordered.Sort((a, b) => (a.Entry < 0 ? int.MaxValue : a.Entry).CompareTo(b.Entry < 0 ? int.MaxValue : b.Entry));
            var perGroup = Mathf.Max(1, Mathf.CeilToInt(ordered.Count / (float)groups.Count));
            for (var i = 0; i < ordered.Count; i++)
            {
                var t = ordered[i].Toggle.transform;
                t.SetParent(groups[Mathf.Min(i / perGroup, groups.Count - 1)], false);
                t.SetSiblingIndex(i % perGroup);
            }
        }

        public void SetPlayButton(string label, bool enabled)
        {
            if (_setSail == null) return;
            _setSail.interactable = enabled;
            var plain = label == "SET SAIL";
            if (_setSailArt != null) _setSailArt.sprite = plain || _settings == null || _settings.setSailNoText == null ? _setSailSprite : _settings.setSailNoText;
            if (_setSailText != null)
            {
                _setSailText.gameObject.SetActive(!plain);
                _setSailText.text = RoguePirates.V4.Foundation.Core.DemoMode.Text(label);
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
            if (_lockReason != null)
            {
                var e = _entries[slot.Entry];
                _lockReason.gameObject.SetActive(e.Locked && !string.IsNullOrEmpty(e.LockReason));
                _lockReason.text = e.Locked ? RoguePirates.V4.Foundation.Core.DemoMode.Text(e.LockReason) : "";
            }
            if (notify) CaptainChanged?.Invoke(slot.Entry);
        }

        /// <summary>V5 M8: the Captain select (a Voyage chosen in the mode select).</summary>
        public void OpenCaptainSelect() => ShowSelect(true);

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
            // Demo: no "Special Ability" heading.
            var heading = FindDeep(details, "SectionTitle");
            if (heading != null) heading.gameObject.SetActive(false);
            var name = FindDeep(details, "CharacterName")?.GetComponent<TextMeshProUGUI>();
            if (name != null && !string.IsNullOrEmpty(e.Label)) name.text = e.Label.ToUpperInvariant();
            var ability = FindDeep(details, "AbilityName")?.GetComponent<TextMeshProUGUI>();
            if (ability != null && !string.IsNullOrEmpty(e.ActiveName)) ability.text = e.ActiveName.ToUpperInvariant();
            var description = FindDeep(details, "AbilityDescription")?.GetComponent<TextMeshProUGUI>();
            if (description != null && !string.IsNullOrEmpty(e.ActiveSummary))
                description.text = RoguePirates.V4.Foundation.Core.DemoMode.Text(string.IsNullOrEmpty(e.PassiveSummary) ? e.ActiveSummary : e.ActiveSummary + "\n<size=80%>" + e.PassiveSummary + "</size>");
        }

        TextMeshProUGUI AddLockText(Transform setSail)
        {
            var go = new GameObject("LockReason", typeof(RectTransform));
            go.transform.SetParent(setSail.parent, false);
            var button = (RectTransform)setSail;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = button.anchorMin;
            rt.anchorMax = button.anchorMax;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(360f, button.rect.width), 40f);
            rt.anchoredPosition = button.anchoredPosition + new Vector2((0.5f - button.pivot.x) * button.rect.width, (1f - button.pivot.y) * button.rect.height + 8f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 24f;
            t.color = new Color(1f, 0.92f, 0.75f);
            t.raycastTarget = false;
            if (_settings != null && _settings.font != null) t.font = _settings.font;
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

        /// <summary>
        /// Playtest (4 Oct, owner: "the leaderboard text is overflowing the button"): the right-hand buttons (PvP, Leaderboard,
        /// Ledger, Raid) share one width that fits the widest label, and the column keeps its right edge. Called again when
        /// the PvP button is added.
        /// </summary>
        public static void FitSideButtons(Transform main)
        {
            var menu = main != null ? FindDeep(main, "RightMenu") as RectTransform : null;
            if (menu == null) return;
            const float padding = 34f, minWidth = 232f;
            var width = minWidth;
            foreach (Transform child in menu)
                if (child.gameObject.activeSelf)
                    foreach (var label in child.GetComponentsInChildren<TextMeshProUGUI>(true))
                    width = Mathf.Max(width, Mathf.Ceil(label.GetPreferredValues(label.text).x) + padding * 2f);
            // The art keeps its aspect, so the buttons also need their full height: the column grows with the PvP button
            // (it squeezed all four to 41 units, which also made the art narrower).
            var height = 0f;
            var count = 0;
            foreach (Transform child in menu)
            {
                if (!child.gameObject.activeSelf) continue;
                var element = child.GetComponent<LayoutElement>();
                if (element != null) { element.preferredWidth = width; height += element.preferredHeight > 0f ? element.preferredHeight : 65f; }
                else height += 65f;
                var art = child.GetComponent<Image>();
                if (art != null) art.preserveAspect = false; // stretches to the shared width (a few percent wider than the art)
                count++;
            }
            var layout = menu.GetComponent<VerticalLayoutGroup>();
            if (layout != null && count > 0) height += layout.spacing * (count - 1) + layout.padding.vertical;
            var right = menu.anchoredPosition.x + menu.sizeDelta.x * (1f - menu.pivot.x);
            menu.sizeDelta = new Vector2(width, Mathf.Max(menu.sizeDelta.y, height));
            menu.anchoredPosition = new Vector2(right - width * (1f - menu.pivot.x), menu.anchoredPosition.y);
            LayoutRebuilder.MarkLayoutForRebuild(menu);
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
            if (!_ownsBackdrops) return;
            if (_mainBackdrop != null) Destroy(_mainBackdrop);
            if (_selectBackdrop != null) Destroy(_selectBackdrop);
        }
    }
}

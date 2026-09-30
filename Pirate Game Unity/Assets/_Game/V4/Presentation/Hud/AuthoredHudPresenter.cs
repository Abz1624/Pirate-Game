using System.Collections.Generic;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The authored gameplay HUD (M6): port of V3AuthoredGameplayHud. Binds the widgets of the authored
    /// <c>Assets/UI/Prefabs/Canvas.prefab</c> instance (timer, gold, kills, captain profile with hull and XP bars, quest
    /// list, boost meter) the same way and lays them out against the minimap, but reads only the V4 views.
    /// Not ported: V3's PvP quest lines (V4 is the solo game).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredHudPresenter : MonoBehaviour
    {
        public const float DefaultTopInset = 24f;
        const float ProfileExtraUp = 16f;
        const float TimerExtraDown = 4f;
        const float QuestExtraDown = 12f;

        TextMeshProUGUI _timerText;
        TextMeshProUGUI _goldText;
        TextMeshProUGUI _killText;
        Image _portrait;
        TextMeshProUGUI _captainNameText;
        TextMeshProUGUI _levelText;
        Slider _hpSlider;
        Slider _xpSlider;
        Slider _boostSlider;
        Button _boostButton;
        RectTransform _timerRect;
        RectTransform _profileRect;
        RectTransform _questRect;
        readonly List<TextMeshProUGUI> _questLines = new List<TextMeshProUGUI>();
        readonly List<RectTransform> _questSlotRoots = new List<RectTransform>();
        readonly List<string> _visibleQuestLines = new List<string>(3);

        PlayerInfoHudArtSettings _art;
        IRunViews _views;
        MinimapPresenter _minimap;
        bool _bound;
        int _lastPortraitCaptain = -1;
        string _lastTimer;
        int _lastGold = int.MinValue, _lastKills = int.MinValue, _lastLevel = int.MinValue;

        public LoadoutHudPresenter Loadout { get; private set; }
        public bool IsBound => _bound;
        /// <summary>False while the results screen hides the profile (the demo HUD follows it).</summary>
        public bool ProfileVisible { get; private set; } = true;

        /// <summary>The authored HUD canvas: a canvas outside the legacy RunHud with the Weapon_Slot and Profile widgets.</summary>
        public static Canvas FindAuthoredCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas.name == "RunCanvas") continue; // M14.11: the run canvas (V3's RunHud sat on it)
                if (canvas.transform.Find("Weapon_Slot") == null || canvas.transform.Find("Profile") == null) continue;
                return canvas;
            }
            return null;
        }

        public void Bind(IRunViews views, MinimapPresenter minimap, IconCatalog icons)
        {
            _views = views;
            _minimap = minimap;
            BindWidgets();
            ConfigureCanvas(GetComponent<Canvas>());
            if (Loadout == null) Loadout = GetComponent<LoadoutHudPresenter>();
            if (Loadout == null) Loadout = gameObject.AddComponent<LoadoutHudPresenter>();
            Loadout.Bind(views, icons);
            ApplyScreenLayout();
        }

        /// <summary>V3 hid the profile while the results screen showed and brought it back on retry/menu.</summary>
        public void SetProfileVisible(bool visible)
        {
            ProfileVisible = visible;
            BindWidgets();
            if (_profileRect != null) _profileRect.gameObject.SetActive(visible);
        }

        static void ConfigureCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.sortingOrder < 15) canvas.sortingOrder = 15;
        }

        void LateUpdate()
        {
            if (!_bound || _views == null) return;
            ApplyScreenLayout();
            RefreshTimer();
            RefreshGold();
            RefreshKillCount();
            RefreshProfile();
            RefreshQuests();
            RefreshBoostMeter();
        }

        void BindWidgets()
        {
            if (_bound) return;
            var root = transform;
            _timerText = FindTmp(root, "TImer/Text (TMP)");
            _goldText = FindTmp(root, "Coin_Count/Text (TMP)");
            _killText = FindTmp(root, "Kill_Count/Text (TMP)");
            if (_killText == null) _killText = CreateKillCount(root);
            _captainNameText = FindTmp(root, "Profile/Name/Text (TMP)");
            _levelText = FindTmp(root, "Profile/Level_Count/Text (TMP)");
            _portrait = root.Find("Profile/Icon")?.GetComponent<Image>();
            _hpSlider = root.Find("Profile/Health_Slider")?.GetComponent<Slider>();
            _xpSlider = root.Find("Profile/XP_Slider")?.GetComponent<Slider>();
            _timerRect = root.Find("TImer") as RectTransform;
            _profileRect = root.Find("Profile") as RectTransform;
            _questRect = root.Find("Quest") as RectTransform;
            EnsureBoostHud(root);
            BindQuestSlots(root);

            _art = PlayerInfoHudArtSettings.Load();
            ConfigureSlider(_hpSlider);
            ConfigureSlider(_xpSlider);
            _bound = _timerText != null || _profileRect != null;

            var weaponRow = root.Find("Weapon_Slot");
            if (weaponRow != null) weaponRow.gameObject.SetActive(true);
        }

        /// <summary>V3 added a kill counter under the gold counter when the prefab has none (it has none today).</summary>
        static TextMeshProUGUI CreateKillCount(Transform root)
        {
            var coin = root.Find("Coin_Count");
            var killRoot = root.Find("Kill_Count");
            if (killRoot == null)
            {
                killRoot = new GameObject("Kill_Count", typeof(RectTransform)).transform;
                killRoot.SetParent(coin != null ? coin.parent : root, false);
                if (killRoot is RectTransform killRt && coin is RectTransform coinRt)
                {
                    killRt.anchorMin = coinRt.anchorMin;
                    killRt.anchorMax = coinRt.anchorMax;
                    killRt.pivot = coinRt.pivot;
                    killRt.anchoredPosition = coinRt.anchoredPosition + new Vector2(0f, -36f);
                    killRt.sizeDelta = coinRt.sizeDelta;
                }
            }

            var textGo = new GameObject("Text (TMP)", typeof(RectTransform));
            textGo.transform.SetParent(killRoot, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = RunHudTheme.Value;
            return text;
        }

        void BindQuestSlots(Transform root)
        {
            _questLines.Clear();
            _questSlotRoots.Clear();
            var content = root.Find("Quest/View/Content");
            if (content == null) return;
            var slots = new List<(int order, RectTransform root, TextMeshProUGUI line)>();
            for (var c = 0; c < content.childCount; c++)
            {
                var child = content.GetChild(c) as RectTransform;
                if (child == null || !child.name.StartsWith("Quest_Slot")) continue;
                var line = child.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                if (line == null) continue;
                slots.Add((QuestSlotOrder(child.name), child, line));
            }
            slots.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var slot in slots)
            {
                _questSlotRoots.Add(slot.root);
                _questLines.Add(slot.line);
            }
        }

        static void ConfigureSlider(Slider slider)
        {
            if (slider == null) return;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
        }

        void ApplyScreenLayout()
        {
            var inset = _minimap != null ? _minimap.cornerInset : DefaultTopInset;

            if (_profileRect != null)
            {
                _profileRect.anchorMin = new Vector2(0f, 1f);
                _profileRect.anchorMax = new Vector2(0f, 1f);
                _profileRect.pivot = new Vector2(0f, 1f);
                _profileRect.anchoredPosition = new Vector2(inset, -inset + ProfileExtraUp);
            }

            if (_timerRect != null)
            {
                _timerRect.anchorMin = new Vector2(0.5f, 1f);
                _timerRect.anchorMax = new Vector2(0.5f, 1f);
                _timerRect.pivot = new Vector2(0.5f, 1f);
                _timerRect.anchoredPosition = new Vector2(0f, -inset - TimerExtraDown);
            }

            if (_questRect != null && _minimap != null)
            {
                var mmRt = _minimap.transform as RectTransform;
                var minimapHeight = mmRt != null ? Mathf.Max(mmRt.rect.height, mmRt.sizeDelta.y) : 0f;
                _questRect.anchorMin = new Vector2(1f, 1f);
                _questRect.anchorMax = new Vector2(1f, 1f);
                _questRect.pivot = new Vector2(1f, 1f);
                _questRect.anchoredPosition = new Vector2(-inset, -(inset + minimapHeight + inset + QuestExtraDown));
            }
        }

        void RefreshTimer()
        {
            var run = _views.Run;
            if (_timerText == null || run == null || !run.Exists) return;
            var elapsed = run.ElapsedSeconds;
            var text = $"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}";
            if (text != _lastTimer) { _lastTimer = text; _timerText.text = text; }
            _timerText.color = run.IsOvertime ? RunHudTheme.TimerOvertime : RunHudTheme.Value;
        }

        void RefreshGold()
        {
            var run = _views.Run;
            if (_goldText == null || run == null || !run.Exists) return;
            if (run.Gold == _lastGold) return;
            _lastGold = run.Gold;
            _goldText.text = run.Gold.ToString("N0");
        }

        void RefreshKillCount()
        {
            var run = _views.Run;
            if (_killText == null || run == null || !run.Exists) return;
            if (run.Kills == _lastKills) return;
            _lastKills = run.Kills;
            _killText.text = $"KILLS {run.Kills:N0}";
        }

        void RefreshProfile()
        {
            var captain = _views.Captain;
            if (captain != null)
            {
                if (_portrait != null && captain.CaptainId != _lastPortraitCaptain)
                {
                    _lastPortraitCaptain = captain.CaptainId;
                    var sprite = _art != null ? _art.GetCaptainIcon((CaptainId)captain.CaptainId) : null;
                    if (sprite != null)
                    {
                        _portrait.sprite = sprite;
                        _portrait.color = Color.white;
                        _portrait.preserveAspect = true;
                    }
                }
                if (_captainNameText != null) _captainNameText.text = captain.DisplayTitle;
            }

            var progression = _views.Progression;
            if (progression != null && progression.Exists)
            {
                var level = Mathf.Max(1, progression.Level);
                if (_levelText != null && level != _lastLevel) { _lastLevel = level; _levelText.text = level.ToString(); }
                if (_xpSlider != null) _xpSlider.value = Mathf.Clamp01(progression.CurrentXp / Mathf.Max(1f, progression.XpToNext));
            }

            var ship = _views.Ship;
            if (ship != null && ship.Exists && _hpSlider != null)
                _hpSlider.value = Mathf.Clamp01(ship.Hp / Mathf.Max(1f, ship.MaxHp));
        }

        void RefreshQuests()
        {
            var stage = _views.Stage;
            if (_questLines.Count == 0 || _views.Run == null || !_views.Run.Exists || stage == null) return;
            QuestLines.Build(stage.Beacon, stage.BossSummoned, stage.BossDefeated, _visibleQuestLines);
            for (var i = 0; i < _questSlotRoots.Count; i++)
            {
                var show = i < _visibleQuestLines.Count;
                if (_questSlotRoots[i].gameObject.activeSelf != show) _questSlotRoots[i].gameObject.SetActive(show);
                if (show && _questLines[i] != null && _questLines[i].text != _visibleQuestLines[i]) _questLines[i].text = _visibleQuestLines[i];
            }
        }

        static int QuestSlotOrder(string slotName)
        {
            if (slotName == "Quest_Slot") return 0;
            if (!slotName.StartsWith("Quest_Slot (")) return 99;
            var inner = slotName.Substring("Quest_Slot (".Length).TrimEnd(')');
            return int.TryParse(inner, out var index) ? index : 99;
        }

        static TextMeshProUGUI FindTmp(Transform root, string path) => root.Find(path)?.GetComponent<TextMeshProUGUI>();

        void EnsureBoostHud(Transform root)
        {
            if (_boostSlider != null) return;
            var boostRoot = root.Find("Boost_Meter");
            _weaponRow = root.Find("Weapon_Slot") as RectTransform;
            if (boostRoot == null)
            {
                var go = new GameObject("Boost_Meter", typeof(RectTransform));
                _boostPlaceholder = rtOf(go);
                go.transform.SetParent(root, false);
                boostRoot = go.transform;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(24f, 24f);
                rt.sizeDelta = new Vector2(180f, 28f);
            }

            _boostSlider = boostRoot.GetComponent<Slider>();
            if (_boostSlider == null) _boostSlider = boostRoot.GetComponentInChildren<Slider>(true);
            if (_boostSlider == null)
            {
                var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
                sliderGo.transform.SetParent(boostRoot, false);
                var srt = sliderGo.GetComponent<RectTransform>();
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = new Vector2(-52f, 0f);
                _boostSlider = sliderGo.GetComponent<Slider>();
                _boostSlider.minValue = 0f;
                _boostSlider.maxValue = 1f;
            }

            _boostButton = boostRoot.GetComponentInChildren<Button>(true);
            if (_boostButton == null)
            {
                var btnGo = new GameObject("BoostButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(boostRoot, false);
                var brt = btnGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(1f, 0.5f);
                brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = new Vector2(44f, 28f);
                btnGo.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.7f, 0.9f);
                _boostButton = btnGo.GetComponent<Button>();
            }
            // A reload keeps nothing, but a re-bind in the same scene must not stack listeners.
            _boostButton.onClick.RemoveListener(OnBoostClicked);
            _boostButton.onClick.AddListener(OnBoostClicked);
        }

        static RectTransform rtOf(GameObject go) => go.GetComponent<RectTransform>();

        RectTransform _boostPlaceholder;
        RectTransform _weaponRow;
        const float BoostAboveRowGap = 10f;

        /// <summary>
        /// R-91 fix (approved at the M6 playtest): V3's placeholder meter sat at 24,24 from the bottom-left, on top of
        /// the first two weapon slots. The placeholder now sits just above the weapon row (which rescales with the
        /// screen); an authored meter keeps its own place.
        /// </summary>
        void PlaceBoostAboveWeaponRow()
        {
            if (_boostPlaceholder == null || _weaponRow == null) return;
            var top = _weaponRow.anchoredPosition.y + _weaponRow.rect.height * _weaponRow.localScale.y;
            var target = new Vector2(24f, top + BoostAboveRowGap);
            if (_boostPlaceholder.anchoredPosition != target) _boostPlaceholder.anchoredPosition = target;
        }

        void OnBoostClicked() => _views?.Commands?.RequestBoost();

        void RefreshBoostMeter()
        {
            PlaceBoostAboveWeaponRow();
            var ship = _views.Ship;
            if (_boostSlider == null || ship == null || !ship.Exists) return;
            _boostSlider.value = ship.BoostNormalized;
        }
    }

    /// <summary>Run objectives, completed steps and the current one only (port of V3MegabonkQuestDisplay).</summary>
    public static class QuestLines
    {
        const string ActivateLabel = "Activate the Beacon to summon the boss";
        const string DefeatLabel = "Defeat the Treasury Boss";

        public static void Build(BeaconReveal beacon, bool bossSummoned, bool bossDefeated, List<string> into)
        {
            into.Clear();
            var findLabel = beacon switch
            {
                BeaconReveal.DirectionSector => "Find the Treasury Beacon (sector hint)",
                BeaconReveal.ExactMarker => "Find the Treasury Beacon (on map)",
                _ => "Find the Treasury Beacon"
            };

            if (beacon < BeaconReveal.Discovered)
            {
                into.Add(FormatStep(findLabel, complete: false, active: true));
                return;
            }

            into.Add(FormatStep(findLabel, complete: true));
            if (!bossSummoned)
            {
                into.Add(FormatStep(ActivateLabel, complete: false, active: true));
                return;
            }

            into.Add(FormatStep(ActivateLabel, complete: true));
            into.Add(bossDefeated ? FormatStep(DefeatLabel, complete: true) : FormatStep(DefeatLabel, complete: false, active: true));
        }

        public static string FormatStep(string label, bool complete, bool active = false)
        {
            if (complete) return $"[OK] {label}";
            return active ? $"» {label}" : label;
        }
    }
}

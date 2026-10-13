using System.Collections.Generic;
using RoguePirates.UI;
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
        Image _boostFill;
        TextMeshProUGUI _boostLabel;
        Button _boostButton;
        RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController _boostController;
        int _lastBoostState = -1, _lastBoostSeconds = int.MinValue;
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
            QuestLines.Build(stage.Beacon, stage.BossSummoned, stage.BossDefeated, _visibleQuestLines, RoguePirates.V4.Foundation.Core.DemoMode.AutoBossSeconds);
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
            if (_boostFill != null) return;
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

            // Playtest (3 Oct, owner): the stand-in slider (a white bar with no fill) becomes a real meter in the Captain
            // active's style: a dark frame, a fill that drains while boosting and refills over the cooldown, and a label
            // (the key when ready, else the seconds left). The whole meter is the touch boost button.
            for (var i = boostRoot.childCount - 1; i >= 0; i--)
            {
                var child = boostRoot.GetChild(i);
                if (child.GetComponent<Slider>() != null || child.name == "BoostButton") child.gameObject.SetActive(false);
            }
            var bg = boostRoot.GetComponent<Image>();
            if (bg == null) bg = boostRoot.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.1f, 0.16f, 0.75f);
            bg.raycastTarget = true;

            var fillGo = new GameObject("BoostFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(boostRoot, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            _boostFill = fillGo.GetComponent<Image>();
            _boostFill.sprite = WhiteSprite();
            _boostFill.type = Image.Type.Filled;
            _boostFill.fillMethod = Image.FillMethod.Horizontal;
            _boostFill.fillOrigin = 0;
            _boostFill.raycastTarget = false;
            _boostFill.color = BoostReadyColor;

            var textGo = new GameObject("BoostLabel", typeof(RectTransform));
            textGo.transform.SetParent(boostRoot, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-8f, 0f);
            _boostLabel = textGo.AddComponent<TextMeshProUGUI>();
            _boostLabel.fontSize = 15f;
            _boostLabel.fontStyle = FontStyles.Bold;
            _boostLabel.alignment = TextAlignmentOptions.Center;
            _boostLabel.color = Color.white;
            _boostLabel.raycastTarget = false;
            _boostLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _boostLabel.overflowMode = TextOverflowModes.Ellipsis;
            _boostLabel.text = BoostReadyText;
            // Hackathon UI (13 Oct, owner): the kit's bar track and green fill instead of the flat blue box.
            _kitBoost = UiKit.HudBar(bg, _boostFill, "Bar_Fill_Green", _boostLabel);

            _boostButton = boostRoot.GetComponent<Button>();
            if (_boostButton == null) _boostButton = boostRoot.gameObject.AddComponent<Button>();
            _boostButton.targetGraphic = bg;
            // A reload keeps nothing, but a re-bind in the same scene must not stack listeners.
            _boostButton.onClick.RemoveListener(OnBoostClicked);
            _boostButton.onClick.AddListener(OnBoostClicked);
        }

        bool _kitBoost;
        static readonly Color BoostReadyColor = new Color(0.25f, 0.75f, 0.95f, 0.95f);
        static readonly Color BoostActiveColor = new Color(0.35f, 0.95f, 1f, 1f);
        static readonly Color BoostCoolingColor = new Color(0.45f, 0.55f, 0.65f, 0.9f);
        static Sprite _whiteSprite; // made once (a filled Image needs a sprite)

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }

        static RectTransform rtOf(GameObject go) => go.GetComponent<RectTransform>();

        RectTransform _boostPlaceholder;
        RectTransform _weaponRow;
        /// <summary>Demo: the art HUD's weapon row, which the boost meter sits above instead of the authored one.</summary>
        public RectTransform WeaponRowForBoost { set { if (value != null) _weaponRow = value; } }
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

        /// <summary>The ready boost names its key on desktop; touch devices have the BOOST button instead.</summary>
        static string BoostReadyText => RoguePirates.V4.Foundation.Platform.VirtualJoystick.TouchControlsWanted ? "Boost  ·  READY" : "Boost  ·  SHIFT";

        void OnBoostClicked() => _views?.Commands?.RequestBoost();

        void RefreshBoostMeter()
        {
            PlaceBoostAboveWeaponRow();
            var ship = _views.Ship;
            if (_boostFill == null || ship == null || !ship.Exists) return;
            if (_boostController == null && ship.Anchor != null)
                _boostController = ship.Anchor.GetComponentInParent<RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController>();
            float fill;
            Color color;
            int state, seconds;
            if (_boostController == null)
            {
                fill = ship.BoostNormalized; color = BoostActiveColor; state = 0; seconds = -1;
            }
            else if (_boostController.IsBoosting)
            {
                // Playtest (3 Oct, owner): the active time drains, then the cooldown refills.
                fill = _boostController.BoostNormalized; color = BoostActiveColor; state = 1;
                seconds = Mathf.CeilToInt(_boostController.BoostRemaining);
            }
            else if (_boostController.CooldownRemaining > 0f)
            {
                fill = _boostController.CooldownNormalized; color = BoostCoolingColor; state = 2;
                seconds = Mathf.CeilToInt(_boostController.CooldownRemaining);
            }
            else
            {
                fill = 1f; color = BoostReadyColor; state = 3; seconds = -1;
            }
            if (_kitBoost) color = state == 2 ? new Color(0.62f, 0.6f, 0.56f, 1f) : state == 1 ? new Color(0.75f, 1f, 1f, 1f) : Color.white;
            if (!Mathf.Approximately(_boostFill.fillAmount, fill)) _boostFill.fillAmount = fill;
            if (_boostFill.color != color) _boostFill.color = color;
            if (state == _lastBoostState && seconds == _lastBoostSeconds) return;
            _lastBoostState = state;
            _lastBoostSeconds = seconds;
            _boostLabel.text = state switch
            {
                1 => $"BOOST!  ·  {seconds}s",
                2 => $"Boost  ·  {seconds}s",
                _ => BoostReadyText
            };
        }
    }

    /// <summary>Run objectives, completed steps and the current one only (port of V3MegabonkQuestDisplay).</summary>
    public static class QuestLines
    {
        const string ActivateLabel = "Activate the Beacon to summon the boss";
        const string DefeatLabel = "Defeat the Treasury Boss";

        public static void Build(BeaconReveal beacon, bool bossSummoned, bool bossDefeated, List<string> into, float bossRisesAtSeconds = 0f)
        {
            into.Clear();
            // Playtest (5 Oct, review): with no Treasury Beacon the boss rises on its own; the quest says when.
            if (bossRisesAtSeconds > 0f)
            {
                var rises = $"Survive until the Barnacle King rises ({Mathf.FloorToInt(bossRisesAtSeconds / 60f)}:{Mathf.FloorToInt(bossRisesAtSeconds % 60f):00})";
                if (!bossSummoned) { into.Add(FormatStep(rises, complete: false, active: true)); return; }
                into.Add(FormatStep(rises, complete: true));
                into.Add(bossDefeated ? FormatStep(DefeatLabel, complete: true) : FormatStep(DefeatLabel, complete: false, active: true));
                return;
            }
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

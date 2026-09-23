using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3.MultiShip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Wires <c>Assets/UI/Prefabs/Canvas.prefab</c> HUD widgets to v3 run systems.</summary>
    [DisallowMultipleComponent]
    public sealed class V3AuthoredGameplayHud : MonoBehaviour
    {
        public const float DefaultTopInset = 24f;
        const float ProfileExtraUp = 16f;
        const float TimerExtraDown = 4f;
        const float QuestExtraDown = 12f;

        public static bool IsActive { get; private set; }

        TextMeshProUGUI _timerText;
        TextMeshProUGUI _goldText;
        TextMeshProUGUI _killText;
        Image _portrait;
        TextMeshProUGUI _captainNameText;
        TextMeshProUGUI _levelText;
        Slider _hpSlider;
        Slider _xpSlider;
        RectTransform _timerRect;
        RectTransform _profileRect;
        RectTransform _questRect;
        readonly List<TextMeshProUGUI> _questLines = new();
        readonly List<RectTransform> _questSlotRoots = new();

        V3PlayerInfoHudArtSettings _art;
        PlayerShipController _player;
        LevelSystem _levels;
        GameDatabase _database;
        bool _bound;
        V3CaptainId _lastPortraitCaptain = (V3CaptainId)(-1);

        public static V3AuthoredGameplayHud EnsureInScene()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas.GetComponentInParent<RunHud>() != null)
                    continue;
                if (canvas.transform.Find("Weapon_Slot") == null || canvas.transform.Find("Profile") == null)
                    continue;

                var hud = canvas.GetComponent<V3AuthoredGameplayHud>();
                if (hud == null)
                    hud = canvas.gameObject.AddComponent<V3AuthoredGameplayHud>();
                hud.BindIfNeeded();
                ConfigureCanvas(canvas);
                return hud;
            }

            return null;
        }

        public static void SetProfileVisible(bool visible)
        {
            var hud = Object.FindFirstObjectByType<V3AuthoredGameplayHud>(FindObjectsInactive.Include);
            hud?.ApplyProfileVisible(visible);
        }

        public void ApplyProfileVisible(bool visible)
        {
            BindIfNeeded();
            if (_profileRect != null)
                _profileRect.gameObject.SetActive(visible);
        }

        static void ConfigureCanvas(Canvas canvas)
        {
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.localScale == Vector3.zero)
                rt.localScale = Vector3.one;

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.sortingOrder < 15)
                canvas.sortingOrder = 15;
        }

        void OnEnable() => IsActive = true;

        void OnDisable() => IsActive = false;

        void Awake() => BindIfNeeded();

        void Start()
        {
            BindIfNeeded();
            BindSystems();
            ApplyScreenLayout();
        }

        void LateUpdate()
        {
            if (!_bound)
                return;

            ApplyScreenLayout();

            if (_player == null || _levels == null)
                BindSystems();

            RefreshTimer();
            RefreshGold();
            RefreshKillCount();
            RefreshProfile();
            RefreshQuests();
        }

        public void BindIfNeeded()
        {
            if (_bound)
                return;

            var root = transform;
            _timerText = FindTmp(root, "TImer/Text (TMP)");
            _goldText = FindTmp(root, "Coin_Count/Text (TMP)");
            _killText = FindTmp(root, "Kill_Count/Text (TMP)");
            if (_killText == null)
            {
                var killRoot = root.Find("Kill_Count");
                if (killRoot == null)
                {
                    killRoot = new GameObject("Kill_Count", typeof(RectTransform)).transform;
                    killRoot.SetParent(root.Find("Coin_Count")?.parent ?? root, false);
                    var killRt = killRoot as RectTransform;
                    if (killRt != null && root.Find("Coin_Count") is RectTransform coinRt)
                    {
                        killRt.anchorMin = coinRt.anchorMin;
                        killRt.anchorMax = coinRt.anchorMax;
                        killRt.pivot = coinRt.pivot;
                        killRt.anchoredPosition = coinRt.anchoredPosition + new Vector2(0f, -36f);
                        killRt.sizeDelta = coinRt.sizeDelta;
                    }
                }

                var killTextGo = new GameObject("Text (TMP)", typeof(RectTransform));
                killTextGo.transform.SetParent(killRoot, false);
                var killTextRt = killTextGo.GetComponent<RectTransform>();
                killTextRt.anchorMin = Vector2.zero;
                killTextRt.anchorMax = Vector2.one;
                killTextRt.offsetMin = Vector2.zero;
                killTextRt.offsetMax = Vector2.zero;
                _killText = killTextGo.AddComponent<TextMeshProUGUI>();
                _killText.fontSize = 22f;
                _killText.alignment = TextAlignmentOptions.Center;
                _killText.color = RunHudTheme.Value;
            }
            _captainNameText = FindTmp(root, "Profile/Name/Text (TMP)");
            _levelText = FindTmp(root, "Profile/Level_Count/Text (TMP)");
            _portrait = FindImage(root, "Profile/Icon");
            _hpSlider = FindSlider(root, "Profile/Health_Slider");
            _xpSlider = FindSlider(root, "Profile/XP_Slider");
            _timerRect = root.Find("TImer") as RectTransform;
            _profileRect = root.Find("Profile") as RectTransform;
            _questRect = root.Find("Quest") as RectTransform;

            _questLines.Clear();
            _questSlotRoots.Clear();
            var questContent = root.Find("Quest/View/Content");
            if (questContent != null)
            {
                var slots = new List<(int order, RectTransform root, TextMeshProUGUI line)>();
                for (var c = 0; c < questContent.childCount; c++)
                {
                    var child = questContent.GetChild(c) as RectTransform;
                    if (child == null || !child.name.StartsWith("Quest_Slot"))
                        continue;
                    var line = child.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                    if (line == null) continue;
                    slots.Add((GetQuestSlotOrder(child.name), child, line));
                }

                slots.Sort((a, b) => a.order.CompareTo(b.order));
                for (var i = 0; i < slots.Count; i++)
                {
                    _questSlotRoots.Add(slots[i].root);
                    _questLines.Add(slots[i].line);
                }
            }

            _art = V3PlayerInfoHudArtSettings.Load();
            ConfigureSliders();
            _bound = _timerText != null || _profileRect != null;

            var weaponRow = root.Find("Weapon_Slot");
            if (weaponRow != null)
                weaponRow.gameObject.SetActive(true);

            V3AuthoredBottomLoadoutHud.Ensure(transform);
        }

        void ApplyScreenLayout()
        {
            var inset = DefaultTopInset;
            var minimap = FindFirstObjectByType<V3MinimapHud>();
            if (minimap != null)
                inset = minimap.cornerInset;

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

            if (_questRect != null && minimap != null)
            {
                var mmRt = minimap.GetComponent<RectTransform>();
                var minimapHeight = mmRt != null ? Mathf.Max(mmRt.rect.height, mmRt.sizeDelta.y) : 0f;
                _questRect.anchorMin = new Vector2(1f, 1f);
                _questRect.anchorMax = new Vector2(1f, 1f);
                _questRect.pivot = new Vector2(1f, 1f);
                _questRect.anchoredPosition = new Vector2(-inset, -(inset + minimapHeight + inset + QuestExtraDown));
            }
        }

        void ConfigureSliders()
        {
            if (_hpSlider != null)
            {
                _hpSlider.interactable = false;
                _hpSlider.transition = Selectable.Transition.None;
            }

            if (_xpSlider != null)
            {
                _xpSlider.interactable = false;
                _xpSlider.transition = Selectable.Transition.None;
            }
        }

        void BindSystems()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_levels == null)
                _levels = LevelSystem.Instance;
            if (_database == null)
                _database = GameDatabaseLoader.Load();
        }

        void RefreshTimer()
        {
            if (_timerText == null || RunManager.Instance == null)
                return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            var mins = Mathf.FloorToInt(elapsed / 60f);
            var secs = Mathf.FloorToInt(elapsed % 60f);
            _timerText.text = $"{mins:00}:{secs:00}";
            _timerText.color = RunManager.Instance.IsOvertime ? RunHudTheme.TimerOvertime : RunHudTheme.Value;
        }

        void RefreshGold()
        {
            if (_goldText == null || RunManager.Instance == null)
                return;

            _goldText.text = RunManager.Instance.Gold.ToString("N0");
        }

        void RefreshKillCount()
        {
            if (_killText == null || RunManager.Instance == null)
                return;

            _killText.text = $"KILLS {RunManager.Instance.KillCount:N0}";
        }

        void RefreshProfile()
        {
            var captain = V3RunContext.IsActive ? V3RunContext.CaptainId : V3CaptainSelection.Selected;

            if (_portrait != null && captain != _lastPortraitCaptain)
            {
                _lastPortraitCaptain = captain;
                var sprite = _art != null ? _art.GetCaptainIcon(captain) : null;
                if (sprite != null)
                {
                    _portrait.sprite = sprite;
                    _portrait.color = Color.white;
                    _portrait.preserveAspect = true;
                }
            }

            if (_captainNameText != null)
                _captainNameText.text = V3CaptainCatalog.GetDisplayTitle(captain);

            if (_levels != null)
            {
                if (_levelText != null)
                    _levelText.text = Mathf.Max(1, _levels.Level).ToString();

                if (_xpSlider != null)
                {
                    var xpMax = Mathf.Max(1f, _levels.XPToNext);
                    _xpSlider.value = Mathf.Clamp01(_levels.CurrentXP / xpMax);
                }
            }

            if (_player != null && _player.Damage != null && _hpSlider != null)
            {
                var maxHp = Mathf.Max(1f, _player.Damage.MaxHP);
                _hpSlider.value = Mathf.Clamp01(_player.Damage.CurrentHP / maxHp);
            }
        }

        void RefreshQuests()
        {
            if (_questLines.Count == 0 || RunManager.Instance == null)
                return;

            if (V3RunSession.IsPvP)
            {
                ApplyQuestLines(new List<string>
                {
                    "PvP Duel",
                    V3PvPRunDirector.RivalDefeated
                        ? "[OK] Victory — rival sunk"
                        : RunManager.Instance.IsRunEnded
                            ? "Defeated"
                            : "» Sink the rival ship"
                });
                return;
            }

            V3MegabonkQuestDisplay.GatherRunState(
                out var beaconState,
                out var bossSummoned,
                out var bossDefeated);
            ApplyQuestLines(V3MegabonkQuestDisplay.BuildVisibleLines(beaconState, bossSummoned, bossDefeated));
        }

        void ApplyQuestLines(List<string> visibleLines)
        {
            for (var i = 0; i < _questSlotRoots.Count; i++)
            {
                var show = i < visibleLines.Count;
                _questSlotRoots[i].gameObject.SetActive(show);
                if (show && _questLines[i] != null)
                    _questLines[i].text = visibleLines[i];
            }
        }

        static int GetQuestSlotOrder(string slotName)
        {
            if (slotName == "Quest_Slot")
                return 0;
            if (!slotName.StartsWith("Quest_Slot ("))
                return 99;

            var inner = slotName.Substring("Quest_Slot (".Length).TrimEnd(')');
            return int.TryParse(inner, out var index) ? index : 99;
        }

        static TextMeshProUGUI FindTmp(Transform root, string path)
        {
            return root.Find(path)?.GetComponent<TextMeshProUGUI>();
        }

        static Image FindImage(Transform root, string path)
        {
            return root.Find(path)?.GetComponent<Image>();
        }

        static Slider FindSlider(Transform root, string path)
        {
            return root.Find(path)?.GetComponent<Slider>();
        }
    }
}

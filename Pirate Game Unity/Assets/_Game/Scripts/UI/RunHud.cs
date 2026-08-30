using HighSeas.Core;
using HighSeas.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public sealed class RunHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI hullText;
        [SerializeField] TextMeshProUGUI xpText;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI timerCaption;
        [SerializeField] TextMeshProUGUI goldText;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] Slider hullSlider;
        [SerializeField] Slider xpSlider;
        [SerializeField] Image hullFill;
        [SerializeField] Image xpFill;

        Player.PlayerShipController _player;
        LevelSystem _levels;

        void Awake()
        {
            EnsureWired();
        }

        void Start()
        {
            _player = FindFirstObjectByType<Player.PlayerShipController>();
            _levels = LevelSystem.Instance;
            if (RunManager.Instance != null)
                RunManager.Instance.GoldChanged += _ => Refresh();
        }

        public void RebuildUiLayout()
        {
            ApplyWiring(RunHudUiFactory.Rebuild(transform));
        }

        void EnsureWired()
        {
            UiCanvasConfigurator.ConfigureFromComponent(this);
            if (NeedsModernRebuild())
                ApplyWiring(RunHudUiFactory.Rebuild(transform));
        }

        bool NeedsModernRebuild()
        {
            var root = transform.Find(RunHudUiFactory.RootName);
            if (root == null) return true;
            if (root.Find("LeftHudPanel/HullRow") == null) return true;
            if (hullSlider == null || xpSlider == null || timerText == null) return true;
            return hullText == null || goldText == null || levelText == null;
        }

        void ApplyWiring(RunHudUiFactory.HudWiring wiring)
        {
            hullText = wiring.hullText;
            xpText = wiring.xpText;
            timerText = wiring.timerText;
            timerCaption = wiring.timerCaption;
            goldText = wiring.goldText;
            levelText = wiring.levelText;
            hullSlider = wiring.hullSlider;
            xpSlider = wiring.xpSlider;
            hullFill = wiring.hullFill;
            xpFill = wiring.xpFill;
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (RunManager.Instance == null) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            var mins = Mathf.FloorToInt(elapsed / 60f);
            var secs = Mathf.FloorToInt(elapsed % 60f);
            var overtime = RunManager.Instance.IsOvertime;

            if (timerCaption != null)
                timerCaption.text = overtime ? "DREAD TIDE" : "SURVIVE";
            if (timerText != null)
            {
                timerText.text = $"{mins:00}:{secs:00}";
                timerText.color = overtime ? RunHudTheme.TimerOvertime : RunHudTheme.Value;
            }

            if (goldText != null)
                goldText.text = RunManager.Instance.Gold.ToString("N0");

            if (_levels != null)
            {
                if (levelText != null)
                    levelText.text = $"LV {_levels.Level}";
                if (xpText != null)
                    xpText.text = $"{Mathf.FloorToInt(_levels.CurrentXP)} / {Mathf.FloorToInt(_levels.XPToNext)}";
                if (xpSlider != null)
                {
                    xpSlider.maxValue = _levels.XPToNext;
                    xpSlider.value = _levels.CurrentXP;
                }
                if (xpFill != null)
                    xpFill.color = RunHudTheme.XpFill;
            }

            if (_player != null && _player.Damage != null)
            {
                var hp = _player.Damage.CurrentHP;
                var maxHp = _player.Damage.MaxHP;
                if (hullText != null)
                    hullText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
                if (hullSlider != null)
                {
                    hullSlider.maxValue = maxHp;
                    hullSlider.value = hp;
                }

                if (hullFill != null)
                {
                    var pct = maxHp > 0f ? hp / maxHp : 1f;
                    hullFill.color = pct <= 0.25f
                        ? Color.Lerp(RunHudTheme.HullDanger, RunHudTheme.HullFill, Mathf.PingPong(Time.time * 3f, 1f))
                        : RunHudTheme.HullFill;
                }
            }
        }
    }

    public sealed class WeaponLevelStripHud : MonoBehaviour
    {
        const float SlotWidth = 112f;
        const float SlotHeight = 48f;

        [SerializeField] RectTransform stripRoot;
        [SerializeField] TextMeshProUGUI[] slotLabels;
        [SerializeField] TextMeshProUGUI[] slotLevels;
        [SerializeField] Image[] slotBorders;

        static readonly Color[] SlotAccents =
        {
            new(0.35f, 0.72f, 1f, 1f),
            new(1f, 0.72f, 0.32f, 1f),
            new(0.62f, 0.92f, 0.48f, 1f),
            new(0.82f, 0.52f, 1f, 1f),
            new(1f, 0.48f, 0.42f, 1f),
            new(0.48f, 0.88f, 0.95f, 1f)
        };

        WeaponController _weapons;
        LevelSystem _levels;
        bool _slotsBuilt;

        void Awake()
        {
            if (transform is not RectTransform)
            {
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            _weapons = FindFirstObjectByType<WeaponController>();
            _levels = LevelSystem.Instance;
            if (_levels != null)
                _levels.LoadoutChanged += Refresh;
            if (!_slotsBuilt && (slotLabels == null || slotLabels.Length == 0 || slotLabels[0] == null))
                BuildSlots(transform);
            Refresh();
        }

        void OnDestroy()
        {
            if (_levels != null)
                _levels.LoadoutChanged -= Refresh;
        }

        void Update() => Refresh();

        public void BuildSlots(Transform parent)
        {
            _slotsBuilt = true;
            stripRoot = parent as RectTransform;
            if (stripRoot == null)
                stripRoot = parent.GetComponent<RectTransform>();

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("WeaponSlot"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            slotLabels = new TextMeshProUGUI[GameConstants.DefaultWeaponSlots];
            slotLevels = new TextMeshProUGUI[GameConstants.DefaultWeaponSlots];
            slotBorders = new Image[GameConstants.DefaultWeaponSlots];

            for (var i = 0; i < GameConstants.DefaultWeaponSlots; i++)
            {
                var slot = new GameObject($"WeaponSlot{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                slot.transform.SetParent(parent, false);
                var layout = slot.GetComponent<LayoutElement>();
                layout.preferredWidth = SlotWidth;
                layout.preferredHeight = SlotHeight;

                var rt = slot.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(SlotWidth, SlotHeight);

                var accent = SlotAccents[i % SlotAccents.Length];
                var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
                border.transform.SetParent(slot.transform, false);
                var borderRt = border.GetComponent<RectTransform>();
                borderRt.sizeDelta = new Vector2(SlotWidth + 4f, SlotHeight + 4f);
                slotBorders[i] = border.GetComponent<Image>();
                slotBorders[i].color = accent;

                var fill = slot.GetComponent<Image>();
                fill.color = RunHudTheme.SlotFill;

                var name = new GameObject("Name", typeof(RectTransform));
                name.transform.SetParent(slot.transform, false);
                var nameRt = name.GetComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0f, 0.5f);
                nameRt.anchorMax = new Vector2(1f, 0.5f);
                nameRt.pivot = new Vector2(0f, 0.5f);
                nameRt.anchoredPosition = new Vector2(8f, 6f);
                nameRt.sizeDelta = new Vector2(-16f, 20f);
                slotLabels[i] = name.AddComponent<TextMeshProUGUI>();
                slotLabels[i].fontSize = 12f;
                slotLabels[i].fontStyle = FontStyles.Bold;
                slotLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
                slotLabels[i].color = RunHudTheme.Value;
                slotLabels[i].overflowMode = TextOverflowModes.Ellipsis;

                var level = new GameObject("Level", typeof(RectTransform));
                level.transform.SetParent(slot.transform, false);
                var levelRt = level.GetComponent<RectTransform>();
                levelRt.anchorMin = new Vector2(0f, 0f);
                levelRt.anchorMax = new Vector2(1f, 0f);
                levelRt.pivot = new Vector2(0f, 0f);
                levelRt.anchoredPosition = new Vector2(8f, 5f);
                levelRt.sizeDelta = new Vector2(-16f, 16f);
                slotLevels[i] = level.AddComponent<TextMeshProUGUI>();
                slotLevels[i].fontSize = 10f;
                slotLevels[i].fontStyle = FontStyles.Bold;
                slotLevels[i].alignment = TextAlignmentOptions.MidlineLeft;
                slotLevels[i].color = accent;
                slot.gameObject.SetActive(false);
            }
        }

        void Refresh()
        {
            if (_weapons == null || slotLabels == null) return;
            for (var i = 0; i < slotLabels.Length; i++)
            {
                if (slotLabels[i] == null) continue;
                var slotRoot = slotLabels[i].transform.parent;
                if (i < _weapons.Weapons.Count)
                {
                    var w = _weapons.Weapons[i];
                    slotLabels[i].text = w.def.displayName;
                    if (slotLevels != null && i < slotLevels.Length && slotLevels[i] != null)
                        slotLevels[i].text = $"LV {w.level}";
                    slotRoot.gameObject.SetActive(true);
                }
                else
                {
                    slotRoot.gameObject.SetActive(false);
                }
            }
        }
    }
}

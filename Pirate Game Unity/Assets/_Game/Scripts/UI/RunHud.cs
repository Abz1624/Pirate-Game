using HighSeas.Core;
using HighSeas.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public sealed class RunHud : MonoBehaviour
    {        [SerializeField] TextMeshProUGUI hullText;
        [SerializeField] TextMeshProUGUI xpText;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI goldText;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] Slider hullSlider;
        [SerializeField] Slider xpSlider;

        Player.PlayerShipController _player;
        LevelSystem _levels;

        void Start()
        {
            _player = FindFirstObjectByType<Player.PlayerShipController>();
            _levels = LevelSystem.Instance;
            if (RunManager.Instance != null)
                RunManager.Instance.GoldChanged += _ => Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (RunManager.Instance == null) return;
            var elapsed = RunManager.Instance.ElapsedSeconds;
            var mins = Mathf.FloorToInt(elapsed / 60f);
            var secs = Mathf.FloorToInt(elapsed % 60f);
            if (timerText != null)
            {
                timerText.text = RunManager.Instance.IsOvertime
                    ? $"DREAD TIDE {mins:00}:{secs:00}"
                    : $"{mins:00}:{secs:00}";
                timerText.color = RunManager.Instance.IsOvertime ? Color.red : Color.white;
            }
            if (goldText != null) goldText.text = $"Gold: {RunManager.Instance.Gold}";
            if (_levels != null)
            {
                if (levelText != null) levelText.text = $"Lv {_levels.Level}";
                if (xpText != null) xpText.text = $"{Mathf.FloorToInt(_levels.CurrentXP)}/{Mathf.FloorToInt(_levels.XPToNext)}";
                if (xpSlider != null)
                {
                    xpSlider.maxValue = _levels.XPToNext;
                    xpSlider.value = _levels.CurrentXP;
                }
            }
            if (_player != null && _player.Damage != null)
            {
                if (hullText != null) hullText.text = $"Hull {Mathf.CeilToInt(_player.Damage.CurrentHP)}/{Mathf.CeilToInt(_player.Damage.MaxHP)}";
                if (hullSlider != null)
                {
                    hullSlider.maxValue = _player.Damage.MaxHP;
                    hullSlider.value = _player.Damage.CurrentHP;
                }
            }
        }
    }

    public sealed class WeaponLevelStripHud : MonoBehaviour
    {
        [SerializeField] RectTransform stripRoot;
        [SerializeField] TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[2];

        WeaponController _weapons;
        LevelSystem _levels;

        void Start()
        {
            _weapons = FindFirstObjectByType<WeaponController>();
            _levels = LevelSystem.Instance;
            if (_levels != null)
                _levels.LoadoutChanged += Refresh;
            EnsureWired();
            Refresh();
        }

        void OnDestroy()
        {
            if (_levels != null)
                _levels.LoadoutChanged -= Refresh;
        }

        void Update() => Refresh();

        void EnsureWired()
        {
            if (stripRoot != null && slotLabels != null && slotLabels.Length > 0 && slotLabels[0] != null)
                return;

            stripRoot = GetComponent<RectTransform>();
            if (stripRoot == null)
                stripRoot = gameObject.AddComponent<RectTransform>();

            stripRoot.anchorMin = new Vector2(0.5f, 0f);
            stripRoot.anchorMax = new Vector2(0.5f, 0f);
            stripRoot.pivot = new Vector2(0.5f, 0f);
            stripRoot.anchoredPosition = new Vector2(0f, 24f);
            stripRoot.sizeDelta = new Vector2(420f, 48f);

            slotLabels = new TextMeshProUGUI[2];
            for (var i = 0; i < slotLabels.Length; i++)
            {
                var child = transform.Find($"WeaponSlot{i}");
                TextMeshProUGUI label;
                if (child != null)
                {
                    label = child.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    var go = new GameObject($"WeaponSlot{i}", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(180f, 36f);
                    rt.anchoredPosition = new Vector2(-95f + i * 190f, 0f);
                    label = go.AddComponent<TextMeshProUGUI>();
                    label.fontSize = 18f;
                    label.alignment = TextAlignmentOptions.Center;
                }
                slotLabels[i] = label;
            }
        }

        void Refresh()
        {
            if (_weapons == null || slotLabels == null) return;
            for (var i = 0; i < slotLabels.Length; i++)
            {
                if (slotLabels[i] == null) continue;
                if (i < _weapons.Weapons.Count)
                {
                    var w = _weapons.Weapons[i];
                    slotLabels[i].text = $"{w.def.displayName} Lv {w.level}";
                    slotLabels[i].gameObject.SetActive(true);
                }
                else
                {
                    slotLabels[i].gameObject.SetActive(false);
                }
            }
        }
    }
}

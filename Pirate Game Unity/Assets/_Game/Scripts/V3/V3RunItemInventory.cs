using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Physical run Items separate from legacy relic triggers.</summary>
    public sealed class V3RunItemInventory : MonoBehaviour
    {
        static V3RunItemInventory _instance;
        public static V3RunItemInventory Instance => _instance;

        readonly List<string> _items = new();
        readonly Dictionary<string, int> _stacks = new();

        public IReadOnlyList<string> Items => _items;

        public static V3RunItemInventory Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("V3RunItemInventory");
            _instance = go.AddComponent<V3RunItemInventory>();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public bool TryAddItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            _stacks.TryGetValue(itemId, out var count);
            _stacks[itemId] = count + 1;
            if (!_items.Contains(itemId))
                _items.Add(itemId);
            ApplyItemEffect(itemId);
            V3ToastHud.Instance?.Show(V3ContentDescriptions.FormatAcquiredItem(itemId), 4f);
            return true;
        }

        void ApplyItemEffect(string itemId)
        {
            if (RunManager.Instance == null) return;

            switch (itemId)
            {
                case "grapeshot_pouch":
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Damage, 0f, 1.06f));
                    break;
                case "double_fuse_cord":
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.AttackSpeed, 0f, 1.08f));
                    break;
                case "serrated_scabbard":
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.CritChance, 0.04f));
                    break;
                case "broadside_drum":
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Quantity, 1f));
                    break;
                default:
                    RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.Luck, 0.03f));
                    break;
            }
        }

        public int GetStackCount(string itemId) =>
            _stacks.TryGetValue(itemId, out var count) ? count : 0;

    }
}

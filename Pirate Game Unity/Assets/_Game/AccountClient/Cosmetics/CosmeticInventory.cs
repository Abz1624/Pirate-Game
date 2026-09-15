using System.Collections.Generic;

namespace RoguePirates.AccountClient.Cosmetics
{
    public sealed class CosmeticInstance
    {
        public string InstanceId { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public string Provenance { get; set; } = "default";
        public bool Equipped { get; set; }
    }

    public sealed class CosmeticInventory
    {
        readonly Dictionary<string, CosmeticInstance> _items = new();

        public void Grant(string definitionId, string provenance, string instanceId)
        {
            _items[instanceId] = new CosmeticInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Provenance = provenance
            };
        }

        public bool TryEquip(string instanceId)
        {
            if (!_items.TryGetValue(instanceId, out var item)) return false;
            foreach (var kv in _items)
                kv.Value.Equipped = kv.Key == instanceId;
            item.Equipped = true;
            return true;
        }

        public IReadOnlyCollection<CosmeticInstance> All => _items.Values;
    }
}

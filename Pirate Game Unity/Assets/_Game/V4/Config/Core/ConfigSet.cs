using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// The config assets one run uses (e.g. SaltwindDefault). Its hash is the SHA-256 of the sorted
    /// "configId:contentHash" lines, so it changes when any member's values change and not when order changes.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Config Set", fileName = "ConfigSet")]
    public sealed class ConfigSet : ScriptableObject
    {
        public string setId = "saltwind-default";
        public List<V4ConfigAsset> configs = new List<V4ConfigAsset>();

        public string ComputeHash() => HashOf(configs);

        public static string HashOf(IEnumerable<V4ConfigAsset> configs)
        {
            var lines = configs.Where(c => c != null)
                .Select(c => $"{c.configId}:{c.ComputeHash()}")
                .OrderBy(l => l, System.StringComparer.Ordinal);
            return CanonicalConfigWriter.Sha256(string.Join("\n", lines));
        }

        public T Find<T>(string configId) where T : V4ConfigAsset =>
            configs.OfType<T>().FirstOrDefault(c => c.configId == configId);

        public IEnumerable<T> All<T>() where T : V4ConfigAsset => configs.OfType<T>();
    }
}

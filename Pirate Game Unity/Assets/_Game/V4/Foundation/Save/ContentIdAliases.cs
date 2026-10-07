using System;
using System.Collections.Generic;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// V5 M8z (8 Oct, close): content ids that were renamed or replaced (the Ramming Prow became the Squid Ink Pots, its
    /// evolution Ironclad Ram became Black Tide). The list is config (UnlockConfig.contentIdAliases), registered here by
    /// the menu and the run when they load their config set, like the weapon switch-off list. The save resolves its owned
    /// ids through it when it loads (and again when the list is registered), and ownership checks, the saved 1v1 draft and
    /// anything else holding an old id resolve it here, so an old id never dangles.
    /// </summary>
    public static class ContentIdAliases
    {
        static Dictionary<string, string> _map = new Dictionary<string, string>();

        /// <summary>The config's aliases, old id to new id (null = none). A chain (a → b → c) resolves to its end.</summary>
        public static void Register(IEnumerable<KeyValuePair<string, string>> aliases)
        {
            var map = new Dictionary<string, string>();
            if (aliases != null)
                foreach (var a in aliases)
                    if (!string.IsNullOrEmpty(a.Key) && !string.IsNullOrEmpty(a.Value) && a.Key != a.Value) map[a.Key] = a.Value;
            _map = map;
        }

        public static bool Any => _map.Count > 0;

        /// <summary>The current id for <paramref name="id"/> (itself when it has no alias).</summary>
        public static string Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            for (var hops = 0; hops < 8 && _map.TryGetValue(id, out var next); hops++) id = next;
            return id;
        }

        /// <summary>The old ids that resolve to <paramref name="id"/>.</summary>
        public static IEnumerable<string> OldIdsOf(string id)
        {
            if (string.IsNullOrEmpty(id)) yield break;
            foreach (var kv in _map)
                if (Resolve(kv.Key) == id) yield return kv.Key;
        }

        /// <summary>The ids resolved, without duplicates (order kept; null = empty).</summary>
        public static string[] ResolveAll(string[] ids)
        {
            if (ids == null) return Array.Empty<string>();
            if (_map.Count == 0) return ids;
            var result = new List<string>(ids.Length);
            foreach (var id in ids)
            {
                var resolved = Resolve(id);
                if (!result.Contains(resolved)) result.Add(resolved);
            }
            return result.ToArray();
        }
    }
}

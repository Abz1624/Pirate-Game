using System;
using System.Collections.Generic;

namespace RoguePirates.AccountClient.Territory
{
    public sealed class TerritorySection
    {
        public string SectionId { get; set; } = string.Empty;
        public int RingIndex { get; set; }
        public List<string> DecorationIds { get; set; } = new();
    }

    public sealed class TerritoryState
    {
        public string OwnerAccountId { get; set; } = string.Empty;
        public List<TerritorySection> Sections { get; set; } = new();
        public int ExpansionLevel { get; set; }
    }

    public static class TerritoryService
    {
        public static TerritoryState CreateStarter(string accountId)
        {
            return new TerritoryState
            {
                OwnerAccountId = accountId,
                ExpansionLevel = 1,
                Sections = new List<TerritorySection>
                {
                    new() { SectionId = "core_harbor", RingIndex = 0 }
                }
            };
        }

        public static IReadOnlyList<TerritorySection> BuildRaidPermutation(TerritoryState owner, int raidSeed)
        {
            var copy = new List<TerritorySection>(owner.Sections);
            if (copy.Count <= 1) return copy;
            var rng = new Random(raidSeed);
            for (var i = copy.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }
    }
}

using UnityEngine;

namespace HighSeas.V3
{
    public static class V3Economy
    {
        /// <summary>ceil(min(1200, 25 × 1.16^k)) — k is number of chests already opened this run.</summary>
        public static int GetChestPrice(int paidChestCount) =>
            Mathf.CeilToInt(Mathf.Min(1200f, 25f * Mathf.Pow(1.16f, Mathf.Max(0, paidChestCount))));
    }
}

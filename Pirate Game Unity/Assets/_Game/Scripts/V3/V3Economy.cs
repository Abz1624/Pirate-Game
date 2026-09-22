using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.V3
{
    public static class V3Economy
    {
        /// <summary>Legacy v3 curve; Saltwind solo uses profile export via StageBalanceProfileContext.</summary>
        public static int GetChestPrice(int paidChestCount)
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null)
                return StageBalanceProfileContext.Runtime.CurrentChestPrice;
            return Mathf.CeilToInt(Mathf.Min(1200f, 25f * Mathf.Pow(1.16f, Mathf.Max(0, paidChestCount))));
        }
    }
}

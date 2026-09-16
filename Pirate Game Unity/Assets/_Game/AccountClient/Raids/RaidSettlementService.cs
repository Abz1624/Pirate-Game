using System;

namespace RoguePirates.AccountClient.Raids
{
    public enum RaidSettlementResult
    {
        Success,
        ShieldRaceRefund,
        AlreadySettled,
        InsufficientQuote
    }

    public sealed class RaidQuote
    {
        public string TargetAccountId { get; set; } = string.Empty;
        public int FeeGold { get; set; }
        public int ExpectedTheftGold { get; set; }
        public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    }

    public sealed class RaidSettlementService
    {
        readonly System.Collections.Generic.HashSet<string> _settledKeys = new();

        public RaidQuote CreateQuote(string targetAccountId, int exposedGold)
        {
            var theft = Math.Min(exposedGold, Math.Max(1, exposedGold / 10));
            return new RaidQuote
            {
                TargetAccountId = targetAccountId,
                FeeGold = 5,
                ExpectedTheftGold = theft
            };
        }

        public RaidSettlementResult Settle(RaidQuote quote, bool shieldActivatedBeforeSettlement)
        {
            if (shieldActivatedBeforeSettlement)
                return RaidSettlementResult.ShieldRaceRefund;
            if (!_settledKeys.Add(quote.IdempotencyKey))
                return RaidSettlementResult.AlreadySettled;
            return RaidSettlementResult.Success;
        }
    }
}

using System;
using System.Threading.Tasks;

namespace RoguePirates.PlatformAdapters.Commerce
{
    public enum PurchaseResult
    {
        Success,
        Cancelled,
        Pending,
        Restored,
        Refunded,
        Unavailable
    }

    public sealed class SandboxCommerceAdapter
    {
        public Task<PurchaseResult> PurchaseAsync(string productId, string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return Task.FromResult(PurchaseResult.Unavailable);
            return Task.FromResult(PurchaseResult.Success);
        }

        public Task<PurchaseResult> RestoreAsync(string accountId) =>
            Task.FromResult(PurchaseResult.Restored);

        public Task<PurchaseResult> RefundAsync(string receiptId) =>
            Task.FromResult(PurchaseResult.Refunded);
    }
}

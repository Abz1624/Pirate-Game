using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoguePirates.PlatformAdapters.Local
{
    public sealed class LocalPlatformAdapter
    {
        public string DeviceId { get; } = Guid.NewGuid().ToString("N");

        public Task<string> GetGuestTokenAsync() => Task.FromResult($"local_{DeviceId}");

        public Task<bool> ValidateReceiptAsync(string receiptJson) => Task.FromResult(true);

        public IReadOnlyDictionary<string, object> GetCapabilities() =>
            new Dictionary<string, object>
            {
                ["ads"] = false,
                ["iap"] = false,
                ["developmentOnly"] = true
            };
    }
}

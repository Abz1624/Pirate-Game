using System;
using System.Threading.Tasks;

namespace RoguePirates.AccountClient
{
    public sealed class AccountSession
    {
        public string AccountId { get; set; } = string.Empty;
        public string PlatformIdentity { get; set; } = "local_guest";
        public long Version { get; set; }
        public bool IsGuest { get; set; } = true;
    }

    public interface IAccountClient
    {
        Task<AccountSession> SignInGuestAsync(string deviceId);
        Task<AccountSession> LinkPlatformAsync(string accountId, string platformToken);
        Task<bool> SyncAsync(AccountSession session);
    }

    public sealed class LocalAccountClient : IAccountClient
    {
        public Task<AccountSession> SignInGuestAsync(string deviceId)
        {
            return Task.FromResult(new AccountSession
            {
                AccountId = $"guest_{deviceId}",
                PlatformIdentity = deviceId,
                IsGuest = true,
                Version = 1
            });
        }

        public Task<AccountSession> LinkPlatformAsync(string accountId, string platformToken)
        {
            return Task.FromResult(new AccountSession
            {
                AccountId = accountId,
                PlatformIdentity = platformToken,
                IsGuest = false,
                Version = 2
            });
        }

        public Task<bool> SyncAsync(AccountSession session) => Task.FromResult(true);
    }
}

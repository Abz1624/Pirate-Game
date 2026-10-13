using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoguePirates.Api.Data;
using RoguePirates.Api.Data.Migrations;
using RoguePirates.Api.Hackathon;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using Xunit;

namespace RoguePirates.Api.Tests;

/// <summary>V5 M9h: wallet sessions expire (30 days sliding, SESSION_LIFETIME_DAYS), revoke-all, and the guest token is
/// rotated when a wallet is linked to the profile (migration AddSessionExpiry).</summary>
public sealed class M9hSessionTests
{
    static async Task<string> SignInAsync(ApiHost host, Account wallet, HttpClient? client = null)
    {
        client ??= host.Anon();
        var nonce = await (await client.PostAsJsonAsync("/api/auth/wallet/nonce", new { wallet = wallet.PublicKey.Key })).ReadJson();
        var message = nonce.GetProperty("message").GetString()!;
        var sig = Encoders.Base58.EncodeData(wallet.Sign(Encoding.UTF8.GetBytes(message)));
        var res = await client.PostAsJsonAsync("/api/auth/wallet/verify", new { wallet = wallet.PublicKey.Key, signature = sig, message });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.ReadJson()).GetProperty("token").GetString()!;
    }

    static async Task<HttpStatusCode> MeAsync(ApiHost host, string token) => (await host.As(token).GetAsync("/api/profiles/me")).StatusCode;

    [Fact]
    public async Task Session_ExpiresThirtyDaysAfterItsLastUse_AndEachUseSlidesIt()
    {
        using var host = new ApiHost();
        var token = await SignInAsync(host, new Account());
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, token));

        host.Clock.Advance(TimeSpan.FromDays(20));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, token)); // used: now valid until day 50
        host.Clock.Advance(TimeSpan.FromDays(25));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, token)); // day 45: valid until day 75
        host.Clock.Advance(TimeSpan.FromDays(30));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, token)); // 30 days unused

        // a new sign-in works again
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, await SignInAsync(host, new Account())));
    }

    [Fact]
    public async Task SessionLifetime_HasAnEnvironmentOverride_AndTheExpiryIsStored()
    {
        using var host = new ApiHost(new() { ["SESSION_LIFETIME_DAYS"] = "2" });
        var token = await SignInAsync(host, new Account());
        using (var scope = host.Services.CreateScope())
        {
            var s = await scope.ServiceProvider.GetRequiredService<GameDb>().PlayerSessions.AsNoTracking().SingleAsync();
            Assert.Equal(host.Clock.UtcNow.AddDays(2), s.ExpiresAt);
        }
        host.Clock.Advance(TimeSpan.FromHours(47));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, token));
        host.Clock.Advance(TimeSpan.FromHours(49));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, token));
    }

    [Fact]
    public async Task SessionsFromBeforeM9h_ExpireThirtyDaysAfterCreation_AndThePurgeDeletesThem()
    {
        using var host = new ApiHost();
        var token = await SignInAsync(host, new Account());
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            await db.PlayerSessions.ExecuteUpdateAsync(u => u.SetProperty(s => s.ExpiresAt, (DateTime?)null)
                .SetProperty(s => s.CreatedAt, host.Clock.UtcNow.AddDays(-40)));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, token));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(1, await WalletAuthService.PurgeExpiredAsync(db, host.Clock.UtcNow, TimeSpan.FromDays(30)));
            Assert.Equal(0, await db.PlayerSessions.CountAsync());
        }
    }

    [Fact]
    public async Task RevokeAll_SignsOutEveryWalletSession()
    {
        using var host = new ApiHost();
        var wallet = new Account();
        var phone = await SignInAsync(host, wallet);
        var laptop = await SignInAsync(host, wallet);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, phone));

        var res = await (await host.As(laptop).PostAsync("/api/auth/sessions/revoke-all", null)).ReadJson();
        Assert.Equal(2, res.GetProperty("revokedSessions").GetInt32());
        Assert.True(res.GetProperty("signedOut").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, res.GetProperty("token").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, phone));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, laptop));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anon().PostAsync("/api/auth/sessions/revoke-all", null)).StatusCode);

        // signing in again works and reaches the same profile
        var again = await SignInAsync(host, wallet);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, again));
    }

    [Fact]
    public async Task RevokeAll_ForAGuest_RotatesItsTokenAndReturnsTheNewOne()
    {
        using var host = new ApiHost();
        var guest = await host.NewPlayerAsync("Anne Bonny");
        var res = await (await guest.Http.PostAsync("/api/auth/sessions/revoke-all", null)).ReadJson();
        Assert.True(res.GetProperty("guestTokenRotated").GetBoolean());
        var fresh = res.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, guest.Token));
        var me = await (await host.As(fresh).GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(guest.Id, me.GetProperty("playerId").GetGuid());
    }

    [Fact]
    public async Task LinkingAWallet_RotatesTheGuestToken_TheWalletSessionReachesTheSameProfile()
    {
        using var host = new ApiHost();
        var guest = await host.NewPlayerAsync("Calico_Jack");
        var session = await SignInAsync(host, new Account(), guest.Http);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, guest.Token));
        var me = await (await host.As(session).GetAsync("/api/profiles/me")).ReadJson();
        Assert.Equal(guest.Id, me.GetProperty("playerId").GetGuid());
        Assert.Equal("Calico_Jack", me.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Migration_RotatesTheOwnTokenOfWalletProfiles_GuestsKeepTheirs()
    {
        using var host = new ApiHost();
        var linked = await host.NewPlayerAsync("Linked");
        var plain = await host.NewPlayerAsync("Plain");
        using (var scope = host.Services.CreateScope())
        {
            // a guest that linked a wallet before M9h: the wallet is set and the guest token still works
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            await db.Players.Where(p => p.Id == linked.Id).ExecuteUpdateAsync(u => u.SetProperty(p => p.WalletAddress, new Account().PublicKey.Key));
            Assert.Equal(HttpStatusCode.OK, await MeAsync(host, linked.Token));
            await db.Database.ExecuteSqlRawAsync(AddSessionExpirySql.RotateLinkedGuestTokens);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(host, linked.Token));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(host, plain.Token));
    }
}

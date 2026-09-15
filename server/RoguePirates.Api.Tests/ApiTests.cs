using RoguePirates.Api.Services;
using Xunit;

namespace RoguePirates.Api.Tests;

public class RunLeaseTests
{
    [Fact]
    public void DuplicateIdempotency_DoesNotCreateSecondLease()
    {
        var svc = new RunLeaseService();
        var account = svc.CreateGuestAccount();
        var first = svc.AcquireLease(account, "key-1");
        var dup = svc.AcquireLease(account, "key-1");
        Assert.True(first.Success);
        Assert.True(dup.Success);
        Assert.Equal(first.LeaseId, dup.LeaseId);
    }

    [Fact]
    public void SecondLeaseWhileActive_IsRejected()
    {
        var svc = new RunLeaseService();
        var account = svc.CreateGuestAccount();
        Assert.True(svc.AcquireLease(account, "a").Success);
        var second = svc.AcquireLease(account, "b");
        Assert.False(second.Success);
    }
}

public class EconomyTests
{
    [Fact]
    public void ForgedBalance_IsRejected()
    {
        var ledger = new LedgerService();
        ledger.TryCollectProduction("acct", "dock", "once");
        Assert.True(ledger.RejectForgedBalance("acct", 999, ledger.GetTreasury("acct")));
    }

    [Fact]
    public void DuplicateCollect_IsIdempotent()
    {
        var ledger = new LedgerService();
        Assert.True(ledger.TryCollectProduction("acct", "dock", "k1"));
        Assert.True(ledger.TryCollectProduction("acct", "dock", "k1"));
        Assert.Equal(10, ledger.GetTreasury("acct"));
    }
}

public class EventRehearsalTests
{
    [Fact]
    public void PrizeGrant_CannotDoublePay()
    {
        var grants = new PrizeGrantService();
        Assert.True(grants.TryGrant("line1", "winnerA"));
        Assert.False(grants.TryGrant("line1", "winnerA"));
    }

    [Fact]
    public void LateSubmission_IsIgnored()
    {
        var standings = new EventStandingService();
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        standings.SubmitVerifiedScore("evt", "player", 100, DateTime.UtcNow, cutoff);
        Assert.Equal(0, standings.GetBest("evt", "player"));
    }
}

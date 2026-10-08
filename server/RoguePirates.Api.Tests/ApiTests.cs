using RoguePirates.Api.Services;
using Xunit;

namespace RoguePirates.Api.Tests;

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

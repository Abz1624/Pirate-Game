namespace RoguePirates.Api.Services;

public sealed class EventStandingService
{
    readonly Dictionary<string, int> _bestScores = new();

    public void SubmitVerifiedScore(string eventId, string accountId, int kills, DateTime submittedUtc, DateTime cutoffUtc)
    {
        if (submittedUtc > cutoffUtc) return;
        var key = $"{eventId}:{accountId}";
        _bestScores[key] = Math.Max(_bestScores.GetValueOrDefault(key), kills);
    }

    public int GetBest(string eventId, string accountId) =>
        _bestScores.GetValueOrDefault($"{eventId}:{accountId}");
}

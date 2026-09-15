namespace RoguePirates.Api.Services;

public sealed class PrizeGrantService
{
    readonly HashSet<string> _granted = new();

    public bool TryGrant(string manifestLineId, string winnerAccountId)
    {
        var key = $"{manifestLineId}:{winnerAccountId}";
        return _granted.Add(key);
    }
}

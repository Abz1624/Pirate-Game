namespace RoguePirates.Api.Services;

public sealed class LedgerService
{
    readonly Dictionary<string, int> _treasury = new();
    readonly HashSet<string> _processed = new();

    public int GetTreasury(string accountId) => _treasury.GetValueOrDefault(accountId);

    public bool TryCollectProduction(string accountId, string buildingId, string idempotencyKey)
    {
        var key = $"{accountId}:{buildingId}:{idempotencyKey}";
        if (!_processed.Add(key)) return true;
        _treasury[accountId] = _treasury.GetValueOrDefault(accountId) + 10;
        return true;
    }

    public bool RejectForgedBalance(string accountId, int clientClaimed, int serverAuthoritative) =>
        clientClaimed != serverAuthoritative;
}

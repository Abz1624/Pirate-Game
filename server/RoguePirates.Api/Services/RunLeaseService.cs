namespace RoguePirates.Api.Services;

public sealed class RunLeaseResult
{
    public bool Success { get; init; }
    public string LeaseId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class RunLeaseService
{
    readonly Dictionary<string, string> _activeLeases = new();
    readonly HashSet<string> _idempotency = new();
    int _guestCounter;

    public string CreateGuestAccount() => $"guest_{Interlocked.Increment(ref _guestCounter)}";

    public RunLeaseResult AcquireLease(string accountId, string idempotencyKey)
    {
        if (!_idempotency.Add(idempotencyKey))
            return new RunLeaseResult { Success = true, LeaseId = _activeLeases.GetValueOrDefault(accountId, string.Empty), Message = "duplicate" };

        if (_activeLeases.ContainsKey(accountId))
            return new RunLeaseResult { Success = false, Message = "lease_already_active" };

        var leaseId = Guid.NewGuid().ToString("N");
        _activeLeases[accountId] = leaseId;
        return new RunLeaseResult { Success = true, LeaseId = leaseId };
    }
}

namespace RoguePirates.Api.Services;

public sealed class CrewService
{
    readonly Dictionary<string, List<string>> _rosters = new();
    readonly HashSet<string> _eventLocks = new();

    public void LockMembership(string crewId) => _eventLocks.Add(crewId);

    public bool TryAddMember(string crewId, string accountId)
    {
        if (_eventLocks.Contains(crewId)) return false;
        if (!_rosters.TryGetValue(crewId, out var roster))
        {
            roster = new List<string>();
            _rosters[crewId] = roster;
        }
        if (roster.Contains(accountId)) return true;
        roster.Add(accountId);
        return true;
    }
}

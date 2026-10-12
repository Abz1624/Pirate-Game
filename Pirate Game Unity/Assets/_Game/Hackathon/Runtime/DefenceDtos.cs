using System;

namespace RoguePirates.Hackathon
{
    // Hackathon (12 Oct): island defence designs (server/README.md, "Island defence designs"), as JsonUtility reads them.

    [Serializable] public sealed class DefenceEnemy { public string kind; public int count; }

    [Serializable]
    public sealed class DefenceWindow
    {
        public int minute; public int startSecond; public DefenceEnemy[] enemies; public int cost; public int budget;
    }

    [Serializable]
    public sealed class DefenceGuardian { public string kind; public string displayName; public int minute; public int arrivesAtSecond; }

    [Serializable] public sealed class DefenceDesign { public DefenceWindow[] windows; public DefenceGuardian guardian; }

    [Serializable] public sealed class DefenceKind { public string kind; public string displayName; public string role; public int cost; }
    [Serializable] public sealed class DefenceBoss { public string kind; public string displayName; public bool isDefault; }
    [Serializable] public sealed class DefencePreset { public string id; public string name; public string description; public DefenceDesign design; }
    [Serializable] public sealed class GuardianMinuteRange { public int min; public int max; public int @default; }

    [Serializable]
    public sealed class DefenceCatalog
    {
        public int windows; public int windowSeconds; public int maxCount; public int raidTimeLimitSeconds;
        public GuardianMinuteRange guardianMinute; public DefenceKind[] kinds; public DefenceBoss[] bosses; public string defaultGuardian;
        public DefenceDesign defaultDesign; public DefencePreset[] presets;
    }

    [Serializable]
    public sealed class IslandDefenceView
    {
        public string islandId; public string islandName; public bool isMine; public bool isCustom; public string updatedAt;
        public int cannonTower; public int walls; public int budgetBonusPercent; public int[] budgets; public DefenceDesign design;
        public DefenceKind[] kinds; public DefenceBoss[] bosses; public int maxCount; public GuardianMinuteRange guardianMinute;
    }

    /// <summary>The raid's level (POST /api/raids/start, <c>defence</c>).</summary>
    [Serializable]
    public sealed class RaidDefence { public bool isCustom; public int seed; public int timeLimitSeconds; public int windowSeconds; public DefenceDesign design; }

    [Serializable] public sealed class DefenceProblem { public int minute; public string field; public string code; public string message; }
    [Serializable] public sealed class DefenceErrorBody { public string error; public string message; public DefenceProblem[] errors; }

    [Serializable] public sealed class ConquestResultRequest { public bool won; public bool guardianSunk; public int minuteReached; public int seconds; }
}

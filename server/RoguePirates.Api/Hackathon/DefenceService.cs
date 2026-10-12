using System.Text.Json;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;

namespace RoguePirates.Api.Hackathon;

// ---------- request / stored shapes (the same JSON for PUT, storage and the views' design part) ----------

public sealed record DefenceEnemy(string? Kind, int Count);
public sealed record DefenceWindow(int Minute, List<DefenceEnemy>? Enemies);
public sealed record DefenceGuardian(string? Kind, int? Minute);
public sealed record DefenceDesign(List<DefenceWindow>? Windows, DefenceGuardian? Guardian);

/// <summary>One validation problem; <c>minute</c> is null for a problem outside the windows (the guardian).</summary>
public sealed record DefenceError(int? Minute, string Field, string Code, string Message);

/// <summary>PUT /api/islands/{id}/defence with an invalid design: 400 invalid_defence with every problem listed.</summary>
public sealed class DefenceInvalidException : Exception
{
    public IReadOnlyList<DefenceError> Errors { get; }
    public DefenceInvalidException(IReadOnlyList<DefenceError> errors) : base("The defence design is not valid.") => Errors = errors;
}

/// <summary>
/// Island defence designs (owner 12 Oct): each island is a roguelike level its owner designs. A 15-minute timeline of
/// one-minute windows, each a list of {kind, count} within the window's budget, plus a guardian boss and the minute it
/// arrives. A raid conquers the island by surviving the waves and sinking the guardian. Islands without a design use
/// <see cref="DefaultDesign"/>. Numbers live in <see cref="IslandRules"/>.
/// </summary>
public sealed class DefenceService
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly GameDb _db;
    readonly IslandService _islands;
    readonly IClock _clock;

    public DefenceService(GameDb db, IslandService islands, IClock clock)
    {
        _db = db;
        _islands = islands;
        _clock = clock;
    }

    // ---------- rules ----------

    /// <summary>Validates a design against an island's budgets. Returns the normalized design (15 windows in order,
    /// zero counts dropped, kinds lower-case) and the problems found (empty when valid).</summary>
    public static (DefenceDesign Design, List<DefenceError> Errors) Validate(DefenceDesign? input, int cannonTower, int walls)
    {
        var errors = new List<DefenceError>();
        if (input == null)
        {
            errors.Add(new(null, "", "missing_design", "Send {\"windows\":[...],\"guardian\":{\"kind\",\"minute\"}}."));
            return (DefaultDesign(), errors);
        }
        var byMinute = new Dictionary<int, List<DefenceEnemy>>();
        var windows = input.Windows ?? new List<DefenceWindow>();
        if (windows.Count > IslandRules.DefenceWindows)
            errors.Add(new(null, "windows", "too_many_windows", $"At most {IslandRules.DefenceWindows} windows."));
        for (var wi = 0; wi < windows.Count; wi++)
        {
            var w = windows[wi];
            var at = $"windows[{wi}]";
            if (w == null) { errors.Add(new(null, at, "invalid_window", "A window is null.")); continue; }
            if (w.Minute < 1 || w.Minute > IslandRules.DefenceWindows)
            {
                errors.Add(new(null, at + ".minute", "invalid_minute", $"minute must be 1..{IslandRules.DefenceWindows}."));
                continue;
            }
            if (byMinute.ContainsKey(w.Minute))
            {
                errors.Add(new(w.Minute, at + ".minute", "duplicate_minute", $"Minute {w.Minute} appears more than once."));
                continue;
            }
            var list = new List<DefenceEnemy>();
            byMinute[w.Minute] = list;
            var seen = new HashSet<string>();
            var cost = 0;
            var enemies = w.Enemies ?? new List<DefenceEnemy>();
            for (var ei = 0; ei < enemies.Count; ei++)
            {
                var e = enemies[ei];
                var eat = $"{at}.enemies[{ei}]";
                var kind = (e?.Kind ?? "").Trim().ToLowerInvariant();
                if (e == null || IslandRules.FindDefenceKind(kind) is not { } def)
                {
                    errors.Add(new(w.Minute, eat + ".kind", "unknown_kind", $"Unknown enemy kind '{e?.Kind}'."));
                    continue;
                }
                if (!seen.Add(kind))
                {
                    errors.Add(new(w.Minute, eat + ".kind", "duplicate_kind", $"{kind} is listed twice in minute {w.Minute}."));
                    continue;
                }
                if (e.Count < 0 || e.Count > IslandRules.DefenceMaxCount)
                {
                    errors.Add(new(w.Minute, eat + ".count", "invalid_count", $"count must be 0..{IslandRules.DefenceMaxCount}."));
                    continue;
                }
                if (e.Count == 0) continue;
                cost += def.Cost * e.Count;
                list.Add(new DefenceEnemy(kind, e.Count));
            }
            var budget = IslandRules.DefenceBudget(w.Minute, cannonTower, walls);
            if (cost > budget)
                errors.Add(new(w.Minute, at, "over_budget", $"Minute {w.Minute} costs {cost} points; its budget is {budget}."));
        }

        var g = input.Guardian;
        var gKind = (g?.Kind ?? IslandRules.DefaultGuardian).Trim().ToLowerInvariant();
        var gMinute = g?.Minute ?? IslandRules.GuardianMinuteDefault;
        if (IslandRules.FindGuardian(gKind) == null)
            errors.Add(new(null, "guardian.kind", "unknown_guardian", "guardian.kind is one of: " + string.Join(", ", IslandRules.GuardianBosses.Select(b => b.Kind)) + "."));
        if (gMinute < IslandRules.GuardianMinuteMin || gMinute > IslandRules.GuardianMinuteMax)
            errors.Add(new(null, "guardian.minute", "invalid_guardian_minute", $"guardian.minute must be {IslandRules.GuardianMinuteMin}..{IslandRules.GuardianMinuteMax}."));

        var design = new DefenceDesign(
            Enumerable.Range(1, IslandRules.DefenceWindows).Select(m => new DefenceWindow(m, byMinute.GetValueOrDefault(m) ?? new List<DefenceEnemy>())).ToList(),
            new DefenceGuardian(gKind, gMinute));
        return (design, errors);
    }

    public static int WindowCost(DefenceWindow w) => (w.Enemies ?? new()).Sum(e => IslandRules.DefenceCost(e.Kind!) * e.Count);

    static DefenceDesign Build(Func<int, (string kind, int count)[]> window, int guardianMinute) => new(
        Enumerable.Range(1, IslandRules.DefenceWindows)
            .Select(m => new DefenceWindow(m, window(m).Where(e => e.count > 0).Select(e => new DefenceEnemy(e.kind, e.count)).ToList()))
            .ToList(),
        new DefenceGuardian(IslandRules.DefaultGuardian, guardianMinute));

    /// <summary>Spends <paramref name="share"/> of each window's base budget per kind (whole units, at most 60).</summary>
    static DefenceDesign Mix(Func<int, (string kind, double share)[]> mix, int guardianMinute) => Build(m =>
    {
        var budget = IslandRules.DefenceBudget(m);
        return mix(m).Select(x => (x.kind, Math.Min(IslandRules.DefenceMaxCount, (int)(budget * x.share) / IslandRules.DefenceCost(x.kind)))).ToArray();
    }, guardianMinute);

    /// <summary>The design of an island whose owner has not saved one: a ramp from fodder to shooters to elites, within
    /// the base budgets (so it is valid on every island), the Barnacle King at minute 5.</summary>
    public static DefenceDesign DefaultDesign() => Build(m => m switch
    {
        1 => new[] { ("deckhand_dinghy", 12), ("swordfish", 8) },
        2 => new[] { ("deckhand_dinghy", 14), ("swordfish", 10) },
        3 => new[] { ("deckhand_dinghy", 12), ("swordfish", 8), ("cutthroat_skiff", 4) },
        4 => new[] { ("deckhand_dinghy", 10), ("cutthroat_skiff", 6), ("cannonboat", 4) },
        5 => new[] { ("deckhand_dinghy", 10), ("rammer_raft", 6), ("cannonboat", 6) },
        6 => new[] { ("deckhand_dinghy", 10), ("cutthroat_skiff", 6), ("cannonboat", 4), ("bomb_boat", 4) },
        7 => new[] { ("swordfish", 10), ("hammerhead", 6), ("cannonboat", 6), ("gunner_cog", 2) },
        8 => new[] { ("deckhand_dinghy", 12), ("shark", 6), ("gunner_cog", 4), ("bomb_boat", 4) },
        9 => new[] { ("deckhand_dinghy", 12), ("cutthroat_skiff", 8), ("gunner_cog", 4), ("needle_sloop", 4) },
        10 => new[] { ("deckhand_dinghy", 10), ("hammerhead", 6), ("gunner_cog", 4), ("bomb_boat", 4), ("elite_hammerhead", 1) },
        11 => new[] { ("swordfish", 12), ("shark", 6), ("gunner_cog", 6), ("bomb_ketch", 3) },
        12 => new[] { ("deckhand_dinghy", 14), ("cannonboat", 6), ("gunner_cog", 6), ("manta_ray", 4) },
        13 => new[] { ("deckhand_dinghy", 14), ("cutthroat_skiff", 8), ("gunner_cog", 6), ("bomb_ketch", 3), ("elite_hammerhead", 1) },
        14 => new[] { ("deckhand_dinghy", 16), ("kraken_spawn", 4), ("gunner_cog", 6), ("reef_crab_miniboss", 1) },
        _ => new[] { ("deckhand_dinghy", 16), ("swordfish", 10), ("gunner_cog", 8), ("elite_hammerhead", 1), ("elite_kraken_spawn", 1) },
    }, IslandRules.GuardianMinuteDefault);

    public sealed record Preset(string Id, string Name, string Description, DefenceDesign Design);

    /// <summary>Ready-made designs the client can apply (each within the base budgets, so valid on every island).</summary>
    public static IReadOnlyList<Preset> Presets() => new[]
    {
        new Preset("swarm", "Swarm", "Wave after wave of cheap boats and swordfish; the Barnacle King at minute 8.",
            Mix(_ => new[] { ("deckhand_dinghy", 0.5), ("swordfish", 0.3), ("rammer_raft", 0.2) }, 8)),
        new Preset("gunline", "Gunline", "Cannonboats and gunner cogs that keep their distance and shoot; the Barnacle King at minute 10.",
            Mix(m => m < 4
                ? new[] { ("deckhand_dinghy", 0.4), ("cannonboat", 0.6) }
                : new[] { ("deckhand_dinghy", 0.2), ("cannonboat", 0.35), ("gunner_cog", 0.3), ("needle_sloop", 0.15) }, 10)),
        new Preset("elite_guard", "Elite guard", "Few but deadly: elites and the Reef Crab from mid-game; the Barnacle King at minute 12.",
            Mix(m => m < 5
                ? new[] { ("deckhand_dinghy", 0.5), ("cutthroat_skiff", 0.5) }
                : m < 9
                    ? new[] { ("deckhand_dinghy", 0.3), ("elite_hammerhead", 0.4), ("elite_kraken_spawn", 0.3) }
                    : new[] { ("deckhand_dinghy", 0.2), ("reef_crab_miniboss", 0.45), ("elite_hammerhead", 0.2), ("elite_kraken_spawn", 0.15) }, 12)),
    };

    // ---------- views ----------

    static object WindowView(DefenceWindow w, int cannonTower, int walls) => new
    {
        minute = w.Minute,
        startSecond = (w.Minute - 1) * IslandRules.DefenceWindowSeconds,
        enemies = (w.Enemies ?? new()).Select(e => new { kind = e.Kind, count = e.Count }),
        cost = WindowCost(w),
        budget = IslandRules.DefenceBudget(w.Minute, cannonTower, walls),
    };

    static object GuardianView(DefenceGuardian g) => new
    {
        kind = g.Kind,
        displayName = IslandRules.FindGuardian(g.Kind!)?.DisplayName,
        minute = g.Minute,
        arrivesAtSecond = (g.Minute!.Value - 1) * IslandRules.DefenceWindowSeconds,
    };

    /// <summary>The design as the client reads it (also the <c>defence</c> of POST /api/raids/start).</summary>
    public static object DesignView(DefenceDesign d, int cannonTower, int walls) => new
    {
        windows = d.Windows!.Select(w => WindowView(w, cannonTower, walls)),
        guardian = GuardianView(d.Guardian!),
    };

    static object KindView(IslandRules.DefenceKind k) => new { kind = k.Kind, displayName = k.DisplayName, role = k.Role, cost = k.Cost };
    static object BossView(IslandRules.GuardianBoss b) => new { kind = b.Kind, displayName = b.DisplayName, isDefault = b.Kind == IslandRules.DefaultGuardian };

    /// <summary>GET /api/defence/catalog.</summary>
    public static object Catalog() => new
    {
        windows = IslandRules.DefenceWindows,
        windowSeconds = IslandRules.DefenceWindowSeconds,
        maxCount = IslandRules.DefenceMaxCount,
        raidTimeLimitSeconds = IslandRules.RaidTimeLimitSeconds,
        budget = new
        {
            @base = IslandRules.DefenceBudgetBase, perMinute = IslandRules.DefenceBudgetPerMinute,
            percentPerBuildingLevel = IslandRules.DefenceBudgetPercentPerLevel, buildings = new[] { IslandRules.CannonTower, IslandRules.Walls },
            formula = "floor((20 + 6 * minute) * (100 + 15 * (cannonTower + walls)) / 100)",
            baseBudgets = Enumerable.Range(1, IslandRules.DefenceWindows).Select(m => IslandRules.DefenceBudget(m)),
        },
        guardianMinute = new { min = IslandRules.GuardianMinuteMin, max = IslandRules.GuardianMinuteMax, @default = IslandRules.GuardianMinuteDefault },
        kinds = IslandRules.DefenceKinds.Select(KindView),
        bosses = IslandRules.GuardianBosses.Select(BossView),
        defaultGuardian = IslandRules.DefaultGuardian,
        defaultDesign = DesignView(DefaultDesign(), 0, 0),
        presets = Presets().Select(p => new { id = p.Id, name = p.Name, description = p.Description, design = DesignView(p.Design, 0, 0) }),
    };

    /// <summary>The island's saved design if it is still valid for its budgets, else the default.</summary>
    public static (DefenceDesign Design, bool IsCustom) EffectiveDesign(Island island, int cannonTower, int walls)
    {
        if (string.IsNullOrEmpty(island.DefenceJson)) return (DefaultDesign(), false);
        try
        {
            var (design, errors) = Validate(JsonSerializer.Deserialize<DefenceDesign>(island.DefenceJson, Json), cannonTower, walls);
            return errors.Count == 0 ? (design, true) : (DefaultDesign(), false);
        }
        catch (JsonException) { return (DefaultDesign(), false); }
    }

    object IslandDefenceView(IslandState s, Guid? viewerId)
    {
        int ct = s.LevelOf(IslandRules.CannonTower), walls = s.LevelOf(IslandRules.Walls);
        var (design, custom) = EffectiveDesign(s.Island, ct, walls);
        return new
        {
            islandId = s.Island.Id,
            islandName = s.Island.Name,
            isMine = viewerId == s.Island.OwnerPlayerId,
            isCustom = custom,
            updatedAt = custom ? s.Island.DefenceUpdatedAt : null,
            cannonTower = ct,
            walls,
            budgetBonusPercent = IslandRules.DefenceBonusPercent(ct, walls),
            budgets = Enumerable.Range(1, IslandRules.DefenceWindows).Select(m => IslandRules.DefenceBudget(m, ct, walls)),
            design = DesignView(design, ct, walls),
            costs = IslandRules.DefenceKinds.ToDictionary(k => k.Kind, k => k.Cost),
            kinds = IslandRules.DefenceKinds.Select(KindView),
            bosses = IslandRules.GuardianBosses.Select(BossView),
            maxCount = IslandRules.DefenceMaxCount,
            guardianMinute = new { min = IslandRules.GuardianMinuteMin, max = IslandRules.GuardianMinuteMax, @default = IslandRules.GuardianMinuteDefault },
        };
    }

    // ---------- endpoints ----------

    /// <summary>GET /api/islands/{id}/defence (anyone).</summary>
    public async Task<object> GetAsync(Guid islandId, Guid? viewerId) => IslandDefenceView(await _islands.LoadAsync(islandId), viewerId);

    /// <summary>PUT /api/islands/{id}/defence (owner): validates against the island's budgets and saves.</summary>
    public async Task<object> PutAsync(Player player, Guid islandId, DefenceDesign? input)
    {
        IslandService.RequireWallet(player);
        await IslandLocks.Economy.WaitAsync();
        try
        {
            var s = await _islands.LoadAsync(islandId);
            if (s.Island.OwnerPlayerId != player.Id) throw new ApiException(403, "not_your_island", "This is not your island.");
            if (await _islands.IsListedAsync(islandId)) throw new ApiException(409, "island_listed", "The island is listed on the market; cancel the listing first.");
            var (design, errors) = Validate(input, s.LevelOf(IslandRules.CannonTower), s.LevelOf(IslandRules.Walls));
            if (errors.Count > 0) throw new DefenceInvalidException(errors);
            s.Island.DefenceJson = JsonSerializer.Serialize(design, Json);
            s.Island.DefenceUpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync();
            return IslandDefenceView(s, player.Id);
        }
        finally { IslandLocks.Economy.Release(); }
    }
}

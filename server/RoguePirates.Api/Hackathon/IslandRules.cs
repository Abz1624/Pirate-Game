using System.Collections.Concurrent;
using System.IO.Compression;

namespace RoguePirates.Api.Hackathon;

/// <summary>Process-wide write locks for the island economy (the API runs as a single instance).</summary>
public static class IslandLocks
{
    /// <summary>Claims, collects, builds and raids (RUM moves).</summary>
    public static readonly SemaphoreSlim Economy = new(1, 1);
    /// <summary>NFT mints (so a retry never mints twice at once).</summary>
    public static readonly SemaphoreSlim Mint = new(1, 1);
    /// <summary>Marketplace trades.</summary>
    public static readonly SemaphoreSlim Market = new(1, 1);
}

/// <summary>A fixed island position on the world map (0..1000 on both axes).</summary>
public sealed record IslandSlot(int Index, string Name, string Region, int X, int Y);

/// <summary>What the client uses to configure the island's AI-captain defender for a raid fight.</summary>
public sealed record DefenderConfig(
    string CaptainId, string CaptainName, int Seed, double HullMultiplier, int ExtraWaves, int WeaponLevel,
    int CannonTower, int Walls, int Difficulty, int TimeLimitSeconds);

/// <summary>
/// Hackathon island economy (plan/hackathon/PLAN.md; raid rules from V5 D19(1) as changed by D24): the vault keeps 60%
/// of the island's RUM (never raidable), a raid takes 25% of the exposed RUM (half on a partial raid), a 4-hour shield
/// after being raided, attacking does not end your own shield, a small RUM raid fee. All numbers live here.
/// </summary>
public static class IslandRules
{
    public const string RumStore = "rum_store", Vault = "vault", CannonTower = "cannon_tower", Walls = "walls";
    public static readonly string[] Kinds = { RumStore, Vault, CannonTower, Walls };

    public const int VaultPercent = 60;
    public const int RaidLootPercent = 25;
    public static readonly TimeSpan ShieldAfterRaid = TimeSpan.FromHours(4);
    public const long RaidFeeRum = 25;
    /// <summary>Results are taken this long after the start (the 16-minute fight + grace); then the raid is abandoned.</summary>
    public static readonly TimeSpan RaidTimeout = TimeSpan.FromMinutes(20);
    /// <summary>The raid fight's length: the 15 designed one-minute windows + one minute to finish the guardian.</summary>
    public const int RaidTimeLimitSeconds = 960;
    /// <summary>Reported seconds may run this far over the time limit (loading, lag).</summary>
    public const int RaidGraceSeconds = 60;
    public const string NftSymbol = "RPISL";
    public const string DefenderCaptainId = "ai_captain";
    public const string DefenderCaptainName = "Captain Marrow";

    /// <summary>RUM a wallet player earns from a voyage (POST /api/runs): kills / 10, rounded down.</summary>
    public static long RumForRun(int kills) => Math.Max(0, kills) / 10;

    /// <summary>Per building kind: the level it starts at, its max level, and the RUM cost to reach level n (index n).</summary>
    static readonly Dictionary<string, (int Start, int Max, long[] Cost)> Buildings = new()
    {
        [RumStore] = (1, 5, new long[] { 0, 0, 300, 700, 1500, 3000 }),
        [Vault] = (1, 3, new long[] { 0, 0, 400, 1000 }),
        [CannonTower] = (0, 3, new long[] { 0, 250, 600, 1400 }),
        [Walls] = (0, 3, new long[] { 0, 200, 500, 1200 }),
    };

    /// <summary>RUM per hour by RUM store level.</summary>
    static readonly int[] StoreRatePerHour = { 0, 120, 180, 260, 360, 500 };
    /// <summary>The island's RUM cap (store + vault) by RUM store level, before the vault bonus.</summary>
    static readonly long[] StoreCap = { 0, 480, 900, 1600, 2600, 4000 };
    /// <summary>The vault raises the cap: level 1 x1, level 2 x1.5, level 3 x2.</summary>
    static readonly int[] VaultCapPercent = { 100, 100, 150, 200 };

    public static bool IsKind(string kind) => Buildings.ContainsKey(kind);
    public static int StartLevel(string kind) => Buildings[kind].Start;
    public static int MaxLevel(string kind) => Buildings[kind].Max;
    /// <summary>The cost of the next level, or null at max.</summary>
    public static long? NextCost(string kind, int level) => level >= Buildings[kind].Max ? null : Buildings[kind].Cost[level + 1];
    public static int RatePerHour(int storeLevel) => StoreRatePerHour[Math.Clamp(storeLevel, 1, 5)];
    public static long Cap(int storeLevel, int vaultLevel) =>
        StoreCap[Math.Clamp(storeLevel, 1, 5)] * VaultCapPercent[Math.Clamp(vaultLevel, 0, 3)] / 100;

    /// <summary>The island level shown on the map: 1 at the start, +1 per building level bought.</summary>
    public static int IslandLevel(IReadOnlyDictionary<string, int> levels) =>
        1 + Kinds.Sum(k => Math.Max(0, levels.GetValueOrDefault(k, StartLevel(k)) - StartLevel(k)));

    /// <summary>The AI-captain defender's numbers from the island's defences.</summary>
    public static DefenderConfig Defender(int cannonTower, int walls, int seed) => new(
        DefenderCaptainId, DefenderCaptainName, seed,
        HullMultiplier: 1.0 + 0.25 * walls,          // walls: 1.0, 1.25, 1.5, 1.75
        ExtraWaves: cannonTower,                     // cannon tower: 0-3 extra enemy waves
        WeaponLevel: 1 + 2 * cannonTower,            // cannon tower: defender weapon level 1, 3, 5, 7
        CannonTower: cannonTower, Walls: walls,
        Difficulty: 1 + cannonTower + walls,         // 1..7, for the UI
        TimeLimitSeconds: RaidTimeLimitSeconds);

    // ---------- island defence designs (AddIslandDefence; owner 12 Oct: each island is a roguelike level) ----------

    /// <summary>The design is a 15-minute timeline of one-minute windows.</summary>
    public const int DefenceWindows = 15, DefenceWindowSeconds = 60;
    /// <summary>Most units of one kind in one window.</summary>
    public const int DefenceMaxCount = 60;
    /// <summary>A window's budget: (20 + 6 x minute) x (1 + 15% per cannon_tower and walls level).</summary>
    public const int DefenceBudgetBase = 20, DefenceBudgetPerMinute = 6, DefenceBudgetPercentPerLevel = 15;
    /// <summary>The guardian boss arrives at the start of this window (2..15, default 5).</summary>
    public const int GuardianMinuteMin = 2, GuardianMinuteMax = 15, GuardianMinuteDefault = 5;

    /// <summary>An enemy kind a defence window can field (ids as in the game's V4/Data Enemies and Variants).</summary>
    public sealed record DefenceKind(string Kind, string DisplayName, string Role, int Cost);

    /// <summary>The enemy cost table (points per unit). Roles: fodder 1, melee/rammers 2, shooters and bombers 3-4,
    /// heavies 4-5, elites 12, the Reef Crab miniboss 30.</summary>
    public static readonly IReadOnlyList<DefenceKind> DefenceKinds = new DefenceKind[]
    {
        new("deckhand_dinghy", "Deckhand Dinghy", "fodder", 1),
        new("swordfish", "Swordfish", "fodder", 1),
        new("rammer_raft", "Rammer Raft", "melee", 2),
        new("cutthroat_skiff", "Cutthroat Skiff", "melee", 2),
        new("hammerhead", "Hammerhead", "melee", 2),
        new("shark", "Shark", "melee", 2),
        new("cannonboat", "Cannonboat", "shooter", 3),
        new("needle_sloop", "Needle Sloop", "shooter", 3),
        new("bomb_boat", "Bomb Boat", "bomber", 3),
        new("manta_ray", "Manta Ray", "melee", 3),
        new("gunner_cog", "Gunner Cog", "shooter", 4),
        new("bomb_ketch", "Bomb Ketch", "bomber", 4),
        new("boarding_barge", "Boarding Barge", "heavy", 4),
        new("kraken_spawn", "Kraken Spawn", "heavy", 5),
        new("elite_hammerhead", "Elite Hammerhead", "elite", 12),
        new("elite_kraken_spawn", "Elite Kraken Spawn", "elite", 12),
        new("reef_crab_miniboss", "Reef Crab", "miniboss", 30),
    };

    static readonly Dictionary<string, DefenceKind> DefenceKindById = DefenceKinds.ToDictionary(k => k.Kind);
    public static DefenceKind? FindDefenceKind(string kind) => DefenceKindById.GetValueOrDefault(kind);
    public static int DefenceCost(string kind) => DefenceKindById[kind].Cost;

    /// <summary>A guardian boss (the game's bosses; barnacle_king is the Saltwind boss).</summary>
    public sealed record GuardianBoss(string Kind, string DisplayName);
    public static readonly IReadOnlyList<GuardianBoss> GuardianBosses = new GuardianBoss[] { new("barnacle_king", "Barnacle King") };
    public const string DefaultGuardian = "barnacle_king";
    public static GuardianBoss? FindGuardian(string kind) => GuardianBosses.FirstOrDefault(b => b.Kind == kind);

    /// <summary>The budget bonus in percent from the island's defence buildings.</summary>
    public static int DefenceBonusPercent(int cannonTower, int walls) =>
        DefenceBudgetPercentPerLevel * (Math.Max(0, cannonTower) + Math.Max(0, walls));

    /// <summary>Window <paramref name="minute"/>'s (1..15) budget in points.</summary>
    public static int DefenceBudget(int minute, int cannonTower = 0, int walls = 0) =>
        (DefenceBudgetBase + DefenceBudgetPerMinute * minute) * (100 + DefenceBonusPercent(cannonTower, walls)) / 100;

    /// <summary>The fixed world map: 24 named islands.</summary>
    public static readonly IReadOnlyList<IslandSlot> Slots = new IslandSlot[]
    {
        new(0, "Skull Rock", "Saltwind Reach", 120, 140),
        new(1, "Gull's Rest", "Saltwind Reach", 260, 90),
        new(2, "Barnacle Cay", "Saltwind Reach", 380, 180),
        new(3, "Driftwood Key", "Saltwind Reach", 200, 300),
        new(4, "Mermaid's Tear", "Coral Shallows", 560, 110),
        new(5, "Coral Crown", "Coral Shallows", 700, 160),
        new(6, "Pearl Atoll", "Coral Shallows", 840, 100),
        new(7, "Turtle Back", "Coral Shallows", 620, 280),
        new(8, "Smuggler's Cove", "Black Tide", 100, 480),
        new(9, "Rumrunner Isle", "Black Tide", 240, 560),
        new(10, "Dead Man's Chest", "Black Tide", 380, 440),
        new(11, "Gallows Point", "Black Tide", 140, 680),
        new(12, "Kraken's Maw", "Maelstrom Deep", 520, 500),
        new(13, "Stormbreak", "Maelstrom Deep", 660, 420),
        new(14, "Thunder Spire", "Maelstrom Deep", 800, 520),
        new(15, "Whirlpool Rock", "Maelstrom Deep", 600, 640),
        new(16, "Cutlass Reef", "Ember Isles", 300, 780),
        new(17, "Powder Keg", "Ember Isles", 440, 880),
        new(18, "Cinder Cay", "Ember Isles", 180, 900),
        new(19, "Ashen Hook", "Ember Isles", 460, 700),
        new(20, "Sunken Bell", "Golden Wake", 720, 760),
        new(21, "Doubloon Bay", "Golden Wake", 860, 680),
        new(22, "Treasure Hollow", "Golden Wake", 900, 860),
        new(23, "Lantern Isle", "Golden Wake", 640, 900),
    };

    public static string ShortWallet(string? wallet) =>
        string.IsNullOrEmpty(wallet) ? "" : wallet.Length <= 10 ? wallet : wallet[..4] + "..." + wallet[^4..];

    // ---------- placeholder NFT image (one PNG per slot, generated once) ----------

    static readonly ConcurrentDictionary<int, byte[]> Images = new();

    /// <summary>A 256x256 placeholder island picture (sea, sand, a green island tinted by slot).</summary>
    public static byte[] IslandImagePng(int slot) => Images.GetOrAdd(slot, s =>
    {
        const int size = 256;
        var hue = s * 15 % 360;
        var (gr, gg, gb) = Hsv(90 + hue / 4.0, 0.55, 0.65);
        return Png(size, size, (x, y) =>
        {
            double dx = x - size / 2.0, dy = y - size / 2.0;
            var r = Math.Sqrt(dx * dx + dy * dy);
            var a = Math.Atan2(dy, dx);
            var rad = 78 + 12 * Math.Sin(5 * a + s) + 6 * Math.Cos(3 * a - s * 0.7);
            if (r < rad - 16) return (gr, gg, gb);
            if (r < rad) return ((byte)226, (byte)204, (byte)150);
            var shade = (byte)Math.Clamp(150 - r / 3, 60, 160);
            return ((byte)20, (byte)(shade / 1.6), shade);
        });
    });

    static (byte, byte, byte) Hsv(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    static byte[] Png(int w, int h, Func<int, int, (byte r, byte g, byte b)> px)
    {
        var raw = new byte[h * (w * 3 + 1)];
        var i = 0;
        for (var y = 0; y < h; y++)
        {
            raw[i++] = 0; // filter: none
            for (var x = 0; x < w; x++)
            {
                var (r, g, b) = px(x, y);
                raw[i++] = r; raw[i++] = g; raw[i++] = b;
            }
        }
        using var idat = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BigEndian(ihdr, 0, (uint)w);
        BigEndian(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2; // 8-bit RGB
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", idat.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BigEndian(len, 0, (uint)data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32(typeBytes.Concat(data).ToArray());
        var c = new byte[4];
        BigEndian(c, 0, crc);
        s.Write(c);
    }

    static void BigEndian(byte[] b, int at, uint v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc32(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

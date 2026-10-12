using System;
using System.Globalization;

namespace RoguePirates.Hackathon
{
    /// <summary>Hackathon (12 Oct, U-3): pure helpers for the islands, raid and market screens (tested in EditMode).</summary>
    public static class IslandRules
    {
        public const long RaidFeeRum = 25;
        public const int RaidTimeLimitSeconds = 300;
        public static readonly string[] BuildingKinds = { "rum_store", "vault", "cannon_tower", "walls" };

        /// <summary>
        /// The raid's goal: sink this many of Captain Marrow's defence fleet (the voyage's enemies) within the time limit.
        /// 40 + 20 per difficulty point, times the walls' hull multiplier, rounded to 5 (difficulty 1: 60 kills).
        /// </summary>
        public static int RaidTargetKills(int difficulty, double hullMultiplier)
        {
            difficulty = Math.Max(1, Math.Min(7, difficulty));
            if (double.IsNaN(hullMultiplier) || hullMultiplier < 1.0) hullMultiplier = 1.0;
            var raw = (40 + 20 * difficulty) * Math.Min(hullMultiplier, 2.0);
            return (int)(Math.Round(raw / 5.0, MidpointRounding.AwayFromZero) * 5);
        }

        public static int RaidTargetKills(DefenderConfig d) => d == null ? 60 : RaidTargetKills(d.difficulty, d.hullMultiplier);

        /// <summary>A partial win needs half the target.</summary>
        public static int PartialKills(int target) => Math.Max(1, (target + 1) / 2);

        /// <summary>The raid's verdict: won at the target; when it ends early (sunk, time up, left), partial at half the
        /// target, else lost.</summary>
        public static (bool won, bool partial) RaidOutcome(int kills, int target)
        {
            if (kills >= target) return (true, false);
            if (kills >= PartialKills(target)) return (false, true);
            return (false, false);
        }

        /// <summary>A map position (server x right, y down on a <paramref name="w"/> x <paramref name="h"/> map) as UI
        /// anchors (0..1, y up).</summary>
        public static (float x, float y) MapAnchor(int x, int y, int w, int h)
        {
            if (w <= 0) w = 1000;
            if (h <= 0) h = 1000;
            var ax = Math.Max(0f, Math.Min(1f, x / (float)w));
            var ay = Math.Max(0f, Math.Min(1f, 1f - y / (float)h));
            return (ax, ay);
        }

        public static string BuildingName(string kind)
        {
            switch (kind)
            {
                case "rum_store": return "RUM store";
                case "vault": return "Vault";
                case "cannon_tower": return "Cannon tower";
                case "walls": return "Walls";
            }
            return kind ?? "?";
        }

        /// <summary>What the building does at <paramref name="level"/> (server/README.md table).</summary>
        public static string BuildingEffect(string kind, int level)
        {
            switch (kind)
            {
                case "rum_store":
                {
                    int[] rate = { 0, 120, 180, 260, 360, 500 };
                    int[] cap = { 0, 480, 900, 1600, 2600, 4000 };
                    var l = Math.Max(1, Math.Min(5, level));
                    return rate[l] + " RUM/h, holds " + cap[l];
                }
                case "vault":
                {
                    string[] mult = { "x1", "x1", "x1.5", "x2" };
                    return "storage " + mult[Math.Max(0, Math.Min(3, level))] + ", 60% never raidable";
                }
                case "cannon_tower":
                    return level <= 0 ? "no extra waves" : "+" + level + " defence " + (level == 1 ? "wave" : "waves") + ", guns lv " + (1 + 2 * level);
                case "walls":
                    return "defender hull x" + (1.0 + 0.25 * Math.Max(0, Math.Min(3, level))).ToString("0.##", CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        /// <summary>The RUM bar's fill (0..1).</summary>
        public static float RumFill(long total, long cap) => cap <= 0 ? 0f : Math.Max(0f, Math.Min(1f, total / (float)cap));

        /// <summary>"3h 12m" / "4m 05s" until <paramref name="untilUtc"/> (null when it is over or unknown).</summary>
        public static string TimeLeft(DateTime nowUtc, DateTime? untilUtc)
        {
            if (!untilUtc.HasValue) return null;
            var left = untilUtc.Value - nowUtc;
            if (left <= TimeSpan.Zero) return null;
            if (left.TotalHours >= 1) return (int)left.TotalHours + "h " + left.Minutes.ToString("00", CultureInfo.InvariantCulture) + "m";
            return left.Minutes + "m " + left.Seconds.ToString("00", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>"4:59" for a raid clock.</summary>
        public static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            var s = (int)Math.Ceiling(seconds);
            return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>A SOL amount typed by the player ("0.05", "0,05") in lamports; -1 when it is not a number.</summary>
        public static long ParseSol(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            text = text.Trim().Replace(',', '.');
            if (text.EndsWith("SOL", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 3).Trim();
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var sol) || sol <= 0) return -1;
            if (sol > 1_000_000m) return -1;
            return (long)Math.Round(sol * Web3Rules.LamportsPerSol);
        }

        public static string ExplorerAddress(string address) =>
            string.IsNullOrEmpty(address) ? null : "https://explorer.solana.com/address/" + address + "?cluster=devnet";

        /// <summary>The player's line for a finished raid.</summary>
        public static string ResultLine(string result, long rumLooted)
        {
            switch (result)
            {
                case "won": return "Raid won! You looted " + rumLooted + " RUM.";
                case "partial": return "Partial raid: you looted " + rumLooted + " RUM.";
                case "lost": return "Raid lost. " + HackathonRules.CaptainName + " held the island.";
                case "abandoned": return "The raid was abandoned.";
                case "started": return "Raid in progress.";
            }
            return result ?? string.Empty;
        }

        /// <summary>The marker kind of a map slot.</summary>
        public enum Marker { Free, Mine, Raidable, Shielded, Listed, Busy }

        public static Marker MarkerOf(IslandView i)
        {
            if (i == null) return Marker.Free;
            if (i.isMine) return Marker.Mine;
            if (i.listed) return Marker.Listed;
            if (i.shielded) return Marker.Shielded;
            if (i.raidable) return Marker.Raidable;
            return Marker.Busy;
        }
    }
}

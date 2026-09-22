using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RoguePirates.Simulation.Balance
{
    public static class StageBalanceProfileExportCodec
    {
        public static string ToCanonicalJson(StageBalanceProfileExport export)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            var clone = CloneForHash(export);
            clone.ContentHash = string.Empty;
            return SerializeObject(clone);
        }

        public static string ComputeContentHash(StageBalanceProfileExport export)
        {
            var canonical = ToCanonicalJson(export);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static StageBalanceProfileExport WithContentHash(StageBalanceProfileExport export)
        {
            var copy = CloneForHash(export);
            copy.ContentHash = ComputeContentHash(copy);
            return copy;
        }

        public static StageBalanceProfileExport FromCanonicalJson(string json) =>
            DeserializeObject(json);

        public static int ChestPriceForPurchaseIndex(StageBalanceProfileExport export, int completedPurchases)
        {
            if (export?.Economy == null) throw new ArgumentNullException(nameof(export));
            if (completedPurchases <= 0) return export.Economy.ChestBasePrice;
            var price = export.Economy.ChestPriceIncrementBase *
                        Math.Pow(export.Economy.ChestPriceGrowth, completedPurchases);
            return (int)Math.Ceiling(price);
        }

        public static int XpToNextLevel(StageBalanceProfileExport export, int currentLevel)
        {
            if (export?.Progression == null) throw new ArgumentNullException(nameof(export));
            if (currentLevel < 0) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            if (currentLevel == 0) return export.Progression.LevelZeroToOneXpCost;

            var l = currentLevel;
            var value = export.Progression.XpCurveMultiplier * Math.Pow(l + 1, export.Progression.XpCurveExponent);
            return (int)Math.Round(5d + value, MidpointRounding.AwayFromZero);
        }

        public static float OvertimeSpawnInterval(StageBalanceProfileExport export, float overtimeSeconds)
        {
            var ot = export.Overtime;
            var steps = (int)Math.Floor(overtimeSeconds / ot.PressureStepSeconds);
            var interval = ot.InitialSpawnIntervalSeconds * Math.Pow(ot.IntervalDecayPerStep, steps);
            return (float)Math.Max(ot.IntervalFloorSeconds, interval);
        }

        static StageBalanceProfileExport CloneForHash(StageBalanceProfileExport source)
        {
            var json = SerializeObject(source);
            return DeserializeObject(json);
        }

        static string SerializeObject(StageBalanceProfileExport export) =>
            MiniJson.Serialize(export);

        static StageBalanceProfileExport DeserializeObject(string json) =>
            MiniJson.DeserializeExport(json);

        /// <summary>Deterministic JSON without external dependencies (Simulation-safe).</summary>
        static class MiniJson
        {
            public static string Serialize(object value) => SerializeValue(value, new StringBuilder()).ToString();

            public static StageBalanceProfileExport DeserializeExport(string json)
            {
                var index = 0;
                SkipWhitespace(json, ref index);
                var result = DeserializeValue(json, ref index) as Dictionary<string, object>;
                return MapExport(result);
            }

            static StageBalanceProfileExport MapExport(Dictionary<string, object> root)
            {
                if (root == null) return null;
                var export = new StageBalanceProfileExport();
                export.ProfileId = GetString(root, "ProfileId");
                export.SchemaVersion = GetInt(root, "SchemaVersion");
                export.BalanceVersion = GetInt(root, "BalanceVersion");
                export.ContentHash = GetString(root, "ContentHash");
                export.Timing = MapTiming(GetDict(root, "Timing"));
                export.Spawn = MapSpawn(GetDict(root, "Spawn"));
                export.Rewards = MapRewards(GetDict(root, "Rewards"));
                export.Exploration = MapExploration(GetDict(root, "Exploration"));
                export.Progression = MapProgression(GetDict(root, "Progression"));
                export.Economy = MapEconomy(GetDict(root, "Economy"));
                export.Overtime = MapOvertime(GetDict(root, "Overtime"));
                export.StarterWeaponIds = GetStringList(root, "StarterWeaponIds");
                export.WeaponUnlocks = MapUnlocks(GetList(root, "WeaponUnlocks"));
                export.CaptainUnlocks = MapUnlocks(GetList(root, "CaptainUnlocks"));
                export.ScheduleProfileKeys = GetStringList(root, "ScheduleProfileKeys");
                export.ScheduleProfileSnapshots = MapSnapshots(GetList(root, "ScheduleProfileSnapshots"));
                export.WeaponStatSnapshots = MapWeaponStats(GetList(root, "WeaponStatSnapshots"));
                export.BossStatSnapshot = MapBoss(GetDict(root, "BossStatSnapshot"));
                return export;
            }

            static StageBossStatSnapshot MapBoss(Dictionary<string, object> d)
            {
                var b = new StageBossStatSnapshot();
                if (d == null) return b;
                b.BossId = GetString(d, "BossId");
                b.BaseHull = GetFloat(d, "BaseHull", b.BaseHull);
                b.BaseDamage = GetFloat(d, "BaseDamage", b.BaseDamage);
                b.MoveSpeed = GetFloat(d, "MoveSpeed", b.MoveSpeed);
                b.AttackCooldownSeconds = GetFloat(d, "AttackCooldownSeconds", b.AttackCooldownSeconds);
                b.PreferredRange = GetFloat(d, "PreferredRange", b.PreferredRange);
                return b;
            }

            static List<StageWeaponStatSnapshot> MapWeaponStats(List<object> list)
            {
                var result = new List<StageWeaponStatSnapshot>();
                if (list == null) return result;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not Dictionary<string, object> d) continue;
                    result.Add(new StageWeaponStatSnapshot
                    {
                        WeaponId = GetString(d, "WeaponId"),
                        BaseDamage = GetFloat(d, "BaseDamage", 10f),
                        BaseCooldown = GetFloat(d, "BaseCooldown", 1.2f),
                        Range = GetFloat(d, "Range", 14f),
                        ProjectileSpeed = GetFloat(d, "ProjectileSpeed", 16f),
                        MaxLevel = GetInt(d, "MaxLevel", 12),
                        DamageUpgradePerLevel = GetFloat(d, "DamageUpgradePerLevel", 0.08f)
                    });
                }

                return result;
            }

            static List<StageScheduleProfileSnapshot> MapSnapshots(List<object> list)
            {
                var result = new List<StageScheduleProfileSnapshot>();
                if (list == null) return result;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not Dictionary<string, object> d) continue;
                    result.Add(new StageScheduleProfileSnapshot
                    {
                        ProfileKey = GetString(d, "ProfileKey"),
                        EnemyId = GetString(d, "EnemyId"),
                        SpawnIntervalSeconds = GetFloat(d, "SpawnIntervalSeconds"),
                        DurationSeconds = GetFloat(d, "DurationSeconds"),
                        StartDelaySeconds = GetFloat(d, "StartDelaySeconds"),
                        TriggerTimeSeconds = GetFloat(d, "TriggerTimeSeconds"),
                        SpawnCount = GetInt(d, "SpawnCount"),
                        EliteHpMultiplier = GetFloat(d, "EliteHpMultiplier", 1f),
                        EliteDamageMultiplier = GetFloat(d, "EliteDamageMultiplier", 1f)
                    });
                }

                return result;
            }

            static StageTimingExport MapTiming(Dictionary<string, object> d)
            {
                var t = new StageTimingExport();
                if (d == null) return t;
                t.GraceSeconds = GetFloat(d, "GraceSeconds", t.GraceSeconds);
                t.StageDeadlineSeconds = GetFloat(d, "StageDeadlineSeconds", t.StageDeadlineSeconds);
                t.BeaconDirectionHintSeconds = GetFloat(d, "BeaconDirectionHintSeconds", t.BeaconDirectionHintSeconds);
                t.BeaconExactMarkerSeconds = GetFloat(d, "BeaconExactMarkerSeconds", t.BeaconExactMarkerSeconds);
                t.StormStartSeconds = GetFloat(d, "StormStartSeconds", t.StormStartSeconds);
                t.StormWarningLeadSeconds = GetFloat(d, "StormWarningLeadSeconds", t.StormWarningLeadSeconds);
                return t;
            }

            static StageSpawnExport MapSpawn(Dictionary<string, object> d)
            {
                var s = new StageSpawnExport();
                if (d == null) return s;
                s.AmbientRatePerSecond = GetFloatArray(d, "AmbientRatePerSecond", s.AmbientRatePerSecond);
                s.AmbientBandDurationSeconds = GetFloat(d, "AmbientBandDurationSeconds", s.AmbientBandDurationSeconds);
                s.MaxActiveEnemies = GetInt(d, "MaxActiveEnemies", s.MaxActiveEnemies);
                s.ReservedEncounterSlots = GetInt(d, "ReservedEncounterSlots", s.ReservedEncounterSlots);
                s.BossAmbientRateMultiplier = GetFloat(d, "BossAmbientRateMultiplier", s.BossAmbientRateMultiplier);
                s.ScheduledEliteSecondsA = GetFloat(d, "ScheduledEliteSecondsA", s.ScheduledEliteSecondsA);
                s.ScheduledEliteSecondsB = GetFloat(d, "ScheduledEliteSecondsB", s.ScheduledEliteSecondsB);
                s.SwarmStartSecondsA = GetFloat(d, "SwarmStartSecondsA", s.SwarmStartSecondsA);
                s.SwarmStartSecondsB = GetFloat(d, "SwarmStartSecondsB", s.SwarmStartSecondsB);
                s.SwarmDurationSeconds = GetFloat(d, "SwarmDurationSeconds", s.SwarmDurationSeconds);
                s.EventWarningSeconds = GetFloat(d, "EventWarningSeconds", s.EventWarningSeconds);
                s.MaxSimultaneousCannonboats = GetInt(d, "MaxSimultaneousCannonboats", s.MaxSimultaneousCannonboats);
                s.MaxSimultaneousAreaDenial = GetInt(d, "MaxSimultaneousAreaDenial", s.MaxSimultaneousAreaDenial);
                s.MaxSimultaneousChargeWindups = GetInt(d, "MaxSimultaneousChargeWindups", s.MaxSimultaneousChargeWindups);
                s.MinChargeWarningSeconds = GetFloat(d, "MinChargeWarningSeconds", s.MinChargeWarningSeconds);
                s.MinExplosiveWarningSeconds = GetFloat(d, "MinExplosiveWarningSeconds", s.MinExplosiveWarningSeconds);
                s.SpawnRetryIntervalSeconds = GetFloat(d, "SpawnRetryIntervalSeconds", s.SpawnRetryIntervalSeconds);
                s.RequiredEncounterTimeoutSeconds = GetFloat(d, "RequiredEncounterTimeoutSeconds", s.RequiredEncounterTimeoutSeconds);
                s.SwarmIntensityMultiplier = GetFloat(d, "SwarmIntensityMultiplier", s.SwarmIntensityMultiplier);
                return s;
            }

            static StageRewardExport MapRewards(Dictionary<string, object> d)
            {
                var r = new StageRewardExport();
                if (d == null) return r;
                r.OrdinaryEnemyGoldChance = GetFloat(d, "OrdinaryEnemyGoldChance", r.OrdinaryEnemyGoldChance);
                r.OrdinaryEnemyGoldAmount = GetInt(d, "OrdinaryEnemyGoldAmount", r.OrdinaryEnemyGoldAmount);
                r.SpecialistEnemyGoldAmount = GetInt(d, "SpecialistEnemyGoldAmount", r.SpecialistEnemyGoldAmount);
                r.ScheduledEliteGoldAmount = GetInt(d, "ScheduledEliteGoldAmount", r.ScheduledEliteGoldAmount);
                r.RandomPowerupChancePerKill = GetFloat(d, "RandomPowerupChancePerKill", r.RandomPowerupChancePerKill);
                r.RandomPowerupMinIntervalSeconds = GetFloat(d, "RandomPowerupMinIntervalSeconds", r.RandomPowerupMinIntervalSeconds);
                r.RepairSuppliesMaxHullFraction = GetFloat(d, "RepairSuppliesMaxHullFraction", r.RepairSuppliesMaxHullFraction);
                r.PowderRushAttackSpeedBonus = GetFloat(d, "PowderRushAttackSpeedBonus", r.PowderRushAttackSpeedBonus);
                r.PowderRushDurationSeconds = GetFloat(d, "PowderRushDurationSeconds", r.PowderRushDurationSeconds);
                r.DeadCalmSlowFraction = GetFloat(d, "DeadCalmSlowFraction", r.DeadCalmSlowFraction);
                r.DeadCalmDurationSeconds = GetFloat(d, "DeadCalmDurationSeconds", r.DeadCalmDurationSeconds);
                r.RepairWreckMaxHullFraction = GetFloat(d, "RepairWreckMaxHullFraction", r.RepairWreckMaxHullFraction);
                r.PowderCacheBonusGold = GetInt(d, "PowderCacheBonusGold", r.PowderCacheBonusGold);
                r.SignalBuoyOccupancySeconds = GetFloat(d, "SignalBuoyOccupancySeconds", r.SignalBuoyOccupancySeconds);
                return r;
            }

            static StageExplorationExport MapExploration(Dictionary<string, object> d)
            {
                var e = new StageExplorationExport();
                if (d == null) return e;
                e.SalvageChestCount = GetInt(d, "SalvageChestCount", e.SalvageChestCount);
                e.DisableGoldTriggeredChestSpawn = GetBool(d, "DisableGoldTriggeredChestSpawn", e.DisableGoldTriggeredChestSpawn);
                e.DisableAutomaticRouteEvents = GetBool(d, "DisableAutomaticRouteEvents", e.DisableAutomaticRouteEvents);
                e.ExplosiveBarrelFuseSeconds = GetFloat(d, "ExplosiveBarrelFuseSeconds", e.ExplosiveBarrelFuseSeconds);
                e.ExplosiveBarrelRadius = GetFloat(d, "ExplosiveBarrelRadius", e.ExplosiveBarrelRadius);
                e.ExplosiveBarrelDamage = GetInt(d, "ExplosiveBarrelDamage", e.ExplosiveBarrelDamage);
                e.ExplosiveBarrelCount = GetInt(d, "ExplosiveBarrelCount", e.ExplosiveBarrelCount);
                e.FirstChestMaxTravelSeconds = GetFloat(d, "FirstChestMaxTravelSeconds", e.FirstChestMaxTravelSeconds);
                e.RepresentativeCruiseSpeed = GetFloat(d, "RepresentativeCruiseSpeed", e.RepresentativeCruiseSpeed);
                return e;
            }

            static StageProgressionExport MapProgression(Dictionary<string, object> d)
            {
                var p = new StageProgressionExport();
                if (d == null) return p;
                p.WeaponSlotCap = GetInt(d, "WeaponSlotCap", p.WeaponSlotCap);
                p.LogbookSlotCap = GetInt(d, "LogbookSlotCap", p.LogbookSlotCap);
                p.LevelUpChoiceCount = GetInt(d, "LevelUpChoiceCount", p.LevelUpChoiceCount);
                p.LevelZeroToOneXpCost = GetInt(d, "LevelZeroToOneXpCost", p.LevelZeroToOneXpCost);
                p.XpCurveMultiplier = GetDouble(d, "XpCurveMultiplier", p.XpCurveMultiplier);
                p.XpCurveExponent = GetDouble(d, "XpCurveExponent", p.XpCurveExponent);
                p.NewWeaponOfferProtectionLevels = GetIntArray(d, "NewWeaponOfferProtectionLevels", p.NewWeaponOfferProtectionLevels);
                return p;
            }

            static StageEconomyExport MapEconomy(Dictionary<string, object> d)
            {
                var e = new StageEconomyExport();
                if (d == null) return e;
                e.ChestBasePrice = GetInt(d, "ChestBasePrice", e.ChestBasePrice);
                e.ChestPriceGrowth = GetFloat(d, "ChestPriceGrowth", e.ChestPriceGrowth);
                e.ChestPriceIncrementBase = GetInt(d, "ChestPriceIncrementBase", e.ChestPriceIncrementBase);
                e.MarksPerQualifyingMinute = GetInt(d, "MarksPerQualifyingMinute", e.MarksPerQualifyingMinute);
                e.MarksPerQualifyingMinuteMinKills = GetInt(d, "MarksPerQualifyingMinuteMinKills", e.MarksPerQualifyingMinuteMinKills);
                e.MarksPerSpecialPoi = GetInt(d, "MarksPerSpecialPoi", e.MarksPerSpecialPoi);
                e.MarksPerScheduledElite = GetInt(d, "MarksPerScheduledElite", e.MarksPerScheduledElite);
                e.MarksBossVictory = GetInt(d, "MarksBossVictory", e.MarksBossVictory);
                e.MarksPerOvertimeMinute = GetInt(d, "MarksPerOvertimeMinute", e.MarksPerOvertimeMinute);
                e.MarksOvertimeCap = GetInt(d, "MarksOvertimeCap", e.MarksOvertimeCap);
                e.RunGoldExpiresAtRunEnd = GetBool(d, "RunGoldExpiresAtRunEnd", e.RunGoldExpiresAtRunEnd);
                e.BankRunGoldToDoubloons = GetBool(d, "BankRunGoldToDoubloons", e.BankRunGoldToDoubloons);
                return e;
            }

            static StageOvertimeExport MapOvertime(Dictionary<string, object> d)
            {
                var o = new StageOvertimeExport();
                if (d == null) return o;
                o.InitialSpawnIntervalSeconds = GetFloat(d, "InitialSpawnIntervalSeconds", o.InitialSpawnIntervalSeconds);
                o.IntervalFloorSeconds = GetFloat(d, "IntervalFloorSeconds", o.IntervalFloorSeconds);
                o.IntervalDecayPerStep = GetFloat(d, "IntervalDecayPerStep", o.IntervalDecayPerStep);
                o.PressureStepSeconds = GetFloat(d, "PressureStepSeconds", o.PressureStepSeconds);
                o.HpMultiplierBase = GetFloat(d, "HpMultiplierBase", o.HpMultiplierBase);
                o.HpMultiplierExponentPerMinute = GetFloat(d, "HpMultiplierExponentPerMinute", o.HpMultiplierExponentPerMinute);
                o.DamageBonusPerMinute = GetFloat(d, "DamageBonusPerMinute", o.DamageBonusPerMinute);
                o.PursuerWeightFirstTwoMinutes = GetFloat(d, "PursuerWeightFirstTwoMinutes", o.PursuerWeightFirstTwoMinutes);
                o.HarrierWeightFirstTwoMinutes = GetFloat(d, "HarrierWeightFirstTwoMinutes", o.HarrierWeightFirstTwoMinutes);
                o.PursuerWeightAfterTwoMinutes = GetFloat(d, "PursuerWeightAfterTwoMinutes", o.PursuerWeightAfterTwoMinutes);
                o.HarrierWeightAfterTwoMinutes = GetFloat(d, "HarrierWeightAfterTwoMinutes", o.HarrierWeightAfterTwoMinutes);
                o.StormSafeRegionStartFraction = GetFloat(d, "StormSafeRegionStartFraction", o.StormSafeRegionStartFraction);
                o.StormSafeRegionEndFraction = GetFloat(d, "StormSafeRegionEndFraction", o.StormSafeRegionEndFraction);
                o.StormContractionStartSeconds = GetFloat(d, "StormContractionStartSeconds", o.StormContractionStartSeconds);
                o.StormContractionEndSeconds = GetFloat(d, "StormContractionEndSeconds", o.StormContractionEndSeconds);
                o.StormOutsideDamagePerSecond = GetFloat(d, "StormOutsideDamagePerSecond", o.StormOutsideDamagePerSecond);
                return o;
            }

            static List<StageUnlockExport> MapUnlocks(List<object> list)
            {
                var result = new List<StageUnlockExport>();
                if (list == null) return result;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is not Dictionary<string, object> d) continue;
                    result.Add(new StageUnlockExport
                    {
                        ContentId = GetString(d, "ContentId"),
                        MarksCost = GetInt(d, "MarksCost")
                    });
                }
                return result;
            }

            static StringBuilder SerializeValue(object value, StringBuilder builder)
            {
                switch (value)
                {
                    case null:
                        return builder.Append("null");
                    case string s:
                        return builder.Append('"').Append(Escape(s)).Append('"');
                    case bool b:
                        return builder.Append(b ? "true" : "false");
                    case int or long or short or byte:
                        return builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    case float f:
                        return builder.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    case double d:
                        return builder.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    case IEnumerable<string> stringList:
                        builder.Append('[');
                        var first = true;
                        foreach (var item in stringList)
                        {
                            if (!first) builder.Append(',');
                            first = false;
                            SerializeValue(item, builder);
                        }
                        return builder.Append(']');
                    case int[] intArray:
                        builder.Append('[');
                        for (var i = 0; i < intArray.Length; i++)
                        {
                            if (i > 0) builder.Append(',');
                            builder.Append(intArray[i].ToString(CultureInfo.InvariantCulture));
                        }
                        return builder.Append(']');
                    case float[] floatArray:
                        builder.Append('[');
                        for (var i = 0; i < floatArray.Length; i++)
                        {
                            if (i > 0) builder.Append(',');
                            builder.Append(floatArray[i].ToString("R", CultureInfo.InvariantCulture));
                        }
                        return builder.Append(']');
                    case IEnumerable<StageUnlockExport> unlocks:
                        builder.Append('[');
                        var uFirst = true;
                        foreach (var unlock in unlocks)
                        {
                            if (!uFirst) builder.Append(',');
                            uFirst = false;
                            SerializeObject(unlock, builder);
                        }
                        return builder.Append(']');
                    case IEnumerable<StageScheduleProfileSnapshot> scheduleSnapshots:
                        builder.Append('[');
                        var sFirst = true;
                        foreach (var snap in scheduleSnapshots)
                        {
                            if (!sFirst) builder.Append(',');
                            sFirst = false;
                            SerializeObject(snap, builder);
                        }
                        return builder.Append(']');
                    case IEnumerable<StageWeaponStatSnapshot> weaponSnapshots:
                        builder.Append('[');
                        var wFirst = true;
                        foreach (var snap in weaponSnapshots)
                        {
                            if (!wFirst) builder.Append(',');
                            wFirst = false;
                            SerializeObject(snap, builder);
                        }
                        return builder.Append(']');
                    default:
                        return SerializeObject(value, builder);
                }
            }

            static StringBuilder SerializeObject(object value, StringBuilder builder)
            {
                builder.Append('{');
                var fields = value.GetType().GetFields();
                Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                for (var i = 0; i < fields.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append('"').Append(fields[i].Name).Append('"').Append(':');
                    SerializeValue(fields[i].GetValue(value), builder);
                }
                return builder.Append('}');
            }

            static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

            static object DeserializeValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;
                var c = json[index];
                if (c == '{') return DeserializeObject(json, ref index);
                if (c == '[') return DeserializeArray(json, ref index);
                if (c == '"') return DeserializeString(json, ref index);
                if (c == 't' || c == 'f') return DeserializeBool(json, ref index);
                if (c == 'n') return DeserializeNull(json, ref index);
                return DeserializeNumber(json, ref index);
            }

            static Dictionary<string, object> DeserializeObject(string json, ref int index)
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                index++;
                SkipWhitespace(json, ref index);
                if (json[index] == '}') { index++; return dict; }
                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    var key = DeserializeString(json, ref index);
                    SkipWhitespace(json, ref index);
                    index++;
                    SkipWhitespace(json, ref index);
                    dict[key] = DeserializeValue(json, ref index);
                    SkipWhitespace(json, ref index);
                    if (json[index] == '}') { index++; break; }
                    index++;
                }
                return dict;
            }

            static List<object> DeserializeArray(string json, ref int index)
            {
                var list = new List<object>();
                index++;
                SkipWhitespace(json, ref index);
                if (json[index] == ']') { index++; return list; }
                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    list.Add(DeserializeValue(json, ref index));
                    SkipWhitespace(json, ref index);
                    if (json[index] == ']') { index++; break; }
                    index++;
                }
                return list;
            }

            static string DeserializeString(string json, ref int index)
            {
                index++;
                var start = index;
                var builder = new StringBuilder();
                while (index < json.Length)
                {
                    var c = json[index++];
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        var esc = json[index++];
                        builder.Append(esc switch
                        {
                            '"' => '"',
                            '\\' => '\\',
                            _ => esc
                        });
                    }
                    else builder.Append(c);
                }
                return builder.ToString();
            }

            static bool DeserializeBool(string json, ref int index)
            {
                if (json.AsSpan(index, 4).SequenceEqual("true".AsSpan())) { index += 4; return true; }
                index += 5;
                return false;
            }

            static object DeserializeNull(string json, ref int index)
            {
                index += 4;
                return null;
            }

            static object DeserializeNumber(string json, ref int index)
            {
                var start = index;
                while (index < json.Length && "0123456789+-eE.".IndexOf(json[index]) >= 0) index++;
                var token = json.Substring(start, index - start);
                if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
                    return double.Parse(token, CultureInfo.InvariantCulture);
                return int.Parse(token, CultureInfo.InvariantCulture);
            }

            static void SkipWhitespace(string json, ref int index)
            {
                while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            }

            static Dictionary<string, object> GetDict(Dictionary<string, object> root, string key) =>
                root.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;

            static List<object> GetList(Dictionary<string, object> root, string key) =>
                root.TryGetValue(key, out var value) ? value as List<object> : null;

            static string GetString(Dictionary<string, object> dict, string key) =>
                dict != null && dict.TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;

            static int GetInt(Dictionary<string, object> dict, string key, int fallback = 0) =>
                dict != null && dict.TryGetValue(key, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;

            static float GetFloat(Dictionary<string, object> dict, string key, float fallback = 0f) =>
                dict != null && dict.TryGetValue(key, out var value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;

            static double GetDouble(Dictionary<string, object> dict, string key, double fallback = 0d) =>
                dict != null && dict.TryGetValue(key, out var value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : fallback;

            static bool GetBool(Dictionary<string, object> dict, string key, bool fallback = false) =>
                dict != null && dict.TryGetValue(key, out var value) && value is bool b ? b : fallback;

            static List<string> GetStringList(Dictionary<string, object> root, string key)
            {
                var list = GetList(root, key);
                var result = new List<string>();
                if (list == null) return result;
                for (var i = 0; i < list.Count; i++)
                    if (list[i] is string s) result.Add(s);
                return result;
            }

            static float[] GetFloatArray(Dictionary<string, object> dict, string key, float[] fallback)
            {
                var list = GetList(dict, key);
                if (list == null || list.Count == 0) return fallback;
                var result = new float[list.Count];
                for (var i = 0; i < list.Count; i++)
                    result[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
                return result;
            }

            static int[] GetIntArray(Dictionary<string, object> dict, string key, int[] fallback)
            {
                var list = GetList(dict, key);
                if (list == null || list.Count == 0) return fallback;
                var result = new int[list.Count];
                for (var i = 0; i < list.Count; i++)
                    result[i] = Convert.ToInt32(list[i], CultureInfo.InvariantCulture);
                return result;
            }
        }
    }
}

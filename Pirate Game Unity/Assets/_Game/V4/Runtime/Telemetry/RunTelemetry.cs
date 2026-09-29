using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Telemetry
{
    /// <summary>
    /// The run's combat telemetry and its JSON export (M14; port of V3's static RunCombatTelemetry and WeaponDamageLedger,
    /// which V4 fed through legacy adapters). Same counters, events and export schema — the file lands where V3's did
    /// (persistentDataPath/telemetry/combat_{experiment}_{seed}.json) — so balance tooling keeps working. One instance per
    /// run; the experiment id, slot cap and switch come from the run's ExperimentConfig.
    /// </summary>
    public sealed class RunTelemetry : IWeaponTelemetry
    {
        sealed class WeaponStats
        {
            public float totalDamage, bossDamage, eliteDamage, controlSeconds;
            public int kills, hits, critHits, controlApplications, maxConcurrentTargetsHit, emissions;
        }

        readonly string _experimentId;
        readonly bool _enabled;
        readonly Func<float> _runTime;
        readonly Func<int> _runSeed;
        readonly List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        readonly Dictionary<int, int> _weaponCountAtLevel = new Dictionary<int, int>();
        readonly Dictionary<float, int> _weaponCountAtTime = new Dictionary<float, int>();
        readonly Dictionary<string, int> _offerChoices = new Dictionary<string, int>();
        readonly Dictionary<string, int> _poiChoices = new Dictionary<string, int>();
        readonly Dictionary<string, WeaponStats> _weapons = new Dictionary<string, WeaponStats>();

        float _timeToWeapon2 = -1f, _timeToWeapon3 = -1f, _timeToWeapon4 = -1f, _timeToWeapon5 = -1f, _timeToWeapon6 = -1f;
        int _newWeaponOffersShown, _newWeaponOffersAccepted;
        float _distanceTravelled;
        Vector3 _lastPos;
        bool _hasLastPos;
        float _headingAccumulator;
        float _lastHeading = float.NaN;
        float _orbitSeconds, _lastOrbitSign;
        float _peakProjectiles, _peakEnemies;
        float _damageTaken;
        int _damageEvents, _itemsAcquired, _marksSettled;
        string _stageProfileHash = string.Empty, _runId = string.Empty;

        public RunTelemetry(string experimentId, int weaponSlotCap, bool enabled, Func<float> runTime, Func<int> runSeed)
        {
            _experimentId = experimentId ?? "unknown";
            _enabled = enabled;
            _runTime = runTime;
            _runSeed = runSeed;
            Record("run_start", new Dictionary<string, object> { ["profileId"] = _experimentId, ["weaponSlotCap"] = weaponSlotCap });
        }

        float Now => _runTime != null ? _runTime() : 0f;

        /// <summary>The most enemies sampled at once (tests, the console).</summary>
        public float PeakEnemies => _peakEnemies;
        /// <summary>Shots a weapon emitted this run (tests, the console).</summary>
        public int Emissions(string weaponId) => weaponId != null && _weapons.TryGetValue(weaponId, out var s) ? s.emissions : 0;

        public void RecordWeaponCount(int count, int level, float elapsedSeconds)
        {
            if (!_enabled) return;
            if (!_weaponCountAtLevel.ContainsKey(level)) _weaponCountAtLevel[level] = count;
            var minuteBucket = Mathf.Floor(elapsedSeconds / 30f) * 0.5f;
            if (!_weaponCountAtTime.ContainsKey(minuteBucket)) _weaponCountAtTime[minuteBucket] = count;
            if (count >= 2 && _timeToWeapon2 < 0f) _timeToWeapon2 = elapsedSeconds;
            if (count >= 3 && _timeToWeapon3 < 0f) _timeToWeapon3 = elapsedSeconds;
            if (count >= 4 && _timeToWeapon4 < 0f) _timeToWeapon4 = elapsedSeconds;
            if (count >= 5 && _timeToWeapon5 < 0f) _timeToWeapon5 = elapsedSeconds;
            if (count >= 6 && _timeToWeapon6 < 0f) _timeToWeapon6 = elapsedSeconds;
        }

        public void RecordNewWeaponOfferShown() => _newWeaponOffersShown++;
        public void RecordNewWeaponOfferAccepted() => _newWeaponOffersAccepted++;

        public void RecordDamageTaken(float amount)
        {
            if (!_enabled || amount <= 0f) return;
            _damageTaken += amount;
            _damageEvents++;
            Record("damage_taken", new Dictionary<string, object> { ["amount"] = amount });
        }

        public void RecordOfferChoice(string kind, string id) => RecordChoice(_offerChoices, (kind ?? "unknown") + ":" + (id ?? "unknown"));
        public void RecordPoiInteraction(string poiType) => RecordChoice(_poiChoices, poiType ?? "unknown");

        public void RecordItemAcquired(string itemId)
        {
            if (_enabled) _itemsAcquired++;
            Record("item_acquired", new Dictionary<string, object> { ["id"] = itemId });
        }

        /// <summary>Gold earned and spent this run: the run's gross counters (V3 recorded the same amounts from inside
        /// RunManager.AddGold / TrySpendGold, which reset them at the run start).</summary>
        public Func<int> GoldEarned, GoldSpent;

        public void RecordStageProfileStart(string profileId, string balanceVersion, string contentHash, string runId)
        {
            if (!_enabled) return;
            _stageProfileHash = contentHash ?? string.Empty;
            _runId = runId ?? string.Empty;
            Record("stage_profile_start", new Dictionary<string, object>
            {
                ["profileId"] = profileId ?? string.Empty, ["balanceVersion"] = balanceVersion ?? string.Empty,
                ["contentHash"] = _stageProfileHash, ["runId"] = _runId
            });
        }

        /// <summary>The Rum settlement (V3's "marks_settlement" event and field names, kept for the tooling).</summary>
        public void RecordMarksSettlement(int marksAwarded, bool applied, int total, int minuteMarks, int specialMarks, int eliteMarks, int bossMarks, int overtimeMarks)
        {
            if (!_enabled) return;
            if (applied) _marksSettled = marksAwarded;
            Record("marks_settlement", new Dictionary<string, object>
            {
                ["marksAwarded"] = marksAwarded, ["applied"] = applied, ["total"] = total, ["minutes"] = minuteMarks,
                ["specialPoi"] = specialMarks, ["elite"] = eliteMarks, ["boss"] = bossMarks, ["overtime"] = overtimeMarks
            });
        }

        public void RecordPlayerDeath(bool objectiveSecured, string source = "player_ship") =>
            Record("player_death", new Dictionary<string, object> { ["objectiveSecured"] = objectiveSecured, ["source"] = source });

        public void RecordBossSummoned(int encounterId) => Record("boss_summoned", new Dictionary<string, object> { ["encounterId"] = encounterId });
        public void RecordBossDefeated(int encounterId) => Record("boss_defeated", new Dictionary<string, object> { ["encounterId"] = encounterId });

        public void RecordRunResultsShown(bool objectiveSecured, int marksAwarded) =>
            Record("run_results_shown", new Dictionary<string, object> { ["objectiveSecured"] = objectiveSecured, ["marksAwarded"] = marksAwarded });

        void RecordChoice(Dictionary<string, int> counts, string key)
        {
            if (!_enabled) return;
            counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;
        }

        public void TickMovement(Vector3 position, float headingDegrees, float dt)
        {
            if (!_enabled) return;
            if (_hasLastPos) _distanceTravelled += Vector3.Distance(_lastPos, position);
            _lastPos = position;
            _hasLastPos = true;
            if (!float.IsNaN(_lastHeading))
            {
                var delta = Mathf.Abs(Mathf.DeltaAngle(_lastHeading, headingDegrees));
                _headingAccumulator += delta;
                var sign = Mathf.Sign(Mathf.DeltaAngle(_lastHeading, headingDegrees));
                if (Mathf.Abs(sign) > 0.01f)
                {
                    if (Mathf.Abs(_lastOrbitSign) > 0.01f && Mathf.Sign(_lastOrbitSign) == Mathf.Sign(sign)) _orbitSeconds += dt;
                    else _orbitSeconds = 0f;
                    _lastOrbitSign = sign;
                }
            }
            _lastHeading = headingDegrees;
        }

        public void SamplePerformance(int projectileCount, int enemyCount)
        {
            if (projectileCount > _peakProjectiles) _peakProjectiles = projectileCount;
            if (enemyCount > _peakEnemies) _peakEnemies = enemyCount;
        }

        public void Record(string eventName, Dictionary<string, object> fields)
        {
            if (!_enabled) return;
            var payload = fields != null ? new Dictionary<string, object>(fields) : new Dictionary<string, object>();
            payload["event"] = eventName;
            payload["t"] = Now;
            _events.Add(payload);
        }

        // ---- weapon ledger (V3 WeaponDamageLedger) ----

        WeaponStats Stats(string weaponId)
        {
            if (!_weapons.TryGetValue(weaponId, out var s)) _weapons[weaponId] = s = new WeaponStats();
            return s;
        }

        public void RecordHit(string weaponId, float damage, bool isCrit, bool isElite, bool isBoss)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            var s = Stats(weaponId);
            s.totalDamage += damage;
            s.hits++;
            if (isCrit) s.critHits++;
            if (isElite) s.eliteDamage += damage;
            if (isBoss) s.bossDamage += damage;
        }

        public void RecordKill(string weaponId) => Stats(weaponId).kills++;
        public void RecordEmission(string weaponId) => Stats(weaponId).emissions++;

        public void RecordControl(string weaponId, float seconds)
        {
            var s = Stats(weaponId);
            s.controlApplications++;
            s.controlSeconds += seconds;
        }

        public void RecordConcurrentTargets(string weaponId, int count)
        {
            var s = Stats(weaponId);
            if (count > s.maxConcurrentTargetsHit) s.maxConcurrentTargetsHit = count;
        }

        // ---- export ----

        public string ExportJsonSummary()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendField(sb, "profileId", _experimentId, true);
            AppendField(sb, "timeToWeapon2", _timeToWeapon2);
            AppendField(sb, "timeToWeapon3", _timeToWeapon3);
            AppendField(sb, "timeToWeapon4", _timeToWeapon4);
            AppendField(sb, "timeToWeapon5", _timeToWeapon5);
            AppendField(sb, "timeToWeapon6", _timeToWeapon6);
            AppendField(sb, "newWeaponOffersShown", _newWeaponOffersShown);
            AppendField(sb, "newWeaponOffersAccepted", _newWeaponOffersAccepted);
            AppendField(sb, "distanceTravelled", _distanceTravelled);
            var elapsed = Now;
            AppendField(sb, "headingChangeDegreesPerMinute", elapsed > 1f ? _headingAccumulator / (elapsed / 60f) : 0f);
            AppendField(sb, "continuousOrbitSeconds", _orbitSeconds);
            AppendField(sb, "peakProjectileCount", _peakProjectiles);
            AppendField(sb, "peakEnemyCount", _peakEnemies);
            AppendField(sb, "damageTaken", _damageTaken);
            AppendField(sb, "damageEvents", _damageEvents);
            AppendField(sb, "itemsAcquired", _itemsAcquired);
            AppendField(sb, "goldEarned", GoldEarned != null ? GoldEarned() : 0);
            AppendField(sb, "goldSpent", GoldSpent != null ? GoldSpent() : 0);
            AppendField(sb, "marksSettled", _marksSettled);
            AppendField(sb, "stageProfileHash", _stageProfileHash, false);
            AppendField(sb, "runId", _runId, false);
            AppendCountMap(sb, "offerChoices", _offerChoices);
            AppendCountMap(sb, "poiChoices", _poiChoices);
            sb.Append(",\"weaponContribution\":{");
            var first = true;
            foreach (var kv in _weapons)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":{");
                sb.Append("\"damage\":").Append(kv.Value.totalDamage.ToString("0.###"));
                sb.Append(",\"kills\":").Append(kv.Value.kills);
                sb.Append(",\"hits\":").Append(kv.Value.hits);
                sb.Append('}');
            }
            sb.Append('}');
            sb.Append(",\"events\":[");
            for (var i = 0; i < _events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{');
                var firstField = true;
                foreach (var field in _events[i])
                {
                    if (!firstField) sb.Append(',');
                    firstField = false;
                    sb.Append('"').Append(field.Key).Append("\":");
                    if (field.Value is string text) sb.Append('"').Append(text.Replace("\"", "\\\"")).Append('"');
                    else if (field.Value is bool boolean) sb.Append(boolean ? "true" : "false");
                    else sb.Append(Convert.ToString(field.Value, CultureInfo.InvariantCulture));
                }
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        public void WriteExportFile()
        {
            if (!_enabled) return;
            var dir = Path.Combine(Application.persistentDataPath, "telemetry");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"combat_{_experimentId}_{(_runSeed != null ? _runSeed() : 0)}.json");
            File.WriteAllText(path, ExportJsonSummary());
            Debug.Log($"RunCombatTelemetry exported to {path}");
        }

        static void AppendField(StringBuilder sb, string key, string value, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":\"").Append(value).Append('"');
        }

        static void AppendField(StringBuilder sb, string key, float value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(value.ToString("0.###"));
        }

        static void AppendField(StringBuilder sb, string key, int value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(value);
        }

        static void AppendCountMap(StringBuilder sb, string name, Dictionary<string, int> values)
        {
            sb.Append(",\"").Append(name).Append("\":{");
            var first = true;
            foreach (var pair in values)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(pair.Key).Append("\":").Append(pair.Value);
            }
            sb.Append('}');
        }
    }
}

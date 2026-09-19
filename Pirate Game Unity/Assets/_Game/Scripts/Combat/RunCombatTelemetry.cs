using System.Collections.Generic;
using System.IO;
using System.Text;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Combat
{
    public sealed class RunCombatTelemetry
    {
        public static RunCombatTelemetry Instance { get; private set; }

        readonly CombatExperimentProfile _profile;
        readonly List<Dictionary<string, object>> _events = new();
        readonly Dictionary<int, int> _weaponCountAtLevel = new();
        readonly Dictionary<float, int> _weaponCountAtTime = new();

        float _timeToWeapon2 = -1f;
        float _timeToWeapon3 = -1f;
        float _timeToWeapon4 = -1f;
        float _timeToWeapon5 = -1f;
        float _timeToWeapon6 = -1f;
        int _newWeaponOffersShown;
        int _newWeaponOffersAccepted;
        float _distanceTravelled;
        Vector3 _lastPos;
        bool _hasLastPos;
        float _headingAccumulator;
        float _lastHeading = float.NaN;
        float _orbitSeconds;
        float _lastOrbitSign;
        float _peakProjectiles;
        float _peakEnemies;
        float _damageTaken;
        int _damageEvents;
        int _itemsAcquired;
        int _goldEarned;
        int _goldSpent;
        readonly Dictionary<string, int> _offerChoices = new();
        readonly Dictionary<string, int> _poiChoices = new();

        public RunCombatTelemetry(CombatExperimentProfile profile)
        {
            _profile = profile;
            Instance = this;
            Record("run_start", new Dictionary<string, object>
            {
                ["profileId"] = profile != null ? profile.profileId : "unknown",
                ["weaponSlotCap"] = profile != null ? profile.weaponSlotCap : GameConstants.DefaultWeaponSlots
            });
        }

        public void RecordWeaponCount(int count, int level, float elapsedSeconds)
        {
            if (!_profile.telemetryEnabled) return;
            if (!_weaponCountAtLevel.ContainsKey(level))
                _weaponCountAtLevel[level] = count;
            var minuteBucket = Mathf.Floor(elapsedSeconds / 30f) * 0.5f;
            if (!_weaponCountAtTime.ContainsKey(minuteBucket))
                _weaponCountAtTime[minuteBucket] = count;

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
            if (!_profile.telemetryEnabled || amount <= 0f) return;
            _damageTaken += amount;
            _damageEvents++;
            Record("damage_taken", new Dictionary<string, object> { ["amount"] = amount });
        }

        public void RecordOfferChoice(string kind, string id) => RecordChoice(_offerChoices, (kind ?? "unknown") + ":" + (id ?? "unknown"));
        public void RecordPoiInteraction(string poiType) => RecordChoice(_poiChoices, poiType ?? "unknown");
        public void RecordItemAcquired(string itemId) { if (_profile.telemetryEnabled) _itemsAcquired++; Record("item_acquired", new Dictionary<string, object> { ["id"] = itemId }); }
        public void RecordGoldEarned(int amount) { if (_profile.telemetryEnabled && amount > 0) _goldEarned += amount; }
        public void RecordGoldSpent(int amount) { if (_profile.telemetryEnabled && amount > 0) _goldSpent += amount; }

        void RecordChoice(Dictionary<string, int> counts, string key)
        {
            if (!_profile.telemetryEnabled) return;
            counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;
        }

        public void TickMovement(Vector3 position, float headingDegrees, float dt)
        {
            if (!_profile.telemetryEnabled) return;
            if (_hasLastPos)
                _distanceTravelled += Vector3.Distance(_lastPos, position);
            _lastPos = position;
            _hasLastPos = true;

            if (!float.IsNaN(_lastHeading))
            {
                var delta = Mathf.Abs(Mathf.DeltaAngle(_lastHeading, headingDegrees));
                _headingAccumulator += delta;
                var sign = Mathf.Sign(Mathf.DeltaAngle(_lastHeading, headingDegrees));
                if (Mathf.Abs(sign) > 0.01f)
                {
                    if (Mathf.Abs(_lastOrbitSign) > 0.01f && Mathf.Sign(_lastOrbitSign) == Mathf.Sign(sign))
                        _orbitSeconds += dt;
                    else
                        _orbitSeconds = 0f;
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
            if (!_profile.telemetryEnabled) return;
            fields["event"] = eventName;
            fields["t"] = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            _events.Add(new Dictionary<string, object>(fields));
        }

        public string ExportJsonSummary()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendField(sb, "profileId", _profile.profileId, true);
            AppendField(sb, "timeToWeapon2", _timeToWeapon2);
            AppendField(sb, "timeToWeapon3", _timeToWeapon3);
            AppendField(sb, "timeToWeapon4", _timeToWeapon4);
            AppendField(sb, "timeToWeapon5", _timeToWeapon5);
            AppendField(sb, "timeToWeapon6", _timeToWeapon6);
            AppendField(sb, "newWeaponOffersShown", _newWeaponOffersShown);
            AppendField(sb, "newWeaponOffersAccepted", _newWeaponOffersAccepted);
            AppendField(sb, "distanceTravelled", _distanceTravelled);
            AppendField(sb, "headingChangeDegreesPerMinute",
                RunManager.Instance != null && RunManager.Instance.ElapsedSeconds > 1f
                    ? _headingAccumulator / (RunManager.Instance.ElapsedSeconds / 60f)
                    : 0f);
            AppendField(sb, "continuousOrbitSeconds", _orbitSeconds);
            AppendField(sb, "peakProjectileCount", _peakProjectiles);
            AppendField(sb, "peakEnemyCount", _peakEnemies);
            AppendField(sb, "damageTaken", _damageTaken);
            AppendField(sb, "damageEvents", _damageEvents);
            AppendField(sb, "itemsAcquired", _itemsAcquired);
            AppendField(sb, "goldEarned", _goldEarned);
            AppendField(sb, "goldSpent", _goldSpent);
            AppendCountMap(sb, "offerChoices", _offerChoices);
            AppendCountMap(sb, "poiChoices", _poiChoices);
            AppendWeaponLedger(sb);
            AppendEvents(sb);
            sb.Append('}');
            return sb.ToString();
        }

        public void WriteExportFile()
        {
            if (!_profile.telemetryEnabled) return;
            var dir = Path.Combine(Application.persistentDataPath, "telemetry");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"combat_{_profile.profileId}_{RunManager.Instance?.RunSeed ?? 0}.json");
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
                sb.Append('\"').Append(pair.Key).Append("\":").Append(pair.Value);
            }
            sb.Append('}');
        }

        void AppendWeaponLedger(StringBuilder sb)
        {
            sb.Append(",\"weaponContribution\":{");
            var snapshot = WeaponDamageLedger.Snapshot();
            var first = true;
            foreach (var kv in snapshot)
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
        }

        void AppendEvents(StringBuilder sb)
        {
            sb.Append(",\"events\":[");
            for (var i = 0; i < _events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{');
                var first = true;
                foreach (var field in _events[i])
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(field.Key).Append("\":");
                    if (field.Value is string text) sb.Append('"').Append(text.Replace("\"", "\\\"")).Append('"');
                    else if (field.Value is bool boolean) sb.Append(boolean ? "true" : "false");
                    else sb.Append(System.Convert.ToString(field.Value, System.Globalization.CultureInfo.InvariantCulture));
                }
                sb.Append('}');
            }
            sb.Append(']');
        }
    }
}

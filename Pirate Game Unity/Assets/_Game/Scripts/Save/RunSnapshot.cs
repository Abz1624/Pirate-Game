using System;
using System.Collections.Generic;
using HighSeas.Combat;
using UnityEngine;

namespace HighSeas.Save
{
    [Serializable]
    public sealed class RunSnapshotData
    {
        public int schemaVersion = 1;
        public string experimentProfileId = "D_FULL_COMBAT_V2";
        public int runSeed;
        public float elapsedSeconds;
        public int level;
        public float currentXp;
        public int gold;
        public int kills;
        public bool objectiveSecured;
        public List<string> weaponIds = new();
        public List<int> weaponLevels = new();
        public List<string> logbookIds = new();
        public List<int> logbookLevels = new();
        public List<string> runItemIds = new();
        public List<string> evolutionIds = new();
        public string captainId;
        public float captainActiveCooldown;
    }

    public static class RunSnapshotService
    {
        const string PrefKey = "highseas_run_snapshot_v1";

        public static bool HasSnapshot => !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey, string.Empty));

        public static void Save(RunSnapshotData data)
        {
            if (data == null) return;
            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefKey, json);
            PlayerPrefs.Save();
        }

        public static RunSnapshotData Load()
        {
            var json = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<RunSnapshotData>(json);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefKey);
        }

        public static RunSnapshotData CaptureFromRun(
            HighSeas.Core.RunManager run,
            HighSeas.UI.LevelSystem levels,
            CombatExperimentProfile profile)
        {
            if (run == null || levels == null) return null;
            var data = new RunSnapshotData
            {
                experimentProfileId = profile != null ? profile.profileId : CombatExperimentContext.Active.profileId,
                runSeed = run.RunSeed,
                elapsedSeconds = run.ElapsedSeconds,
                level = levels.Level,
                currentXp = levels.CurrentXP,
                gold = run.Gold,
                kills = run.KillCount,
                objectiveSecured = run.ObjectiveSecured,
                captainId = V3.V3CaptainSelection.Selected.ToString()
            };

            var weapons = levels.OwnedWeapons;
            for (var i = 0; i < weapons.Count; i++)
            {
                data.weaponIds.Add(weapons[i]);
                data.weaponLevels.Add(levels.GetWeaponLevel(weapons[i]));
            }

            var logbooks = levels.OwnedLogbooks;
            for (var i = 0; i < logbooks.Count; i++)
            {
                data.logbookIds.Add(logbooks[i]);
                data.logbookLevels.Add(levels.GetLogbookLevel(logbooks[i]));
            }

            var items = V3.V3RunItemInventory.Instance;
            if (items != null)
            {
                foreach (var id in items.Items)
                    data.runItemIds.Add(id);
            }

            for (var i = 0; i < data.weaponIds.Count; i++)
            {
                if (WeaponEvolutionRuntime.TryGetEvolutionId(data.weaponIds[i], out var evolutionId))
                    data.evolutionIds.Add(evolutionId);
            }

            var active = V3.V3CaptainActiveController.Instance;
            if (active != null)
                data.captainActiveCooldown = active.CooldownRemaining;

            return data;
        }
    }
}

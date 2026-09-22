using HighSeas.Balance;
using HighSeas.Encounters;

namespace HighSeas.Save
{
    public enum RunSnapshotRestoreVerdict
    {
        OkLoadoutSuspend,
        RejectUnsupportedSchema,
        RejectProfileMismatch,
        RejectIncompleteActiveEncounter
    }

    /// <summary>
    /// Supported suspend snapshots: loadout/progress fields only (schema v1/v2).
    /// Mid-fight encounter resume is not supported; risky snapshots are rejected cleanly.
    /// </summary>
    public static class RunSnapshotRestoreLogic
    {
        public const int SupportedSchemaVersion = 2;

        public static RunSnapshotRestoreVerdict Evaluate(
            RunSnapshotData data,
            string expectedProfileContentHash = null)
        {
            if (data == null) return RunSnapshotRestoreVerdict.RejectIncompleteActiveEncounter;
            if (data.schemaVersion > SupportedSchemaVersion)
                return RunSnapshotRestoreVerdict.RejectUnsupportedSchema;

            if (!string.IsNullOrEmpty(expectedProfileContentHash) &&
                !string.IsNullOrEmpty(data.profileContentHash) &&
                data.profileContentHash != expectedProfileContentHash)
                return RunSnapshotRestoreVerdict.RejectProfileMismatch;

            if (HasRiskyActiveEncounter(data))
                return RunSnapshotRestoreVerdict.RejectIncompleteActiveEncounter;

            return RunSnapshotRestoreVerdict.OkLoadoutSuspend;
        }

        public static bool HasRiskyActiveEncounter(RunSnapshotData data)
        {
            if (data.schemaVersion < 2) return false;

            if (data.bossSummoned && !data.bossDefeated)
            {
                if (data.activeBossEncounterId <= 0)
                    return true;

                if (data.activeBossState == (int)RunEncounterState.Failed)
                    return false;

                if (data.activeBossSpawnedCount <= 0)
                    return true;

                if (data.activeBossState == (int)RunEncounterState.Requested ||
                    data.activeBossState == (int)RunEncounterState.Spawned)
                    return true;
            }

            if (data.activePowderCacheEncounterId > 0 &&
                data.activePowderCacheState != (int)RunEncounterState.Failed &&
                data.activePowderCacheState != (int)RunEncounterState.Completed &&
                data.activePowderCacheSpawnedCount <= 0)
                return true;

            return false;
        }
    }
}

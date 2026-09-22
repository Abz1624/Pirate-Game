using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    public readonly struct StageBalanceValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        StageBalanceValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static StageBalanceValidationResult Ok() => new(true, null);

        public static StageBalanceValidationResult Fail(string message) =>
            new(false, message ?? "Unknown stage balance validation error.");
    }

    public static class StageBalanceProfileValidator
    {
        public static StageBalanceValidationResult Validate(StageBalanceProfile profile)
        {
            if (profile == null)
                return StageBalanceValidationResult.Fail("Stage balance profile reference is null.");

            if (string.IsNullOrWhiteSpace(profile.profileId))
                return StageBalanceValidationResult.Fail("profileId is required.");

            if (profile.schemaVersion <= 0)
                return StageBalanceValidationResult.Fail("schemaVersion must be positive.");

            if (profile.balanceVersion <= 0)
                return StageBalanceValidationResult.Fail("balanceVersion must be positive.");

            if (profile.encounterSchedule == null)
                return StageBalanceValidationResult.Fail("encounterSchedule reference is required.");

            if (profile.ambientRatePerSecond == null || profile.ambientRatePerSecond.Length == 0)
                return StageBalanceValidationResult.Fail("ambientRatePerSecond must contain at least one band.");

            for (var i = 0; i < profile.ambientRatePerSecond.Length; i++)
            {
                if (profile.ambientRatePerSecond[i] < 0f)
                    return StageBalanceValidationResult.Fail("ambient rates cannot be negative.");
            }

            if (profile.maxActiveEnemies <= 0)
                return StageBalanceValidationResult.Fail("maxActiveEnemies must be positive.");

            if (profile.reservedEncounterSlots < 0 ||
                profile.reservedEncounterSlots >= profile.maxActiveEnemies)
                return StageBalanceValidationResult.Fail("reservedEncounterSlots must be within maxActiveEnemies.");

            if (profile.salvageChestCount <= 0)
                return StageBalanceValidationResult.Fail("salvageChestCount must be positive.");

            if (profile.starterWeaponIds == null || profile.starterWeaponIds.Length == 0)
                return StageBalanceValidationResult.Fail("starterWeaponIds cannot be empty.");

            var duplicateWeapons = FindDuplicateIds(profile.starterWeaponIds);
            if (duplicateWeapons != null)
                return StageBalanceValidationResult.Fail($"Duplicate starter weapon id '{duplicateWeapons}'.");

            var scheduleValidation = ValidateScheduleReferences(
                profile.encounterSchedule,
                profile.enforceCompleteScheduleReferences);
            if (!scheduleValidation.IsValid)
                return scheduleValidation;

            return StageBalanceValidationResult.Ok();
        }

        static StageBalanceValidationResult ValidateScheduleReferences(
            EncounterSpawnSchedule schedule,
            bool requireCompleteReferences)
        {
            if (!requireCompleteReferences)
                return StageBalanceValidationResult.Ok();
            if (schedule.bossSummon == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.bossSummon is required.");
            if (schedule.powderCacheAmbush == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.powderCacheAmbush is required.");
            if (schedule.swarmPulse == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.swarmPulse is required.");
            if (schedule.drownedPursuer == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.drownedPursuer is required.");
            if (schedule.drownedHarrier == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.drownedHarrier is required.");
            if (schedule.hammerheadBeat == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.hammerheadBeat is required.");
            if (schedule.krakenBeat == null)
                return StageBalanceValidationResult.Fail("encounterSchedule.krakenBeat is required.");

            return StageBalanceValidationResult.Ok();
        }

        static string FindDuplicateIds(IReadOnlyList<string> ids)
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!seen.Add(id)) return id;
            }
            return null;
        }
    }
}

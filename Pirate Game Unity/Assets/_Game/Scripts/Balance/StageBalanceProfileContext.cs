using System;
using HighSeas.V3.MultiShip;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Balance
{
    public sealed class StageBalanceRuntimeState
    {
        public StageBalanceProfileExport Snapshot { get; }
        public int SuccessfulChestPurchases { get; private set; }

        public StageBalanceRuntimeState(StageBalanceProfileExport snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public int CurrentChestPrice =>
            StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(Snapshot, SuccessfulChestPurchases);

        public void RegisterSuccessfulChestPurchase() => SuccessfulChestPurchases++;
    }

    public static class StageBalanceProfileContext
    {
        static StageBalanceProfile _authoring;
        static StageBalanceRuntimeState _runtime;

        public static bool IsActive => _runtime != null;
        public static StageBalanceProfile Authoring => _authoring;
        public static StageBalanceProfileExport Export => _runtime?.Snapshot;
        public static StageBalanceRuntimeState Runtime => _runtime;

        public static void BeginRun(StageBalanceProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _authoring = profile;
            _runtime = new StageBalanceRuntimeState(profile.ToExport());
        }

        public static void EndRun()
        {
            _runtime = null;
            _authoring = null;
        }

        public static void ResetForTests()
        {
            _runtime = null;
            _authoring = null;
        }
    }

    public static class StageBalanceProfileResolver
    {
        public static bool ShouldUseStageBalance =>
            !V3RunSession.IsLocalMultiplayer && StageBalanceProfileContext.IsActive;

        public static bool TryResolveForCurrentScene(
            out StageBalanceProfile profile,
            out string errorMessage)
        {
            profile = null;
            errorMessage = null;

            if (V3RunSession.IsLocalMultiplayer)
                return false;

            var host = UnityEngine.Object.FindFirstObjectByType<StageBalanceProfileHost>();
            if (host == null)
            {
                errorMessage = "StageBalanceProfileHost is missing from the solo run scene.";
                return false;
            }

            profile = host.Profile;
            if (profile == null)
            {
                errorMessage = "StageBalanceProfileHost.profile is not assigned.";
                return false;
            }

            if (!host.RequireForSinglePlayerRun)
                return true;

            var validation = StageBalanceProfileValidator.Validate(profile);
            if (validation.IsValid)
                return true;

            errorMessage = validation.ErrorMessage;
            profile = null;
            return false;
        }
    }
}

using System;

namespace RoguePirates.V4.Views
{
    /// <summary>Button presses from the HUD and results screen (M14; was LegacyHudCommands): boost to the ship, restart and
    /// menu to V4, unlock goals to the V4 unlock service.</summary>
    public sealed class HudCommands : IHudCommands
    {
        readonly Action _boost;
        readonly Action _retry;
        readonly Action _menu;
        readonly RoguePirates.V4.Rewards.UnlockService _unlocks;
        readonly Func<bool> _pause;

        readonly Func<string, int> _completeGoal;

        public HudCommands(Action boost, Action retry, Action menu, RoguePirates.V4.Rewards.UnlockService unlocks, Func<bool> pause = null,
            Func<string, int> completeGoal = null)
        {
            _completeGoal = completeGoal;
            _boost = boost;
            _retry = retry;
            _menu = menu;
            _unlocks = unlocks;
            _pause = pause;
        }

        public bool TogglePause() => _pause != null && _pause();
        public int CompleteGoal(string goal) => _completeGoal != null ? _completeGoal(goal) : 0;

        /// <summary>V5 M7: the player's pause on top of the run's own (a level-up or a chest choice is never overridden).</summary>
        public static bool TogglePause(RoguePirates.V4.Foundation.Core.RunManager run)
        {
            if (run == null || run.IsRunEnded) return false;
            if (run.PauseReason == RoguePirates.V4.Foundation.Core.PauseReason.None) { run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.UserPause); return true; }
            if (run.PauseReason == RoguePirates.V4.Foundation.Core.PauseReason.UserPause) run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None);
            return false;
        }

        public void RequestBoost() => _boost?.Invoke();
        public void Retry() => _retry?.Invoke();
        public void GoToMenu() => _menu?.Invoke();
        public string SelectNextUnlockGoal() => _unlocks?.SelectNextGoal();
        public string PurchaseUnlockGoal() => _unlocks?.PurchaseGoal();
    }
}

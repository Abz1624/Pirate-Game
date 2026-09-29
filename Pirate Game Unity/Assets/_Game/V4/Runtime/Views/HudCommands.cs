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

        public HudCommands(Action boost, Action retry, Action menu, RoguePirates.V4.Rewards.UnlockService unlocks)
        {
            _boost = boost;
            _retry = retry;
            _menu = menu;
            _unlocks = unlocks;
        }

        public void RequestBoost() => _boost?.Invoke();
        public void Retry() => _retry?.Invoke();
        public void GoToMenu() => _menu?.Invoke();
        public string SelectNextUnlockGoal() => _unlocks?.SelectNextGoal();
        public string PurchaseUnlockGoal() => _unlocks?.PurchaseGoal();
    }
}

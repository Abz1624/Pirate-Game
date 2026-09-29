using HighSeas.Core;
using UnityEngine.InputSystem;

namespace RoguePirates.V4.Stage
{
    public enum PromptLayout { BottomBar, CenterCompact, SalvageChestPopup }

    /// <summary>What the interaction prompt shows (the prompt presenter draws it).</summary>
    public interface IInteractionPromptView
    {
        bool Visible { get; }
        string Text { get; }
        PromptLayout Layout { get; }
    }

    /// <summary>The prompt's touch USE button (the prompt presenter forwards presses).</summary>
    public interface IInteractionTouch
    {
        bool TouchHolding { set; }
        void TouchPressed();
    }

    /// <summary>
    /// Hold-to-interact and the chest Take/Skip choice (M7): port of V3InteractionController's logic; the prompt panel is
    /// the prompt presenter's. Each frame (in V3InteractionController.Update's place): pick the nearest interactable POI
    /// in range, then chest price popup (pauses the run with the POIChoice reason; E takes, R skips), Signal Buoy charge
    /// text, Powder Cache label, instant landmarks, or the Beacon's hold (E or the touch button). Same texts and rules.
    /// </summary>
    public sealed class InteractionModel : IInteractionPromptView, IInteractionTouch
    {
        readonly StageContentModule _stage;
        readonly IStageRun _run;
        readonly IStageToasts _toasts;
        int _target = -1;
        float _holdTimer;
        bool _holding;
        int _salvageChestDismissedIndex = -1;
        int _salvageChestInsufficientFundsNotifiedIndex = -1;
        bool _salvageChestPauseActive;
        RunState _salvageChestResumeState = RunState.Playing;

        public InteractionModel(StageContentModule stage, IStageRun run, IStageToasts toasts)
        {
            _stage = stage;
            _run = run;
            _toasts = toasts;
        }

        public bool Visible { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public PromptLayout Layout { get; private set; } = PromptLayout.BottomBar;
        public int Target => _target;
        /// <summary>Set by the prompt presenter while its touch button is held.</summary>
        public bool TouchHolding { get; set; }
        /// <summary>Off: no prompts or hold-to-interact (V3 disabled its controller in scenes without landmarks). On everywhere since the M8 playtest (the Module Lab too).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>V3InteractionController.Update.</summary>
        public void Tick(float dt)
        {
            if (!Enabled) return;
            if (_run.Exists && _run.IsRunEnded)
            {
                EndSalvageChestChoicePause();
                CancelHold();
                return;
            }
            if (_run.Exists && _run.IsPaused && _run.PauseReason != PauseReason.POIChoice)
            {
                _salvageChestPauseActive = false;
                CancelHold();
                return;
            }

            RefreshNearestTarget();
            if (_target < 0)
            {
                EndSalvageChestChoicePause();
                Visible = false;
                return;
            }
            UpdateLayout();

            var holdDuration = _stage.RequiredHoldSeconds(_target);
            var label = _stage.PromptLabel(_target);
            // R-101 fix, extended at the M8 playtest (off = V3): while the Beacon's summon telegraph runs the hold is done;
            // V3 kept offering it, so a held E ran a second 0-100% hold that did nothing until the boss appeared.
            if (_stage.Config != null && _stage.Config.promptReleasesUsedLandmark && _stage.IsTelegraphing(_target))
            {
                CancelHold();
                Visible = true;
                Text = $"{label} — summoning...";
                return;
            }
            var keyboard = Keyboard.current;
            var interactHeld = (keyboard != null && keyboard.eKey.isPressed) || TouchHolding;

            if (holdDuration <= 0f)
            {
                if (_stage.SpringsOnPress(_target))
                {
                    Visible = _stage.IsPlayerInRange(_target);
                    Text = $"{label} — press E or USE";
                    if (Visible && keyboard != null && keyboard.eKey.wasPressedThisFrame) _stage.TryCompleteInteraction(_target);
                    return;
                }
                if (_stage.UsesChargeInteraction(_target))
                {
                    Visible = _stage.IsPlayerInRange(_target);
                    Text = $"{label} — charging {_stage.SignalBuoyCharge01(_target):0%}";
                    return;
                }
                if (_stage.UsesProximityAmbush(_target))
                {
                    Visible = _stage.IsPlayerInRange(_target);
                    Text = label;
                    return;
                }
                if (_stage.ShowsSalvageChestPrice(_target) || _stage.ShowsFreeChestPopup(_target))
                {
                    TickSalvageChest(keyboard);
                    return;
                }
                Visible = false;
                if (_stage.IsPlayerInRange(_target)) _stage.TryCompleteInteraction(_target);
                return;
            }

            if (interactHeld)
            {
                _holding = true;
                _holdTimer += dt;
                Visible = true;
                Text = $"{label} — hold {UnityEngine.Mathf.Clamp01(_holdTimer / holdDuration):0%}";
                if (_holdTimer >= holdDuration)
                {
                    _stage.TryCompleteInteraction(_target);
                    CancelHold();
                }
            }
            else if (_holding) CancelHold();
            else
            {
                Visible = true;
                Text = $"{label} — hold E or USE ({holdDuration:0.#}s)";
            }
        }

        void TickSalvageChest(Keyboard keyboard)
        {
            if (!_stage.CanInteract(_target))
            {
                Visible = false;
                EndSalvageChestChoicePause();
                return;
            }
            if (!_stage.IsPlayerInRange(_target))
            {
                if (_salvageChestDismissedIndex == _target) _salvageChestDismissedIndex = -1;
                _salvageChestInsufficientFundsNotifiedIndex = -1;
                Visible = false;
                EndSalvageChestChoicePause();
                return;
            }
            if (_salvageChestDismissedIndex == _target)
            {
                Visible = false;
                EndSalvageChestChoicePause();
                return;
            }
            var free = _stage.ShowsFreeChestPopup(_target); // the Powder Cache's Rare Treasure Chest (M8 playtest)
            if (!free && !_stage.CanAffordSalvageChest())
            {
                Visible = false;
                EndSalvageChestChoicePause();
                if (_salvageChestInsufficientFundsNotifiedIndex != _target)
                {
                    _salvageChestInsufficientFundsNotifiedIndex = _target;
                    _toasts?.Show($"Not enough funds — need {_stage.QuoteChestPrice()} gold.", 2.5f);
                }
                return;
            }

            _salvageChestInsufficientFundsNotifiedIndex = -1;
            Visible = true;
            BeginSalvageChestChoicePause();
            Text = free
                ? "<b>Rare Treasure Chest</b>\n<size=120%>Free</size>\nTake (E)  ·  Skip (R)"
                : $"<b>Salvage Chest</b>\n<size=120%>{_stage.QuoteChestPrice()} gold</size>\nTake (E)  ·  Skip (R)";
            if (keyboard == null) return;
            if (keyboard.eKey.wasPressedThisFrame)
            {
                _stage.TryCompleteInteraction(_target);
                EndSalvageChestChoicePause();
            }
            else if (keyboard.rKey.wasPressedThisFrame) SkipChest();
        }

        /// <summary>Skip (R): the chest's prompt stays closed until the ship leaves its range (trace pilot uses it too).</summary>
        public void SkipChest()
        {
            if (_target < 0) return;
            _salvageChestDismissedIndex = _target;
            EndSalvageChestChoicePause();
        }

        /// <summary>The touch USE button's click (V3 OnTouchInteractPressed).</summary>
        public void TouchPressed()
        {
            if (_target < 0 || _stage.RequiredHoldSeconds(_target) > 0f) return;
            if (_stage.ShowsSalvageChestPrice(_target) || _stage.ShowsFreeChestPopup(_target))
            {
                if (_stage.IsPlayerInRange(_target) && (_stage.ShowsFreeChestPopup(_target) || _stage.CanAffordSalvageChest()))
                {
                    _stage.TryCompleteInteraction(_target);
                    EndSalvageChestChoicePause();
                }
                return;
            }
            _stage.TryCompleteInteraction(_target);
        }

        /// <summary>
        /// Keep the current target while in range, even once used (V3 kept it, so a second chest nearby is not offered
        /// until the ship leaves the first one's range); else the nearest interactable marker in range.
        /// </summary>
        void RefreshNearestTarget()
        {
            var rules = _stage.Config;
            // R-101 fix: let go of a used landmark, and (M8 playtest) of a hidden one (a Powder Cache whose ambush is on).
            var releaseUsed = rules != null && _target >= 0 &&
                ((rules.promptReleasesUsedLandmark && _stage.IsConsumed(_target)) || ((rules.promptReleasesUsedLandmark || rules.powderCacheEliteChallenge) && _stage.IsHidden(_target)));
            if (_target >= 0 && _stage.IsPlayerInRange(_target) && !releaseUsed) return;
            // R-96 fix: forget a skipped chest once the ship has left it (V3 dropped the target before this could happen).
            if (_target >= 0 && rules != null && rules.forgetChestSkipWhenLeaving && _salvageChestDismissedIndex == _target) _salvageChestDismissedIndex = -1;
            _target = -1;
            var best = float.MaxValue;
            var order = _stage.MarkerOrder;
            for (var i = 0; i < order.Count; i++)
            {
                var index = order[i];
                if (!_stage.CanInteract(index)) continue;
                var dist = _stage.DistanceToShip(index);
                if (dist > _stage.InteractRange(index) || dist >= best) continue;
                best = dist;
                _target = index;
            }
        }

        void UpdateLayout()
        {
            Layout = _stage.ShowsFreeChestPopup(_target) || (_stage.ShowsSalvageChestPrice(_target) && _stage.CanAffordSalvageChest())
                ? PromptLayout.SalvageChestPopup
                : _stage.RequiredHoldSeconds(_target) > 0f ? PromptLayout.CenterCompact : PromptLayout.BottomBar;
        }

        void CancelHold()
        {
            _holding = false;
            _holdTimer = 0f;
        }

        void BeginSalvageChestChoicePause()
        {
            if (_salvageChestPauseActive || !_run.Exists || _run.IsRunEnded || _run.IsPaused) return;
            _salvageChestResumeState = _run.State;
            _salvageChestPauseActive = true;
            _run.SetPause(PauseReason.POIChoice);
            _run.SetState(RunState.POIChoicePaused);
        }

        void EndSalvageChestChoicePause()
        {
            if (!_salvageChestPauseActive) return;
            _salvageChestPauseActive = false;
            if (!_run.Exists || _run.PauseReason != PauseReason.POIChoice) return;
            _run.SetPause(PauseReason.None);
            var resume = _salvageChestResumeState;
            if (resume == RunState.POIChoicePaused || resume == RunState.LevelUpPaused) resume = RunState.Playing;
            _run.SetState(resume);
        }
    }
}

using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.V3
{
    /// <summary>Prototype captain active abilities (doc §9), gated by main-menu toggle.</summary>
    public sealed class V3CaptainActiveController : MonoBehaviour
    {
        public static V3CaptainActiveController Instance { get; private set; }

        [SerializeField] V3CaptainId captainId = V3CaptainId.Rook;

        float _cooldownRemaining;
        float _braceTimer;
        float _kegRadiusBoostTimer;
        float _richieTimer;
        float _emberFlameTimer;
        float _roxieTimer;

        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownDuration => GetDefinition().cooldown;
        public bool IsReady => _cooldownRemaining <= 0f;
        public string ActiveDisplayName => GetDefinition().displayName;
        public float IncomingDamageMultiplier => _braceTimer > 0f ? 0.4f : 1f;
        public float PowderKegBlastRadiusMultiplier => _kegRadiusBoostTimer > 0f ? 1.2f : 1f;
        public bool FlameBoostActive => _emberFlameTimer > 0f;
        public float FlameRangeMultiplier => FlameBoostActive ? 1.2f : 1f;
        public float BurnDamageMultiplier => FlameBoostActive ? 1.3f : 1f;

        struct ActiveDefinition
        {
            public string displayName;
            public string description;
            public float cooldown;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(V3CaptainId id) => captainId = id;

        void Update()
        {
            if (!CombatExperimentContext.CaptainActivesEnabled) return;
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded) return;

            var dt = Time.deltaTime;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
            _braceTimer = Mathf.Max(0f, _braceTimer - dt);
            _kegRadiusBoostTimer = Mathf.Max(0f, _kegRadiusBoostTimer - dt);
            _richieTimer = Mathf.Max(0f, _richieTimer - dt);
            _emberFlameTimer = Mathf.Max(0f, _emberFlameTimer - dt);
            _roxieTimer = Mathf.Max(0f, _roxieTimer - dt);
            _richieDamageBuffTimer = Mathf.Max(0f, _richieDamageBuffTimer - dt);

            if (!WasActivatePressed()) return;
            TryActivate();
        }

        float _richieDamageBuffTimer;
        float _roxieNearMissCredit;

        static bool WasActivatePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame))
                return true;
            return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        public bool TryActivate()
        {
            if (!CombatExperimentContext.CaptainActivesEnabled || !IsReady) return false;
            var def = GetDefinition();
            var player = FindFirstObjectByType<PlayerShipController>();
            var weapons = FindFirstObjectByType<WeaponController>();

            switch (captainId)
            {
                case V3CaptainId.Brine:
                    _braceTimer = 2f;
                    player?.ApplySlow(0.75f, 2f);
                    break;
                case V3CaptainId.Flint:
                    weapons?.CaptainArmPowderKegs();
                    _kegRadiusBoostTimer = 4f;
                    break;
                case V3CaptainId.Richie:
                    _richieTimer = 3f;
                    _richieDamageBuffTimer = 5f;
                    player?.ApplyMoveEffect(1.35f, 3f);
                    break;
                case V3CaptainId.Ember:
                    _emberFlameTimer = 5f;
                    break;
                case V3CaptainId.Roxie:
                    _roxieTimer = 2.5f;
                    player?.ApplyMoveEffect(1.4f, 2.5f);
                    weapons?.CaptainFireLightningSalvo();
                    break;
                default:
                    weapons?.CaptainFireAllWeapons();
                    break;
            }

            _cooldownRemaining = def.cooldown;
            V3ToastHud.Instance?.Show($"{def.displayName}!", 2.5f);
            return true;
        }

        public float GetRichieActiveDamageBonus() =>
            _richieDamageBuffTimer > 0f ? 0.1f : 0f;

        public void NotifyNearMissDodge(float secondsReduced = 0.35f)
        {
            if (captainId != V3CaptainId.Roxie || _roxieTimer <= 0f) return;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - secondsReduced);
        }

        ActiveDefinition GetDefinition() => captainId switch
        {
            V3CaptainId.Brine => new ActiveDefinition
            {
                displayName = "Brace!",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Brine),
                cooldown = 20f
            },
            V3CaptainId.Flint => new ActiveDefinition
            {
                displayName = "Light the Fuse",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Flint),
                cooldown = 18f
            },
            V3CaptainId.Richie => new ActiveDefinition
            {
                displayName = "Cut and Run",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Richie),
                cooldown = 16f
            },
            V3CaptainId.Ember => new ActiveDefinition
            {
                displayName = "Stoke the Flames",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Ember),
                cooldown = 18f
            },
            V3CaptainId.Roxie => new ActiveDefinition
            {
                displayName = "Ride the Gale",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Roxie),
                cooldown = 15f
            },
            _ => new ActiveDefinition
            {
                displayName = "All Guns!",
                description = V3CaptainCatalog.GetActiveSummary(V3CaptainId.Rook),
                cooldown = 18f
            }
        };
    }
}

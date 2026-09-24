using HighSeas.Audio;
using HighSeas.Core;
using HighSeas.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.PlayerSystems
{
    public sealed class ShipBoostController : MonoBehaviour
    {
        [SerializeField] float boostDuration = 2.5f;
        [SerializeField] float boostCooldown = 6f;
        [SerializeField] float boostMoveMultiplier = 1.5f;

        PlayerShipController _ship;
        float _boostTimer;
        float _cooldownTimer;

        public float BoostNormalized => _boostTimer > 0f ? _boostTimer / boostDuration : 0f;
        public bool CanBoost => _cooldownTimer <= 0f && _boostTimer <= 0f;

        public void Bind(PlayerShipController ship) => _ship = ship;

        void Update()
        {
            if (_ship == null || RunManager.Instance == null || RunManager.Instance.IsPaused) return;

            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);
            if (_boostTimer > 0f)
            {
                _boostTimer -= Time.deltaTime;
                _ship.ApplyMoveEffect(boostMoveMultiplier, 0.05f);
                if (_boostTimer <= 0f)
                    _cooldownTimer = boostCooldown;
            }

            var boostPressed = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
                boostPressed = true;
            if (boostPressed && CanBoost)
                StartBoost();

        }

        public void RequestBoost()
        {
            if (!CanBoost) return;
            StartBoost();
        }

        void StartBoost()
        {
            _boostTimer = boostDuration;
            SfxService.PlayPlaceholder("boost");
        }
    }
}

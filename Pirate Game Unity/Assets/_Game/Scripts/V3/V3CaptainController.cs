using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.UI;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Applies Rook/Brine/Flint captain profiles and per-level passives during a v3 run.</summary>
    public sealed class V3CaptainController : MonoBehaviour
    {
        public static V3CaptainController Instance { get; private set; }

        [SerializeField] V3CaptainId captainId = V3CaptainId.Rook;

        V3CaptainProfile _profile;
        float _fastMoveTimer;
        bool _runningFuseActive;

        public V3CaptainId CaptainId => captainId;
        public V3CaptainProfile Profile => _profile;

        void Awake() => Instance = this;

        public void Initialize(V3CaptainId id, PlayerShipController player)
        {
            if (LevelSystem.Instance != null)
                LevelSystem.Instance.LevelChanged -= OnLevelChanged;

            captainId = id;
            _profile = V3CaptainProfile.Get(id);
            ApplyBaseStats(player);

            if (LevelSystem.Instance != null)
                LevelSystem.Instance.LevelChanged += OnLevelChanged;
        }

        void OnDestroy()
        {
            if (LevelSystem.Instance != null)
                LevelSystem.Instance.LevelChanged -= OnLevelChanged;
            if (Instance == this)
                Instance = null;
        }

        void OnLevelChanged(int _) => ApplyBrineArmor();

        void ApplyBaseStats(PlayerShipController player)
        {
            if (RunManager.Instance == null) return;

            var stats = RunManager.Instance.Stats;
            stats.SetBase(StatId.Hull, _profile.BaseHull);
            stats.SetBase(StatId.MoveSpeed, _profile.BaseMoveSpeed);

            if (player != null)
            {
                player.InitializeFromRun(_profile.BaseHull);
                ApplyBrineArmor();
            }
        }

        void ApplyBrineArmor()
        {
            if (captainId != V3CaptainId.Brine) return;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null || RunManager.Instance == null) return;
            PlayerHullSync.SyncMaxFromRunStats(player);
            player.Damage.SetArmor(GetBrineArmor());
        }

        static float GetPerLevelBonus(int level, float perLevel, float cap) =>
            Mathf.Min(cap, perLevel * Mathf.Max(0, level));

        float GetBrineArmor()
        {
            var level = LevelSystem.Instance != null ? LevelSystem.Instance.Level : 1;
            return GetPerLevelBonus(level, 0.5f, 25f);
        }

        /// <summary>Additive global damage bonus for Rook/Flint (applied at damage roll).</summary>
        public float GetAdditiveDamageBonus()
        {
            var level = LevelSystem.Instance != null ? LevelSystem.Instance.Level : 1;
            var bonus = captainId switch
            {
                V3CaptainId.Rook => GetPerLevelBonus(level, 0.005f, 0.25f),
                V3CaptainId.Flint => GetPerLevelBonus(level, 0.005f, 0.25f),
                V3CaptainId.Richie => GetRichieGoldTensionBonus(),
                V3CaptainId.Ember => GetPerLevelBonus(level, 0.004f, 0.18f),
                V3CaptainId.Roxie => GetPerLevelBonus(level, 0.003f, 0.15f),
                _ => 0f
            };
            return bonus;
        }

        float GetRichieGoldTensionBonus()
        {
            if (RunManager.Instance == null) return 0f;
            return Mathf.Min(0.2f, RunManager.Instance.Gold / 4500f);
        }

        public float GetBurnDamageMultiplier() =>
            captainId == V3CaptainId.Ember ? 1.12f : 1f;

        public float GetLightningDamageMultiplier() =>
            captainId == V3CaptainId.Roxie ? 1.1f : 1f;

        void Update()
        {
            if (captainId != V3CaptainId.Flint || RunManager.Instance == null || RunManager.Instance.IsPaused)
                return;

            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;

            var speed = new Vector3(player.Velocity.x, 0f, player.Velocity.z).magnitude;
            if (speed >= 4f)
                _fastMoveTimer += Time.deltaTime;
            else
            {
                _fastMoveTimer = 0f;
                _runningFuseActive = false;
            }

            if (!_runningFuseActive && _fastMoveTimer >= 2f)
                _runningFuseActive = true;
        }

        /// <summary>Extra weapon damage multiplier from captain passives (Flint keg bonuses).</summary>
        public float GetWeaponDamageMultiplier(string weaponId)
        {
            if (captainId != V3CaptainId.Flint || weaponId != "powder_kegs")
                return 1f;

            return _runningFuseActive ? 1.15f : 1f;
        }
    }
}

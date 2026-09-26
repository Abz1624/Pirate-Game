using System;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>The boss's attack state (V3 BarnacleKingAttackState, same fields).</summary>
    public struct BossAttackState
    {
        public BossAttackKind Attack;
        public BossPhase Phase;
        public float PhaseTimer;
        public Vector3 CommittedDirection;
        public Vector3 MarkedPoint;
        public bool Enraged;
        /// <summary>An attack to run next instead of the rotation. V3 kept the slot but never filled it.</summary>
        public BossAttackKind QueuedFollowUp;
        /// <summary>Unused, as in V3 (kept so the state reads the same).</summary>
        public int PatternIndex;
        public int ScriptPhase;
        public int RotationStep;
        public float PendingHpPhase;
        public bool Phase3ChargePassPending;
    }

    /// <summary>
    /// Port of V3 BarnacleKingBossAttackLogic (SaltwindFinaleLogic.cs). Same branches, same order and the same float
    /// expressions; the constants come from <see cref="BossConfig"/> (defaults = V3's), so with a default config every
    /// state sequence is V3's. Draws no random numbers.
    /// </summary>
    public static class BossAttackLogic
    {
        /// <summary>The boss stays paused during a rest until the rest timer falls to this (V3 literal).</summary>
        public const float RecoveryPauseFloor = 0.05f;
        /// <summary>The last health phase (V3 literal): shortened rests and double charges from here.</summary>
        public const int FinalScriptPhase = 3;
        // V3's switch defaults for "no attack" (only reachable with a None in a rotation, which validation rejects).
        const float NoAttackTelegraphSeconds = 1f;
        const float NoAttackExecuteSeconds = 0.25f;
        const float NoAttackRecoverySeconds = 1.5f;

        public static bool IsPlayerInsideMarkedBlast(Vector3 markedPoint, Vector3 playerPosition, float radius)
        {
            var flat = playerPosition - markedPoint;
            flat.y = 0f;
            return flat.sqrMagnitude <= radius * radius;
        }

        public static bool ShouldApplyMarkedAreaDamage(in BossAttackState state) =>
            state.Attack == BossAttackKind.MarkedBombardment &&
            state.Phase == BossPhase.Execute &&
            state.PhaseTimer > 0f;

        public static bool CanDealDamage(in BossAttackState state) =>
            state.Phase == BossPhase.Execute && state.PhaseTimer > 0f;

        public static bool IsMovementPaused(in BossAttackState state) =>
            state.Phase == BossPhase.Telegraph ||
            (state.Phase == BossPhase.Recovery && state.PhaseTimer > RecoveryPauseFloor);

        public static bool IsChargeCommit(in BossAttackState state) =>
            state.Attack == BossAttackKind.CommittedCharge &&
            state.Phase == BossPhase.Execute;

        public static void BeginRun(ref BossAttackState state, BossConfig config)
        {
            state = default;
            state.Phase = BossPhase.Recovery;
            state.PhaseTimer = config.startRecoverySeconds;
        }

        public static void SetEnraged(ref BossAttackState state, bool enraged) => state.Enraged = enraged;

        /// <summary>A lower health phase is only taken up during a rest; it starts when that rest ends.</summary>
        public static void ApplyHealthPhase(ref BossAttackState state, float hpRatio, BossConfig config)
        {
            var target = hpRatio > config.phase2Threshold ? 1 : hpRatio > config.phase3Threshold ? 2 : 3;
            if (target > state.ScriptPhase && state.Phase == BossPhase.Recovery)
            {
                state.PendingHpPhase = target;
                state.RotationStep = 0;
            }
            else if (state.ScriptPhase == 0)
                state.ScriptPhase = 1;
        }

        public static void Tick(ref BossAttackState state, float dt, Vector3 bossPosition, Vector3 playerPosition, BossConfig config)
        {
            if (state.PhaseTimer > 0f)
                state.PhaseTimer -= dt;

            if (state.PhaseTimer > 0f)
                return;

            switch (state.Phase)
            {
                case BossPhase.Recovery:
                    if (state.PendingHpPhase > state.ScriptPhase)
                        state.ScriptPhase = Mathf.RoundToInt(state.PendingHpPhase);
                    BeginTelegraph(ref state, PickNextAttack(ref state, config), bossPosition, playerPosition, config);
                    break;
                case BossPhase.Telegraph:
                    state.Phase = BossPhase.Execute;
                    state.PhaseTimer = ExecuteSeconds(state.Attack, config);
                    break;
                case BossPhase.Execute:
                    EnterRecovery(ref state, config);
                    break;
            }
        }

        public static float TelegraphSeconds(BossAttackKind attack, BossConfig c) => attack switch
        {
            BossAttackKind.AimedSpread => c.spread.telegraphSeconds,
            BossAttackKind.CommittedCharge => c.charge.telegraphSeconds,
            BossAttackKind.MarkedBombardment => c.bombard.telegraphSeconds,
            BossAttackKind.ShellSlam => c.slam.telegraphSeconds,
            _ => NoAttackTelegraphSeconds
        };

        public static float ExecuteSeconds(BossAttackKind attack, BossConfig c) => attack switch
        {
            BossAttackKind.AimedSpread => c.spread.executeSeconds,
            BossAttackKind.CommittedCharge => c.charge.executeSeconds,
            BossAttackKind.MarkedBombardment => c.bombard.executeSeconds,
            BossAttackKind.ShellSlam => c.slam.executeSeconds,
            _ => NoAttackExecuteSeconds
        };

        public static float RecoverySeconds(BossAttackKind attack, BossConfig c) => attack switch
        {
            BossAttackKind.AimedSpread => c.spread.recoverySeconds,
            BossAttackKind.CommittedCharge => c.charge.recoverySeconds,
            BossAttackKind.MarkedBombardment => c.bombard.recoverySeconds,
            BossAttackKind.ShellSlam => c.slam.recoverySeconds,
            _ => NoAttackRecoverySeconds
        };

        /// <summary>The rotation for a health phase; an empty one (rejected by validation) falls back to V3's.</summary>
        public static BossAttackKind[] RotationFor(int scriptPhase, BossConfig config)
        {
            var script = config.RotationFor(scriptPhase);
            if (script != null && script.Length > 0) return script;
            return scriptPhase <= 1 ? BossConfig.V3Phase1Rotation : scriptPhase == 2 ? BossConfig.V3Phase2Rotation : BossConfig.V3Phase3Rotation;
        }

        static BossAttackKind PickNextAttack(ref BossAttackState state, BossConfig config)
        {
            if (state.QueuedFollowUp != BossAttackKind.None)
            {
                var follow = state.QueuedFollowUp;
                state.QueuedFollowUp = BossAttackKind.None;
                return follow;
            }

            if (state.Phase3ChargePassPending)
            {
                state.Phase3ChargePassPending = false;
                return BossAttackKind.CommittedCharge;
            }

            var script = RotationFor(state.ScriptPhase, config);
            var attack = script[state.RotationStep % script.Length];
            state.RotationStep++;

            if (state.ScriptPhase >= FinalScriptPhase && attack == BossAttackKind.CommittedCharge && config.charge.finalPhaseSecondCharge)
                state.Phase3ChargePassPending = true;

            return attack;
        }

        static void BeginTelegraph(ref BossAttackState state, BossAttackKind attack, Vector3 bossPosition, Vector3 playerPosition, BossConfig config)
        {
            state.Attack = attack;
            state.Phase = BossPhase.Telegraph;
            state.PhaseTimer = TelegraphSeconds(attack, config);

            var toPlayer = playerPosition - bossPosition;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
                toPlayer = Vector3.forward;
            state.CommittedDirection = toPlayer.normalized;
            state.MarkedPoint = playerPosition;
        }

        static void EnterRecovery(ref BossAttackState state, BossConfig config)
        {
            state.Phase = BossPhase.Recovery;
            var recovery = RecoverySeconds(state.Attack, config);
            if ((state.Attack != BossAttackKind.ShellSlam || config.enragedRecoveryAppliesToShellSlam)
                && (state.Enraged || state.ScriptPhase >= FinalScriptPhase))
                recovery *= config.enragedRecoveryScale;
            state.PhaseTimer = recovery;
            state.Attack = BossAttackKind.None;
        }
    }

    /// <summary>
    /// Port of V3 BarnacleKingBossAttackDirector (M7): drives the boss's telegraphed spread / charge / bombardment /
    /// slam pattern during the boss fight. Ticked where V3 ticked it (first in the legacy enemy-world tick, before the
    /// enemy world moves the boss) with that frame's dt.
    /// V4 changes: a plain class over contracts instead of a MonoBehaviour on /Systems reading RunManager, EnemyWorld
    /// and the ship directly; the constants come from <see cref="BossConfig"/>, read live each tick; the marked circle
    /// is drawn by a presenter from <see cref="IBossView"/> instead of by the director; the toast cooldown counts the
    /// tick's dt (V3 read Time.deltaTime, the same value in its Update). V3's first gate ("stage profile active or weapon
    /// playtest active") is the optional <c>stageActive</c> function; without one the director is always enabled.
    /// Like V3 it draws no random numbers, and the toast cooldown is not reset between runs (V3 kept it on the
    /// component).
    /// </summary>
    public sealed class BossDirector : IBossView
    {
        readonly IBossControl _bosses;
        readonly IHostileProjectileSink _projectiles;
        readonly IPlayerDamageSink _player;
        readonly IBossEffects _effects;
        readonly IRunStatus _run;
        readonly Func<BossConfig> _config;
        readonly Func<bool> _objectiveSecured;
        readonly Func<bool> _stageActive;

        BossAttackState _state;
        BossConfig _defaultConfig;
        int _bossSlot = -1;
        bool _active;
        bool _executeFired;
        float _toastCooldown;
        bool _telegraphVisible;
        Vector3 _telegraphCenter;
        float _telegraphRadius;

        public BossDirector(IBossControl bosses, IHostileProjectileSink projectiles, IPlayerDamageSink player, IBossEffects effects,
            IRunStatus run, Func<BossConfig> config, Func<bool> objectiveSecured, Func<bool> stageActive = null)
        {
            _bosses = bosses;
            _projectiles = projectiles;
            _player = player;
            _effects = effects;
            _run = run;
            _config = config;
            _objectiveSecured = objectiveSecured;
            _stageActive = stageActive;
        }

        public BossAttackState State => _state;
        public int BossSlot => _bossSlot;

        // ---- IBossView ----------------------------------------------------------------------------------------
        public bool Active => _active;
        public BossAttackKind CurrentAttack => _state.Attack;
        public BossPhase Phase => _state.Phase;
        public float PhaseTimer => _state.PhaseTimer;
        public int ScriptPhase => _state.ScriptPhase;
        public bool Enraged => _state.Enraged;
        public Vector3 MarkedPoint => _state.MarkedPoint;
        public bool MarkedTelegraphVisible => _telegraphVisible;
        public Vector3 MarkedTelegraphCenter => _telegraphCenter;
        public float MarkedTelegraphRadius => _telegraphRadius;

        /// <summary>The live config; a default one (V3's values) if the config set has none.</summary>
        BossConfig Config()
        {
            var config = _config != null ? _config() : null;
            if (config != null) return config;
            if (_defaultConfig == null)
            {
                Debug.LogWarning("BossDirector: no BossConfig in the config set; using V3's values.");
                _defaultConfig = ScriptableObject.CreateInstance<BossConfig>();
                _defaultConfig.hideFlags = HideFlags.HideAndDontSave;
                _defaultConfig.configId = "boss.default";
            }
            return _defaultConfig;
        }

        public void ResetForRun()
        {
            _active = false;
            _bossSlot = -1;
            _executeFired = false;
            _telegraphVisible = false;
            BossAttackLogic.BeginRun(ref _state, Config());
            _bosses?.ClearAttackControl();
        }

        public void Tick(float dt)
        {
            if (_stageActive != null && !_stageActive()) return;
            if (_run == null || !_run.Exists || _run.IsPaused || _run.IsRunEnded) return;
            if (_bosses == null || !_bosses.Exists || _player == null || !_player.Exists) return;
            if (!_run.IsGameplayActive) return;
            if (_objectiveSecured != null && _objectiveSecured())
            {
                if (_active) ResetForRun();
                return;
            }

            if (!_bosses.TryFindActiveBoss(out var boss))
            {
                _bossSlot = -1;
                if (_active) ResetForRun();
                return;
            }
            _bossSlot = boss.Slot;

            var config = Config();
            if (!_active)
            {
                _active = true;
                BossAttackLogic.BeginRun(ref _state, config);
            }

            var hpRatio = boss.MaxHp > 0f ? boss.Hp / boss.MaxHp : 1f;
            BossAttackLogic.SetEnraged(ref _state, hpRatio <= config.enrageThreshold);
            BossAttackLogic.ApplyHealthPhase(ref _state, hpRatio, config);

            var playerPos = _player.Position;
            var bossPos = boss.Position;
            var previousPhase = _state.Phase;
            BossAttackLogic.Tick(ref _state, dt, bossPos, playerPos, config);

            if (_state.Phase == BossPhase.Execute && previousPhase != BossPhase.Execute)
                _executeFired = false;

            UpdateMarkedTelegraph(config);
            ApplyMovementControl(boss, config);
            if (BossAttackLogic.CanDealDamage(_state) && !_executeFired)
            {
                FireAttack(boss, config);
                _executeFired = true;
            }

            MaybeShowTelegraphToast(dt, config);
        }

        void ApplyMovementControl(in BossSnapshot boss, BossConfig config)
        {
            if (BossAttackLogic.IsMovementPaused(_state))
            {
                _bosses.SetAttackControl(_bossSlot, pauseMovement: true, forcedVelocity: Vector3.zero, useForced: false);
                return;
            }

            if (BossAttackLogic.IsChargeCommit(_state))
            {
                var vel = _state.CommittedDirection * (boss.MoveSpeed * config.charge.speedMultiplier);
                _bosses.SetAttackControl(_bossSlot, pauseMovement: false, forcedVelocity: vel, useForced: true);
                return;
            }

            _bosses.ClearAttackControl();
        }

        void FireAttack(in BossSnapshot boss, BossConfig config)
        {
            switch (_state.Attack)
            {
                case BossAttackKind.AimedSpread:
                    FireSpread(boss, config.spread);
                    break;
                case BossAttackKind.MarkedBombardment:
                    ApplyMarkedBlast(boss, config.bombard);
                    break;
                case BossAttackKind.ShellSlam:
                    ApplyShellSlam(boss, config.slam);
                    break;
            }
        }

        void ApplyShellSlam(in BossSnapshot boss, BossSlamAttack slam)
        {
            var center = _state.MarkedPoint;
            center.y = 0f;
            // As V3: no alive check here (the ship's damage receiver ignores hits once sunk).
            if (BossAttackLogic.IsPlayerInsideMarkedBlast(center, _player.Position, slam.radius))
                _player.Damage(boss.Damage * slam.damageMultiplier, center);
            _effects?.Explosion(center, slam.radius);
        }

        void FireSpread(in BossSnapshot boss, BossSpreadAttack spread)
        {
            if (_projectiles == null || !_projectiles.Exists) return;

            var baseDir = _state.CommittedDirection;
            var count = spread.shotCount;
            var spreadDeg = spread.spreadDegrees;
            for (var i = 0; i < count; i++)
            {
                // V3's expression (count 5); a single shot (V4 only) goes straight.
                var t = count > 1 ? (i - (count - 1) * 0.5f) / (count - 1) : 0f;
                var dir = Quaternion.Euler(0f, t * spreadDeg, 0f) * baseDir;
                SpawnShot(boss, dir, spread.projectileSpeed, boss.Damage * spread.damageMultiplier, spread);
            }
        }

        void ApplyMarkedBlast(in BossSnapshot boss, BossBombardAttack bombard)
        {
            if (!BossAttackLogic.ShouldApplyMarkedAreaDamage(_state)) return;
            if (!_player.Exists || !_player.IsAlive) return;

            if (BossAttackLogic.IsPlayerInsideMarkedBlast(_state.MarkedPoint, _player.Position, bombard.radius))
            {
                var damage = boss.Damage * bombard.damageMultiplier;
                _player.Damage(damage, _state.MarkedPoint);
            }

            _telegraphVisible = false;
        }

        void SpawnShot(in BossSnapshot boss, Vector3 dir, float speed, float damage, BossSpreadAttack spread)
        {
            var muzzle = boss.Position + dir * (boss.Radius + spread.muzzleForwardOffset) + Vector3.up * spread.muzzleHeight;
            _projectiles.TrySpawn(muzzle, dir, speed, damage, spread.projectileLifeSeconds, spread.projectileHitRadius, spread.projectileVisualRadius);
        }

        void UpdateMarkedTelegraph(BossConfig config)
        {
            if (_state.Phase != BossPhase.Telegraph
                || (_state.Attack != BossAttackKind.MarkedBombardment
                    && _state.Attack != BossAttackKind.ShellSlam))
            {
                _telegraphVisible = false;
                return;
            }

            _telegraphRadius = _state.Attack == BossAttackKind.ShellSlam ? config.slam.radius : config.bombard.radius;
            _telegraphCenter = _state.MarkedPoint;
            _telegraphVisible = true;
        }

        void MaybeShowTelegraphToast(float dt, BossConfig config)
        {
            if (_state.Phase != BossPhase.Telegraph) return;
            if (_state.Attack == BossAttackKind.MarkedBombardment) return;
            _toastCooldown -= dt;
            if (_toastCooldown > 0f) return;
            _toastCooldown = config.toastIntervalSeconds;
            var label = _state.Attack switch
            {
                BossAttackKind.AimedSpread => config.spread.toastText,
                BossAttackKind.CommittedCharge => config.charge.toastText,
                BossAttackKind.ShellSlam => config.slam.toastText,
                _ => null
            };
            if (!string.IsNullOrEmpty(label))
                _effects?.Toast(label, config.toastDurationSeconds);
        }
    }
}

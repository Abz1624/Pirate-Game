using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Barnacle King telegraphed spread/charge/bombard patterns during boss fight (Saltwind C05).</summary>
    public sealed class BarnacleKingBossAttackDirector : MonoBehaviour
    {
        const float ChargeSpeedMult = 2.6f;
        const float SpreadProjectileSpeed = 14f;
        const float BombardProjectileSpeed = 11f;

        EnemyWorld _enemyWorld;
        PlayerShipController _player;
        BarnacleKingAttackState _state;
        int _bossSlot = -1;
        bool _active;
        bool _executeFired;
        float _toastCooldown;

        public static BarnacleKingBossAttackDirector Ensure(EnemyWorld world, PlayerShipController player)
        {
            var existing = FindFirstObjectByType<BarnacleKingBossAttackDirector>();
            if (existing != null)
            {
                existing.Initialize(world, player);
                return existing;
            }

            var systems = GameObject.Find("Systems");
            var go = systems != null ? systems : new GameObject("BarnacleKingBossAttackDirector");
            var component = go.GetComponent<BarnacleKingBossAttackDirector>() ??
                            go.AddComponent<BarnacleKingBossAttackDirector>();
            component.Initialize(world, player);
            return component;
        }

        public void Initialize(EnemyWorld world, PlayerShipController player)
        {
            _enemyWorld = world;
            _player = player;
        }

        public void ResetForRun()
        {
            _active = false;
            _bossSlot = -1;
            _executeFired = false;
            BarnacleKingBossAttackLogic.BeginRun(ref _state);
            _enemyWorld?.ClearBossAttackControl();
        }

        public void Tick(float dt)
        {
            if (!StageBalanceProfileContext.IsActive) return;
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded)
                return;
            if (_enemyWorld == null || _player == null) return;
            if (!RunManager.Instance.IsGameplayActive) return;
            if (RunEncounterTracker.BossObjectiveSecured)
            {
                if (_active) ResetForRun();
                return;
            }

            if (!_enemyWorld.TryFindActiveBoss(out _bossSlot, out var boss))
            {
                if (_active) ResetForRun();
                return;
            }

            if (!_active)
            {
                _active = true;
                BarnacleKingBossAttackLogic.BeginRun(ref _state);
            }

            var enraged = boss.maxHp > 0f && boss.hp <= boss.maxHp * 0.5f;
            BarnacleKingBossAttackLogic.SetEnraged(ref _state, enraged);

            var playerPos = _player.Position;
            var bossPos = boss.position;
            var previousPhase = _state.Phase;
            BarnacleKingBossAttackLogic.Tick(ref _state, dt, bossPos, playerPos);

            if (_state.Phase == BarnacleKingBossPhase.Execute && previousPhase != BarnacleKingBossPhase.Execute)
                _executeFired = false;

            ApplyMovementControl(boss);
            if (BarnacleKingBossAttackLogic.CanDealDamage(_state) && !_executeFired)
            {
                FireAttack(boss);
                _executeFired = true;
            }

            MaybeShowTelegraphToast();
        }

        void ApplyMovementControl(in EnemyAgent boss)
        {
            if (BarnacleKingBossAttackLogic.IsMovementPaused(_state))
            {
                _enemyWorld.SetBossAttackControl(_bossSlot, pauseMovement: true, forcedVelocity: Vector3.zero, useForced: false);
                return;
            }

            if (BarnacleKingBossAttackLogic.IsChargeCommit(_state))
            {
                var vel = _state.CommittedDirection * (boss.moveSpeed * ChargeSpeedMult);
                _enemyWorld.SetBossAttackControl(_bossSlot, pauseMovement: false, forcedVelocity: vel, useForced: true);
                return;
            }

            _enemyWorld.ClearBossAttackControl();
        }

        void FireAttack(in EnemyAgent boss)
        {
            var hostiles = HostileProjectileWorld.Instance;
            if (hostiles == null) return;

            switch (_state.Attack)
            {
                case BarnacleKingAttackKind.AimedSpread:
                    FireSpread(boss, hostiles);
                    break;
                case BarnacleKingAttackKind.MarkedBombardment:
                    FireBombard(boss, hostiles);
                    break;
            }
        }

        void FireSpread(in EnemyAgent boss, HostileProjectileWorld hostiles)
        {
            var baseDir = _state.CommittedDirection;
            const int count = 5;
            const float spreadDeg = 12f;
            for (var i = 0; i < count; i++)
            {
                var t = (i - (count - 1) * 0.5f) / (count - 1);
                var dir = Quaternion.Euler(0f, t * spreadDeg, 0f) * baseDir;
                SpawnShot(hostiles, boss, dir, SpreadProjectileSpeed, boss.damage * 0.55f);
            }
        }

        void FireBombard(in EnemyAgent boss, HostileProjectileWorld hostiles)
        {
            var toMark = _state.MarkedPoint - boss.position;
            toMark.y = 0f;
            if (toMark.sqrMagnitude < 0.01f)
                toMark = _state.CommittedDirection;
            var dir = toMark.normalized;
            for (var i = 0; i < 3; i++)
            {
                var jitter = Quaternion.Euler(0f, (i - 1) * 8f, 0f) * dir;
                SpawnShot(hostiles, boss, jitter, BombardProjectileSpeed, boss.damage * 0.7f);
            }
        }

        static void SpawnShot(
            HostileProjectileWorld hostiles,
            in EnemyAgent boss,
            Vector3 dir,
            float speed,
            float damage)
        {
            var muzzle = boss.position + dir * (boss.radius + 1.4f) + Vector3.up * 0.6f;
            hostiles.TrySpawn(muzzle, dir, speed, damage, 2.4f, 0.55f, 0.45f);
        }

        void MaybeShowTelegraphToast()
        {
            if (_state.Phase != BarnacleKingBossPhase.Telegraph) return;
            _toastCooldown -= Time.deltaTime;
            if (_toastCooldown > 0f) return;
            _toastCooldown = 1.2f;
            var label = _state.Attack switch
            {
                BarnacleKingAttackKind.AimedSpread => "Barnacle King — spread incoming",
                BarnacleKingAttackKind.CommittedCharge => "Barnacle King — charge incoming",
                BarnacleKingAttackKind.MarkedBombardment => "Barnacle King — bombardment incoming",
                _ => null
            };
            if (label != null)
                V3ToastHud.Instance?.Show(label, 1.1f);
        }
    }
}

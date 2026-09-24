using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Playtest;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Barnacle King telegraphed spread/charge/bombard patterns during boss fight (Saltwind C05).</summary>
    public sealed class BarnacleKingBossAttackDirector : MonoBehaviour
    {
        const float ChargeSpeedMult = 2.6f;
        const float SpreadProjectileSpeed = 14f;

        EnemyWorld _enemyWorld;
        PlayerShipController _player;
        BarnacleKingAttackState _state;
        int _bossSlot = -1;
        bool _active;
        bool _executeFired;
        float _toastCooldown;
        GameObject _markedTelegraph;
        Material _markedTelegraphMaterial;

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
            HideMarkedTelegraph();
            BarnacleKingBossAttackLogic.BeginRun(ref _state);
            _enemyWorld?.ClearBossAttackControl();
        }

        public void Tick(float dt)
        {
            if (!StageBalanceProfileContext.IsActive && !WeaponEnemyPlaytestSession.Active) return;
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

            var hpRatio = boss.maxHp > 0f ? boss.hp / boss.maxHp : 1f;
            BarnacleKingBossAttackLogic.SetEnraged(ref _state, hpRatio <= 0.3f);
            BarnacleKingBossAttackLogic.ApplyHealthPhase(ref _state, hpRatio);

            var playerPos = _player.Position;
            var bossPos = boss.position;
            var previousPhase = _state.Phase;
            BarnacleKingBossAttackLogic.Tick(ref _state, dt, bossPos, playerPos);

            if (_state.Phase == BarnacleKingBossPhase.Execute && previousPhase != BarnacleKingBossPhase.Execute)
                _executeFired = false;

            UpdateMarkedTelegraph();
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
            switch (_state.Attack)
            {
                case BarnacleKingAttackKind.AimedSpread:
                    FireSpread(boss, HostileProjectileWorld.Instance);
                    break;
                case BarnacleKingAttackKind.MarkedBombardment:
                    ApplyMarkedBlast(boss);
                    break;
                case BarnacleKingAttackKind.ShellSlam:
                    ApplyShellSlam(boss);
                    break;
            }
        }

        void ApplyShellSlam(in EnemyAgent boss)
        {
            var center = _state.MarkedPoint;
            center.y = 0f;
            if (BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(center, _player.Position, 5f))
                _player.Damage?.TakeDamage(boss.damage * 1.35f, center, false);
            SaltwindCombatEffects.PlayExplosion(center, 5f);
        }

        void FireSpread(in EnemyAgent boss, HostileProjectileWorld hostiles)
        {
            if (hostiles == null) return;

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

        void ApplyMarkedBlast(in EnemyAgent boss)
        {
            if (!BarnacleKingBossAttackLogic.ShouldApplyMarkedAreaDamage(_state)) return;
            if (_player?.Damage == null || !_player.Damage.IsAlive) return;

            if (BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(_state.MarkedPoint, _player.Position))
            {
                var damage = boss.damage * 1.15f;
                _player.Damage.TakeDamage(damage, _state.MarkedPoint, false);
            }

            HideMarkedTelegraph();
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

        void UpdateMarkedTelegraph()
        {
            if (_state.Phase != BarnacleKingBossPhase.Telegraph
                || (_state.Attack != BarnacleKingAttackKind.MarkedBombardment
                    && _state.Attack != BarnacleKingAttackKind.ShellSlam))
            {
                HideMarkedTelegraph();
                return;
            }

            EnsureMarkedTelegraph();
            var radius = _state.Attack == BarnacleKingAttackKind.ShellSlam
                ? 5f
                : BarnacleKingBossAttackLogic.MarkedBombardmentRadius;
            _markedTelegraph.transform.position = _state.MarkedPoint + Vector3.up * 0.08f;
            _markedTelegraph.transform.localScale = new Vector3(radius * 2f, 0.12f, radius * 2f);
            _markedTelegraph.SetActive(true);
        }

        void EnsureMarkedTelegraph()
        {
            if (_markedTelegraph != null) return;
            _markedTelegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _markedTelegraph.name = "BarnacleKingMarkedTelegraph";
            var collider = _markedTelegraph.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _markedTelegraphMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            _markedTelegraphMaterial.color = new Color(1f, 0.35f, 0.1f, 0.55f);
            _markedTelegraph.GetComponent<Renderer>().sharedMaterial = _markedTelegraphMaterial;
        }

        void HideMarkedTelegraph()
        {
            if (_markedTelegraph != null)
                _markedTelegraph.SetActive(false);
        }

        void OnDestroy()
        {
            if (_markedTelegraph != null)
                Destroy(_markedTelegraph);
            if (_markedTelegraphMaterial != null)
                Destroy(_markedTelegraphMaterial);
        }

        void MaybeShowTelegraphToast()
        {
            if (_state.Phase != BarnacleKingBossPhase.Telegraph) return;
            if (_state.Attack == BarnacleKingAttackKind.MarkedBombardment) return;
            _toastCooldown -= Time.deltaTime;
            if (_toastCooldown > 0f) return;
            _toastCooldown = 1.2f;
            var label = _state.Attack switch
            {
                BarnacleKingAttackKind.AimedSpread => "Barnacle King — spread incoming",
                BarnacleKingAttackKind.CommittedCharge => "Barnacle King — charge incoming",
                BarnacleKingAttackKind.ShellSlam => "Barnacle King — shell slam",
                _ => null
            };
            if (label != null)
                V3ToastHud.Instance?.Show(label, 1.1f);
        }
    }
}

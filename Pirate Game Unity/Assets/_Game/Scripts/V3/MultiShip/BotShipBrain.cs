using HighSeas.Enemies;
using HighSeas.Platform;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Bot steering: stay on a short leash around the human player ship.</summary>
    [DisallowMultipleComponent]
    public sealed class BotShipBrain : MonoBehaviour, IShipSteerInput
    {
        const float HullRadius = 1.15f;
        const float AvoidanceProbe = 9f;
        const float EnemyEngageRange = 16f;
        const float EnemyMaxDistFromPlayer = 15f;

        [SerializeField] float orbitDistance = 12f;
        [SerializeField] float followDistance = 9f;

        PlayerShipController _ship;
        PlayerShipController _followTarget;
        V3ShipTeam _team;
        EnemyWorld _enemies;
        int _steerBias;
        Vector3 _formationOffset;

        public void Initialize(PlayerShipController ship, V3ShipTeam team, PlayerShipController followTarget)
        {
            _ship = ship;
            _team = team;
            _followTarget = followTarget;
            _steerBias = ship != null ? ship.GetInstanceID() : 0;
            _formationOffset = team == V3ShipTeam.RivalBot
                ? Vector3.zero
                : new Vector3(((_steerBias & 2) == 0 ? 7f : -7f), 0f, -4f);
            if (_ship != null)
                _ship.ConfigureBotControl(this);
        }

        public void SetFormationOffset(Vector3 localOffset) => _formationOffset = localOffset;

        void Start() => _enemies = FindFirstObjectByType<EnemyWorld>();

        void LateUpdate()
        {
            RecoverFromIslandOverlap();
            EnforcePlayerLeash();
        }

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || _ship.Damage == null || !_ship.Damage.IsAlive)
                return Vector2.zero;

            if (_followTarget == null || !_followTarget.Damage.IsAlive)
                return Vector2.zero;

            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(_ship.Position, HullRadius))
                return EscapeIslandSteer();

            if (V3MultiShipBotBounds.IsBeyondHardLeash(_ship.Position, _followTarget.Position))
                return SteerToward(GetFormationWorldPosition(), followDistance, strafe: false);

            Vector2 input;
            if (V3RunSession.IsPvP)
                input = SteerPvP();
            else if (V3RunSession.IsCoOp)
                input = SteerCoOp();
            else
                return Vector2.zero;

            return ApplyIslandAvoidance(input);
        }

        Vector3 GetFormationWorldPosition()
        {
            var playerPos = _followTarget.Position;
            var right = Vector3.Cross(Vector3.up, _followTarget.MovementForward).normalized;
            var forward = _followTarget.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            var offset = _formationOffset;
            if (offset.sqrMagnitude > 0.01f)
                return playerPos + right * offset.x + forward.normalized * offset.z;

            return playerPos;
        }

        Vector3 ClampTargetNearPlayer(Vector3 worldTarget) =>
            V3MultiShipBotBounds.ClampNearPlayer(worldTarget, _followTarget.Position);

        Vector2 EscapeIslandSteer()
        {
            if (_followTarget != null)
                return SteerToward(GetFormationWorldPosition(), followDistance, strafe: false);

            var sign = (_steerBias & 1) == 0 ? 1f : -1f;
            return new Vector2(sign, -0.5f);
        }

        void RecoverFromIslandOverlap()
        {
            if (_ship == null || !IslandObstacles.IsAvailable)
                return;

            if (!IslandObstacles.IsPositionBlocked(_ship.Position, HullRadius))
                return;

            var height = IslandObstacles.GetCollisionHeight(HullRadius);
            var freed = IslandObstacles.Depenetrate(_ship.Position, HullRadius, height);
            if (_followTarget != null)
                freed = V3MultiShipBotBounds.PullOntoLeash(freed, _followTarget.Position, HullRadius);
            else
                freed = V3MultiShipBotBounds.ClampShipPosition(freed, HullRadius);
            _ship.SetWorldPosition(freed);
        }

        void EnforcePlayerLeash()
        {
            if (_ship == null || _followTarget == null || !V3RunSession.IsMultiShip)
                return;

            var pulled = V3MultiShipBotBounds.PullOntoLeash(
                _ship.Position,
                _followTarget.Position,
                HullRadius);

            if ((pulled - _ship.Position).sqrMagnitude > 0.05f)
            {
                _ship.SetWorldPosition(pulled);
                _ship.ResetMotion();
            }
        }

        Vector2 SteerPvP()
        {
            var orbitPoint = ClampTargetNearPlayer(_followTarget.Position);
            return SteerToward(orbitPoint, orbitDistance, strafe: true);
        }

        Vector2 SteerCoOp()
        {
            if (TryGetEnemyNearPlayer(out var enemyPos))
                return SteerToward(ClampTargetNearPlayer(enemyPos), orbitDistance * 0.9f, strafe: true);

            return SteerToward(GetFormationWorldPosition(), followDistance, strafe: false);
        }

        bool TryGetEnemyNearPlayer(out Vector3 enemyPosition)
        {
            enemyPosition = default;
            if (_enemies == null || _followTarget == null)
                return false;

            if (!_enemies.TryGetNearest(_ship.Position, EnemyEngageRange, out var enemyIndex, false))
                return false;

            var enemyPos = _enemies.Agents[enemyIndex].position;
            var toPlayer = enemyPos - _followTarget.Position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > EnemyMaxDistFromPlayer * EnemyMaxDistFromPlayer)
                return false;

            enemyPosition = enemyPos;
            return true;
        }

        Vector2 ApplyIslandAvoidance(Vector2 input)
        {
            if (!IslandObstacles.IsAvailable || _ship == null)
                return input;

            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return input;

            var speed = Mathf.Lerp(5f, 11f, Mathf.Abs(input.y));
            var desiredVel = forward * (input.y >= 0f ? speed : -speed * 0.45f);
            var preferred = desiredVel.sqrMagnitude > 0.01f ? desiredVel.normalized : forward;

            var steered = IslandObstacles.ApplyAvoidanceSteering(
                _ship.Position,
                desiredVel,
                preferred,
                HullRadius,
                AvoidanceProbe,
                _steerBias);

            steered.y = 0f;
            if (steered.sqrMagnitude < 0.05f)
                return input;

            if (_followTarget != null)
            {
                var afterMove = _ship.Position + steered.normalized * 2f;
                if (V3MultiShipBotBounds.IsBeyondHardLeash(afterMove, _followTarget.Position))
                    return SteerToward(GetFormationWorldPosition(), followDistance, strafe: false);
            }

            var newForward = steered.normalized;
            var signedAngle = Vector3.SignedAngle(forward, newForward, Vector3.up);
            var steer = Mathf.Clamp(signedAngle / 50f, -1f, 1f);
            var throttle = input.y;
            if (Mathf.Abs(signedAngle) > 25f)
                throttle = Mathf.Min(throttle, 0.5f);

            return new Vector2(steer, Mathf.Clamp(throttle, -1f, 1f));
        }

        Vector2 SteerToward(Vector3 worldTarget, float desiredRange, bool strafe)
        {
            worldTarget = ClampTargetNearPlayer(worldTarget);

            var to = worldTarget - _ship.Position;
            to.y = 0f;
            var dist = to.magnitude;
            if (dist < 0.01f)
                return Vector2.zero;

            var forward = _ship.MovementForward;
            forward.y = 0f;
            var desiredDir = to.normalized;
            if (strafe && dist < desiredRange * 1.1f)
                desiredDir = Vector3.Cross(Vector3.up, to).normalized;

            var signedAngle = Vector3.SignedAngle(forward, desiredDir, Vector3.up);
            var steer = Mathf.Clamp(signedAngle / 55f, -1f, 1f);
            var throttle = dist > desiredRange ? 1f : dist < desiredRange * 0.65f ? -0.2f : 0.4f;
            return new Vector2(steer, Mathf.Clamp(throttle, -1f, 1f));
        }
    }
}

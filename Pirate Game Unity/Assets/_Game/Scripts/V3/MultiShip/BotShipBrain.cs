using HighSeas.Enemies;
using HighSeas.Platform;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Bot steering: co-op convoy with the human; islands handled by navigation only (no position snaps).</summary>
    [DisallowMultipleComponent]
    public sealed class BotShipBrain : MonoBehaviour, IShipSteerInput
    {
        const float AvoidanceProbe = 16f;
        const float EnemyEngageRange = 16f;
        const float EnemyMaxDistFromPlayer = 18f;
        const float FormationLeadSeconds = 0.45f;
        const float ConvoyCatchUpDistance = 14f;
        const float ConvoyCruiseDistance = 5f;

        [SerializeField] float orbitDistance = 12f;
        [SerializeField] float followDistance = 8f;

        PlayerShipController _ship;
        PlayerShipController _followTarget;
        V3ShipTeam _team;
        EnemyWorld _enemies;
        int _steerBias;
        Vector3 _formationOffset;

        float HullRadius => _ship != null ? _ship.IslandCollisionRadius : 1.15f;
        float HullHeight => _ship != null ? _ship.IslandCollisionHeight : 4.5f;

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

        const float StormEdgeSteerClearance = 18f;

        void Start() => _enemies = FindFirstObjectByType<EnemyWorld>();

        void LateUpdate()
        {
            if (_ship == null || !V3RunSession.IsMultiShip)
                return;

            if (!V3MultiShipBotBounds.IsOutsidePlayableArea(_ship.Position, HullRadius))
                return;

            var clamped = V3MultiShipBotBounds.ClampShipPosition(_ship.Position, HullRadius);
            if ((clamped - _ship.Position).sqrMagnitude < 0.0001f)
                return;

            _ship.SetWorldPosition(clamped);
        }

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || _ship.Damage == null || !_ship.Damage.IsAlive)
                return Vector2.zero;

            if (_followTarget == null || !_followTarget.Damage.IsAlive)
                return Vector2.zero;

            Vector2 input;
            if (V3RunSession.IsPvP)
                input = SteerPvP();
            else if (V3RunSession.IsCoOp)
                input = SteerCoOp();
            else
                return Vector2.zero;

            if (IslandObstacles.IsAvailable && IsHullBlocked(_ship.Position))
                input = BlendToward(input, SteerAroundIslandTowardFormation(), 0.65f);

            input = ApplyIslandAvoidance(input, GetFormationWorldPosition());
            return ApplyStormEdgeSteer(input);
        }

        Vector2 ApplyStormEdgeSteer(Vector2 input)
        {
            if (_ship == null || _followTarget == null || !V3RunSession.IsMultiShip)
                return input;

            var clearance = V3MultiShipBotBounds.ClearanceFromStormEdge(_ship.Position);
            if (clearance >= StormEdgeSteerClearance)
                return input;

            var steerInward = clearance <= 0f
                ? 0.75f
                : Mathf.Clamp01(1f - clearance / StormEdgeSteerClearance) * 0.55f;
            var towardPlayer = SteerToward(_followTarget.Position, followDistance * 1.2f, strafe: false, convoy: true);
            return BlendToward(input, towardPlayer, steerInward);
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
            var slot = offset.sqrMagnitude > 0.01f
                ? playerPos + right * offset.x + forward.normalized * offset.z
                : playerPos;

            var playerVel = _followTarget.Velocity;
            playerVel.y = 0f;
            if (playerVel.sqrMagnitude > 1f)
                slot += playerVel * FormationLeadSeconds;

            return ClampSteerTarget(slot);
        }

        Vector3 ClampSteerTarget(Vector3 worldTarget) =>
            V3MultiShipBotBounds.ClampShipPosition(worldTarget, HullRadius);

        bool IsHullBlocked(Vector3 position) =>
            IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, HullRadius, HullHeight);

        bool IsDirectPathBlocked(Vector3 target, int samples = 6)
        {
            if (!IslandObstacles.IsAvailable || _ship == null)
                return false;

            var from = _ship.Position;
            for (var i = 1; i <= samples; i++)
            {
                var t = i / (float)samples;
                if (IsHullBlocked(Vector3.Lerp(from, target, t)))
                    return true;
            }

            return false;
        }

        Vector3 PickReachableTarget(Vector3 primary, Vector3 fallback)
        {
            primary = ClampSteerTarget(primary);
            if (!IsDirectPathBlocked(primary))
                return primary;

            fallback = ClampSteerTarget(fallback);
            return !IsDirectPathBlocked(fallback) ? fallback : primary;
        }

        Vector2 SteerCoOp()
        {
            var formation = GetFormationWorldPosition();
            if (TryGetEnemyNearPlayer(out var enemyPos))
            {
                var combatTarget = PickReachableTarget(enemyPos, formation);
                if (!IsDirectPathBlocked(combatTarget))
                    return SteerToward(combatTarget, orbitDistance * 0.9f, strafe: true, convoy: false);
            }

            return SteerConvoyFormation(formation);
        }

        Vector2 SteerConvoyFormation(Vector3 formationSlot)
        {
            var to = formationSlot - _ship.Position;
            to.y = 0f;
            var dist = to.magnitude;

            if (dist < ConvoyCruiseDistance)
                return MatchPlayerConvoy();

            return SteerToward(formationSlot, followDistance, strafe: false, convoy: true);
        }

        Vector2 MatchPlayerConvoy()
        {
            var playerForward = _followTarget.MovementForward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.01f)
                playerForward = Vector3.forward;

            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return new Vector2(0f, 0.45f);

            var signedAngle = Vector3.SignedAngle(forward, playerForward.normalized, Vector3.up);
            var steer = Mathf.Clamp(signedAngle / 48f, -1f, 1f);

            var playerVel = _followTarget.Velocity;
            playerVel.y = 0f;
            var throttle = Mathf.Clamp01(playerVel.magnitude / 22f);
            if (throttle < 0.2f)
                throttle = 0.42f;

            return new Vector2(steer, throttle);
        }

        Vector2 SteerAroundIslandTowardFormation()
        {
            var formation = GetFormationWorldPosition();
            var toFormation = formation - _ship.Position;
            toFormation.y = 0f;
            if (toFormation.sqrMagnitude < 0.01f)
                return EscapeIslandSteer();

            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return new Vector2(0f, 0.5f);

            var desiredVel = toFormation.normalized * 8f;
            var steered = IslandObstacles.ApplyAvoidanceSteering(
                _ship.Position,
                desiredVel,
                toFormation.normalized,
                HullRadius,
                AvoidanceProbe,
                _steerBias);

            steered.y = 0f;
            if (steered.sqrMagnitude < 0.05f)
                return EscapeIslandSteer();

            var signedAngle = Vector3.SignedAngle(forward, steered.normalized, Vector3.up);
            return new Vector2(Mathf.Clamp(signedAngle / 45f, -1f, 1f), 0.55f);
        }

        Vector2 EscapeIslandSteer()
        {
            var formation = GetFormationWorldPosition();
            var toFormation = formation - _ship.Position;
            toFormation.y = 0f;

            Vector3 escapeDir;
            if (toFormation.sqrMagnitude > 0.25f)
                escapeDir = toFormation.normalized;
            else
            {
                var sign = (_steerBias & 1) == 0 ? 1f : -1f;
                return new Vector2(sign, 0.5f);
            }

            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return new Vector2(0f, 0.5f);

            var signedAngle = Vector3.SignedAngle(forward, escapeDir, Vector3.up);
            return new Vector2(Mathf.Clamp(signedAngle / 42f, -1f, 1f), 0.5f);
        }

        Vector2 SteerPvP()
        {
            var orbitPoint = PickReachableTarget(_followTarget.Position, GetFormationWorldPosition());
            return SteerToward(orbitPoint, orbitDistance, strafe: true, convoy: false);
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

        Vector2 ApplyIslandAvoidance(Vector2 input, Vector3 preferredWorldTarget)
        {
            if (!IslandObstacles.IsAvailable || _ship == null)
                return input;

            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return input;

            var speed = Mathf.Lerp(5f, 11f, Mathf.Abs(input.y));
            var desiredVel = forward * (input.y >= 0f ? speed : -speed * 0.45f);

            var toPreferred = preferredWorldTarget - _ship.Position;
            toPreferred.y = 0f;
            var preferred = toPreferred.sqrMagnitude > 0.5f
                ? toPreferred.normalized
                : desiredVel.sqrMagnitude > 0.01f ? desiredVel.normalized : forward;

            var steered = IslandObstacles.ApplyAvoidanceSteering(
                _ship.Position,
                desiredVel,
                preferred,
                HullRadius,
                AvoidanceProbe,
                _steerBias);

            steered.y = 0f;
            if (steered.sqrMagnitude < 0.05f)
            {
                if (IsAheadBlocked(forward))
                    return SteerAroundIslandTowardFormation();
                return input;
            }

            if (IsHullBlocked(_ship.Position + steered.normalized * 2.5f))
                return BlendToward(input, SteerAroundIslandTowardFormation(), 0.7f);

            var newForward = steered.normalized;
            var signedAngle = Vector3.SignedAngle(forward, newForward, Vector3.up);
            var steer = Mathf.Clamp(signedAngle / 50f, -1f, 1f);
            var throttle = input.y;
            if (Mathf.Abs(signedAngle) > 22f)
                throttle = Mathf.Min(throttle, 0.55f);
            if (IsAheadBlocked(forward))
                throttle = Mathf.Min(throttle, 0.4f);

            return new Vector2(steer, Mathf.Clamp(throttle, -0.25f, 1f));
        }

        static Vector2 BlendToward(Vector2 from, Vector2 to, float t)
        {
            t = Mathf.Clamp01(t);
            return Vector2.Lerp(from, to, t);
        }

        bool IsAheadBlocked(Vector3 forward)
        {
            if (!IslandObstacles.IsAvailable || _ship == null)
                return false;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return false;

            return IsHullBlocked(_ship.Position + forward.normalized * (HullRadius + 4f));
        }

        Vector2 SteerToward(Vector3 worldTarget, float desiredRange, bool strafe, bool convoy)
        {
            worldTarget = ClampSteerTarget(worldTarget);

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

            float throttle;
            if (convoy)
            {
                throttle = dist > ConvoyCatchUpDistance
                    ? 1f
                    : Mathf.Lerp(0.5f, 0.95f, dist / ConvoyCatchUpDistance);
                if (IsDirectPathBlocked(worldTarget))
                    throttle = Mathf.Min(throttle, 0.55f);
            }
            else
            {
                throttle = dist > desiredRange ? 1f : dist < desiredRange * 0.65f ? -0.15f : 0.4f;
                if (IsDirectPathBlocked(worldTarget))
                    throttle = Mathf.Min(throttle, 0.5f);
            }

            return new Vector2(steer, Mathf.Clamp(throttle, -0.2f, 1f));
        }
    }
}

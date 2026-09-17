using HighSeas.Enemies;
using HighSeas.Platform;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Simple bot steering for PvP rivals and co-op ally ships.</summary>
    [DisallowMultipleComponent]
    public sealed class BotShipBrain : MonoBehaviour, IShipSteerInput
    {
        [SerializeField] float engageRange = 28f;
        [SerializeField] float orbitDistance = 14f;
        [SerializeField] float followDistance = 10f;

        PlayerShipController _ship;
        PlayerShipController _followTarget;
        V3ShipTeam _team;
        EnemyWorld _enemies;

        public void Initialize(PlayerShipController ship, V3ShipTeam team, PlayerShipController followTarget)
        {
            _ship = ship;
            _team = team;
            _followTarget = followTarget;
            if (_ship != null)
                _ship.ConfigureBotControl(this);
        }

        void Start() => _enemies = FindFirstObjectByType<EnemyWorld>();

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || _ship.Damage == null || !_ship.Damage.IsAlive)
                return Vector2.zero;

            if (V3RunSession.IsPvP)
                return SteerPvP();

            if (V3RunSession.IsCoOp)
                return SteerCoOp();

            return Vector2.zero;
        }

        Vector2 SteerPvP()
        {
            var target = _followTarget;
            if (target == null || !target.Damage.IsAlive)
                return Vector2.zero;

            return SteerToward(target.Position, orbitDistance, strafe: true);
        }

        Vector2 SteerCoOp()
        {
            if (_enemies != null && _enemies.TryGetNearest(_ship.Position, engageRange, out var enemyIndex, false))
            {
                var pos = _enemies.Agents[enemyIndex].position;
                return SteerToward(pos, orbitDistance * 0.85f, strafe: true);
            }

            if (_followTarget != null)
            {
                var offset = (_ship.Position - _followTarget.Position);
                offset.y = 0f;
                if (offset.magnitude > followDistance * 1.4f)
                    return SteerToward(_followTarget.Position + offset.normalized * followDistance, followDistance, strafe: false);
            }

            return new Vector2(0f, 0.35f);
        }

        Vector2 SteerToward(Vector3 worldTarget, float desiredRange, bool strafe)
        {
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
            var throttle = dist > desiredRange ? 1f : dist < desiredRange * 0.65f ? -0.25f : 0.45f;
            return new Vector2(steer, Mathf.Clamp(throttle, -1f, 1f));
        }
    }
}

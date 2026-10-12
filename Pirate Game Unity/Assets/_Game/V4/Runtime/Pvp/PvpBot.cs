using System.Collections.Generic;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.V4.Pvp
{
    /// <summary>
    /// Playtest (3 Oct, owner): the practice bot's ship, simulated on the host's client (the server expects its state, hits,
    /// pickups and sinking from the host with asBot). A simple captain: it closes to 12 to 18 m and circles, chases barrels
    /// it is nearer to (health barrels when hurt), keeps inside the storm ring, boosts now and then, and fires a cannon
    /// volley every so often at where the player will be (a dodgeable shot: it hits only if the player is still near the
    /// aim point when it lands). Its damage grows with its level (XP barrels).
    /// </summary>
    public sealed class PvpBot
    {
        public const string WeaponId = "bot_cannons";
        const float OrbitMin = 12f, OrbitMax = 18f;
        const float TurnDegreesPerSecond = 110f;

        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Hp, MaxHp;
        public int Level = 1;
        public bool Alive = true;
        public bool Boosting => _boostLeft > 0f;
        /// <summary>The bot's top speed (set from the player's ship so the two are even).</summary>
        public float Speed = 13f;
        /// <summary>The bot's incoming hits (the same PvP rules as a player's).</summary>
        public readonly PvpHitWindow Hits = new PvpHitWindow();

        readonly RoguePirates.V4.Foundation.Core.RunStream _rng; // V5 M9a (I9a.2): seeded from the match seed
        float _boostLeft, _boostCooldown = 6f, _strafeSign = 1f, _strafeTimer = 4f, _stuckTimer;

        public PvpBot(int seed, Vector3 position, float yaw, float maxHp)
        {
            _rng = new RoguePirates.V4.Foundation.Core.RunStream(seed);
            Position = position;
            Yaw = yaw;
            Hp = MaxHp = maxHp;
        }

        float Rand() => (float)_rng.NextDouble();

        public void LevelUp()
        {
            Level++;
            var grow = MaxHp * 0.03f; // a sturdier hull each level (its Logbooks in spirit)
            MaxHp += grow;
            Hp += grow;
        }

        public void Heal(float fraction) => Hp = Mathf.Min(MaxHp, Hp + MaxHp * fraction);

        /// <summary>Moves the bot. <paramref name="barrels"/> are the live barrels (world positions).</summary>
        public void Tick(float dt, Vector3 player, bool playerAlive, Vector3 centre, float stormRadius, IEnumerable<PvpBarrel> barrels, bool canMove)
        {
            if (!Alive || !canMove)
            {
                Velocity = Vector3.zero;
                return;
            }
            _strafeTimer -= dt;
            if (_strafeTimer <= 0f) { _strafeSign = -_strafeSign; _strafeTimer = 3f + Rand() * 4f; }
            _boostCooldown -= dt;
            if (_boostLeft > 0f) _boostLeft -= dt;

            var goal = ChooseGoal(player, playerAlive, centre, stormRadius, barrels, out var wantBoost);
            if (wantBoost && _boostCooldown <= 0f) { _boostLeft = 2f; _boostCooldown = 9f + Rand() * 6f; }

            var to = goal - Position;
            to.y = 0f;
            var speed = Speed * (Boosting ? 1.5f : 1f);
            if (to.sqrMagnitude < 1f) speed *= 0.4f;
            var desiredYaw = to.sqrMagnitude > 0.01f ? PvpRules.YawOf(to) : Yaw;
            Yaw = Mathf.MoveTowardsAngle(Yaw, desiredYaw, TurnDegreesPerSecond * dt);
            var forward = PvpRules.Forward(Yaw);
            var step = forward * speed * dt;
            var next = Position + step;
            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(next, 1.6f))
            {
                // Steer around the island: try turning either way.
                var turned = false;
                foreach (var angle in new[] { 45f, -45f, 90f, -90f, 135f, -135f })
                {
                    var f = PvpRules.Forward(Yaw + angle * _strafeSign);
                    var alt = Position + f * speed * dt;
                    if (IslandObstacles.IsPositionBlocked(alt, 1.6f)) continue;
                    Yaw = Mathf.MoveTowardsAngle(Yaw, Yaw + angle * _strafeSign, TurnDegreesPerSecond * 2f * dt);
                    next = alt;
                    forward = f;
                    turned = true;
                    break;
                }
                if (!turned) { next = Position; _stuckTimer += dt; }
            }
            else _stuckTimer = 0f;
            if (_stuckTimer > 1.5f) { _strafeSign = -_strafeSign; _stuckTimer = 0f; }
            next = PlayBounds.Clamp(next, 2f);
            Velocity = dt > 0f ? (next - Position) / dt : Vector3.zero;
            Velocity.y = 0f;
            Position = new Vector3(next.x, 0f, next.z);
        }

        Vector3 ChooseGoal(Vector3 player, bool playerAlive, Vector3 centre, float stormRadius, IEnumerable<PvpBarrel> barrels, out bool boost)
        {
            boost = false;
            var fromCentre = Position - centre;
            fromCentre.y = 0f;
            if (fromCentre.magnitude > stormRadius - 6f)
            {
                boost = fromCentre.magnitude > stormRadius;
                return centre;
            }
            // A barrel it is closer to than the player (a health barrel when hurt, whatever the distance).
            PvpBarrel best = null;
            var bestScore = float.MaxValue;
            if (barrels != null)
                foreach (var b in barrels)
                {
                    var mine = Flat(b.World - Position);
                    var theirs = Flat(b.World - player);
                    var wanted = b.Xp || Hp < MaxHp * 0.6f;
                    if (!wanted || mine > 45f || (mine > theirs + 4f && b.Xp)) continue;
                    if (mine < bestScore) { bestScore = mine; best = b; }
                }
            if (best != null)
            {
                boost = bestScore > 18f;
                return best.World;
            }
            if (!playerAlive) return centre;
            // Close to orbit range and circle the player.
            var away = Position - player;
            away.y = 0f;
            var d = away.magnitude;
            if (d > 30f) boost = true;
            if (d < 0.1f) away = Vector3.right;
            var radial = away.normalized;
            var tangent = new Vector3(-radial.z, 0f, radial.x) * _strafeSign;
            var range = d > OrbitMax ? OrbitMin + 2f : d < OrbitMin ? OrbitMax : d;
            return player + radial * range + tangent * 10f;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}

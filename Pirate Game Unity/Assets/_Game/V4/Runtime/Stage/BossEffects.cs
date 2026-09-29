using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// The boss's blast effect and toasts (M14.9b; was LegacyBossEffects over V3's SaltwindCombatEffects): the blast goes
    /// through the enemy world's effect sink (the same blast the bomb boats play), the toasts through the run's toast sink.
    /// </summary>
    public sealed class BossEffects : IBossEffects
    {
        readonly IEnemyEffects _effects;
        readonly IStageToasts _toasts;

        public BossEffects(IEnemyEffects effects, IStageToasts toasts)
        {
            _effects = effects;
            _toasts = toasts;
        }

        public void Explosion(Vector3 position, float radius) => _effects?.PlayExplosion(position, radius);
        public void Toast(string text, float seconds) => _toasts?.Show(text, seconds);
    }
}

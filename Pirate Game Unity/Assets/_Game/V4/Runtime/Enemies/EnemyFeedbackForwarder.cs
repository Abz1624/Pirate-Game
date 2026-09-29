using System;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Hit feedback presentation draws (the enemy's damage highlight, camera shake and sound, the damage number).</summary>
    public interface IEnemyHitFeedback
    {
        void DamageHighlight(Transform view);
        void HitShakeAndSound(float damage);
        void DamageNumber(Vector3 position, float damage, bool isCrit);
    }

    /// <summary>
    /// The enemy simulation's feedback (M14; was LegacyEnemyFeedback over V3's JuiceVfxBroker, CameraShakeService, SfxService,
    /// TriggerBus and FloatingDamageNumbers): forwarded to the presenter the presentation root registers, nothing while
    /// none is. The Hit event itself is published by the simulation (<see cref="EnemySimulation.HitPublished"/>).
    /// </summary>
    public sealed class EnemyFeedbackForwarder : IEnemyFeedback
    {
        readonly Func<IEnemyHitFeedback> _target;

        public EnemyFeedbackForwarder(Func<IEnemyHitFeedback> target) { _target = target; }

        public void DamageHighlight(Transform view) => _target?.Invoke()?.DamageHighlight(view);
        public void HitShakeAndSound(float damage) => _target?.Invoke()?.HitShakeAndSound(damage);
        public void RaiseHit(float damage, bool isCrit, Vector3 position, string weaponId) { }
        public void DamageNumber(Vector3 position, float damage, bool isCrit) => _target?.Invoke()?.DamageNumber(position, damage, isCrit);
    }
}

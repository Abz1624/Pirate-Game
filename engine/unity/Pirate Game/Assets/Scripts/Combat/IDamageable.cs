using UnityEngine;

namespace PirateGame.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, GameObject source);
    }
}

using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class SaltwindCombatEffects
    {
        public static void PlayExplosion(Vector3 position, float blastRadius = 3f)
        {
            position.y = 0f;
            var juice = Object.FindFirstObjectByType<WeaponJuiceController>();
            juice?.OnMineExploded(position, blastRadius);
        }
    }
}

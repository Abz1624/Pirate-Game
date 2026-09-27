using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Playtest;
using HighSeas.V3;
using RoguePirates.V4.Stage;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{


    /// <summary>The boss's explosion effect and toasts (legacy presentation until M14).</summary>
    [DeletedInPhase(14, "Boss effects move to V4 presenters")]
    public sealed class LegacyBossEffects : IBossEffects
    {
        public void Explosion(Vector3 position, float radius) => SaltwindCombatEffects.PlayExplosion(position, radius);

        public void Toast(string text, float seconds)
        {
            var hud = V3ToastHud.Instance;
            if (hud != null) hud.Show(text, seconds);
        }
    }

    /// <summary>The boss director's gates that are still legacy state: the boss objective and V3's stage gate.</summary>
    [DeletedInPhase(14, "V4 encounter tracker (the kill path still completes encounters in the static tracker)")]
    public sealed class LegacyBossObjective
    {
        /// <summary>RunEncounterTracker.BossObjectiveSecured (the director stands down once the boss encounter is done).</summary>
        public bool Secured => RunEncounterTracker.BossObjectiveSecured;

        /// <summary>
        /// V3's first gate: stage balance profile active or the weapon/enemy playtest running. Always true in a solo V4
        /// run (the profile context is begun at run start); false in local multiplayer, where V4 does not begin it.
        /// </summary>
        public bool StageActive => StageBalanceProfileContext.IsActive || WeaponEnemyPlaytestSession.Active;
    }
}

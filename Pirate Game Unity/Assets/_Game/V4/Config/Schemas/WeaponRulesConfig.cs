using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Weapon rules that are not one weapon's (M5).</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Weapon Rules", fileName = "WeaponRules")]
    public sealed class WeaponRulesConfig : V4ConfigAsset
    {
        [Tooltip("\"Fire all weapons\" (captain active, Captain's Bell) speeds orbit blades' hits for this long instead of firing them.")]
        public float fireAllOrbitVolleySeconds = 2f;

        [Header("V3 fixes (approved at the M5 gate; off is V3's)")]
        [Tooltip("R-22: item attack speed shortened the cooldown and sped up its countdown (applied twice). On = once.")]
        public bool itemAttackSpeedAppliedOnce = true;
        [Tooltip("R-22: the time a cooldown ran past zero was dropped. On = carried into the next cooldown (at most one frame's worth).")]
        public bool carryCooldownOvershoot = true;
        [Header("Lightning salvo without a lightning weapon (Roxie's active)")]
        public float salvoRange = 18f, salvoRadius = 3.5f, salvoDamage = 12f, salvoProcCoefficient = 0.35f;
        [Tooltip("Damage-ledger id for the salvo.")] public string salvoLedgerId = "captain_roxie_salvo";
        [Tooltip("R-88: V3 looked for lightning weapons by ids no weapon has. On = the salvo fires your area-strike weapon when you have one.")]
        public bool salvoUsesLightningWeapon = true;
        [Tooltip("R-88: V3 passed 0 (every hit dealt the 1-damage floor).")] public float salvoLevelFactor = 1f;

        [Header("V5 M4: slows and knockbacks (combat design 2.3)")]
        [Tooltip("The most an ordinary enemy can be slowed (0.6 = it keeps at least 40% of its speed).")]
        [Range(0f, 0.9f)] public float maxSlow = 0.6f;
        [Tooltip("Bosses and minibosses take this share of a slow...")] [Range(0f, 1f)] public float bossSlowShare = 0.5f;
        [Tooltip("...and are never slowed by more than this in total.")] [Range(0f, 0.9f)] public float bossMaxSlow = 0.2f;
        [Tooltip("Elites take this share of a knockback's distance (bosses are not moved).")] [Range(0f, 1f)] public float eliteKnockbackShare = 0.5f;
        [Tooltip("The most one hit can push a regular enemy (m), whatever the upgrades.")] [Min(0f)] public float maxKnockbackMeters = 3f;
        [Tooltip("Seconds a knockback takes to push its distance.")] [Min(0.01f)] public float knockbackSeconds = 0.15f;
        [Tooltip("V5 M4, balance model only: the share of the time the ship boosts, for weapons that strike only while boosting (2.5 s of boost every 8.5 s: it assumes the player boosts whenever the boost is ready).")]
        [Range(0f, 1f)] public float modelBoostUptime = 0.3f;

        [Header("Effects (M6, presentation only)")]
        [Tooltip("Juice-settings weapon binding whose impact scale sizes every keg-style blast (V3: Powder Kegs').")]
        public string blastVfxBinding;
        [Tooltip("Juice-settings weapon binding whose impact scale sizes predicted-area impacts (V3: Mortar's).")]
        public string impactVfxBinding;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(salvoRadius), salvoRadius);
            report.Positive(this, nameof(salvoRange), salvoRange);
        }
    }
}

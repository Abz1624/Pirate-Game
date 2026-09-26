using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>Weapon rules that are not one weapon's (M5).</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Weapon Rules", fileName = "WeaponRules")]
    public sealed class WeaponRulesConfig : V4ConfigAsset
    {
        [Tooltip("\"Fire all weapons\" (captain active, Captain's Bell) speeds orbit blades' hits for this long instead of firing them.")]
        public float fireAllOrbitVolleySeconds = 2f;
        [Header("Lightning salvo without a lightning weapon (Roxie's active)")]
        public float salvoRange = 18f, salvoRadius = 3.5f, salvoDamage = 12f, salvoProcCoefficient = 0.35f;
        [Tooltip("Damage-ledger id for the salvo.")] public string salvoLedgerId = "captain_roxie_salvo";

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(salvoRadius), salvoRadius);
            report.Positive(this, nameof(salvoRange), salvoRange);
        }
    }
}

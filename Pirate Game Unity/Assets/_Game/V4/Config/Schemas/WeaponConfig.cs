using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class WeaponUpgradeAxis
    {
        public ConfigEnums.UpgradeAxis axis = ConfigEnums.UpgradeAxis.Damage;
        public float baseIncrement, offerWeight = 1f, maxAccumulated;
        public string displayFormat = string.Empty;
    }

    /// <summary>Single projectiles and bursts (Deck Cannons, Swivel Guns, Harpoon Gun, Long Nine before its lane).</summary>
    [Serializable]
    public sealed class ProjectileBlock
    {
        [Tooltip("Shots per trigger (Swivel Guns 2); the next shot waits burstGapSeconds, then the weapon cooldown starts.")] public int burstShots = 1;
        public float burstGapSeconds = 0.18f;
        [Tooltip("Base damage multiplier for each shot (Swivel Guns 0.55).")] public float damageMultiplier = 1f;
        [Tooltip("Projectile radius = max(minSize, sizeBase × weapon size × player Size × sizeMultiplier).")] public float sizeBase = 0.35f;
        public float sizeMultiplier = 1f, minSize;
        public float lifeSeconds = 3f;
        [Tooltip("Degrees between the shots of a multi-shot volley.")] public float spreadStepDegrees = 4f;
        [Tooltip("Volleys re-aim at the target with the actual projectile speed (V3: every volley except bursts).")] public bool leadWithActualSpeed = true;
        [Tooltip("Projectiles curve slightly toward the targeted enemy (V3: Swivel Guns, Deck Cannons, Long Nine).")] public bool homing;
        [Tooltip("Targets the closest enemy, strongly preferring those within 6 m (V3 Swivel Guns defense bias).")] public bool closeDefenseTargeting;
        [Tooltip("Hits apply Tarred and count control time (V3 Harpoon Gun).")] public bool tarOnHit;
        public float tarSlow = 0.25f, tarSlowBoss = 0.125f, controlSecondsPerHit = 1.2f;
        public bool splashOnDespawn = true;
        [Tooltip("V3 (register R-87): the tick path overwrote the burst gap with the full cooldown, so the second shot came a cooldown later. Off = the burst keeps its gap (approved fix).")]
        public bool v3FullCooldownAfterFirstShot;
    }

    /// <summary>How this weapon's projectiles look (V3 picked these by weapon id in ProjectileWorld).</summary>
    [Serializable]
    public sealed class ProjectileVisual
    {
        [Tooltip("Empty = the default projectile model.")] public GameObject prefab;
        public float scaleMultiplier = 1f;
        [Tooltip("Visual radius = gameplay radius × this (Swivel Guns 0.85).")] public float radiusMultiplier = 1f;
    }

    /// <summary>
    /// How the weapon's effects look (M6): the choices V3's juice controller and aim overlay made by weapon id,
    /// as data. Presentation only; nothing here changes gameplay.
    /// </summary>
    [Serializable]
    public sealed class WeaponEffectsVisual
    {
        [Tooltip("Muzzle effect: the juice settings' cannon muzzle (V3's cannon weapons) instead of the shooting prefab.")]
        public bool cannonMuzzle;
        [Tooltip("Muzzle scale when the juice settings have no binding for this weapon (V3: 1.12 for Broadside Battery).")]
        public float fallbackMuzzleScale = 1f;
        [Tooltip("Aim overlay shape (O key). ByTargeting picks it from pattern and targeting as V3 did.")]
        public ConfigEnums.AimShape aimShape = ConfigEnums.AimShape.ByTargeting;
        [Tooltip("Aim overlay: the target fan's width in degrees while a target is locked (V3 Swivel Guns: 22; 360 without a target).")]
        public float aimTargetFanDegrees = 22f;
        [Tooltip("Aim overlay: smallest predicted-area radius (V3: 4 for Mortar, 2 otherwise).")]
        public float aimMinAreaRadius = 2f;
        [Tooltip("Aim overlay: seconds the shape shows its locked colour after firing (V3: 0.55 Lightning Mast, 0.18 otherwise).")]
        public float firePulseSeconds = 0.18f;
        [Tooltip("Aim overlay: the fire flash fades out (V3: Lightning Mast).")]
        public bool firePulseFades;
        [Tooltip("Aim overlay: tint the orbit ring (V3: gold for Flying Cutlasses).")]
        public bool orbitRingTinted;
        public Color orbitRingColor = new Color(1f, 0.88f, 0.28f, 1f);
        [Tooltip("Summon view model (V3 boarding skiffs used the Cannonboat model); empty = a brown box.")]
        public GameObject summonModel;
        public float summonModelScale = 0.42f;
        [Tooltip("Enemies hit by this weapon do not flash (V3: Powder Kegs). Read by the enemy world's hit path from M8.")]
        [AppliesFrom(8)] public bool noHitHighlight;
    }

    /// <summary>Instant forward lanes that hit every enemy in them (Long Nine beam; Stormshot's lane).</summary>
    [Serializable]
    public sealed class LaneBlock
    {
        [Tooltip("Fire a lane instead of a projectile (Long Nine).")] public bool enabled;
        [Tooltip("A beam of half-width beamHalfWidth × size (Long Nine); otherwise an arc lane of the targeting arc.")] public bool beam;
        public float beamHalfWidth = 0.32f;
        [Tooltip("Degrees between lanes when quantity fires several.")] public float volleySpreadDegrees = 4f;
    }

    /// <summary>Pellet fans (Broadside Battery, Grapeshot).</summary>
    [Serializable]
    public sealed class FanBlock
    {
        public int basePellets = 3;
        [Tooltip("Quantity upgrades add pellets (V3: not for Broadside Battery, register R-22).")] public bool quantityAddsPellets = true;
        public float damageMultiplier = 0.6f;
        [Tooltip("Pellet speed floor (Broadside 42).")] public float minSpeed;
        public float sizeBase = 0.38f;
        public float hitRadius = 0.8f;
        public float lifeSeconds = 2.5f;
        [Tooltip("A muzzle effect per pellet; otherwise one side salvo effect (Broadside).")] public bool muzzlePerPellet = true;
        [Tooltip("Pellets aimed at an enemy home in on it and count as concurrent targets (V3: not Broadside).")] public bool trackPelletTargets = true;
        [Tooltip("Port/starboard muzzles sit this far to the side.")] public float sideMuzzleOffset = 0.85f;
        public bool splashOnDespawn = true;
    }

    /// <summary>
    /// Blades circling the ship (Flying Cutlasses). Hits are V4's and live-editable; the drawn blades are still the legacy
    /// juice controller's (V3 constants) until M6, so edits here move the hit spheres away from the drawn blades.
    /// </summary>
    [Serializable]
    public sealed class OrbitBlock
    {
        public int baseBlades = 2;
        public float orbitRadius = 5f, heightOffset = 0.35f, angularSpeedDegrees = 180f;
        public float hitRadius = 2.75f, hitIntervalSeconds = 0.12f, volleyHitIntervalSeconds = 0.096f;
        public float damageMultiplier = 0.35f;
    }

    /// <summary>A forward flame stream that runs, then recharges (Flamethrower).</summary>
    [Serializable]
    public sealed class StreamBlock
    {
        public float tickIntervalSeconds = 0.08f, streamSeconds = 2.75f;
        [Tooltip("Recharge = max(rechargeMinSeconds, cooldown × rechargeCooldownFactor).")] public float rechargeMinSeconds = 0.55f, rechargeCooldownFactor = 0.75f;
        public float forwardOffset = 1.65f;
    }

    /// <summary>A burning patch dropped astern (Burning Oil as a trail weapon).</summary>
    [Serializable]
    public sealed class TrailBlock
    {
        public float backOffset = 1.4f, height = 0.2f, radiusBase = 2.8f, damageMultiplier = 0.65f;
    }

    /// <summary>Kegs dropped astern that arm, then blow when an enemy touches them (Powder Kegs).</summary>
    [Serializable]
    public sealed class MineBlock
    {
        public float backOffset = 1.5f, height = 0.55f, armSeconds = 0.2f;
        public float triggerRadius = 3f, blastRadius = 3.2f, blastDamageMultiplier = 0.72f;
        [Tooltip("Elites are reached this much further.")] public float eliteReachBonus = 0.75f;
        [Tooltip("V5 (owner, 1 Oct 2026): an unblown keg sinks and is gone after this many seconds (0 = never, V3).")]
        [Min(0f)] public float lifeSeconds = 45f;
        [Tooltip("Seconds the sinking takes (visual only; the keg stops counting when it starts to sink).")]
        [Min(0f)] public float sinkSeconds = 1.5f;
    }

    /// <summary>A strike on the target's position hitting everything around it (Lightning Mast).</summary>
    [Serializable]
    public sealed class AreaStrikeBlock
    {
        public float radiusBase = 3.5f, radiusPerQuantity = 0.5f, shockSeconds = 0.5f;
        [Tooltip("Chain lightning (register R-19 fix): hit the target, then jump to the nearest enemy not yet hit within chainRange, chainTargets + chainTargetsPerQuantity × quantity times, each jump dealing chainFalloff of the last. Off = V3's area strike around the target.")]
        public bool chain = true;
        public int chainTargets = 4, chainTargetsPerQuantity = 1;
        public float chainRange = 7f, chainFalloff = 0.85f;
        [Tooltip("Size of each strike effect on a chained enemy.")] public float chainStrikeRadius = 1.4f;
    }

    /// <summary>Damage around the ship (aura weapons).</summary>
    [Serializable]
    public sealed class AuraBlock
    {
        public float radiusBase = 4f, damageMultiplier = 0.35f, curseSeconds = 0.4f;
    }

    /// <summary>A shell landing on the densest cluster after a delay (Mortar).</summary>
    [Serializable]
    public sealed class PredictedAreaBlock
    {
        public float impactDelaySeconds = 0.6f;
        public float telegraphHeight = 0.2f;
        [Tooltip("V3 (register R-70): the impact effect drew from the combat stream. On only for exact V3 comparisons.")] public bool v3ImpactEffectDraws;
        [Tooltip("Register R-27 fix: the landing ring lasts exactly the impact delay (V3's reticle showed 1.1 s for a 0.6 s shell). Off = V3's reticle.")]
        public bool telegraphMatchesImpact = true;
    }

    /// <summary>Boarding skiffs that chase a target (Boarding Party).</summary>
    [Serializable]
    public sealed class SummonBlock
    {
        public float backOffset = 5.5f, lateralOffset = 2.4f, spawnDepth = -0.7f, surfaceHeight = 0.12f, spawnSeconds = 0.55f;
        public float firstStrikeDelay = 0.15f, lifetimeSeconds = 9f, moveSpeed = 9.5f, strikeRange = 1.35f, strikeInterval = 0.55f;
    }

    /// <summary>
    /// One weapon, filled from the V3 WeaponDefinition after V3's runtime weapon catalog retunes (the catalog is gone since M16), plus the pattern
    /// constants V3 kept in WeaponController and the juice settings (M5). The behavior picks the firing logic; its block
    /// holds that logic's numbers. No V4 code compares weapon ids.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Weapon", fileName = "Weapon")]
    public sealed class WeaponConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string weaponId;
        [Tooltip("The name the V4 HUD shows (from M6) and the icon file name; level-up cards read the V3 definition until M9.")] [AppliesFrom(6)] public string displayName;
        [Tooltip("The upgrade card's first line (M14; V3 V3ContentDescriptions.GetWeaponDescription).")] [AppliesFrom(14)] public string description = string.Empty;
        [Tooltip("The new-weapon card's firing line (M14; V3 V3ContentDescriptions.GetWeaponBehaviorLine).")] [AppliesFrom(14)] public string behaviorLine = string.Empty;
        [Tooltip("V3's pattern (offers, HUD and the legacy loadout use it).")] [AppliesFrom(9)] public ConfigEnums.WeaponPattern pattern = ConfigEnums.WeaponPattern.AutoProjectile;
        [Tooltip("The V4 firing logic.")] public ConfigEnums.WeaponBehavior behavior = ConfigEnums.WeaponBehavior.Projectile;
        public ConfigEnums.TargetingMode targeting = ConfigEnums.TargetingMode.Nearest;
        [Tooltip("Read through the legacy WeaponDefinition (Admiral's Ledger family count) until M9.")] [AppliesFrom(9)] public string[] tags = Array.Empty<string>();
        public float baseDamage = 10f, baseCooldown = 1.2f, range = 14f;
        public int baseQuantity = 1;
        public float projectileSpeed = 16f, duration = 2f, size = 1f, knockback = 1f;
        public float fanSpreadDegrees = 50f, targetArcDegrees = 120f;
        public bool preferElite, preferHighestHull;
        [Tooltip("Level-up offers are the legacy level system's until M9.")] [AppliesFrom(9)] public int maxLevel = 12;
        [AppliesFrom(9)] public ConfigEnums.UpgradeAxis[] validUpgradeAxes = Array.Empty<ConfigEnums.UpgradeAxis>();
        [Tooltip("Upgrade lines a level-up can roll (V3's axis configs, or its pattern defaults when a weapon had none).")]
        [AppliesFrom(9)] public WeaponUpgradeAxis[] upgradeAxes = Array.Empty<WeaponUpgradeAxis>();
        [Tooltip("Position in V3's run database (level-up picks walk the weapons in this order).")] [AppliesFrom(9)] public int databaseOrder = -1;
        public float procCoefficient = 1f;
        public Color projectileColor = Color.white;
        [Tooltip("The combat debug aim overlay's colour for this weapon (RGB; M16: was V3's palette keyed by weapon id).")]
        public Color aimOverlayColor = new Color(0.7f, 0.7f, 0.7f);
        [Tooltip("Damage per hit scales with weapon level (× damageBonus × level × 0.1 + 1). V3 applies this to projectiles and lanes only (register R-21).")]
        public bool levelDamageFactor;
        [Tooltip("True for weapons V3 created at runtime (no V3 asset).")] public bool runtimeCatalogWeapon;

        public ProjectileVisual projectileVisual = new ProjectileVisual();
        [Tooltip("The hit/kill effect its hits play (V3: Deck Cannons, Broadside Battery and Swivel Guns a cannon impact; Powder Kegs none, their blast is their own).")]
        [AppliesFrom(14)] public ConfigEnums.WeaponHitEffect hitEffect = ConfigEnums.WeaponHitEffect.Standard;
        public ProjectileBlock projectile = new ProjectileBlock();
        public LaneBlock lane = new LaneBlock();
        public FanBlock fan = new FanBlock();
        public OrbitBlock orbit = new OrbitBlock();
        public StreamBlock stream = new StreamBlock();
        public TrailBlock trail = new TrailBlock();
        public MineBlock mine = new MineBlock();
        public AreaStrikeBlock areaStrike = new AreaStrikeBlock();
        public AuraBlock aura = new AuraBlock();
        public PredictedAreaBlock predictedArea = new PredictedAreaBlock();
        public SummonBlock summon = new SummonBlock();
        [Tooltip("Effect looks (M6, presentation only).")] public WeaponEffectsVisual visual = new WeaponEffectsVisual();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(weaponId)) report.Error(this, nameof(weaponId), "missing weapon id");
            report.Positive(this, nameof(baseCooldown), baseCooldown);
            report.Positive(this, nameof(range), range);
            report.NotNegative(this, nameof(baseDamage), baseDamage);
            if (maxLevel < 1) report.Error(this, nameof(maxLevel), "must be at least 1");
            report.WeightsNotZero(this, nameof(upgradeAxes), Array.ConvertAll(upgradeAxes, a => a.offerWeight));
            if (projectile.burstShots < 1) report.Error(this, "projectile.burstShots", "must be at least 1");
            if (fan.basePellets < 1) report.Error(this, "fan.basePellets", "must be at least 1");
            if (behavior == ConfigEnums.WeaponBehavior.ConeStream) report.Positive(this, "stream.tickIntervalSeconds", stream.tickIntervalSeconds);
        }
    }
}

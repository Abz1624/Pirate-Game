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
        [Tooltip("The tar's slow (share of speed). V5 M8z (7 Oct, D21): a real slow through the enemy world's slow rules, so bosses and minibosses take its boss share (tarSlowBoss is only the Tarred status's strength on bosses, the same value).")]
        public float tarSlow = 0.25f, tarSlowBoss = 0.125f, controlSecondsPerHit = 1.2f;
        [Tooltip("V5 M8z (7 Oct, D21): seconds the tar's slow lasts after each hit (0 = the tar does not slow, as before D21).")]
        [Min(0f)] public float tarSlowSeconds;
        public bool splashOnDespawn = true;
        [Tooltip("V3 (register R-87): the tick path overwrote the burst gap with the full cooldown, so the second shot came a cooldown later. Off = the burst keeps its gap (approved fix).")]
        public bool v3FullCooldownAfterFirstShot;
        [Tooltip("V5 M4: enemies a shot passes through after its first hit (0 = it stops at the first, as before); each enemy is hit once per shot.")]
        [Range(0, 7)] public int pierce;
        [Tooltip("V5 M4: each hit slows the enemy by this share of its speed (0 = no slow) for slowSeconds; the strongest slow holds.")]
        [Range(0f, 0.9f)] public float slowFraction;
        [Min(0f)] public float slowSeconds;
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
    /// <summary>V5 M8z (7 Oct): a weapon's coloured stand-in model (presentation only).</summary>
    public enum WeaponPlaceholderLook { None, Parrots, MonkeyGunners, Anchors, Lanterns, Doubloons }

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
        [Tooltip("V5 M4: an Orbit weapon's own model, drawn at each blade (empty = the Flying Cutlasses' blades).")]
        public GameObject orbitModel;
        public float orbitModelScale = 1f;
        // V5 M8z (7 Oct): which stand-in the presenters draw, moved from a weapon-id switch in WeaponPlaceholderModels.
        [Tooltip("V5 M8z (playtest 3 Oct, owner: no cannonballs for weapons that are not cannons): the coloured stand-in drawn while the weapon's model is a cannon asset or missing (summons, orbit models, projectiles, the strike's coins).")]
        public WeaponPlaceholderLook placeholderLook = WeaponPlaceholderLook.None;
        [Tooltip("V5 M8z (7 Oct, D21): a predicted-area shell lands as a ring in the weapon's own colour and its whirlpool ticks pulse in it (the Squid Ink Pots' ink), instead of the mortar's blast; the shell is then not an explosion (no explosion items).")]
        public bool pulseImpact;
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
        [Tooltip("V5 M4: each hit pushes the enemy this far away from the ship (0 = no push, as the cutlasses).")]
        [Min(0f)] public float knockbackMeters;
        [Tooltip("V5 M4: > 0 = each enemy can be hit once per this many seconds by this weapon, checked every frame (the Anchor Flail); 0 = the blades hit everything in contact every hitIntervalSeconds (the cutlasses).")]
        [Min(0f)] public float perTargetIntervalSeconds;
        [Tooltip("V5 M4b: > 0 = each blade is a beam from the ship to orbitRadius, made of this many hit spheres (the Lighthouse Lantern); 0 = one sphere per blade.")]
        [Min(0)] public int beamSegments;
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
        [Tooltip("V5 M4: each pulse slows every enemy it hits by this share of its speed (0 = none) for slowSeconds.")]
        [Range(0f, 0.9f)] public float slowFraction;
        [Min(0f)] public float slowSeconds;
        [Tooltip("V5 M4: the pulse is centred this far ahead of the ship (m; 0 = on the ship).")]
        public float forwardOffset;
        [Tooltip("V5 M4: each pulse pushes the enemies it hits this far away from the ship (m; 0 = none).")]
        [Min(0f)] public float knockbackMeters;
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
        [Tooltip("V5 M4b: > 0 = the impact opens a whirlpool for this many seconds that drags enemies in and hurts them, then implodes (the Maelstrom Charge).")]
        [Min(0f)] public float whirlpoolSeconds;
        [Min(0f)] public float pullMetersPerSecond = 3f;
        [Min(0.05f)] public float whirlpoolTickSeconds = 0.5f;
        [Tooltip("Each whirlpool tick's damage, × base damage.")] [Min(0f)] public float whirlpoolTickDamageMultiplier = 0.2f;
        [Tooltip("The implosion's damage, × base damage.")] [Min(0f)] public float implodeDamageMultiplier = 1.5f;
        // V5 M8z (7 Oct, D21): the slowing slick (the Squid Ink Pots), a whirlpool with no pull and no implosion that slows.
        [Tooltip("V5 M8z (D21): the shell and every whirlpool tick slow the enemies they hit by this share of their speed (0 = none) for slowSeconds; the Slow card and milestones add to it, the Duration card lengthens the whirlpool and the slow.")]
        [Range(0f, 0.9f)] public float slowFraction;
        [Min(0f)] public float slowSeconds;
        [Tooltip("V5 M8z (D21): aim at the back of the pack: the enemy in range scoring highest on distance + rearNeighbourMeters per neighbour within the shell's radius (off = the densest cluster, the Mortar). Each point of quantity lobs one more shell at the next pack back.")]
        public bool rearTargeting;
        [Min(0f)] public float rearNeighbourMeters = 1f;
        [Tooltip("V5 M8z (D21): rear targeting lands where the target will be when the shell lands (its velocity × the impact delay).")]
        public bool leadTarget = true;
    }

    /// <summary>
    /// V5 M8z (7 Oct, D21, Q20): a weapon's rank milestone. From weapon level <see cref="level"/> on, the weapon has these
    /// extras on top of its upgrades (applied at every loadout sync with the evolution, so they never stack).
    /// </summary>
    [Serializable]
    public sealed class WeaponMilestone
    {
        [Min(1)] public int level = 4;
        [Tooltip("Extra quantity (one more shell, shot or summon).")] [Min(0)] public int extraQuantity;
        [Tooltip("Size (radius) × this.")] [Min(0.1f)] public float sizeMultiplier = 1f;
        [Tooltip("Added to the weapon's slow (share of speed).")] [Range(0f, 0.9f)] public float extraSlow;
        [Tooltip("Duration × this (a summon's life, a whirlpool's or slick's seconds, a slow's seconds where the Duration card reaches it).")] [Min(0.1f)] public float durationMultiplier = 1f;
        // V5 M8z (7 Oct, Q20): the effects the other new weapons' milestones need (the design's "another pierce", "+25°/s",
        // "+0.5 s slow", "a second pulse after 0.35 s"), generic: any weapon whose behaviour reads them takes them.
        [Tooltip("V5 M8z (Q20): extra enemies a shot passes through (a projectile's pierce).")] [Min(0)] public int extraPierce;
        [Tooltip("V5 M8z (Q20): an Orbit weapon's spin × this (blades, anchors and beams sweep faster).")] [Min(0.1f)] public float spinMultiplier = 1f;
        [Tooltip("V5 M8z (Q20): seconds added to the weapon's slow (before the Duration card).")] [Min(0f)] public float extraSlowSeconds;
        [Tooltip("V5 M8z (Q20): every firing repeats this many more times, echoDelaySeconds apart (a second pulse, a second coin).")] [Min(0)] public int echoes;
        [Min(0.05f)] public float echoDelaySeconds = 0.35f;
        [Tooltip("The card's line when the milestone is reached (\"a second pot\").")] public string description = string.Empty;
    }

    /// <summary>Boarding skiffs that chase a target (Boarding Party).</summary>
    [Serializable]
    public sealed class SummonBlock
    {
        public float backOffset = 5.5f, lateralOffset = 2.4f, spawnDepth = -0.7f, surfaceHeight = 0.12f, spawnSeconds = 0.55f;
        public float firstStrikeDelay = 0.15f, lifetimeSeconds = 9f, moveSpeed = 9.5f, strikeRange = 1.35f, strikeInterval = 0.55f;
        [Tooltip("V5 M4b: one summon per point of quantity (parrots); off = one per firing (V3's skiffs).")]
        public bool perQuantity;
        [Tooltip("V5 M4b: when its target is gone the summon picks the nearest enemy within retargetRange and lives on until its lifetime ends; off = it leaves with its target (V3).")]
        public bool retarget;
        public float retargetRange = 12f;
        [Tooltip("V5 M4b: > 0 = it fires a shot of this speed at its target from strikeRange instead of striking alongside (the monkey turret).")]
        [Min(0f)] public float shotSpeed;
        public float shotSize = 0.3f;
        [Tooltip("V5 M4b: at most this many of this weapon's summons alive at once (0 = no cap, V3); further spawns are skipped. V5 M8z (7 Oct, Q20): a summon lives lifetimeSeconds × the weapon's Duration (its milestones; no summon has the Duration card).")]
        [Min(0)] public int maxAlive;
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
        [Tooltip("V5 M4b: extra gold a kill by this weapon drops (Cursed Doubloons), at most goldPerKillRunCap a run (anti-farming).")]
        [Min(0)] public int goldPerKill;
        [Min(0)] public int goldPerKillRunCap;
        [Tooltip("V5 M8z (7 Oct, D21, Q20): rank milestones (weapon levels 4, 8 and 12 for the new weapons), in level order.")]
        public WeaponMilestone[] milestones = Array.Empty<WeaponMilestone>();

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

        /// <summary>V5 M8z (8 Oct, close): a milestone's echoes repeat this behavior's firing (not an orbit's or a flame
        /// stream's: they never stop firing). The weapon manager, the director and the validator share this rule.</summary>
        public static bool Echoes(ConfigEnums.WeaponBehavior behavior) =>
            behavior != ConfigEnums.WeaponBehavior.Orbit && behavior != ConfigEnums.WeaponBehavior.ConeStream;

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
            // V5 M8z (7 Oct, D21): milestones within the weapon's levels, in order.
            var last = 0;
            foreach (var m in milestones ?? Array.Empty<WeaponMilestone>())
            {
                if (m == null) { report.Error(this, nameof(milestones), "empty milestone"); continue; }
                if (m.level <= last || m.level > maxLevel) report.Error(this, nameof(milestones), $"milestone level {m.level} must rise and be at most maxLevel {maxLevel}");
                // V5 M8z (8 Oct, close): an orbit or a flame stream has no discrete firing to repeat (the manager skips them).
                if (m.echoes > 0 && !Echoes(behavior))
                    report.Error(this, nameof(milestones), $"milestone level {m.level}: echoes on a {behavior} weapon never fire (Orbit and ConeStream do not echo)");
                last = m.level;
            }
        }
    }
}

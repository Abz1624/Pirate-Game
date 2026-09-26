using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// The boss's attacks. Same names and values as V3's BarnacleKingAttackKind (None = 0 is "no attack": the state
    /// between attacks; it is not allowed in a rotation).
    /// </summary>
    public enum BossAttackKind { None = 0, AimedSpread = 1, CommittedCharge = 2, MarkedBombardment = 3, ShellSlam = 4 }

    /// <summary>Aimed spread: a fan of shots at the ship.</summary>
    [Serializable]
    public sealed class BossSpreadAttack
    {
        [Tooltip("Seconds the boss holds still and warns before firing.")] public float telegraphSeconds = 1.2f;
        [Tooltip("Seconds of the firing step (the shots leave at its start).")] public float executeSeconds = 0.25f;
        [Tooltip("Seconds the boss rests after the attack before the next one.")] public float recoverySeconds = 2f;
        [Tooltip("Shots in the fan.")] public int shotCount = 5;
        [Tooltip("Width of the fan in degrees, first shot to last.")] public float spreadDegrees = 12f;
        [Tooltip("Each shot's damage = boss damage × this.")] public float damageMultiplier = 0.55f;
        [Tooltip("Shot speed, metres per second.")] public float projectileSpeed = 14f;
        [Tooltip("Seconds a shot flies before it disappears.")] public float projectileLifeSeconds = 2.4f;
        [Tooltip("How close a shot must pass to hit the ship.")] public float projectileHitRadius = 0.55f;
        [Tooltip("Size of the shot's model.")] public float projectileVisualRadius = 0.45f;
        [Tooltip("Shots start this far beyond the boss's edge, towards the ship.")] public float muzzleForwardOffset = 1.4f;
        [Tooltip("Shots start this high above the boss's position.")] public float muzzleHeight = 0.6f;
        [Tooltip("Warning toast while telegraphing. Empty = none.")] public string toastText = "Barnacle King — spread incoming";
    }

    /// <summary>Committed charge: the boss locks a direction and rushes along it.</summary>
    [Serializable]
    public sealed class BossChargeAttack
    {
        [Tooltip("Seconds the boss holds still and warns before charging.")] public float telegraphSeconds = 1.5f;
        [Tooltip("Seconds the charge lasts.")] public float executeSeconds = 0.4f;
        [Tooltip("Seconds the boss rests after the charge.")] public float recoverySeconds = 2f;
        [Tooltip("Charge speed = the boss's move speed × this.")] public float speedMultiplier = 2.6f;
        [Tooltip("In the last health phase every charge from the rotation is followed by a second charge.")] public bool finalPhaseSecondCharge = true;
        [Tooltip("Warning toast while telegraphing. Empty = none.")] public string toastText = "Barnacle King — charge incoming";
    }

    /// <summary>Marked bombardment: a circle is marked where the ship was, then hit (no toast, as V3).</summary>
    [Serializable]
    public sealed class BossBombardAttack
    {
        [Tooltip("Seconds the circle is shown before it is hit.")] public float telegraphSeconds = 1.5f;
        [Tooltip("Seconds of the blast step (the damage lands at its start).")] public float executeSeconds = 0.35f;
        [Tooltip("Seconds the boss rests after the blast.")] public float recoverySeconds = 2.5f;
        [Tooltip("Radius of the marked circle.")] public float radius = 5.5f;
        [Tooltip("Damage to the ship inside the circle = boss damage × this.")] public float damageMultiplier = 1.15f;
    }

    /// <summary>Shell slam: a circle is marked where the ship was, then slammed with an explosion.</summary>
    [Serializable]
    public sealed class BossSlamAttack
    {
        [Tooltip("Seconds the circle is shown before the slam.")] public float telegraphSeconds = 1.2f;
        [Tooltip("Seconds of the slam step (the damage lands at its start).")] public float executeSeconds = 0.25f;
        [Tooltip("Seconds the boss rests after the slam.")] public float recoverySeconds = 2f;
        [Tooltip("Radius of the slam circle (and of its explosion effect).")] public float radius = 5f;
        [Tooltip("Damage to the ship inside the circle = boss damage × this.")] public float damageMultiplier = 1.35f;
        [Tooltip("Warning toast while telegraphing. Empty = none.")] public string toastText = "Barnacle King — shell slam";
    }

    /// <summary>
    /// The Barnacle King's attack pattern (M7): the constants V3 held in BarnacleKingBossAttackLogic and
    /// BarnacleKingBossAttackDirector, as data. The defaults are V3's values, so an asset left at its defaults plays
    /// exactly like V3. Read live by the boss director, so console edits apply from the next step of the pattern.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Boss", fileName = "BossConfig")]
    public sealed class BossConfig : V4ConfigAsset
    {
        public static readonly BossAttackKind[] V3Phase1Rotation = { BossAttackKind.AimedSpread, BossAttackKind.ShellSlam, BossAttackKind.AimedSpread, BossAttackKind.MarkedBombardment };
        public static readonly BossAttackKind[] V3Phase2Rotation = { BossAttackKind.AimedSpread, BossAttackKind.MarkedBombardment, BossAttackKind.CommittedCharge, BossAttackKind.ShellSlam };
        public static readonly BossAttackKind[] V3Phase3Rotation = { BossAttackKind.CommittedCharge, BossAttackKind.AimedSpread, BossAttackKind.MarkedBombardment, BossAttackKind.ShellSlam };

        [Header("Health phases (health = current / max)")]
        [Tooltip("Above this health the boss is in phase 1; at or below it, phase 2. A new phase starts after the current attack's rest.")]
        public float phase2Threshold = 0.65f;
        [Tooltip("At or below this health the boss is in phase 3 (the last phase: shorter rests, double charges).")]
        public float phase3Threshold = 0.3f;
        [Tooltip("At or below this health the boss is enraged: shorter rests after attacks.")]
        public float enrageThreshold = 0.3f;

        [Header("Attack order per phase (repeats; None is not allowed)")]
        public BossAttackKind[] phase1Rotation = (BossAttackKind[])V3Phase1Rotation.Clone();
        public BossAttackKind[] phase2Rotation = (BossAttackKind[])V3Phase2Rotation.Clone();
        public BossAttackKind[] phase3Rotation = (BossAttackKind[])V3Phase3Rotation.Clone();

        [Header("Rests")]
        [Tooltip("Seconds before the first attack once the boss is found.")]
        public float startRecoverySeconds = 0.5f;
        [Tooltip("When enraged or in phase 3, rests after attacks are multiplied by this.")]
        public float enragedRecoveryScale = 0.75f;
        [Tooltip("Whether the shortened rest also applies after a shell slam (V3: no).")]
        public bool enragedRecoveryAppliesToShellSlam;

        [Header("Attacks")]
        public BossSpreadAttack spread = new BossSpreadAttack();
        public BossChargeAttack charge = new BossChargeAttack();
        public BossBombardAttack bombard = new BossBombardAttack();
        public BossSlamAttack slam = new BossSlamAttack();

        [Header("Warnings")]
        [Tooltip("Seconds between repeats of the telegraph toast while an attack is being telegraphed.")]
        public float toastIntervalSeconds = 1.2f;
        [Tooltip("Seconds each telegraph toast stays on screen.")]
        public float toastDurationSeconds = 1.1f;

        [Header("Marked circle (presentation only)")]
        [Tooltip("Colour of the circle shown before a bombardment or slam.")]
        public Color telegraphColor = new Color(1f, 0.35f, 0.1f, 0.55f);
        [Tooltip("The circle is drawn this high above the marked point.")]
        public float telegraphHeight = 0.08f;
        [Tooltip("Thickness of the circle's disc.")]
        public float telegraphThickness = 0.12f;

        public BossAttackKind[] RotationFor(int scriptPhase) =>
            scriptPhase <= 1 ? phase1Rotation : scriptPhase == 2 ? phase2Rotation : phase3Rotation;

        public override void Validate(ConfigValidationReport report)
        {
            if (!(phase3Threshold <= phase2Threshold)) report.Error(this, nameof(phase3Threshold), $"must not be above phase2Threshold ({phase3Threshold} > {phase2Threshold})");
            ValidateRotation(report, nameof(phase1Rotation), phase1Rotation);
            ValidateRotation(report, nameof(phase2Rotation), phase2Rotation);
            ValidateRotation(report, nameof(phase3Rotation), phase3Rotation);
            report.NotNegative(this, nameof(startRecoverySeconds), startRecoverySeconds);
            report.NotNegative(this, nameof(enragedRecoveryScale), enragedRecoveryScale);
            report.Positive(this, nameof(toastIntervalSeconds), toastIntervalSeconds);

            if (spread == null || charge == null || bombard == null || slam == null) { report.Error(this, "", "an attack block is missing"); return; }
            Timings(report, "spread", spread.telegraphSeconds, spread.executeSeconds, spread.recoverySeconds);
            Timings(report, "charge", charge.telegraphSeconds, charge.executeSeconds, charge.recoverySeconds);
            Timings(report, "bombard", bombard.telegraphSeconds, bombard.executeSeconds, bombard.recoverySeconds);
            Timings(report, "slam", slam.telegraphSeconds, slam.executeSeconds, slam.recoverySeconds);
            if (spread.shotCount < 1) report.Error(this, "spread.shotCount", $"must be at least 1 ({spread.shotCount})");
            report.Positive(this, "spread.projectileSpeed", spread.projectileSpeed);
            report.Positive(this, "spread.projectileLifeSeconds", spread.projectileLifeSeconds);
            report.Positive(this, "spread.projectileHitRadius", spread.projectileHitRadius);
            report.NotNegative(this, "charge.speedMultiplier", charge.speedMultiplier);
            report.Positive(this, "bombard.radius", bombard.radius);
            report.Positive(this, "slam.radius", slam.radius);
        }

        void ValidateRotation(ConfigValidationReport report, string path, BossAttackKind[] rotation)
        {
            if (rotation == null || rotation.Length == 0) { report.Error(this, path, "is empty; the boss would use V3's order"); return; }
            for (var i = 0; i < rotation.Length; i++)
                if (rotation[i] == BossAttackKind.None) report.Error(this, $"{path}[{i}]", "None is not an attack");
        }

        void Timings(ConfigValidationReport report, string block, float telegraph, float execute, float recovery)
        {
            report.NotNegative(this, block + ".telegraphSeconds", telegraph);
            // The attack lands while the execute timer is above 0, so a zero execute step would never land.
            report.Positive(this, block + ".executeSeconds", execute);
            report.NotNegative(this, block + ".recoverySeconds", recovery);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HighSeas.Balance;
using HighSeas.V3;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Creates or updates the Barnacle King boss config (M7) from V3: the timings and radius BarnacleKingBossAttackLogic
    /// exposes, its three attack rotations and the director's charge and spread speeds (read by reflection), and V3's
    /// in-code literals, which are <see cref="BossConfig"/>'s defaults. Idempotent; it rewrites every value, so run it
    /// only to rebuild V3's values. It does not add the asset to the config set.
    /// </summary>
    public static class V4BossConfigGenerator
    {
        public const string AssetPath = V3ConfigMigrator.DataRoot + "/Stage/BossConfig_BarnacleKing.asset";
        public const string ConfigId = "boss.barnacle_king";

        [MenuItem("RoguePirates/V4/Generate Boss Config")]
        static void GenerateFromMenu() => Debug.Log(Generate());

        public static string Generate()
        {
            var log = new StringBuilder();
            EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
            var config = AssetDatabase.LoadAssetAtPath<BossConfig>(AssetPath);
            var created = config == null;
            if (created)
            {
                config = ScriptableObject.CreateInstance<BossConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
            }
            FillFromV3(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            log.AppendLine($"{(created ? "created" : "updated")} {AssetPath} ({config.configId})");
            log.AppendLine($"rotations: [{string.Join(", ", config.phase1Rotation)}] [{string.Join(", ", config.phase2Rotation)}] [{string.Join(", ", config.phase3Rotation)}]");
            var report = new ConfigValidationReport();
            config.Validate(report);
            log.Append(report.IsValid ? "valid" : report.ToString());
            return log.ToString();
        }

        /// <summary>Resets every field to V3's values (identity included).</summary>
        public static void FillFromV3(BossConfig c)
        {
            var name = c.name; // CopySerialized copies the object name too
            var defaults = ScriptableObject.CreateInstance<BossConfig>();
            try { EditorUtility.CopySerialized(defaults, c); }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
            c.name = name;

            c.configId = ConfigId;
            c.spread.telegraphSeconds = BarnacleKingBossAttackLogic.SpreadTelegraphSeconds;
            c.spread.recoverySeconds = BarnacleKingBossAttackLogic.SpreadRecoverySeconds;
            c.charge.telegraphSeconds = BarnacleKingBossAttackLogic.ChargeTelegraphSeconds;
            c.charge.executeSeconds = BarnacleKingBossAttackLogic.ChargeCommitSeconds;
            c.charge.recoverySeconds = BarnacleKingBossAttackLogic.ChargeRecoverySeconds;
            c.bombard.telegraphSeconds = BarnacleKingBossAttackLogic.BombardTelegraphSeconds;
            c.bombard.recoverySeconds = BarnacleKingBossAttackLogic.BombardRecoverySeconds;
            c.bombard.radius = BarnacleKingBossAttackLogic.MarkedBombardmentRadius;
            // V3's shell slam reused the spread's telegraph and recovery constants.
            c.slam.telegraphSeconds = BarnacleKingBossAttackLogic.SpreadTelegraphSeconds;
            c.slam.recoverySeconds = BarnacleKingBossAttackLogic.SpreadRecoverySeconds;
            c.enragedRecoveryScale = BarnacleKingBossAttackLogic.EnragedRecoveryScale;
            c.charge.speedMultiplier = DirectorConstant("ChargeSpeedMult", c.charge.speedMultiplier);
            c.spread.projectileSpeed = DirectorConstant("SpreadProjectileSpeed", c.spread.projectileSpeed);
            c.phase1Rotation = V3Rotation("Phase1") ?? c.phase1Rotation;
            c.phase2Rotation = V3Rotation("Phase2") ?? c.phase2Rotation;
            c.phase3Rotation = V3Rotation("Phase3") ?? c.phase3Rotation;
        }

        /// <summary>A private const of V3's BarnacleKingBossAttackDirector, or the fallback if it is gone.</summary>
        public static float DirectorConstant(string name, float fallback)
        {
            var field = typeof(BarnacleKingBossAttackDirector).GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            return field != null && field.IsLiteral ? Convert.ToSingle(field.GetRawConstantValue()) : fallback;
        }

        /// <summary>One of V3's private attack rotations (Phase1-3 in BarnacleKingBossAttackLogic), converted by value.</summary>
        public static BossAttackKind[] V3Rotation(string name)
        {
            var field = typeof(BarnacleKingBossAttackLogic).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null) is BarnacleKingAttackKind[] v3 ? v3.Select(a => (BossAttackKind)(int)a).ToArray() : null;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}

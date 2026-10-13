using System;
using System.Collections.Generic;

namespace RoguePirates.Simulation.Balance
{
    public enum BalanceLabCaptain
    {
        Rook,
        Brine,
        Flint,
        Richie,
        Ember,
        Roxie
    }

    public enum BalanceSkillPreset { Conservative, Average, Aggressive, Expert }
    public enum BalanceEnemyBehavior { Chaser, Ranged, Charge, Suicide, Tank, Flank }

    [Serializable]
    public sealed class BalanceWeaponLoadoutEntry
    {
        public string Id = "deck_cannons";
        public float Damage = 11f;
        public float Cooldown = 1.05f;
        public float Range = 22f;
        public float ProjectileSpeed = 16f;
        public float TargetMultiplier = 1f;
        public float SizeMultiplier = 1f;
        public BalanceWeaponLoadoutEntry Clone() => (BalanceWeaponLoadoutEntry)MemberwiseClone();
    }

    [Serializable]
    public sealed class BalanceEnemyRosterEntry
    {
        public string Id = "shark";
        public bool Enabled = true;
        public float Weight = 1f;
        public int MaxConcurrent;
        public float AvailableAfterSeconds;
        public float AvailableUntilSeconds;
        public float Hull = 16f;
        public float Damage = 9f;
        public float MoveSpeed = 4.75f;
        public float AttackCooldown = 1.5f;
        public float PreferredRange = 8f;
        public int Xp = 4;
        public int Gold = 1;
        public BalanceEnemyBehavior Behavior;
        public BalanceEnemyRosterEntry Clone() => (BalanceEnemyRosterEntry)MemberwiseClone();
    }

    [Serializable]
    public sealed class BalanceWaveEntry
    {
        public string EnemyId;
        public int Count = 1;
        public BalanceWaveEntry Clone() => (BalanceWaveEntry)MemberwiseClone();
    }
    [Serializable]
    public sealed class BalanceWaveDefinition
    {
        public string Name = "Wave";
        public float DelaySeconds;
        public float DurationSeconds = 45f;
        public float SpawnGapSeconds = 2f;
        public List<BalanceWaveEntry> Composition = new();
        public BalanceWaveDefinition Clone()
        {
            var copy = (BalanceWaveDefinition)MemberwiseClone();
            copy.Composition = new List<BalanceWaveEntry>();
            for (var i = 0; i < Composition.Count; i++) copy.Composition.Add(Composition[i]?.Clone());
            return copy;
        }
    }

    /// <summary>
    /// A deterministic, presentation-free balance experiment. Values are multipliers unless noted.
    /// Keep this data separate from production ScriptableObjects: a lab run must never change a player's build.
    /// </summary>
    [Serializable]
    public sealed class BalanceLabScenario
    {
        public string Name = "Saltwind baseline";
        public int Seed = 12345;
        public int Runs = 100;
        public float DurationSeconds = 600f;
        public SimInputPolicy InputPolicy = SimInputPolicy.Orbit;
        public BalanceSkillPreset SkillPreset = BalanceSkillPreset.Average;

        // Player / captain.
        public BalanceLabCaptain Captain = BalanceLabCaptain.Rook;
        public float PlayerHullMultiplier = 1f;
        public float PlayerMoveSpeedMultiplier = 1f;
        public float XpGainMultiplier = 1f;
        public float XpRequirementMultiplier = 1f;

        // Weapons / items / level-up choices. WeaponCount is an expected loadout count, capped by WeaponSlotCap.
        public int WeaponSlotCap = 6;
        public int WeaponCount = 1;
        public string WeaponId = "deck_cannons";
        public float WeaponBaseDamage = 11f;
        public float WeaponBaseCooldown = 1.05f;
        public float WeaponRange = 22f;
        public float WeaponProjectileSpeed = 16f;
        public float WeaponPatternTargetMultiplier = 1f;
        public float WeaponDamageMultiplier = 1f;
        public float WeaponCooldownMultiplier = 1f;
        public float ProjectileSpeedMultiplier = 1f;
        public float ProjectileRadiusMultiplier = 1f;
        public float ItemDamageMultiplier = 1f;
        public List<BalanceWeaponLoadoutEntry> WeaponLoadout = new();
        public bool SimulateUpgradePath = true;
        public float DamageUpgradePerLevel = .08f;
        public int EvolutionLevel = 10;
        public float EvolutionDamageMultiplier = 1.5f;

        // Director / enemies / boss.
        public bool DirectorEnabled = true;
        public string EnemyId = "shark";
        public float EnemyBaseHull = 16f;
        public float EnemyBaseDamage = 9f;
        public float EnemyBaseMoveSpeed = 4.75f;
        public float EnemyXpValue = 4f;
        // Snapshot of SpawnDirectorSettings for the selected standard enemy. These are copied,
        // never written back to the asset, so an experiment remains reproducible.
        public float DirectorThreatMultiplier = 1f;
        public int DirectorMaxActiveEnemies = 28;
        public float EnemySpawnWeight = 1f;
        public bool EnemySpawnEnabled = true;
        public int EnemySpawnMaxConcurrent;
        public float EnemyAvailableAfterSeconds;
        public float EnemyAvailableUntilSeconds;
        public float EnemyDirectorSpeedMultiplier = 1f;
        public float PressurePulseMultiplier = 1.25f;
        public float PressurePulseDurationSeconds = 20f;
        public List<BalanceEnemyRosterEntry> EnemyRoster = new();
        public bool UseScriptedWaves;
        public bool ContinueAfterScriptedWaves = true;
        public List<BalanceWaveDefinition> ScriptedWaves = new();
        public float SpawnDensityMultiplier = 1f;
        public float EnemyHullMultiplier = 1f;
        public float EnemyDamageMultiplier = 1f;
        public float EnemyMoveSpeedMultiplier = 1f;
        public float BossHullMultiplier = 1f;
        public float BossDamageMultiplier = 1f;
        public string BossId = "barnacle_king";
        public float BossBaseHull = 500f;
        public float BossBaseDamage = 20f;
        public float BossBaseMoveSpeed = 3.5f;
        public BalanceEnemyBehavior BossBehavior = BalanceEnemyBehavior.Chaser;
        public float BossAttackCooldown = 1.5f;
        public float BossPreferredRange = 8f;
        public float BossSpawnSeconds = 600f;

        // POIs are modelled as a deterministic route reward, not as a replacement for POIManager interaction code.
        public bool EnablePois = true;
        public int SalvageChestsOpened = 6;
        public int RepairWrecksUsed = 1;
        public int PowderCachesCleared = 1;
        public int SignalBuoysCharged; // 13 Oct (owner): the Signal Buoy is switched off in Saltwind (StageContentConfig.disabledPoiTypes), so the lab charges none (was 1, +10% damage)
        public int TreasureChartsUsed = 1;
        public float PoiDamageMultiplier = 1f;
        public float PoiHealingMultiplier = 1f;
        public float PoiXpMultiplier = 1f;
        public float PoiTravelSeconds = 12f;
        public float PoiInteractionRiskDamage = 6f;
        public float PoiFailureChance = .10f;
        // Economy is intentionally explicit, making merchant tuning testable rather than a hidden bonus.
        public int StartingGold;
        public int MerchantVisits = 1;
        public int MerchantItemPrice = 30;
        public int MerchantRerollPrice = 10;
        public int PlannedMerchantPurchases = 1;
        public float MerchantItemPowerMultiplier = 1.10f;
        public float TargetAverageSurvivalMin = .45f;
        public float TargetAverageSurvivalMax = .70f;
        public float TargetExpertBossWinMin = .70f;
        public float TargetMerchantAffordabilityMin = .60f;

        // Saltwind profile + lab matrix (Step 12). Never written to player saves or production assets.
        public StageBalanceProfileExport StageProfile;
        public string StageProfileContentHash = string.Empty;
        public int StageBalanceVersion;
        public SaltwindLabBuildPolicy SaltwindBuildPolicy = SaltwindLabBuildPolicy.None;
        public SaltwindLabUnlockPool SaltwindUnlockPool = SaltwindLabUnlockPool.FreshSave;
        public string BehaviorModelLabel = string.Empty;

        public BalanceLabScenario Clone()
        {
            var copy = (BalanceLabScenario)MemberwiseClone();
            copy.WeaponLoadout = new List<BalanceWeaponLoadoutEntry>();
            if (WeaponLoadout != null) for (var i = 0; i < WeaponLoadout.Count; i++) copy.WeaponLoadout.Add(WeaponLoadout[i]?.Clone());
            copy.EnemyRoster = new List<BalanceEnemyRosterEntry>();
            if (EnemyRoster != null) for (var i = 0; i < EnemyRoster.Count; i++) copy.EnemyRoster.Add(EnemyRoster[i]?.Clone());
            copy.ScriptedWaves = new List<BalanceWaveDefinition>();
            if (ScriptedWaves != null) for (var i = 0; i < ScriptedWaves.Count; i++) copy.ScriptedWaves.Add(ScriptedWaves[i]?.Clone());
            copy.StageProfile = StageProfile;
            return copy;
        }

        public void Normalize()
        {
            Runs = Math.Max(1, Runs);
            DurationSeconds = Math.Max(1f, DurationSeconds);
            WeaponSlotCap = Math.Max(1, WeaponSlotCap);
            WeaponCount = Math.Max(1, Math.Min(WeaponCount, WeaponSlotCap));
            SalvageChestsOpened = Math.Max(0, SalvageChestsOpened);
            RepairWrecksUsed = Math.Max(0, RepairWrecksUsed);
            PowderCachesCleared = Math.Max(0, PowderCachesCleared);
            SignalBuoysCharged = Math.Max(0, SignalBuoysCharged);
            TreasureChartsUsed = Math.Max(0, TreasureChartsUsed);
            MerchantVisits = Math.Max(0, MerchantVisits);
            MerchantItemPrice = Math.Max(0, MerchantItemPrice);
            MerchantRerollPrice = Math.Max(0, MerchantRerollPrice);
            PlannedMerchantPurchases = Math.Max(0, PlannedMerchantPurchases);
            MerchantItemPowerMultiplier = AtLeast(MerchantItemPowerMultiplier);
            PlayerHullMultiplier = AtLeast(PlayerHullMultiplier);
            PlayerMoveSpeedMultiplier = AtLeast(PlayerMoveSpeedMultiplier);
            XpGainMultiplier = AtLeast(XpGainMultiplier);
            XpRequirementMultiplier = AtLeast(XpRequirementMultiplier);
            WeaponDamageMultiplier = AtLeast(WeaponDamageMultiplier);
            WeaponCooldownMultiplier = AtLeast(WeaponCooldownMultiplier);
            ProjectileSpeedMultiplier = AtLeast(ProjectileSpeedMultiplier);
            ProjectileRadiusMultiplier = AtLeast(ProjectileRadiusMultiplier);
            WeaponBaseDamage = AtLeast(WeaponBaseDamage);
            WeaponBaseCooldown = AtLeast(WeaponBaseCooldown);
            WeaponRange = AtLeast(WeaponRange);
            WeaponProjectileSpeed = AtLeast(WeaponProjectileSpeed);
            WeaponPatternTargetMultiplier = AtLeast(WeaponPatternTargetMultiplier);
            ItemDamageMultiplier = AtLeast(ItemDamageMultiplier);
            DamageUpgradePerLevel = Math.Max(0f, DamageUpgradePerLevel);
            EvolutionLevel = Math.Max(1, EvolutionLevel);
            EvolutionDamageMultiplier = AtLeast(EvolutionDamageMultiplier);
            SpawnDensityMultiplier = AtLeast(SpawnDensityMultiplier);
            EnemyHullMultiplier = AtLeast(EnemyHullMultiplier);
            EnemyDamageMultiplier = AtLeast(EnemyDamageMultiplier);
            EnemyMoveSpeedMultiplier = AtLeast(EnemyMoveSpeedMultiplier);
            EnemyBaseHull = AtLeast(EnemyBaseHull);
            EnemyBaseDamage = AtLeast(EnemyBaseDamage);
            EnemyBaseMoveSpeed = AtLeast(EnemyBaseMoveSpeed);
            EnemyXpValue = AtLeast(EnemyXpValue);
            DirectorThreatMultiplier = AtLeast(DirectorThreatMultiplier);
            DirectorMaxActiveEnemies = Math.Max(1, DirectorMaxActiveEnemies);
            EnemySpawnWeight = AtLeast(EnemySpawnWeight);
            EnemySpawnMaxConcurrent = Math.Max(0, EnemySpawnMaxConcurrent);
            EnemyAvailableAfterSeconds = Math.Max(0f, EnemyAvailableAfterSeconds);
            EnemyAvailableUntilSeconds = Math.Max(0f, EnemyAvailableUntilSeconds);
            EnemyDirectorSpeedMultiplier = AtLeast(EnemyDirectorSpeedMultiplier);
            PressurePulseMultiplier = AtLeast(PressurePulseMultiplier);
            PressurePulseDurationSeconds = Math.Max(0f, PressurePulseDurationSeconds);
            BossHullMultiplier = AtLeast(BossHullMultiplier);
            BossDamageMultiplier = AtLeast(BossDamageMultiplier);
            BossBaseHull = AtLeast(BossBaseHull);
            BossBaseDamage = AtLeast(BossBaseDamage);
            BossBaseMoveSpeed = AtLeast(BossBaseMoveSpeed);
            BossAttackCooldown = AtLeast(BossAttackCooldown);
            BossPreferredRange = Math.Max(0f, BossPreferredRange);
            BossSpawnSeconds = Math.Max(1f, BossSpawnSeconds);
            PoiDamageMultiplier = AtLeast(PoiDamageMultiplier);
            PoiHealingMultiplier = AtLeast(PoiHealingMultiplier);
            PoiXpMultiplier = AtLeast(PoiXpMultiplier);
            PoiTravelSeconds = Math.Max(0f, PoiTravelSeconds);
            PoiInteractionRiskDamage = Math.Max(0f, PoiInteractionRiskDamage);
            PoiFailureChance = Math.Max(0f, Math.Min(1f, PoiFailureChance));
            if (WeaponLoadout == null) WeaponLoadout = new List<BalanceWeaponLoadoutEntry>();
            if (EnemyRoster == null) EnemyRoster = new List<BalanceEnemyRosterEntry>();
            if (ScriptedWaves == null) ScriptedWaves = new List<BalanceWaveDefinition>();
        }

        static float AtLeast(float value) => Math.Max(0.01f, value);

        public float CaptainHullMultiplier => Captain switch
        {
            BalanceLabCaptain.Brine => 1.10f,
            BalanceLabCaptain.Flint => 0.90f,
            BalanceLabCaptain.Richie => 0.95f,
            BalanceLabCaptain.Ember => 0.92f,
            BalanceLabCaptain.Roxie => 0.88f,
            _ => 1f
        };

        public float CaptainMoveSpeedMultiplier => Captain switch
        {
            BalanceLabCaptain.Brine => 5.7f / 6.2f,
            BalanceLabCaptain.Flint => 6.3f / 6.2f,
            BalanceLabCaptain.Richie => 6.1f / 6.2f,
            BalanceLabCaptain.Ember => 5.9f / 6.2f,
            BalanceLabCaptain.Roxie => 6.5f / 6.2f,
            _ => 1f
        };
    }
}

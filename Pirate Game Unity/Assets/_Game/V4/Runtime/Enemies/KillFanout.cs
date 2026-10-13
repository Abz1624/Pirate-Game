using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// What a kill sets off, in V3's order (M14; was LegacyEnemyKillTargets over the legacy kill path): the run's kill count,
    /// the Killed event, the kill gold (V3 SaltwindStageEconomyLogic.ResolveKillGoldDrop over the stage config, same Director
    /// draw), drops and the powerup roll, the encounter tracker, the stage's boss-killed moment (V3 told POIManager right
    /// before TriggerBus.Kill; M14: effects and the trace recorder read the Killed event), item kill counters and the weapon kill record. V3's miniboss-arena notify is gone: the V4 timeline
    /// owns arenas.
    /// </summary>
    public sealed class KillFanout : IEnemyKillTargets
    {
        public Action<bool> RegisterKill;
        /// <summary>Publishes Killed (unit id, flags: 1 elite, 2 boss, 4 pays ordinary rewards; position, run time, weapon).</summary>
        public Action<int, int, Vector3, float, string> Killed;
        public Func<float> RunTime;
        public Func<StageKillGold> KillGold;
        public IRunRandom Random;
        public Action<Vector3, int, int> SpawnKillDrops;
        public Action<Vector3, bool, bool> RollPowerup;
        public EncounterTracker Encounters;
        public Action BossKilled;
        public Action ItemKill;
        public Action<string> WeaponKill;
        /// <summary>V5 M4b: extra gold a kill by this weapon drops (Cursed Doubloons; capped per run by the provider).</summary>
        public Func<string, int> WeaponKillGold;
        /// <summary>V5 M5: elite kill gold and XP × this (Black Spot); boss gold × this (the Plunderer Logbook).</summary>
        public Func<float> EliteRewardMultiplier, BossGoldMultiplier;
        /// <summary>Hackathon (13 Oct, owner): Devil's Match. The unit is burning as it dies (by unit id; it is still in its
        /// slot), and what its death sets off (the blast, at its position; the build decides whether an item asks for it).</summary>
        public Func<int, bool> IsBurning;
        public Action<Vector3> BurningDeath;
        float _xpCarry, _goldCarry;

        static int Carry(float value, ref float carry)
        {
            value += carry;
            var whole = Mathf.FloorToInt(value);
            carry = value - whole;
            return whole;
        }

        public void OnKilled(in EnemyUnit e, EnemyDefinition def)
        {
            RegisterKill?.Invoke(e.isBoss);
            Killed?.Invoke(e.id, (e.isElite ? 1 : 0) | (e.isBoss ? 2 : 0) | (e.ordinaryRewards ? 4 : 0), e.position,
                RunTime != null ? RunTime() : 0f, e.lastHitWeaponId);
            // M11: an adaptive elite marked "ordinary rewards" pays the unit's ordinary rewards (no Rich gold, elite payout,
            // forced powerup or elite-kill item procs); playtest round 1 made elite rewards the default (a DirectorConfig switch).
            var rewardElite = e.isElite && !e.ordinaryRewards;
            var rewardRich = e.affix == EliteAffix.Rich && !e.ordinaryRewards;
            var gold = def.goldValue;
            var killGold = KillGold?.Invoke();
            if (killGold != null && Random != null) gold = ResolveKillGold(killGold, def, Random, rewardRich, rewardElite, e.isBoss);
            var xp = def.xpValue;
            if (rewardElite && EliteRewardMultiplier != null)
            {
                // V5 M5 Black Spot: +10% of a 2-6 XP elite rounds away, so the fraction carries to the next elite kill.
                var m = EliteRewardMultiplier();
                if (m != 1f) { xp = Carry(xp * m, ref _xpCarry); gold = Carry(gold * m, ref _goldCarry); }
            }
            if ((e.isBoss || def.isBoss) && BossGoldMultiplier != null)
            {
                var m = BossGoldMultiplier();
                if (m != 1f) gold = Carry(gold * m, ref _goldCarry);
            }
            // The coins' bonus last: capped per run, never multiplied.
            if (WeaponKillGold != null && !string.IsNullOrEmpty(e.lastHitWeaponId)) gold += WeaponKillGold(e.lastHitWeaponId);
            SpawnKillDrops?.Invoke(e.position, xp, gold);
            RollPowerup?.Invoke(e.position, e.isBoss, rewardElite);
            Encounters?.RegisterKill(e.id);
            if (e.isBoss) BossKilled?.Invoke();
            ItemKill?.Invoke();
            if (!string.IsNullOrEmpty(e.lastHitWeaponId)) WeaponKill?.Invoke(e.lastHitWeaponId);
            if (BurningDeath != null && IsBurning != null && IsBurning(e.id)) BurningDeath(e.position);
        }

        /// <summary>V3 SaltwindStageEconomyLogic.ResolveKillGoldDrop: ordinary kills drop gold on a Director-stream roll,
        /// specialists and scheduled elites a fixed amount, the boss its own value; a Rich enemy doubles it.</summary>
        public static int ResolveKillGold(StageKillGold g, EnemyDefinition def, IRunRandom random, bool richAffix, bool isElite, bool isBoss)
        {
            if (g == null || def == null || random == null) return 0;
            int amount;
            if (isBoss || def.isBoss) amount = def.goldValue;
            else if (def.isMiniboss || (isElite && def.threatCost >= 3)) amount = g.scheduledEliteGoldAmount;
            else if (IsSpecialist(def)) amount = g.specialistEnemyGoldAmount;
            else amount = random.DirectorFloat() < g.ordinaryEnemyGoldChance ? g.ordinaryEnemyGoldAmount : 0;
            if (richAffix && amount > 0) amount *= 2;
            return amount;
        }

        static bool IsSpecialist(EnemyDefinition def)
        {
            if (def.tags != null)
                for (var i = 0; i < def.tags.Length; i++)
                    if (def.tags[i] == "specialist") return true;
            return def.threatCost >= 3 && !def.isBoss && !def.isMiniboss;
        }
    }
}

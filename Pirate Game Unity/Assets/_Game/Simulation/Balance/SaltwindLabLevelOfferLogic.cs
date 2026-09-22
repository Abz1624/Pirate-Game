using System;
using System.Collections.Generic;
using RoguePirates.Simulation;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>
    /// Saltwind fill-state level-up rolls for Balance Lab (matches LevelOfferGenerator weights + pity; weapons-only autopick).
    /// </summary>
    public static class SaltwindLabLevelOfferLogic
    {
        public enum LevelUpAction
        {
            None,
            AddWeapon,
            UpgradeWeapon
        }

        public readonly struct LevelUpPick
        {
            public LevelUpAction Action { get; }
            public string WeaponId { get; }

            public LevelUpPick(LevelUpAction action, string weaponId)
            {
                Action = action;
                WeaponId = weaponId;
            }
        }

        public static bool TryGetWeaponSnapshot(
            StageBalanceProfileExport export,
            string weaponId,
            out StageWeaponStatSnapshot snapshot)
        {
            snapshot = null;
            if (export?.WeaponStatSnapshots == null || string.IsNullOrEmpty(weaponId)) return false;
            for (var i = 0; i < export.WeaponStatSnapshots.Count; i++)
            {
                var snap = export.WeaponStatSnapshots[i];
                if (snap != null && snap.WeaponId == weaponId)
                {
                    snapshot = snap;
                    return true;
                }
            }

            return false;
        }

        public static LevelUpPick RollAutopick(
            StageBalanceProfileExport export,
            SaltwindLabBuildPolicy policy,
            SaltwindLabUnlockPool pool,
            IReadOnlyList<string> ownedWeaponIds,
            IDictionary<string, int> weaponLevels,
            int playerLevel,
            int weaponSlotCap,
            ref SimRng lootRng)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            ownedWeaponIds ??= Array.Empty<string>();
            weaponLevels ??= new Dictionary<string, int>();

            var accessible = SaltwindLabProgressionParityLogic.ResolveAccessibleWeaponIds(export, pool);
            var canAdd = ownedWeaponIds.Count < weaponSlotCap && HasUnowned(accessible, ownedWeaponIds);
            var canUpgrade = HasUpgradeable(export, ownedWeaponIds, weaponLevels);
            if (!canAdd && !canUpgrade) return new LevelUpPick(LevelUpAction.None, null);

            var weights = GetWeaponOnlyFillWeights(ownedWeaponIds.Count);
            var total = 0f;
            if (canUpgrade) total += weights.upgrade;
            if (canAdd) total += weights.newWeapon;
            if (total <= 0f) return new LevelUpPick(LevelUpAction.None, null);

            var roll = lootRng.NextFloat01() * total;
            LevelUpPick pick;
            if (canUpgrade)
            {
                if (roll <= weights.upgrade)
                {
                    var upgradeId = PickUpgradeWeapon(export, ownedWeaponIds, weaponLevels, ref lootRng);
                    pick = string.IsNullOrEmpty(upgradeId)
                        ? new LevelUpPick(LevelUpAction.None, null)
                        : new LevelUpPick(LevelUpAction.UpgradeWeapon, upgradeId);
                }
                else
                {
                    var newId = PickNewWeapon(export, policy, pool, ownedWeaponIds, ref lootRng);
                    pick = string.IsNullOrEmpty(newId)
                        ? new LevelUpPick(LevelUpAction.None, null)
                        : new LevelUpPick(LevelUpAction.AddWeapon, newId);
                }
            }
            else
            {
                var newId = PickNewWeapon(export, policy, pool, ownedWeaponIds, ref lootRng);
                pick = string.IsNullOrEmpty(newId)
                    ? new LevelUpPick(LevelUpAction.None, null)
                    : new LevelUpPick(LevelUpAction.AddWeapon, newId);
            }

            pick = ApplyPityIfNeeded(export, pool, ownedWeaponIds, weaponLevels, playerLevel, weaponSlotCap, pick, ref lootRng);
            return pick;
        }

        static LevelUpPick ApplyPityIfNeeded(
            StageBalanceProfileExport export,
            SaltwindLabUnlockPool pool,
            IReadOnlyList<string> ownedWeaponIds,
            IDictionary<string, int> weaponLevels,
            int playerLevel,
            int weaponSlotCap,
            LevelUpPick pick,
            ref SimRng lootRng)
        {
            var target = GetPityTargetWeaponCount(
                playerLevel,
                export.Progression?.NewWeaponOfferProtectionLevels);
            if (target <= 0 || ownedWeaponIds.Count >= target || ownedWeaponIds.Count >= weaponSlotCap)
                return pick;
            if (pick.Action == LevelUpAction.AddWeapon) return pick;

            var pityId = PickNewWeapon(export, SaltwindLabBuildPolicy.None, pool, ownedWeaponIds, ref lootRng);
            return string.IsNullOrEmpty(pityId)
                ? pick
                : new LevelUpPick(LevelUpAction.AddWeapon, pityId);
        }

        static int GetPityTargetWeaponCount(int playerLevel, int[] protectionLevels)
        {
            protectionLevels ??= new[] { 4, 8, 12, 16 };
            var target = 0;
            for (var i = 0; i < protectionLevels.Length; i++)
            {
                if (playerLevel >= protectionLevels[i])
                    target = i + 3;
            }

            return target;
        }

        static (float upgrade, float newWeapon) GetWeaponOnlyFillWeights(int equippedWeapons)
        {
            float upgrade;
            float newWeapon;
            if (equippedWeapons <= 2)
            {
                upgrade = 23f;
                newWeapon = 42f;
            }
            else if (equippedWeapons == 3)
            {
                upgrade = 32f;
                newWeapon = 32f;
            }
            else if (equippedWeapons == 4)
            {
                upgrade = 38f;
                newWeapon = 26f;
            }
            else if (equippedWeapons == 5)
            {
                upgrade = 46f;
                newWeapon = 18f;
            }
            else
            {
                upgrade = 58f;
                newWeapon = 0f;
            }

            return (upgrade, newWeapon);
        }

        static bool HasUnowned(IReadOnlyList<string> accessible, IReadOnlyList<string> owned)
        {
            for (var i = 0; i < accessible.Count; i++)
            {
                if (!Contains(owned, accessible[i])) return true;
            }

            return false;
        }

        static bool HasUpgradeable(
            StageBalanceProfileExport export,
            IReadOnlyList<string> owned,
            IDictionary<string, int> weaponLevels)
        {
            for (var i = 0; i < owned.Count; i++)
            {
                var id = owned[i];
                weaponLevels.TryGetValue(id, out var level);
                if (level <= 0) level = 1;
                var max = TryGetWeaponSnapshot(export, id, out var snap) ? snap.MaxLevel : 12;
                if (level < max) return true;
            }

            return false;
        }

        static string PickNewWeapon(
            StageBalanceProfileExport export,
            SaltwindLabBuildPolicy policy,
            SaltwindLabUnlockPool pool,
            IReadOnlyList<string> ownedWeaponIds,
            ref SimRng lootRng)
        {
            var accessible = SaltwindLabProgressionParityLogic.ResolveAccessibleWeaponIds(export, pool);
            var queue = SaltwindBalanceLabScenarioFactory.ResolveWeaponQueue(policy, pool, export);
            var candidates = new List<string>();
            var weights = new List<float>();
            for (var i = 0; i < accessible.Count; i++)
            {
                var id = accessible[i];
                if (Contains(ownedWeaponIds, id)) continue;
                candidates.Add(id);
                weights.Add(PolicyWeight(id, queue));
            }

            if (candidates.Count == 0) return null;
            return WeightedPick(candidates, weights, ref lootRng);
        }

        static float PolicyWeight(string weaponId, IReadOnlyList<string> queue)
        {
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i] == weaponId)
                    return Math.Max(1f, queue.Count - i);
            }

            return 1f;
        }

        static string PickUpgradeWeapon(
            StageBalanceProfileExport export,
            IReadOnlyList<string> ownedWeaponIds,
            IDictionary<string, int> weaponLevels,
            ref SimRng lootRng)
        {
            var candidates = new List<string>();
            var weights = new List<float>();
            for (var i = 0; i < ownedWeaponIds.Count; i++)
            {
                var id = ownedWeaponIds[i];
                weaponLevels.TryGetValue(id, out var level);
                if (level <= 0) level = 1;
                var max = TryGetWeaponSnapshot(export, id, out var snap) ? snap.MaxLevel : 12;
                if (level >= max) continue;
                candidates.Add(id);
                weights.Add(id == ownedWeaponIds[0] ? 1.2f : 1f);
            }

            if (candidates.Count == 0) return null;
            return WeightedPick(candidates, weights, ref lootRng);
        }

        static string WeightedPick(IReadOnlyList<string> candidates, IReadOnlyList<float> weights, ref SimRng rng)
        {
            var total = 0f;
            for (var i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0f) return candidates[0];
            var roll = rng.NextFloat01() * total;
            var acc = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        static bool Contains(IReadOnlyList<string> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id) return true;
            }

            return false;
        }
    }
}

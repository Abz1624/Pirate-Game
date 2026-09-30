using System.Linq;
using RoguePirates.V4.Config;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M3 (build balance): evolutions reachable (Q18b: the evolution card from weapon level 8 with its item; Rare and
    /// Epic cards grant 2 levels, Legendary 3; a pity rule), then the balance retunes the weapon, Logbook, item and Captain
    /// models ask for (plan/v5/phase-03-builds/PLAN.md). Every value changed here has its V3 value in the v3-parity preset.
    /// </summary>
    public static class V5BuildBalance
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string BalanceVersion = "saltwind-v5-m3";

        [MenuItem("RoguePirates/V5/Write M3 evolution reachability")]
        public static string ApplyEvolutionRules()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            var p = set != null ? set.All<ProgressionConfig>().FirstOrDefault() : null;
            if (p == null) return "progression config not found";
            Undo.RecordObject(p, "V5 M3 evolution rules");
            p.evolutionUnlockLevel = 8;
            p.levelsPerRarity = new[] { 1, 1, 2, 2, 3 };
            p.rarePityLevelUps = 6;
            p.pityMinRarity = 2;
            p.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return "ok: evolution from level 8, levels per rarity 1/1/2/2/3, pity after 6";
        }

        /// <summary>
        /// G3.2: runs the weapon parity tuner (RoguePirates.V4.DevTools.Balance.WeaponModel.TuneParity, deterministic) on a
        /// runtime copy of the set, then writes each weapon's tuned base damage and upgrade increments to its asset. Run
        /// it once on the pre-M3 weapons (it tunes from what the assets hold).
        /// </summary>
        /// <summary>
        /// I3.2 and G3.4: chest prices for 4–6 items by 10:00 (were 20, then 25 × 1.22^n), a 40% chance that a chest gives
        /// the missing evolution item of a carried weapon at level 8 or higher, and Flint's hull 90 → 120 so every
        /// Captain's median run lies within ±10% of the others' (the simulator's baseline personality, 200 runs each).
        /// </summary>
        [MenuItem("RoguePirates/V5/Write M3 item flow and Captains")]
        public static string ApplyItemFlowAndCaptains()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            var stage = set != null ? set.All<StageContentConfig>().FirstOrDefault() : null;
            if (stage == null) return "stage config not found";
            Undo.RecordObject(stage, "V5 M3 item flow");
            stage.chestPriceIncrementBase = 45;
            stage.chestPriceGrowth = 1.35f;
            stage.evolutionItemChance = 0.4f;
            stage.evolutionItemMinWeaponLevel = 0; // follows the evolution card's level (8)
            stage.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(stage);
            // Captains are data, not content ids in game code; this Editor step names the one the model found low.
            var flint = set.All<CaptainConfig>().FirstOrDefault(c => c.captainKey == "flint");
            if (flint != null)
            {
                Undo.RecordObject(flint, "V5 M3 Captains");
                flint.baseHull = 120f;
                flint.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(flint);
            }
            AssetDatabase.SaveAssets();
            return "ok: chests 20 then 45 × 1.35^n, evolution item 40% from level 8, Flint hull 120";
        }

        [MenuItem("RoguePirates/V5/Write M3 weapon parity")]
        public static string ApplyWeaponParity()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            using var rt = new ConfigSetRuntime(set);
            var content = new RoguePirates.V4.DevTools.Balance.SimContent(rt);
            var tunes = RoguePirates.V4.DevTools.Balance.WeaponModel.TuneParity(content, 300, 4);
            var lines = new System.Text.StringBuilder();
            foreach (var tuned in rt.All<WeaponConfig>())
            {
                var asset = set.All<WeaponConfig>().FirstOrDefault(w => w.configId == tuned.configId);
                if (asset == null) continue;
                Undo.RecordObject(asset, "V5 M3 weapon parity");
                asset.baseDamage = tuned.baseDamage;
                for (var i = 0; i < asset.upgradeAxes.Length && i < tuned.upgradeAxes.Length; i++)
                {
                    asset.upgradeAxes[i].baseIncrement = tuned.upgradeAxes[i].baseIncrement;
                    asset.upgradeAxes[i].maxAccumulated = tuned.upgradeAxes[i].maxAccumulated;
                }
                asset.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(asset);
                var t = tunes.FirstOrDefault(x => x.WeaponId == asset.weaponId);
                lines.Append($"{asset.weaponId}: damage ×{t.DamageScale:0.00}, growth ×{t.GrowthScale:0.00}; ");
            }
            AssetDatabase.SaveAssets();
            return "ok: " + lines;
        }

        /// <summary>
        /// G3.3: runs the Logbook and item band tuner (RoguePirates.V4.DevTools.Balance.BuildModel.TuneBands, deterministic)
        /// on a runtime copy and writes the changed Logbooks and items to their assets. Run it once on the pre-M3 values.
        /// </summary>
        [MenuItem("RoguePirates/V5/Write M3 Logbook and item bands")]
        public static string ApplyPieceBands()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            if (set == null) return "config set not found";
            using var rt = new ConfigSetRuntime(set);
            var log = RoguePirates.V4.DevTools.Balance.BuildModel.TuneBands(new RoguePirates.V4.DevTools.Balance.SimContent(rt));
            var changed = new System.Collections.Generic.HashSet<string>(log.Select(t => t.Id));
            foreach (var tuned in rt.All<LogbookConfig>().Where(l => changed.Contains(l.logbookId)))
            {
                var asset = set.All<LogbookConfig>().First(l => l.configId == tuned.configId);
                Undo.RecordObject(asset, "V5 M3 bands");
                asset.perLevelAdditive = tuned.perLevelAdditive;
                asset.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(asset);
            }
            foreach (var tuned in rt.All<ItemConfig>().Where(i => changed.Contains(i.itemId)))
            {
                var asset = set.All<ItemConfig>().First(i => i.configId == tuned.configId);
                Undo.RecordObject(asset, "V5 M3 bands");
                for (var i = 0; i < asset.statModifiers.Length; i++) { asset.statModifiers[i].additive = tuned.statModifiers[i].additive; asset.statModifiers[i].multiplicative = tuned.statModifiers[i].multiplicative; }
                for (var i = 0; i < asset.effects.Length; i++) { asset.effects[i].value = tuned.effects[i].value; asset.effects[i].cap = tuned.effects[i].cap; }
                asset.balanceVersion = BalanceVersion;
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            return "ok: " + string.Join("; ", log.Select(t => $"{t.Id}.{t.Field} {t.From:0.###} → {t.To:0.###}"));
        }
    }
}

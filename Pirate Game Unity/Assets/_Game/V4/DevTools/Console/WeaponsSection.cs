using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Legacy;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Weapons (M5): every equipped weapon with its V4 state (behavior, level, cooldown or stream, evolution), Fire now,
    /// Rank up, Remove, Evolve; add or solo any weapon. Combat numbers, pattern blocks and evolutions are edited live in
    /// the Config section (weapon.* and evolution.* assets).
    /// </summary>
    public sealed class WeaponsSection : IConsoleSection
    {
        public string Title => "Weapons";
        public string SearchText => "weapons weapon fire cooldown rank level evolution evolve solo loadout projectiles";

        const float TextWidth = 440f;
        GUIStyle _wrap;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game?.Context == null || !game.Context.TryGet<IWeaponManager>(out var weapons)) { GUILayout.Label("No V4 Weapon Manager in this scene."); return; }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };

            GUILayout.Label($"{weapons.Slots.Count} weapons · {weapons.Projectiles.ActiveCount} projectiles in flight. Numbers: Config section (weapon.*, evolution.*, weapon_rules).", _wrap, GUILayout.Width(TextWidth));
            var evolutions = game.Config?.All<WeaponEvolutionConfig>().ToList();
            for (var i = 0; i < weapons.Slots.Count; i++)
            {
                var w = weapons.Slots[i];
                var state = w.Config == null ? "no config"
                    : w.Config.behavior == ConfigEnums.WeaponBehavior.ConeStream
                        ? (w.StreamRemaining > 0f ? $"stream {w.StreamRemaining:0.0}s" : w.RechargeRemaining > 0f ? $"recharge {w.RechargeRemaining:0.0}s" : "ready")
                        : w.CooldownTimer > 0f ? $"cd {w.CooldownTimer:0.00}s" : "ready";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{w.Id}  lv{w.Level}  {w.Config?.behavior}{(w.Evolution != null ? "  → " + w.Evolution.displayName : "")}  {state}", GUILayout.Width(300));
                if (GUILayout.Button("Fire", GUILayout.Width(40))) weapons.FireSlot(i);
                if (GUILayout.Button("+1", GUILayout.Width(28))) LegacyWeaponLab.RankUp(game, i);
                var evolution = evolutions?.FirstOrDefault(e => e.baseWeapon != null && e.baseWeapon.weaponId == w.Id);
                GUI.enabled = evolution != null && w.Evolution == null;
                if (GUILayout.Button("Evolve", GUILayout.Width(52)) && evolution != null) LegacyWeaponLab.Evolve(game, w.Id, evolution.evolutionId);
                GUI.enabled = true;
                if (GUILayout.Button("x", GUILayout.Width(22))) LegacyWeaponLab.Remove(game, i);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.Label("Add or solo a weapon:", _wrap, GUILayout.Width(TextWidth));
            var owned = weapons.Slots.Select(s => s.Id).ToList();
            foreach (var config in game.Config.All<WeaponConfig>().OrderBy(c => c.weaponId))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{config.weaponId} ({config.behavior})", GUILayout.Width(250));
                GUI.enabled = !owned.Contains(config.weaponId) && owned.Count < LegacyWeaponLab.SlotCap(game);
                if (GUILayout.Button("Add", GUILayout.Width(44))) LegacyWeaponLab.Add(game, config.weaponId);
                GUI.enabled = true;
                if (GUILayout.Button("Solo", GUILayout.Width(44))) LegacyWeaponLab.Solo(game, config.weaponId);
                GUILayout.EndHorizontal();
            }
        }
    }
}

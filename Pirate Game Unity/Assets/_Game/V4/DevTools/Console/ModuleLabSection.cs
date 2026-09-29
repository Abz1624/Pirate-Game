using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Module Lab (M2): spawn any archetype or variant ahead of the ship, clear all enemies, and inspect the resolved
    /// values with the layer that set each one. Shown only in the V4ModuleLab scene.
    /// </summary>
    public sealed class ModuleLabSection : IConsoleSection
    {
        public string Title => "Module Lab";
        public string SearchText => "lab spawn enemy archetype variant inspector";

        string _selected;
        float _distance = 14f;
        PlayerShipController _ship;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game == null || !game.IsModuleLab) { GUILayout.Label("Open the V4ModuleLab scene to spawn enemies on demand."); return; }
            var config = game.Config;
            if (_ship == null) _ship = Object.FindFirstObjectByType<PlayerShipController>();
            var ship = _ship;
            RoguePirates.V4.Spawning.IEnemySpawnSink sink = null;
            IEnemyDevControl dev = null;
            if (config == null || ship == null || game.Context == null || !game.Context.TryGet(out sink) || !game.Context.TryGet(out dev))
            {
                GUILayout.Label("Run not ready.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Enemies alive: {sink.ActiveCount}", GUILayout.Width(130));
            GUILayout.Label("Distance", GUILayout.Width(56));
            _distance = GUILayout.HorizontalSlider(_distance, 6f, 30f, GUILayout.Width(90));
            GUILayout.Label($"{_distance:0} m", GUILayout.Width(40));
            if (GUILayout.Button("Clear all"))
            {
                dev.DespawnAll();
                // A boss or Powder Cache elites removed this way were never sunk: the stage forgets them (summon or spring again).
                if (game.Context.TryGet<RoguePirates.V4.Stage.IStageContent>(out var stage)) { stage.Module.ForgetDespawnedBoss(); stage.Module.ForgetDespawnedPowderCaches(); }
            }
            GUILayout.EndHorizontal();

            // Elite rows spawn the elite's own definition flagged elite (Rich affix), so its elite combat profile runs
            // (R-03). Spawns go through the enemy sink's Spawn without a spawn profile: no encounter overrides or
            // profile-driven initial timers.
            var rows = config.All<EnemyArchetypeConfig>().Where(a => a.inLegacyDatabase)
                .Select(a => (id: a.enemyId, label: a.enemyId, spawnId: a.enemyId, elite: false, stats: (System.Func<ResolvedEnemyStats>)(() => EnemyStatResolver.Resolve(a))))
                .Concat(config.All<EnemyVariantConfig>().Where(v => v.baseArchetype != null).Select(v =>
                    (id: v.variantId, label: v.variantId + (v.kind == VariantKind.RegistryElite ? " (elite)" : " (variant)"), spawnId: v.variantId,
                        elite: v.kind == VariantKind.RegistryElite, stats: (System.Func<ResolvedEnemyStats>)(() => EnemyStatResolver.Resolve(v)))))
                .Concat(config.All<EnemyArchetypeConfig>().Where(a => !a.inLegacyDatabase).Select(a =>
                    (id: a.enemyId, label: a.enemyId + " (V4 only)", spawnId: a.enemyId, elite: false, stats: (System.Func<ResolvedEnemyStats>)(() => EnemyStatResolver.Resolve(a)))))
                .OrderBy(r => r.label).ToList();

            foreach (var row in rows)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(row.label, _selected == row.id ? GUI.skin.box : GUI.skin.label, GUILayout.Width(210))) _selected = row.id;
                if (GUILayout.Button("Spawn", GUILayout.Width(60))) Spawn(sink, ship, row.spawnId, row.elite);
                GUILayout.EndHorizontal();
                if (_selected == row.id) DrawInspector(row.stats());
            }
        }

        void Spawn(RoguePirates.V4.Spawning.IEnemySpawnSink sink, PlayerShipController ship, string enemyId, bool elite)
        {
            var index = sink.FindDefinition(enemyId);
            if (index < 0) { Debug.LogWarning($"Module Lab: {enemyId} is not in the run database"); return; }
            var forward = ship.MovementForward.sqrMagnitude > 0.01f ? ship.MovementForward.normalized : Vector3.forward;
            var position = ship.Position + forward * _distance;
            sink.Spawn(index, position, elite ? ConfigEnums.EliteAffix.Rich : ConfigEnums.EliteAffix.None, 1f, 1f, 1f);
        }

        static void DrawInspector(ResolvedEnemyStats r)
        {
            void Row(string name, object value)
            {
                var layer = r.Provenance.TryGetValue(name, out var l) ? l.ToString() : "-";
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(name, GUILayout.Width(120));
                GUILayout.Label(value?.ToString() ?? "none", GUILayout.Width(120));
                GUILayout.Label(layer);
                GUILayout.EndHorizontal();
            }
            Row("behavior", r.behavior); Row("baseHP", r.baseHP); Row("baseDamage", r.baseDamage); Row("moveSpeed", r.moveSpeed);
            Row("radius", r.radius); Row("threatCost", r.threatCost); Row("xpValue", r.xpValue); Row("goldValue", r.goldValue);
            Row("preferredRange", r.preferredRange); Row("attackCooldown", r.attackCooldown); Row("tier", r.tier);
            Row("visualPrefab", r.visualPrefab != null ? r.visualPrefab.name : "none (placeholder)");
            Row("combat", r.combat != null && r.combat.registered ? r.combat.archetype.ToString() : "not registered");
        }
    }
}

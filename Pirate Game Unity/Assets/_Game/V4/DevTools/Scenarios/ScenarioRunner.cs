using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Online;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Scenarios
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): sets up a loaded scenario in the run scene through the game's own hooks (the console's and
    /// the Module Lab's): the clock jump (<see cref="RunManager.JumpClock"/>), the timeline's Trigger now / Skip, live
    /// config edits, <c>ProgressionModule.DevSetWeapons/DevRankUp/DevEvolve/DevRankLogbook/GrantLevelUp</c>, the enemy
    /// sink's spawn (as the Module Lab spawns), the stage's points of interest and boss summon. Each frame after that it
    /// keeps what the scenario holds (enemies sunk at once, the ship held into a shore). Lives and dies with the run scene;
    /// the switches the load changed (DemoMode, the saved Captain, a 1v1 relay) go back when it goes.
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        /// <summary>What the load changed outside the scene, put back when the scenario's scene goes.</summary>
        public struct Restore
        {
            public bool DemoOff;
            public int CaptainId;

            public void Apply()
            {
                DemoMode.Off = DemoOff;
                if (Progression.CaptainChoice.LegacyId != CaptainId) Progression.CaptainChoice.LegacyId = CaptainId;
            }
        }

        public ScenarioConfig Config { get; private set; }
        public V4Game Game { get; private set; }
        public PlayerShipController Ship { get; private set; }
        /// <summary>The in-process 1v1 relay (null outside a 1v1 scenario).</summary>
        public FakePvpRelay Relay { get; private set; }
        /// <summary>Setup problems (unknown ids, no shore...): the scenario ran without that part.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>The units the scenario spawned, in its enemy list's order.</summary>
        public readonly List<(string enemyId, int unitId)> Spawned = new List<(string, int)>();
        /// <summary>Points of interest the scenario placed (layout index per entry; -1 when it failed).</summary>
        public readonly List<int> PlacedPois = new List<int>();
        /// <summary>The shore the ship was driven into (IntoShore), and its outward normal.</summary>
        public Vector3 Shore { get; private set; }
        public Vector3 ShoreNormal { get; private set; }
        /// <summary>Enemies sunk by <see cref="ScenarioConfig.sinkOtherEnemies"/>.</summary>
        public int Sunk { get; private set; }

        Restore _restore;
        IEnemyDevControl _dev;
        EnemySimulation _sim;
        readonly HashSet<int> _own = new HashSet<int>();
        float _holdAt;

        public void Begin(ScenarioConfig scenario, V4Game game, FakePvpRelay relay, Restore restore)
        {
            Config = scenario;
            Game = game;
            Relay = relay;
            _restore = restore;
            Ship = FindFirstObjectByType<PlayerShipController>();
            var context = game.Context;
            context.TryGet(out _dev);
            _sim = game.Shell != null && game.Shell.Enemies != null ? game.Shell.Enemies.Simulation : null;
            if (Ship == null) { Errors.Add("no player ship"); return; }
            Ship.Damage.SetPlaytestInvulnerable(scenario.invulnerable);

            relay?.StartClock();
            ApplySwitches(scenario, game);
            ApplyClock(scenario, game);
            if (!game.IsPvp) ApplyBuild(scenario, game);
            ApplyShip(scenario);
            ApplyPois(scenario, game);
            ApplyEnemies(scenario, game);
            ApplyBoss(scenario, game);
            context.TryGet<ISpawnController>(out var spawn);
            foreach (var id in scenario.triggerPhases)
                if (spawn == null || !spawn.TriggerNow(id)) Errors.Add($"phase {id}: could not trigger");
        }

        // ---- Setup -------------------------------------------------------------------------------------------------

        void ApplySwitches(ScenarioConfig s, V4Game game)
        {
            var config = game.Config;
            if (config == null) { Errors.Add("no config set"); return; }
            var experiment = config.Single<ExperimentConfig>();
            if (experiment != null && experiment.encounterSpawnEnabled != s.ambientSpawning)
                Edit(experiment.configId, nameof(ExperimentConfig.encounterSpawnEnabled), s.ambientSpawning ? "true" : "false");
            foreach (var e in s.configEdits) Edit(e.configId, e.path, e.value);
            if (s.skipLevelUpCards && !game.IsPvp) game.Build.Progression.AutoLevelUp = () => { };
        }

        void Edit(string configId, string path, string value)
        {
            var time = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            if (!Game.Config.TryApplyEdit(configId, path, value, time, out var error)) Errors.Add($"{configId}.{path} = {value}: {error}");
        }

        /// <summary>The clock: phases already due are skipped first (else the jump fires them all at once), then the jump.</summary>
        void ApplyClock(ScenarioConfig s, V4Game game)
        {
            game.Context.TryGet<ISpawnController>(out var spawn);
            var timeline = game.Config?.Single<WaveTimelineConfig>();
            if (spawn != null && timeline != null && s.scriptedPhases != ScenarioConfig.ScriptedPhaseMode.AsTheGame)
                foreach (var p in timeline.phases)
                {
                    if (p == null || s.triggerPhases.Contains(p.phaseId) || PhaseKinds.IsAmbient(p) || p.isBoss) continue;
                    var scripted = PhaseKinds.IsFocus(p) || PhaseKinds.IsPulse(p);
                    var skip = s.scriptedPhases == ScenarioConfig.ScriptedPhaseMode.SkipAll
                        ? scripted || p.isOvertime || p.isStorm
                        : scripted && p.trigger.kind == PhaseTriggerKind.Time && p.trigger.timeSeconds < s.clockSeconds;
                    if (skip) spawn.Skip(p.phaseId);
                }
            var run = RunManager.Instance;
            if (run != null && s.clockSeconds > run.ElapsedSeconds) run.JumpClock(s.clockSeconds);
        }

        void ApplyBuild(ScenarioConfig s, V4Game game)
        {
            var config = game.Config;
            var progression = game.Build.Progression;
            var loadout = s.loadout;
            var weaponIds = loadout.weapons.Select(w => w.weaponId).ToList();
            foreach (var id in weaponIds)
                if (!config.All<WeaponConfig>().Any(w => w.weaponId == id)) Errors.Add($"weapon {id}: unknown");
            weaponIds.RemoveAll(id => !config.All<WeaponConfig>().Any(w => w.weaponId == id));
            if (loadout.replaceWeapons) progression.DevSetWeapons(weaponIds);
            else if (weaponIds.Any(id => progression.Loadout.IndexOf(id) < 0))
                progression.DevSetWeapons(progression.OwnedWeapons.Concat(weaponIds.Where(id => progression.Loadout.IndexOf(id) < 0)).ToList());

            foreach (var w in loadout.weapons)
            {
                var index = progression.Loadout.IndexOf(w.weaponId);
                if (index < 0) continue;
                for (var guard = 0; progression.WeaponLevel(w.weaponId) < w.level && guard < 64; guard++) progression.DevRankUp(index);
                if (string.IsNullOrEmpty(w.evolutionId)) continue;
                if (config.All<WeaponEvolutionConfig>().Any(e => e.evolutionId == w.evolutionId)) progression.DevEvolve(w.weaponId, w.evolutionId);
                else Errors.Add($"evolution {w.evolutionId}: unknown");
            }
            foreach (var l in loadout.logbooks)
            {
                if (!config.All<LogbookConfig>().Any(c => c.logbookId == l.logbookId)) { Errors.Add($"logbook {l.logbookId}: unknown"); continue; }
                for (var guard = 0; progression.LogbookLevel(l.logbookId) < l.rank && guard < 32; guard++) progression.DevRankLogbook(l.logbookId);
            }
            foreach (var item in loadout.items)
                if (!game.Build.Items.TryAddItem(item)) Errors.Add($"item {item}: not added (unknown or already carried)");

            // The level alone (no cards, no picks): level-ups apply through an empty automatic pick while it is raised.
            var keep = progression.AutoLevelUp;
            progression.AutoLevelUp = () => { };
            for (var guard = 0; progression.Level < loadout.playerLevel && guard < 200; guard++) progression.GrantLevelUp();
            if (loadout.xpIntoLevel > 0f) progression.AddXp(Mathf.FloorToInt(progression.XpToNext * Mathf.Clamp(loadout.xpIntoLevel, 0f, 0.99f)));
            progression.AutoLevelUp = keep;
        }

        void ApplyShip(ScenarioConfig s)
        {
            if (s.shipStart != ScenarioConfig.ShipStart.IntoShore) return;
            if (!FindShore(Ship.transform.position, out var shore, out var normal)) { Errors.Add("no island shore to drive into"); return; }
            Shore = shore;
            ShoreNormal = normal;
            Ship.PlaceForTest(shore + normal * (Ship.IslandCollisionRadius + 3f), -normal * 12f);
            _holdAt = Time.time + 0.3f;
        }

        void ApplyPois(ScenarioConfig s, V4Game game)
        {
            if (s.pointsOfInterest.Count == 0) return;
            if (!game.Context.TryGet<IStageContent>(out var stage)) { Errors.Add("no stage content"); return; }
            foreach (var poi in s.pointsOfInterest)
            {
                var index = stage.Module.DevPlacePoi(poi.type, Around(poi.distance, poi.bearing));
                PlacedPois.Add(index);
                if (index < 0) Errors.Add($"{poi.type}: no stage layout to place it in");
            }
        }

        void ApplyEnemies(ScenarioConfig s, V4Game game)
        {
            if (s.enemies.Count == 0) return;
            if (!game.Context.TryGet<IEnemySpawnSink>(out var sink)) { Errors.Add("no enemy world"); return; }
            foreach (var e in s.enemies)
            {
                var definition = sink.FindDefinition(e.enemyId);
                if (definition < 0) { Errors.Add($"enemy {e.enemyId}: not in the run database"); continue; }
                for (var i = 0; i < Mathf.Max(1, e.count); i++)
                {
                    var bearing = e.bearing + (i - (e.count - 1) * 0.5f) * e.spread;
                    var slot = sink.Spawn(definition, Around(e.distance, bearing), e.eliteAffix, 1f, Mathf.Max(0.01f, e.hpMultiplier), 1f);
                    if (slot < 0) { Errors.Add($"enemy {e.enemyId}: no room to spawn"); continue; }
                    var unit = sink.UnitIdAt(slot);
                    if (e.focus) sink.MarkFocus(unit);
                    _own.Add(unit);
                    Spawned.Add((e.enemyId, unit));
                }
            }
        }

        void ApplyBoss(ScenarioConfig s, V4Game game)
        {
            if (s.boss == ScenarioConfig.BossSummon.None) return;
            if (!game.Context.TryGet<IStageContent>(out var stage)) { Errors.Add("no stage content: no boss"); return; }
            if (s.boss == ScenarioConfig.BossSummon.Summon)
            {
                if (!stage.TrySummonBoss()) Errors.Add("the boss summon failed");
                return;
            }
            var pois = stage.Module.Layout?.pois;
            var beacon = pois != null ? pois.FindIndex(p => p.type == POIType.BossBeacon) : -1;
            if (beacon < 0) { Errors.Add("no Treasury Beacon on the stage (add one in Points of interest)"); return; }
            Ship.SetWorldPosition(pois[beacon].position + new Vector3(3f, 0f, 0f));
            stage.Module.TryCompleteInteraction(beacon);
        }

        /// <summary>A point <paramref name="distance"/> metres from the ship, <paramref name="bearing"/> degrees clockwise from the top of the screen.</summary>
        public Vector3 Around(float distance, float bearing)
        {
            var cam = Camera.main;
            var up = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (up.sqrMagnitude < 0.01f && cam != null) up = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            if (up.sqrMagnitude < 0.01f) up = Vector3.forward;
            var p = Ship.Position + Quaternion.AngleAxis(bearing, Vector3.up) * up.normalized * distance;
            return PlayBounds.Clamp(p, 2f);
        }

        /// <summary>A point off an island's shore and the shore's outward normal (cast at the nearest island).</summary>
        public static bool FindShore(Vector3 from, out Vector3 shore, out Vector3 normal)
        {
            shore = normal = Vector3.zero;
            var islands = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == IslandObstacles.Layer)
                .OrderBy(c => (c.bounds.center - from).sqrMagnitude).ToList();
            foreach (var island in islands)
            {
                var center = island.bounds.center;
                var start = center + Vector3.forward * (island.bounds.extents.magnitude + 20f);
                start.y = from.y;
                var dir = center - start;
                dir.y = 0f;
                if (!Physics.Raycast(start, dir.normalized, out var hit, dir.magnitude + 50f, IslandObstacles.Mask)) continue;
                normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.1f) continue;
                normal.Normalize();
                shore = hit.point;
                return true;
            }
            return false;
        }

        // ---- Held conditions ---------------------------------------------------------------------------------------

        void Update()
        {
            var run = RunManager.Instance;
            if (Config == null || run == null || run.IsPaused || run.IsRunEnded) return;
            if (Config.sinkOtherEnemies) SinkOthers();
            if (Config.holdIntoShore && Config.shipStart == ScenarioConfig.ShipStart.IntoShore && ShoreNormal != Vector3.zero && Time.time >= _holdAt)
            {
                _holdAt = Time.time + 0.3f;
                Ship.PlaceForTest(Shore + ShoreNormal * (Ship.IslandCollisionRadius + 2f), -ShoreNormal * 12f);
                Ship.KeepIslandCooldownsForTest(); // a held stick keeps the cooldowns PlaceForTest resets
            }
        }

        void SinkOthers()
        {
            if (_sim == null || _dev == null) return;
            var agents = _sim.Agents;
            var defs = _sim.Definitions;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active || _own.Contains(agents[i].id)) continue;
                var d = agents[i].definitionIndex;
                if (defs != null && d >= 0 && d < defs.Length && defs[d] != null && defs[d].isBoss) continue;
                _dev.Kill(i, "scenario");
                Sunk++;
            }
        }

        void OnDestroy()
        {
            if (Scenario.Current == this) Scenario.Current = null;
            _restore.Apply();
            if (Relay != null && PvpSession.Exists) PvpSession.Instance.Forget();
        }
    }
}

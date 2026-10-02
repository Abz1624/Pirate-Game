using RoguePirates.V4.DevTools.Console;
using RoguePirates.V4.DevTools.Trace;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Stage;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trailer
{
    /// <summary>
    /// Trailer capture helpers (Editor and development builds only, like the rest of DevTools). Driven from the Editor
    /// with <c>unity command eval</c>: hide the HUD and the console gear, god mode, the autopilot, film cameras around
    /// the player ship, slow motion, and one-call beats (evolve, boss, jump the clock). Nothing here changes the game's
    /// rules; it only sets up shots.
    /// </summary>
    public static class TrailerDirector
    {
        static TuningConsole Console => Object.FindFirstObjectByType<TuningConsole>();
        static V4Game Game => Console != null ? Console.Game : null;
        static PlayerShipController Ship => Object.FindFirstObjectByType<PlayerShipController>();

        /// <summary>One call before a take: HUD on or off, god mode, the autopilot, normal time.</summary>
        public static string Prepare(bool hud = false, bool god = true, bool autopilot = true, string profile = "Strong")
        {
            var ship = Ship;
            if (ship == null) return "no player ship (start a run first)";
            SetHud(hud);
            if (ship.Damage != null) ship.Damage.SetPlaytestInvulnerable(god);
            var player = Object.FindFirstObjectByType<HumanPlayer>();
            if (player != null) player.Uninstall();
            if (autopilot && System.Enum.TryParse<HumanPlayer.BuildProfile>(profile, out var p)) HumanPlayer.Install(ship, p);
            SlowMo(1f);
            return $"ready: hud {hud}, god {god}, autopilot {(autopilot ? profile : "off")}, t={Elapsed:0.0}s";
        }

        public static float Elapsed => RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;

        /// <summary>Shows or hides every canvas and the console's IMGUI gear.</summary>
        public static void SetHud(bool on)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) canvas.enabled = on;
            var console = Console;
            if (console != null) console.enabled = on;
            SetDamageNumbers(on);
            SetBoundsVeil(on);
        }

        /// <summary>The play-area veil and walls read as a purple wall in wide trailer shots.</summary>
        public static void SetBoundsVeil(bool on)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("BoundsVeil") || t.name == "V4BoundsWalls")
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        /// <summary>
        /// Hides the floating damage numbers: their pooled copies and the template new copies are made from. They are
        /// the only TextMesh objects in a run (DamageNumberPresenter); the pool renames its copies.
        /// </summary>
        public static void SetDamageNumbers(bool on)
        {
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.TryGetComponent<MeshRenderer>(out var mesh))
                    mesh.enabled = on;
        }

        /// <summary>Live enemies (for checking a crowd before a take).</summary>
        public static int Enemies()
        {
            var game = Game;
            return game != null && game.Context != null && game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink) ? sink.ActiveCount : -1;
        }

        public static void SlowMo(float scale)
        {
            Time.timeScale = Mathf.Clamp(scale, 0.05f, 4f);
            RunSection.TimeScaleChanged = !Mathf.Approximately(scale, 1f);
        }

        /// <summary>Film camera: chase, low, hero, orbit, crane, wide, top, or game (the normal follow camera).</summary>
        public static string Camera(string mode, float distance = 0f, float speed = 0f, string focus = null)
        {
            var cam = UnityEngine.Camera.main;
            var ship = Ship;
            if (cam == null || ship == null) return "no camera or ship";
            var rig = cam.GetComponent<TrailerCamera>();
            var follow = cam.GetComponent<FollowCamera>();
            if (mode == "game")
            {
                if (rig != null) rig.enabled = false;
                if (follow != null) { follow.enabled = true; follow.SnapToTarget(); }
                return "game camera";
            }
            if (rig == null) rig = cam.gameObject.AddComponent<TrailerCamera>();
            if (follow != null) follow.enabled = false;
            Transform focusTransform = null;
            if (!string.IsNullOrEmpty(focus))
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name.Contains(focus) && (focusTransform == null || t.root == t)) focusTransform = t;
            rig.Begin(ship.transform, mode, distance, speed, focusTransform);
            rig.enabled = true;
            return $"camera {mode}{(focusTransform != null ? " on " + focusTransform.name : "")}";
        }

        /// <summary>Adds a weapon by id, ranks it up and, when asked, evolves it.</summary>
        public static string Weapon(string weaponId, int ranks = 6, string evolutionId = null)
        {
            var game = Game;
            if (game == null) return "no game";
            WeaponLab.Add(game, weaponId);
            var loadout = WeaponLab.Loadout(game);
            var slot = -1;
            for (var i = 0; i < loadout.Count; i++) if (loadout[i] == weaponId) slot = i;
            if (slot < 0) return $"{weaponId} not added";
            for (var i = 0; i < ranks; i++) WeaponLab.RankUp(game, slot);
            if (!string.IsNullOrEmpty(evolutionId)) WeaponLab.Evolve(game, weaponId, evolutionId);
            return $"{weaponId} slot {slot}, +{ranks}{(evolutionId != null ? ", evolved " + evolutionId : "")}";
        }

        /// <summary>Keeps only the given weapons (comma separated).</summary>
        public static string Loadout(string weaponIds)
        {
            var game = Game;
            if (game == null) return "no game";
            WeaponLab.SetLoadout(game, weaponIds.Split(','));
            return string.Join(",", WeaponLab.Loadout(game));
        }

        public static string Evolve(string weaponId, string evolutionId)
        {
            var game = Game;
            if (game == null) return "no game";
            WeaponLab.Evolve(game, weaponId, evolutionId);
            return WeaponLab.IsEvolved(game, weaponId) ? "evolved" : "not evolved";
        }

        public static string Levels(int count)
        {
            var levels = Game?.Build?.Progression;
            if (levels == null) return "no progression";
            for (var i = 0; i < count; i++) levels.AddXp(Mathf.Max(1, levels.XpToNext - Mathf.FloorToInt(levels.CurrentXp)));
            return $"added {count} levels";
        }

        public static string SummonBoss()
        {
            var game = Game;
            if (game == null || game.Context == null || !game.Context.TryGet<IStageContent>(out var stage)) return "no stage";
            stage.RevealBeacon();
            return stage.TrySummonBoss() ? "boss summoned" : "summon refused";
        }

        /// <summary>Jumps the run clock forward (hordes and peaks are scheduled on it).</summary>
        public static string JumpTo(float seconds)
        {
            var run = RunManager.Instance;
            if (run == null) return "no run";
            if (seconds > run.ElapsedSeconds) run.JumpClock(seconds);
            return $"t={run.ElapsedSeconds:0.0}s";
        }

        /// <summary>Reloads the run scene on a fixed seed, so a take can be repeated.</summary>
        public static string Restart(int seed)
        {
            SlowMo(1f);
            V4Game.RestartRunWithSeed(seed);
            return $"restarting on seed {seed}";
        }

        /// <summary>Turns god mode off and sinks the ship.</summary>
        public static string Sink()
        {
            var ship = Ship;
            if (ship == null || ship.Damage == null || !ship.Damage.IsAlive) return "no live ship";
            ship.Damage.SetPlaytestInvulnerable(false);
            ship.Damage.TakeDamage(1_000_000f, ship.Position, false);
            return "sunk";
        }

        /// <summary>The scheduled hordes as "label@seconds" (to jump just before one).</summary>
        public static string Hordes()
        {
            var timeline = Game?.Config?.Single<RoguePirates.V4.Config.WaveTimelineConfig>();
            if (timeline == null || timeline.flow == null) return "no flow";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var h in timeline.flow.hordes) parts.Add($"{h.label}({h.formation},{h.count})@{h.timeSeconds:0}");
            return string.Join("; ", parts);
        }

        /// <summary>Root objects whose name contains the text (to find the boss or a landmark for the camera).</summary>
        public static string Find(string text)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name.Contains(text)) names.Add($"{t.name}@{t.position.x:0},{t.position.z:0}");
            return string.Join("; ", names);
        }

        public static string Status()
        {
            var run = RunManager.Instance;
            var game = Game;
            var loadout = game != null ? string.Join(",", WeaponLab.Loadout(game)) : "-";
            return run == null ? "no run" : $"t={run.ElapsedSeconds:0.0}s state={run.State} kills={run.KillCount} enemies={Enemies()} weapons={loadout} timeScale={Time.timeScale}";
        }
    }
}

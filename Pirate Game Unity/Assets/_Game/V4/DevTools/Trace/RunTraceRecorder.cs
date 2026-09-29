using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.POI;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.Progression;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Records one scripted run as JSON Lines: spawns, kills, level-ups, offers taken, chests, boss, 5 s ship samples,
    /// per-minute summaries, death and the Marks breakdown. Reads legacy state only and never changes the simulation,
    /// except for the scripted inputs a player would give (card pick, chest skip, boss summon at a fixed time).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RunTraceRecorder : MonoBehaviour
    {
        struct AgentInfo { public string Enemy; public bool Elite; public bool Boss; public Vector3 Pos; }
        struct KillEvent { public Vector3 Pos; public bool Elite; public bool Boss; public string Weapon; public bool Used; }

        TraceRequest _request;
        readonly List<string> _lines = new List<string>(4096);
        readonly Dictionary<int, AgentInfo> _alive = new Dictionary<int, AgentInfo>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _removed = new List<int>();
        readonly List<KillEvent> _kills = new List<KillEvent>();
        readonly SortedDictionary<string, int> _minuteSpawns = new SortedDictionary<string, int>();

        RoguePirates.V4.Enemies.IEnemyWorldView _enemies;
        RoguePirates.V4.Enemies.IEnemyDevControl _enemyDev;
        RoguePirates.V4.Stage.IStageContent _stage;
        PlayerShipController _ship;
        bool _started, _ended, _finished;
        int _startFrame, _endFrame;
        int _lastLevel = -1, _minute, _minuteKills, _totalSpawns, _totalKills;
        float _nextSample;
        bool _bossSummoned, _bossDefeated;
        int _modalOpenFrames, _chestPauseFrames;

        public void Begin(TraceRequest request)
        {
            _request = request;
        }

        float SimTime => RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
        int Frame => Time.frameCount - _startFrame;

        /// <summary>
        /// Legacy pickups align barrels to skinned-renderer bounds (R-74), and skinned bounds / animation only update
        /// while some camera draws them, so a run could depend on whether the Editor's Game or Scene view was visible.
        /// Trace runs pin that state: skinned meshes always update bounds and animators always animate.
        /// </summary>
        static void PinVisualState()
        {
            foreach (var skinned in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                if (!skinned.updateWhenOffscreen) skinned.updateWhenOffscreen = true;
            foreach (var animator in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void OnDestroy()
        {
            if (_v4Events != null) _v4Events.TickPublished -= OnV4Events;
            if (_started && !_finished) Finish("aborted");
        }

        void LateUpdate()
        {
            if (_request == null || _finished) return;
            PinVisualState();
            var run = RunManager.Instance;
            if (run == null) return;

            if (!_started)
            {
                if (run.State != RunState.Playing) return;
                StartRecording();
            }

            if (_ended)
            {
                if (Time.frameCount - _endFrame >= 3) WriteSettlementAndFinish("death");
                return;
            }

            // Playtest round 1: the human pilot plays for itself (cards, chests, the Beacon, the boss, minibosses).
            var human = _request.pilot == HumanPilot.PatternId;
            if (!human) RespondToPauses(run);
            var t = SimTime;
            ScanAgents(t);
            PollProgress(t, run);

            if (!human && !_bossSummoned && t >= _request.bossAtSeconds && !run.IsRunEnded && !run.IsPaused)
            {
                if (_stage != null) _stage.TrySummonBoss();
            }
            if (!human && _bossSummoned && !_bossDefeated && t >= _request.killBossAtSeconds && !run.IsRunEnded && !run.IsPaused)
                TryKillBoss();
            if (!human) TrackArena(t, run);

            if (t >= _nextSample)
            {
                _nextSample += 5f;
                var hull = _ship != null && _ship.Damage != null ? _ship.Damage.CurrentHP : 0f;
                var pos = _ship != null ? _ship.Position : Vector3.zero;
                Add(new JsonLine("sample").Num("t", t).Num("x", pos.x).Num("z", pos.z).Num("hull", hull)
                    .Int("alive", _alive.Count).Int("gold", run.Gold).Int("kills", run.KillCount));
            }

            var minuteNow = Mathf.FloorToInt(t / 60f);
            if (minuteNow > _minute) FlushMinute(run, minuteNow);

            if (run.IsRunEnded)
            {
                _ended = true;
                _endFrame = Time.frameCount;
                Add(new JsonLine("death").Num("t", t).Str("state", run.State.ToString()).Bool("objective", run.ObjectiveSecured));
                return;
            }

            if (t >= _request.maxSimSeconds) Finish("timeout");
        }

        void StartRecording()
        {
            _started = true;
            _startFrame = Time.frameCount;
            var game = FindFirstObjectByType<RoguePirates.V4.V4Game>();
            if (game != null && game.Context != null)
            {
                game.Context.TryGet(out _stage);
                game.Context.TryGet(out _enemies);
                game.Context.TryGet(out _enemyDev);
                game.Context.TryGet(out _cards); // M14.11: the level-up presenter's pick API
            }
            _ship = FindFirstObjectByType<PlayerShipController>();
            // M14: V4 runs publish kills and chests as events.
            var v4Game = FindFirstObjectByType<RoguePirates.V4.V4Game>();
            if (v4Game != null && v4Game.Events != null)
            {
                _v4Events = v4Game.Events;
                _v4Events.TickPublished += OnV4Events;
            }
            else Debug.LogWarning("V4 trace: no V4 run; kills will be recorded as despawns.");

            if (_request.invulnerable && _ship != null) _ship.Damage.SetPlaytestInvulnerable(true); // long runs (overtime, storm)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Add(new JsonLine("header").Int("schema", 1).Str("label", _request.label).Str("scene", scene)
                .Int("seed", RunManager.Instance.RunSeed).Str("pilot", _request.pilot).Num("dt", _request.captureDeltaTime)
                .Str("captain", CaptainSelection.Selected.ToString()).Str("unity", Application.unityVersion)
                .Str("platform", Application.isEditor ? "editor" : Application.platform.ToString()).Str("screen", $"{Screen.width}x{Screen.height}")
                .Num("bossAt", _request.bossAtSeconds).Num("killBossAt", _request.killBossAtSeconds).Num("killArenaAfter", _request.killArenaFocusAfterSeconds).Str("commit", _request.commit).Str("v4SetHash", FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Config?.Hash ?? string.Empty).Str("director", FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Config?.Single<RoguePirates.V4.Config.DirectorConfig>()?.version ?? string.Empty).Str("directorMode", FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Config?.Single<RoguePirates.V4.Config.DirectorConfig>()?.mode.ToString() ?? string.Empty).Str("preset", string.IsNullOrEmpty(_request.presetPath) ? string.Empty : Path.GetFileNameWithoutExtension(_request.presetPath)).Str("visualPins", "skinned-offscreen+always-animate").Str("configHash", StageBalanceProfileContext.Export?.ContentHash).Bool("invulnerable", _request.invulnerable));
        }

        void OnKill(bool elite, bool boss, Vector3 pos, string weapon) =>
            _kills.Add(new KillEvent { Pos = pos, Elite = elite, Boss = boss, Weapon = weapon });

        RoguePirates.V4.Events.ICombatEventReader _v4Events;

        /// <summary>V4's Killed (flags: 1 elite, 2 boss, 4 ordinary rewards; V3's TriggerBus elite flag was "elite rewards") and ChestOpened.</summary>
        void OnV4Events(System.Collections.Generic.IReadOnlyList<RoguePirates.V4.Events.CombatEvent> batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (e.Kind == RoguePirates.V4.Events.CombatEventKind.Killed)
                    OnKill((e.B & 1) != 0 && (e.B & 4) == 0, (e.B & 2) != 0, new Vector3(e.X, 0f, e.Y), e.Tag);
                else if (e.Kind == RoguePirates.V4.Events.CombatEventKind.ChestOpened)
                    OnChest();
            }
        }

        void OnChest() => Add(new JsonLine("chest").Num("t", SimTime).Int("gold", RunManager.Instance != null ? RunManager.Instance.Gold : 0));

        void ScanAgents(float t)
        {
            if (_enemies == null || !_enemies.Exists) { _kills.Clear(); return; }
            _seen.Clear();
            var capacity = _enemies.Capacity;
            for (var i = 0; i < capacity; i++)
            {
                if (!_enemies.TryGet(i, out var a)) continue;
                _seen.Add(a.unitId);
                if (!_alive.TryGetValue(a.unitId, out var info))
                {
                    info = new AgentInfo { Enemy = a.enemyId, Elite = a.isElite, Boss = a.isBoss };
                    _totalSpawns++;
                    _minuteSpawns.TryGetValue(info.Enemy, out var c);
                    _minuteSpawns[info.Enemy] = c + 1;
                    Add(new JsonLine("spawn").Num("t", t).Int("f", Frame).Int("uid", a.unitId).Str("enemy", info.Enemy)
                        .Bool("elite", info.Elite).Bool("boss", info.Boss).Num("x", a.position.x).Num("z", a.position.z));
                }
                info.Pos = a.position;
                _alive[a.unitId] = info;
            }

            _removed.Clear();
            foreach (var id in _alive.Keys)
                if (!_seen.Contains(id)) _removed.Add(id);
            _removed.Sort();

            foreach (var id in _removed)
            {
                var info = _alive[id];
                _alive.Remove(id);
                var match = -1;
                var best = 16f;
                for (var k = 0; k < _kills.Count; k++)
                {
                    if (_kills[k].Used) continue;
                    var d = (_kills[k].Pos - info.Pos).sqrMagnitude;
                    if (d < best) { best = d; match = k; }
                }

                if (match >= 0)
                {
                    var kill = _kills[match];
                    kill.Used = true;
                    _kills[match] = kill;
                    _totalKills++;
                    _minuteKills++;
                    Add(new JsonLine("kill").Num("t", t).Int("f", Frame).Int("uid", id).Str("enemy", info.Enemy)
                        .Bool("elite", kill.Elite).Bool("boss", kill.Boss).Str("weapon", kill.Weapon));
                }
                else
                {
                    Add(new JsonLine("despawn").Num("t", t).Int("f", Frame).Int("uid", id).Str("enemy", info.Enemy));
                }
            }

            foreach (var kill in _kills)
            {
                if (kill.Used) continue;
                _totalKills++;
                _minuteKills++;
                Add(new JsonLine("kill").Num("t", t).Int("f", Frame).Int("uid", -1).Str("enemy", "?")
                    .Bool("elite", kill.Elite).Bool("boss", kill.Boss).Str("weapon", kill.Weapon));
            }
            _kills.Clear();
        }

        RoguePirates.V4.V4Game _levelGame;
        RoguePirates.V4.Progression.IOfferChoice _cards;

        /// <summary>V4 runs: the first card after the configured number of frames, through the presenter's pick.</summary>
        void RespondToCards()
        {
            var offers = _cards.IsShowing ? _cards.Shown : null;
            if (offers == null || offers.Count == 0) { _modalOpenFrames = 0; return; }
            if (++_modalOpenFrames < _request.pauseResponseFrames) return;
            _modalOpenFrames = 0;
            var chosen = offers[0];
            Add(new JsonLine("offer").Num("t", SimTime).Int("f", Frame).Str("kind", chosen.Kind.ToString()).Str("id", chosen.Id)
                .Int("choices", offers.Count));
            _cards.Pick(0);
        }

        /// <summary>The player level from the V4 progression module (M9); -1 if none.</summary>
        int CurrentLevel()
        {
            if (_levelGame == null) _levelGame = FindFirstObjectByType<RoguePirates.V4.V4Game>();
            if (_levelGame != null && _levelGame.Build != null) return _levelGame.Build.Progression.Level;
            return -1;
        }

        void PollProgress(float t, RunManager run)
        {
            var level = CurrentLevel();
            if (level >= 0 && level != _lastLevel)
            {
                if (_lastLevel >= 0) Add(new JsonLine("level").Num("t", t).Int("f", Frame).Int("level", level));
                _lastLevel = level;
            }

            if (_stage != null)
            {
                if (!_bossSummoned && _stage.BossSummoned) { _bossSummoned = true; Add(new JsonLine("boss").Num("t", t).Str("state", "summoned")); }
                if (!_bossDefeated && _stage.BossDefeated) { _bossDefeated = true; Add(new JsonLine("boss").Num("t", t).Str("state", "defeated")); }
            }
        }

        RoguePirates.V4.Spawning.ISpawnController _v4Spawn;
        bool _v4SpawnLooked;

        RoguePirates.V4.Spawning.ISpawnController V4Spawn
        {
            get
            {
                if (_v4SpawnLooked) return _v4Spawn;
                var game = FindFirstObjectByType<RoguePirates.V4.V4Game>();
                if (game == null || game.Context == null) return null;
                _v4SpawnLooked = true;
                game.Context.TryGet(out _v4Spawn);
                return _v4Spawn;
            }
        }

        int _arenaFocus = -1;
        float _arenaStart;

        /// <summary>Logs miniboss arenas and kills the focus enemy after the configured delay (weapon-hit path).</summary>
        void TrackArena(float t, RunManager run)
        {
            // V4 (M3+): the spawn controller owns arenas.
            var spawn = V4Spawn;
            var active = spawn != null && spawn.ArenaActive;
            if (!active) { _arenaFocus = -1; return; }
            var focus = spawn.ArenaFocusUnitId;
            if (focus != _arenaFocus)
            {
                _arenaFocus = focus;
                _arenaStart = t;
                Add(new JsonLine("arena").Num("t", t).Int("f", Frame).Int("focus", focus).Str("state", "begin"));
            }
            if (_request.killArenaFocusAfterSeconds < 0f || t < _arenaStart + _request.killArenaFocusAfterSeconds || run.IsRunEnded || run.IsPaused) return;
            if (_enemies == null || !_enemies.TryFindByUnitId(focus, out var index)) return;
            _enemyDev.Kill(index, "deck_cannons");
            Add(new JsonLine("arena").Num("t", t).Int("f", Frame).Int("focus", focus).Str("state", "killed"));
        }

        /// <summary>Lethal hit on the active boss through the weapon-hit path (same as SaltwindScenePlayModeTests).</summary>
        void TryKillBoss()
        {
            if (_enemies == null || !_enemies.Exists) return;
            var capacity = _enemies.Capacity;
            for (var i = 0; i < capacity; i++)
            {
                if (!_enemies.TryGet(i, out var agent) || !agent.isBoss) continue;
                _enemyDev.Kill(i, "deck_cannons");
                return;
            }
        }

        void RespondToPauses(RunManager run)
        {
            if (_cards != null) RespondToCards();

            if (run.PauseReason == PauseReason.POIChoice)
            {
                if (++_chestPauseFrames >= _request.pauseResponseFrames)
                {
                    _chestPauseFrames = 0;
                    if (_stage != null)
                    {
                        var index = _stage.PromptTarget;
                        if (index >= 0)
                        {
                            _stage.SkipChest();
                            Add(new JsonLine("chestSkip").Num("t", SimTime).Int("f", Frame).Int("poi", index));
                        }
                    }
                }
            }
            else _chestPauseFrames = 0;
        }

        void FlushMinute(RunManager run, int minuteNow)
        {
            while (_minute < minuteNow)
            {
                var hull = _ship != null && _ship.Damage != null ? _ship.Damage.CurrentHP : 0f;
                Add(new JsonLine("minute").Int("m", _minute).Counts("spawns", _minuteSpawns).Int("kills", _minuteKills)
                    .Int("gold", run.Gold).Int("level", System.Math.Max(0, CurrentLevel())).Num("hull", hull));
                if (_request.pilot == HumanPilot.PatternId) { AddHurtLines(); AddPowerLine(run); } // playtest round 1: balance measurement
                _minuteSpawns.Clear();
                _minuteKills = 0;
                _minute++;
            }
        }

        /// <summary>Human-pilot runs: every hit of 2+ damage with what was near the ship (why the pilot died).</summary>
        void AddHurtLines()
        {
            var human = FindFirstObjectByType<HumanPlayer>();
            if (human == null) return;
            foreach (var h in human.Hurts) Add(new JsonLine("hurt").Str("h", h));
            human.Hurts.Clear();
        }

        /// <summary>Human-pilot runs: Pressure, the power index and the build each minute (balance measurement).</summary>
        void AddPowerLine(RunManager run)
        {
            var game = FindFirstObjectByType<RoguePirates.V4.V4Game>();
            if (game == null || game.Context == null || !game.Context.TryGet<RoguePirates.V4.Difficulty.IPressureView>(out var p)) return;
            var weapons = game.Build != null ? game.Build.Progression.OwnedWeapons.Count : 0;
            var human = FindFirstObjectByType<HumanPlayer>();
            Add(new JsonLine("power").Int("m", _minute).Num("t", run.ElapsedSeconds).Num("pressure", p.Pressure).Num("offense", p.Offense)
                .Num("offenseNorm", p.OffenseNorm).Num("defense", p.DefenseNorm).Num("performance", p.Performance).Int("weapons", weapons)
                .Int("alive", _alive.Count).Str("goal", human != null ? human.Pilot.Goal : "").Str("profile", human != null ? human.Profile.ToString() : ""));
        }

        void WriteSettlementAndFinish(string reason)
        {
            var run = RunManager.Instance;
            if (run != null) FlushMinute(run, _minute + 1);
            // M12: the V4 Rum settlement, in V3's "marks" line shape (the V3 goldens compare it field for field).
            RoguePirates.V4.Rewards.IRumView rum = null;
            FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Context?.TryGet(out rum);
            if (rum != null)
                Add(new JsonLine("marks").Bool("applied", rum.SettlementApplied).Int("awarded", rum.SettledTotal)
                    .Int("minutes", rum.SettledRumOf(RoguePirates.V4.Config.RumSource.QualifyingMinute))
                    .Int("poi", rum.SettledRumOf(RoguePirates.V4.Config.RumSource.SpecialLandmark))
                    .Int("elites", rum.SettledRumOf(RoguePirates.V4.Config.RumSource.ScheduledElite))
                    .Int("boss", rum.SettledRumOf(RoguePirates.V4.Config.RumSource.BossVictory))
                    .Int("overtime", rum.SettledRumOf(RoguePirates.V4.Config.RumSource.OvertimeMinute))
                    .Int("total", rum.SettledTotal));
            Finish(reason);
        }

        void Finish(string reason)
        {
            if (_finished) return;
            _finished = true;
            Add(new JsonLine("end").Str("reason", reason).Num("t", SimTime).Int("frames", Frame)
                .Int("spawns", _totalSpawns).Int("kills", _totalKills));

            var path = string.IsNullOrEmpty(_request.outPath)
                ? Path.Combine(Application.persistentDataPath, $"v4-trace-{_request.label}.jsonl")
                : _request.outPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            // Write then rename, so a watcher never reads a half-written trace.
            var partial = path + ".partial";
            File.WriteAllLines(partial, _lines);
            if (File.Exists(path)) File.Delete(path);
            File.Move(partial, path);
            Debug.Log($"V4 trace written: {path} ({_lines.Count} lines, {reason})");

#if UNITY_EDITOR
            if (_request.stopPlayModeWhenDone) UnityEditor.EditorApplication.isPlaying = false;
#else
            if (_request.stopPlayModeWhenDone) Application.Quit();
#endif
        }

        void Add(JsonLine line) => _lines.Add(line.ToString());
    }
}

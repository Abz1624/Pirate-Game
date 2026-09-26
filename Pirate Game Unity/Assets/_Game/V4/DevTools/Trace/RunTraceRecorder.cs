using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
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

        EnemyWorld _world;
        POIManager _poi;
        RoguePirates.V4.Stage.IStageContent _stage; // M7: V4 stage content (the V3 scene keeps POIManager)
        PlayerShipController _ship;
        TriggerBus _bus;
        bool _started, _ended, _finished;
        int _startFrame, _endFrame;
        int _lastLevel = -1, _minute, _minuteKills, _totalSpawns, _totalKills;
        float _nextSample;
        bool _bossSummoned, _bossDefeated;
        int _modalOpenFrames, _chestPauseFrames;

        static readonly FieldInfo OffersField = typeof(LevelUpModal).GetField("_offers", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PanelField = typeof(LevelUpModal).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        static readonly MethodInfo PickMethod = typeof(LevelUpModal).GetMethod("Pick", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo TargetField = typeof(V3InteractionController).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo DismissedField = typeof(V3InteractionController).GetField("_salvageChestDismissedIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo EndChestPauseMethod = typeof(V3InteractionController).GetMethod("EndSalvageChestChoicePause", BindingFlags.Instance | BindingFlags.NonPublic);

        public void Begin(TraceRequest request)
        {
            _request = request;
            if (OffersField == null || PanelField == null || PickMethod == null || TargetField == null || DismissedField == null || EndChestPauseMethod == null)
                Debug.LogError("V4 trace: a pause-responder member was not found; replays may stall at modals.");
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
            if (_bus != null) { _bus.Kill -= OnKill; _bus.ChestOpened -= OnChest; }
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

            RespondToPauses(run);
            var t = SimTime;
            ScanAgents(t);
            PollProgress(t, run);

            if (!_bossSummoned && t >= _request.bossAtSeconds && !run.IsRunEnded && !run.IsPaused)
            {
                if (_stage != null) _stage.TrySummonBoss();
                else if (_poi != null) _poi.TrySummonBoss();
            }
            if (_bossSummoned && !_bossDefeated && t >= _request.killBossAtSeconds && !run.IsRunEnded && !run.IsPaused)
                TryKillBoss();
            TrackArena(t, run);

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
            _world = FindFirstObjectByType<EnemyWorld>();
            _poi = FindFirstObjectByType<POIManager>();
            var game = FindFirstObjectByType<RoguePirates.V4.V4Game>();
            if (game != null && game.Context != null) game.Context.TryGet(out _stage);
            _ship = FindFirstObjectByType<PlayerShipController>();
            _bus = TriggerBus.Instance;
            if (_bus != null) { _bus.Kill += OnKill; _bus.ChestOpened += OnChest; }
            else Debug.LogWarning("V4 trace: TriggerBus.Instance is null; kills will be recorded as despawns.");

            if (_request.invulnerable && _ship != null) _ship.Damage.SetPlaytestInvulnerable(true); // long runs (overtime, storm)
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Add(new JsonLine("header").Int("schema", 1).Str("label", _request.label).Str("scene", scene)
                .Int("seed", RunManager.Instance.RunSeed).Str("pilot", _request.pilot).Num("dt", _request.captureDeltaTime)
                .Str("captain", V3CaptainSelection.Selected.ToString()).Str("unity", Application.unityVersion)
                .Str("platform", Application.isEditor ? "editor" : Application.platform.ToString()).Str("screen", $"{Screen.width}x{Screen.height}")
                .Num("bossAt", _request.bossAtSeconds).Num("killBossAt", _request.killBossAtSeconds).Num("killArenaAfter", _request.killArenaFocusAfterSeconds).Str("commit", _request.commit).Str("v4SetHash", FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Config?.Hash ?? string.Empty).Str("preset", string.IsNullOrEmpty(_request.presetPath) ? string.Empty : Path.GetFileNameWithoutExtension(_request.presetPath)).Str("visualPins", "skinned-offscreen+always-animate").Str("configHash", StageBalanceProfileContext.Export?.ContentHash).Bool("invulnerable", _request.invulnerable));
        }

        void OnKill(bool elite, bool boss, Vector3 pos, string weapon) =>
            _kills.Add(new KillEvent { Pos = pos, Elite = elite, Boss = boss, Weapon = weapon });

        void OnChest() => Add(new JsonLine("chest").Num("t", SimTime).Int("gold", RunManager.Instance != null ? RunManager.Instance.Gold : 0));

        void ScanAgents(float t)
        {
            if (_world == null || _world.Agents == null) { _kills.Clear(); return; }
            var agents = _world.Agents;
            var defs = _world.Definitions;
            _seen.Clear();
            for (var i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                if (!a.active) continue;
                _seen.Add(a.id);
                if (!_alive.TryGetValue(a.id, out var info))
                {
                    var def = defs != null && a.definitionIndex >= 0 && a.definitionIndex < defs.Length ? defs[a.definitionIndex] : null;
                    info = new AgentInfo { Enemy = def != null ? def.id : "?", Elite = a.isElite, Boss = a.isBoss };
                    _totalSpawns++;
                    _minuteSpawns.TryGetValue(info.Enemy, out var c);
                    _minuteSpawns[info.Enemy] = c + 1;
                    Add(new JsonLine("spawn").Num("t", t).Int("f", Frame).Int("uid", a.id).Str("enemy", info.Enemy)
                        .Bool("elite", info.Elite).Bool("boss", info.Boss).Num("x", a.position.x).Num("z", a.position.z));
                }
                info.Pos = a.position;
                _alive[a.id] = info;
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

        void PollProgress(float t, RunManager run)
        {
            var levels = LevelSystem.Instance;
            if (levels != null && levels.Level != _lastLevel)
            {
                if (_lastLevel >= 0) Add(new JsonLine("level").Num("t", t).Int("f", Frame).Int("level", levels.Level));
                _lastLevel = levels.Level;
            }

            if (_stage != null)
            {
                if (!_bossSummoned && _stage.BossSummoned) { _bossSummoned = true; Add(new JsonLine("boss").Num("t", t).Str("state", "summoned")); }
                if (!_bossDefeated && _stage.BossDefeated) { _bossDefeated = true; Add(new JsonLine("boss").Num("t", t).Str("state", "defeated")); }
            }
            else if (_poi != null)
            {
                if (!_bossSummoned && _poi.BossSummoned) { _bossSummoned = true; Add(new JsonLine("boss").Num("t", t).Str("state", "summoned")); }
                if (!_bossDefeated && _poi.BossDefeated) { _bossDefeated = true; Add(new JsonLine("boss").Num("t", t).Str("state", "defeated")); }
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
            // V4 (M3+): the spawn controller owns arenas. V3 scenes (goldens): the static V3 arena.
            var spawn = V4Spawn;
            var active = spawn != null ? spawn.ArenaActive : HighSeas.V3.V3MinibossArena.Active;
            if (!active) { _arenaFocus = -1; return; }
            var focus = spawn != null ? spawn.ArenaFocusUnitId : HighSeas.V3.V3MinibossArena.FocusAgentId;
            if (focus != _arenaFocus)
            {
                _arenaFocus = focus;
                _arenaStart = t;
                Add(new JsonLine("arena").Num("t", t).Int("f", Frame).Int("focus", focus).Str("state", "begin"));
            }
            if (_request.killArenaFocusAfterSeconds < 0f || t < _arenaStart + _request.killArenaFocusAfterSeconds || run.IsRunEnded || run.IsPaused) return;
            if (_world == null || !_world.TryGetAgentByUnitId(focus, out var index)) return;
            var agent = _world.Agents[index];
            _world.ApplyDamage(index, Mathf.Max(agent.hp, agent.maxHp) + 500f, false, weaponId: "deck_cannons");
            Add(new JsonLine("arena").Num("t", t).Int("f", Frame).Int("focus", focus).Str("state", "killed"));
        }

        /// <summary>Lethal hit on the active boss through the weapon-hit path (same as SaltwindScenePlayModeTests).</summary>
        void TryKillBoss()
        {
            if (_world == null || _world.Agents == null) return;
            for (var i = 0; i < _world.Agents.Length; i++)
            {
                var agent = _world.Agents[i];
                if (!agent.active || !agent.isBoss) continue;
                _world.ApplyDamage(i, Mathf.Max(agent.hp, agent.maxHp) + 500f, false, weaponId: "deck_cannons");
                return;
            }
        }

        void RespondToPauses(RunManager run)
        {
            // LevelUpModal.Pick hides the panel but leaves _offers set, so "open" means panel visible AND offers set
            // (the same test LevelUpModal.Update uses before reading the 1/2/3 keys).
            var modal = LevelUpModal.Instance;
            var panel = modal != null && PanelField != null ? PanelField.GetValue(modal) as GameObject : null;
            var open = panel != null && panel.activeSelf;
            var offers = open && OffersField != null ? OffersField.GetValue(modal) as List<LevelOffer> : null;
            if (offers != null && offers.Count > 0)
            {
                if (++_modalOpenFrames >= _request.pauseResponseFrames)
                {
                    _modalOpenFrames = 0;
                    var chosen = offers[0];
                    Add(new JsonLine("offer").Num("t", SimTime).Int("f", Frame).Str("kind", chosen.kind.ToString()).Str("id", chosen.id)
                        .Int("choices", offers.Count));
                    PickMethod?.Invoke(modal, new object[] { 0 });
                }
            }
            else _modalOpenFrames = 0;

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
                    else
                    {
                        var interaction = V3InteractionController.Instance;
                        var target = interaction != null ? TargetField?.GetValue(interaction) as POIInteractable : null;
                        if (target != null)
                        {
                            DismissedField.SetValue(interaction, target.Index);
                            EndChestPauseMethod.Invoke(interaction, null);
                            Add(new JsonLine("chestSkip").Num("t", SimTime).Int("f", Frame).Int("poi", target.Index));
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
                    .Int("gold", run.Gold).Int("level", LevelSystem.Instance != null ? LevelSystem.Instance.Level : 0).Num("hull", hull));
                _minuteSpawns.Clear();
                _minuteKills = 0;
                _minute++;
            }
        }

        void WriteSettlementAndFinish(string reason)
        {
            var run = RunManager.Instance;
            if (run != null) FlushMinute(run, _minute + 1);
            var b = SaltwindRunSettlement.LastBreakdown;
            Add(new JsonLine("marks").Bool("applied", SaltwindRunSettlement.LastSettlementApplied)
                .Int("awarded", SaltwindRunSettlement.LastMarksAwarded).Int("minutes", b.QualifyingOrdinaryMinutes)
                .Int("poi", b.SpecialPoiCredits).Int("elites", b.ScheduledEliteCredits).Int("boss", b.BossVictoryCredits)
                .Int("overtime", b.OvertimeMinuteCredits).Int("total", b.TotalMarks));
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

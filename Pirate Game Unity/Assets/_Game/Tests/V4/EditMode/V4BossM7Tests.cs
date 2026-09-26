using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Presentation.Stage;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M7 boss director: the V4 port against V3's BarnacleKingBossAttackLogic plus a line-for-line copy of V3
    /// BarnacleKingBossAttackDirector's tick and firing rules (the MonoBehaviour itself needs RunManager and the legacy
    /// worlds), over scripted health and ship paths with fixed and varying dt; then the rules one at a time, the config
    /// defaults against V3's constants, live edits, the gates, no random draws and the marked-circle presenter.
    /// </summary>
    [Category("V4")]
    public sealed class V4BossM7Tests
    {
        // ---- Log format shared by the V4 fakes and the V3 reference ------------------------------------------------

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => $"({F(v.x)},{F(v.y)},{F(v.z)})";

        static string StateLine(int phase, int attack, float timer, Vector3 dir, Vector3 marked, bool enraged, int queued,
            int script, int rotation, float pending, bool secondCharge, bool telegraph, Vector3 center, float radius) =>
            $"state p={phase} a={attack} t={F(timer)} dir={V(dir)} mark={V(marked)} enr={enraged} q={queued} sp={script} rot={rotation} " +
            $"pend={F(pending)} c2={secondCharge} tele={(telegraph ? $"{V(center)} r={F(radius)}" : "off")}";

        // ---- Fakes ---------------------------------------------------------------------------------------------

        sealed class FakeBossWorld : IBossControl
        {
            readonly List<string> _log;
            public FakeBossWorld(List<string> log) { _log = log; }
            public bool Present = true;
            public bool HasBoss;
            public BossSnapshot Boss;
            public bool Exists => Present;
            public bool TryFindActiveBoss(out BossSnapshot boss)
            {
                boss = HasBoss ? Boss : default;
                if (!HasBoss) boss.Slot = -1;
                return HasBoss;
            }
            public void SetAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced) =>
                _log.Add($"ctrl set {slot} {pauseMovement} {V(forcedVelocity)} {useForced}");
            public void ClearAttackControl() => _log.Add("ctrl clear");
        }

        sealed class FakeProjectiles : IHostileProjectileSink
        {
            readonly List<string> _log;
            public FakeProjectiles(List<string> log) { _log = log; }
            public bool Exists => true;
            public bool TrySpawn(Vector3 position, Vector3 direction, float speed, float damage, float life, float hitRadius, float visualRadius)
            {
                _log.Add($"shot {V(position)} {V(direction)} {F(speed)} {F(damage)} {F(life)} {F(hitRadius)} {F(visualRadius)}");
                return true;
            }
        }

        sealed class FakePlayer : IPlayerDamageSink
        {
            readonly List<string> _log;
            public FakePlayer(List<string> log) { _log = log; }
            public Vector3 Pos;
            public bool Alive = true;
            public bool Exists => true;
            public bool IsAlive => Alive;
            public Vector3 Position => Pos;
            public void Damage(float amount, Vector3 source) => _log.Add($"dmg {F(amount)} {V(source)}");
            public void DamageOverTime(float amount, Vector3 source) => _log.Add($"dot {F(amount)} {V(source)}");
        }

        sealed class FakeEffects : IBossEffects
        {
            readonly List<string> _log;
            public FakeEffects(List<string> log) { _log = log; }
            public void Explosion(Vector3 position, float radius) => _log.Add($"boom {V(position)} {F(radius)}");
            public void Toast(string text, float seconds) => _log.Add($"toast {text} {F(seconds)}");
        }

        sealed class FakeRun : IRunStatus
        {
            public bool Paused, Ended, Gameplay = true;
            public bool Exists => true;
            public float ElapsedSeconds => 0f;
            public bool IsPaused => Paused;
            public bool IsRunEnded => Ended;
            public bool IsGameplayActive => Gameplay;
            public bool IsBossFight => Gameplay;
            public int PlayerLevel => 1;
            public int Kills => 0;
        }

        // ---- V3 reference: BarnacleKingBossAttackDirector.Tick after its RunManager/world gates, verbatim ----------

        sealed class V3Reference
        {
            static readonly float ChargeSpeedMult = V4BossConfigGenerator.DirectorConstant("ChargeSpeedMult", float.NaN);
            static readonly float SpreadProjectileSpeed = V4BossConfigGenerator.DirectorConstant("SpreadProjectileSpeed", float.NaN);

            readonly List<string> _log;
            BarnacleKingAttackState _state;
            int _bossSlot = -1;
            bool _active;
            bool _executeFired;
            float _toastCooldown;
            bool _telegraph;
            Vector3 _telegraphCenter;
            float _telegraphRadius;

            public V3Reference(List<string> log) { _log = log; }

            public void ResetForRun()
            {
                _active = false;
                _bossSlot = -1;
                _executeFired = false;
                _telegraph = false;
                BarnacleKingBossAttackLogic.BeginRun(ref _state);
                _log.Add("ctrl clear");
            }

            public void Tick(float dt, Frame f)
            {
                if (f.Secured)
                {
                    if (_active) ResetForRun();
                    return;
                }
                if (!f.HasBoss)
                {
                    _bossSlot = -1;
                    if (_active) ResetForRun();
                    return;
                }
                var boss = f.Boss;
                _bossSlot = boss.Slot;
                if (!_active)
                {
                    _active = true;
                    BarnacleKingBossAttackLogic.BeginRun(ref _state);
                }

                var hpRatio = boss.MaxHp > 0f ? boss.Hp / boss.MaxHp : 1f;
                BarnacleKingBossAttackLogic.SetEnraged(ref _state, hpRatio <= 0.3f);
                BarnacleKingBossAttackLogic.ApplyHealthPhase(ref _state, hpRatio);

                var playerPos = f.Player;
                var bossPos = boss.Position;
                var previousPhase = _state.Phase;
                BarnacleKingBossAttackLogic.Tick(ref _state, dt, bossPos, playerPos);

                if (_state.Phase == BarnacleKingBossPhase.Execute && previousPhase != BarnacleKingBossPhase.Execute)
                    _executeFired = false;

                UpdateMarkedTelegraph();
                ApplyMovementControl(boss);
                if (BarnacleKingBossAttackLogic.CanDealDamage(_state) && !_executeFired)
                {
                    FireAttack(boss, f);
                    _executeFired = true;
                }

                MaybeShowTelegraphToast(dt);
            }

            void ApplyMovementControl(BossSnapshot boss)
            {
                if (BarnacleKingBossAttackLogic.IsMovementPaused(_state))
                {
                    _log.Add($"ctrl set {_bossSlot} {true} {V(Vector3.zero)} {false}");
                    return;
                }
                if (BarnacleKingBossAttackLogic.IsChargeCommit(_state))
                {
                    var vel = _state.CommittedDirection * (boss.MoveSpeed * ChargeSpeedMult);
                    _log.Add($"ctrl set {_bossSlot} {false} {V(vel)} {true}");
                    return;
                }
                _log.Add("ctrl clear");
            }

            void FireAttack(BossSnapshot boss, Frame f)
            {
                switch (_state.Attack)
                {
                    case BarnacleKingAttackKind.AimedSpread: FireSpread(boss); break;
                    case BarnacleKingAttackKind.MarkedBombardment: ApplyMarkedBlast(boss, f); break;
                    case BarnacleKingAttackKind.ShellSlam: ApplyShellSlam(boss, f); break;
                }
            }

            void ApplyShellSlam(BossSnapshot boss, Frame f)
            {
                var center = _state.MarkedPoint;
                center.y = 0f;
                if (BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(center, f.Player, 5f))
                    _log.Add($"dmg {F(boss.Damage * 1.35f)} {V(center)}");
                _log.Add($"boom {V(center)} {F(5f)}");
            }

            void FireSpread(BossSnapshot boss)
            {
                var baseDir = _state.CommittedDirection;
                const int count = 5;
                const float spreadDeg = 12f;
                for (var i = 0; i < count; i++)
                {
                    var t = (i - (count - 1) * 0.5f) / (count - 1);
                    var dir = Quaternion.Euler(0f, t * spreadDeg, 0f) * baseDir;
                    var muzzle = boss.Position + dir * (boss.Radius + 1.4f) + Vector3.up * 0.6f;
                    _log.Add($"shot {V(muzzle)} {V(dir)} {F(SpreadProjectileSpeed)} {F(boss.Damage * 0.55f)} {F(2.4f)} {F(0.55f)} {F(0.45f)}");
                }
            }

            void ApplyMarkedBlast(BossSnapshot boss, Frame f)
            {
                if (!BarnacleKingBossAttackLogic.ShouldApplyMarkedAreaDamage(_state)) return;
                if (!f.Alive) return;
                if (BarnacleKingBossAttackLogic.IsPlayerInsideMarkedBlast(_state.MarkedPoint, f.Player))
                {
                    var damage = boss.Damage * 1.15f;
                    _log.Add($"dmg {F(damage)} {V(_state.MarkedPoint)}");
                }
                _telegraph = false;
            }

            void UpdateMarkedTelegraph()
            {
                if (_state.Phase != BarnacleKingBossPhase.Telegraph
                    || (_state.Attack != BarnacleKingAttackKind.MarkedBombardment && _state.Attack != BarnacleKingAttackKind.ShellSlam))
                {
                    _telegraph = false;
                    return;
                }
                _telegraphRadius = _state.Attack == BarnacleKingAttackKind.ShellSlam ? 5f : BarnacleKingBossAttackLogic.MarkedBombardmentRadius;
                _telegraphCenter = _state.MarkedPoint;
                _telegraph = true;
            }

            void MaybeShowTelegraphToast(float dt)
            {
                if (_state.Phase != BarnacleKingBossPhase.Telegraph) return;
                if (_state.Attack == BarnacleKingAttackKind.MarkedBombardment) return;
                _toastCooldown -= dt;
                if (_toastCooldown > 0f) return;
                _toastCooldown = 1.2f;
                var label = _state.Attack switch
                {
                    BarnacleKingAttackKind.AimedSpread => "Barnacle King — spread incoming",
                    BarnacleKingAttackKind.CommittedCharge => "Barnacle King — charge incoming",
                    BarnacleKingAttackKind.ShellSlam => "Barnacle King — shell slam",
                    _ => null
                };
                if (label != null) _log.Add($"toast {label} {F(1.1f)}");
            }

            public string State() => StateLine((int)_state.Phase, (int)_state.Attack, _state.PhaseTimer, _state.CommittedDirection,
                _state.MarkedPoint, _state.Enraged, (int)_state.QueuedFollowUp, _state.ScriptPhase, _state.RotationStep,
                _state.PendingHpPhase, _state.Phase3ChargePassPending, _telegraph, _telegraphCenter, _telegraphRadius);
        }

        // ---- Scripted frames -------------------------------------------------------------------------------------

        struct Frame
        {
            public float Dt;
            public bool HasBoss, Alive, Secured, Paused;
            public BossSnapshot Boss;
            public Vector3 Player;
        }

        static Frame MakeFrame(float dt, float t, float hp, float maxHp)
        {
            var bossPos = new Vector3(Mathf.Sin(t * 0.1f) * 3f, 0f, Mathf.Cos(t * 0.13f) * 2f);
            var r = 4f + 5f * (0.5f + 0.5f * Mathf.Sin(t * 0.37f));
            var a = t * 0.5f;
            return new Frame
            {
                Dt = dt, HasBoss = true, Alive = true,
                Boss = new BossSnapshot { Slot = 3, Position = bossPos, Hp = hp, MaxHp = maxHp, MoveSpeed = 3.2f, Damage = 18f, Radius = 2.2f },
                Player = new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r)
            };
        }

        /// <summary>90 s at 60 fps, health falling steadily from full to 2% over 80 s.</summary>
        static List<Frame> SteadyFight()
        {
            var frames = new List<Frame>();
            var t = 0f;
            const float dt = 1f / 60f;
            for (var i = 0; i < 60 * 90; i++)
            {
                frames.Add(MakeFrame(dt, t, Mathf.Lerp(1f, 0.02f, Mathf.Clamp01(t / 80f)) * 1200f, 1200f));
                t += dt;
            }
            return frames;
        }

        /// <summary>
        /// Varying dt; health in steps landing exactly on the 0.65 and 0.3 thresholds; a pause, the boss vanishing and
        /// returning, the ship sunk, max HP 0, the objective secured and a fresh boss after it.
        /// </summary>
        static List<Frame> EventfulFight()
        {
            var dts = new[] { 0.011f, 1f / 60f, 0.033f, 0.05f, 0.0213f, 1f / 30f };
            var frames = new List<Frame>();
            var t = 0f;
            for (var i = 0; i < 2900; i++)
            {
                var dt = dts[i % dts.Length];
                var hp = i < 500 ? 100f : i < 1000 ? 65f : i < 1400 ? 30f : 12f;
                var f = MakeFrame(dt, t, hp, 100f);
                if (i >= 300 && i < 360) f.Paused = true;
                if (i >= 900 && i < 960) f.HasBoss = false;
                if (i >= 1200 && i < 1500) f.Alive = false;
                if (i >= 1600 && i < 1650) { f.Boss.MaxHp = 0f; }
                if (i >= 2400 && i < 2500) f.Secured = true;
                if (i >= 2500) f.Boss.Hp = 100f; // a new boss after the secured window
                if (i >= 2700) f.Player = f.Boss.Position + new Vector3(0.02f, 1f, 0.03f); // on top of the boss: direction falls back to forward
                frames.Add(f);
                t += dt;
            }
            return frames;
        }

        static List<string> RunV3(IEnumerable<Frame> frames)
        {
            var log = new List<string>();
            var v3 = new V3Reference(log);
            v3.ResetForRun();
            v3.ResetForRun(); // V3RunBootstrap reset the director twice at run start
            var step = 0;
            foreach (var f in frames)
            {
                log.Add($"#{step++}");
                if (!f.Paused) v3.Tick(f.Dt, f);
                log.Add(v3.State());
            }
            return log;
        }

        List<string> RunV4(IEnumerable<Frame> frames, BossConfig config)
        {
            var log = new List<string>();
            var world = new FakeBossWorld(log);
            var player = new FakePlayer(log);
            var run = new FakeRun();
            var secured = false;
            var director = new BossDirector(world, new FakeProjectiles(log), player, new FakeEffects(log), run, () => config, () => secured);
            director.ResetForRun();
            director.ResetForRun();
            var step = 0;
            foreach (var f in frames)
            {
                world.HasBoss = f.HasBoss;
                world.Boss = f.Boss;
                player.Pos = f.Player;
                player.Alive = f.Alive;
                secured = f.Secured;
                run.Paused = f.Paused;
                log.Add($"#{step++}");
                director.Tick(f.Dt);
                var s = director.State;
                log.Add(StateLine((int)s.Phase, (int)s.Attack, s.PhaseTimer, s.CommittedDirection, s.MarkedPoint, s.Enraged,
                    (int)s.QueuedFollowUp, s.ScriptPhase, s.RotationStep, s.PendingHpPhase, s.Phase3ChargePassPending,
                    director.MarkedTelegraphVisible, director.MarkedTelegraphCenter, director.MarkedTelegraphRadius));
            }
            return log;
        }

        static void AssertSameLog(List<string> v3, List<string> v4)
        {
            var n = Math.Min(v3.Count, v4.Count);
            for (var i = 0; i < n; i++)
            {
                if (v3[i] == v4[i]) continue;
                var frame = v3.Take(i + 1).LastOrDefault(l => l.StartsWith("#"));
                Assert.Fail($"first difference at line {i} (frame {frame}):\nV3: {v3[i]}\nV4: {v4[i]}");
            }
            Assert.AreEqual(v3.Count, v4.Count, "log lengths");
        }

        // ---- Fixture --------------------------------------------------------------------------------------------

        readonly List<Object> _owned = new List<Object>();

        BossConfig NewConfig()
        {
            var c = ScriptableObject.CreateInstance<BossConfig>();
            c.configId = "boss.test";
            _owned.Add(c);
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        // ---- Side by side with V3 -------------------------------------------------------------------------------

        [Test]
        public void SteadyFight_MatchesV3_FrameForFrame()
        {
            var frames = SteadyFight();
            var v4 = RunV4(frames, NewConfig());
            AssertSameLog(RunV3(frames), v4);

            // The scenario has to exercise every rule it compares.
            Assert.Greater(v4.Count(l => l.StartsWith("shot ")), 0, "spread shots");
            Assert.Greater(v4.Count(l => l.StartsWith("dmg ")), 0, "blast damage");
            Assert.Greater(v4.Count(l => l.StartsWith("boom ")), 0, "slam explosions");
            Assert.Greater(v4.Count(l => l.StartsWith("toast ")), 0, "toasts");
            Assert.Greater(v4.Count(l => l.StartsWith("ctrl set 3 False")), 0, "charge velocity");
            Assert.Greater(v4.Count(l => l.StartsWith("ctrl set 3 True")), 0, "movement pause");
            Assert.IsTrue(v4.Any(l => l.Contains(" sp=3 ")), "phase 3 reached");
            Assert.IsTrue(v4.Any(l => l.Contains(" c2=True ")), "second charge pass");
        }

        [Test]
        public void EventfulFight_MatchesV3_FrameForFrame()
        {
            var frames = EventfulFight();
            var v4 = RunV4(frames, NewConfig());
            AssertSameLog(RunV3(frames), v4);
            Assert.Greater(v4.Count(l => l.StartsWith("dmg ")), 0, "blast damage");
            Assert.IsTrue(v4.Any(l => l.Contains(" sp=2 ")), "phase 2 at exactly 0.65");
            Assert.IsTrue(v4.Any(l => l.Contains(" dir=(0,0,1) ")), "forward fallback when the ship is on the boss");
        }

        [Test]
        public void GeneratedConfig_MatchesV3_FrameForFrame()
        {
            var config = NewConfig();
            V4BossConfigGenerator.FillFromV3(config);
            var frames = SteadyFight();
            AssertSameLog(RunV3(frames), RunV4(frames, config));
        }

        // ---- Rules one at a time --------------------------------------------------------------------------------

        sealed class Rig
        {
            public readonly List<string> Log = new List<string>();
            public readonly FakeBossWorld World;
            public readonly FakePlayer Player;
            public readonly FakeRun Run = new FakeRun();
            public readonly BossDirector Director;
            public bool Secured;
            public bool StageActive = true;
            public float Time;

            public Rig(BossConfig config, float hpRatio = 1f)
            {
                World = new FakeBossWorld(Log) { HasBoss = true };
                World.Boss = new BossSnapshot { Slot = 1, Position = Vector3.zero, Hp = hpRatio * 100f, MaxHp = 100f, MoveSpeed = 3f, Damage = 10f, Radius = 2f };
                Player = new FakePlayer(Log) { Pos = new Vector3(0f, 0f, 8f) };
                Director = new BossDirector(World, new FakeProjectiles(Log), Player, new FakeEffects(Log), Run, () => config, () => Secured, () => StageActive);
                Director.ResetForRun();
            }

            public float Hp { set => World.Boss.Hp = value * 100f; }

            public void Step(float dt = 1f / 60f)
            {
                Director.Tick(dt);
                Time += dt;
            }

            /// <summary>Ticks until the next telegraph starts; returns its attack.</summary>
            public BossAttackKind NextTelegraph(float maxSeconds = 20f)
            {
                var start = Time;
                var was = Director.Phase;
                while (Time - start < maxSeconds)
                {
                    Step();
                    if (Director.Phase == BossPhase.Telegraph && was != BossPhase.Telegraph) return Director.CurrentAttack;
                    was = Director.Phase;
                }
                Assert.Fail("no telegraph");
                return BossAttackKind.None;
            }

            /// <summary>Ticks until the current attack ends; returns the rest it set (the timer on the tick it entered Recovery).</summary>
            public float NextRecovery(float maxSeconds = 20f)
            {
                var start = Time;
                var was = Director.Phase;
                while (Time - start < maxSeconds)
                {
                    Step();
                    if (Director.Phase == BossPhase.Recovery && was == BossPhase.Execute) return Director.PhaseTimer;
                    was = Director.Phase;
                }
                Assert.Fail("no recovery");
                return 0f;
            }
        }

        [Test]
        public void HealthPhase_ChangesOnlyAfterTheCurrentAttacksRest()
        {
            var rig = new Rig(NewConfig());
            Assert.AreEqual(BossAttackKind.AimedSpread, rig.NextTelegraph());
            Assert.AreEqual(1, rig.Director.ScriptPhase);
            rig.Hp = 0.2f;
            // Still phase 1 through the telegraph and the execute step.
            while (rig.Director.Phase != BossPhase.Recovery)
            {
                rig.Step();
                if (rig.Director.Phase != BossPhase.Recovery) Assert.AreEqual(1, rig.Director.ScriptPhase, "mid-attack");
            }
            // Enraged (health <= 0.3) already shortens the phase-1 spread's rest: 2 × 0.75.
            Assert.AreEqual(1.5f, rig.Director.PhaseTimer, 1e-6f);
            // The lower phase is taken up on the next tick (the first one in the rest) and waits for the rest to end.
            rig.Step();
            Assert.AreEqual(1, rig.Director.ScriptPhase, "still phase 1 during the rest");
            Assert.AreEqual(3f, rig.Director.State.PendingHpPhase, "phase 3 pending");
            // After the rest: phase 3's rotation from its start.
            Assert.AreEqual(BossAttackKind.CommittedCharge, rig.NextTelegraph());
            Assert.AreEqual(3, rig.Director.ScriptPhase);
        }

        [Test]
        public void FinalPhase_EveryRotationChargeIsFollowedByASecondCharge()
        {
            var rig = new Rig(NewConfig(), 0.2f);
            var attacks = Enumerable.Range(0, 7).Select(_ => rig.NextTelegraph()).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                BossAttackKind.CommittedCharge, BossAttackKind.CommittedCharge, BossAttackKind.AimedSpread,
                BossAttackKind.MarkedBombardment, BossAttackKind.ShellSlam, BossAttackKind.CommittedCharge, BossAttackKind.CommittedCharge
            }, attacks);

            var config = NewConfig();
            config.charge.finalPhaseSecondCharge = false;
            rig = new Rig(config, 0.2f);
            attacks = Enumerable.Range(0, 5).Select(_ => rig.NextTelegraph()).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                BossAttackKind.CommittedCharge, BossAttackKind.AimedSpread, BossAttackKind.MarkedBombardment,
                BossAttackKind.ShellSlam, BossAttackKind.CommittedCharge
            }, attacks);
        }

        [Test]
        public void EnragedRest_IsShortened_ExceptAfterShellSlam()
        {
            // Phase 3 (enraged): charge 2 × 0.75 (twice), spread 2 × 0.75, bombard 2.5 × 0.75, slam 2 (not shortened).
            var rig = new Rig(NewConfig(), 0.2f);
            var rests = Enumerable.Range(0, 5).Select(_ => rig.NextRecovery()).ToArray();
            CollectionAssert.AreEqual(new[] { 1.5f, 1.5f, 1.5f, 1.875f, 2f }, rests);

            // Enraged without phase 3 (config: enraged below 70%, health 68% = phase 1): spread 1.5, slam 2.
            var config = NewConfig();
            config.enrageThreshold = 0.7f;
            rig = new Rig(config, 0.68f);
            Assert.AreEqual(1.5f, rig.NextRecovery(), 1e-6f, "spread");
            Assert.AreEqual(2f, rig.NextRecovery(), 1e-6f, "slam");
            Assert.IsTrue(rig.Director.Enraged);
            Assert.AreEqual(1, rig.Director.ScriptPhase);

            // Not enraged, phase 1: full rests.
            rig = new Rig(NewConfig(), 1f);
            Assert.AreEqual(2f, rig.NextRecovery(), 1e-6f);
        }

        [Test]
        public void Attacks_FireOnceAtTheStartOfExecute_WithV3Numbers()
        {
            var rig = new Rig(NewConfig());
            Assert.AreEqual(BossAttackKind.AimedSpread, rig.NextTelegraph());
            rig.Log.Clear();
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            var shots = rig.Log.Where(l => l.StartsWith("shot ")).ToList();
            Assert.AreEqual(5, shots.Count);
            // Middle shot straight at the ship (+z): muzzle = boss + dir × (2 + 1.4) + up × 0.6; damage 10 × 0.55.
            Assert.AreEqual($"shot {V(Vector3.forward * (2f + 1.4f) + Vector3.up * 0.6f)} {V(Vector3.forward)} {F(14f)} {F(10f * 0.55f)} {F(2.4f)} {F(0.55f)} {F(0.45f)}", shots[2]);
            rig.Log.Clear();
            while (rig.Director.Phase == BossPhase.Execute) rig.Step();
            Assert.IsFalse(rig.Log.Any(l => l.StartsWith("shot ")), "fires once per execute step");

            // Shell slam next; the ship stays on the marked point: 10 × 1.35 at the flattened centre, plus the explosion.
            Assert.AreEqual(BossAttackKind.ShellSlam, rig.NextTelegraph());
            Assert.IsTrue(rig.Director.MarkedTelegraphVisible);
            Assert.AreEqual(5f, rig.Director.MarkedTelegraphRadius);
            rig.Log.Clear();
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            CollectionAssert.Contains(rig.Log, $"dmg {F(10f * 1.35f)} {V(new Vector3(0f, 0f, 8f))}");
            CollectionAssert.Contains(rig.Log, $"boom {V(new Vector3(0f, 0f, 8f))} {F(5f)}");
            Assert.IsFalse(rig.Director.MarkedTelegraphVisible);

            // Spread, then marked bombardment (radius 5.5, × 1.15), no toast for it.
            rig.NextTelegraph();
            Assert.AreEqual(BossAttackKind.MarkedBombardment, rig.NextTelegraph());
            Assert.AreEqual(5.5f, rig.Director.MarkedTelegraphRadius);
            rig.Log.Clear();
            rig.Player.Pos = new Vector3(5.4f, 0f, 8f); // still inside 5.5 of the mark
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            CollectionAssert.Contains(rig.Log, $"dmg {F(10f * 1.15f)} {V(new Vector3(0f, 0f, 8f))}");
            Assert.IsFalse(rig.Log.Any(l => l.StartsWith("toast ")), "bombardment has no toast");
        }

        [Test]
        public void SunkShip_TakesNoBombardment_ButTheSlamStillCallsDamage()
        {
            var rig = new Rig(NewConfig());
            rig.Player.Alive = false;
            Assert.AreEqual(BossAttackKind.AimedSpread, rig.NextTelegraph());
            Assert.AreEqual(BossAttackKind.ShellSlam, rig.NextTelegraph());
            rig.Log.Clear();
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            Assert.IsTrue(rig.Log.Any(l => l.StartsWith("dmg ")), "V3 slam had no alive check");
            rig.NextTelegraph();
            Assert.AreEqual(BossAttackKind.MarkedBombardment, rig.NextTelegraph());
            rig.Log.Clear();
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            Assert.IsFalse(rig.Log.Any(l => l.StartsWith("dmg ")), "V3 bombardment skipped a sunk ship");
        }

        [Test]
        public void Toasts_RepeatEvery1_2Seconds_WhileTelegraphing()
        {
            var config = NewConfig();
            config.charge.telegraphSeconds = 3f;
            var rig = new Rig(config, 0.2f); // phase 3 opens with a charge
            Assert.AreEqual(BossAttackKind.CommittedCharge, rig.NextTelegraph());
            while (rig.Director.Phase == BossPhase.Telegraph) rig.Step();
            var toasts = rig.Log.Where(l => l.StartsWith("toast ")).ToList();
            Assert.AreEqual(3, toasts.Count, "at 0, 1.2 and 2.4 s of a 3 s telegraph");
            Assert.AreEqual($"toast Barnacle King — charge incoming {F(1.1f)}", toasts[0]);
        }

        [Test]
        public void Movement_PausedInTelegraphAndRest_ForcedDuringCharge()
        {
            var rig = new Rig(NewConfig(), 0.2f);
            rig.Step();
            Assert.AreEqual($"ctrl set 1 True {V(Vector3.zero)} False", rig.Log.Last(), "paused during the opening rest");
            Assert.AreEqual(BossAttackKind.CommittedCharge, rig.NextTelegraph());
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            // Charge along the committed direction (+z) at move speed 3 × 2.6.
            Assert.AreEqual($"ctrl set 1 False {V(Vector3.forward * (3f * 2.6f))} True", rig.Log.Last());
            rig.NextRecovery();
            // The last 0.05 s of a rest release the boss.
            while (rig.Director.PhaseTimer > BossAttackLogic.RecoveryPauseFloor) rig.Step();
            Assert.AreEqual("ctrl clear", rig.Log.Last());
        }

        [Test]
        public void Gates_PauseKeepsState_ObjectiveAndLostBossReset()
        {
            var rig = new Rig(NewConfig());
            rig.NextTelegraph();
            rig.NextTelegraph(); // slam: marked circle showing
            Assert.IsTrue(rig.Director.MarkedTelegraphVisible);
            var timer = rig.Director.PhaseTimer;

            rig.Run.Paused = true;
            for (var i = 0; i < 30; i++) rig.Step();
            Assert.AreEqual(timer, rig.Director.PhaseTimer, "paused: no change");
            Assert.IsTrue(rig.Director.MarkedTelegraphVisible, "paused: the circle stays as it was (V3)");
            rig.Run.Paused = false;

            rig.StageActive = false;
            rig.Step();
            Assert.AreEqual(timer, rig.Director.PhaseTimer, "stage gate");
            rig.StageActive = true;

            rig.Run.Gameplay = false;
            rig.Step();
            Assert.AreEqual(timer, rig.Director.PhaseTimer, "gameplay gate");
            rig.Run.Gameplay = true;

            rig.World.Present = false;
            rig.Step();
            Assert.IsTrue(rig.Director.Active, "no enemy world: nothing happens, not even a reset (V3)");
            rig.World.Present = true;

            rig.Secured = true;
            rig.Log.Clear();
            rig.Step();
            Assert.IsFalse(rig.Director.Active);
            Assert.IsFalse(rig.Director.MarkedTelegraphVisible);
            CollectionAssert.AreEqual(new[] { "ctrl clear" }, rig.Log);
            rig.Step();
            Assert.AreEqual(1, rig.Log.Count, "secured and inactive: no further resets");
            rig.Secured = false;

            rig.Step();
            Assert.IsTrue(rig.Director.Active, "a boss found again starts a fresh pattern");
            rig.World.HasBoss = false;
            rig.Log.Clear();
            rig.Step();
            Assert.IsFalse(rig.Director.Active);
            Assert.AreEqual(-1, rig.Director.BossSlot);
            CollectionAssert.AreEqual(new[] { "ctrl clear" }, rig.Log);
        }

        [Test]
        public void LiveConfigEdits_ApplyFromTheNextAttack()
        {
            var config = NewConfig();
            var rig = new Rig(config);
            Assert.AreEqual(BossAttackKind.AimedSpread, rig.NextTelegraph());
            config.spread.shotCount = 3;
            config.spread.damageMultiplier = 1f;
            rig.Log.Clear();
            while (rig.Director.Phase != BossPhase.Execute) rig.Step();
            var shots = rig.Log.Where(l => l.StartsWith("shot ")).ToList();
            Assert.AreEqual(3, shots.Count);
            StringAssert.Contains($" {F(10f)} ", shots[0]);

            config.phase1Rotation = new[] { BossAttackKind.MarkedBombardment };
            Assert.AreEqual(BossAttackKind.MarkedBombardment, rig.NextTelegraph());
        }

        [Test]
        public void MissingConfig_FallsBackToV3Values()
        {
            var log = new List<string>();
            var world = new FakeBossWorld(log) { HasBoss = true, Boss = new BossSnapshot { Slot = 0, Hp = 1f, MaxHp = 1f, Damage = 10f, Radius = 2f } };
            var director = new BossDirector(world, new FakeProjectiles(log), new FakePlayer(log) { Pos = Vector3.forward * 8f }, new FakeEffects(log), new FakeRun(), () => null, () => false);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "BossDirector: no BossConfig in the config set; using V3's values.");
            director.ResetForRun();
            Assert.AreEqual(0.5f, director.PhaseTimer);
            for (var i = 0; i < 60; i++) director.Tick(1f / 60f);
            Assert.AreEqual(BossAttackKind.AimedSpread, director.CurrentAttack);
        }

        // ---- Config ---------------------------------------------------------------------------------------------

        [Test]
        public void DefaultConfig_EqualsV3Constants()
        {
            var c = NewConfig();
            Assert.AreEqual(BarnacleKingBossAttackLogic.SpreadTelegraphSeconds, c.spread.telegraphSeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.ChargeTelegraphSeconds, c.charge.telegraphSeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.BombardTelegraphSeconds, c.bombard.telegraphSeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.SpreadTelegraphSeconds, c.slam.telegraphSeconds, "V3 slam used the spread telegraph");
            Assert.AreEqual(BarnacleKingBossAttackLogic.ChargeCommitSeconds, c.charge.executeSeconds);
            Assert.AreEqual(0.35f, c.bombard.executeSeconds);
            Assert.AreEqual(0.25f, c.spread.executeSeconds);
            Assert.AreEqual(0.25f, c.slam.executeSeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.SpreadRecoverySeconds, c.spread.recoverySeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.ChargeRecoverySeconds, c.charge.recoverySeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.BombardRecoverySeconds, c.bombard.recoverySeconds);
            Assert.AreEqual(BarnacleKingBossAttackLogic.SpreadRecoverySeconds, c.slam.recoverySeconds, "V3 slam used the spread recovery");
            Assert.AreEqual(BarnacleKingBossAttackLogic.EnragedRecoveryScale, c.enragedRecoveryScale);
            Assert.IsFalse(c.enragedRecoveryAppliesToShellSlam);
            Assert.AreEqual(BarnacleKingBossAttackLogic.MarkedBombardmentRadius, c.bombard.radius);
            Assert.AreEqual(V4BossConfigGenerator.DirectorConstant("ChargeSpeedMult", float.NaN), c.charge.speedMultiplier);
            Assert.AreEqual(V4BossConfigGenerator.DirectorConstant("SpreadProjectileSpeed", float.NaN), c.spread.projectileSpeed);
            CollectionAssert.AreEqual(V4BossConfigGenerator.V3Rotation("Phase1"), c.phase1Rotation);
            CollectionAssert.AreEqual(V4BossConfigGenerator.V3Rotation("Phase2"), c.phase2Rotation);
            CollectionAssert.AreEqual(V4BossConfigGenerator.V3Rotation("Phase3"), c.phase3Rotation);

            // V3 literals (no symbol to read): thresholds, start rest, spread, blasts, toasts, circle.
            Assert.AreEqual(0.65f, c.phase2Threshold);
            Assert.AreEqual(0.3f, c.phase3Threshold);
            Assert.AreEqual(0.3f, c.enrageThreshold);
            Assert.AreEqual(0.5f, c.startRecoverySeconds);
            Assert.AreEqual(5, c.spread.shotCount);
            Assert.AreEqual(12f, c.spread.spreadDegrees);
            Assert.AreEqual(0.55f, c.spread.damageMultiplier);
            Assert.AreEqual(2.4f, c.spread.projectileLifeSeconds);
            Assert.AreEqual(0.55f, c.spread.projectileHitRadius);
            Assert.AreEqual(0.45f, c.spread.projectileVisualRadius);
            Assert.AreEqual(1.4f, c.spread.muzzleForwardOffset);
            Assert.AreEqual(0.6f, c.spread.muzzleHeight);
            Assert.AreEqual(1.15f, c.bombard.damageMultiplier);
            Assert.AreEqual(5f, c.slam.radius);
            Assert.AreEqual(1.35f, c.slam.damageMultiplier);
            Assert.IsTrue(c.charge.finalPhaseSecondCharge);
            Assert.AreEqual(1.2f, c.toastIntervalSeconds);
            Assert.AreEqual(1.1f, c.toastDurationSeconds);
            Assert.AreEqual(new Color(1f, 0.35f, 0.1f, 0.55f), c.telegraphColor);
            Assert.AreEqual(0.08f, c.telegraphHeight);
            Assert.AreEqual(0.12f, c.telegraphThickness);
            Assert.AreEqual((int)BarnacleKingAttackKind.ShellSlam, (int)BossAttackKind.ShellSlam, "enum values mirror V3's");
            Assert.AreEqual((int)BarnacleKingBossPhase.Execute, (int)BossPhase.Execute);
        }

        [Test]
        public void GeneratorFill_EqualsTheDefaults()
        {
            var filled = NewConfig();
            filled.spread.shotCount = 9;
            filled.phase3Rotation = new[] { BossAttackKind.ShellSlam };
            V4BossConfigGenerator.FillFromV3(filled);
            Assert.AreEqual(V4BossConfigGenerator.ConfigId, filled.configId);
            var defaults = NewConfig();
            defaults.configId = V4BossConfigGenerator.ConfigId;
            Assert.AreEqual(defaults.ComputeHash(), filled.ComputeHash());
        }

        [Test]
        public void Validation_AcceptsDefaults_RejectsBrokenPatterns()
        {
            var c = NewConfig();
            var report = new ConfigValidationReport();
            c.Validate(report);
            Assert.IsTrue(report.IsValid, report.ToString());

            c.phase2Rotation = new[] { BossAttackKind.AimedSpread, BossAttackKind.None };
            c.phase3Rotation = new BossAttackKind[0];
            c.slam.executeSeconds = 0f;
            c.phase3Threshold = 0.8f;
            report = new ConfigValidationReport();
            c.Validate(report);
            var paths = report.Errors.Select(e => e.Path).ToList();
            CollectionAssert.Contains(paths, "phase2Rotation[1]");
            CollectionAssert.Contains(paths, "phase3Rotation");
            CollectionAssert.Contains(paths, "slam.executeSeconds");
            CollectionAssert.Contains(paths, "phase3Threshold");
        }

        // ---- No random draws ------------------------------------------------------------------------------------

        [Test]
        public void Director_DrawsNoRandomNumbers()
        {
            UnityEngine.Random.InitState(4242);
            var state = UnityEngine.Random.state;
            RunV4(EventfulFight(), NewConfig());
            Assert.AreEqual(state, UnityEngine.Random.state, "no draws from Unity's random state");

            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Game", "V4", "Runtime", "Stage", "BossDirector.cs"));
            StringAssert.DoesNotContain("Random", source);
            var ctor = typeof(BossDirector).GetConstructors().Single();
            Assert.IsFalse(ctor.GetParameters().Any(p => p.ParameterType == typeof(IRunRandom)), "no run random stream");
        }

        // ---- Presenter ------------------------------------------------------------------------------------------

        sealed class FakeBossView : IBossView
        {
            public bool Visible;
            public Vector3 Center;
            public float Radius;
            public bool Active => true;
            public BossAttackKind CurrentAttack => BossAttackKind.ShellSlam;
            public BossPhase Phase => BossPhase.Telegraph;
            public float PhaseTimer => 1f;
            public int ScriptPhase => 1;
            public bool Enraged => false;
            public Vector3 MarkedPoint => Center;
            public bool MarkedTelegraphVisible => Visible;
            public Vector3 MarkedTelegraphCenter => Center;
            public float MarkedTelegraphRadius => Radius;
        }

        [Test]
        public void TelegraphPresenter_DrawsTheV3Circle_AndCleansUp()
        {
            var host = new GameObject("BossTelegraphPresenterTest");
            _owned.Add(host);
            var presenter = host.AddComponent<BossTelegraphPresenter>();
            var view = new FakeBossView();
            var config = NewConfig();
            presenter.Bind(view, () => config);

            presenter.Refresh();
            Assert.IsTrue(presenter.Circle == null, "nothing created before the first telegraph");

            view.Visible = true;
            view.Center = new Vector3(3f, 0.3f, -2f);
            view.Radius = 5.5f;
            presenter.Refresh();
            var circle = presenter.Circle;
            Assert.IsTrue(circle != null && circle.activeSelf);
            _owned.Add(circle);
            Assert.AreEqual(view.Center + Vector3.up * 0.08f, circle.transform.position);
            Assert.AreEqual(new Vector3(11f, 0.12f, 11f), circle.transform.localScale);
            Assert.IsTrue(circle.GetComponent<Collider>() == null, "collider removed");
            var material = circle.GetComponent<Renderer>().sharedMaterial;
            var expected = new Color(1f, 0.35f, 0.1f, 0.55f);
            Assert.Less(Mathf.Abs(material.color.r - expected.r) + Mathf.Abs(material.color.g - expected.g) + Mathf.Abs(material.color.b - expected.b) + Mathf.Abs(material.color.a - expected.a), 1e-3f,
                $"telegraph colour {material.color}"); // the material stores colours at its own precision

            config.telegraphColor = Color.red;
            presenter.Refresh();
            Assert.AreEqual(Color.red, material.color, "live colour edit");

            view.Visible = false;
            presenter.Refresh();
            Assert.IsFalse(circle.activeSelf);

            // OnDestroy (not called for edit-mode objects) removes the circle and its material.
            typeof(BossTelegraphPresenter).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
            Assert.IsTrue(circle == null, "circle destroyed");
            Assert.IsTrue(material == null, "material destroyed");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>One minute of a simulated run.</summary>
    public struct SimMinute
    {
        public float AliveAverage;
        public int Kills, Level, Gold, Spawns;
        public float DamageTaken, Healed, HullShareAtEnd, SingleTargetDps, Offense, Pressure;
        public bool Reached;
    }

    /// <summary>What one simulated run produced.</summary>
    public sealed class SimRunResult
    {
        public string PersonalityId, CaptainKey;
        public int Seed;
        public float EndSeconds;
        public bool Died;
        public string EndReason;
        public int Kills, GoldEarned, Rum, FinalLevel, LevelAt5 = -1, LevelAt10 = -1, ChestsOpened, LandmarksUsed, ElitesSunk, Refreshes, ItemsBy10;
        /// <summary>The build's offense index at 10:00 (the director's formula; 0 when the run ended before).</summary>
        public float OffenseAt10;
        public float BossSummonSeconds = -1f, BossKillSeconds = -1f, DamageTaken, BossSpawnHp;
        /// <summary>Hull lost per enemy kind (what sinks this personality).</summary>
        public readonly Dictionary<string, float> DamageBySource = new Dictionary<string, float>();
        /// <summary>The director's first offense sample (the run's starting offense).</summary>
        public float Offense0;
        public bool BossWon => BossKillSeconds >= 0f;
        public readonly List<float> LevelUpTimes = new List<float>();
        public readonly List<(string weapon, float time)> Evolutions = new List<(string, float)>();
        public readonly List<string> Weapons = new List<string>(), Logbooks = new List<string>(), Items = new List<string>();
        public readonly List<SimMinute> Minutes = new List<SimMinute>();
        /// <summary>Each arena (phase id, start, seconds until cleared, the focus enemy's HP at spawn).</summary>
        public readonly List<(string phase, float start, float seconds, float hp)> Arenas = new List<(string, float, float, float)>();
        /// <summary>Seconds each fodder / elite took to sink from its first hit (for the model's time-to-kill check).</summary>
        public float FodderTtkSum, EliteTtkSum, MinibossTtkSum;
        public int FodderTtkCount, EliteTtkCount, MinibossTtkCount;
        public float EndMinutes => EndSeconds / 60f;
    }

    /// <summary>
    /// V5 M2: one simulated Saltwind run (Q20: new on V4, no V3 code). The build is the real V4 build modules
    /// (<see cref="SimBuild"/>); the world is abstract: every enemy is a distance to the ship that shrinks at its speed
    /// times the personality's movement factor, weapons hit the nearest enemies in range at their resolved damage, rate,
    /// volley and coverage, and enemies in reach hit back with the personality's dodge and contact avoidance. Spawning
    /// follows the timeline (bands, the V5 flow's waves, floor and hordes, arenas, pulses, the boss, overtime), enemy
    /// stats the difficulty curve, and the director (Observe or Adaptive) its own sample loop with the V4 formulas.
    /// Deterministic: the same seed and configs give the same result.
    /// </summary>
    public sealed class SimRun
    {
        struct Enemy
        {
            public bool Active, Elite, Focus, Boss, Cache, Ambient;
            public SimEnemyDef Def;
            public float Hp, MaxHp, Damage, Speed, Dist, AttackTimer, FirstHitAt;
            public float XpMultiplier, GoldMultiplier;
            public int ScheduledElite;
        }

        readonly SimContent _c;
        readonly PlayerPersonality _p;
        readonly SimCalibration _k;
        readonly System.Random _rng;
        readonly SimBuild _b;
        readonly SimRunResult _r = new SimRunResult();
        readonly Enemy[] _enemies;
        readonly float _dt;
        int _alive;
        float _t;

        // Spawning state
        float _ambientNext, _gapReadyAt, _topUpBudgetAt, _overtimeNext;
        int _topUpBudget;
        readonly HashSet<string> _phasesStarted = new HashSet<string>();
        readonly HashSet<int> _hordesDone = new HashSet<int>();
        string _arenaPhase;
        readonly List<(float start, float end)> _releases = new List<(float, float)>();
        float _capBlockedAt = float.NegativeInfinity;

        // Director
        float _pressure = 1f, _sampleTimer, _offense0;
        readonly Queue<(float time, int kills, int spawns, float hullLost)> _window = new Queue<(float, int, int, float)>();
        int _pendingKills, _pendingSpawns;
        float _pendingHullLost;

        // Player-side state
        float _xpCarry, _hitBudget, _battleSignalUntil, _deadCalmUntil, _lastPowerupAt = float.NegativeInfinity;
        int _powerupsDropped, _chestsSeen, _cacheAlive;
        float _uncollectedXp;
        readonly Dictionary<WeaponSlot, float> _weaponTimers = new Dictionary<WeaponSlot, float>(); // by weapon: slots re-sync on level-ups
        readonly List<float> _chestTimes = new List<float>();
        readonly List<(float time, PersonalityLandmarks kind)> _landmarks = new List<(float, PersonalityLandmarks)>();
        float _beaconFoundAt = float.PositiveInfinity, _bossSummonAt = float.PositiveInfinity, _activeDecisionAt;
        int _bossIndex = -1;
        bool _bossDone, _cachePending;
        int _bossLevel;
        readonly float _fodderHp;
        int _cacheStartedCount;

        // Per-minute bookkeeping
        SimMinute _minute;
        float _aliveSum;
        int _aliveSamples, _minuteIndex;
        readonly Dictionary<int, int> _killsPerMinute = new Dictionary<int, int>();
        readonly Dictionary<int, int> _killsPerOvertimeMinute = new Dictionary<int, int>();

        public SimRun(SimContent content, PlayerPersonality personality, int seed, string captainKey = null)
        {
            _c = content;
            _p = personality;
            _k = content.Targets != null ? content.Targets.calibration : new SimCalibration();
            _rng = new System.Random(seed);
            _dt = content.Targets != null ? Mathf.Clamp(content.Targets.stepSeconds, 0.05f, 1f) : 0.25f;
            var captain = content.Captain(captainKey);
            _b = new SimBuild(content, personality, _rng, captain);
            _enemies = new Enemy[Mathf.Max(8, EnemyCap + 8)];
            _r.PersonalityId = personality.personalityId;
            _r.CaptainKey = captain != null ? captain.captainKey : null;
            _r.Seed = seed;
            _fodderHp = content.FodderMaxBaseHp;
            PlanExploration();
        }

        public SimBuild Build => _b;
        /// <summary>Tuning harness (V5 M3c): the ship takes no damage (the level rhythm is fitted without deaths).</summary>
        public bool Unsinkable { get => _b.Unsinkable; set => _b.Unsinkable = value; }
        /// <summary>Every hit the ship takes (time, damage, the enemy's id) — calibration and tests.</summary>
        public Action<float, float, string> HitTaken;
        /// <summary>Calibration harness (the V4 trace player's switches): sink an arena's enemy this long after it spawns (−1 = off).</summary>
        public float KillArenaAfterSeconds = -1f;
        /// <summary>Calibration harness: summon the boss at this time whatever the personality does (−1 = off)...</summary>
        public float SummonBossAtSeconds = -1f;
        /// <summary>...and sink it at this time if it still floats (−1 = off).</summary>
        public float KillBossAtSeconds = -1f;
        float _arenaStartedAt, _arenaHp;
        public float Time => _t;
        public int Alive => _alive;
        public float Pressure => _pressure;

        WaveTimelineConfig T => _c.Timeline;
        FlowSettings Flow => T != null && T.flow != null && T.flow.enabled ? T.flow : null;
        int EnemyCap => T != null && T.maxActiveEnemies > 0 ? Mathf.Clamp(T.maxActiveEnemies, 8, 180) : 80;
        int AmbientCap => Mathf.Max(1, EnemyCap - (T != null ? T.reservedEncounterSlots : 0));
        float Deadline => T != null ? T.stageDeadlineSeconds : 600f;
        bool BossFight => _bossIndex >= 0 && _enemies[_bossIndex].Active && _enemies[_bossIndex].Boss;
        bool Adaptive => _c.Mode == DirectorMode.Adaptive;

        /// <summary>Plays the run to its end.</summary>
        public SimRunResult Play()
        {
            var maxSeconds = (_c.Targets != null ? _c.Targets.maxMinutes : 30f) * 60f;
            while (true)
            {
                Step();
                if (!_b.Alive) { Finish(true, "sunk"); break; }
                if (StopNow()) { Finish(false, "quit"); break; }
                if (_t >= maxSeconds) { Finish(false, "time limit"); break; }
            }
            return _r;
        }

        bool StopNow()
        {
            switch (_p.stop)
            {
                case PersonalityStop.Time: return _t >= _p.stopMinutes * 60f;
                case PersonalityStop.AfterBoss: return _r.BossWon && _t >= _r.BossKillSeconds + _p.stopMinutes * 60f;
                default: return false;
            }
        }

        /// <summary>One simulation step (public for tests that jump through a run).</summary>
        public void Step()
        {
            _b.RunTime = _t;
            SampleDirector();
            Spawn();
            MoveAndAttack();
            FireWeapons();
            TickBuild();
            TickExploration();
            Bookkeep();
            _t += _dt;
        }

        // ---- Exploration: landmarks, chests, the Beacon ----------------------------------------------------------------

        void PlanExploration()
        {
            float Explore() => Mathf.Lerp(_k.exploreMinutes.x, _k.exploreMinutes.y, (float)_rng.NextDouble()) * 60f;
            foreach (PersonalityLandmarks kind in new[] { PersonalityLandmarks.Wreck, PersonalityLandmarks.Cache, PersonalityLandmarks.Buoy, PersonalityLandmarks.Chart })
                if ((_p.landmarks & kind) != 0) _landmarks.Add((Explore(), kind));
            _landmarks.Sort((a, b) => a.time.CompareTo(b.time));
            var chests = _c.Stage != null ? _c.Stage.salvageChestCount : 0;
            var share = Mathf.Clamp01(_k.chestsFoundBaseShare + (1f - _k.chestsFoundBaseShare) * _p.pickupGreed);
            _chestsSeen = Mathf.RoundToInt(chests * share);
            for (var i = 0; i < _chestsSeen; i++) _chestTimes.Add(Explore());
            _chestTimes.Sort();

            var s = _c.Stage;
            var hint = s != null ? s.beaconDirectionHintSeconds : 300f;
            var marker = s != null ? s.beaconExactMarkerSeconds : 420f;
            var markerFound = marker + _k.beaconTravelSeconds;
            switch (_p.bossTiming)
            {
                case PersonalityBossTiming.Minute:
                    // Explorers stumble on the Beacon while sailing; the marker shows it to everyone else.
                    _beaconFoundAt = Mathf.Min(markerFound, Explore() * (0.6f + 0.4f * (1f - _p.pickupGreed)));
                    _bossSummonAt = Mathf.Max(_p.bossMinute * 60f, _beaconFoundAt);
                    break;
                case PersonalityBossTiming.OnHint:
                    _beaconFoundAt = hint + _k.hintSearchSeconds;
                    _bossSummonAt = _beaconFoundAt;
                    break;
                case PersonalityBossTiming.OnMarker:
                    _beaconFoundAt = markerFound;
                    _bossSummonAt = _beaconFoundAt;
                    break;
                default:
                    _bossSummonAt = float.PositiveInfinity;
                    break;
            }
        }

        void TickExploration()
        {
            var s = _c.Stage;
            // Landmarks
            while (_landmarks.Count > 0 && _landmarks[0].time <= _t)
            {
                var kind = _landmarks[0].kind;
                _landmarks.RemoveAt(0);
                if (_arenaPhase != null || BossFight) { _landmarks.Add((_t + 20f, kind)); _landmarks.Sort((a, b) => a.time.CompareTo(b.time)); break; }
                UseLandmark(kind, s);
            }
            // Chests: bought when found (policy), or on the way back once affordable.
            for (var i = 0; i < _chestTimes.Count; i++)
            {
                if (_chestTimes[i] > _t) break;
                if (!WantsChest()) continue;
                var price = s != null ? RoguePirates.V4.Stage.ChestEconomy.ChestPrice(s, _r.ChestsOpened) : int.MaxValue;
                if (_b.Gold < price) continue;
                _b.Gold -= price;
                _chestTimes.RemoveAt(i);
                OpenChest(0);
                break; // one per step
            }
            // The Barnacle King
            var summonAt = SummonBossAtSeconds >= 0f ? SummonBossAtSeconds : _bossSummonAt;
            if (!_bossDone && _bossIndex < 0 && _t >= summonAt && (_arenaPhase == null || SummonBossAtSeconds >= 0f)) SummonBoss();
            if (KillBossAtSeconds >= 0f && _t >= KillBossAtSeconds && BossFight) Remove(_bossIndex, true);
            if (KillArenaAfterSeconds >= 0f && _arenaPhase != null && _t >= _arenaStartedAt + KillArenaAfterSeconds)
                for (var i = 0; i < _enemies.Length; i++) if (_enemies[i].Active && _enemies[i].Focus) Remove(i, true);
            // The cache ambush: paid when every ambusher is sunk.
            if (_cachePending && _cacheAlive <= 0)
            {
                _cachePending = false;
                _b.Gold += s != null ? s.powderCacheBonusGold : 20;
                OpenChest(s != null ? s.powderCacheChestMinRarity : 2);
            }
        }

        bool WantsChest()
        {
            switch (_p.chests)
            {
                case PersonalityChests.All: return true;
                case PersonalityChests.Some: return _b.Gold >= 2 * ChestPriceNow(); // PLAYER_PERSONALITIES.md: buys when gold >= 2x price
                case PersonalityChests.Evolution: return _b.WantsEvolutionItem(out _) || _b.Gold >= 3 * ChestPriceNow();
                default: return false;
            }
        }

        int ChestPriceNow() => _c.Stage != null ? RoguePirates.V4.Stage.ChestEconomy.ChestPrice(_c.Stage, _r.ChestsOpened) : int.MaxValue;

        void UseLandmark(PersonalityLandmarks kind, StageContentConfig s)
        {
            _r.LandmarksUsed++;
            switch (kind)
            {
                case PersonalityLandmarks.Wreck:
                    _b.Heal(_b.MaxHull * (s != null ? s.repairWreckHealFraction : 0.25f));
                    break;
                case PersonalityLandmarks.Buoy:
                    var rewards = s != null ? s.signalBuoyRewards : null;
                    if (rewards != null && rewards.Length > 0)
                    {
                        var reward = rewards[_rng.Next(rewards.Length)];
                        _b.Stats.Add("landmark:buoy", reward.stat, reward.add, reward.multiply);
                    }
                    break;
                case PersonalityLandmarks.Chart:
                    // Reveals chests nearby: the chests still to be found come sooner.
                    var reveal = s != null ? s.treasureChartRevealCount : 3;
                    for (var i = 0; i < _chestTimes.Count && reveal > 0; i++) if (_chestTimes[i] > _t + 30f) { _chestTimes[i] = _t + 30f + 10f * reveal; reveal--; }
                    _chestTimes.Sort();
                    break;
                case PersonalityLandmarks.Cache:
                    StartCacheAmbush(s);
                    break;
            }
        }

        void StartCacheAmbush(StageContentConfig s)
        {
            if (s == null) return;
            var spawners = s.powderCacheEliteChallenge && s.powderCacheEliteSpawners != null && s.powderCacheEliteSpawners.Length > 0
                ? s.powderCacheEliteSpawners : new[] { s.powderCacheSpawner };
            var count = s.powderCacheEliteChallenge ? s.powderCacheEliteCount : s.powderCacheAmbushCount;
            for (var i = 0; i < count; i++)
            {
                var spawner = spawners[i % spawners.Length];
                if (spawner == null || spawner.anchor == null) continue;
                var def = _c.Enemy(spawner.anchor.enemyId);
                var slot = SpawnEnemy(def, spawner, 1f, 1f, 1f, 8f + i, EnemyCap, focus: false, ambient: false);
                if (slot < 0) continue;
                _enemies[slot].Cache = true;
                _cacheAlive++;
            }
            _cachePending = true;
            _cacheStartedCount++;
        }

        void OpenChest(int minRarity)
        {
            var item = RollChestItem(minRarity);
            if (item == null) return;
            _b.AddItem(item);
            _r.ChestsOpened++;
            if (_t <= 600f) _r.ItemsBy10++;
        }

        /// <summary>The V4 chest roll (ChestEconomy): a rarity by the weights (luck boosts uncommon and better), then an item of it.</summary>
        string RollChestItem(int minRarity)
        {
            var s = _c.Stage;
            var pool = _c.ChestPool;
            if (s == null || pool.Count == 0) return null;
            var weights = s.chestRarityWeights;
            var luckBoost = 1f + Mathf.Max(0f, _b.Stat(StatId.Luck)) / 100f;
            var stageBoost = 1f + s.chestRareBoostPerStageDepth * Mathf.Max(0, s.chestStageDepth - 1);
            var total = 0f;
            var w = new float[weights.Length];
            for (var i = 0; i < weights.Length; i++)
            {
                if (i < minRarity) continue;
                w[i] = weights[i] * (i >= 2 ? stageBoost : 1f) * (i >= 1 ? luckBoost : 1f);
                total += w[i];
            }
            var roll = (float)_rng.NextDouble() * total;
            var rarity = minRarity;
            for (var i = 0; i < w.Length; i++) { roll -= w[i]; if (roll <= 0f && w[i] > 0f) { rarity = i; break; } }
            // V4 ChestEconomy's V5 M3 rule: a chance of the missing evolution item of a carried, unevolved weapon at the
            // stage's minimum level (players cannot choose a chest's item).
            if (s.evolutionItemChance > 0f && minRarity == 0) // bought chests only (the cache chest rolls its own pool)
            {
                var wanted = new List<string>();
                foreach (var slot in _b.Weapons)
                {
                    if (slot.Evolution != null || slot.Level < RoguePirates.V4.Stage.ChestEconomy.EvolutionItemMinLevel(s, _c.Progression)) continue;
                    var evo = _c.EvolutionFor(slot.Id);
                    if (evo == null || string.IsNullOrEmpty(evo.requiredItemId) || _b.Items.HasItem(evo.requiredItemId) || wanted.Contains(evo.requiredItemId)) continue;
                    if (pool.Any(i => i.itemId == evo.requiredItemId)) wanted.Add(evo.requiredItemId);
                }
                if (wanted.Count > 0 && _rng.NextDouble() < s.evolutionItemChance) return wanted[_rng.Next(wanted.Count)];
            }
            for (var tier = rarity; tier >= 0; tier--)
            {
                var candidates = pool.Where(i => i.rarity == tier && IsItemEligible(i.itemId)).ToList();
                if (candidates.Count > 0) return candidates[_rng.Next(candidates.Count)].itemId;
            }
            return pool[0].itemId;
        }

        bool IsItemEligible(string itemId)
        {
            var evolution = _c.Evolutions.FirstOrDefault(e => e.requiredItemId == itemId);
            if (evolution == null || evolution.baseWeapon == null) return true;
            return _c.IsWeaponAvailable(evolution.baseWeapon.weaponId) || _b.Progression.OwnedWeapons.Contains(evolution.baseWeapon.weaponId);
        }

        void SummonBoss()
        {
            var phase = T?.phases.FirstOrDefault(p => p.isBoss);
            var spawner = phase?.spawner ?? _c.Stage?.bossSpawner;
            if (spawner == null || spawner.anchor == null) { _bossDone = true; return; }
            var def = _c.Enemy(spawner.anchor.enemyId);
            _bossIndex = SpawnEnemy(def, spawner, 1f, 1f, 1f, _k.spawnDistance, EnemyCap + 8, focus: false, ambient: false);
            _bossLevel = _b.Progression.Level;
            if (_bossIndex < 0) { _bossDone = true; return; }
            _enemies[_bossIndex].Boss = true;
            _r.BossSummonSeconds = _t;
            _r.BossSpawnHp = _enemies[_bossIndex].MaxHp;
        }

        // ---- Spawning ----------------------------------------------------------------------------------------------------

        float OrdinaryMultiplier(bool hp)
        {
            var d = _c.Curve;
            if (d == null || _t < d.ordinaryScalingDelaySeconds) return 1f;
            return (hp ? d.ordinaryHp : d.ordinaryDamage).Evaluate((_t - d.ordinaryScalingDelaySeconds) / 60f);
        }

        float LeverScale(float atMax) => LeverMath.Scale(_pressure, PressureMax, atMax);
        float PressureMax => _c.Curve != null ? _c.Curve.pressureMax : 1.35f;
        float PressureMin => _c.Curve != null ? _c.Curve.pressureMin : 0.85f;

        int FreeSlot()
        {
            for (var i = 0; i < _enemies.Length; i++) if (!_enemies[i].Active) return i;
            return -1;
        }

        /// <param name="ordinary">V4's ordinary-time HP and damage growth: every spawn made through the spawn executor's burst
        /// (bands, floor, hordes, elites, encounters, the boss, overtime); not a ring-placed focus enemy (the Reef Crab),
        /// which the scripted phases spawn directly.</param>
        int SpawnEnemy(SimEnemyDef def, EnemySpawnerConfig spawner, float hpMult, float dmgMult, float speedMult, float distance, int cap, bool focus, bool ambient, bool ordinary = true)
        {
            if (def == null || _alive >= cap) return -1;
            var slot = FreeSlot();
            if (slot < 0) return -1;
            var minute = _t / 60f;
            var hp = CurveEvaluator.EnemyHp(_c.Curve, def.BaseHp, minute, 1, 0) * (ordinary ? OrdinaryMultiplier(true) : 1f);
            var dmg = CurveEvaluator.EnemyDamage(_c.Curve, def.BaseDamage, minute, 1, 0) * (ordinary ? OrdinaryMultiplier(false) : 1f);
            var elite = spawner != null && spawner.elite != null && spawner.elite.isElite;
            var xpMult = 1f;
            var goldMult = 1f;
            // V4 EnemySimulation.SpawnFromProfile: a spawner's elite block applies only when the spawner is elite, and an
            // elite without an affix is Rich.
            if (elite)
            {
                hpMult *= spawner.elite.hpMultiplier;
                dmgMult *= spawner.elite.damageMultiplier;
                speedMult *= spawner.elite.speedMultiplier;
                xpMult = spawner.elite.xpMultiplier;
                goldMult = spawner.elite.goldMultiplier;
                hp *= AffixHp(spawner.elite.affix == ConfigEnums.EliteAffix.None ? ConfigEnums.EliteAffix.Rich : spawner.elite.affix);
            }
            if (Adaptive && ambient && !def.IsBoss)
            {
                var d = _c.Director;
                if (d.toughnessLever) { hpMult *= LeverScale(d.toughnessHpAtMax); dmgMult *= LeverScale(d.toughnessDamageAtMax); }
                if (d.capHpLever && _t - _capBlockedAt <= d.capBlockedWindowSeconds) hpMult *= LeverMath.CapHp(_pressure, PressureMax, d);
                if (d.eliteLever && !elite && _rng.NextDouble() < LeverMath.EliteChance(_pressure, PressureMax, d))
                {
                    elite = true;
                    hpMult *= d.eliteHpMultiplier * AffixHp(d.eliteAffix);
                    dmgMult *= d.eliteDamageMultiplier;
                }
            }
            if (Adaptive && focus && _c.Director.focusLever)
            {
                hpMult *= LeverScale(_c.Director.focusHpAtMax);
                dmgMult *= LeverScale(_c.Director.focusDamageAtMax);
            }
            var e = _c.Experiment;
            hp *= Mathf.Max(0.1f, hpMult) * (e != null ? Mathf.Max(0.1f, e.enemyHpMultiplier) : 1f);
            dmg *= Mathf.Max(0.1f, dmgMult) * (e != null ? e.enemyDamageMultiplier : 1f);
            _enemies[slot] = new Enemy
            {
                Active = true, Def = def, Hp = hp, MaxHp = hp, Damage = dmg, Speed = def.Speed * Mathf.Max(0.1f, speedMult),
                Dist = distance, AttackTimer = def.Cooldown * (float)_rng.NextDouble(), FirstHitAt = -1f,
                Elite = elite || focus, Focus = focus, Ambient = ambient, XpMultiplier = xpMult, GoldMultiplier = goldMult, ScheduledElite = -1,
            };
            _alive++;
            _pendingSpawns++;
            _minute.Spawns++;
            return slot;
        }

        /// <summary>V4 EnemySimulation.Spawn's affix HP (Armored ×1.8, Swift ×0.9, Rich ×1.25, others ×1).</summary>
        public static float AffixHp(ConfigEnums.EliteAffix affix) =>
            affix == ConfigEnums.EliteAffix.Armored ? 1.8f : affix == ConfigEnums.EliteAffix.Swift ? 0.9f : affix == ConfigEnums.EliteAffix.Rich ? 1.25f : 1f;

        bool InRelease(float t)
        {
            foreach (var r in _releases) if (t >= r.start && t < r.end) return true;
            return false;
        }

        void OpenRelease(float start, float seconds) => _releases.Add((start, start + seconds));

        bool InLull(float t)
        {
            var f = Flow;
            if (f == null) return false;
            for (var i = 0; i < f.hordes.Count; i++)
            {
                if (_hordesDone.Contains(i)) continue;
                var h = f.hordes[i];
                if (t >= h.timeSeconds - f.release.preHordeLullSeconds && t < h.timeSeconds) return true;
            }
            return false;
        }

        float ThreatRate => Adaptive && _c.Director.threatRateLever ? LeverMath.ThreatRate(_pressure, _c.Director) : 1f;
        float Density => (_c.Experiment != null ? Mathf.Max(0.1f, _c.Experiment.spawnDensityMultiplier) : 1f) * _b.Items.SpawnDensityMultiplier();

        TimelinePhase CurrentBand()
        {
            TimelinePhase best = null;
            foreach (var p in T.phases)
            {
                if (p.ratePerSecond <= 0f || p.isOvertime || p.trigger.kind != PhaseTriggerKind.Time || p.trigger.timeSeconds > _t) continue;
                if (best == null || p.trigger.timeSeconds >= best.trigger.timeSeconds) best = p;
            }
            return best;
        }

        float PulseMultiplier()
        {
            var m = 1f;
            foreach (var p in T.phases)
                if (p.ratePerSecond <= 0f && p.duration.kind == PhaseDurationKind.Seconds && p.trigger.kind == PhaseTriggerKind.Time
                    && _t >= p.trigger.timeSeconds && _t < p.trigger.timeSeconds + p.duration.seconds) m *= Mathf.Max(0f, p.rateMultiplier);
            return m;
        }

        void Spawn()
        {
            if (T == null) return;
            // Releases that ended are dropped.
            _releases.RemoveAll(r => r.end <= _t && r.start < _t);
            StartScriptedPhases();
            if (_arenaPhase != null)
            {
                if (ArenaCleared()) { _r.Arenas.Add((_arenaPhase, _arenaStartedAt, _t - _arenaStartedAt, _arenaHp)); OpenRelease(_t, Flow != null ? Flow.release.afterArenaSeconds : 0f); _arenaPhase = null; _ambientNext = _t; }
                else return; // arenas stop ambient spawns (and the flow)
            }
            if (_t > Deadline) { SpawnOvertime(); return; }
            if (_t < T.graceSeconds) return;

            var band = CurrentBand();
            var release = Flow != null && (InRelease(_t) || InLull(_t));
            var rate = Density * PulseMultiplier() * ThreatRate;
            // V4: a level-up during the boss fight resumes the run as Playing (R-84) unless the stage keeps the fight.
            if (BossFight && (_b.Progression.Level == _bossLevel || (_c.Stage != null && _c.Stage.bossFightSurvivesLevelUp))) rate *= T.bossAmbientRateMultiplier;
            if (Flow != null)
            {
                var wave = Flow.WaveAt(_t, out _);
                if (wave != null) rate *= wave.rateMultiplier;
                if (release) rate *= Flow.release.rateMultiplier;
            }
            // Ambient band: one spawn per interval (1 / rate), never faster than the global gap.
            if (band != null && _t >= _ambientNext && _t >= _gapReadyAt)
            {
                if (_alive >= AmbientCap) { _capBlockedAt = _t; _ambientNext = _t + T.spawnRetryIntervalSeconds; }
                else
                {
                    var count = band.burstCount > 0 ? band.burstCount : 1;
                    for (var i = 0; i < count; i++) SpawnFromBand(band, _k.spawnDistance);
                    _gapReadyAt = _t + T.globalMinSpawnGapSeconds;
                    _ambientNext = _t + Mathf.Max(T.globalMinSpawnGapSeconds, 1f / Mathf.Max(0.001f, band.ratePerSecond * Mathf.Max(0.1f, rate)));
                }
            }
            if (Flow != null && band != null) TickFlow(band, release);
        }

        void SpawnFromBand(TimelinePhase band, float distance, int cap = -1)
        {
            var def = PickFromPools(band) ?? (band.spawner != null && band.spawner.anchor != null ? _c.Enemy(band.spawner.anchor.enemyId) : null);
            SpawnEnemy(def, band.spawner, 1f, 1f, 1f, distance, cap > 0 ? cap : AmbientCap, focus: false, ambient: true);
        }

        SimEnemyDef PickFromPools(TimelinePhase band)
        {
            if (band.pools == null || band.pools.Count == 0) return null;
            var total = 0f;
            foreach (var pool in band.pools) if (pool.entries.Count > 0) total += pool.share;
            if (total <= 0f) return null;
            var roll = (float)_rng.NextDouble() * total;
            foreach (var pool in band.pools)
            {
                if (pool.entries.Count == 0) continue;
                roll -= pool.share;
                if (roll > 0f) continue;
                var weight = 0f;
                foreach (var e in pool.entries) weight += Mathf.Max(0f, e.weight);
                var pick = (float)_rng.NextDouble() * weight;
                foreach (var e in pool.entries)
                {
                    pick -= Mathf.Max(0f, e.weight);
                    if (pick <= 0f) return _c.Enemy(e);
                }
                return _c.Enemy(pool.entries[pool.entries.Count - 1]);
            }
            return null;
        }

        void TickFlow(TimelinePhase band, bool release)
        {
            var f = Flow;
            var wave = f.WaveAt(_t, out _);
            // Hordes (never during the boss fight; they wait for an open release window; skipped when more than 30 s late)
            for (var i = 0; i < f.hordes.Count; i++)
            {
                if (_hordesDone.Contains(i)) continue;
                var h = f.hordes[i];
                if (_t < h.timeSeconds) continue;
                if (_t > h.timeSeconds + 30f) { _hordesDone.Add(i); continue; }
                if (BossFight || InRelease(_t)) continue;
                _hordesDone.Add(i);
                var def = h.archetype != null ? _c.Enemy(h.archetype.enemyId) : null;
                var spawned = 0;
                for (var n = 0; n < h.count; n++)
                    // V4 FlowDirector.SpawnHorde: ordinary-time growth, no affix, no adaptive levers.
                    if (SpawnEnemy(def, null, h.hpMultiplier, h.damageMultiplier, h.speedMultiplier, Mathf.Max(h.distance, _k.spawnDistance * 0.9f),
                            AmbientCap + T.reservedEncounterSlots / 2, focus: false, ambient: false) >= 0) spawned++;
                if (spawned > 0) OpenRelease(_t + f.release.hordeReleaseDelaySeconds, f.release.afterHordeSeconds);
            }
            if (wave == null) return;
            // The floor: one top-up per global gap, at most maxTopUpPerTick per interval.
            var floor = wave.minAliveShare * AmbientCap * ThreatRate * Density;
            if (release) floor *= f.release.floorMultiplier;
            if (BossFight) floor *= f.release.bossFightFloorMultiplier;
            if (_t >= _topUpBudgetAt) { _topUpBudget = Mathf.Max(0, wave.maxTopUpPerTick); _topUpBudgetAt = _t + Mathf.Max(0.05f, wave.topUpIntervalSeconds); }
            while (_alive < Mathf.FloorToInt(floor) && _topUpBudget > 0 && _t >= _gapReadyAt && _alive < AmbientCap)
            {
                SpawnFromBand(band, _k.spawnDistance);
                _topUpBudget--;
                _gapReadyAt = _t + T.globalMinSpawnGapSeconds;
                break; // one per gap, and a step is shorter than a gap
            }
        }

        void StartScriptedPhases()
        {
            foreach (var p in T.phases)
            {
                if (p.trigger.kind != PhaseTriggerKind.Time || p.trigger.timeSeconds > _t || _phasesStarted.Contains(p.phaseId)) continue;
                if (p.ratePerSecond > 0f || p.isOvertime || p.isStorm) { _phasesStarted.Add(p.phaseId); continue; }
                if (p.focus != null && p.focus.enabled)
                {
                    if (_arenaPhase != null || BossFight)
                    {
                        // Held while another arena or the boss runs; dropped after its retry window.
                        if (_t > p.trigger.timeSeconds + p.focus.retryWindowSeconds) _phasesStarted.Add(p.phaseId);
                        continue;
                    }
                    _phasesStarted.Add(p.phaseId);
                    StartArena(p);
                }
                else if (p.spawner != null && p.spawner.anchor != null && p.duration.kind == PhaseDurationKind.Seconds)
                {
                    _phasesStarted.Add(p.phaseId);
                    var def = _c.Enemy(p.spawner.anchor.enemyId);
                    for (var i = 0; i < Mathf.Max(1, p.spawner.timing.spawnCount); i++)
                        SpawnEnemy(def, p.spawner, 1f, 1f, 1f, _k.spawnDistance, AmbientCap, focus: false, ambient: true);
                }
                else _phasesStarted.Add(p.phaseId);
            }
        }

        void StartArena(TimelinePhase p)
        {
            var f = p.focus;
            var archetypeId = f.variant != null ? f.variant.variantId : f.archetype != null ? f.archetype.enemyId : p.spawner != null && p.spawner.anchor != null ? p.spawner.anchor.enemyId : null;
            var def = _c.Enemy(archetypeId);
            var spawner = f.placement == FocusPlacement.SpawnerProfile ? p.spawner : null;
            var hp = f.hpMultiplier * (spawner == null ? AffixHp(f.affix) : 1f);
            var slot = SpawnEnemy(def, spawner, hp, f.damageMultiplier, f.speedMultiplier, f.ringDistance > 0f ? f.ringDistance : _k.spawnDistance, EnemyCap, focus: true, ambient: false,
                ordinary: spawner != null);
            if (slot < 0) return;
            _enemies[slot].ScheduledElite = f.scheduledEliteIndex;
            _enemies[slot].XpMultiplier = Mathf.Max(_enemies[slot].XpMultiplier, 1f);
            _arenaPhase = p.phaseId;
            _arenaStartedAt = _t;
            _arenaHp = _enemies[slot].MaxHp;
        }

        bool ArenaCleared()
        {
            for (var i = 0; i < _enemies.Length; i++) if (_enemies[i].Active && _enemies[i].Focus) return false;
            return true;
        }

        // V4 OvertimePhase's formulas (internal to the runtime; pinned against it by V5BalanceM2Tests).
        public static float OvertimeInterval(WaveTimelineConfig t, float elapsed)
        {
            var o = t.overtime;
            var steps = (int)Math.Floor(Mathf.Max(0f, elapsed - t.stageDeadlineSeconds) / o.pressureStepSeconds);
            return (float)Math.Max(o.intervalFloorSeconds, o.initialSpawnIntervalSeconds * Math.Pow(o.intervalDecayPerStep, steps));
        }

        public static float OvertimeHpMultiplier(WaveTimelineConfig t, DifficultyCurveConfig d, float elapsed) =>
            d.overtimeHpMultiplierBase * Mathf.Pow(2f, Mathf.Max(0f, (elapsed - t.stageDeadlineSeconds) / 60f) * d.overtimeHpMultiplierExponentPerMinute);

        public static float OvertimeDamageMultiplier(WaveTimelineConfig t, DifficultyCurveConfig d, float elapsed) =>
            1f + Mathf.Max(0f, (elapsed - t.stageDeadlineSeconds) / 60f) * d.overtimeDamageBonusPerMinute;

        public static float OvertimeStatRamp(WaveTimelineConfig t, float elapsed)
        {
            var o = t.overtime;
            var k = o.spawnStatRampSeconds > 0f ? Mathf.Clamp01((elapsed - t.stageDeadlineSeconds) / o.spawnStatRampSeconds) : 1f;
            return Mathf.Lerp(o.spawnStatRampStart, o.spawnStatRampEnd, k);
        }

        void SpawnOvertime()
        {
            var phase = T.phases.FirstOrDefault(p => p.isOvertime);
            if (phase == null || _t < _overtimeNext) return;
            var o = T.overtime;
            _overtimeNext = _t + OvertimeInterval(T, _t);
            var minutes = Mathf.Max(0f, _t - Deadline) / 60f;
            var pursuer = minutes < 2f ? o.pursuerWeightFirstTwoMinutes : o.pursuerWeightAfterTwoMinutes;
            var spawner = _rng.NextDouble() < pursuer || phase.alternateSpawner == null ? phase.spawner : phase.alternateSpawner;
            if (spawner == null || spawner.anchor == null) return;
            var ramp = OvertimeStatRamp(T, _t);
            var hp = OvertimeHpMultiplier(T, _c.Curve, _t) * ramp;
            var dmg = OvertimeDamageMultiplier(T, _c.Curve, _t) * ramp;
            if (_alive >= AmbientCap) { _capBlockedAt = _t; return; }
            // Overtime spawns are not ambient in V4 (the adaptive levers leave them alone).
            SpawnEnemy(_c.Enemy(spawner.anchor.enemyId), spawner, hp, dmg, 1f, _k.spawnDistance, AmbientCap, focus: false, ambient: false);
        }

        // ---- Enemies close in and attack ---------------------------------------------------------------------------------

        /// <summary>How fast an enemy closes on the ship: at most its speed; inside the engage distance the ship moves away.</summary>
        float Closing(float speed, float dist, float shipSpeed)
        {
            if (dist > _p.engageDistance) return speed * _k.farClosingShare;
            return Mathf.Max(speed * _k.minClosingShare, speed - shipSpeed * _k.movementRetreat[(int)_p.movement]);
        }

        void MoveAndAttack()
        {
            var slow = _t < _deadCalmUntil ? 1f - (_c.Powerups != null ? _c.Powerups.deadCalmSlowFraction : 0.3f) : 1f;
            var shipSpeed = _b.Stat(StatId.MoveSpeed) * _c.ShipSpeedMultiplier;
            var contacts = 0;
            // I-frames: the ship takes at most one hit per ContactIFrame seconds.
            _hitBudget = Mathf.Min(_hitBudget + _dt / GameConstants.ContactIFrame, 2f);
            var incoming = _b.IncomingDamageMultiplier;
            for (var i = 0; i < _enemies.Length; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.Active) continue;
                var ranged = e.Def.Attack == SimAttack.Ranged || e.Def.Attack == SimAttack.Area;
                if (e.Def.Attack == SimAttack.None) { if (e.Dist > _k.contactDistance * 3f) e.Dist = Mathf.Max(_k.contactDistance * 3f, e.Dist - Closing(e.Speed * slow, e.Dist, shipSpeed) * _dt); continue; }
                var hold = ranged ? Mathf.Max(_k.contactDistance, e.Def.PreferredRange * _k.rangedHoldShare) : _k.contactDistance;
                if (e.Dist > hold) e.Dist = Mathf.Max(hold, e.Dist - Closing(e.Speed * slow, e.Dist, shipSpeed) * _dt);
                e.AttackTimer -= _dt;
                if (e.AttackTimer > 0f) continue;
                var reached = e.Dist <= hold + 0.01f;
                if (!reached) continue;
                float chance;
                var damage = e.Damage;
                switch (e.Def.Attack)
                {
                    case SimAttack.Ranged:
                        chance = _k.rangedAccuracy * (1f - _p.dodgeSkill);
                        damage *= Mathf.Max(1f, e.Def.Shots * _k.volleyHitShare);
                        break;
                    case SimAttack.Area:
                        chance = _k.areaAccuracy * (1f - _p.dodgeSkill);
                        break;
                    case SimAttack.Boss:
                        chance = _k.bossAccuracy * (1f - _p.dodgeSkill);
                        e.AttackTimer = _k.bossAttackCycleSeconds;
                        if (Chance(chance)) Hit(damage * incoming, e.Def.Id);
                        continue;
                    case SimAttack.Suicide:
                        if (contacts >= _k.maxContacts) continue;
                        contacts++;
                        if (Chance(_k.hazardAccuracy * (1f - _p.dodgeSkill))) Hit(damage * incoming, e.Def.Id);
                        Remove(i, false);
                        continue;
                    default:
                        if (contacts >= _k.maxContacts) continue;
                        contacts++;
                        chance = (e.Elite ? _k.eliteContactShare : _k.contactHitShare) * (1f - (e.Def.Telegraphed ? _p.dodgeSkill : _p.contactAvoidance));
                        break;
                }
                e.AttackTimer = e.Def.Cooldown;
                if (Chance(chance)) Hit(damage * incoming, e.Def.Id);
            }
        }

        bool Chance(float p) => _rng.NextDouble() < p;

        void Hit(float amount, string source)
        {
            if (_hitBudget < 1f || amount <= 0f) return;
            _hitBudget -= 1f;
            HitTaken?.Invoke(_t, amount, source);
            var before = _b.Hull;
            _b.Damage(amount);
            var lost = before - _b.Hull;
            var key = source ?? "?";
            _r.DamageBySource[key] = (_r.DamageBySource.TryGetValue(key, out var sum) ? sum : 0f) + lost;
            _minute.DamageTaken += lost;
            _r.DamageTaken += lost;
            if (_b.MaxHull > 0f) _pendingHullLost += lost / _b.MaxHull;
        }

        // ---- Weapons -----------------------------------------------------------------------------------------------------

        /// <summary>Damage one hit of this weapon deals now (crit rolled), before elite and overkill.</summary>
        float HitDamage(WeaponSlot w, bool elite)
        {
            var m = _b.Modifiers;
            var damage = w.Config.baseDamage * WeaponStatResolver.LevelMult(w) * Mathf.Max(0f, m.GlobalDamage + m.CaptainAdditiveDamage)
                * m.ItemGlobalDamageMultiplier * m.CaptainWeaponMultiplier(w.Id);
            if (w.Evolution != null && _c.Director != null) damage *= Mathf.Max(1f, _c.Director.evolutionOffenseMultiplier);
            if (_t < _battleSignalUntil && _c.Powerups != null) damage *= _c.Powerups.battleSignalDamageMultiplier;
            if (Chance(Mathf.Clamp01(m.CritChance + m.ItemCritChanceBonus))) damage *= Mathf.Max(1f, m.CritDamage);
            if (elite) damage *= Mathf.Max(0f, m.EliteDamage);
            return damage;
        }

        float Coverage(WeaponSlot w)
        {
            var c = _c.Director != null ? _c.Director.behaviorCoverage : null;
            var b = (int)w.Config.behavior;
            return c != null && b < c.Length ? Mathf.Max(0.1f, c[b]) : 1f;
        }

        float Range(WeaponSlot w) => w.Config.range * w.SizeMult * _b.Modifiers.Size;

        void FireWeapons()
        {
            var slots = _b.Weapons;
            var m = _b.Modifiers;
            for (var s = 0; s < slots.Count; s++)
            {
                var w = slots[s];
                _weaponTimers.TryGetValue(w, out var timer);
                timer -= _dt;
                if (timer > 0f) { _weaponTimers[w] = timer; continue; }
                var cooldown = Mathf.Max(0.05f, WeaponStatResolver.Cooldown(w, m, m.ItemAttackSpeedMultiplier));
                timer += cooldown;
                if (timer < -cooldown) timer = 0f; // no backlog when nothing was in range
                _weaponTimers[w] = timer;
                FireOnce(w);
            }
        }

        /// <summary>One volley: quantity × coverage hits on the nearest enemies in range (a hit's damage past what kills is wasted).</summary>
        void FireOnce(WeaponSlot w)
        {
            var range = Range(w);
            var hits = WeaponStatResolver.ProjectileQuantity(w, _b.Modifiers) * Coverage(w) * Mathf.Clamp01(_k.hitEfficiency);
            var whole = Mathf.FloorToInt(hits);
            if (_rng.NextDouble() < hits - whole) whole++;
            for (var h = 0; h < whole; h++)
            {
                var target = Nearest(range, h);
                if (target < 0) return;
                ref var e = ref _enemies[target];
                var damage = HitDamage(w, e.Elite || e.Boss);
                if (e.FirstHitAt < 0f) e.FirstHitAt = _t;
                e.Hp -= damage;
                if (e.Hp <= 0f) Remove(target, true);
            }
        }

        readonly int[] _nearIndex = new int[4];
        readonly float[] _nearDist = new float[4];

        /// <summary>
        /// The nearest live enemy in range; later hits of a volley spread over the few nearest (the n-th hit goes to one of
        /// the n+1 nearest, at most the 4th), as fans, pierce and splash do.
        /// </summary>
        int Nearest(float range, int skip)
        {
            var count = 0;
            for (var i = 0; i < _enemies.Length; i++)
            {
                ref var e = ref _enemies[i];
                if (!e.Active || e.Dist > range) continue;
                // Insert into the (up to) 4 nearest, kept sorted by distance.
                var at = count;
                while (at > 0 && _nearDist[at - 1] > e.Dist)
                {
                    if (at < 4) { _nearDist[at] = _nearDist[at - 1]; _nearIndex[at] = _nearIndex[at - 1]; }
                    at--;
                }
                if (at < 4) { _nearDist[at] = e.Dist; _nearIndex[at] = i; }
                if (count < 4) count++;
            }
            if (count == 0) return -1;
            var pick = skip > 0 ? _rng.Next(Mathf.Min(skip + 1, count)) : 0;
            return _nearIndex[pick];
        }

        void Remove(int i, bool killed)
        {
            ref var e = ref _enemies[i];
            if (!e.Active) return;
            e.Active = false;
            _alive--;
            if (e.Cache) _cacheAlive--;
            if (!killed) return;
            _pendingKills++;
            _r.Kills++;
            _minute.Kills++;
            if (_t <= Deadline) Increment(_killsPerMinute, (int)(_t / 60f));
            else Increment(_killsPerOvertimeMinute, (int)((_t - Deadline) / 60f));
            if (e.FirstHitAt >= 0f)
            {
                if (e.Focus && e.ScheduledElite >= 0) { _r.EliteTtkSum += _t - e.FirstHitAt; _r.EliteTtkCount++; }
                else if (e.Focus) { _r.MinibossTtkSum += _t - e.FirstHitAt; _r.MinibossTtkCount++; }
                else if (!e.Boss && !e.Elite && e.Def.BaseHp <= _fodderHp) { _r.FodderTtkSum += _t - e.FirstHitAt; _r.FodderTtkCount++; }
            }
            _b.Items.NotifyKill();
            // XP: the share this player picks up (greed and pickup radius); Magnetic Tide brings the rest later.
            var xp = e.Def.Xp * e.XpMultiplier;
            var share = Mathf.Clamp01(_k.xpBaseShare + (1f - _k.xpBaseShare) * _p.pickupGreed) * Mathf.Clamp(_b.PickupRadius / GameConstants.DefaultPickupRadius, 0.8f, 1.25f);
            share = Mathf.Clamp01(share);
            _uncollectedXp += xp * (1f - share);
            GrantXp(xp * share);
            // Gold (the stage's kill gold) and health barrels
            var s = _c.Stage;
            var gold = 0;
            if (e.ScheduledElite >= 0 && s != null) gold = s.killGold.scheduledEliteGoldAmount;
            else if (e.Boss) gold = e.Def.Gold;
            else if (s != null) gold = e.Elite ? s.killGold.specialistEnemyGoldAmount : _rng.NextDouble() < s.killGold.ordinaryEnemyGoldChance ? s.killGold.ordinaryEnemyGoldAmount : 0;
            gold = Mathf.RoundToInt(gold * e.GoldMultiplier * _b.Stat(StatId.GoldGain));
            _b.Gold += gold;
            _r.GoldEarned += gold;
            _minute.Gold += gold;
            if (_c.Pickups != null && _rng.NextDouble() < _c.Pickups.healthDropChance
                && _rng.NextDouble() < Mathf.Clamp01(_k.barrelBaseShare + (1f - _k.barrelBaseShare) * _p.pickupGreed))
            {
                var before = _b.Hull;
                _b.Heal(_c.Pickups.healthDropValue);
                _minute.Healed += _b.Hull - before;
            }
            RollPowerup(e.Elite || e.Boss);
            if (e.ScheduledElite >= 0) _r.ElitesSunk++;
            if (e.Boss)
            {
                _r.BossKillSeconds = _t;
                _bossDone = true;
                _bossIndex = -1;
                if (Flow != null) OpenRelease(_t, Flow.release.afterBossKillSeconds);
            }
        }

        static void Increment(Dictionary<int, int> d, int key) => d[key] = d.TryGetValue(key, out var n) ? n + 1 : 1;

        void GrantXp(float amount)
        {
            // As the game: × the XP gain stat per pickup, then the pace, the fractions carried (review: gain before the pace).
            _xpCarry += amount * _b.XpGain * _c.XpPickupMultiplier;
            var whole = Mathf.FloorToInt(_xpCarry);
            if (whole <= 0) return;
            _xpCarry -= whole;
            var level = _b.Progression.Level;
            _b.GrantScaledXp(whole);
            if (_b.Progression.Level != level) _r.Refreshes = _b.Refreshes;
        }

        void RollPowerup(bool elite)
        {
            var c = _c.Powerups;
            if (c == null || _powerupsDropped >= c.maxPerStage) return;
            var forced = elite && c.elitesAlwaysDrop;
            if (!forced && (_t - _lastPowerupAt < c.minIntervalSeconds || _rng.NextDouble() >= c.dropChancePerKill)) return;
            if (_rng.NextDouble() > Mathf.Clamp01(0.5f + 0.5f * _p.pickupGreed)) return; // not picked up
            _powerupsDropped++;
            _lastPowerupAt = _t;
            var total = 0f;
            foreach (var entry in c.pool) total += Mathf.Max(0f, entry.weight);
            var roll = (float)_rng.NextDouble() * total;
            var kind = PowerupKind.RepairSupplies;
            foreach (var entry in c.pool) { roll -= Mathf.Max(0f, entry.weight); if (roll <= 0f) { kind = entry.kind; break; } }
            switch (kind)
            {
                case PowerupKind.RepairSupplies: _b.Heal(_b.MaxHull * c.repairMaxHullFraction); break;
                case PowerupKind.BattleSignal: _battleSignalUntil = _t + c.battleSignalSeconds; break;
                case PowerupKind.PowderRush: _battleSignalUntil = _t + c.powderRushSeconds; break;
                case PowerupKind.DeadCalm: _deadCalmUntil = _t + c.deadCalmSeconds; break;
                case PowerupKind.MagneticTide:
                    var xp = _uncollectedXp;
                    _uncollectedXp = 0f;
                    GrantXp(xp);
                    break;
            }
        }

        // ---- Build ticks (items, Captain active) -----------------------------------------------------------------------

        void TickBuild()
        {
            _b.Items.Tick(_dt);
            var pressed = false;
            if (_b.Captain.IsReady && _c.CaptainActivesEnabled && _t >= _activeDecisionAt)
            {
                switch (_p.activeUse)
                {
                    case PersonalityActive.OnCooldown: pressed = _alive > 0; break;
                    case PersonalityActive.Danger: pressed = _b.HullShare < 0.4f || BossFight; break;
                    case PersonalityActive.Sometimes: pressed = _rng.NextDouble() < 0.5; _activeDecisionAt = _t + 3f; break;
                    case PersonalityActive.Forgets: pressed = _rng.NextDouble() < 0.1; _activeDecisionAt = _t + 5f; break;
                }
            }
            _b.Captain.HasWeaponTarget = () => AnyInRange();
            var uses = _b.Captain.ActiveUses;
            _b.Captain.Tick(_dt, pressed);
            if (_b.Captain.ActiveUses > uses && _b.Captain.Config?.active != null && _b.Captain.Config.active.fireAllWeapons)
                foreach (var w in _b.Weapons.ToArray()) FireOnce(w); // kills may level up and re-sync the slots
            _b.SyncSlots();
        }

        bool AnyInRange()
        {
            var range = 0f;
            foreach (var w in _b.Weapons) range = Mathf.Max(range, Range(w));
            for (var i = 0; i < _enemies.Length; i++) if (_enemies[i].Active && _enemies[i].Dist <= range) return true;
            return false;
        }

        // ---- The director (V4's sample loop, on the simulator's clock) -----------------------------------------------

        void SampleDirector()
        {
            var d = _c.Director;
            if (d == null || _c.Mode == DirectorMode.Off) return;
            _sampleTimer += _dt;
            while (_sampleTimer + 0.0001f >= d.sampleIntervalSeconds)
            {
                _sampleTimer = Mathf.Max(0f, _sampleTimer - d.sampleIntervalSeconds);
                _window.Enqueue((_t, _pendingKills, _pendingSpawns, _pendingHullLost));
                _pendingKills = _pendingSpawns = 0;
                _pendingHullLost = 0f;
                while (_window.Count > 0 && _window.Peek().time <= _t - d.performanceWindowSeconds) _window.Dequeue();
                int kills = 0, spawns = 0;
                var lost = 0f;
                foreach (var w in _window) { kills += w.kills; spawns += w.spawns; lost += w.hullLost; }
                var offense = PlayerPowerIndex.Offense(_b, d, out _, DifficultyDirector.HitCap(d, _c.Curve, _t));
                if (_r.Offense0 <= 0f) _r.Offense0 = offense;
                var growth = DifficultyDirector.ExpectedGrowth(d, _c.Curve, _t);
                if (_offense0 <= 0f && offense > 0f) _offense0 = offense / Mathf.Max(0.0001f, growth);
                var expected = _offense0 * growth;
                var sample = new DirectorSample
                {
                    OffenseNorm = expected > 0f ? offense / expected : 1f,
                    DefenseNorm = PlayerPowerIndex.Defense(_b),
                    Performance = DirectorMath.Performance(kills, spawns, lost, Mathf.Min(d.performanceWindowSeconds, Mathf.Max(1f, _t)), d),
                };
                var floor = _t >= d.pressureFloorAfterSeconds ? Mathf.Clamp(d.pressureFloor, PressureMin, PressureMax) : PressureMin;
                _pressure = DirectorMath.Step(_pressure, DirectorMath.Target(sample, d, floor, PressureMax), d.sampleIntervalSeconds, d, PressureMin, PressureMax);
                _pressure = Mathf.Max(_pressure, floor);
                _minute.Offense = offense;
            }
        }

        // ---- Per-minute samples and the end ------------------------------------------------------------------------------

        /// <summary>Single-target damage per second of the build (every weapon on one enemy, no coverage).</summary>
        public float SingleTargetDps()
        {
            var m = _b.Modifiers;
            var total = 0f;
            foreach (var w in _b.Weapons)
            {
                var damage = w.Config.baseDamage * WeaponStatResolver.LevelMult(w) * Mathf.Max(0f, m.GlobalDamage + m.CaptainAdditiveDamage) * m.ItemGlobalDamageMultiplier * m.CaptainWeaponMultiplier(w.Id);
                if (w.Evolution != null && _c.Director != null) damage *= Mathf.Max(1f, _c.Director.evolutionOffenseMultiplier);
                var crit = 1f + Mathf.Clamp01(m.CritChance + m.ItemCritChanceBonus) * Mathf.Max(0f, m.CritDamage - 1f);
                total += damage * crit * WeaponStatResolver.ProjectileQuantity(w, m) / Mathf.Max(0.05f, WeaponStatResolver.Cooldown(w, m, m.ItemAttackSpeedMultiplier));
            }
            return total;
        }

        void Bookkeep()
        {
            _aliveSum += _alive;
            _aliveSamples++;
            var minuteIndex = (int)((_t + _dt) / 60f);
            if (minuteIndex == _minuteIndex) return;
            CloseMinute();
            _minuteIndex = minuteIndex;
            if (_minuteIndex == 5) _r.LevelAt5 = _b.Progression.Level;
            if (_minuteIndex == 10)
            {
                _r.LevelAt10 = _b.Progression.Level;
                if (_c.Director != null) _r.OffenseAt10 = PlayerPowerIndex.Offense(_b, _c.Director, out _, DifficultyDirector.HitCap(_c.Director, _c.Curve, _t));
            }
        }

        void CloseMinute()
        {
            _minute.AliveAverage = _aliveSamples > 0 ? _aliveSum / _aliveSamples : 0f;
            _minute.Level = _b.Progression.Level;
            _minute.HullShareAtEnd = _b.HullShare;
            _minute.SingleTargetDps = SingleTargetDps();
            _minute.Pressure = _pressure;
            _minute.Reached = true;
            _r.Minutes.Add(_minute);
            _minute = default;
            _aliveSum = 0f;
            _aliveSamples = 0;
        }

        void Finish(bool died, string reason)
        {
            _r.EndSeconds = _t;
            _r.Died = died;
            _r.EndReason = reason;
            _r.FinalLevel = _b.Progression.Level;
            _r.Refreshes = _b.Refreshes;
            _r.LevelUpTimes.AddRange(_b.LevelUpTimes);
            _r.Evolutions.AddRange(_b.Evolutions);
            _r.Weapons.AddRange(_b.Progression.OwnedWeapons);
            _r.Logbooks.AddRange(_b.Progression.OwnedLogbooks);
            _r.Items.AddRange(_b.Items.Items);
            _r.Rum = SettleRum();
        }

        /// <summary>The run's Rum with the reward config's rows (the RewardModule's rules, from the simulator's counts).</summary>
        int SettleRum()
        {
            var c = _c.Rewards;
            if (c == null) return 0;
            var closedMinutes = (int)(_t / 60f);
            var closedOvertime = _t > Deadline ? (int)((_t - Deadline) / 60f) : 0;
            var total = 0;
            foreach (var row in c.rows)
            {
                if (row == null || !row.enabled) continue;
                var candidates = 0;
                switch (row.source)
                {
                    case RumSource.QualifyingMinute:
                        for (var m = 0; m < closedMinutes; m++)
                            if ((m + 1) * 60f <= Deadline && _killsPerMinute.TryGetValue(m, out var k) && k >= row.minKills) candidates++;
                        break;
                    case RumSource.OvertimeMinute:
                        for (var m = 0; m < closedOvertime; m++)
                            if (_killsPerOvertimeMinute.TryGetValue(m, out var k) && k >= row.minKills) candidates++;
                        break;
                    case RumSource.SpecialLandmark: candidates = _r.LandmarksUsed; break;
                    case RumSource.ScheduledElite: candidates = _r.ElitesSunk; break;
                    case RumSource.BossVictory: candidates = _r.BossWon ? 1 : 0; break;
                    case RumSource.ChestOpened: candidates = _r.ChestsOpened; break;
                    case RumSource.RunGoldBanked:
                        total += RoguePirates.V4.Rewards.RewardModule.GoldRum(_b.Gold, row.maxRum, row.goldForHalf);
                        continue;
                }
                var credits = candidates;
                if (row.maxCredits > 0) credits = Mathf.Min(credits, row.maxCredits);
                if (row.maxRum > 0 && row.rum > 0) credits = Mathf.Min(credits, row.maxRum / row.rum);
                var rum = credits * Mathf.Max(0, row.rum);
                if (row.maxRum > 0) rum = Mathf.Min(rum, row.maxRum);
                total += rum;
            }
            return total;
        }
    }
}

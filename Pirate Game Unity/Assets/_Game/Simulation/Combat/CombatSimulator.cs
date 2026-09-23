using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using RoguePirates.Simulation.Balance;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Simulation
{
    public sealed class CombatSimulator
    {
        const int MaxPressureEnemies = 28;
        readonly PauseReasonSet _pause = new();
        readonly SimClock _clock = new();
        readonly LastRumSystem _lastRum = new();
        readonly WeaponSystem _weapon = new();
        readonly List<WeaponSystem> _loadoutWeapons = new();
        readonly List<SimActor> _actors = new();
        readonly List<SimProjectile> _projectiles = new();
        readonly List<SimEvent> _events = new();
        readonly SimRng _mapRng;
        readonly SimRng _combatRng;
        SimRng _lootRng;

        int _nextActorId = 1;
        int _nextProjectileId = 1;
        SimActor _player;
        bool _objectiveSecured;
        SimRunState _state = SimRunState.Playing;
        float _xp;
        int _level = 1;
        int _kills;
        CombatExperimentSimConfig _experimentConfig = CombatExperimentSimConfig.FromProfileId("D_FULL_COMBAT_V2");
        float _spawnBudget;
        int _peakEnemyCount;
        BalanceLabScenario _labScenario;
        float _poiHealRemaining;
        float _nextPoiHealAtSeconds = float.PositiveInfinity;
        int _nextRepairWreckIndex;
        int _nextMerchantChestIndex;
        readonly Dictionary<string, int> _weaponLevels = new();
        int _gold;
        int _merchantPurchasesCompleted;
        bool _evolutionApplied;
        int _poiInteractionsResolved;
        float _objectiveSecuredSeconds = -1f;
        bool _bossSpawned;
        float _waveSpawnTimer;
        int _waveCompositionCursor;

        public int Seed { get; }
        public int PeakEnemyCount => _peakEnemyCount;
        public IReadOnlyList<SimEvent> Events => _events;
        public SimRunState State => _state;
        public bool ObjectiveSecured => _objectiveSecured;
        public int Tick => _clock.Tick;
        public float SimSeconds => _clock.SimSeconds;
        public int Kills => _kills;
        public int Level => _level;
        public int Gold => _gold;
        public int MerchantPurchasesCompleted => _merchantPurchasesCompleted;
        public float PlayerHullFraction => _player.MaxHull.Raw <= 0 ? 0f : _player.Hull.ToFloat() / _player.MaxHull.ToFloat();
        public int LivingEnemyCount => CountLivingEnemies();
        public float ObjectiveSecuredSeconds => _objectiveSecuredSeconds;

        public void ApplyExperimentConfig(CombatExperimentSimConfig config)
        {
            _experimentConfig = config ?? CombatExperimentSimConfig.FromProfileId("D_FULL_COMBAT_V2");
            _weapon.SetDamageMultiplier(_experimentConfig.WeaponDamageMultiplier);
        }

        /// <summary>Applies a non-persistent Balance Lab scenario to this isolated simulation.</summary>
        public void ApplyLabScenario(BalanceLabScenario scenario)
        {
            _labScenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _labScenario.Normalize();
            _experimentConfig = new CombatExperimentSimConfig
            {
                ProfileId = "BALANCE_LAB",
                WeaponSlotCap = _labScenario.WeaponSlotCap,
                DirectorV2Enabled = _labScenario.DirectorEnabled,
                DirectionalTargetingEnabled = true,
                SpawnDensityMultiplier = _labScenario.SpawnDensityMultiplier,
                EnemyHpMultiplier = _labScenario.EnemyHullMultiplier,
                EnemyDamageMultiplier = _labScenario.EnemyDamageMultiplier
            };
            var poiDamage = !_labScenario.EnablePois ? 1f :
                MathF.Pow(1.10f * _labScenario.PoiDamageMultiplier, _labScenario.SignalBuoysCharged) *
                MathF.Pow(1.05f * _labScenario.PoiDamageMultiplier, _labScenario.PowderCachesCleared);
            var loadoutDamage = _labScenario.WeaponDamageMultiplier * _labScenario.ItemDamageMultiplier *
                _labScenario.WeaponCount * _labScenario.WeaponPatternTargetMultiplier * poiDamage;
            _loadoutWeapons.Clear();
            _gold = _labScenario.StartingGold;
            if (_labScenario.WeaponLoadout.Count > 0)
            {
                // Each equipped weapon keeps its own cadence and targeting, unlike the legacy aggregate proxy.
                for (var i = 0; i < _labScenario.WeaponLoadout.Count && i < _labScenario.WeaponSlotCap; i++)
                {
                    var entry = _labScenario.WeaponLoadout[i];
                    if (entry == null) continue;
                    var system = new WeaponSystem();
                    system.SetDamageMultiplier(_labScenario.WeaponDamageMultiplier * _labScenario.ItemDamageMultiplier * poiDamage);
                    system.SetPatternTargetMultiplier(entry.TargetMultiplier);
                    system.SetAuthoredWeapon(entry.Damage, entry.Cooldown, entry.Range, entry.ProjectileSpeed);
                    system.ApplyLabTuning(_labScenario.WeaponCooldownMultiplier, _labScenario.ProjectileSpeedMultiplier,
                        _labScenario.ProjectileRadiusMultiplier * entry.SizeMultiplier);
                    _loadoutWeapons.Add(system);
                }
            }
            _weapon.SetDamageMultiplier(loadoutDamage);
            _weapon.SetAuthoredWeapon(
                _labScenario.WeaponBaseDamage,
                _labScenario.WeaponBaseCooldown,
                _labScenario.WeaponRange,
                _labScenario.WeaponProjectileSpeed);
            _weapon.ApplyLabTuning(
                // The fast simulator represents a multi-weapon build as combined output cadence.
                // This keeps the expected loadout field from being a damage-only, misleading proxy.
                _labScenario.WeaponCooldownMultiplier / _labScenario.WeaponCount,
                _labScenario.ProjectileSpeedMultiplier,
                _labScenario.ProjectileRadiusMultiplier);
            _player.MaxHull = SimFixed.FromFloat(SimConstants.DefaultHull * _labScenario.PlayerHullMultiplier * _labScenario.CaptainHullMultiplier);
            _player.Hull = _player.MaxHull;
            if (_labScenario.EnablePois)
            {
                _poiHealRemaining = _player.MaxHull.ToFloat() * 0.30f * _labScenario.RepairWrecksUsed * _labScenario.PoiHealingMultiplier;
                _nextRepairWreckIndex = 0;
                _nextMerchantChestIndex = 0;
                _nextPoiHealAtSeconds = _labScenario.StageProfile != null && _labScenario.RepairWrecksUsed > 0
                    ? SaltwindLabRouteParityLogic.RepairWreckCheckpointSeconds(
                        0,
                        _labScenario.RepairWrecksUsed,
                        _labScenario.DurationSeconds)
                    : float.PositiveInfinity;
            }

            _weaponLevels.Clear();
            for (var i = 0; i < _labScenario.WeaponLoadout.Count; i++)
            {
                var id = _labScenario.WeaponLoadout[i]?.Id;
                if (string.IsNullOrEmpty(id)) continue;
                _weaponLevels[id] = 1;
            }

            _bossSpawned = false;
            _objectiveSecured = false;
            _objectiveSecuredSeconds = -1f;
            _state = SimRunState.Playing;
        }

        public CombatSimulator(int seed)
        {
            Seed = seed;
            _mapRng = SimRng.CreateStream(seed, 73856093);
            _lootRng = SimRng.CreateStream(seed, 19349663);
            _combatRng = SimRng.CreateStream(seed, 50331653);

            _player = CreatePlayer();
            _actors.Add(_player);
            SpawnShark(new SimVec2(SimFixed.FromInt(8), SimFixed.FromInt(0)));
        }

        SimActor CreatePlayer()
        {
            return new SimActor
            {
                Id = _nextActorId++,
                IsPlayer = true,
                Position = SimVec2.Zero,
                Hull = SimFixed.FromFloat(SimConstants.DefaultHull),
                MaxHull = SimFixed.FromFloat(SimConstants.DefaultHull)
            };
        }

        void SpawnShark(SimVec2 pos, string forcedEnemyId = null)
        {
            var minute = _clock.SimSeconds / 60f;
            var rosterEntry = string.IsNullOrEmpty(forcedEnemyId) ? PickRosterEntry() : FindRosterEntry(forcedEnemyId);
            var baseHull = rosterEntry?.Hull ?? _labScenario?.EnemyBaseHull ?? SimConstants.SharkBaseHp;
            var hp = SimMath.EnemyHP(baseHull, minute, 1, 0).ToFloat()
                     * MathF.Max(0.1f, _experimentConfig.EnemyHpMultiplier);
            _actors.Add(new SimActor
            {
                Id = _nextActorId++,
                ArchetypeId = rosterEntry?.Id ?? _labScenario?.EnemyId ?? "shark",
                Behavior = rosterEntry?.Behavior ?? BalanceEnemyBehavior.Chaser,
                BaseDamage = rosterEntry?.Damage ?? _labScenario?.EnemyBaseDamage ?? SimConstants.SharkBaseDamage,
                BaseMoveSpeed = rosterEntry?.MoveSpeed ?? _labScenario?.EnemyBaseMoveSpeed ?? SimConstants.SharkMoveSpeed,
                AttackCooldownSeconds = rosterEntry?.AttackCooldown ?? SimConstants.EnemyContactCooldownSeconds,
                PreferredRange = rosterEntry?.PreferredRange ?? 0f,
                XpValue = rosterEntry?.Xp ?? (int)(_labScenario?.EnemyXpValue ?? 8f),
                GoldValue = rosterEntry?.Gold ?? 1,
                Position = pos,
                Hull = SimFixed.FromFloat(hp),
                MaxHull = SimFixed.FromFloat(hp)
            });
        }

        BalanceEnemyRosterEntry FindRosterEntry(string id)
        {
            if (_labScenario == null || _labScenario.EnemyRoster == null) return null;
            for (var i = 0; i < _labScenario.EnemyRoster.Count; i++)
                if (_labScenario.EnemyRoster[i]?.Id == id) return _labScenario.EnemyRoster[i];
            return null;
        }

        BalanceEnemyRosterEntry PickRosterEntry()
        {
            if (_labScenario == null || _labScenario.EnemyRoster.Count == 0) return null;
            var now = _clock.SimSeconds;
            var total = 0f;
            for (var i = 0; i < _labScenario.EnemyRoster.Count; i++)
            {
                var entry = _labScenario.EnemyRoster[i];
                if (!IsRosterEntryAvailable(entry, now)) continue;
                total += entry.Weight;
            }
            if (total <= 0f) return null;
            var roll = _mapRng.NextFloat01() * total;
            for (var i = 0; i < _labScenario.EnemyRoster.Count; i++)
            {
                var entry = _labScenario.EnemyRoster[i];
                if (!IsRosterEntryAvailable(entry, now)) continue;
                roll -= entry.Weight;
                if (roll <= 0f) return entry;
            }
            return null;
        }

        bool IsRosterEntryAvailable(BalanceEnemyRosterEntry entry, float now)
        {
            if (entry == null || !entry.Enabled || entry.Weight <= 0f || now < entry.AvailableAfterSeconds ||
                (entry.AvailableUntilSeconds > 0f && now > entry.AvailableUntilSeconds)) return false;
            if (entry.MaxConcurrent <= 0) return true;
            var count = 0;
            for (var i = 0; i < _actors.Count; i++)
                if (!_actors[i].IsPlayer && !_actors[i].IsDead && _actors[i].ArchetypeId == entry.Id) count++;
            return count < entry.MaxConcurrent;
        }

        bool HasAvailableRosterEntry()
        {
            if (_labScenario == null || _labScenario.EnemyRoster.Count == 0) return true;
            for (var i = 0; i < _labScenario.EnemyRoster.Count; i++)
                if (IsRosterEntryAvailable(_labScenario.EnemyRoster[i], _clock.SimSeconds)) return true;
            return false;
        }

        void TickSpawnPressure(float dt)
        {
            if (!_experimentConfig.DirectorV2Enabled)
                return;
            if (_labScenario != null && !_labScenario.EnemySpawnEnabled)
                return;
            if (_labScenario != null && _labScenario.UseScriptedWaves)
            {
                if (TryGetActiveWave(out var wave))
                {
                    _waveSpawnTimer -= dt;
                    if (_waveSpawnTimer <= 0f && CountLivingEnemies() < _labScenario.DirectorMaxActiveEnemies)
                    {
                        SpawnShark(RandomSpawnPosition(), NextWaveEnemyId(wave));
                        _waveSpawnTimer = MathF.Max(.05f, wave.SpawnGapSeconds);
                    }
                    _peakEnemyCount = Math.Max(_peakEnemyCount, CountLivingEnemies());
                    return;
                }
                if (!_labScenario.ContinueAfterScriptedWaves) return;
            }

            var minute = _clock.SimSeconds / 60f;
            if (_labScenario != null &&
                (_clock.SimSeconds < _labScenario.EnemyAvailableAfterSeconds ||
                 (_labScenario.EnemyAvailableUntilSeconds > 0f && _clock.SimSeconds > _labScenario.EnemyAvailableUntilSeconds)))
                return;
            if (_labScenario != null && _labScenario.EnemyRoster.Count > 0 && !HasAvailableRosterEntry())
                return;

            var rate = SimMath.ComputeV3SpawnRate(
                minute,
                _experimentConfig.SpawnDensityMultiplier,
                _experimentConfig.DirectorV2Enabled);
            if (_labScenario?.StageProfile != null)
            {
                rate = SaltwindLabSpawnParityLogic.EffectiveAmbientSpawnsPerSecond(
                    _labScenario.StageProfile,
                    _clock.SimSeconds,
                    TryGetLivingBoss(out _));
            }
            else if (_labScenario != null)
            {
                rate *= _labScenario.DirectorThreatMultiplier * _labScenario.EnemySpawnWeight;
                var inFirstPulse = _clock.SimSeconds >= 240f && _clock.SimSeconds < 240f + _labScenario.PressurePulseDurationSeconds;
                var inSecondPulse = _clock.SimSeconds >= 420f && _clock.SimSeconds < 420f + _labScenario.PressurePulseDurationSeconds;
                if (inFirstPulse || inSecondPulse) rate *= _labScenario.PressurePulseMultiplier;
            }
            _spawnBudget += rate * dt;
            var activeCap = _labScenario?.DirectorMaxActiveEnemies ?? MaxPressureEnemies;
            if (_labScenario != null && _labScenario.EnemySpawnMaxConcurrent > 0)
                activeCap = Math.Min(activeCap, _labScenario.EnemySpawnMaxConcurrent);
            while (_spawnBudget >= 1f && CountLivingEnemies() < activeCap)
            {
                _spawnBudget -= 1f;
                SpawnShark(RandomSpawnPosition());
            }

            _peakEnemyCount = Math.Max(_peakEnemyCount, CountLivingEnemies());
        }

        int CountLivingEnemies()
        {
            var count = 0;
            for (var i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                if (!a.IsPlayer && !a.IsDead) count++;
            }
            return count;
        }

        void SpawnBoss()
        {
            if (_bossSpawned) return;
            _bossSpawned = true;
            if (_labScenario?.StageProfile != null)
            {
                CullNonBossEnemiesForFinale();
                // Pre-boss kiting can drift the player far from the origin; lab projectiles despawn outside ±40.
                SnapPlayerToSaltwindBossArena();
            }

            _actors.Add(new SimActor
            {
                Id = _nextActorId++,
                IsBoss = true,
                ArchetypeId = _labScenario?.BossId ?? "boss",
                Behavior = _labScenario?.BossBehavior ?? BalanceEnemyBehavior.Chaser,
                BaseDamage = _labScenario?.BossBaseDamage ?? 20f,
                BaseMoveSpeed = _labScenario?.BossBaseMoveSpeed ?? 3.5f,
                AttackCooldownSeconds = _labScenario?.BossAttackCooldown ?? 1.5f,
                PreferredRange = _labScenario?.BossPreferredRange ?? 8f,
                Position = new SimVec2(SimFixed.FromInt(12), SimFixed.FromInt(4)),
                Hull = SimFixed.FromFloat((_labScenario?.BossBaseHull ?? 500f) * (_labScenario?.BossHullMultiplier ?? 1f)),
                MaxHull = SimFixed.FromFloat((_labScenario?.BossBaseHull ?? 500f) * (_labScenario?.BossHullMultiplier ?? 1f))
            });
            _state = SimRunState.BossFight;
        }

        public void SetPause(SimPauseReason reason, bool active)
        {
            if (active) _pause.Add(reason);
            else _pause.Remove(reason);
        }

        public void SetMoveInput(float x, float y)
        {
            if (_pause.BlocksSimulation || _state == SimRunState.PlayerDead || _state == SimRunState.RunComplete)
                return;

            var v = new SimVec2(SimFixed.FromFloat(x), SimFixed.FromFloat(y)).Normalized();
            _player.Velocity = v.Mul(SimConstants.DefaultMoveSpeed *
                (_labScenario?.PlayerMoveSpeedMultiplier ?? 1f) *
                (_labScenario?.CaptainMoveSpeedMultiplier ?? 1f));
        }

        SimVec2 RandomSpawnPosition()
        {
            var angle = _mapRng.NextFloat01() * MathF.PI * 2f;
            var dist = 8f + _mapRng.NextFloat01() * 10f;
            return _player.Position.Add(new SimVec2(SimFixed.FromFloat(MathF.Cos(angle) * dist), SimFixed.FromFloat(MathF.Sin(angle) * dist)));
        }

        bool TryGetActiveWave(out BalanceWaveDefinition activeWave)
        {
            activeWave = null;
            if (_labScenario?.ScriptedWaves == null) return false;
            var cursor = 0f;
            for (var i = 0; i < _labScenario.ScriptedWaves.Count; i++)
            {
                var wave = _labScenario.ScriptedWaves[i];
                if (wave == null) continue;
                cursor += wave.DelaySeconds;
                var end = cursor + wave.DurationSeconds;
                if (_clock.SimSeconds >= cursor && _clock.SimSeconds < end && wave.Composition != null && wave.Composition.Count > 0)
                {
                    activeWave = wave;
                    return true;
                }
                cursor = end;
            }
            return false;
        }

        string NextWaveEnemyId(BalanceWaveDefinition wave)
        {
            var total = 0;
            for (var j = 0; j < wave.Composition.Count; j++) total += Math.Max(1, wave.Composition[j]?.Count ?? 1);
            var index = _waveCompositionCursor++ % Math.Max(1, total);
            for (var j = 0; j < wave.Composition.Count; j++)
            {
                var candidate = wave.Composition[j];
                index -= Math.Max(1, candidate?.Count ?? 1);
                if (index < 0) return candidate?.EnemyId;
            }
            return null;
        }

        public bool IsSaltwindBossFinaleActive() =>
            _labScenario?.StageProfile != null &&
            _bossSpawned &&
            _state == SimRunState.BossFight;

        /// <summary>Moves away from the closest threat with a tangent component, approximating a cautious kiting player.</summary>
        public void SetKiteInput()
        {
            SimActor closest = null;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < _actors.Count; i++)
            {
                var actor = _actors[i];
                if (actor.IsPlayer || actor.IsDead) continue;
                var distance = actor.Position.Sub(_player.Position).LengthSq().Raw;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                closest = actor;
            }

            if (closest == null)
            {
                SetMoveInput(1f, 0f);
                return;
            }

            var away = _player.Position.Sub(closest.Position).Normalized();
            var tangent = new SimVec2(SimFixed.FromFloat(-away.Y.ToFloat() * 0.55f), SimFixed.FromFloat(away.X.ToFloat() * 0.55f));
            var direction = away.Add(tangent).Normalized();
            SetMoveInput(direction.X.ToFloat(), direction.Y.ToFloat());
        }

        public void Step(float wallDeltaSeconds)
        {
            _clock.AdvanceWall(wallDeltaSeconds);
            if (_pause.BlocksSimulation || _state == SimRunState.PlayerDead || _state == SimRunState.RunComplete)
                return;

            _lastRum.BeginTick();
            var dt = SimConstants.TickDurationSeconds;
            _clock.AdvanceTick(false);

            MoveActors(dt);
            var preferBoss = ShouldPreferBossTargets();
            if (_loadoutWeapons.Count == 0)
                _weapon.Tick(dt, _player, _actors, _projectiles, ref _nextProjectileId, _events, _clock.Tick, preferBoss);
            else
                for (var i = 0; i < _loadoutWeapons.Count; i++)
                    _loadoutWeapons[i].Tick(dt, _player, _actors, _projectiles, ref _nextProjectileId, _events, _clock.Tick, preferBoss);
            AdvanceProjectiles(dt);
            ResolveContacts(dt);
            ResolveEnemyDeaths();
            TickEnemies(dt);
            TickSpawnPressure(dt);

            TickPoiRouteReward();
            TickMerchantEconomy();

            var bossTick = (int)MathF.Round((_labScenario?.BossSpawnSeconds ?? 20f) / SimConstants.TickDurationSeconds);
            if (!_bossSpawned && _clock.Tick >= bossTick)
                SpawnBoss();

            if (_state == SimRunState.RunComplete)
                return;

            if (_player.IsDead)
            {
                if (!_objectiveSecured)
                    _state = SimRunState.PlayerDead;
                else
                    _state = SimRunState.RunComplete;
            }
        }

        /// <summary>Advance wall time at a render rate; simulation ticks use fixed 30 Hz accumulation.</summary>
        public void StepRenderFrames(int renderFps, float wallSeconds)
        {
            var accumulator = 0f;
            var frameTime = 1f / renderFps;
            var frames = (int)(wallSeconds * renderFps);
            for (var i = 0; i < frames; i++)
            {
                accumulator += frameTime;
                while (accumulator >= SimConstants.TickDurationSeconds)
                {
                    accumulator -= SimConstants.TickDurationSeconds;
                    Step(SimConstants.TickDurationSeconds);
                }
            }
        }

        void MoveActors(float dt)
        {
            foreach (var a in _actors)
            {
                if (a.IsDead) continue;
                a.Position = a.Position.Add(a.Velocity.Mul(dt));
            }
        }

        void AdvanceProjectiles(float dt)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (!p.Active) continue;
                p.Position = p.Position.Add(p.Velocity.Mul(dt));
                if (p.Position.X.ToFloat() > 40f || p.Position.X.ToFloat() < -40f ||
                    p.Position.Y.ToFloat() > 40f || p.Position.Y.ToFloat() < -40f)
                {
                    p.Active = false;
                    _projectiles.RemoveAt(i);
                }
            }
        }

        void ResolveContacts(float dt)
        {
            for (var pi = _projectiles.Count - 1; pi >= 0; pi--)
            {
                var p = _projectiles[pi];
                if (!p.Active) continue;
                foreach (var a in _actors)
                {
                    if (a.IsDead || a.Id == p.OwnerId) continue;
                    if (!CirclesOverlap(p.Position, p.Radius, a.Position, SimConstants.EnemyRadiusMeters)) continue;
                    ApplyDamage(a, p.Damage, p.OwnerId);
                    p.Active = false;
                    _projectiles.RemoveAt(pi);
                    break;
                }
            }
        }

        void TickEnemies(float dt)
        {
            foreach (var e in _actors)
            {
                if (e.IsDead || e.IsPlayer) continue;
                e.ContactCooldown = MathF.Max(0f, e.ContactCooldown - dt);
                var toPlayer = _player.Position.Sub(e.Position);
                if (toPlayer.LengthSq().Raw < SimFixed.FromFloat(0.01f).Raw) continue;
                var dir = toPlayer.Normalized();
                var baseSpeed = e.IsBoss ? (_labScenario?.BossBaseMoveSpeed ?? 3.5f) : (e.BaseMoveSpeed > 0f ? e.BaseMoveSpeed : (_labScenario?.EnemyBaseMoveSpeed ?? SimConstants.SharkMoveSpeed));
                var speed = baseSpeed * (_labScenario?.EnemyMoveSpeedMultiplier ?? 1f) *
                    (e.IsBoss ? 1f : (_labScenario?.EnemyDirectorSpeedMultiplier ?? 1f));
                if (e.Behavior == BalanceEnemyBehavior.Charge) speed *= 1.35f;
                if (e.Behavior == BalanceEnemyBehavior.Tank) speed *= 0.75f;
                if (e.Behavior == BalanceEnemyBehavior.Ranged && e.PreferredRange > 0f && toPlayer.LengthSq().ToFloat() < e.PreferredRange * e.PreferredRange)
                    dir = dir.Mul(-1f);
                e.Velocity = dir.Mul(speed);
                e.Position = e.Position.Add(e.Velocity.Mul(dt));

                if (CirclesOverlap(e.Position, SimConstants.EnemyRadiusMeters, _player.Position, SimConstants.PlayerRadiusMeters))
                {
                    if (e.ContactCooldown <= 0f)
                    {
                        var baseDmg = e.IsBoss
                            ? (_labScenario?.BossBaseDamage ?? 20f) * (_labScenario?.BossDamageMultiplier ?? 1f)
                            : (e.BaseDamage > 0f ? e.BaseDamage : (_labScenario?.EnemyBaseDamage ?? SimConstants.SharkBaseDamage));
                        var dmg = SimFixed.FromFloat(baseDmg * MathF.Max(0.1f, _experimentConfig.EnemyDamageMultiplier));
                        ApplyDamage(_player, dmg, e.Id);
                        e.ContactCooldown = e.AttackCooldownSeconds > 0f ? e.AttackCooldownSeconds : SimConstants.EnemyContactCooldownSeconds;
                        if (e.Behavior == BalanceEnemyBehavior.Suicide) e.Hull = SimFixed.FromInt(0);
                    }
                }

            }
        }

        void ResolveEnemyDeaths()
        {
            for (var i = 0; i < _actors.Count; i++)
            {
                var enemy = _actors[i];
                if (enemy.IsPlayer || !enemy.IsDead || enemy.DeathReported) continue;
                enemy.DeathReported = true;
                _kills++;
                GrantXp(enemy.IsBoss ? 120f : (enemy.XpValue > 0 ? enemy.XpValue : (_labScenario?.EnemyXpValue ?? 8f)));
                if (!enemy.IsBoss) _gold += Math.Max(0, enemy.GoldValue);
                if (enemy.IsBoss)
                    SecureObjective();
            }
        }

        void ApplyDamage(SimActor target, SimFixed damage, int sourceId)
        {
            if (target.IsDead) return;
            target.Hull = target.Hull.Sub(damage);
            _events.Add(new SimEvent(SimEventKind.DamageDealt, _clock.Tick, sourceId, target.Id, damage));

            if (target.IsPlayer && target.Hull.Raw <= 0)
                _lastRum.TryPreventLethal(ref target.Hull);

            if (target.Hull.Raw <= 0)
            {
                target.Hull = SimFixed.FromInt(0);
                _events.Add(new SimEvent(SimEventKind.ActorDied, _clock.Tick, target.Id));
                if (target.IsBoss)
                    SecureObjective();
            }
        }

        void GrantXp(float amount)
        {
            if (_labScenario != null)
            {
                var poiXp = _labScenario.EnablePois
                    ? MathF.Pow(1.05f * _labScenario.PoiXpMultiplier, _labScenario.SalvageChestsOpened + _labScenario.TreasureChartsUsed)
                    : 1f;
                amount *= _labScenario.XpGainMultiplier * poiXp;
            }
            _xp += amount;
            _events.Add(new SimEvent(SimEventKind.XpGained, _clock.Tick, _player.Id, value: SimFixed.FromFloat(amount)));
            var needed = XpNeeded();
            while (_xp >= needed)
            {
                _xp -= needed;
                _level++;
                _events.Add(new SimEvent(SimEventKind.LevelUpOffer, _clock.Tick, _player.Id, value: SimFixed.FromInt(_level)));
                ApplyUpgradePath();
                needed = XpNeeded();
            }
        }

        void ApplyUpgradePath()
        {
            if (_labScenario == null || !_labScenario.SimulateUpgradePath) return;

            if (_labScenario.StageProfile != null)
            {
                ApplySaltwindStageLevelUp();
                return;
            }

            TryAddSaltwindPolicyWeapon();
            var multiplier = 1f + _labScenario.DamageUpgradePerLevel;
            if (!_evolutionApplied && _level >= _labScenario.EvolutionLevel)
            {
                multiplier *= _labScenario.EvolutionDamageMultiplier;
                _evolutionApplied = true;
            }
            if (_loadoutWeapons.Count == 0) _weapon.MultiplyDamage(multiplier);
            else for (var i = 0; i < _loadoutWeapons.Count; i++) _loadoutWeapons[i].MultiplyDamage(multiplier);
            _labScenario.WeaponCount = Math.Max(_labScenario.WeaponCount, _loadoutWeapons.Count > 0 ? _loadoutWeapons.Count : 1);
        }

        void ApplySaltwindStageLevelUp()
        {
            var export = _labScenario.StageProfile;
            var slotCap = SaltwindLabProgressionParityLogic.WeaponSlotCapAtLevel(export, _level);
            var owned = CollectOwnedWeaponIds();
            var pick = SaltwindLabLevelOfferLogic.RollAutopick(
                export,
                _labScenario.SaltwindBuildPolicy,
                _labScenario.SaltwindUnlockPool,
                owned,
                _weaponLevels,
                _level,
                slotCap,
                ref _lootRng);

            switch (pick.Action)
            {
                case SaltwindLabLevelOfferLogic.LevelUpAction.AddWeapon:
                    AppendLabWeapon(pick.WeaponId);
                    _weaponLevels[pick.WeaponId] = 1;
                    break;
                case SaltwindLabLevelOfferLogic.LevelUpAction.UpgradeWeapon:
                    ApplyLabWeaponUpgrade(pick.WeaponId);
                    break;
            }

            _labScenario.WeaponCount = Math.Max(_labScenario.WeaponCount, _loadoutWeapons.Count > 0 ? _loadoutWeapons.Count : 1);
        }

        List<string> CollectOwnedWeaponIds()
        {
            var owned = new List<string>();
            for (var i = 0; i < _labScenario.WeaponLoadout.Count; i++)
            {
                var id = _labScenario.WeaponLoadout[i]?.Id;
                if (!string.IsNullOrEmpty(id)) owned.Add(id);
            }

            return owned;
        }

        void ApplyLabWeaponUpgrade(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            _weaponLevels.TryGetValue(weaponId, out var level);
            if (level <= 0) level = 1;
            SaltwindLabLevelOfferLogic.TryGetWeaponSnapshot(_labScenario.StageProfile, weaponId, out var snap);
            if (snap != null && level >= snap.MaxLevel) return;

            level++;
            _weaponLevels[weaponId] = level;
            var mult = snap != null
                ? 1f + snap.DamageUpgradePerLevel
                : 1f + _labScenario.DamageUpgradePerLevel;
            for (var i = 0; i < _loadoutWeapons.Count; i++)
            {
                if (_labScenario.WeaponLoadout[i]?.Id != weaponId) continue;
                _loadoutWeapons[i].MultiplyDamage(mult);
                if (_labScenario.WeaponLoadout[i] != null)
                    _labScenario.WeaponLoadout[i].Damage *= mult;
                break;
            }
        }

        void TryAddSaltwindPolicyWeapon()
        {
            if (_labScenario.SaltwindBuildPolicy == SaltwindLabBuildPolicy.None) return;
            if (_labScenario.StageProfile != null &&
                !SaltwindLabProgressionParityLogic.ShouldOfferNewWeaponOnLevel(_level))
                return;
            if (_labScenario.StageProfile == null && _level % 2 != 0) return;

            var cap = _labScenario.StageProfile != null
                ? SaltwindLabProgressionParityLogic.WeaponSlotCapAtLevel(_labScenario.StageProfile, _level)
                : Math.Min(_labScenario.WeaponSlotCap, 1 + _level / 2);
            if (_loadoutWeapons.Count >= cap) return;

            var owned = new List<string>();
            for (var i = 0; i < _labScenario.WeaponLoadout.Count; i++)
            {
                var id = _labScenario.WeaponLoadout[i]?.Id;
                if (!string.IsNullOrEmpty(id)) owned.Add(id);
            }

            if (_labScenario.StageProfile != null &&
                SaltwindLabProgressionParityLogic.TryPickPolicyWeapon(
                    _labScenario.StageProfile,
                    _labScenario.SaltwindBuildPolicy,
                    _labScenario.SaltwindUnlockPool,
                    owned,
                    out var picked))
            {
                AppendLabWeapon(picked);
                return;
            }

            var queue = SaltwindBalanceLabScenarioFactory.ResolveWeaponQueue(
                _labScenario.SaltwindBuildPolicy,
                _labScenario.SaltwindUnlockPool,
                _labScenario.StageProfile);
            for (var i = 0; i < queue.Count; i++)
            {
                var id = queue[i];
                if (WeaponAlreadyLoaded(id)) continue;
                AppendLabWeapon(id);
                break;
            }
        }

        bool WeaponAlreadyLoaded(string weaponId)
        {
            for (var i = 0; i < _labScenario.WeaponLoadout.Count; i++)
            {
                if (_labScenario.WeaponLoadout[i]?.Id == weaponId) return true;
            }

            return false;
        }

        void AppendLabWeapon(string id)
        {
            var damage = _labScenario.WeaponBaseDamage;
            var cooldown = _labScenario.WeaponBaseCooldown;
            var range = _labScenario.WeaponRange;
            var projectileSpeed = _labScenario.WeaponProjectileSpeed;
            if (_labScenario.StageProfile != null &&
                SaltwindLabLevelOfferLogic.TryGetWeaponSnapshot(_labScenario.StageProfile, id, out var snap))
            {
                damage = snap.BaseDamage;
                cooldown = snap.BaseCooldown;
                range = snap.Range;
                projectileSpeed = snap.ProjectileSpeed;
            }

            var entry = new BalanceWeaponLoadoutEntry
            {
                Id = id,
                Damage = damage,
                Cooldown = cooldown,
                Range = range,
                ProjectileSpeed = projectileSpeed,
                TargetMultiplier = _labScenario.WeaponPatternTargetMultiplier
            };
            var system = new WeaponSystem();
            system.SetDamageMultiplier(_labScenario.WeaponDamageMultiplier * _labScenario.ItemDamageMultiplier);
            system.SetPatternTargetMultiplier(entry.TargetMultiplier);
            system.SetAuthoredWeapon(entry.Damage, entry.Cooldown, entry.Range, entry.ProjectileSpeed);
            system.ApplyLabTuning(_labScenario.WeaponCooldownMultiplier, _labScenario.ProjectileSpeedMultiplier,
                _labScenario.ProjectileRadiusMultiplier * entry.SizeMultiplier);
            _loadoutWeapons.Add(system);
            _labScenario.WeaponLoadout.Add(entry);
        }

        float XpNeeded()
        {
            if (_labScenario?.StageProfile != null)
                return StageBalanceProfileExportCodec.XpToNextLevel(_labScenario.StageProfile, _level);
            return SimMath.XPToNext(_level) * (_labScenario?.XpRequirementMultiplier ?? 1f);
        }

        void TickPoiRouteReward()
        {
            if (_poiHealRemaining <= 0f || _labScenario == null || !_labScenario.EnablePois) return;
            if (_clock.SimSeconds < _nextPoiHealAtSeconds) return;
            var heal = MathF.Min(_poiHealRemaining, _player.MaxHull.ToFloat() * 0.15f);
            _poiHealRemaining -= heal;
            _player.Hull = SimFixed.FromFloat(MathF.Min(_player.MaxHull.ToFloat(), _player.Hull.ToFloat() + heal));
            _nextRepairWreckIndex++;
            _nextPoiHealAtSeconds = _nextRepairWreckIndex < _labScenario.RepairWrecksUsed
                ? SaltwindLabRouteParityLogic.RepairWreckCheckpointSeconds(
                    _nextRepairWreckIndex,
                    _labScenario.RepairWrecksUsed,
                    _labScenario.DurationSeconds)
                : float.PositiveInfinity;
            ResolvePoiRisk();
        }

        void ResolvePoiRisk()
        {
            // Route time and failure risk let POI rewards compete with danger instead of being a free multiplier.
            if (_labScenario == null || _poiInteractionsResolved >= _labScenario.RepairWrecksUsed) return;
            _poiInteractionsResolved++;
            if (_lootRng.NextFloat01() < _labScenario.PoiFailureChance) return;
            var risk = _labScenario.PoiInteractionRiskDamage * (1f + _labScenario.PoiTravelSeconds / 60f);
            ApplyDamage(_player, SimFixed.FromFloat(risk), -1000 - _poiInteractionsResolved);
        }

        void TickMerchantEconomy()
        {
            if (_labScenario == null || !_labScenario.EnablePois || _labScenario.MerchantVisits <= 0 ||
                _merchantPurchasesCompleted >= _labScenario.PlannedMerchantPurchases) return;
            var checkpoint = _labScenario.StageProfile != null
                ? SaltwindLabRouteParityLogic.SalvageChestCheckpointSeconds(
                    _labScenario.StageProfile,
                    _nextMerchantChestIndex,
                    _labScenario.DurationSeconds)
                : (_merchantPurchasesCompleted + 1f) / (_labScenario.MerchantVisits + 1f) * _labScenario.DurationSeconds;
            if (_clock.SimSeconds < checkpoint) return;
            var cost = _labScenario.StageProfile != null
                ? StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(
                    _labScenario.StageProfile, _merchantPurchasesCompleted)
                : _labScenario.MerchantItemPrice + (_merchantPurchasesCompleted > 0 ? _labScenario.MerchantRerollPrice : 0);
            if (_gold < cost) return;
            _gold -= cost;
            _merchantPurchasesCompleted++;
            _nextMerchantChestIndex++;
            if (_loadoutWeapons.Count == 0)
                _weapon.MultiplyDamage(_labScenario.MerchantItemPowerMultiplier);
            else
                for (var i = 0; i < _loadoutWeapons.Count; i++)
                    _loadoutWeapons[i].MultiplyDamage(_labScenario.MerchantItemPowerMultiplier);
        }

        public void SecureObjective()
        {
            if (_objectiveSecured) return;
            _objectiveSecured = true;
            _objectiveSecuredSeconds = _clock.SimSeconds;
            _state = _labScenario?.StageProfile != null ? SimRunState.RunComplete : SimRunState.ObjectiveSecured;
            _events.Add(new SimEvent(SimEventKind.ObjectiveSecured, _clock.Tick, _player.Id));
        }

        bool ShouldPreferBossTargets() =>
            _labScenario?.StageProfile != null && TryGetLivingBoss(out _);

        bool TryGetLivingBoss(out SimActor boss)
        {
            boss = null;
            for (var i = 0; i < _actors.Count; i++)
            {
                var actor = _actors[i];
                if (!actor.IsBoss || actor.IsDead) continue;
                boss = actor;
                return true;
            }

            return false;
        }

        void CullNonBossEnemiesForFinale()
        {
            for (var i = _actors.Count - 1; i >= 0; i--)
            {
                var actor = _actors[i];
                if (actor.IsPlayer || actor.IsBoss) continue;
                _actors.RemoveAt(i);
            }
        }

        void SnapPlayerToSaltwindBossArena()
        {
            _player.Position = new SimVec2(SimFixed.FromInt(8), SimFixed.FromInt(4));
            _player.Velocity = default;
        }

        /// <summary>Fixture: apply simultaneous lethal to player and boss on same tick.</summary>
        public void ApplySameTickLethalFixture()
        {
            if (_actors.Find(a => a.IsBoss) == null)
                SpawnBoss();
            var boss = _actors.Find(a => a.IsBoss);
            if (boss == null) return;
            _player.Hull = SimFixed.FromFloat(SimConstants.DefaultHull);
            var lethal = SimFixed.FromFloat(9999f);
            ApplyDamage(boss, lethal, _player.Id);
            ApplyDamage(_player, lethal, boss.Id);
        }

        public string ComputeStateHash()
        {
            var json = string.Join("|",
                Seed.ToString(),
                _clock.Tick.ToString(),
                _state.ToString(),
                _objectiveSecured.ToString(),
                _player.Hull.Raw.ToString(),
                _kills.ToString(),
                _level.ToString(),
                _actors.Count.ToString(),
                _mapRng.StateSnapshot.ToString(),
                _combatRng.StateSnapshot.ToString(),
                _lootRng.StateSnapshot.ToString());
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            return ToHex(bytes);
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2"));
            return sb.ToString();
        }

        static bool CirclesOverlap(SimVec2 a, float radiusA, SimVec2 b, float radiusB)
        {
            var dx = a.X.ToFloat() - b.X.ToFloat();
            var dy = a.Y.ToFloat() - b.Y.ToFloat();
            var r = radiusA + radiusB;
            return dx * dx + dy * dy <= r * r;
        }
    }
}

using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;
using Behavior = RoguePirates.V4.Config.ConfigEnums.WeaponBehavior;

namespace RoguePirates.V4.Weapons
{
    /// <summary>What the console, Module Lab and presenters can ask of the weapons.</summary>
    public interface IWeaponManager
    {
        IReadOnlyList<WeaponSlot> Slots { get; }
        PlayerProjectiles Projectiles { get; }
        /// <summary>Fire every weapon once (captain active, Captain's Bell); orbit blades hit faster for a while instead.</summary>
        void FireAllOnce();
        void ArmAllKegs();
        void FireLightningSalvo();
        /// <summary>Fire one slot now, ignoring its cooldown (Module Lab).</summary>
        bool FireSlot(int index);
        /// <summary>Drops kegs, strikes, skiffs, follow-ups and projectiles (loadout replaced, as V3's SetWeaponLoadout did).</summary>
        void ClearInFlight();
        /// <summary>Where the orbit blades are this frame (their hit spheres); presentation draws the blades there.</summary>
        IReadOnlyList<OrbitBehavior.HitSphere> OrbitHits { get; }
        /// <summary>V5 M4: where a given Orbit weapon's blades are (each Orbit weapon has its own).</summary>
        IReadOnlyList<OrbitBehavior.HitSphere> OrbitHitsOf(WeaponSlot slot) => OrbitHits;
        /// <summary>V5 M5 Leviathan Heart: a telegraphed blast on the densest crowd within 20 m.</summary>
        void ItemPulse(string ledgerId, float damage, float radius, float telegraphSeconds) { }
    }

    /// <summary>
    /// The V4 Weapon Manager (M5): one slot per equipped weapon, one behavior per firing logic, one stat resolver, no
    /// weapon-name checks. Port of V3 WeaponController's runtime: the loadout (weapons, levels, upgrades, evolutions)
    /// still comes from the legacy level system through <see cref="IWeaponLoadout"/> until M9; firing, cooldowns,
    /// streams, kegs, strikes, skiffs, orbit hits and player projectiles are V4. Ticks where WeaponController ticked
    /// (its legacy update slot) so the traces match V3.
    /// </summary>
    public sealed class WeaponManager : IWeaponManager
    {
        readonly WeaponCombat _c;
        readonly IWeaponLoadout _loadout;
        readonly IRunStatus _run;
        readonly ConfigSetRuntime _config;
        readonly List<WeaponSlot> _slots = new List<WeaponSlot>();
        readonly Dictionary<object, WeaponSlot> _byKey = new Dictionary<object, WeaponSlot>();
        readonly Dictionary<Behavior, WeaponBehavior> _behaviors;
        readonly HashSet<string> _missingConfigWarned = new HashSet<string>();
        readonly List<WeaponSlot> _rebuild = new List<WeaponSlot>();

        readonly ProjectileBehavior _projectile;
        readonly FanBehavior _fan;
        readonly OrbitBehavior _orbit;
        readonly ConeStreamBehavior _stream;
        readonly MineBehavior _mine;
        readonly AreaStrikeBehavior _strike;
        readonly SummonBehavior _summon;

        public WeaponManager(WeaponCombat combat, IWeaponLoadout loadout, IRunStatus run, ConfigSetRuntime config)
        {
            _c = combat;
            _loadout = loadout;
            _run = run;
            _config = config;
            _projectile = new ProjectileBehavior(combat);
            _fan = new FanBehavior(combat);
            _orbit = new OrbitBehavior(combat);
            _stream = new ConeStreamBehavior(combat);
            _mine = new MineBehavior(combat);
            _strike = new AreaStrikeBehavior(combat);
            _summon = new SummonBehavior(combat);
            if (combat.Projectiles != null) combat.Projectiles.HitAdjust = combat.Healthy; // V5 M5
            _behaviors = new Dictionary<Behavior, WeaponBehavior>
            {
                [Behavior.Projectile] = _projectile,
                [Behavior.Fan] = _fan,
                [Behavior.Orbit] = _orbit,
                [Behavior.ConeStream] = _stream,
                [Behavior.Trail] = new TrailBehavior(combat),
                [Behavior.Mine] = _mine,
                [Behavior.AreaStrike] = _strike,
                [Behavior.Aura] = new AuraBehavior(combat),
                [Behavior.PredictedArea] = new PredictedAreaBehavior(combat),
                [Behavior.Summon] = _summon,
            };
        }

        public IReadOnlyList<WeaponSlot> Slots => _slots;
        public PlayerProjectiles Projectiles => _c.Projectiles;
        public WeaponCombat Combat => _c;
        public OrbitBehavior Orbit => _orbit;
        public IReadOnlyList<OrbitBehavior.HitSphere> OrbitHits => _orbit.Hits;
        public IReadOnlyList<OrbitBehavior.HitSphere> OrbitHitsOf(WeaponSlot slot) => _orbit.HitsOf(slot);
        /// <summary>The orbit behaviour (tests).</summary>
        public OrbitBehavior OrbitBehavior => _orbit;

        WeaponRulesConfig Rules => _c.Rules();

        /// <summary>The weapon tick, in WeaponController.Update's place in the frame.</summary>
        public void Tick(float dt)
        {
            if (_run == null || !_run.Exists || _run.IsPaused) return;
            if (_c.Ship == null || !_c.Ship.Exists) return;
            if ((_c.Enemies == null || !_c.Enemies.Exists) && !_c.Hooks.ShipVersusShip) return;
            if (_c.Hooks.AutoFireDisabled) return;
            SyncLoadout();
            TickWeapons(dt);
            _orbit.TickHits(_slots, dt);
            MirrorLoadout();
        }

        /// <summary>Orbit blade positions, at V3's presentation slot (order 50) with Time.time; used by the next tick.</summary>
        public void TickOrbitPositions(float time)
        {
            if (_run == null || !_run.Exists || _run.IsPaused) return;
            SyncLoadout();
            _orbit.ComputeHits(_slots, time);
        }

        void TickWeapons(float dt)
        {
            _c.OrbitVolleyTimer = Mathf.Max(0f, _c.OrbitVolleyTimer - dt);
            _mine.TickKegs(dt);
            TickDelayedStrikes(dt);
            TickWhirlpools(dt);
            _summon.TickSkiffs(dt);
            _fan.TickFollowUps(dt);
            TickEchoes(dt);
            var itemAttackSpeed = _c.Mods.ItemAttackSpeedMultiplier;
            var rules = Rules;
            // R-22 fixes: item attack speed once (V3 also sped up the countdown), and the overshoot past zero carried
            // into the next cooldown (V3 dropped it), capped at one frame so a weapon that waited does not catch up.
            var countdown = rules != null && rules.itemAttackSpeedAppliedOnce ? 1f : itemAttackSpeed;
            var carry = rules != null && rules.carryCooldownOvershoot;
            for (var wi = 0; wi < _slots.Count; wi++)
            {
                var w = _slots[wi];
                if (w.Config == null) continue;
                if (w.Config.behavior == Behavior.ConeStream)
                {
                    _stream.TickSlot(w, dt, itemAttackSpeed);
                    continue;
                }

                // Playtest (3 Oct, owner): a summon weapon's cooldown starts when its last summon despawns (held full meanwhile).
                if (w.Config.behavior == Behavior.Summon && _summon.HasAlive(w.Config.weaponId))
                {
                    w.CooldownTimer = Mathf.Max(w.CooldownTimer, WeaponStatResolver.Cooldown(w, _c.Mods, itemAttackSpeed));
                    continue;
                }

                w.CooldownTimer -= dt * countdown;
                if (w.CooldownTimer > 0f) continue;

                var cd = WeaponStatResolver.Cooldown(w, _c.Mods, itemAttackSpeed);
                var next = carry ? cd + Mathf.Max(w.CooldownTimer, -dt * countdown) : cd;
                if (w.BurstShotsLeft > 0)
                {
                    if (_projectile.FireBurstShot(w) == WeaponFireOutcome.Emitted)
                    {
                        w.BurstShotsLeft--;
                        w.CooldownTimer = w.BurstShotsLeft > 0 && !w.Config.projectile.v3FullCooldownAfterFirstShot ? w.Config.projectile.burstGapSeconds : next;
                        _c.RecordEmission(w);
                    }
                    continue;
                }

                if (FirePattern(w) == WeaponFireOutcome.Emitted)
                {
                    // R-87 fix: a burst that just started keeps its gap (V3 overwrote it with the full cooldown).
                    if (!(w.BurstShotsLeft > 0 && !w.Config.projectile.v3FullCooldownAfterFirstShot)) w.CooldownTimer = next;
                    _c.RecordEmission(w);
                    QueueEchoes(w);
                }
            }
        }

        /// <summary>V5 M8z (7 Oct, Q20): a milestone's echoes: the firing repeats (the Siren Bell's second pulse 0.35 s later).</summary>
        void QueueEchoes(WeaponSlot w)
        {
            if (w.Echoes <= 0 || w.Config == null || w.Config.behavior == Behavior.Orbit || w.Config.behavior == Behavior.ConeStream) return;
            _c.Echoes.Add(new PendingEcho { Weapon = w, Timer = w.EchoDelaySeconds, Left = w.Echoes });
        }

        void TickEchoes(float dt)
        {
            var list = _c.Echoes;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var echo = list[i];
                echo.Timer -= dt;
                if (echo.Timer > 0f) { list[i] = echo; continue; }
                var w = echo.Weapon;
                var behavior = w != null && _slots.Contains(w) ? BehaviorOf(w) : null; // a weapon that left the loadout does not echo
                if (behavior == null) { list.RemoveAt(i); continue; }
                behavior.FireOnce(w); // part of the same firing: no emission counted
                if (--echo.Left > 0) { echo.Timer += Mathf.Max(0.05f, w.EchoDelaySeconds); list[i] = echo; }
                else list.RemoveAt(i);
            }
        }

        WeaponFireOutcome FirePattern(WeaponSlot w)
        {
            var behavior = BehaviorOf(w);
            if (behavior == null || !behavior.CanFire(w)) return WeaponFireOutcome.NoTarget;
            if (!_c.Targeting.TryResolveTarget(w.Config, out _, out _)
                && behavior.NeedsTarget
                && !_c.Hooks.TryResolveShipAim(w.Id, out _))
                return WeaponFireOutcome.NoTarget;
            return behavior.Fire(w);
        }

        WeaponBehavior BehaviorOf(WeaponSlot w) => w.Config != null && _behaviors.TryGetValue(w.Config.behavior, out var b) ? b : null;

        /// <summary>V5 M5 Leviathan Heart: a delayed blast on the densest crowd (no item procs: no proc coefficient).</summary>
        public void ItemPulse(string ledgerId, float damage, float radius, float telegraphSeconds)
        {
            if (_c.Ship == null || !_c.Ship.Exists || damage <= 0f) return;
            var target = _c.Targeting.PredictClusterGround(20f);
            _c.Effects?.TimedRing(target, radius, telegraphSeconds);
            _c.Strikes.Add(new DelayedStrike
            {
                Active = true, Position = target, Timer = telegraphSeconds, Radius = radius, BaseDamage = damage, DamageBonus = 1f,
                ProcCoefficient = 0f, WeaponId = ledgerId,
            });
        }

        /// <summary>V3 TickDelayedStrikes: mortar shells and Tempest markers, one list.</summary>
        void TickDelayedStrikes(float dt)
        {
            var list = _c.Strikes;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var strike = list[i];
                if (!strike.Active) { list.RemoveAt(i); continue; }

                strike.Timer -= dt;
                if (strike.Timer > 0f) { list[i] = strike; continue; }

                var hits = 0;
                var explosion = strike.AreaShell && !strike.PulseImpact ? _c.Mods.ExplosionDamageMultiplier : 1f;
                for (var ai = 0; ai < _c.Enemies.Capacity; ai++)
                {
                    if (!_c.Enemies.IsActive(ai)) continue;
                    var position = _c.Enemies.Position(ai);
                    if (strike.AreaShell)
                    {
                        if (!WeaponCombat.WithinHorizontalRadius(strike.Position, position, strike.Radius)) continue;
                    }
                    else if (Vector3.Distance(position, strike.Position) > strike.Radius)
                        continue;
                    hits++;
                    // V5 M8z (7 Oct, D21, Q32): elite damage (Admiral) on elites, the boss and minibosses; the ledger keeps elites.
                    var dmg = _c.Calculator.Roll(strike.BaseDamage, strike.DamageBonus, _c.Enemies.IsEliteDamageTarget(ai), 0f, strike.ProcCoefficient, strike.WeaponId);
                    if (strike.AreaShell && explosion != 1f) dmg = new DamageRoll(dmg.Amount * explosion, dmg.IsCrit); // R-40: mortar shells explode
                    _c.ApplyWeaponDamage(ai, strike.WeaponId, dmg, _c.Enemies.IsElite(ai), _c.Enemies.IsBoss(ai));
                    if (strike.SlowFraction > 0f) _c.Damage.ApplySlow(ai, strike.SlowFraction, strike.SlowSeconds); // V5 M8z (D21): the ink pot's splash slows at once
                }
                _c.Hooks.RecordConcurrentTargets(strike.WeaponId, hits);
                if (strike.AreaShell)
                {
                    if (!strike.PulseImpact) _c.Mods.ExplosionResolved();
                    if (strike.ImpactDraws) DrawV3ImpactNumbers();
                    if (strike.PulseImpact) _c.Effects?.WeaponPulse(strike.WeaponId, strike.Position, strike.Radius); // V5 M8z (D21): the ink lands
                    else _c.Effects?.AreaImpact(strike.Position, strike.Radius);
                    _c.Effects?.WaterSplash(strike.Position);
                    if (strike.Whirlpool != null) OpenWhirlpool(strike);
                }
                else
                    _c.Effects?.Explosion(strike.Position, strike.Radius);
                list.RemoveAt(i);
            }
        }

        /// <summary>V5 M4b: the Maelstrom's whirlpool opens where its shell lands. V5 M8z (7 Oct, D21): the Duration card
        /// lengthens it (the Maelstrom has none, so it is as before) and it carries the shell's slow (the ink slick).</summary>
        void OpenWhirlpool(in DelayedStrike strike)
        {
            var p = strike.Whirlpool.predictedArea;
            var duration = strike.DurationMult > 0f ? strike.DurationMult : 1f;
            var seconds = p.whirlpoolSeconds * duration;
            _c.Whirlpools.Add(new Whirlpool
            {
                Active = true, Position = strike.Position, Radius = strike.Radius, Left = seconds, TickTimer = p.whirlpoolTickSeconds,
                BaseDamage = strike.BaseDamage, DamageBonus = strike.DamageBonus, ProcCoefficient = strike.ProcCoefficient, Config = strike.Whirlpool,
                Ticks = WeaponStatResolver.WhirlpoolTicks(p, duration), SlowFraction = strike.SlowFraction, SlowSeconds = strike.SlowSeconds,
            });
            _c.Effects?.TimedRing(strike.Position, strike.Radius, seconds);
        }

        /// <summary>V5 M4b: each tick a whirlpool drags the enemies inside toward its centre and hurts them; at the end it
        /// implodes.</summary>
        void TickWhirlpools(float dt)
        {
            var list = _c.Whirlpools;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var pool = list[i];
                var p = pool.Config.predictedArea;
                pool.Left -= dt;
                pool.TickTimer -= dt;
                // Whole ticks (review: a float race between the two timers dropped or added the last one by frame rate);
                // a long frame deals every tick it covered.
                var ticks = pool.Ticks;
                while (pool.TickTimer <= 0f && pool.TicksDone < ticks)
                {
                    pool.TickTimer += p.whirlpoolTickSeconds;
                    pool.TicksDone++;
                    WhirlpoolHit(pool, p.whirlpoolTickDamageMultiplier, p.pullMetersPerSecond * p.whirlpoolTickSeconds, true);
                    if (pool.Config.visual.pulseImpact) _c.Effects?.WeaponPulse(pool.Config.weaponId, pool.Position, pool.Radius);
                }
                if (pool.TicksDone >= ticks && pool.Left <= 0f)
                {
                    // V5 M8z (D21): a slick with no implosion just ends.
                    if (p.implodeDamageMultiplier > 0f)
                    {
                        WhirlpoolHit(pool, p.implodeDamageMultiplier, 0f, false);
                        _c.Effects?.AreaImpact(pool.Position, pool.Radius);
                    }
                    list.RemoveAt(i);
                }
                else list[i] = pool;
            }
        }

        void WhirlpoolHit(in Whirlpool pool, float multiplier, float pull, bool tick)
        {
            for (var ai = 0; ai < _c.Enemies.Capacity; ai++)
            {
                if (!_c.Enemies.IsActive(ai) || !WeaponCombat.WithinHorizontalRadius(pool.Position, _c.Enemies.Position(ai), pool.Radius)) continue;
                var isElite = _c.Enemies.IsEliteDamageTarget(ai);
                var dmg = _c.Calculator.RollAmount(pool.BaseDamage * multiplier, pool.DamageBonus, isElite, 0f, pool.ProcCoefficient, pool.Config.weaponId);
                _c.DamageAndRecord(ai, pool.Config.weaponId, dmg, false, pool.Config.weaponId);
                if (pull > 0f) _c.Damage.Pull(ai, pool.Position, pull);
                if (tick && pool.SlowFraction > 0f) _c.Damage.ApplySlow(ai, pool.SlowFraction, pool.SlowSeconds); // V5 M8z (D21)
            }
        }

        /// <summary>
        /// V3's mortar impact effect drew from the combat stream: 4–6 bursts, each an angle and a distance (register R-70).
        /// V4 makes the same draws here, in the simulation, so later crit rolls stay aligned; the effect itself is placed
        /// without them.
        /// </summary>
        void DrawV3ImpactNumbers()
        {
            var burstCount = 4 + Mathf.FloorToInt(_c.Random.CombatFloat() * 3f);
            for (var i = 0; i < burstCount; i++)
            {
                _c.Random.CombatFloat();
                _c.Random.CombatFloat();
            }
        }

        // ---- Fire-all, kegs, salvo (V3 captain actives and Captain's Bell) ----------------------------------------

        public void FireAllOnce()
        {
            if (_run == null || !_run.Exists || _c.Enemies == null || !_c.Enemies.Exists || _c.Ship == null || !_c.Ship.Exists) return;
            SyncLoadout();
            for (var wi = 0; wi < _slots.Count; wi++)
            {
                var w = _slots[wi];
                var behavior = BehaviorOf(w);
                if (behavior == null) continue;
                if (w.Config.behavior == Behavior.Orbit)
                {
                    _c.OrbitVolleyTimer = Rules != null ? Rules.fireAllOrbitVolleySeconds : 2f;
                    continue;
                }
                behavior.FireOnce(w);
                _c.RecordEmission(w);
                QueueEchoes(w); // V5 M8z (7 Oct, Q20)
            }
            MirrorLoadout();
        }

        public void ArmAllKegs() => _mine.ArmAll();

        /// <summary>
        /// V3 CaptainFireLightningSalvo looks for lightning weapons by ids no weapon has, so it always strikes the fallback
        /// (register R-88); V4 does the same.
        /// </summary>
        public void FireLightningSalvo()
        {
            if (_run == null || !_run.Exists || _c.Enemies == null || !_c.Enemies.Exists || _c.Ship == null || !_c.Ship.Exists) return;
            var rules = Rules;
            if (rules != null && rules.salvoUsesLightningWeapon)
            {
                // R-88 fix: fire the area-strike weapon you have (V3 looked for it by ids no weapon has).
                SyncLoadout();
                foreach (var w in _slots)
                {
                    if (w.Config == null || w.Config.behavior != Behavior.AreaStrike) continue;
                    _strike.FireOnce(w);
                    _c.RecordEmission(w);
                    return;
                }
            }
            _strike.FireSalvo(rules);
        }

        public bool FireSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var w = _slots[index];
            var behavior = BehaviorOf(w);
            if (behavior == null || _c.Ship == null || !_c.Ship.Exists) return false;
            if (w.Config.behavior == Behavior.Orbit) { _c.OrbitVolleyTimer = Rules != null ? Rules.fireAllOrbitVolleySeconds : 2f; return true; }
            behavior.FireOnce(w);
            _c.RecordEmission(w);
            QueueEchoes(w); // V5 M8z (7 Oct, Q20)
            MirrorLoadout();
            return true;
        }

        // ---- Loadout record -----------------------------------------------------------------------------------------

        /// <summary>
        /// Mirrors the record's loadout into V4 slots. A new record entry (newly equipped or replaced weapon) gets a fresh
        /// slot with no cooldown, as V3's new WeaponInstance did; upgrade values are copied every tick.
        /// </summary>
        public void SyncLoadout()
        {
            if (_loadout == null) return;
            _rebuild.Clear();
            var count = _loadout.Count;
            for (var i = 0; i < count; i++)
            {
                var e = _loadout.Get(i);
                if (e.Key == null) continue;
                if (!_byKey.TryGetValue(e.Key, out var slot))
                {
                    slot = new WeaponSlot { Key = e.Key };
                    _byKey[e.Key] = slot;
                }
                if (slot.Config == null || slot.Config.weaponId != e.WeaponId) slot.Config = FindWeapon(e.WeaponId);
                slot.Evolution = string.IsNullOrEmpty(e.EvolutionId) ? null : FindEvolution(e.EvolutionId);
                slot.Level = e.Level;
                slot.DamageBonus = e.DamageBonus;
                slot.QuantityBonus = e.QuantityBonus;
                slot.CooldownMult = e.CooldownMult;
                slot.SpeedMult = e.SpeedMult;
                slot.SizeMult = e.SizeMult;
                slot.DurationMult = e.DurationMult;
                slot.KnockbackMult = e.KnockbackMult;
                slot.SlowBonus = e.SlowBonus; // V5 M8z (7 Oct, D21)
                WeaponStatResolver.ApplyEvolution(slot); // and the reached milestones (Q20)
                _rebuild.Add(slot);
            }

            var changed = _rebuild.Count != _slots.Count;
            for (var i = 0; !changed && i < _rebuild.Count; i++) changed = !ReferenceEquals(_rebuild[i], _slots[i]);
            if (changed)
            {
                foreach (var old in _slots)
                    if (!_rebuild.Contains(old)) _byKey.Remove(old.Key);
                _slots.Clear();
                _slots.AddRange(_rebuild);
            }
        }

        void MirrorLoadout()
        {
            if (_loadout == null) return;
            var count = Mathf.Min(_loadout.Count, _slots.Count);
            for (var i = 0; i < count; i++)
            {
                var w = _slots[i];
                _loadout.Mirror(i, new LoadoutMirror
                {
                    CooldownTimer = w.CooldownTimer,
                    AwaitingBurstShot = w.BurstShotsLeft > 0,
                    StreamRemaining = w.StreamRemaining,
                    RechargeRemaining = w.RechargeRemaining,
                    StreamTickTimer = w.StreamTickTimer,
                });
            }
        }

        WeaponConfig FindWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || _config == null) return null;
            foreach (var w in _config.All<WeaponConfig>())
                if (w.weaponId == weaponId) return w;
            if (_missingConfigWarned.Add(weaponId))
                Debug.LogWarning($"V4 WeaponManager: no weapon config for '{weaponId}'; it does not fire");
            return null;
        }

        readonly Dictionary<string, WeaponEvolutionConfig> _evolutions = new Dictionary<string, WeaponEvolutionConfig>();

        /// <summary>Configs are edited in place, so the asset per id never changes during a run: looked up once.</summary>
        WeaponEvolutionConfig FindEvolution(string evolutionId)
        {
            if (_config == null) return null;
            if (_evolutions.TryGetValue(evolutionId, out var cached)) return cached;
            WeaponEvolutionConfig found = null;
            foreach (var e in _config.All<WeaponEvolutionConfig>())
                if (e.evolutionId == evolutionId) { found = e; break; }
            _evolutions[evolutionId] = found;
            return found;
        }

        /// <summary>Clears in-flight kegs, strikes, skiffs, follow-ups and projectiles (run end, Module Lab reset).</summary>
        public void ClearInFlight() => ClearInFlight(true);

        /// <summary>effects = false at teardown: nothing may be created while the scene closes.</summary>
        public void ClearInFlight(bool effects)
        {
            _c.ClearInFlight();
            _c.Projectiles?.ClearAll(effects);
            _orbit.ClearHits();
        }
    }
}

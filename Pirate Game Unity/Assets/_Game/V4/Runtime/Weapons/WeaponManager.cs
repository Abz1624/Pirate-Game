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
            _summon.TickSkiffs(dt);
            _fan.TickFollowUps(dt);
            var itemAttackSpeed = _c.Mods.ItemAttackSpeedMultiplier;
            for (var wi = 0; wi < _slots.Count; wi++)
            {
                var w = _slots[wi];
                if (w.Config == null) continue;
                if (w.Config.behavior == Behavior.ConeStream)
                {
                    _stream.TickSlot(w, dt, itemAttackSpeed);
                    continue;
                }

                w.CooldownTimer -= dt * itemAttackSpeed;
                if (w.CooldownTimer > 0f) continue;

                var cd = WeaponStatResolver.Cooldown(w, _c.Mods, itemAttackSpeed);
                if (w.BurstShotsLeft > 0)
                {
                    if (_projectile.FireBurstShot(w) == WeaponFireOutcome.Emitted)
                    {
                        w.BurstShotsLeft--;
                        w.CooldownTimer = cd;
                        _c.RecordEmission(w);
                    }
                    continue;
                }

                if (FirePattern(w) == WeaponFireOutcome.Emitted)
                {
                    w.CooldownTimer = cd;
                    _c.RecordEmission(w);
                }
            }
        }

        WeaponFireOutcome FirePattern(WeaponSlot w)
        {
            var behavior = BehaviorOf(w);
            if (behavior == null) return WeaponFireOutcome.NoTarget;
            if (!_c.Targeting.TryResolveTarget(w.Config, out _, out _)
                && behavior.NeedsTarget
                && !_c.Hooks.TryResolveShipAim(w.Id, out _))
                return WeaponFireOutcome.NoTarget;
            return behavior.Fire(w);
        }

        WeaponBehavior BehaviorOf(WeaponSlot w) => w.Config != null && _behaviors.TryGetValue(w.Config.behavior, out var b) ? b : null;

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
                    var isElite = _c.Enemies.IsElite(ai);
                    var dmg = _c.Calculator.Roll(strike.BaseDamage, strike.DamageBonus, isElite, 0f, strike.ProcCoefficient, strike.WeaponId);
                    _c.ApplyWeaponDamage(ai, strike.WeaponId, dmg, isElite, _c.Enemies.IsBoss(ai));
                }
                _c.Hooks.RecordConcurrentTargets(strike.WeaponId, hits);
                if (strike.AreaShell)
                {
                    if (strike.ImpactDraws) DrawV3ImpactNumbers();
                    _c.Effects?.AreaImpact(strike.Position, strike.Radius);
                    _c.Effects?.WaterSplash(strike.Position);
                }
                else
                    _c.Effects?.Explosion(strike.Position, strike.Radius);
                list.RemoveAt(i);
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
            _strike.FireSalvo(Rules);
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

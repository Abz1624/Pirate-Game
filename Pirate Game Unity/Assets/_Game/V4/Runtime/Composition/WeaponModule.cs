using System;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;
using RoguePirates.V4.Ports;

namespace RoguePirates.V4.Composition
{
    /// <summary>
    /// Builds the V4 weapon module over the legacy adapters (M5) and puts its ticks where V3's pieces ticked:
    /// the weapon tick in WeaponController's scene slot (file ID 658786988, set by V4SceneSetup.ApplyM5), orbit blade
    /// positions at order 50 (V3's juice controller), player projectiles where V3RunBootstrap ticked ProjectileWorld.
    /// Effects go through <see cref="WeaponEffectsForwarder"/> to the V4 weapon presenter (M6). M9: the loadout is the
    /// V4 progression's and the multipliers come from the stat resolver, Captain and item modules.
    /// </summary>
    public sealed class WeaponModule : IDisposable
    {
        /// <summary>M12: the weapon hit records (the results damage share listens to them).</summary>
        public WeaponCombatHooks Hooks { get; private set; }


        readonly RunBootstrapSequence _shell;
        readonly CombatEvents _events;
        readonly RunClock _clock;
        readonly WeaponEffectsForwarder _effects;
        readonly BuildModule _build;
        ExperimentConfig _experiment;

        public WeaponManager Manager { get; }
        /// <summary>M14.14: the run's frame (ticks register here instead of legacy update slots).</summary>
        public FramePipeline Frame { get; set; }

        public WeaponModule(RunBootstrapSequence shell, RunContext context, ConfigSetRuntime config, RunPort run, ShipPort ship, CombatEvents events, RunClock clock,
            EnemyWorldModule enemyWorld, BuildModule build, IWeaponTelemetry telemetry)
        {
            _shell = shell;
            _build = build;
            _events = events;
            _clock = clock;
            var enemies = enemyWorld.Contracts; // M8: the V4 enemy world (was LegacyWeaponEnemies over V3's EnemyWorld)
            // M6: effects go to the V4 weapon presenter the presentation root registers (was LegacyWeaponEffects).
            // M14.9b: the timed ring and the water splash too (were the kept legacy ring view and V3's JuiceVfxBroker).
            _effects = new WeaponEffectsForwarder(() => context.TryGet<IWeaponEffects>(out var fx) ? fx : null);
            var hooks = new WeaponCombatHooks(telemetry, enemyWorld.Hellfire); // M14: V4 hooks (was LegacyWeaponHooks)
            Hooks = hooks;
            var mods = build.WeaponModifiers;
            var random = run;

            var combat = new WeaponCombat
            {
                Enemies = enemies,
                Damage = enemies,
                Ship = ship,
                Mods = mods,
                Hooks = hooks,
                Effects = _effects,
                Random = random,
                Calculator = new DamageCalculator(mods, random),
                Targeting = new WeaponTargetingService(enemies, ship)
                {
                    // The asset is edited in place: look it up once, read the flag per call.
                    DirectionalTargeting = () => (_experiment ??= config?.Single<ExperimentConfig>())?.directionalWeaponTargetingEnabled ?? true,
                },
                Projectiles = new PlayerProjectiles(SlotPoolCapacities.Current.PlayerProjectiles, enemies, enemies, hooks, _effects)
                {
                    Views = () => context.TryGet<IProjectileViews>(out var views) ? views : null,
                },
                Rules = () => config?.All<WeaponRulesConfig>().FirstOrDefault(),
            };
            combat.OnEmission = PublishFired;
            Manager = new WeaponManager(combat, build.Progression.Loadout, run, config);
        }

        void PublishFired(WeaponSlot slot) =>
            _events?.Publish(new CombatEvent(CombatEventKind.Fired, _clock != null ? _clock.TickIndex : 0, _clock != null ? _clock.SimTime : 0f, tag: slot.Id));

        /// <summary>Run start, after the progression module equipped the starting weapon: bind the ticks.</summary>
        public void Bind()
        {
            Frame?.Register(FrameStep.Weapons, TickWeapons);
            Frame?.Register(FrameStep.OrbitPositions, Manager.TickOrbitPositions);
        }

        void TickWeapons(float dt)
        {
            _build.BeforeWeaponTick(); // Captain's Bell (Captain actives fire the weapons from the Captain's tick)
            Manager.Tick(dt);
        }

        /// <summary>Player projectiles, in ProjectileWorld's place in V3RunBootstrap.Update.</summary>
        public void TickProjectiles(float dt) => Manager.Projectiles.Tick(dt);

        public void Dispose()
        {
            Manager.ClearInFlight(false);
        }
    }
}

using System;
using System.Linq;
using HighSeas.Core;
using HighSeas.Weapons;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Builds the V4 weapon module over the legacy adapters (M5) and puts its ticks where V3's pieces ticked:
    /// the weapon tick in WeaponController's scene slot (file ID 658786988, set by V4SceneSetup.ApplyM5), orbit blade
    /// positions at order 50 (V3's juice controller), player projectiles where V3RunBootstrap ticked ProjectileWorld.
    /// Effects go through <see cref="WeaponEffectsForwarder"/> to the V4 weapon presenter (M6).
    /// </summary>
    [DeletedInPhase(9, "Weapons read V4 progression and V4 enemies; slots go with the legacy frame order (M8)")]
    public sealed class LegacyWeaponWiring : IDisposable
    {
        public const string WeaponsRole = "weapons";

        readonly LegacyRunShell _shell;
        readonly CombatEvents _events;
        readonly RunClock _clock;
        readonly WeaponEffectsForwarder _effects;
        readonly LegacyActiveProbe _probe;
        ExperimentConfig _experiment;

        public WeaponManager Manager { get; }

        public LegacyWeaponWiring(LegacyRunShell shell, RunContext context, ConfigSetRuntime config, IRunStatus run, CombatEvents events, RunClock clock,
            LegacyEnemyWiring enemyWorld)
        {
            _shell = shell;
            _events = events;
            _clock = clock;
            var enemies = enemyWorld.Contracts; // M8: the V4 enemy world (was LegacyWeaponEnemies over V3's EnemyWorld)
            var ship = new LegacyWeaponShip(() => _shell.Player);
            Func<string, HighSeas.Data.WeaponDefinition> definition = id => _shell.Database != null ? _shell.Database.GetWeapon(id) : null;
            // M6: effects go to the V4 weapon presenter the presentation root registers (was LegacyWeaponEffects).
            // The timed ring and the water splash stay the kept legacy views' until M14.
            _effects = new WeaponEffectsForwarder(() => context.TryGet<IWeaponEffects>(out var fx) ? fx : null,
                LegacyHazardTelegraphView.ShowTimedRing,
                position => HighSeas.VFX.JuiceVfxBroker.Instance?.PlayWaterSplash(position));
            var hooks = new LegacyWeaponHooks(() => _shell.Player, definition, enemyWorld.Hellfire);
            var mods = new LegacyWeaponModifiers();
            var random = new LegacyCombatRandom();

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
            Manager = new WeaponManager(combat, new LegacyWeaponRecord(() => _shell.Bridge.Weapons), run, config);
            _probe = new LegacyActiveProbe(Manager);
        }

        void PublishFired(WeaponSlot slot) =>
            _events?.Publish(new CombatEvent(CombatEventKind.Fired, _clock != null ? _clock.TickIndex : 0, _clock != null ? _clock.SimTime : 0f, tag: slot.Id));

        /// <summary>Run start, after the level system synced the loadout: switch the record off and bind the ticks.</summary>
        public void Bind()
        {
            _shell.Bridge.MakeRecordOnly();
            LegacyUpdateSlot slot = null;
            foreach (var s in Object.FindObjectsByType<LegacyUpdateSlot>(FindObjectsSortMode.None))
                if (s.role == WeaponsRole) { slot = s; break; }
            if (slot == null)
            {
                Debug.LogWarning("V4Game: no weapons LegacyUpdateSlot in the scene (run RoguePirates/V4/Setup M5); adding one, frame order differs from V3.");
                var host = GameObject.Find("Systems") ?? new GameObject("V4WeaponsSlot");
                slot = host.AddComponent<LegacyUpdateSlot>();
                slot.role = WeaponsRole;
            }
            slot.Tick = TickWeapons;

            if (Object.FindFirstObjectByType<LegacyOrder50Slot>() == null)
                new GameObject("V4OrbitSlot").AddComponent<LegacyOrder50Slot>().Tick = Manager.TickOrbitPositions;
        }

        void TickWeapons(float dt)
        {
            _probe.Poll();
            Manager.Tick(dt);
        }

        /// <summary>Player projectiles, in ProjectileWorld's place in V3RunBootstrap.Update.</summary>
        public void TickProjectiles(float dt) => Manager.Projectiles.Tick(dt);

        public void Dispose()
        {
            Manager.ClearInFlight(false);
            _probe.Dispose();
        }
    }
}

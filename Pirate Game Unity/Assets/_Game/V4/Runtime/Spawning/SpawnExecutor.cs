using System.Collections.Generic;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// One spawn attempt of a spawner: caps, composition, placement and the enemy-world call. Port of
    /// EncounterSpawner.TryExecuteSpawnBurst with the same checks, RNG calls and order, so traces stay comparable.
    /// </summary>
    internal sealed class SpawnExecutor
    {
        readonly SpawnServices _s;
        readonly SpawnComposition _composition;
        int _nextProfileInstanceId = 1;
        float _capBlockedAt = float.NegativeInfinity; // M11: last time the ambient cap blocked a spawn

        /// <summary>M11: ambient spawns the elite lever upgraded this run (console, tests).</summary>
        public int AdaptiveElites { get; private set; }

        public SpawnExecutor(SpawnServices services, SpawnComposition composition)
        {
            _s = services;
            _composition = composition;
        }

        public void ResetForRun()
        {
            _nextProfileInstanceId = 1;
            _capBlockedAt = float.NegativeInfinity;
            AdaptiveElites = 0;
        }

        /// <summary>M11: the ambient cap blocked a spawn now (the ambient spawner checks the cap before calling TryBurst).</summary>
        public void NoteAmbientCapBlocked(float runTime) => _capBlockedAt = runTime;

        public int NextInstanceId() => _nextProfileInstanceId++;

        /// <summary>Hard cap on alive enemies (V3: schedule maxActiveEnemies, clamped to 8..180).</summary>
        public int EnemyCap
        {
            get
            {
                var t = _s.Timeline;
                var cap = t != null && t.maxActiveEnemies > 0 ? t.maxActiveEnemies : 80;
                return Mathf.Min(Mathf.Max(cap, 8), 180);
            }
        }

        /// <summary>Ambient spawns stop here, keeping slots free for Beacon and encounter spawns.</summary>
        public int AmbientCap => Mathf.Max(1, EnemyCap - (_s.Timeline != null ? _s.Timeline.reservedEncounterSlots : 0));

        public float OrdinaryHpMultiplier(float elapsed) => Ordinary(elapsed, true);
        public float OrdinaryDamageMultiplier(float elapsed) => Ordinary(elapsed, false);

        float Ordinary(float elapsed, bool hp)
        {
            var d = _s.Difficulty;
            if (d == null || elapsed < d.ordinaryScalingDelaySeconds) return 1f;
            var minute = (elapsed - d.ordinaryScalingDelaySeconds) / 60f;
            return (hp ? d.ordinaryHp : d.ordinaryDamage).Evaluate(minute);
        }

        /// <summary>
        /// Spawns up to <paramref name="count"/> enemies from a spawner. <paramref name="compositionPhase"/> (ambient
        /// phases) picks the enemy from the phase's pools; <paramref name="ambient"/> marks ambient spawns (telegraph).
        /// Returns how many spawned.
        /// </summary>
        public int TryBurst(SpawnProfile profile, int count, bool ignoreCapSoft, float hpMultiplier, float damageMultiplier,
            int profileInstanceId, ref int leaderUnitId, TimelinePhase compositionPhase = null, bool ambient = false, string enemyId = null,
            int runEncounterId = 0)
        {
            if (profile == null || profile.enemy == null || !_s.Placement.Ready) return 0;

            var elapsed = _s.Run.ElapsedSeconds;
            hpMultiplier *= OrdinaryHpMultiplier(elapsed);
            damageMultiplier *= OrdinaryDamageMultiplier(elapsed);

            var levers = ambient && _s.Levers != null && _s.Levers.Adaptive ? _s.Levers : null; // M11: ambient spawns only
            if (!ignoreCapSoft && _s.Sink.ActiveCount >= AmbientCap)
            {
                if (ambient) _capBlockedAt = elapsed;
                return 0;
            }

            var definitionIndex = string.IsNullOrEmpty(enemyId) ? -1 : _s.Sink.FindDefinition(enemyId);
            if (definitionIndex < 0 && compositionPhase != null && compositionPhase.pools.Count > 0 && _s.Run.Exists)
            {
                var picked = _composition.Pick(compositionPhase, elapsed, _s.Run.PlayerLevel);
                if (picked != null) definitionIndex = _s.Sink.FindDefinition(picked);
            }
            if (definitionIndex < 0) definitionIndex = _s.Sink.IndexOf(profile.enemy);
            if (definitionIndex < 0) return 0;

            profileInstanceId = profileInstanceId > 0 ? profileInstanceId : NextInstanceId();
            var spawnRadius = _s.Sink.DefinitionRadius(definitionIndex) + 0.35f;
            var isBoss = _s.Sink.DefinitionIsBoss(definitionIndex);
            // Playtest (3 Oct, owner): only bosses and minibosses may appear on screen (out of their whirlpool); every
            // other spawn (ambient, encounter elites, kill-band elites, scheduled elites) is placed outside the view.
            var mayAppearOnScreen = isBoss || _s.Sink.DefinitionIsMiniboss(definitionIndex);
            var placement = mayAppearOnScreen ? profile.placement : OffScreen(profile.placement);
            // M11 lever: while the cap keeps blocking, bodies cannot be added, so new ambient spawns get more HP instead.
            if (levers != null && !isBoss && elapsed - _capBlockedAt <= levers.Config.capBlockedWindowSeconds) hpMultiplier *= levers.CapHpMultiplier;
            // Playtest round 1: every ambient spawn gets tougher toward the top of the band.
            if (levers != null && !isBoss) { hpMultiplier *= levers.AmbientHpMultiplier; damageMultiplier *= levers.AmbientDamageMultiplier; }
            var spawned = 0;
            count = Mathf.Max(1, count);
            for (var i = 0; i < count; i++)
            {
                if (_s.Sink.ActiveCount >= (ignoreCapSoft ? EnemyCap : AmbientCap))
                {
                    if (ambient) _capBlockedAt = elapsed;
                    break;
                }

                var role = ResolveFormationRole(profile, i, count);
                if (role == FormationRole.Leader) leaderUnitId = -1;

                // V3: encounters (and bosses) may fall back to spots near the ship (off screen still, for non-bosses:
                // the off-screen settings push that ring beyond the view). The whirlpool's point is the boss's alone.
                var position = default(Vector3);
                if (!(mayAppearOnScreen && SpawnPointOverride.TryTake(out position)) &&
                    !_s.Placement.TryPick(placement, spawnRadius, runEncounterId > 0 || isBoss, out position)) continue;
                _s.Placement.OffsetGroup(profile.groupLayout, i, count, placement.groupSpacing, ref position);
                if (!_s.Placement.IsFormationPositionValid(position, spawnRadius, placement)) continue;

                // M11 elite lever (its own seeded stream); a profile that is already elite stays as it is.
                var adaptiveElite = levers != null && !isBoss && !(profile.elite != null && profile.elite.isElite) && levers.RollElite();
                var spawnProfile = adaptiveElite ? _s.Profiles.EliteCopy(profile, levers.Config) : profile;
                var slot = _s.Sink.SpawnFromProfile(definitionIndex, position, spawnProfile, profileInstanceId, role, i, leaderUnitId,
                    hpMultiplier, damageMultiplier);
                if (adaptiveElite && slot >= 0) _s.Sink.MarkAdaptiveElite(slot, profile, !levers.AdaptiveElitesPayEliteRewards);
                if (slot < 0) continue;
                if (adaptiveElite) AdaptiveElites++;

                spawned++;
                var unitId = _s.Sink.UnitIdAt(slot);
                // Playtest (3 Oct, owner): bosses and the kill band's elites can never be one-shot (time-to-kill floor).
                var floorLevers = _s.Levers;
                if (floorLevers != null && floorLevers.Config != null && (isBoss || adaptiveElite))
                    _s.Sink.EnsureMinimumHp(unitId, floorLevers.PlayerDps * (isBoss ? floorLevers.Config.bossMinSeconds : floorLevers.Config.adaptiveEliteMinSeconds));
                // M6: ambient spawns are flagged; the spawn warning presenter telegraphs them (was V3SpawnTelegraph).
                _s.Publish(CombatEventKind.Spawned, unitId, definitionIndex, position, _s.Sink.DefinitionId(definitionIndex),
                    (ambient ? CombatEventFlags.AmbientSpawn : 0) | (adaptiveElite ? CombatEventFlags.AdaptiveElite : 0));
                if (runEncounterId > 0) _s.Encounters?.RegisterSpawned(runEncounterId, unitId);
                if (role == FormationRole.Leader || (role == FormationRole.Auto && i == 0)) leaderUnitId = unitId;
            }
            return spawned;
        }

        static readonly Dictionary<SpawnPlacementSettings, SpawnPlacementSettings> OffScreenCopies =
            new Dictionary<SpawnPlacementSettings, SpawnPlacementSettings>();

        /// <summary>Playtest (3 Oct, owner): these placement settings, but always outside the player's view (a cached
        /// copy when the asset allows on-screen spawns, e.g. the Powder Cache elites and the elite Kraken).</summary>
        public static SpawnPlacementSettings OffScreen(SpawnPlacementSettings settings)
        {
            if (settings == null) return new SpawnPlacementSettings();
            if (settings.requireOutsidePlayerFov) return settings;
            if (OffScreenCopies.TryGetValue(settings, out var copy)) return copy;
            copy = new SpawnPlacementSettings
            {
                requireOutsidePlayerFov = true, distanceMin = settings.distanceMin, distanceMax = settings.distanceMax,
                viewportMargin = settings.viewportMargin, positionAttempts = settings.positionAttempts,
                allowedArcDegrees = settings.allowedArcDegrees, arcYawOffset = settings.arcYawOffset,
                groupSpacing = settings.groupSpacing, fallbackPolicy = settings.fallbackPolicy,
            };
            OffScreenCopies[settings] = copy;
            return copy;
        }

        /// <summary>Playtest (3 Oct, owner): pushes a spot straight away from the ship until it is outside the view
        /// (hordes and ring spawns are placed by distance, which can still land on screen). False when no spot is clear.</summary>
        public static bool PushOffScreen(ISpawnPlacement placement, float spawnRadius, ref Vector3 position)
        {
            var player = placement.PlayerPosition;
            var offset = position - player;
            var height = position.y;
            offset.y = 0f;
            var dir = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward;
            var distance = Mathf.Max(offset.magnitude, placement.ViewEdgeDistance);
            for (var step = 0; step < 8; step++, distance += 2f)
            {
                var candidate = player + dir * distance;
                candidate.y = height;
                if (!placement.IsFormationPositionValid(candidate, spawnRadius, OffScreenSettings)) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        static readonly SpawnPlacementSettings OffScreenSettings = new SpawnPlacementSettings();

        static FormationRole ResolveFormationRole(SpawnProfile profile, int index, int count)
        {
            if (profile.specialBehavior != EnemySpecialBehavior.Formation) return FormationRole.Auto;
            if (profile.formation.role != FormationRole.Auto) return profile.formation.role;
            return index == 0 ? FormationRole.Leader : FormationRole.Follower;
        }
    }
}

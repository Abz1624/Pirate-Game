using HighSeas.Enemies;
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

        public SpawnExecutor(SpawnServices services, SpawnComposition composition)
        {
            _s = services;
            _composition = composition;
        }

        public void ResetForRun() => _nextProfileInstanceId = 1;
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
            int profileInstanceId, ref int leaderUnitId, TimelinePhase compositionPhase = null, bool ambient = false, string enemyId = null)
        {
            if (profile == null || profile.enemy == null || !_s.Placement.Ready) return 0;

            var elapsed = _s.Run.ElapsedSeconds;
            hpMultiplier *= OrdinaryHpMultiplier(elapsed);
            damageMultiplier *= OrdinaryDamageMultiplier(elapsed);

            if (!ignoreCapSoft && _s.Sink.ActiveCount >= AmbientCap) return 0;

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
            var spawned = 0;
            count = Mathf.Max(1, count);
            for (var i = 0; i < count; i++)
            {
                if (_s.Sink.ActiveCount >= (ignoreCapSoft ? EnemyCap : AmbientCap)) break;

                var role = ResolveFormationRole(profile, i, count);
                if (role == FormationRole.Leader) leaderUnitId = -1;

                if (!_s.Placement.TryPick(profile.placement, spawnRadius, isBoss, out var position)) continue;
                _s.Placement.OffsetGroup(profile.groupLayout, i, count, profile.placement.groupSpacing, ref position);
                if (!_s.Placement.IsFormationPositionValid(position, spawnRadius, profile.placement)) continue;

                var slot = _s.Sink.SpawnFromProfile(definitionIndex, position, profile, profileInstanceId, role, i, leaderUnitId,
                    hpMultiplier, damageMultiplier);
                if (slot < 0) continue;

                spawned++;
                var unitId = _s.Sink.UnitIdAt(slot);
                // M6: ambient spawns are flagged; the spawn warning presenter telegraphs them (was V3SpawnTelegraph).
                _s.Publish(CombatEventKind.Spawned, unitId, definitionIndex, position, _s.Sink.DefinitionId(definitionIndex),
                    ambient ? CombatEventFlags.AmbientSpawn : 0);
                if (role == FormationRole.Leader || (role == FormationRole.Auto && i == 0)) leaderUnitId = unitId;
            }
            return spawned;
        }

        static FormationRole ResolveFormationRole(SpawnProfile profile, int index, int count)
        {
            if (profile.specialBehavior != EnemySpecialBehavior.Formation) return FormationRole.Auto;
            if (profile.formation.role != FormationRole.Auto) return profile.formation.role;
            return index == 0 ? FormationRole.Leader : FormationRole.Follower;
        }
    }
}

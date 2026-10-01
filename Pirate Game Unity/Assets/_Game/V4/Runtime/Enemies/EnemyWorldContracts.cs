using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// The V4 enemy world behind the contracts the other modules already use (they were implemented over V3's
    /// EnemyWorld until M8, with the same semantics): spawn sink, weapon query and damage, boss control, the read-only
    /// views, dev actions and the presenters' enemy view.
    /// </summary>
    public sealed class EnemyWorldContracts : IEnemySpawnSink, IEnemyQuery, IEnemyDamageSink, IBossControl, IEnemyWorldView, IEnemyDevControl, IEnemyView
    {
        readonly EnemySimulation _sim;
        readonly EnemyStatuses _statuses;

        public EnemyWorldContracts(EnemySimulation simulation, EnemyStatuses statuses)
        {
            _sim = simulation;
            _statuses = statuses;
        }

        EnemyDefinition[] Defs => _sim.Definitions;

        EnemyDefinition At(int index)
        {
            var defs = Defs;
            return defs != null && index >= 0 && index < defs.Length ? defs[index] : null;
        }

        // ---- IEnemySpawnSink (was LegacyEnemySpawnSink) ----------------------------------------------------------------

        public int ActiveCount => _sim.ActiveCount;

        public int FindDefinition(string enemyId)
        {
            var defs = Defs;
            if (defs == null || string.IsNullOrEmpty(enemyId)) return -1;
            for (var i = 0; i < defs.Length; i++) if (defs[i] != null && defs[i].id == enemyId) return i;
            return -1;
        }

        public int IndexOf(EnemyDefinition definition)
        {
            var defs = Defs;
            if (defs == null || definition == null) return -1;
            for (var i = 0; i < defs.Length; i++) if (defs[i] == definition || defs[i]?.id == definition.id) return i;
            return -1;
        }

        public string DefinitionId(int index) => At(index)?.id;
        public bool DefinitionIsBoss(int index) => At(index) != null && At(index).isBoss;
        public float DefinitionRadius(int index) => At(index) != null ? At(index).radius : 1f;
        public float DefinitionHeightOffset(int index) => At(index) != null ? At(index).visualHeightOffset : 0f;

        public void MarkAdaptiveElite(int slot, SpawnProfile sourceProfile, bool ordinaryRewards) => _sim.MarkAdaptiveElite(slot, sourceProfile, ordinaryRewards);

        public int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId,
            FormationRole role, int formationSlot, int leaderUnitId, float hpMultiplier, float damageMultiplier) =>
            _sim.SpawnFromProfile(definitionIndex, position, profile, profileInstanceId, role, formationSlot, leaderUnitId, hpMultiplier, damageMultiplier);

        public int Spawn(int definitionIndex, Vector3 position, ConfigEnums.EliteAffix affix, float speedMultiplier, float hpMultiplier, float damageMultiplier) =>
            _sim.Spawn(definitionIndex, position, (EliteAffix)(int)affix, speedMultiplier, hpMultiplier, damageMultiplier);

        public int UnitIdAt(int slot) => slot >= 0 && slot < _sim.Agents.Length ? _sim.Agents[slot].id : -1;
        public bool IsUnitActive(int unitId) => unitId > 0 && _sim.TryGetAgentByUnitId(unitId, out _);

        public int MaxActiveUnitId()
        {
            var max = 0;
            foreach (var a in _sim.Agents) if (a.active && a.id > max) max = a.id;
            return max;
        }

        public int NewestUnitAfter(int afterUnitId, int definitionIndex)
        {
            if (definitionIndex < 0) return -1;
            var best = -1;
            foreach (var a in _sim.Agents)
                if (a.active && a.definitionIndex == definitionIndex && a.id > afterUnitId && a.id > best) best = a.id;
            return best;
        }

        public int CountActive(int definitionIndex)
        {
            if (definitionIndex < 0) return 0;
            var count = 0;
            foreach (var a in _sim.Agents) if (a.active && a.definitionIndex == definitionIndex) count++;
            return count;
        }

        public int CountActiveFromProfile(SpawnProfile profile)
        {
            if (profile == null) return 0;
            var count = 0;
            foreach (var a in _sim.Agents) if (a.active && a.spawnProfile == profile) count++;
            return count;
        }

        public float ThreatOfNewestAfter(int afterUnitId)
        {
            var best = -1;
            var threat = 0f;
            foreach (var a in _sim.Agents)
                if (a.active && a.id > afterUnitId && a.id > best) { best = a.id; threat = At(a.definitionIndex)?.threatCost ?? 0; }
            return threat;
        }

        public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier) => _sim.SetExperimentMultipliers(hpMultiplier, damageMultiplier);

        // ---- IEnemyQuery / IEnemyDamageSink (was LegacyWeaponEnemies) --------------------------------------------------

        bool IEnemyQuery.Exists => true;
        public int Capacity => _sim.Agents.Length;
        public bool IsActive(int i) => _sim.Agents[i].active;
        // Raw reads, active or not: V3 read a just-killed enemy's last position after damaging it.
        public Vector3 Position(int i) => _sim.Agents[i].position;
        // V5 M4: a slowed enemy reports its slowed velocity (aim leads and hit sampling use it).
        public Vector3 Velocity(int i) => _sim.Agents[i].velocity * _sim.SpeedMultiplier(i);
        public float Radius(int i) => _sim.Agents[i].radius;
        public float Hp(int i) => _sim.Agents[i].hp;
        public bool IsElite(int i) => _sim.Agents[i].isElite;
        public bool IsBoss(int i) => _sim.Agents[i].isBoss;
        public bool TryGetNearest(Vector3 from, float range, out int index, bool preferElite = false) => _sim.TryGetNearest(from, range, out index, preferElite);
        public int UnitId(int i) => _sim.Agents[i].id;

        /// <summary>R-40: weapon damage × this against controlled enemies (Leviathan's Tooth; 1 unless the effect is on).</summary>
        public Func<float> ControlledTargetMultiplier;

        public void ApplyDamage(int index, float amount, bool isCrit, bool showHitFeedback = true, string weaponId = null)
        {
            if (ControlledTargetMultiplier != null && _statuses != null && _statuses.IsControlled(index))
                amount *= ControlledTargetMultiplier();
            _sim.ApplyDamage(index, amount, isCrit, showHitFeedback, weaponId);
        }

        public void ApplyStatus(int index, WeaponStatus status, float strength)
        {
            var type = status switch
            {
                WeaponStatus.Burn => StatusType.Burn,
                WeaponStatus.Shock => StatusType.Shock,
                WeaponStatus.Curse => StatusType.Curse,
                _ => StatusType.Tarred,
            };
            _statuses.ApplyStatus(index, type, strength);
        }

        public void ApplySlow(int index, float fraction, float seconds) => _sim.ApplySlow(index, fraction, seconds);
        public void Knockback(int index, Vector3 from, float meters) => _sim.Knockback(index, from, meters);

        // ---- IBossControl (was LegacyBossControl) ------------------------------------------------------------------

        bool IBossControl.Exists => true;

        public bool TryFindActiveBoss(out BossSnapshot boss)
        {
            boss = default;
            boss.Slot = -1;
            if (!_sim.TryFindActiveBoss(out var slot, out var agent)) return false;
            boss.Slot = slot;
            boss.Position = agent.position;
            boss.Hp = agent.hp;
            boss.MaxHp = agent.maxHp;
            boss.MoveSpeed = agent.moveSpeed;
            boss.Damage = agent.damage;
            boss.Radius = agent.radius;
            return true;
        }

        public void SetAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced) =>
            _sim.SetBossAttackControl(slot, pauseMovement, forcedVelocity, useForced);

        public void ClearAttackControl() => _sim.ClearBossAttackControl();

        // ---- IEnemyWorldView / IEnemyDevControl ---------------------------------------------------------------------

        bool IEnemyWorldView.Exists => true;

        public bool TryGet(int slot, out EnemySnapshot enemy)
        {
            enemy = default;
            var agents = _sim.Agents;
            if (slot < 0 || slot >= agents.Length || !agents[slot].active) return false;
            var a = agents[slot];
            var def = At(a.definitionIndex);
            enemy = new EnemySnapshot
            {
                slot = slot,
                unitId = a.id,
                definitionIndex = a.definitionIndex,
                enemyId = def != null ? def.id : "?",
                position = a.position,
                velocity = a.velocity,
                radius = a.radius,
                hp = a.hp,
                maxHp = a.maxHp,
                isElite = a.isElite,
                isBoss = a.isBoss
            };
            return true;
        }

        public bool TryFindByUnitId(int unitId, out int slot) => _sim.TryGetAgentByUnitId(unitId, out slot);

        public void Kill(int slot, string weaponId)
        {
            var agents = _sim.Agents;
            if (slot < 0 || slot >= agents.Length || !agents[slot].active) return;
            _sim.ApplyDamage(slot, Mathf.Max(agents[slot].hp, agents[slot].maxHp) + 500f, false, weaponId: weaponId);
        }

        public void SetHealthFraction(int slot, float fraction)
        {
            var agents = _sim.Agents;
            if (slot < 0 || slot >= agents.Length || !agents[slot].active) return;
            agents[slot].hp = Mathf.Clamp(agents[slot].maxHp * fraction, 1f, agents[slot].maxHp);
        }

        public void DespawnAll() => _sim.DespawnAllActive();

        // ---- IEnemyView (was LegacyEnemyView) ------------------------------------------------------------------------

        public void CollectThreats(List<ThreatMarker> into)
        {
            into.Clear();
            var defs = Defs;
            if (defs == null || defs.Length == 0) return;
            var agents = _sim.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (!agent.active) continue;
                if (agent.definitionIndex < 0 || agent.definitionIndex >= defs.Length) continue;
                var def = defs[agent.definitionIndex];
                if (def == null || (!def.isBoss && !def.isMiniboss)) continue;
                into.Add(new ThreatMarker { position = agent.position, isBoss = def.isBoss });
            }
        }

        public bool IsKnownDefinition(int definitionIndex) => At(definitionIndex) != null;

        public EnemyTier TierOf(int definitionIndex) =>
            IsKnownDefinition(definitionIndex) ? EnemyTierLogic.ResolveTier(Defs[definitionIndex]) : EnemyTier.Soldier;
    }
}

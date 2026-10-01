using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>The enemy world's scene values (V3 serialized them on the EnemyWorld component in the Saltwind scene).</summary>
    [Serializable]
    public sealed class EnemyWorldSettings
    {
        public bool collideWithIslands = true;
        [Tooltip("Scene value 6 (the V3 component's code default is 10).")] public float islandAvoidanceProbeDistance = 6f;
        public float islandCollisionPadding = 0.35f;
        public float shipCollisionPadding = 0.55f;
        public float enemySeparationPadding = 0.2f;
    }

    /// <summary>
    /// The V4 enemy world (M8). Port of V3's EnemyWorld, one piece per build step; this file holds the slots, unit
    /// ids, spawning and despawning. Slots are a plain array filled lowest-free first and unit ids count up from 1 per
    /// run, incremented only when a spawn succeeds, exactly as V3 (slot order drives targeting, kill order and the
    /// Director-stream gold draws; unit ids drive orbit sides, encircle angles, flank parity and island steering).
    /// </summary>
    public sealed partial class EnemySimulation
    {
        const float EliteVisualScaleMultiplier = 2.15f;
        const float MinibossVisualScaleMultiplier = 2.85f;
        const float PressureBeatVisualScaleMultiplier = 1.35f;

        readonly IEnemyRunState _run;
        readonly IEnemyViewSink _views;
        readonly IEnemySpawnParams _params;
        readonly EnemyWorldSettings _settings;
        readonly CombatStateStore _states = new CombatStateStore();
        readonly SpatialHash _hash = new SpatialHash(4f); // V3's hash type (a plain container, no shared state)
        EnemyCombatServices _services;

        /// <summary>M10: the difficulty curve enemy HP and damage grow by (null = V3's GameMath formulas).</summary>
        public Func<RoguePirates.V4.Config.DifficultyCurveConfig> Curve;
        IEnemyTarget _target;
        IEnemyFeedback _feedback;
        IEnemyKillTargets _kills;
        /// <summary>M14: publishes the Hit event (damage, crit, position, weapon).</summary>
        public System.Action<float, bool, Vector3, string> HitPublished;

        EnemyUnit[] _agents;
        EnemyDefinition[] _definitions;
        ArchetypeWorldRules[] _rules = Array.Empty<ArchetypeWorldRules>();
        int _activeCount;
        int _nextId = 1;
        float _experimentHpMultiplier = 1f;
        float _experimentDamageMultiplier = 1f;
        int _bossControlSlot = -1;
        bool _bossPauseMovement;
        bool _bossUseForcedVelocity;
        Vector3 _bossForcedVelocity;

        /// <summary>After a slot is freed (unit id, profile): statuses, encounter mapping, the legacy spawner's count.</summary>
        public Action<int, int, SpawnProfile> Despawned;
        /// <summary>The weapon's hits do not flash the enemy (V3 skipped the highlight for Powder Kegs by id).</summary>
        public Func<string, bool> SuppressHitHighlight;
        /// <summary>After a spawn succeeded (slot).</summary>
        public Action<int> Spawned;

        public EnemySimulation(int capacity, IEnemyRunState run, IEnemyViewSink views, IEnemySpawnParams spawnParams, EnemyWorldSettings settings)
        {
            _agents = new EnemyUnit[capacity];
            _run = run;
            _views = views;
            _params = spawnParams;
            _settings = settings ?? new EnemyWorldSettings();
        }

        /// <summary>The per-run combat modules, the ship, hit feedback and kill targets (set once before the run ticks).</summary>
        public void Bind(EnemyCombatServices services, IEnemyTarget target, IEnemyFeedback feedback, IEnemyKillTargets kills)
        {
            _services = services;
            _target = target;
            _feedback = feedback;
            _kills = kills;
        }

        public EnemyCombatServices Services => _services;

        public int ActiveCount => _activeCount;
        public EnemyUnit[] Agents => _agents;
        public EnemyDefinition[] Definitions => _definitions;
        public CombatStateStore CombatStates => _states;
        public EnemyWorldSettings Settings => _settings;
        public int NextUnitId => _nextId;

        public ArchetypeWorldRules RulesFor(int definitionIndex) =>
            definitionIndex >= 0 && definitionIndex < _rules.Length && _rules[definitionIndex] != null ? _rules[definitionIndex] : NoRules;

        static readonly ArchetypeWorldRules NoRules = new ArchetypeWorldRules();

        /// <summary>
        /// Each definition's id rules from the run config: the archetype or variant whose id equals the definition's id
        /// (V3 matched the exact id, so a variant never inherits its base's rules). Null = no rules.
        /// </summary>
        public static ArchetypeWorldRules[] BuildRules(ConfigSetRuntime config, EnemyDefinition[] definitions)
        {
            var rules = new ArchetypeWorldRules[definitions != null ? definitions.Length : 0];
            if (config == null || definitions == null) return rules;
            var byId = new System.Collections.Generic.Dictionary<string, ArchetypeWorldRules>();
            foreach (var a in config.All<EnemyArchetypeConfig>()) if (!string.IsNullOrEmpty(a.enemyId) && a.world != null) byId[a.enemyId] = a.world;
            foreach (var v in config.All<EnemyVariantConfig>()) if (!string.IsNullOrEmpty(v.variantId) && v.world != null) byId[v.variantId] = v.world;
            for (var i = 0; i < definitions.Length; i++)
                if (definitions[i] != null && byId.TryGetValue(definitions[i].id, out var r)) rules[i] = r;
            return rules;
        }

        /// <summary>The run's definitions and, index-aligned, each one's id rules (null entries = no rules).</summary>
        public void SetDefinitions(EnemyDefinition[] definitions, ArchetypeWorldRules[] rules)
        {
            _definitions = definitions;
            _rules = rules ?? Array.Empty<ArchetypeWorldRules>();
            if (definitions != null) _views?.Prepare(definitions);
        }

        public void SetExperimentMultipliers(float hpMultiplier, float damageMultiplier)
        {
            _experimentHpMultiplier = Mathf.Max(0.1f, hpMultiplier);
            _experimentDamageMultiplier = Mathf.Max(0.1f, damageMultiplier);
        }

        public int CountActiveByDefinition(int definitionIndex)
        {
            var count = 0;
            for (var i = 0; i < _agents.Length; i++)
                if (_agents[i].active && _agents[i].definitionIndex == definitionIndex) count++;
            return count;
        }

        public bool TryGetAgentByUnitId(int unitId, out int agentIndex)
        {
            for (var i = 0; i < _agents.Length; i++)
            {
                if (_agents[i].active && _agents[i].id == unitId)
                {
                    agentIndex = i;
                    return true;
                }
            }
            agentIndex = -1;
            return false;
        }

        public bool TryFindActiveBoss(out int slot, out EnemyUnit agent)
        {
            slot = -1;
            agent = default;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active || !_agents[i].isBoss) continue;
                slot = i;
                agent = _agents[i];
                return true;
            }
            return false;
        }

        /// <summary>The boss director's hold on the boss (pause, or a forced velocity for the charge).</summary>
        public void SetBossAttackControl(int slot, bool pauseMovement, Vector3 forcedVelocity, bool useForced)
        {
            _bossControlSlot = slot;
            _bossPauseMovement = pauseMovement;
            _bossForcedVelocity = forcedVelocity;
            _bossUseForcedVelocity = useForced;
        }

        public void ClearBossAttackControl()
        {
            _bossControlSlot = -1;
            _bossPauseMovement = false;
            _bossUseForcedVelocity = false;
            _bossForcedVelocity = Vector3.zero;
        }

        public float GetCollisionRadiusForAgent(in EnemyUnit agent)
        {
            if (_definitions == null || agent.definitionIndex < 0 || agent.definitionIndex >= _definitions.Length)
                return agent.radius + _settings.islandCollisionPadding;
            var def = _definitions[agent.definitionIndex];
            var padding = def.category == EnemyCategory.Ship ? _settings.shipCollisionPadding : _settings.islandCollisionPadding;
            return agent.radius + padding;
        }

        public void EnsureAgentCapacity(int minimum)
        {
            minimum = Mathf.Max(64, minimum);
            if (_agents != null && _agents.Length >= minimum) return;
            var grown = new EnemyUnit[minimum];
            if (_agents != null) Array.Copy(_agents, grown, _agents.Length);
            _agents = grown;
        }

        // ---- Spawning (EnemyWorld.Spawn, SpawnFromProfile, SpawnPressureBeat) ----------------------------------------

        public int Spawn(int definitionIndex, Vector3 position, EliteAffix affix = EliteAffix.None,
            float speedMultiplier = 1f, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (_definitions == null || definitionIndex < 0 || definitionIndex >= _definitions.Length) return -1;
            var def = _definitions[definitionIndex];
            var slot = -1;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return -1;

            var rules = RulesFor(definitionIndex);
            var v3Scale = _run.EnemyVisualScale;
            var defVisualScale = def.visualScale > 0.01f ? def.visualScale : 1f;
            if (def.visualPrefab == null)
                defVisualScale = _views.FallbackVisualScale(def);
            var appearanceScale = defVisualScale * v3Scale * GetThreatVisualScaleMultiplier(def, affix);
            // V5 M3d: a type drawn larger (viewSize) is hit and collides at its drawn size (1 under the v3-parity preset).
            var gameplayRadius = def.radius * appearanceScale * (def.viewSize > 0f ? def.viewSize : 1f);
            position = PlayBounds.Clamp(position, gameplayRadius + 1f);
            position.y = def.visualHeightOffset;

            var minute = _run.Exists ? _run.ElapsedSeconds / 60f : 0f;
            var tier = _run.Exists ? _run.Tier : 1;
            var stage = _run.Exists ? _run.StageIndex : 0;
            // M10: the difficulty curve (its defaults are V3's GameMath formulas, bit for bit); V3's formula without one.
            var curve = Curve?.Invoke();
            var hp = curve != null ? Difficulty.CurveEvaluator.EnemyHp(curve, def.baseHP, minute, tier, stage) : GameMath.EnemyHP(def.baseHP, minute, tier, stage);
            var dmg = curve != null ? Difficulty.CurveEvaluator.EnemyDamage(curve, def.baseDamage, minute, tier, stage) : GameMath.EnemyDamage(def.baseDamage, minute, tier, stage);
            if (affix == EliteAffix.Armored) hp *= 1.8f;
            if (affix == EliteAffix.Swift) hp *= 0.9f;
            if (affix == EliteAffix.Rich) hp *= 1.25f;
            hp *= Mathf.Max(0.1f, hpMultiplier) * _experimentHpMultiplier;
            dmg *= Mathf.Max(0.1f, damageMultiplier) * _experimentDamageMultiplier;

            var visualScale = appearanceScale;
            var enemyId = _nextId;
            var view = _views.CreateView(def, position, visualScale, affix != EliteAffix.None, enemyId);

            var baseSpeed = def.moveSpeed * Mathf.Max(0.1f, speedMultiplier);
            if (affix == EliteAffix.Swift) baseSpeed *= 1.35f;

            _agents[slot] = new EnemyUnit
            {
                id = _nextId++,
                active = true,
                position = position,
                hp = hp,
                maxHp = hp,
                damage = dmg,
                moveSpeed = baseSpeed,
                radius = gameplayRadius,
                definitionIndex = definitionIndex,
                affix = affix,
                isElite = affix != EliteAffix.None,
                isBoss = def.isBoss,
                chargeTimer = rules.initialChargeTimer != null && rules.initialChargeTimer.enabled ? rules.initialChargeTimer.value
                    : def.behavior == EnemyBehavior.TelegraphedCharge ? (def.isMiniboss ? 1.15f : 1.5f)
                    : 2f,
                attackCooldown = GetInitialAttackCooldown(def, rules),
                swordfishSpringVisual = false,
                view = view
            };

            if (_settings.collideWithIslands && IslandObstacles.IsAvailable)
            {
                var collisionRadius = GetCollisionRadiusForAgent(_agents[slot]);
                var height = IslandObstacles.GetCollisionHeight(collisionRadius);
                _agents[slot].position = IslandObstacles.Depenetrate(position, collisionRadius, height);
                if (view != null)
                    view.position = _agents[slot].position;
            }

            if (_params.CombatEnabled && _params.HasCombatProfile(def))
            {
                _agents[slot].patternParams = _params.ResolveForDefinition(def);
                _states.GetOrCreate(_agents[slot].id, def.id, rules.initialCombatPhaseTimer);
            }

            _activeCount++;
            _agents[slot].healthBar = _views.AttachHealthBar(def, view, affix != EliteAffix.None);
            Spawned?.Invoke(slot);
            return slot;
        }

        public int SpawnFromProfile(int definitionIndex, Vector3 position, SpawnProfile profile, int profileInstanceId,
            FormationRole formationRole, int formationSlot, int formationLeaderAgentId, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (profile == null)
                return -1;

            var elite = profile.elite;
            var affix = elite.isElite ? elite.affix : EliteAffix.None;
            if (elite.isElite && affix == EliteAffix.None)
                affix = EliteAffix.Rich;

            var speedMult = profile.movement.speedMultiplier * (elite.isElite ? elite.speedMultiplier : 1f);
            var slot = Spawn(
                definitionIndex,
                position,
                affix,
                speedMult,
                hpMultiplier * (elite.isElite ? elite.hpMultiplier : 1f),
                damageMultiplier * (elite.isElite ? elite.damageMultiplier : 1f));

            if (slot < 0)
                return -1;

            var leaderUnitId = formationLeaderAgentId;
            if (formationRole == FormationRole.Leader)
                leaderUnitId = _agents[slot].id;

            var spawnedDef = _definitions[definitionIndex];
            _agents[slot].spawnProfile = profile;
            _agents[slot].patternParams = _params.Resolve(profile, spawnedDef, profileInstanceId, formationRole, formationSlot, leaderUnitId);
            if (_params.CombatEnabled && spawnedDef != null)
                _states.GetOrCreate(_agents[slot].id, spawnedDef.id, RulesFor(definitionIndex).initialCombatPhaseTimer);
            if (_agents[slot].patternParams.attackCooldown > 0f)
                _agents[slot].attackCooldown = _agents[slot].patternParams.attackCooldown * 0.5f;
            if (_agents[slot].patternParams.movementPattern == EnemyMovementPattern.TelegraphedCharge)
            {
                var chargeCooldown = _agents[slot].patternParams.chargeCooldown;
                _agents[slot].chargeTimer = chargeCooldown > 0f ? chargeCooldown : 2f;
            }

            if (elite.isElite && elite.sizeMultiplier > 0.01f)
            {
                if (_agents[slot].view != null)
                    _agents[slot].view.localScale *= elite.sizeMultiplier;
                MultiplyAgentRadius(slot, elite.sizeMultiplier);
            }

            return slot;
        }

        /// <summary>M11: marks an adaptive elite — it pays ordinary rewards when asked (playtest round 1 default: elite rewards) and counts against its source spawner (the elite
        /// copy of the profile is only used to spawn it).</summary>
        public void MarkAdaptiveElite(int slot, SpawnProfile sourceProfile, bool ordinaryRewards = true)
        {
            if (slot < 0 || slot >= _agents.Length || !_agents[slot].active) return;
            _agents[slot].ordinaryRewards = ordinaryRewards;
            _agents[slot].spawnProfile = sourceProfile;
        }

        /// <summary>Scheduled pressure-beat elite: 6× HP, 1.25× damage (V3 combat spec §9).</summary>
        public int SpawnPressureBeat(int definitionIndex, Vector3 position)
        {
            var slot = Spawn(definitionIndex, position, EliteAffix.Rich, 1f, 6f, 1.25f);
            if (slot >= 0)
            {
                if (_agents[slot].view != null)
                    _agents[slot].view.localScale *= PressureBeatVisualScaleMultiplier;
                MultiplyAgentRadius(slot, PressureBeatVisualScaleMultiplier);
            }
            return slot;
        }

        static float GetThreatVisualScaleMultiplier(EnemyDefinition def, EliteAffix affix)
        {
            if (def != null && def.isMiniboss)
                return MinibossVisualScaleMultiplier;
            if (affix != EliteAffix.None)
                return EliteVisualScaleMultiplier;
            return 1f;
        }

        void MultiplyAgentRadius(int slot, float multiplier)
        {
            if (slot < 0 || slot >= _agents.Length || !_agents[slot].active) return;
            if (multiplier <= 0.01f) return;
            var agent = _agents[slot];
            agent.radius *= multiplier;
            _agents[slot] = agent;
        }

        /// <summary>V3 GetInitialAttackCooldown; its enemy-id cases are the archetype's <c>world.initialAttackCooldown</c>.</summary>
        static float GetInitialAttackCooldown(EnemyDefinition def, ArchetypeWorldRules rules)
        {
            if (def.category != EnemyCategory.Ship &&
                def.behavior != EnemyBehavior.MaintainRange &&
                def.behavior != EnemyBehavior.FormationRanged)
                return 0f;
            if (rules.initialAttackCooldown != null && rules.initialAttackCooldown.enabled) return rules.initialAttackCooldown.value;
            return def.attackCooldown > 0f ? def.attackCooldown * 0.5f : 1f;
        }

        // ---- Despawning ------------------------------------------------------------------------------------------------

        public void DespawnAllActive()
        {
            if (_agents == null) return;
            for (var i = 0; i < _agents.Length; i++)
                if (_agents[i].active) Despawn(i);
        }

        /// <summary>Removes the active enemy with the lowest unit id (earliest spawned).</summary>
        public bool TryDespawnOldestActive(out int despawnedUnitId)
        {
            despawnedUnitId = -1;
            var oldestSlot = -1;
            var oldestId = int.MaxValue;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                if (_agents[i].id >= oldestId) continue;
                oldestId = _agents[i].id;
                oldestSlot = i;
            }
            if (oldestSlot < 0) return false;
            despawnedUnitId = oldestId;
            Despawn(oldestSlot);
            return true;
        }

        public int CullAllExceptAgent(int keepAgentId)
        {
            if (keepAgentId <= 0 || _agents == null) return 0;
            var culled = 0;
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active || _agents[i].id == keepAgentId) continue;
                Despawn(i);
                culled++;
            }
            return culled;
        }

        public bool DespawnByUnitId(int unitId)
        {
            if (!TryGetAgentByUnitId(unitId, out var index)) return false;
            Despawn(index);
            return true;
        }

        /// <summary>
        /// V3 Despawn order: view and bar released, per-unit combat state cleared, slot freed, then the listeners
        /// (statuses by slot, encounter mapping, the legacy spawner's count) in V3's order.
        /// </summary>
        public void Despawn(int index)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].active) return;

            var despawnProfile = _agents[index].spawnProfile;
            var def = _definitions[_agents[index].definitionIndex];
            _views.Release(def, _agents[index].view, _agents[index].healthBar);
            _agents[index].healthBar = null;

            var releasedAgentId = _agents[index].id;
            ClearUnitState(releasedAgentId);
            _states.Clear(releasedAgentId);
            _agents[index].active = false;
            _activeCount--;
            Despawned?.Invoke(index, releasedAgentId, despawnProfile);
        }

        /// <summary>Per-unit state of the ported behaviours (Reef Crab miniboss state; later steps add theirs).</summary>
        partial void ClearUnitState(int unitId);
    }
}

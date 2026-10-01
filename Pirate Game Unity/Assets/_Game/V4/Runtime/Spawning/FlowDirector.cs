using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>V5 M1: what the enemy flow is doing now (console, tests, telemetry).</summary>
    public interface IFlowStatus
    {
        bool FlowEnabled { get; }
        /// <summary>Index of the current minute wave, or -1 while the flow is not running (arena, overtime, boss-free pauses).</summary>
        int WaveIndex { get; }
        /// <summary>The minimum-alive floor in force now (after release, lull, boss-fight and adaptive multipliers); 0 while not running.</summary>
        int Floor { get; }
        bool InRelease { get; }
        float ReleaseRemaining { get; }
        /// <summary>Enemies spawned this run to keep the floor.</summary>
        int ToppedUp { get; }
        /// <summary>Hordes that spawned at least one enemy.</summary>
        int HordesSpawned { get; }
    }

    /// <summary>
    /// V5 M1 enemy flow, layered on the V4 ambient bands (which keep running at their rates):
    /// <list type="bullet">
    /// <item>Minute waves: the current wave multiplies the band's rate and sets a minimum-alive floor. While fewer enemies
    /// are alive than the floor, one enemy from the current band (same spawner, pools, caps and placement) is added every
    /// global spawn gap, at most maxTopUpPerTick per top-up interval. The floor follows the adaptive threat-rate lever and
    /// the experiment's spawn density, so weak builds get relief.</item>
    /// <item>Hordes: telegraphed formations (ring, line, cluster) of weak, fast enemies, formed just beyond the camera's
    /// view with the island and bounds checks on; a lull before each one makes room; skipped during the boss fight, held
    /// while an arena or a release window is open, dropped when more than 30 s late.</item>
    /// <item>Release windows: after an arena ends, after the boss is sunk and a while after each horde, the rate and the
    /// floor back off; during the boss fight the floor is lowered.</item>
    /// </list>
    /// Off (the whole class does nothing) unless <see cref="FlowSettings.enabled"/>; the v3-parity preset turns it off.
    /// </summary>
    internal sealed class FlowDirector : IFlowStatus
    {
        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        readonly AmbientSpawner _ambient;

        float _intervalEndsAt;
        int _intervalBudget;
        float _nextTopUpAt;
        float _releaseUntil = float.NegativeInfinity;
        bool _arenaWasActive;
        readonly HashSet<string> _hordesDone = new HashSet<string>();
        readonly Dictionary<string, float> _hordeAngles = new Dictionary<string, float>();
        readonly List<(float start, float duration)> _pendingReleases = new List<(float, float)>();
        float _now;

        /// <summary>A horde more than this late (held back by an arena or a release, or the clock jumped past it) is skipped.</summary>
        const float HordeLateSeconds = 30f;

        /// <summary>Hordes use placement checks for bounds and islands only (they form beyond the view by distance).</summary>
        static readonly SpawnPlacementSettings HordePlacement = new SpawnPlacementSettings { requireOutsidePlayerFov = false };

        public FlowDirector(SpawnServices services, SpawnExecutor executor, AmbientSpawner ambient)
        {
            _s = services;
            _executor = executor;
            _ambient = ambient;
        }

        FlowSettings Flow => _s.Timeline?.flow;
        public bool FlowEnabled => Flow != null && Flow.enabled;
        public int WaveIndex { get; private set; } = -1;
        public int Floor { get; private set; }
        public bool InRelease => _now < _releaseUntil;
        public float ReleaseRemaining => Mathf.Max(0f, _releaseUntil - _now);
        public int ToppedUp { get; private set; }
        public int HordesSpawned { get; private set; }

        public void BeginRun()
        {
            _intervalEndsAt = 0f;
            _intervalBudget = 0;
            _nextTopUpAt = 0f;
            _releaseUntil = float.NegativeInfinity;
            _arenaWasActive = false;
            _hordesDone.Clear();
            _hordeAngles.Clear();
            _pendingReleases.Clear();
            _now = 0f;
            WaveIndex = -1;
            Floor = 0;
            ToppedUp = 0;
            HordesSpawned = 0;
        }

        /// <summary>
        /// Multiplier on the ambient spawn rate: the current wave's, and the release multiplier while a release window or a
        /// pre-horde lull is open.
        /// </summary>
        public float RateMultiplier(float now)
        {
            if (!FlowEnabled) return 1f;
            var flow = Flow;
            var multiplier = flow.Sample(now, out _, out var rate) ? Mathf.Max(0f, rate) : 1f;
            if (flow.release != null && (now < _releaseUntil || InLull(now))) multiplier *= Mathf.Max(0f, flow.release.rateMultiplier);
            return multiplier;
        }

        public void OnEvents(IReadOnlyList<CombatEvent> events)
        {
            if (!FlowEnabled) return;
            for (var i = 0; i < events.Count; i++)
                if (events[i].Kind == CombatEventKind.BossState && events[i].A == 2) // the Barnacle King was sunk
                    StartRelease(events[i].SimTime, Flow.release?.afterBossKillSeconds ?? 0f);
        }

        /// <summary>
        /// Called every ambient frame before the arena and deadline checks, so an arena ending is noticed even while
        /// ambient spawning is paused. The status reads "not running" until <see cref="Tick"/> sets it this frame.
        /// </summary>
        public void Observe(float now, bool arenaActive)
        {
            _now = now;
            WaveIndex = -1;
            Floor = 0;
            if (!FlowEnabled) { _arenaWasActive = arenaActive; return; }
            if (_arenaWasActive && !arenaActive) StartRelease(now, Flow.release?.afterArenaSeconds ?? 0f);
            _arenaWasActive = arenaActive;
            for (var i = _pendingReleases.Count - 1; i >= 0; i--)
            {
                if (now < _pendingReleases[i].start) continue;
                StartRelease(now, _pendingReleases[i].duration);
                _pendingReleases.RemoveAt(i);
            }
        }

        void StartRelease(float now, float seconds)
        {
            if (seconds <= 0f) return;
            _releaseUntil = Mathf.Max(_releaseUntil, now + seconds);
        }

        /// <summary>A horde is due within the lull lead (not yet spawned, not skipped).</summary>
        bool InLull(float now)
        {
            var flow = Flow;
            var lead = flow?.release != null ? flow.release.preHordeLullSeconds : 0f;
            if (lead <= 0f) return false;
            foreach (var h in flow.hordes)
                if (h != null && !_hordesDone.Contains(h.hordeId) && now >= h.timeSeconds - lead && now < h.timeSeconds) return true;
            return false;
        }

        /// <summary>
        /// Hordes and the floor. Called from the ambient slot only when ambient spawning may run (no arena, before the
        /// stage deadline, past the grace, placement ready).
        /// </summary>
        public void Tick(float now, bool bossFight)
        {
            _now = now;
            if (!FlowEnabled) return;
            TickHordes(now, bossFight);
            TickFloor(now, bossFight);
        }

        // ---- Floor -------------------------------------------------------------------------------------------

        void TickFloor(float now, bool bossFight)
        {
            var flow = Flow;
            var wave = flow.WaveAt(now, out var index);
            WaveIndex = index;
            if (wave == null) { Floor = 0; return; }

            flow.Sample(now, out var share, out _); // V5 M3d: between keyframes when smoothWaves is on
            var release = flow.release;
            if (release != null && (now < _releaseUntil || InLull(now))) share *= release.floorMultiplier;
            if (release != null && bossFight) share *= release.bossFightFloorMultiplier;
            // The adaptive threat-rate lever (0.85–1.75; 1 unless Adaptive) and the experiment's density scale the floor too.
            if (_s.Levers != null && _s.Levers.Adaptive) share *= _s.Levers.ThreatRateMultiplier;
            var experiment = _s.Experiment;
            if (experiment != null) share *= Mathf.Max(0.1f, experiment.spawnDensityMultiplier);
            Floor = Mathf.Min(_executor.AmbientCap, Mathf.RoundToInt(share * _executor.AmbientCap));

            if (now >= _intervalEndsAt)
            {
                _intervalEndsAt = now + Mathf.Max(0.05f, wave.topUpIntervalSeconds);
                _intervalBudget = Mathf.Max(1, wave.maxTopUpPerTick);
            }
            if (_intervalBudget <= 0 || now < _nextTopUpAt) return;
            if (_s.Sink.ActiveCount >= Floor) return;
            if (!_ambient.TryGetCurrent(_s.Timeline, now, out var phase, out var profile, out var instanceId)) return;

            var leader = -1;
            var spawned = _executor.TryBurst(profile, 1, false, 1f, 1f, instanceId, ref leader, phase, ambient: true);
            // One enemy per global spawn gap (the V4 spacing between spawns), whether or not this one fit.
            var timeline = _s.Timeline;
            _nextTopUpAt = now + (timeline != null ? timeline.globalMinSpawnGapSeconds : 0.35f);
            if (spawned <= 0) return; // cap or no placement: try again after the gap
            _intervalBudget -= spawned;
            ToppedUp += spawned;
        }

        // ---- Hordes ------------------------------------------------------------------------------------------

        void TickHordes(float now, bool bossFight)
        {
            var hordes = Flow.hordes;
            for (var i = 0; i < hordes.Count; i++)
            {
                var horde = hordes[i];
                if (horde == null || _hordesDone.Contains(horde.hordeId)) continue;
                if (horde.warningLeadSeconds > 0f && !_hordeAngles.ContainsKey(horde.hordeId) && !bossFight &&
                    now >= horde.timeSeconds - horde.warningLeadSeconds && now < horde.timeSeconds)
                    Warn(horde);
                if (now < horde.timeSeconds) continue;
                if (bossFight)
                {
                    Skip(horde, "boss_fight"); // like swarm pulses: never during the boss fight
                    continue;
                }
                if (now - horde.timeSeconds > HordeLateSeconds)
                {
                    Skip(horde, "late"); // held too long by an arena or a release, or the clock jumped past it
                    continue;
                }
                if (now < _releaseUntil) continue; // wait for the breathing room to end
                _hordesDone.Add(horde.hordeId);
                var spawned = SpawnHorde(horde, now);
                _s.Feedback.Telemetry(spawned > 0 ? "horde_spawned" : "horde_empty", new Dictionary<string, object> { ["horde"] = horde.hordeId, ["count"] = spawned });
                if (spawned <= 0) continue;
                HordesSpawned++;
                var release = Flow.release;
                if (release != null && release.afterHordeSeconds > 0f)
                    _pendingReleases.Add((now + release.hordeReleaseDelaySeconds, release.afterHordeSeconds));
            }
        }

        void Skip(HordeEvent horde, string reason)
        {
            _hordesDone.Add(horde.hordeId);
            _s.Feedback.Telemetry("horde_skipped", new Dictionary<string, object> { ["horde"] = horde.hordeId, ["reason"] = reason });
        }

        float AngleOf(HordeEvent horde)
        {
            if (!_hordeAngles.TryGetValue(horde.hordeId, out var angle))
                _hordeAngles[horde.hordeId] = angle = _s.Random.DirectorFloat() * Mathf.PI * 2f;
            return angle;
        }

        void Warn(HordeEvent horde)
        {
            var angle = AngleOf(horde);
            if (!string.IsNullOrEmpty(horde.warningToast)) _s.Feedback.Toast(horde.warningToast, horde.warningLeadSeconds);
            // The direction the horde will come from: the HUD shows an edge marker and a minimap ping there.
            var centre = _s.Placement.PlayerPosition + HordeCentre(horde, angle, Distance(horde));
            _s.Publish(CombatEventKind.Warning, 0, 0, centre, "horde:" + horde.hordeId, CombatEventFlags.HordeWarning);
        }

        float Distance(HordeEvent horde) => Mathf.Max(horde.distance, _s.Placement.ViewEdgeDistance + 2f);

        int SpawnHorde(HordeEvent horde, float now)
        {
            if (horde.archetype == null || !_s.Placement.Ready) return 0;
            var index = _s.Sink.FindDefinition(horde.archetype.enemyId);
            if (index < 0) return 0;
            var radius = _s.Sink.DefinitionRadius(index) + 0.35f;
            var hp = horde.hpMultiplier * _executor.OrdinaryHpMultiplier(now);
            var damage = horde.damageMultiplier * _executor.OrdinaryDamageMultiplier(now);
            var player = _s.Placement.PlayerPosition;
            var angle = AngleOf(horde);
            var distance = Distance(horde);
            var shaped = new HordeEvent { formation = horde.formation, count = horde.count, distance = distance, spacing = horde.spacing };
            // Hordes may use half of the slots reserved for encounters (the Beacon and Powder Cache keep the other half).
            var timeline = _s.Timeline;
            var cap = Mathf.Min(_executor.EnemyCap, _executor.AmbientCap + (timeline != null ? timeline.reservedEncounterSlots / 2 : 0));
            var spawned = 0;
            for (var i = 0; i < horde.count; i++)
            {
                if (_s.Sink.ActiveCount >= cap) break;
                var position = player + Offset(shaped, i, angle);
                position.y = _s.Sink.DefinitionHeightOffset(index);
                if (!_s.Placement.IsFormationPositionValid(position, radius, HordePlacement)) continue; // islands, bounds
                var slot = _s.Sink.Spawn(index, position, ConfigEnums.EliteAffix.None, horde.speedMultiplier, hp, damage);
                if (slot < 0) continue;
                spawned++;
                _s.Publish(CombatEventKind.Spawned, _s.Sink.UnitIdAt(slot), index, position, horde.archetype.enemyId, CombatEventFlags.AmbientSpawn);
            }
            return spawned;
        }

        /// <summary>Where a horde's formation is centred, relative to the ship.</summary>
        public static Vector3 HordeCentre(HordeEvent horde, float angle, float distance) =>
            new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance; // a ring's marker points at its first member

        /// <summary>Offset of the i-th horde member from the ship.</summary>
        public static Vector3 Offset(HordeEvent horde, int i, float angle)
        {
            var count = Mathf.Max(1, horde.count);
            switch (horde.formation)
            {
                case HordeFormation.Ring:
                {
                    var a = angle + i * (Mathf.PI * 2f / count);
                    return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * horde.distance;
                }
                case HordeFormation.Line:
                {
                    var forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var across = new Vector3(-forward.z, 0f, forward.x);
                    return forward * horde.distance + across * ((i - (count - 1) * 0.5f) * horde.spacing);
                }
                default: // Cluster: a sunflower spiral around a point on one side
                {
                    var centre = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * horde.distance;
                    var r = horde.spacing * 0.75f * Mathf.Sqrt(i);
                    var a = i * 2.39996323f;
                    return centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                }
            }
        }
    }
}

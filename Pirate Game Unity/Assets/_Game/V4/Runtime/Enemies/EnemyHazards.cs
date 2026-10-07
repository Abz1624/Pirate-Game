using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>The player ship as the enemy simulation sees it (V3 read PlayerShipController/DamageReceiver directly).</summary>
    public interface IEnemyTarget
    {
        bool Exists { get; }
        Vector3 Position { get; }
        /// <summary>Horizontal velocity (y = 0).</summary>
        Vector3 Velocity { get; }
        /// <summary>The ship's island collision radius, used as its contact radius (V3 default 1.15 without a ship).</summary>
        float ContactRadius { get; }
        bool HasDamageReceiver { get; }
        void TakeDamage(float amount, Vector3 source, bool isCrit);
        void ApplySlow(float speedMultiplier, float seconds);
    }

    /// <summary>Enemy-caused effects that are presentation today (V3 SaltwindCombatEffects.PlayExplosion).</summary>
    public interface IEnemyEffects
    {
        void PlayExplosion(Vector3 position, float blastRadius);
    }

    /// <summary>
    /// Charge / area-denial / slow budgets (port of SaltwindCombatEncounterBudget, per run instead of static). V3
    /// quirks kept: charges are never released by the charge code, area denial is never released (R-06; the fix is
    /// <see cref="TickAreaDenialRelease"/>), and the charge reservation's result is ignored.
    /// </summary>
    public sealed class EncounterBudget
    {
        /// <summary>Shortest charge telegraph: long enough to read and dodge, short enough that chargers do not idle
        /// (demo playtest: the 1 s floor made the hammerhead pause too long).</summary>
        public const float MinChargeWarning = 0.6f;
        public const float MinExplosiveWarning = 1.2f;

        readonly System.Func<int> _maxCharges;
        readonly System.Func<int> _maxAreaDenial;
        int _activeCharges;
        int _activeAreaDenial;
        int _activeSlowSources;

        /// <param name="maxCharges">V3: the stage profile's MaxSimultaneousChargeWindups (2 without a profile).</param>
        /// <param name="maxAreaDenial">V3: the stage profile's MaxSimultaneousAreaDenial (2 without a profile).</param>
        public EncounterBudget(System.Func<int> maxCharges, System.Func<int> maxAreaDenial)
        {
            _maxCharges = maxCharges;
            _maxAreaDenial = maxAreaDenial;
        }

        public int ActiveCharges => _activeCharges;
        public int ActiveAreaDenial => _activeAreaDenial;
        public int ActiveSlowSources => _activeSlowSources;

        public void ResetForRun()
        {
            _activeCharges = 0;
            _activeAreaDenial = 0;
            _activeSlowSources = 0;
            _areaDenialReleaseAt.Clear();
            _areaDenialTracked = 0;
        }

        int MaxCharges => _maxCharges != null ? _maxCharges() : 2;
        int MaxAreaDenial => _maxAreaDenial != null ? _maxAreaDenial() : 2;

        public bool TryReserveCharge(int attackId)
        {
            if (_activeCharges >= MaxCharges) return false;
            _activeCharges++;
            return true;
        }

        public void ReleaseCharge() => _activeCharges = Mathf.Max(0, _activeCharges - 1);

        public bool TryReserveAreaDenial(int attackId)
        {
            if (_activeAreaDenial >= MaxAreaDenial) return false;
            _activeAreaDenial++;
            return true;
        }

        readonly System.Collections.Generic.Queue<float> _areaDenialReleaseAt = new System.Collections.Generic.Queue<float>();
        int _areaDenialTracked;

        /// <summary>
        /// R-06 fix (was LegacyAreaDenialRelease on V3's static budget, M3-M7): each area-denial reservation is freed <paramref name="holdSeconds"/>
        /// after it was seen; 0 keeps V3's never-released budget (V3's behaviour, by config; V5 M8z (7 Oct, review)). Called once per frame where the
        /// legacy release ran (after the enemy frame), with the run clock.
        /// </summary>
        public void TickAreaDenialRelease(float runSeconds, float holdSeconds)
        {
            var active = _activeAreaDenial;
            if (active < _areaDenialTracked) { _areaDenialReleaseAt.Clear(); _areaDenialTracked = 0; }
            for (; _areaDenialTracked < active; _areaDenialTracked++) _areaDenialReleaseAt.Enqueue(runSeconds + holdSeconds);
            if (holdSeconds <= 0f) return;
            while (_areaDenialReleaseAt.Count > 0 && _areaDenialReleaseAt.Peek() <= runSeconds)
            {
                _areaDenialReleaseAt.Dequeue();
                _areaDenialTracked--;
                active = Mathf.Max(0, active - 1);
            }
            _activeAreaDenial = active;
        }

        public bool TryReserveRanged(int attackId) => true;

        public bool TryReserveSlow(int attackId)
        {
            if (_activeSlowSources >= 1) return false;
            _activeSlowSources++;
            return true;
        }

        public void ReleaseSlow() => _activeSlowSources = Mathf.Max(0, _activeSlowSources - 1);
    }

    /// <summary>
    /// Telegraphed hazard circles: Kraken trap explosions, Net Cutter slow nets and Powder Barge mines (port of
    /// SaltwindHazardTelegraph, per run), and visual-only attack warnings in slots of their own. Slots created lazily and
    /// filled lowest-free first; a reused slot keeps
    /// the fields its new kind does not set, as in V3. V3 cached the player statically, so from the second run in a
    /// process its hazards never hurt (R-99); here the target is the run's ship.
    /// </summary>
    public sealed class HazardTelegraphs : IHazardView
    {
        sealed class PendingHazard
        {
            public bool active;
            public HazardKind kind;
            public Vector3 center;
            public float radius;
            public float timer;
            public float damage;
            public float slowStrength;
            public float slowDuration;
            public bool armed;
            public float duration;
            public Vector3 direction;
            public float length;
            public int owner;
        }

        // Playtest (3 Oct, owner, round 7): 48 slots (was 24): every telegraphed enemy attack now has a ground warning.
        // V5 M8z (6 Oct, owner): the visual-only warnings have their own slots, so a full set of warnings never drops a
        // damaging circle, mine or net: slots [0, DamagingSlots) are damaging, the rest are warnings.
        public const int DamagingSlots = 32, WarningSlots = 48;
        readonly PendingHazard[] _slots = new PendingHazard[DamagingSlots + WarningSlots];
        readonly IEnemyTarget _player;
        readonly IEnemyEffects _effects;

        public HazardTelegraphs(IEnemyTarget player, IEnemyEffects effects)
        {
            _player = player;
            _effects = effects;
        }

        public int Capacity => _slots.Length;

        public bool TryGet(int index, out HazardSnapshot hazard)
        {
            hazard = default;
            if (index < 0 || index >= _slots.Length) return false;
            var slot = _slots[index];
            if (slot == null || !slot.active) return false;
            hazard = new HazardSnapshot
            {
                kind = slot.kind, center = slot.center, radius = slot.radius, timer = slot.timer, armed = slot.armed,
                duration = slot.duration, direction = slot.direction, length = slot.length,
            };
            return true;
        }

        public HazardKind KindAt(int index) => _slots[index] != null ? _slots[index].kind : HazardKind.Explosion;

        public void Tick(float dt)
        {
            var player = _player != null && _player.Exists ? _player : null;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.active) continue;
                if (slot.kind == HazardKind.Mine)
                {
                    if (!slot.armed)
                    {
                        slot.timer -= dt;
                        if (slot.timer <= 0f)
                            slot.armed = true;
                    }
                    else
                    {
                        slot.slowDuration -= dt;
                        var playerPos = player != null ? player.Position : Vector3.zero;
                        playerPos.y = 0f;
                        var hit = player != null && Vector3.Distance(slot.center, playerPos) <= slot.radius;
                        if (hit || slot.slowDuration <= 0f)
                        {
                            if (hit && player.HasDamageReceiver)
                                player.TakeDamage(slot.damage, slot.center, false);
                            _effects?.PlayExplosion(slot.center, slot.radius);
                            slot.active = false;
                        }
                    }

                    continue;
                }

                slot.timer -= dt;
                if (slot.kind == HazardKind.Warning || slot.kind == HazardKind.WarningLine)
                {
                    if (slot.timer <= 0f) slot.active = false; // visual only: the attacker deals its own damage
                    continue;
                }
                if (slot.kind == HazardKind.NetSlow && slot.timer <= 0f)
                {
                    slot.active = false;
                    continue;
                }

                if (slot.timer > 0f) continue;

                if (player != null)
                {
                    var playerPos = player.Position;
                    playerPos.y = 0f;
                    if (Vector3.Distance(slot.center, playerPos) <= slot.radius)
                    {
                        if (slot.kind == HazardKind.NetSlow)
                            player.ApplySlow(1f - slot.slowStrength, slot.slowDuration);
                        else if (player.HasDamageReceiver)
                            player.TakeDamage(slot.damage, slot.center, false);
                    }
                }

                // Playtest (3 Oct, owner, round 7: "I could see the powder keg explosion smoke on the player ship but not
                // what caused it"): a trap or bomb circle always bursts where its ring was, hit or miss.
                if (slot.kind == HazardKind.Explosion) _effects?.PlayExplosion(slot.center, slot.radius);
                slot.active = false;
            }
        }

        public void SpawnNetSegment(Vector3 center, float length, float activeSeconds, float slowStrength, float slowDuration)
        {
            center.y = 0f;
            for (var i = 0; i < DamagingSlots; i++)
            {
                _slots[i] ??= new PendingHazard();
                if (_slots[i].active) continue;
                _slots[i].active = true;
                _slots[i].kind = HazardKind.NetSlow;
                _slots[i].center = center;
                _slots[i].radius = length * 0.5f;
                _slots[i].timer = activeSeconds;
                _slots[i].slowStrength = slowStrength;
                _slots[i].slowDuration = slowDuration;
                _slots[i].duration = activeSeconds;
                _slots[i].owner = 0;
                return;
            }
        }

        public void SpawnMine(Vector3 center, float armSeconds, float lifeSeconds, float damage)
        {
            center.y = 0f;
            for (var i = 0; i < DamagingSlots; i++)
            {
                _slots[i] ??= new PendingHazard();
                if (_slots[i].active) continue;
                _slots[i].active = true;
                _slots[i].kind = HazardKind.Mine;
                _slots[i].center = center;
                _slots[i].radius = 2.2f;
                _slots[i].timer = armSeconds;
                _slots[i].damage = damage;
                _slots[i].armed = false;
                _slots[i].slowDuration = lifeSeconds;
                _slots[i].duration = armSeconds;
                _slots[i].owner = 0;
                return;
            }
        }

        public void SpawnCircle(Vector3 center, float radius, float fuseSeconds, float damage)
        {
            center.y = 0f;
            fuseSeconds = Mathf.Max(fuseSeconds, EncounterBudget.MinExplosiveWarning);
            for (var i = 0; i < DamagingSlots; i++)
            {
                _slots[i] ??= new PendingHazard();
                if (_slots[i].active) continue;
                _slots[i].active = true;
                _slots[i].kind = HazardKind.Explosion;
                _slots[i].center = center;
                _slots[i].radius = radius;
                _slots[i].timer = fuseSeconds;
                _slots[i].damage = damage;
                _slots[i].duration = fuseSeconds;
                _slots[i].owner = 0;
                return;
            }
        }

        /// <summary>
        /// Playtest (3 Oct, owner, round 7): a visual-only warning circle on the water for <paramref name="seconds"/> (its
        /// inner ring fills as the attack comes). With an <paramref name="owner"/> (the unit id) a later call moves and
        /// re-times that unit's warning instead of adding another, so a warning can follow a moving attacker.
        /// </summary>
        public void Warn(Vector3 center, float radius, float seconds, int owner = 0) =>
            Place(HazardKind.Warning, center, Vector3.zero, 0f, radius, seconds, owner);

        /// <summary>Playtest (3 Oct, owner, round 7): a visual-only warning lane from <paramref name="start"/> along
        /// <paramref name="direction"/> (a charge's path, an aimed cannonball's line).</summary>
        public void WarnLine(Vector3 start, Vector3 direction, float length, float halfWidth, float seconds, int owner = 0)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || length <= 0.05f) return;
            Place(HazardKind.WarningLine, start, direction.normalized, length, halfWidth, seconds, owner);
        }

        /// <summary>Removes a unit's warnings (its attack ended early: it died, exploded or gave up).</summary>
        public void ClearWarnings(int owner)
        {
            if (owner == 0) return;
            foreach (var slot in _slots)
                if (slot != null && slot.active && slot.owner == owner && (slot.kind == HazardKind.Warning || slot.kind == HazardKind.WarningLine))
                    slot.active = false;
        }

        void Place(HazardKind kind, Vector3 center, Vector3 direction, float length, float radius, float seconds, int owner)
        {
            if (seconds <= 0f) return;
            center.y = 0f;
            PendingHazard target = null;
            if (owner != 0)
                for (var i = DamagingSlots; i < _slots.Length && target == null; i++)
                {
                    var slot = _slots[i];
                    if (slot != null && slot.active && slot.owner == owner && slot.kind == kind) target = slot;
                }
            var fresh = target == null;
            if (fresh)
                for (var i = DamagingSlots; i < _slots.Length && target == null; i++)
                {
                    _slots[i] ??= new PendingHazard();
                    if (!_slots[i].active) target = _slots[i];
                }
            if (target == null) return;
            target.active = true;
            target.kind = kind;
            target.center = center;
            target.direction = direction;
            target.length = length;
            target.radius = radius;
            target.timer = seconds;
            target.owner = owner;
            target.damage = 0f;
            target.armed = false;
            if (fresh || seconds > target.duration) target.duration = seconds;
        }

        public void ClearAll()
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i] != null)
                    _slots[i].active = false;
        }
    }
}

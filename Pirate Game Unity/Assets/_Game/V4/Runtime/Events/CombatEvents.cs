using System;
using System.Collections.Generic;

namespace RoguePirates.V4.Events
{
    public enum CombatEventKind
    {
        Spawned,
        Hit,
        Killed,
        Fired,
        PlayerDamaged,
        PlayerHealed,
        PhaseChanged,
        Warning,
        LevelUp,
        OffersShown,
        OfferChosen,
        Pickup,
        ItemGained,
        PowerupCollected,
        ChestOpened,
        LandmarkUsed,
        BeaconState,
        BossState,
        RunState,
        GoldChanged,
        PowerChanged,
        RumChanged,
        ConfigChanged
    }

    /// <summary>One fact from the simulation. Small value type: ids in A/B, numbers in X/Y/Z, a string tag for content ids.</summary>
    public readonly struct CombatEvent
    {
        public readonly CombatEventKind Kind;
        public readonly long Tick;
        public readonly float SimTime;
        public readonly int A;
        public readonly int B;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly string Tag;
        /// <summary>Kind-specific flags (<see cref="CombatEventFlags"/>).</summary>
        public readonly int Flags;

        public CombatEvent(CombatEventKind kind, long tick, float simTime, int a = 0, int b = 0,
            float x = 0f, float y = 0f, float z = 0f, string tag = null, int flags = 0)
        {
            Flags = flags;
            Kind = kind;
            Tick = tick;
            SimTime = simTime;
            A = a;
            B = b;
            X = x;
            Y = y;
            Z = z;
            Tag = tag;
        }

        public override string ToString() => $"{Kind}@{SimTime:0.###} a={A} b={B} tag={Tag}";
    }

    public static class CombatEventFlags
    {
        /// <summary>Spawned: an ambient (timeline) spawn. Only these telegraph on the HUD, as V3's spawner did.</summary>
        public const int AmbientSpawn = 1;
    }

    public interface ICombatEventWriter
    {
        void Publish(in CombatEvent evt);
    }

    public interface ICombatEventReader
    {
        /// <summary>
        /// Called once per simulation tick with that tick's events, in publish order. Legacy producers that publish
        /// outside a tick (their own Update, or while paused) are delivered in an extra batch at the end of the frame,
        /// stamped with the current tick. The list is only valid during the call; copy what you keep.
        /// </summary>
        event Action<IReadOnlyList<CombatEvent>> TickPublished;
        /// <summary>The most recent batch; valid until the next EndTick (the buffer is reused).</summary>
        IReadOnlyList<CombatEvent> LastTick { get; }
        long TotalPublished { get; }
    }

    /// <summary>
    /// Per-tick event buffer. Producers publish during a tick; <see cref="EndTick"/> hands the batch to readers in
    /// publish order and starts the next batch. Readers must not publish from their handlers.
    /// </summary>
    public sealed class CombatEvents : ICombatEventWriter, ICombatEventReader
    {
        List<CombatEvent> _current = new List<CombatEvent>(256);
        List<CombatEvent> _published = new List<CombatEvent>(256);
        bool _dispatching;

        public event Action<IReadOnlyList<CombatEvent>> TickPublished;
        public IReadOnlyList<CombatEvent> LastTick => _published;
        public IReadOnlyList<CombatEvent> Pending => _current;
        public long TotalPublished { get; private set; }

        public void Publish(in CombatEvent evt)
        {
            if (_dispatching) throw new InvalidOperationException("CombatEvents readers must not publish while a tick is being dispatched.");
            _current.Add(evt);
        }

        public void EndTick()
        {
            (_published, _current) = (_current, _published);
            _current.Clear();
            TotalPublished += _published.Count;
            if (_published.Count == 0 || TickPublished == null) return;
            _dispatching = true;
            try { TickPublished.Invoke(_published); }
            finally { _dispatching = false; }
        }

        public void Clear()
        {
            _current.Clear();
            _published.Clear();
        }
    }
}

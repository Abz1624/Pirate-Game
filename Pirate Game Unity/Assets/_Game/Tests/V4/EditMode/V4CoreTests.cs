using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.Simulation;
using RoguePirates.V4.Events;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class RunClockTests
    {
        [Test]
        public void Advance_YieldsWholeTicks_AndCarriesRemainder()
        {
            var clock = new RunClock(60f);
            Assert.AreEqual(0, clock.Advance(0.01f));
            Assert.AreEqual(1, clock.Advance(0.01f)); // 0.02 accumulated -> one 1/60 tick, remainder kept
            Assert.AreEqual(2, clock.Advance(2f / 60f));
        }

        [Test]
        public void Advance_CapsStepsPerFrame_AndDropsBacklog()
        {
            var clock = new RunClock(60f, 5);
            Assert.AreEqual(5, clock.Advance(1f));
            Assert.AreEqual(0, clock.Advance(0f));
            Assert.AreEqual(1, clock.Advance(1f / 60f + 0.0001f));
        }

        [Test]
        public void NextTick_CountsTicksAndSimTime()
        {
            var clock = new RunClock(60f);
            var first = clock.NextTick();
            var second = clock.NextTick();
            Assert.AreEqual(0, first.Index);
            Assert.AreEqual(1, second.Index);
            Assert.AreEqual(1f / 60f, second.SimTime, 1e-6f);
            Assert.AreEqual(2, clock.TickIndex);
        }

        [Test]
        public void Pause_IsASet_ClearingOneReasonKeepsTheOther()
        {
            var clock = new RunClock();
            clock.Pause.Add(SimPauseReason.Choice);
            clock.Pause.Add(SimPauseReason.Manual);
            clock.Pause.Remove(SimPauseReason.Choice);
            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(0, clock.Advance(1f));
            clock.Pause.Remove(SimPauseReason.Manual);
            Assert.IsFalse(clock.IsPaused);
        }
    }

    [Category("V4")]
    public sealed class CombatEventsTests
    {
        [Test]
        public void EndTick_DeliversBatchInPublishOrder()
        {
            var events = new CombatEvents();
            var received = new List<CombatEventKind>();
            events.TickPublished += batch => { foreach (var e in batch) received.Add(e.Kind); };
            events.Publish(new CombatEvent(CombatEventKind.Spawned, 0, 0f));
            events.Publish(new CombatEvent(CombatEventKind.Hit, 0, 0f));
            events.Publish(new CombatEvent(CombatEventKind.Killed, 0, 0f));
            Assert.IsEmpty(received, "nothing is delivered before EndTick");
            events.EndTick();
            CollectionAssert.AreEqual(new[] { CombatEventKind.Spawned, CombatEventKind.Hit, CombatEventKind.Killed }, received);
            Assert.AreEqual(3, events.TotalPublished);
        }

        [Test]
        public void EndTick_StartsAFreshBatch()
        {
            var events = new CombatEvents();
            var sizes = new List<int>();
            events.TickPublished += batch => sizes.Add(batch.Count);
            events.Publish(new CombatEvent(CombatEventKind.Fired, 0, 0f));
            events.EndTick();
            events.EndTick();
            events.Publish(new CombatEvent(CombatEventKind.Fired, 2, 0f));
            events.Publish(new CombatEvent(CombatEventKind.Fired, 2, 0f));
            events.EndTick();
            CollectionAssert.AreEqual(new[] { 1, 2 }, sizes);
        }

        [Test]
        public void PublishingFromAReader_Throws()
        {
            var events = new CombatEvents();
            events.TickPublished += _ => events.Publish(new CombatEvent(CombatEventKind.Hit, 1, 0f));
            events.Publish(new CombatEvent(CombatEventKind.Hit, 0, 0f));
            Assert.Throws<InvalidOperationException>(() => events.EndTick());
        }
    }

    [Category("V4")]
    public sealed class ModuleRegistryTests
    {
        sealed class Probe : IRunModule
        {
            readonly List<string> _log;
            public Probe(string id, int order, List<string> log) { Id = id; TickOrder = order; _log = log; }
            public string Id { get; }
            public int TickOrder { get; }
            public void Initialize(RunContext context) => _log.Add("init:" + Id);
            public void BeginRun(in RunStart start) => _log.Add("begin:" + Id);
            public void Tick(in SimTick tick) => _log.Add("tick:" + Id);
            public void EndRun(in RunEnd end) => _log.Add("end:" + Id);
        }

        [Test]
        public void Ticks_RunInTickOrder_TiesKeepRegistrationOrder()
        {
            var log = new List<string>();
            var registry = new ModuleRegistry();
            registry.Add(new Probe("b", 20, log));
            registry.Add(new Probe("a", 10, log));
            registry.Add(new Probe("c", 20, log));
            registry.TickAll(new SimTick(0, 1f / 60f, 0f));
            CollectionAssert.AreEqual(new[] { "tick:a", "tick:b", "tick:c" }, log);
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            var registry = new ModuleRegistry();
            registry.Add(new Probe("x", 0, new List<string>()));
            Assert.Throws<InvalidOperationException>(() => registry.Add(new Probe("x", 1, new List<string>())));
        }

        interface IProbeContract { }
        sealed class ProbeImpl : IProbeContract { }

        [Test]
        public void Context_ResolvesInterfaceContracts_Only()
        {
            var context = new RunContext(new RunClock(), new CombatEvents());
            var impl = new ProbeImpl();
            context.Register<IProbeContract>(impl);
            Assert.AreSame(impl, context.Get<IProbeContract>());
            Assert.Throws<InvalidOperationException>(() => context.Register<IProbeContract>(new ProbeImpl()));
            Assert.Throws<ArgumentException>(() => context.Register(new ProbeImpl()));
        }
    }
}

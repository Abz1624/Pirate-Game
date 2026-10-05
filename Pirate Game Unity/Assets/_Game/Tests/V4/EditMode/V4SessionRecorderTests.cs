using NUnit.Framework;
using RoguePirates.V4.Events;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Telemetry;

namespace RoguePirates.V4.Tests
{
    /// <summary>Playtest (6 Oct, owner): the session recorder's samples, damage by source, end reason and one save.</summary>
    [Category("V4")]
    public sealed class V4SessionRecorderTests
    {
        [Test]
        public void RecordsSamplesDamageBySourceAndSavesOnceAtTheEnd()
        {
            var clock = new RunClock();
            var events = new CombatEvents();
            var context = new RunContext(clock, events);
            float time = 0f, hull = 100f;
            var saves = 0;
            string json = null, name = null;
            var sunk = false;
            var recorder = new SessionRecorder
            {
                RunTime = () => time,
                Level = () => 1 + (int)(time / 20f),
                Kills = () => (int)time,
                Gold = () => 5,
                HullKnown = () => true,
                Hull = () => hull,
                MaxHull = () => 100f,
                SunkNow = () => sunk,
                Captain = () => "rook",
                StartingWeapon = () => "deck_cannons",
                Save = (n, j) => { saves++; name = n; json = j; },
            };
            recorder.Initialize(context);
            recorder.BeginRun(new RunStart(42, false));
            const float dt = 1f / 30f;
            for (var t = 0; t < 30 * 65; t++)
            {
                time = t * dt;
                if (t == 300) events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, t, time, z: 12f, tag: "brute_raider"));
                if (t == 600) events.Publish(new CombatEvent(CombatEventKind.PlayerDamaged, t, time, b: 1, z: 3f, tag: "storm"));
                if (t == 900) events.Publish(new CombatEvent(CombatEventKind.LevelUp, t, time, a: 2));
                if (t == 1200) hull = 70f;
                if (t == 1201) hull = 85f; // a heal pickup
                recorder.Tick(new SimTick(t, dt, time));
                events.EndTick();
            }
            sunk = true;
            recorder.OnRunState(RunState.RunComplete);
            recorder.EndRun(new RunEnd(time, false));

            Assert.AreEqual(1, saves, "saved once (results, then the run end)");
            StringAssert.StartsWith("session-", name);
            StringAssert.Contains("rook", name);
            StringAssert.Contains("\"endReason\":\"sunk\"", json);
            StringAssert.Contains("\"brute_raider\":{\"damage\":12,\"hits\":1}", json);
            StringAssert.Contains("\"storm\":{\"damage\":3,\"hits\":1}", json);
            StringAssert.Contains("\"type\":\"heal\"", json);
            StringAssert.Contains("\"t\":60,", json);
            recorder.Detach();
        }
    }
}

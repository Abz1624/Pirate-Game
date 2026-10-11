using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RoguePirates.V5.Net.Tests
{
    /// <summary>V5 M9c S0b: the load probe's prototype enemy delta encoding (round trip) and the test-preset gate.</summary>
    [Category("V5")]
    public sealed class V5NetLoadProbeEditModeTests
    {
        /// <summary>
        /// The server writes each snapshot against the client's acked baseline (5 snapshots back, a 270 ms round trip at
        /// 15 Hz) and leaves a random share of the changed enemies out (a starving byte budget); enemies come and go and
        /// some return. The client decodes with its own history; its table must equal the server's model every time.
        /// </summary>
        [Test]
        public void EnemyDelta_RoundTrips_LaggedBaselines_Starvation_RemovalsAndReturns()
        {
            NetEnemyDelta.Clamps = 0;
            var rng = new System.Random(7);
            var world = new Dictionary<int, (Vector3 p, Vector3 v, float hp, int def)>();
            var nextId = 1;
            for (var i = 0; i < 140; i++) world[nextId++] = (new Vector3(rng.Next(-60, 60), 0, rng.Next(-60, 60)), Vector3.zero, 1f, rng.Next(0, 12));

            var serverPrev = new EnemyTable();
            var serverHistory = new Dictionary<int, EnemyTable>();
            var clientTable = new EnemyTable();
            var clientScratch = new EnemyTable();
            var clientHistory = new Dictionary<ushort, EnemyTable>();
            var w = new NetWriter(4096);
            var bytes = new List<int>();
            var removedSeen = 0;
            var leftOut = 0;
            for (var n = 0; n < 400; n++)
            {
                // The world moves: headings change now and then, hp drops, some sink and new ones appear, a few return.
                var ids = new List<int>(world.Keys);
                foreach (var id in ids)
                {
                    var e = world[id];
                    if (rng.NextDouble() < 0.1) e.v = new Vector3((float)rng.NextDouble() * 16 - 8, 0, (float)rng.NextDouble() * 16 - 8);
                    e.p += e.v / 15f;
                    if (rng.NextDouble() < 0.05) e.hp = Mathf.Max(0.05f, e.hp - 0.2f);
                    world[id] = e;
                    if (rng.NextDouble() < 0.01) world.Remove(id);
                }
                while (world.Count < 140) world[nextId++] = (new Vector3(rng.Next(-60, 60), 0, rng.Next(-60, 60)), Vector3.zero, 1f, rng.Next(0, 12));

                // The view: up to 108 enemies, sometimes a different set (an enemy can leave and come back).
                var view = new List<int>(world.Keys);
                view.Sort();
                if (n % 7 == 3) view.RemoveRange(0, Math.Min(10, view.Count));
                if (view.Count > 108) view.RemoveRange(108, view.Count - 108);

                var target = new EnemyTable();
                foreach (var id in view)
                {
                    var e = world[id];
                    var q = NetEnemyDelta.Quantize(id, e.def, e.p, e.v, e.hp, 0);
                    var pi = serverPrev.IndexOf(id);
                    var starve = rng.NextDouble() < 0.3;
                    if (pi >= 0 && starve) { target.Add(serverPrev.Items[pi]); leftOut++; }
                    else if (pi < 0 && starve) leftOut++;
                    else target.Add(q);
                }
                target.SortById();
                for (var i = 0; i < serverPrev.Count; i++) if (target.IndexOf(serverPrev.Items[i].id) < 0) removedSeen++;
                var snapshotId = (ushort)(n % 65535);
                target.SnapshotId = snapshotId;
                var baseline = n >= 5 ? serverHistory[n - 5] : null;
                w.Clear();
                var written = NetEnemyDelta.Write(w, snapshotId, baseline, serverPrev, target, NetEnemyDelta.Grid(0f), NetEnemyDelta.Grid(0f));
                bytes.Add(written);
                Assert.AreEqual(w.Length, written);

                var ok = NetEnemyDelta.Read(new NetReader(w.ToArray()), id => clientHistory.TryGetValue(id, out var t) ? t : null, clientTable, clientScratch, out var readId);
                Assert.IsTrue(ok, $"snapshot {n} decodes");
                Assert.AreEqual(snapshotId, readId);
                Assert.IsTrue(clientTable.SameAs(target), $"snapshot {n}: the client's table is the server's model");
                var keep = new EnemyTable();
                keep.CopyFrom(clientTable);
                clientHistory[readId] = keep;

                var stored = new EnemyTable();
                stored.CopyFrom(target);
                serverHistory[n] = stored;
                serverPrev.CopyFrom(target);
            }
            Assert.Greater(removedSeen, 50, "removals were exercised");
            Assert.Greater(leftOut, 1000, "starvation was exercised");
            Assert.AreEqual(0, NetEnemyDelta.Clamps);
        }

        [Test]
        public void EnemyDelta_MovingEnemyAgainstA270msBaseline_CostsAboutFourToSixBytes()
        {
            var b = NetEnemyDelta.Quantize(42, 3, new Vector3(10f, 0f, 10f), new Vector3(6f, 0f, 0f), 1f, 0);
            // 5 snapshots (333 ms) later at 6 m/s: 2 m along x, heading unchanged.
            var q = NetEnemyDelta.Quantize(42, 3, new Vector3(12f, 0f, 10f), new Vector3(6f, 0f, 0f), 1f, 0);
            Assert.AreEqual(3, NetEnemyDelta.ChangeBytes(q, true, b), "mask + two 1-byte steps");
            var turned = NetEnemyDelta.Quantize(42, 3, new Vector3(12f, 0f, 11f), new Vector3(6f, 0f, 3f), 0.8f, 0);
            Assert.AreEqual(5, NetEnemyDelta.ChangeBytes(turned, true, b), "mask, steps, heading and hp");
            Assert.AreEqual(9, NetEnemyDelta.ChangeBytes(q, false, default), "a new enemy in full");
            Assert.AreEqual(-5, NetEnemyDelta.UnZigZag(NetEnemyDelta.ZigZag(-5)));
            Assert.AreEqual(int.MaxValue, NetEnemyDelta.UnZigZag(NetEnemyDelta.ZigZag(int.MaxValue)));
        }

        [Test]
        public void TestPresets_RefusedWithoutTheAllowSwitch()
        {
            var allow = Environment.GetEnvironmentVariable(NetTestPresets.AllowVariable);
            var preset = Environment.GetEnvironmentVariable(NetTestPresets.PresetVariable);
            var forTests = NetTestPresets.AllowForTests;
            try
            {
                NetTestPresets.AllowForTests = false;
                Environment.SetEnvironmentVariable(NetTestPresets.AllowVariable, null);
                Environment.SetEnvironmentVariable(NetTestPresets.PresetVariable, NetTestPresets.Load200);
                Assert.IsFalse(NetTestPresets.Allowed);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("refused"));
                Assert.IsNull(NetTestPresets.Requested(), "asked for without RP_ALLOW_TEST_PRESETS=1: refused");
                Environment.SetEnvironmentVariable(NetTestPresets.AllowVariable, "true");
                Assert.IsFalse(NetTestPresets.Allowed, "only the exact value 1 allows it");
                Environment.SetEnvironmentVariable(NetTestPresets.AllowVariable, "1");
                Assert.AreEqual(NetTestPresets.Load200, NetTestPresets.Requested());
            }
            finally
            {
                Environment.SetEnvironmentVariable(NetTestPresets.AllowVariable, allow);
                Environment.SetEnvironmentVariable(NetTestPresets.PresetVariable, preset);
                NetTestPresets.AllowForTests = forTests;
            }
        }
    }
}

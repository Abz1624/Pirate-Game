using System;
using NUnit.Framework;
using RoguePirates.V4.Online;

namespace RoguePirates.V4.Tests
{
    /// <summary>Playtest (3 Oct, owner): the online client's pure rules and the local profile list.</summary>
    public sealed class PlaytestOnlineTests
    {
        [TestCase("  Anne Bonny ", "Anne Bonny")]
        [TestCase("Calico_Jack", "Calico_Jack")]
        [TestCase("abc", "abc")]
        [TestCase("a-b-c-d-e-f-g-h-", "a-b-c-d-e-f-g-h-")]
        public void CleanName_accepts_the_api_rules(string raw, string expected) =>
            Assert.AreEqual(expected, OnlineRules.CleanName(raw, out _));

        [TestCase("ab")]
        [TestCase("seventeen chars!!")]
        [TestCase("bad!name")]
        [TestCase("")]
        [TestCase(null)]
        public void CleanName_refuses_bad_names(string raw)
        {
            Assert.IsNull(OnlineRules.CleanName(raw, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void DefaultName_is_valid() =>
            Assert.IsNotNull(OnlineRules.CleanName(OnlineRules.DefaultName(-123456), out _));

        [Test]
        public void Weekly_reset_is_the_next_monday_utc()
        {
            Assert.AreEqual(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), OnlineRules.NextWeeklyReset(new DateTime(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), OnlineRules.NextWeeklyReset(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), OnlineRules.NextWeeklyReset(new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc)));
        }

        [Test]
        public void Countdown_and_run_time_labels()
        {
            var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("1d 12h 0m", OnlineRules.Countdown(now, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual("0h 0m 5s", OnlineRules.Countdown(now, now.AddSeconds(5)));
            Assert.AreEqual("15:00", OnlineRules.RunTime(900));
            Assert.AreEqual("1:01:05", OnlineRules.RunTime(3665));
        }

        [Test]
        public void Build_hash_is_stable_and_order_independent()
        {
            var a = OnlineRules.BuildHash(new[] { "w:cannon:3", "l:speed:1" });
            Assert.AreEqual(a, OnlineRules.BuildHash(new[] { "l:speed:1", "w:cannon:3" }));
            Assert.AreNotEqual(a, OnlineRules.BuildHash(new[] { "w:cannon:4", "l:speed:1" }));
            Assert.AreEqual(16, a.Length);
        }

        [Test]
        public void Query_value_reads_the_api_override()
        {
            Assert.AreEqual("https://api.example.com", OnlineRules.QueryValue("https://www.roguepirates.fun/play/?join=K7&api=https%3A%2F%2Fapi.example.com#x", "api"));
            Assert.IsNull(OnlineRules.QueryValue("https://www.roguepirates.fun/play/", "api"));
        }

        [Test]
        public void Base_url_resolution_order()
        {
            var hosts = new[] { "localhost", "*.roguepirates.fun" };
            // Editor: the local API unless overridden.
            Assert.AreEqual("http://localhost:8080", OnlineRules.ResolveBaseUrl(null, "", true, true, false, "http://localhost:8080/", "https://api.roguepirates.fun", hosts));
            Assert.AreEqual("http://10.0.0.2:8080", OnlineRules.ResolveBaseUrl(null, "http://10.0.0.2:8080", true, true, false, "http://localhost:8080", "", hosts));
            // Release build: production; empty production = offline (null).
            Assert.AreEqual("https://api.roguepirates.fun", OnlineRules.ResolveBaseUrl(null, "", false, false, false, "http://localhost:8080", "https://api.roguepirates.fun", hosts));
            Assert.IsNull(OnlineRules.ResolveBaseUrl(null, "", false, false, false, "http://localhost:8080", "", hosts));
            // Release WebGL ?api=: allowed hosts only (a shared link cannot send tokens to another server).
            Assert.AreEqual("https://api.roguepirates.fun", OnlineRules.ResolveBaseUrl("https://api.roguepirates.fun", "", false, false, false, "", "", hosts));
            Assert.IsNull(OnlineRules.ResolveBaseUrl("https://evil.example.com", "", false, false, false, "", "", hosts));
            Assert.AreEqual("https://evil.example.com", OnlineRules.ResolveBaseUrl("https://evil.example.com", "", false, true, false, "", "", hosts));
            Assert.IsNull(OnlineRules.ResolveBaseUrl("ftp://x", "not a url", false, false, false, "", "", hosts));
        }

        [Test]
        public void Profile_store_keeps_a_list_and_the_active_profile()
        {
            string saved = null;
            var store = new ProfileStore(() => saved, s => saved = s);
            Assert.IsNull(store.Active);
            var a = store.Add("Anne Bonny");
            var b = store.Add("Calico Jack");
            Assert.AreEqual(b.localId, store.Active.localId);
            Assert.IsFalse(b.Registered);
            store.Apply(a.localId, "pid-a", "tok-a", "Anne Bonny", "#4821", 1000);
            Assert.IsTrue(store.SetActive(a.localId));

            var reloaded = new ProfileStore(() => saved, s => saved = s);
            Assert.AreEqual(2, reloaded.Profiles.Count);
            Assert.AreEqual("Anne Bonny#4821", reloaded.Active.Display);
            Assert.AreEqual("tok-a", reloaded.Active.token);

            reloaded.Rename(a.localId, "Anne B", false);
            Assert.IsTrue(reloaded.Active.nameDirty);
            reloaded.Unregister(a.localId);
            Assert.IsFalse(reloaded.Active.Registered);
            Assert.IsTrue(reloaded.Remove(a.localId));
            Assert.AreEqual(b.localId, reloaded.Active.localId);
            Assert.IsTrue(reloaded.Remove(b.localId));
            Assert.IsNull(reloaded.Active);
        }

        [Test]
        public void Profile_store_survives_bad_json()
        {
            var store = new ProfileStore(() => "{not json", _ => { });
            Assert.AreEqual(0, store.Profiles.Count);
        }
    }
}

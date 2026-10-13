using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (13 Oct, review): signed transfers the server has not answered for yet, kept in PlayerPrefs so they survive
    /// an app kill (Android often kills the game while the wallet app is in front during Mobile Wallet Adapter signing).
    /// A transfer is recorded the moment the wallet returns its signature, before it is sent
    /// (<see cref="Web3Session.TransferSol"/>'s onSigned), and removed only when the server has given its final answer
    /// (accepted, refused for good, refunded) or devnet can no longer land it. Until then every retry posts the same
    /// signature: the player never pays twice.
    /// <list type="bullet">
    /// <item><see cref="Deposit"/>: a 1v1 stake, keyed by lobby id;</item>
    /// <item><see cref="Buy"/>: a market purchase, keyed by listing id;</item>
    /// <item><see cref="List"/>: an Island NFT sent to the market escrow, keyed by island id (with the asked price).</item>
    /// </list>
    /// The storage is a seam (<see cref="Read"/>, <see cref="Write"/>, <see cref="Now"/>) for the EditMode tests.
    /// </summary>
    public static class PendingTransfers
    {
        public const string PrefsKey = "rp.hackathon.pendingTransfers";
        public const string Deposit = "deposit", Buy = "buy", List = "list";

        [Serializable]
        public sealed class Entry
        {
            public string kind;
            public string key;
            public string signature;
            public string wallet;
            /// <summary>The amount (lamports) or, for a listing, the asked price.</summary>
            public long lamports;
            /// <summary>When the wallet signed it (unix seconds, UTC).</summary>
            public long signedAt;
            /// <summary>The last answer that was not final (shown to the player), if any.</summary>
            public string lastError;

            public double AgeSeconds(DateTime now) => Math.Max(0, (now - DateTimeOffset.FromUnixTimeSeconds(signedAt).UtcDateTime).TotalSeconds);
        }

        [Serializable]
        sealed class Saved
        {
            public List<Entry> entries = new List<Entry>();
        }

        public static Func<string> Read = DefaultRead;
        public static Action<string> Write = DefaultWrite;
        public static Func<DateTime> Now = () => DateTime.UtcNow;

        static string DefaultRead() => PlayerPrefs.GetString(PrefsKey, string.Empty);

        static void DefaultWrite(string json)
        {
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save(); // at once: the app may be killed next
        }

        /// <summary>Back to PlayerPrefs (tests swap the seams).</summary>
        public static void UseDefaultStorage()
        {
            Read = DefaultRead;
            Write = DefaultWrite;
            Now = () => DateTime.UtcNow;
        }

        static Saved Load()
        {
            var json = Read != null ? Read() : null;
            Saved saved = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { saved = JsonUtility.FromJson<Saved>(json); }
                catch (Exception e) { Debug.LogWarning("PendingTransfers: unreadable record: " + e.Message); }
            }
            if (saved == null) saved = new Saved();
            if (saved.entries == null) saved.entries = new List<Entry>();
            saved.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.signature) || string.IsNullOrEmpty(e.kind));
            return saved;
        }

        static void Save(Saved saved) => Write?.Invoke(JsonUtility.ToJson(saved));

        /// <summary>The pending transfer of this kind and key, or null.</summary>
        public static Entry Get(string kind, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return Load().entries.Find(e => e.kind == kind && e.key == key);
        }

        public static bool Has(string kind, string key) => Get(kind, key) != null;

        /// <summary>Records a signed transfer (replaces one of the same kind and key).</summary>
        public static Entry Remember(string kind, string key, string signature, string wallet, long lamports)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(signature)) return null;
            var saved = Load();
            saved.entries.RemoveAll(e => e.kind == kind && e.key == key);
            var entry = new Entry
            {
                kind = kind, key = key, signature = signature, wallet = wallet, lamports = lamports,
                signedAt = new DateTimeOffset(Now()).ToUnixTimeSeconds(),
            };
            saved.entries.Add(entry);
            Save(saved);
            return entry;
        }

        /// <summary>Notes the last non-final answer on a pending transfer (shown when the player comes back).</summary>
        public static void Note(string kind, string key, string error)
        {
            var saved = Load();
            var e = saved.entries.Find(x => x.kind == kind && x.key == key);
            if (e == null) return;
            e.lastError = error;
            Save(saved);
        }

        /// <summary>The server gave its final answer (or devnet can no longer land it): forget it.</summary>
        public static void Forget(string kind, string key)
        {
            var saved = Load();
            if (saved.entries.RemoveAll(e => e.kind == kind && e.key == key) > 0) Save(saved);
        }

        /// <summary>Every pending transfer of <paramref name="kind"/> (of <paramref name="wallet"/> when given).</summary>
        public static List<Entry> All(string kind, string wallet = null) =>
            Load().entries.FindAll(e => e.kind == kind && (wallet == null || string.IsNullOrEmpty(e.wallet) || e.wallet == wallet));
    }
}

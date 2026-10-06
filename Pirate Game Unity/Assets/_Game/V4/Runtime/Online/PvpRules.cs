using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): the pure rules of the 1v1 mode (EditMode tested): PvP damage (scaled, brief invulnerability
    /// after each hit, no one-shots), the automatic level-up order, the storm ring, the clock offset and the arena to world
    /// mapping. The tuning values are here, in one place.
    /// </summary>
    public static class PvpRules
    {
        // Tuned on the test matches (3 Oct): the owner's first guess (x0.35, 0.4 s, 15%) sank a 120-hull ship in 21 s. At
        // close range six level 1 weapons send about 70 raw damage a second (0.1 s batches of 5 to 70). With these a level 1
        // ship parked in range lasts about 30 s and a moving one about a minute; level-ups shorten it, and the cap keeps any
        // sinking above 10 s (one capped hit per invulnerability window).
        /// <summary>Incoming PvP weapon damage x this (the sender reports the weapon's raw damage).</summary>
        public const float DamageScale = 0.06f;
        /// <summary>Seconds of invulnerability after a PvP hit lands: hits inside it are pooled and land as one hit when it
        /// ends (so many small hits count like one big one, and nothing stacks into a burst).</summary>
        public const float InvulnerableSeconds = 0.5f;
        /// <summary>No single hit takes more than this share of max hull (no one-shots); a pooled hit is capped too.</summary>
        public const float MaxHitFraction = 0.05f;
        /// <summary>Local weapon damage on the opponent is summed and sent as one "hit" this often.</summary>
        public const float HitBatchSeconds = 0.1f;
        /// <summary>The own ship's "state" rate (the server caps 30/s).</summary>
        public const float StateHz = 15f;
        /// <summary>The opponent is drawn this far in the past (two state intervals), between two received states.</summary>
        public const float InterpolationDelaySeconds = 0.12f;
        /// <summary>The server refuses a hit above this.</summary>
        public const float MaxHitDamage = 500f;

        /// <summary>Hull taken by one incoming PvP hit: raw x <see cref="DamageScale"/> x the build's incoming multiplier
        /// (Armor, Brace!), capped at <see cref="MaxHitFraction"/> of max hull. 0 while invulnerable.</summary>
        public static float IncomingHull(float raw, float incomingMultiplier, float maxHull, bool invulnerable)
        {
            if (invulnerable || raw <= 0f) return 0f;
            var amount = raw * DamageScale * Mathf.Max(0f, incomingMultiplier);
            return Mathf.Min(amount, Mathf.Max(1f, maxHull) * MaxHitFraction);
        }

        /// <summary>Still inside the invulnerability window of the last hit.</summary>
        public static bool Invulnerable(float now, float lastHitAt) => now - lastHitAt < InvulnerableSeconds;

        /// <summary>The raw damage of a batch as the server takes it (0..500, rounded to 0.1).</summary>
        public static float ClampHit(float raw) => Mathf.Clamp(Mathf.Round(raw * 10f) / 10f, 0f, MaxHitDamage);

        // ---- Storm ----

        /// <summary>The safe radius at server time <paramref name="nowMs"/>: the start radius until shrinkStart, then linear
        /// to the end radius at shrinkEnd.</summary>
        public static float StormRadius(StormInfo storm, long nowMs)
        {
            if (storm == null || storm.startRadius <= 0f) return 120f;
            if (nowMs <= storm.shrinkStartMs) return storm.startRadius;
            if (nowMs >= storm.shrinkEndMs) return storm.endRadius;
            var t = (float)(nowMs - storm.shrinkStartMs) / Mathf.Max(1f, storm.shrinkEndMs - storm.shrinkStartMs);
            return Mathf.Lerp(storm.startRadius, storm.endRadius, t);
        }

        /// <summary>Storm damage per second after <paramref name="secondsOutside"/> without a break: base + growth x seconds.</summary>
        public static float StormDps(StormInfo storm, float secondsOutside) =>
            storm == null ? 8f : storm.damagePerSecond + storm.damageGrowthPerSecond * Mathf.Max(0f, secondsOutside);

        // ---- Clock ----

        /// <summary>Server minus client clock from a ping/pong: the server time is taken at the round trip's midpoint.</summary>
        public static long ClockOffset(long clientSentMs, long clientReceivedMs, long serverMs) =>
            serverMs - (clientSentMs + (clientReceivedMs - clientSentMs) / 2);

        /// <summary>"m:ss" of milliseconds left (never negative).</summary>
        public static string Clock(long msLeft)
        {
            if (msLeft < 0) msLeft = 0;
            var s = (int)Math.Ceiling(msLeft / 1000.0);
            return $"{s / 60}:{s % 60:00}";
        }

        // ---- Arena and headings ----

        /// <summary>Unity yaw in degrees (0 = +Z, 90 = +X) of a direction on the water.</summary>
        public static float YawOf(Vector3 forward)
        {
            if (forward.x * forward.x + forward.z * forward.z < 1e-6f) return 0f;
            var yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return yaw < 0f ? yaw + 360f : yaw;
        }

        public static Vector3 Forward(float yaw)
        {
            var r = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>World position of an arena point (the arena centre sits on the play area's centre).</summary>
        public static Vector3 ToWorld(Vector3 worldCentre, float x, float z) => new Vector3(worldCentre.x + x, 0f, worldCentre.z + z);

        /// <summary>Arena point of a world position.</summary>
        public static Vector2 ToArena(Vector3 worldCentre, Vector3 world) => new Vector2(world.x - worldCentre.x, world.z - worldCentre.z);
    }

    /// <summary>
    /// Playtest (3 Oct, owner): one ship's incoming PvP hits: "brief invulnerability after each hit, no one-shots". A hit
    /// outside the window lands at once (scaled, capped) and opens the window; hits inside it are pooled and land as one
    /// capped hit when it ends (which opens the next window). Hull per second is therefore at most
    /// <see cref="PvpRules.MaxHitFraction"/> / <see cref="PvpRules.InvulnerableSeconds"/> of max hull.
    /// </summary>
    public sealed class PvpHitWindow
    {
        float _windowEnds = float.NegativeInfinity;
        float _pending;

        public bool Invulnerable(float now) => now < _windowEnds;
        public float Pending => _pending;

        /// <summary>A hit of <paramref name="raw"/> damage at <paramref name="now"/>: the hull to take now (0 while invulnerable).</summary>
        public float Hit(float now, float raw, float incomingMultiplier, float maxHull)
        {
            if (raw <= 0f) return 0f;
            if (Invulnerable(now))
            {
                _pending += raw * PvpRules.DamageScale * Mathf.Max(0f, incomingMultiplier);
                return 0f;
            }
            _windowEnds = now + PvpRules.InvulnerableSeconds;
            return PvpRules.IncomingHull(raw, incomingMultiplier, maxHull, false);
        }

        /// <summary>Each frame: the pooled hits as one capped hit once the window is over (0 otherwise).</summary>
        public float Tick(float now, float maxHull)
        {
            if (_pending <= 0f || Invulnerable(now)) return 0f;
            var take = Mathf.Min(_pending, Mathf.Max(1f, maxHull) * PvpRules.MaxHitFraction);
            _pending = 0f;
            _windowEnds = now + PvpRules.InvulnerableSeconds;
            return take;
        }

        public void Clear()
        {
            _pending = 0f;
            _windowEnds = float.NegativeInfinity;
        }
    }

    /// <summary>One entry of the combined priority list (a weapon or a Logbook).</summary>
    public readonly struct PvpPick
    {
        public readonly bool IsWeapon;
        public readonly string Id;
        public PvpPick(bool isWeapon, string id) { IsWeapon = isWeapon; Id = id; }
        public override string ToString() => (IsWeapon ? "w:" : "l:") + Id;
    }

    /// <summary>
    /// Playtest (3 Oct, owner): on level-up the game does not pause; the next weapon or Logbook is levelled by the player's
    /// priority. Chosen order: the two drafts interleaved (weapon 1, Logbook 1, weapon 2, Logbook 2, ... weapon 6, Logbook 6),
    /// walked round-robin: each level-up takes the next entry after the last one levelled, skipping maxed ones, and wraps
    /// to the top. So the top priorities rise first and everything keeps rising evenly.
    /// </summary>
    public sealed class PvpLevelOrder
    {
        readonly List<PvpPick> _order = new List<PvpPick>();
        int _cursor;

        public IReadOnlyList<PvpPick> Order => _order;
        /// <summary>The entry the next level-up starts looking from.</summary>
        public int Cursor => _cursor;

        public PvpLevelOrder(IReadOnlyList<string> weapons, IReadOnlyList<string> logbooks)
        {
            var n = Mathf.Max(weapons != null ? weapons.Count : 0, logbooks != null ? logbooks.Count : 0);
            for (var i = 0; i < n; i++)
            {
                if (weapons != null && i < weapons.Count && !string.IsNullOrEmpty(weapons[i])) _order.Add(new PvpPick(true, weapons[i]));
                if (logbooks != null && i < logbooks.Count && !string.IsNullOrEmpty(logbooks[i])) _order.Add(new PvpPick(false, logbooks[i]));
            }
        }

        /// <summary>The entry the next level-up raises (and moves past it), or null when everything is maxed.</summary>
        public PvpPick? Next(Func<PvpPick, bool> maxed)
        {
            for (var step = 0; step < _order.Count; step++)
            {
                var i = (_cursor + step) % _order.Count;
                if (maxed != null && maxed(_order[i])) continue;
                _cursor = (i + 1) % _order.Count;
                return _order[i];
            }
            return null;
        }
    }

    /// <summary>A received ship state, kept for interpolation.</summary>
    public struct PvpSnapshot
    {
        public float Time;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
    }

    /// <summary>
    /// The opponent's received states, drawn <see cref="PvpRules.InterpolationDelaySeconds"/> in the past: between the two
    /// states around that moment, or extrapolated from the last one's velocity (at most 0.5 s) when the next is late.
    /// </summary>
    public sealed class PvpInterpolator
    {
        readonly PvpSnapshot[] _buffer = new PvpSnapshot[16];
        int _count;

        public int Count => _count;
        public bool HasData => _count > 0;
        public PvpSnapshot Latest => _count > 0 ? _buffer[_count - 1] : default;

        public void Clear() => _count = 0;

        public void Add(PvpSnapshot s)
        {
            if (_count > 0 && s.Time <= _buffer[_count - 1].Time) s.Time = _buffer[_count - 1].Time + 0.001f;
            if (_count == _buffer.Length)
            {
                Array.Copy(_buffer, 1, _buffer, 0, _buffer.Length - 1);
                _count--;
            }
            _buffer[_count++] = s;
        }

        /// <summary>The position and yaw to draw at local time <paramref name="now"/>.</summary>
        public bool Sample(float now, out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0f;
            if (_count == 0) return false;
            var t = now - PvpRules.InterpolationDelaySeconds;
            if (t <= _buffer[0].Time) { position = _buffer[0].Position; yaw = _buffer[0].Yaw; return true; }
            for (var i = 1; i < _count; i++)
            {
                if (t > _buffer[i].Time) continue;
                var a = _buffer[i - 1];
                var b = _buffer[i];
                var k = Mathf.InverseLerp(a.Time, b.Time, t);
                position = Vector3.Lerp(a.Position, b.Position, k);
                yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k);
                return true;
            }
            var last = _buffer[_count - 1];
            var ahead = Mathf.Min(0.5f, t - last.Time);
            position = last.Position + last.Velocity * ahead;
            yaw = last.Yaw;
            return true;
        }
    }

    /// <summary>
    /// The few JSON reads JsonUtility cannot do: the per-player objects of "match_end" (stats and ratings are keyed by player
    /// id). A small scanner for one object value by key, aware of strings and nesting.
    /// </summary>
    public static class PvpJson
    {
        /// <summary>The message type ("t"), or null when the text is not a JSON object with one.</summary>
        public static string TypeOf(string json)
        {
            if (string.IsNullOrEmpty(json) || json[0] != '{') return null;
            try { return JsonUtility.FromJson<PvpMsg>(json)?.t; }
            catch (Exception) { return null; }
        }

        /// <summary>The object value (with braces) of the first <c>"key":</c> at the top level of <paramref name="json"/>
        /// (or of its object <paramref name="parent"/> when given), or null.</summary>
        public static string ObjectAt(string json, string key, string parent = null)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (parent != null)
            {
                json = ObjectAt(json, parent);
                if (json == null) return null;
            }
            var depth = 0;
            var inString = false;
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '{' || c == '[') { depth++; continue; }
                if (c == '}' || c == ']') { depth--; continue; }
                if (c != '"') continue;
                // a key at depth 1?
                var end = json.IndexOf('"', i + 1);
                if (end < 0) return null;
                if (depth == 1 && end - i - 1 == key.Length && string.CompareOrdinal(json, i + 1, key, 0, key.Length) == 0)
                {
                    var j = end + 1;
                    while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                    if (j < json.Length && json[j] == ':')
                    {
                        j++;
                        while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                        return j < json.Length && json[j] == '{' ? Balanced(json, j) : null;
                    }
                }
                inString = true;
            }
            return null;
        }

        static string Balanced(string json, int start)
        {
            var depth = 0;
            var inString = false;
            for (var i = start; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return json.Substring(start, i - start + 1);
            }
            return null;
        }

        /// <summary>A player's object in a dictionary field of the message ("stats" or "ratings"), as <typeparamref name="T"/>.</summary>
        public static T Entry<T>(string json, string field, string playerId) where T : class
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            var obj = ObjectAt(json, playerId, field);
            if (obj == null) return null;
            try { return JsonUtility.FromJson<T>(obj); }
            catch (Exception) { return null; }
        }
    }
}

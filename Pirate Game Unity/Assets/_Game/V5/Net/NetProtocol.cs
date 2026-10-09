using System;
using System.Collections.Generic;
using System.Text;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.PlayerSystems;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): the match protocol. Netcode for GameObjects carries it as named messages (no NetworkObject per ship
    /// or enemy): clients send inputs only; the server sends its state. Rates, radii and quantization live here; the
    /// format is written down in plan/v5/phase-09b-netcode/README.md. The rates and radii are constants for the spike;
    /// they move to a config asset before I13b.1 promotes this code (recorded in that README).
    /// </summary>
    public static class NetProtocol
    {
        /// <summary>2 (part 2b): the snapshot carries the boost's timers; level-up messages. 3 (M9b review): the client names
        /// the tick its ship holds from when the cards arrive (<see cref="PickAction.Hold"/>).</summary>
        public const byte Version = 3;

        /// <summary>The server's fixed simulation rate and the client's input rate (one input per tick).</summary>
        public const int TickRate = 30;
        public const float TickSeconds = 1f / TickRate;
        /// <summary>A snapshot every 2nd tick: 15 Hz.</summary>
        public const int SnapshotEveryTicks = 2;
        /// <summary>Each input packet repeats the newest inputs (a lost packet costs nothing while the next one arrives).</summary>
        public const int InputRedundancy = 8;
        /// <summary>Inputs buffered beyond this many are skipped unapplied (the server never steps faster than its tick).</summary>
        public const int MaxInputBacklog = 6;
        /// <summary>A silent socket counts as gone after this long (UTP's default is 30 s; a resume must fit in 10 s).</summary>
        public const int DisconnectTimeoutMs = 5000;

        /// <summary>Interest management: enemies within <see cref="NearRadius"/> of the ship go in every snapshot, those out
        /// to <see cref="FarRadius"/> in every <see cref="FarEveryNthSnapshot"/>th one (a full set), none beyond.</summary>
        public const float NearRadius = 60f;
        public const float FarRadius = 120f;
        public const int FarEveryNthSnapshot = 3;
        public const int MaxEnemiesPerSnapshot = 384;
        /// <summary>Enemies per message, so a batch stays near one packet (a lost fragment loses its batch only).</summary>
        public const int MaxEnemiesPerBatch = 96;
        /// <summary>Enemy positions: offsets from the snapshot's origin in steps of 2 cm (int16: +-655 m; error at most 1 cm per axis).</summary>
        public const float PositionStep = 0.02f;

        public const string Input = "rp9b.in";
        public const string ClientReport = "rp9b.report";
        public const string Welcome = "rp9b.welcome";
        public const string Snapshot = "rp9b.snap";
        public const string Enemies = "rp9b.enemies";
        public const string Defs = "rp9b.defs";
        public const string End = "rp9b.end";
        /// <summary>V5 M9b part 2b: the level-up cards the server's run dealt (server to client, reliable).</summary>
        public const string Offer = "rp9b.offer";
        /// <summary>The player's choice (client to server, reliable): pick a card, skip or refresh.</summary>
        public const string Pick = "rp9b.pick";
        /// <summary>How a card set was resolved and the client tick the ship moves again from (server to client, reliable).</summary>
        public const string OfferClosed = "rp9b.offerdone";

        /// <summary>A level-up the client leaves unanswered this long (or a client gone) takes the first card (the default
        /// pick), so a voyage never waits for good.</summary>
        public const float LevelUpTimeoutSeconds = 30f;
        /// <summary>V5 M9b review (D9b.1 level-up pull-back): when cards are dealt the server's ship keeps applying the
        /// client's inputs until the client names its hold tick (the tick it had reached when the cards arrived), then holds
        /// from there; so neither side takes ticks back. The hold is clamped to at least the server's next unapplied tick
        /// and at most the newest input it has received plus <see cref="HoldLeadTicks"/>; a pick's resume tick to at most
        /// the next unapplied tick plus twice that. No hold named within <see cref="HoldWaitSeconds"/> (a lost client):
        /// the server holds from its next unapplied tick and tells the client.</summary>
        public const int HoldLeadTicks = 6;
        /// <summary>A hold or a pick that arrives late (a lost packet resent) is still exact up to this many ticks back (1 s):
        /// a late hold rewinds the server's ship (the run is paused), a late pick steps the held ticks on the inputs received.</summary>
        public const uint LateTickWindow = 30;
        public const float HoldWaitSeconds = 1f;
        /// <summary>Hold (M9b review): the client names the tick its ship holds from (in the pick's resume tick).</summary>
        public enum PickAction : byte { Pick = 1, Skip = 2, Refresh = 3, Hold = 4 }
        public enum OfferResolution : byte { Picked = 1, Skipped = 2, Refreshed = 3, TimedOut = 4, Cancelled = 5 }

        /// <summary>The connection payload's first byte.</summary>
        public const byte ConnectTicket = 1, ConnectResume = 2;
        public const int ResumeTokenBytes = 16;

        public const byte ButtonBoost = 1;
        public const byte EnemyElite = 1, EnemyBoss = 2;
        public const byte ShipAlive = 1, ShipRunEnded = 2, ShipMovementOn = 4, ShipBoosting = 8, ShipPaused = 16;
        /// <summary>V5 M9b review: the server still waits for the client's hold tick (its ship may be past it).</summary>
        public const byte ShipHoldPending = 32;
        public enum EndReason : byte { Sunk = 1, RunEnded = 2, ServerClosing = 3, SessionExpired = 4 }

        /// <summary>A named message's framing in Netcode (message header and the name's 8-byte hash), for the wire estimate.</summary>
        public const int NamedMessageOverheadBytes = 12;
    }

    /// <summary>One tick of input: steer and throttle quantized to sbyte (the client predicts on the quantized value too).</summary>
    public struct NetInput : IEquatable<NetInput>
    {
        public sbyte steer, throttle;
        public byte buttons;

        public Vector2 SteerThrottle => new Vector2(steer / 127f, throttle / 127f);
        public bool Boost => (buttons & NetProtocol.ButtonBoost) != 0;

        public static NetInput From(Vector2 steerThrottle, bool boost) => new NetInput
        {
            steer = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steerThrottle.x, -1f, 1f) * 127f),
            throttle = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steerThrottle.y, -1f, 1f) * 127f),
            buttons = boost ? NetProtocol.ButtonBoost : (byte)0,
        };

        public bool Equals(NetInput other) => steer == other.steer && throttle == other.throttle && buttons == other.buttons;
        public override bool Equals(object obj) => obj is NetInput other && Equals(other);
        public override int GetHashCode() => (byte)steer | (byte)throttle << 8 | buttons << 16;
    }

    /// <summary>The ship and run state in a snapshot (the server's word on all of it).</summary>
    public struct ShipSnapshot
    {
        public uint serverTick;
        /// <summary>The newest client input tick the server has applied (0: none yet since the welcome).</summary>
        public uint ackTick;
        public PlayerShipController.MotionState motion;
        /// <summary>V5 M9b part 2b: the boost's timers (the client rewinds them with the motion).</summary>
        public ShipBoostController.NetState boost;
        public float hull, maxHull, runTime;
        public ushort level;
        public uint kills, gold;
        public byte flags;

        public bool Alive => (flags & NetProtocol.ShipAlive) != 0;
        public bool RunEnded => (flags & NetProtocol.ShipRunEnded) != 0;
        public bool Boosting => (flags & NetProtocol.ShipBoosting) != 0;
        /// <summary>The server's run is paused (level-up cards).</summary>
        public bool Paused => (flags & NetProtocol.ShipPaused) != 0;
        /// <summary>V5 M9b review: the server has not had the client's hold tick yet.</summary>
        public bool HoldPending => (flags & NetProtocol.ShipHoldPending) != 0;
    }

    /// <summary>V5 M9b part 2b: a level-up's cards as the server's run dealt them (the real offers).</summary>
    public sealed class OfferMessage
    {
        public ushort seq;
        /// <summary>The first client input tick the paused run does not move the ship on; 0: not fixed yet, the client names
        /// it (<see cref="NetProtocol.PickAction.Hold"/>: the tick after the one it has reached).</summary>
        public uint pauseFrom;
        public byte refreshesRemaining, pendingLevelUps;
        public float timeoutSeconds;
        public readonly List<Offer> offers = new List<Offer>();
    }

    public struct PickMessage
    {
        public ushort seq;
        public NetProtocol.PickAction action;
        public byte index;
        /// <summary>The client tick its ship moves again from (a pick or a skip; the server may take a later one).</summary>
        public uint resumeTick;
    }

    public struct OfferClosedMessage
    {
        public ushort seq;
        public NetProtocol.OfferResolution how;
        public byte index;
        /// <summary>The client tick the ship moves again from; 0 while the run stays paused (more cards follow).</summary>
        public uint resumeTick;
    }

    public struct WelcomeMessage
    {
        public byte version;
        public byte[] resumeToken;
        public uint serverTick;
        public byte tickRate, snapshotEvery;
        public int seed;
        public ShipSnapshot ship;
    }

    /// <summary>One enemy in a batch, as decoded (position already back in world space).</summary>
    public struct NetEnemy
    {
        public int unitId;
        public byte definition;
        public Vector3 position;
        public byte heading, hp, flags;
    }

    /// <summary>A batch's header.</summary>
    public struct EnemyBatchHeader
    {
        public uint serverTick;
        public byte batchIndex, batchCount;
        public bool full;
        /// <summary>More enemies were in range than a snapshot carries: the client removes nothing on it.</summary>
        public bool truncated;
        public float originX, originZ;
        public ushort count;
    }

    /// <summary>The message bodies, on a plain byte buffer (testable without a network; sent as one named message each).</summary>
    public static class NetCodec
    {
        // ---- Input ------------------------------------------------------------------------------------------------

        /// <summary>Inputs newest first: <paramref name="inputs"/>[0] is tick <paramref name="newestTick"/>.</summary>
        public static void WriteInputs(NetWriter w, uint newestTick, NetInput[] inputs, int count)
        {
            w.U8(NetProtocol.Version);
            w.U32(newestTick);
            w.U8((byte)count);
            for (var i = 0; i < count; i++)
            {
                w.I8(inputs[i].steer);
                w.I8(inputs[i].throttle);
                w.U8(inputs[i].buttons);
            }
        }

        /// <summary>Reads an input packet; anything after the inputs is not read (its length goes to <paramref name="ignoredBytes"/>).</summary>
        public static bool ReadInputs(NetReader r, out uint newestTick, NetInput[] into, out int count, out int ignoredBytes)
        {
            newestTick = 0; count = 0; ignoredBytes = 0;
            if (r.Remaining < 6 || r.U8() != NetProtocol.Version) return false;
            newestTick = r.U32();
            var n = r.U8();
            if (n > into.Length || r.Remaining < n * 3) return false;
            for (var i = 0; i < n; i++) into[i] = new NetInput { steer = r.I8(), throttle = r.I8(), buttons = r.U8() };
            count = n;
            ignoredBytes = r.Remaining;
            return true;
        }

        // ---- Ship ---------------------------------------------------------------------------------------------------

        public static void WriteMotion(NetWriter w, in PlayerShipController.MotionState m)
        {
            w.Vec3(m.position);
            w.F32(m.rotation.x); w.F32(m.rotation.y); w.F32(m.rotation.z); w.F32(m.rotation.w);
            w.F32(m.velocity.x); w.F32(m.velocity.z);
            w.F32(m.knockback.x); w.F32(m.knockback.z);
            w.F32(m.knockbackDecay); w.F32(m.yawRate); w.F32(m.steerCommit); w.F32(m.lastSteer); w.F32(m.reverseTurnTimer);
            w.F32(m.slowMultiplier); w.F32(m.slowTimer); w.F32(m.moveEffectMultiplier); w.F32(m.moveEffectTimer);
            w.F32(m.moveSpeedMultiplier); w.F32(m.turnRateMultiplier); w.F32(m.runSpeedMultiplier); w.F32(m.targetRoll);
            w.F32(m.stepTime); w.F32(m.nextIslandImpactAt); w.F32(m.nextIslandDamageAt);
            w.U8(m.movementEnabled ? (byte)1 : (byte)0);
        }

        public static PlayerShipController.MotionState ReadMotion(NetReader r)
        {
            var m = new PlayerShipController.MotionState();
            m.position = r.Vec3();
            m.rotation = new Quaternion(r.F32(), r.F32(), r.F32(), r.F32());
            m.velocity = new Vector3(r.F32(), 0f, r.F32());
            m.knockback = new Vector3(r.F32(), 0f, r.F32());
            m.knockbackDecay = r.F32(); m.yawRate = r.F32(); m.steerCommit = r.F32(); m.lastSteer = r.F32(); m.reverseTurnTimer = r.F32();
            m.slowMultiplier = r.F32(); m.slowTimer = r.F32(); m.moveEffectMultiplier = r.F32(); m.moveEffectTimer = r.F32();
            m.moveSpeedMultiplier = r.F32(); m.turnRateMultiplier = r.F32(); m.runSpeedMultiplier = r.F32(); m.targetRoll = r.F32();
            m.stepTime = r.F32(); m.nextIslandImpactAt = r.F32(); m.nextIslandDamageAt = r.F32();
            m.movementEnabled = r.U8() != 0;
            return m;
        }

        public static void WriteShip(NetWriter w, in ShipSnapshot s)
        {
            w.U8(NetProtocol.Version);
            w.U32(s.serverTick);
            w.U32(s.ackTick);
            WriteMotion(w, s.motion);
            w.F32(s.boost.boostTimer); w.F32(s.boost.cooldownTimer);
            w.F32(s.hull); w.F32(s.maxHull); w.F32(s.runTime);
            w.U16(s.level);
            w.VarU32(s.kills); w.VarU32(s.gold);
            w.U8(s.flags);
        }

        public static bool ReadShip(NetReader r, out ShipSnapshot s)
        {
            s = default;
            if (r.Remaining < 1 || r.U8() != NetProtocol.Version) return false;
            s.serverTick = r.U32();
            s.ackTick = r.U32();
            s.motion = ReadMotion(r);
            s.boost = new ShipBoostController.NetState { boostTimer = r.F32(), cooldownTimer = r.F32() };
            s.hull = r.F32(); s.maxHull = r.F32(); s.runTime = r.F32();
            s.level = r.U16();
            s.kills = r.VarU32(); s.gold = r.VarU32();
            s.flags = r.U8();
            return true;
        }

        public static void WriteWelcome(NetWriter w, in WelcomeMessage m)
        {
            w.U8(NetProtocol.Version);
            w.Bytes(m.resumeToken, NetProtocol.ResumeTokenBytes);
            w.U32(m.serverTick);
            w.U8(m.tickRate); w.U8(m.snapshotEvery);
            w.I32(m.seed);
            WriteShip(w, m.ship);
        }

        public static bool ReadWelcome(NetReader r, out WelcomeMessage m)
        {
            m = default;
            if (r.Remaining < 1 || r.U8() != NetProtocol.Version) return false;
            m.version = NetProtocol.Version;
            m.resumeToken = r.Bytes(NetProtocol.ResumeTokenBytes);
            m.serverTick = r.U32();
            m.tickRate = r.U8(); m.snapshotEvery = r.U8();
            m.seed = r.I32();
            return ReadShip(r, out m.ship);
        }

        // ---- Enemies ------------------------------------------------------------------------------------------------

        public static void WriteBatchHeader(NetWriter w, in EnemyBatchHeader h)
        {
            w.U8(NetProtocol.Version);
            w.U32(h.serverTick);
            w.U8(h.batchIndex); w.U8(h.batchCount);
            w.U8((byte)((h.full ? 1 : 0) | (h.truncated ? 2 : 0)));
            w.F32(h.originX); w.F32(h.originZ);
            w.U16(h.count);
        }

        public static bool ReadBatchHeader(NetReader r, out EnemyBatchHeader h)
        {
            h = default;
            if (r.Remaining < 1 || r.U8() != NetProtocol.Version) return false;
            h.serverTick = r.U32();
            h.batchIndex = r.U8(); h.batchCount = r.U8();
            var flags = r.U8();
            h.full = (flags & 1) != 0;
            h.truncated = (flags & 2) != 0;
            h.originX = r.F32(); h.originZ = r.F32();
            h.count = r.U16();
            return true;
        }

        public static short Quantize(float offset) => (short)Mathf.Clamp(Mathf.RoundToInt(offset / NetProtocol.PositionStep), short.MinValue, short.MaxValue);
        public static float Dequantize(short q) => q * NetProtocol.PositionStep;

        public static byte HeadingByte(Vector3 velocity)
        {
            if (velocity.x * velocity.x + velocity.z * velocity.z < 1e-4f) return 0;
            var degrees = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            return (byte)(Mathf.RoundToInt(degrees / 360f * 256f) & 0xFF);
        }

        public static float HeadingDegrees(byte heading) => heading * 360f / 256f;

        public static void WriteEnemy(NetWriter w, in EnemyBatchHeader h, int unitId, int definition, Vector3 position, Vector3 velocity, float hpFraction, byte flags)
        {
            w.VarU32((uint)unitId);
            w.U8((byte)Mathf.Clamp(definition, 0, 255));
            w.I16(Quantize(position.x - h.originX));
            w.I16(Quantize(position.z - h.originZ));
            w.U8(HeadingByte(velocity));
            w.U8((byte)Mathf.Clamp(Mathf.RoundToInt(hpFraction * 255f), 0, 255));
            w.U8(flags);
        }

        public static NetEnemy ReadEnemy(NetReader r, in EnemyBatchHeader h, float y) => new NetEnemy
        {
            unitId = (int)r.VarU32(),
            definition = r.U8(),
            position = new Vector3(h.originX + Dequantize(r.I16()), y, h.originZ + Dequantize(r.I16())),
            heading = r.U8(),
            hp = r.U8(),
            flags = r.U8(),
        };

        // ---- Definitions, end ---------------------------------------------------------------------------------------

        public static void WriteDefs(NetWriter w, (byte index, string id)[] defs, int count)
        {
            w.U8(NetProtocol.Version);
            w.U8((byte)count);
            for (var i = 0; i < count; i++) { w.U8(defs[i].index); w.Str(defs[i].id); }
        }

        public static bool ReadDefs(NetReader r, Action<byte, string> each)
        {
            if (r.Remaining < 2 || r.U8() != NetProtocol.Version) return false;
            var n = r.U8();
            for (var i = 0; i < n; i++) { var index = r.U8(); each(index, r.Str()); }
            return true;
        }

        public static void WriteEnd(NetWriter w, NetProtocol.EndReason reason, float runTime, uint kills)
        {
            w.U8(NetProtocol.Version);
            w.U8((byte)reason);
            w.F32(runTime);
            w.VarU32(kills);
        }

        public static bool ReadEnd(NetReader r, out NetProtocol.EndReason reason, out float runTime, out uint kills)
        {
            reason = 0; runTime = 0f; kills = 0;
            if (r.Remaining < 1 || r.U8() != NetProtocol.Version) return false;
            reason = (NetProtocol.EndReason)r.U8();
            runTime = r.F32();
            kills = r.VarU32();
            return true;
        }

        // ---- Level-ups (V5 M9b part 2b) ----------------------------------------------------------------------------

        /// <summary>At most this many cards and stat lines a card (a level-up deals 3-4).</summary>
        public const int MaxOffers = 8, MaxLines = 16;

        public static void WriteOffer(NetWriter w, OfferMessage m)
        {
            w.U8(NetProtocol.Version);
            w.U16(m.seq);
            w.U32(m.pauseFrom);
            w.U8(m.refreshesRemaining);
            w.U8(m.pendingLevelUps);
            w.F32(m.timeoutSeconds);
            var n = Math.Min(m.offers.Count, MaxOffers);
            w.U8((byte)n);
            for (var i = 0; i < n; i++)
            {
                var o = m.offers[i];
                w.U8((byte)o.Kind);
                w.Str(o.Id);
                w.Str(o.EvolutionId);
                w.U8((byte)Mathf.Clamp(o.Rarity, 0, 255));
                w.U8((byte)Mathf.Clamp(o.CurrentLevel, 0, 255));
                w.U8((byte)Mathf.Clamp(o.NextLevel, 0, 255));
                w.U8((byte)((o.IsWeapon ? 1 : 0) | (o.IsNew ? 2 : 0) | (o.Id == null ? 4 : 0) | (o.EvolutionId == null ? 8 : 0)));
                var lines = o.Lines != null ? Math.Min(o.Lines.Length, MaxLines) : 0;
                w.U8((byte)(o.Lines == null ? 255 : lines));
                for (var j = 0; j < lines; j++) { w.U8((byte)o.Lines[j].Axis); w.F32(o.Lines[j].Value); }
            }
        }

        public static bool ReadOffer(NetReader r, out OfferMessage m)
        {
            m = null;
            if (r.Remaining < 1 || r.U8() != NetProtocol.Version) return false;
            var msg = new OfferMessage { seq = r.U16(), pauseFrom = r.U32(), refreshesRemaining = r.U8(), pendingLevelUps = r.U8(), timeoutSeconds = r.F32() };
            var n = r.U8();
            if (n > MaxOffers) return false;
            for (var i = 0; i < n; i++)
            {
                var o = new Offer { Kind = (OfferKind)r.U8(), Id = r.Str(), EvolutionId = r.Str(), Rarity = r.U8(), CurrentLevel = r.U8(), NextLevel = r.U8() };
                var flags = r.U8();
                o.IsWeapon = (flags & 1) != 0;
                o.IsNew = (flags & 2) != 0;
                if ((flags & 4) != 0) o.Id = null;
                if ((flags & 8) != 0) o.EvolutionId = null;
                var lines = r.U8();
                if (lines != 255)
                {
                    if (lines > MaxLines) return false;
                    o.Lines = new UpgradeLine[lines];
                    for (var j = 0; j < lines; j++) o.Lines[j] = new UpgradeLine((ConfigEnums.UpgradeAxis)r.U8(), r.F32());
                }
                msg.offers.Add(o);
            }
            m = msg;
            return true;
        }

        public static void WritePick(NetWriter w, in PickMessage m)
        {
            w.U8(NetProtocol.Version);
            w.U16(m.seq);
            w.U8((byte)m.action);
            w.U8(m.index);
            w.U32(m.resumeTick);
        }

        public static bool ReadPick(NetReader r, out PickMessage m)
        {
            m = default;
            if (r.Remaining < 9 || r.U8() != NetProtocol.Version) return false;
            m.seq = r.U16();
            m.action = (NetProtocol.PickAction)r.U8();
            m.index = r.U8();
            m.resumeTick = r.U32();
            return true;
        }

        public static void WriteOfferClosed(NetWriter w, in OfferClosedMessage m)
        {
            w.U8(NetProtocol.Version);
            w.U16(m.seq);
            w.U8((byte)m.how);
            w.U8(m.index);
            w.U32(m.resumeTick);
        }

        public static bool ReadOfferClosed(NetReader r, out OfferClosedMessage m)
        {
            m = default;
            if (r.Remaining < 9 || r.U8() != NetProtocol.Version) return false;
            m.seq = r.U16();
            m.how = (NetProtocol.OfferResolution)r.U8();
            m.index = r.U8();
            m.resumeTick = r.U32();
            return true;
        }
    }

    /// <summary>A growable little-endian byte writer (reused per message).</summary>
    public sealed class NetWriter
    {
        byte[] _buffer;
        public int Length { get; private set; }
        public byte[] Buffer => _buffer;

        public NetWriter(int capacity = 256) { _buffer = new byte[capacity]; }

        public void Clear() => Length = 0;

        void Ensure(int more)
        {
            if (Length + more <= _buffer.Length) return;
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, Length + more));
        }

        public void U8(byte v) { Ensure(1); _buffer[Length++] = v; }
        public void I8(sbyte v) => U8((byte)v);
        public void U16(ushort v) { Ensure(2); _buffer[Length++] = (byte)v; _buffer[Length++] = (byte)(v >> 8); }
        public void I16(short v) => U16((ushort)v);
        public void U32(uint v) { Ensure(4); for (var i = 0; i < 4; i++) _buffer[Length++] = (byte)(v >> (8 * i)); }
        public void I32(int v) => U32((uint)v);
        public void F32(float v) => U32((uint)BitConverter.SingleToInt32Bits(v));
        public void Vec3(Vector3 v) { F32(v.x); F32(v.y); F32(v.z); }

        public void VarU32(uint v)
        {
            while (v >= 0x80) { U8((byte)(v | 0x80)); v >>= 7; }
            U8((byte)v);
        }

        public void Bytes(byte[] bytes, int count)
        {
            Ensure(count);
            for (var i = 0; i < count; i++) _buffer[Length++] = bytes != null && i < bytes.Length ? bytes[i] : (byte)0;
        }

        public void Str(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var n = Math.Min(bytes.Length, 255);
            U8((byte)n);
            Bytes(bytes, n);
        }

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Array.Copy(_buffer, copy, Length);
            return copy;
        }
    }

    /// <summary>The matching reader. Reading past the end throws (a malformed message is dropped by its handler).</summary>
    public sealed class NetReader
    {
        readonly byte[] _buffer;
        readonly int _end;
        public int Position { get; private set; }
        public int Remaining => _end - Position;

        public NetReader(byte[] buffer, int length = -1)
        {
            _buffer = buffer;
            _end = length < 0 ? buffer.Length : length;
        }

        byte Next()
        {
            if (Position >= _end) throw new IndexOutOfRangeException("net message ended early");
            return _buffer[Position++];
        }

        public byte U8() => Next();
        public sbyte I8() => (sbyte)Next();
        public ushort U16() => (ushort)(Next() | Next() << 8);
        public short I16() => (short)U16();
        public uint U32() { uint v = 0; for (var i = 0; i < 4; i++) v |= (uint)Next() << (8 * i); return v; }
        public int I32() => (int)U32();
        public float F32() => BitConverter.Int32BitsToSingle((int)U32());
        public Vector3 Vec3() => new Vector3(F32(), F32(), F32());

        public uint VarU32()
        {
            uint v = 0;
            for (var shift = 0; shift < 35; shift += 7)
            {
                var b = Next();
                v |= (uint)(b & 0x7F) << shift;
                if (b < 0x80) return v;
            }
            throw new FormatException("varint too long");
        }

        public byte[] Bytes(int count)
        {
            var bytes = new byte[count];
            for (var i = 0; i < count; i++) bytes[i] = Next();
            return bytes;
        }

        public string Str()
        {
            var n = U8();
            return Encoding.UTF8.GetString(Bytes(n));
        }
    }
}

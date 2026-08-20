using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>Decoded on-chain snapshot (quantised).</summary>
    [Serializable]
    public struct SnapshotPlayerState
    {
        public byte slot;
        public short x;
        public short z;
        public ushort heading;
        public byte hpPct;
        public byte level;
        public bool downed;
        public uint bank;
        public ushort kills;
    }

    [Serializable]
    public struct SnapshotEnemyState
    {
        public ushort id;
        public byte archetype;
        public short x;
        public short z;
        public byte hpPct;
        public byte element;
    }

    [Serializable]
    public struct SnapshotProjectileState
    {
        public ushort id;
        public short x;
        public short z;
        public short vx;
        public short vz;
    }

    [Serializable]
    public struct SnapshotOrbState
    {
        public ushort id;
        public short x;
        public short z;
        public uint value;
    }

    public enum GameEventKind : byte
    {
        None = 0,
        Fire = 1,
        Hit = 2,
        Death = 3,
        LevelUp = 4,
        Evolution = 5,
        Revive = 6,
        SpawnEnemy = 7,
        BossTelegraph = 8,
        PlayerKill = 9,
    }

    [Serializable]
    public struct GameEventState
    {
        public GameEventKind kind;
        public uint tick;
        public ushort actor;
        public ushort target;
        public short x;
        public short z;
        public ushort value;
    }

    public class MatchSnapshot
    {
        public uint tick;
        public byte stormRadiusPct = 100;
        public ushort matchTimerSec;
        public SnapshotPlayerState[] players = Array.Empty<SnapshotPlayerState>();
        public SnapshotEnemyState[] enemies = Array.Empty<SnapshotEnemyState>();
        public SnapshotProjectileState[] enemyProjectiles = Array.Empty<SnapshotProjectileState>();
        public SnapshotOrbState[] orbs = Array.Empty<SnapshotOrbState>();
        public List<GameEventState> events = new();
    }

    public static class SnapshotDecoder
    {
        /// <summary>8-byte disc + SnapshotAccount header (repr C, 48 bytes).</summary>
        public const int HeaderLen = 56;
        /// <summary>
        /// Stride between SnapshotPlayer tail entries. The on-chain Rust struct is 16 bytes;
        /// its writer uses bytes 0..15 with no trailing padding.
        /// </summary>
        public const int PlayerStride = 16;
        public const int EnemyStride = 10;
        public const int ProjectileStride = 10;
        public const int OrbStride = 10;
        public const int EventStride = 16;
        public const int EventRing = 32;

        public static MatchSnapshot Decode(ReadOnlySpan<byte> data, byte minPlayerCount = 0)
        {
            var snap = new MatchSnapshot();
            if (data.Length < HeaderLen) return snap;

            snap.tick = BitConverter.ToUInt32(data.Slice(40, 4));
            var headerCount = data[44];
            var enemyCount = data[45];
            var projectileCount = data[46];
            var maxPlayers = data.Length > 50 ? data[50] : (byte)12;
            snap.stormRadiusPct = data.Length > 51 ? data[51] : (byte)100;
            snap.matchTimerSec = data.Length > 53 ? BitConverter.ToUInt16(data.Slice(52, 2)) : (ushort)0;
            var orbCount = data.Length > 54 ? data[54] : (byte)0;

            var playerCount = Math.Max(headerCount, minPlayerCount);
            if (maxPlayers > 0)
                playerCount = Math.Min(playerCount, maxPlayers);

            var offset = HeaderLen;
            snap.players = new SnapshotPlayerState[playerCount];
            for (var i = 0; i < playerCount && offset + PlayerStride <= data.Length; i++)
            {
                snap.players[i] = ReadPlayer(data, offset, (byte)i);
                offset += PlayerStride;
            }

            snap.enemies = new SnapshotEnemyState[enemyCount];
            for (var i = 0; i < enemyCount && offset + EnemyStride <= data.Length; i++)
            {
                snap.enemies[i] = new SnapshotEnemyState
                {
                    id = BitConverter.ToUInt16(data.Slice(offset, 2)),
                    archetype = data[offset + 2],
                    x = BitConverter.ToInt16(data.Slice(offset + 3, 2)),
                    z = BitConverter.ToInt16(data.Slice(offset + 5, 2)),
                    hpPct = data[offset + 7],
                    element = data[offset + 8]
                };
                offset += EnemyStride;
            }

            snap.enemyProjectiles = new SnapshotProjectileState[projectileCount];
            for (var i = 0; i < projectileCount && offset + ProjectileStride <= data.Length; i++)
            {
                snap.enemyProjectiles[i] = new SnapshotProjectileState
                {
                    id = BitConverter.ToUInt16(data.Slice(offset, 2)),
                    x = BitConverter.ToInt16(data.Slice(offset + 2, 2)),
                    z = BitConverter.ToInt16(data.Slice(offset + 4, 2)),
                    vx = BitConverter.ToInt16(data.Slice(offset + 6, 2)),
                    vz = BitConverter.ToInt16(data.Slice(offset + 8, 2))
                };
                offset += ProjectileStride;
            }

            var actualOrbs = Math.Min(orbCount, (byte)((data.Length - offset) / OrbStride));
            snap.orbs = new SnapshotOrbState[actualOrbs];
            for (var i = 0; i < actualOrbs && offset + OrbStride <= data.Length; i++)
            {
                snap.orbs[i] = new SnapshotOrbState
                {
                    id = BitConverter.ToUInt16(data.Slice(offset, 2)),
                    x = BitConverter.ToInt16(data.Slice(offset + 2, 2)),
                    z = BitConverter.ToInt16(data.Slice(offset + 4, 2)),
                    value = BitConverter.ToUInt32(data.Slice(offset + 6, 4))
                };
                offset += OrbStride;
            }

            var eventHead = data.Length > 47 ? data[47] : (byte)0;
            snap.events = new List<GameEventState>();
            for (var i = 0; i < EventRing && offset + EventStride <= data.Length; i++)
            {
                var ev = ReadEvent(data, offset);
                if (ev.kind != GameEventKind.None)
                    snap.events.Add(ev);
                offset += EventStride;
            }

            return snap;
        }

        static SnapshotPlayerState ReadPlayer(ReadOnlySpan<byte> data, int offset, byte index)
        {
            // Player tail is indexed by array position; on-chain slot byte may be unset on tick 0.
            return new SnapshotPlayerState
            {
                slot = index,
                x = BitConverter.ToInt16(data.Slice(offset + 1, 2)),
                z = BitConverter.ToInt16(data.Slice(offset + 3, 2)),
                heading = BitConverter.ToUInt16(data.Slice(offset + 5, 2)),
                hpPct = data[offset + 7],
                level = data[offset + 8],
                downed = data[offset + 9] != 0,
                bank = BitConverter.ToUInt32(data.Slice(offset + 10, 4)),
                kills = BitConverter.ToUInt16(data.Slice(offset + 14, 2))
            };
        }

        static GameEventState ReadEvent(ReadOnlySpan<byte> data, int offset)
        {
            return new GameEventState
            {
                kind = (GameEventKind)data[offset],
                tick = BitConverter.ToUInt32(data.Slice(offset + 1, 4)),
                actor = BitConverter.ToUInt16(data.Slice(offset + 5, 2)),
                target = BitConverter.ToUInt16(data.Slice(offset + 7, 2)),
                x = BitConverter.ToInt16(data.Slice(offset + 9, 2)),
                z = BitConverter.ToInt16(data.Slice(offset + 11, 2)),
                value = BitConverter.ToUInt16(data.Slice(offset + 13, 2))
            };
        }

        public static bool IsPlayerSlotPopulated(in SnapshotPlayerState p)
        {
            if (p.hpPct > 0) return true;
            return p.x != 0 || p.z != 0 || p.heading != 0 || p.level > 0;
        }

        public static bool TryGetPlayerBySlot(MatchSnapshot snap, byte slot, out SnapshotPlayerState player)
        {
            player = default;
            if (snap?.players == null) return false;

            if (slot < snap.players.Length)
            {
                player = snap.players[slot];
                player.slot = slot;
                return true;
            }

            foreach (var p in snap.players)
            {
                if (p.slot != slot) continue;
                player = p;
                return true;
            }

            return false;
        }

        public static bool IsRemotePlayerVisible(in SnapshotPlayerState p, byte expectedPlayers)
        {
            if (IsPlayerSlotPopulated(p)) return true;
            return expectedPlayers > 1;
        }
    }
}

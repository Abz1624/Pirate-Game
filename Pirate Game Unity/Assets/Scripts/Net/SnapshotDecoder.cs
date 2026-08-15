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

    public enum GameEventKind : byte
    {
        None = 0,
        Fire = 1,
        Hit = 2,
        Death = 3,
        LevelUp = 4,
        Evolution = 5,
        Revive = 6,
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
        public SnapshotPlayerState[] players = Array.Empty<SnapshotPlayerState>();
        public SnapshotEnemyState[] enemies = Array.Empty<SnapshotEnemyState>();
        public SnapshotProjectileState[] enemyProjectiles = Array.Empty<SnapshotProjectileState>();
        public List<GameEventState> events = new();
    }

    public static class SnapshotDecoder
    {
        /// <summary>8-byte disc + SnapshotAccount header (repr C)).</summary>
        public const int HeaderLen = 52;
        /// <summary>Stride between SnapshotPlayer tail entries (matches on-chain snapshot_tail).</summary>
        public const int PlayerStride = 12;

        /// <summary>Decode snapshot account bytes from chain.</summary>
        public static MatchSnapshot Decode(ReadOnlySpan<byte> data, byte minPlayerCount = 0)
        {
            var snap = new MatchSnapshot();
            if (data.Length < HeaderLen) return snap;

            snap.tick = BitConverter.ToUInt32(data.Slice(40, 4));
            var headerCount = data[44];
            var enemyCount = data[45];
            var projectileCount = data[46];
            var maxPlayers = data.Length > 50 ? data[50] : (byte)12;
            // Use the larger of header vs match account count (header can lag behind join).
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
            for (var i = 0; i < enemyCount && offset + 10 <= data.Length; i++)
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
                offset += 10;
            }

            snap.enemyProjectiles = new SnapshotProjectileState[projectileCount];
            for (var i = 0; i < projectileCount && offset + 10 <= data.Length; i++)
            {
                snap.enemyProjectiles[i] = new SnapshotProjectileState
                {
                    id = BitConverter.ToUInt16(data.Slice(offset, 2)),
                    x = BitConverter.ToInt16(data.Slice(offset + 2, 2)),
                    z = BitConverter.ToInt16(data.Slice(offset + 4, 2)),
                    vx = BitConverter.ToInt16(data.Slice(offset + 6, 2)),
                    vz = BitConverter.ToInt16(data.Slice(offset + 8, 2))
                };
                offset += 10;
            }

            return snap;
        }

        static SnapshotPlayerState ReadPlayer(ReadOnlySpan<byte> data, int offset, byte index)
        {
            var slot = data[offset];
            if (slot > 11)
                slot = index;
            return new SnapshotPlayerState
            {
                slot = slot,
                x = BitConverter.ToInt16(data.Slice(offset + 1, 2)),
                z = BitConverter.ToInt16(data.Slice(offset + 3, 2)),
                heading = BitConverter.ToUInt16(data.Slice(offset + 5, 2)),
                hpPct = data[offset + 7],
                level = data[offset + 8],
                downed = data[offset + 9] != 0
            };
        }

        public static bool IsPlayerSlotPopulated(in SnapshotPlayerState p)
        {
            if (p.hpPct > 0) return true;
            return p.x != 0 || p.z != 0 || p.heading != 0 || p.level > 0;
        }
    }
}

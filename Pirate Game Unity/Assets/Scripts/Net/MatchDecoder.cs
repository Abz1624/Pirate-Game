using System;
using Solana.Unity.Wallet;

namespace PirateGame.Net
{
    public static class MatchDecoder
    {
        const int Discriminator = 8;
        const int TideOffset = Discriminator + 40;
        const int CapacityOffset = Discriminator + 41;
        const int PlayerCountOffset = Discriminator + 43;
        public const int StateOffset = Discriminator + 44;
        public const int DelegatedOffset = Discriminator + 53;

        public const byte MatchStateLobby = 0;
        public const byte MatchStateRunning = 1;
        public const byte MatchStateEnded = 2;

        /// <summary>repr(C) PlayerSlot on-chain (32+32+1+1+4+4+4+2+4 + alignment padding).</summary>
        public const int PlayerSlotLen = 88;
        /// <summary>MatchAccount header is 56 bytes before the players array (repr(C) padding).</summary>
        public const int PlayersOffset = Discriminator + 56;
        public const int MinAccountBytes = PlayersOffset + 12 * PlayerSlotLen;

        public static byte ReadTide(ReadOnlySpan<byte> data)
        {
            if (data.Length <= TideOffset) return 0;
            return data[TideOffset];
        }

        public static byte ReadCapacity(ReadOnlySpan<byte> data)
        {
            if (data.Length <= CapacityOffset) return 0;
            return data[CapacityOffset];
        }

        public static string ReadAuthority(ReadOnlySpan<byte> data)
        {
            if (data.Length < Discriminator + 32) return "";
            return new PublicKey(data.Slice(Discriminator, 32).ToArray()).Key;
        }

        public static bool IsOpenLobby(ReadOnlySpan<byte> data)
        {
            if (data.Length < MinAccountBytes)
                return false;
            if (ReadState(data) != MatchStateLobby)
                return false;
            if (ReadDelegated(data) != 0)
                return false;
            var count = ReadPlayerCount(data);
            var cap = ReadCapacity(data);
            return cap > 0 && count < cap;
        }

        public static byte ReadPlayerCount(ReadOnlySpan<byte> data)
        {
            if (data.Length <= PlayerCountOffset) return 0;
            return data[PlayerCountOffset];
        }

        public static byte ReadState(ReadOnlySpan<byte> data)
        {
            if (data.Length <= StateOffset) return 0;
            return data[StateOffset];
        }

        public static byte ReadDelegated(ReadOnlySpan<byte> data)
        {
            if (data.Length <= DelegatedOffset) return 0;
            return data[DelegatedOffset];
        }

        /// <summary>Match is live on ER: delegated and Running.</summary>
        public static bool IsDelegatedAndRunning(ReadOnlySpan<byte> data)
        {
            return ReadDelegated(data) == 1 && ReadState(data) == MatchStateRunning;
        }

        public static bool TryFindPlayerSlot(ReadOnlySpan<byte> data, PublicKey owner, out byte slot)
        {
            return TryFindPlayerSlot(data, owner, null, out slot);
        }

        public static bool TryFindPlayerSlot(ReadOnlySpan<byte> data, PublicKey owner, PublicKey sessionKey, out byte slot)
        {
            slot = 0;
            if (data.Length < PlayersOffset + PlayerSlotLen) return false;
            var ownerBytes = owner.KeyBytes;
            ReadOnlySpan<byte> sessionBytes = default;
            var hasSession = sessionKey != null;
            if (hasSession)
                sessionBytes = sessionKey.KeyBytes;
            var playerCount = ReadPlayerCount(data);
            var searchMax = Math.Min(12, Math.Max((int)playerCount, 1));
            for (byte i = 0; i < searchMax; i++)
            {
                var off = PlayersOffset + i * PlayerSlotLen;
                if (data.Length < off + 64) break;
                if (data.Slice(off, 32).SequenceEqual(ownerBytes))
                {
                    slot = i;
                    return true;
                }
                if (hasSession && data.Slice(off + 32, 32).SequenceEqual(sessionBytes))
                {
                    slot = i;
                    return true;
                }
            }
            return false;
        }

        public static byte FindPlayerSlot(ReadOnlySpan<byte> data, PublicKey owner)
        {
            if (TryFindPlayerSlot(data, owner, out var slot))
                return slot;
            return 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;

namespace PirateGame.Net.Generated
{
    /// <summary>
    /// Hand-written Anchor instruction builders for pirate_coop.
    /// Discriminators: sha256("global:&lt;ix&gt;")[0..8].
    /// </summary>
    public static class PirateCoopProgram
    {
        public const string ProgramIdString = "EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC";
        public const string DelegationProgramIdString = "DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh";
        public const string SystemProgramIdString = "11111111111111111111111111111111";

        public static readonly PublicKey ProgramId = new(ProgramIdString);
        public static readonly PublicKey DelegationProgramId = new(DelegationProgramIdString);
        public static readonly PublicKey SystemProgramId = new(SystemProgramIdString);

        public static readonly byte[] DiscriminatorInitializeConfig = { 208, 127, 21, 1, 194, 190, 196, 70 };
        public static readonly byte[] DiscriminatorCreateMatch = { 107, 2, 184, 145, 70, 142, 17, 165 };
        public static readonly byte[] DiscriminatorInitWorld = { 10, 53, 110, 193, 166, 122, 251, 1 };
        public static readonly byte[] DiscriminatorExtendWorld = { 63, 4, 89, 230, 234, 193, 38, 53 };
        public static readonly byte[] DiscriminatorJoinMatch = { 244, 8, 47, 130, 192, 59, 179, 44 };
        public static readonly byte[] DiscriminatorSetReady = { 105, 78, 7, 162, 181, 167, 186, 43 };
        public static readonly byte[] DiscriminatorStartMatch = { 100, 246, 223, 181, 176, 101, 255, 19 };
        public static readonly byte[] DiscriminatorDelegateMatch = { 30, 116, 9, 69, 147, 61, 133, 238 };
        public static readonly byte[] DiscriminatorDelegateMatchAccount = { 197, 176, 73, 153, 24, 66, 77, 159 };
        public static readonly byte[] DiscriminatorDelegateSnapshotAccount = { 230, 199, 229, 92, 202, 128, 128, 204 };
        public static readonly byte[] DiscriminatorDelegateWorldAccount = { 77, 132, 97, 184, 102, 55, 249, 44 };
        public static readonly byte[] DiscriminatorSubmitInput = { 56, 184, 154, 91, 173, 63, 21, 138 };
        public static readonly byte[] DiscriminatorTickWorld = { 221, 177, 252, 140, 140, 234, 197, 15 };
        public static readonly byte[] DiscriminatorDraftPick = { 33, 48, 8, 117, 124, 77, 188, 90 };
        public static readonly byte[] DiscriminatorEndMatch = { 34, 116, 122, 191, 100, 222, 20, 117 };
        public static readonly byte[] DiscriminatorCreditProfile = { 9, 125, 43, 5, 62, 179, 12, 73 };

        public const byte MatchModeCoop = 0;
        public const byte MatchModePvPvE = 1;

        /// <summary>Solana max account growth per instruction (bytes).</summary>
        public const int WorldAccountChunkBytes = 10_240;
        /// <summary>8-byte discriminator + pirate_sim::WORLD_STATE_LEN (on-chain world_space()).</summary>
        public const int WorldAccountTotalBytes = 9_456;

        public static PublicKey ConfigPda() =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("config"));

        public static PublicKey MatchPda(PublicKey authority, ulong seed) =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("match"), authority.KeyBytes, U64Le(seed));

        public static PublicKey SnapshotPda(PublicKey matchPda) =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("snapshot"), matchPda.KeyBytes);

        public static PublicKey WorldPda(PublicKey matchPda) =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("world"), matchPda.KeyBytes);

        public static PublicKey ProfilePda(PublicKey owner) =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("profile"), owner.KeyBytes);

        public static PublicKey BufferPda(PublicKey pda) =>
            FindPda(ProgramId, Encoding.UTF8.GetBytes("buffer"), pda.KeyBytes);

        public static PublicKey DelegationRecordPda(PublicKey pda) =>
            FindPda(DelegationProgramId, Encoding.UTF8.GetBytes("delegation"), pda.KeyBytes);

        public static PublicKey DelegationMetadataPda(PublicKey pda) =>
            FindPda(DelegationProgramId, Encoding.UTF8.GetBytes("delegation-metadata"), pda.KeyBytes);

        static PublicKey FindPda(PublicKey program, params byte[][] seeds)
        {
            if (!PublicKey.TryFindProgramAddress(seeds, program, out var pda, out _))
                throw new InvalidOperationException("PDA derivation failed");
            return pda;
        }

        public static TransactionInstruction InitializeConfig(PublicKey authority, PublicKey config, uint version)
        {
            var data = new byte[12];
            DiscriminatorInitializeConfig.CopyTo(data, 0);
            var versionBytes = BitConverter.GetBytes(version);
            if (!BitConverter.IsLittleEndian) Array.Reverse(versionBytes);
            versionBytes.CopyTo(data, 8);
            return Ix(data,
                AccountMeta.Writable(authority, true),
                AccountMeta.Writable(config, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction CreateMatch(
            PublicKey authority,
            PublicKey config,
            PublicKey matchAccount,
            PublicKey snapshot,
            ulong seed,
            byte tide,
            byte capacity,
            byte mode)
        {
            var data = new byte[19];
            DiscriminatorCreateMatch.CopyTo(data, 0);
            U64Le(seed).CopyTo(data, 8);
            data[16] = tide;
            data[17] = capacity;
            data[18] = mode;
            return Ix(data,
                AccountMeta.Writable(authority, true),
                AccountMeta.ReadOnly(config, false),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(snapshot, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction InitWorld(
            PublicKey authority,
            PublicKey matchAccount,
            PublicKey world)
        {
            var data = new byte[8];
            DiscriminatorInitWorld.CopyTo(data, 0);
            return Ix(data,
                AccountMeta.Writable(authority, true),
                AccountMeta.ReadOnly(matchAccount, false),
                AccountMeta.Writable(world, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction ExtendWorld(
            PublicKey authority,
            PublicKey matchAccount,
            PublicKey world)
        {
            var data = new byte[8];
            DiscriminatorExtendWorld.CopyTo(data, 0);
            return Ix(data,
                AccountMeta.Writable(authority, true),
                AccountMeta.ReadOnly(matchAccount, false),
                AccountMeta.Writable(world, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction JoinMatch(PublicKey owner, PublicKey matchAccount, PublicKey sessionKey)
        {
            var data = new byte[40];
            DiscriminatorJoinMatch.CopyTo(data, 0);
            sessionKey.KeyBytes.CopyTo(data, 8);
            return Ix(data,
                AccountMeta.ReadOnly(owner, true),
                AccountMeta.Writable(matchAccount, false));
        }

        public static TransactionInstruction SetReady(PublicKey player, PublicKey matchAccount, bool ready)
        {
            var data = new byte[9];
            DiscriminatorSetReady.CopyTo(data, 0);
            data[8] = ready ? (byte)1 : (byte)0;
            return Ix(data,
                AccountMeta.ReadOnly(player, true),
                AccountMeta.Writable(matchAccount, false));
        }

        public static TransactionInstruction StartMatch(
            PublicKey authority,
            PublicKey matchAccount,
            PublicKey snapshot,
            PublicKey world)
        {
            return Ix(DiscriminatorStartMatch,
                AccountMeta.ReadOnly(authority, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(snapshot, false),
                AccountMeta.Writable(world, false));
        }

        public static TransactionInstruction DelegateMatch(
            PublicKey payer,
            PublicKey matchAccount,
            PublicKey snapshot,
            PublicKey world)
        {
            return Ix(DiscriminatorDelegateMatch,
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(snapshot, false),
                AccountMeta.Writable(world, false),
                AccountMeta.ReadOnly(ProgramId, false),
                AccountMeta.Writable(BufferPda(matchAccount), false),
                AccountMeta.Writable(DelegationRecordPda(matchAccount), false),
                AccountMeta.Writable(DelegationMetadataPda(matchAccount), false),
                AccountMeta.Writable(BufferPda(snapshot), false),
                AccountMeta.Writable(DelegationRecordPda(snapshot), false),
                AccountMeta.Writable(DelegationMetadataPda(snapshot), false),
                AccountMeta.Writable(BufferPda(world), false),
                AccountMeta.Writable(DelegationRecordPda(world), false),
                AccountMeta.Writable(DelegationMetadataPda(world), false),
                AccountMeta.ReadOnly(DelegationProgramId, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        /// <summary>Split delegation (one account per tx) to stay under BPF heap limits.</summary>
        public static TransactionInstruction DelegateMatchAccount(PublicKey payer, PublicKey matchAccount)
        {
            return Ix(DiscriminatorDelegateMatchAccount,
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.ReadOnly(ProgramId, false),
                AccountMeta.Writable(BufferPda(matchAccount), false),
                AccountMeta.Writable(DelegationRecordPda(matchAccount), false),
                AccountMeta.Writable(DelegationMetadataPda(matchAccount), false),
                AccountMeta.ReadOnly(DelegationProgramId, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction DelegateSnapshotAccount(
            PublicKey payer,
            PublicKey matchAccount,
            PublicKey snapshot)
        {
            return Ix(DiscriminatorDelegateSnapshotAccount,
                AccountMeta.Writable(payer, true),
                AccountMeta.ReadOnly(matchAccount, false),
                AccountMeta.Writable(snapshot, false),
                AccountMeta.ReadOnly(ProgramId, false),
                AccountMeta.Writable(BufferPda(snapshot), false),
                AccountMeta.Writable(DelegationRecordPda(snapshot), false),
                AccountMeta.Writable(DelegationMetadataPda(snapshot), false),
                AccountMeta.ReadOnly(DelegationProgramId, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction DelegateWorldAccount(
            PublicKey payer,
            PublicKey matchAccount,
            PublicKey world)
        {
            return Ix(DiscriminatorDelegateWorldAccount,
                AccountMeta.Writable(payer, true),
                AccountMeta.ReadOnly(matchAccount, false),
                AccountMeta.Writable(world, false),
                AccountMeta.ReadOnly(ProgramId, false),
                AccountMeta.Writable(BufferPda(world), false),
                AccountMeta.Writable(DelegationRecordPda(world), false),
                AccountMeta.Writable(DelegationMetadataPda(world), false),
                AccountMeta.ReadOnly(DelegationProgramId, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static TransactionInstruction SubmitInput(
            PublicKey player,
            PublicKey matchAccount,
            PublicKey world,
            sbyte throttle,
            sbyte steer,
            byte buttons)
        {
            var data = new byte[11];
            DiscriminatorSubmitInput.CopyTo(data, 0);
            data[8] = (byte)throttle;
            data[9] = (byte)steer;
            data[10] = buttons;
            return Ix(data,
                AccountMeta.ReadOnly(player, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(world, false));
        }

        public static TransactionInstruction TickWorld(
            PublicKey payer,
            PublicKey matchAccount,
            PublicKey snapshot,
            PublicKey world,
            PublicKey config,
            byte maxTicks = 1)
        {
            var data = new byte[9];
            DiscriminatorTickWorld.CopyTo(data, 0);
            data[8] = maxTicks;
            return Ix(data,
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(snapshot, false),
                AccountMeta.Writable(world, false),
                AccountMeta.ReadOnly(config, false));
        }

        public static TransactionInstruction DraftPick(
            PublicKey player,
            PublicKey matchAccount,
            PublicKey world,
            byte cardIndex,
            byte[] vrfSeed32)
        {
            var data = new byte[41];
            DiscriminatorDraftPick.CopyTo(data, 0);
            data[8] = cardIndex;
            if (vrfSeed32 == null || vrfSeed32.Length != 32)
                Array.Clear(data, 9, 32);
            else
                vrfSeed32.CopyTo(data, 9);
            return Ix(data,
                AccountMeta.ReadOnly(player, true),
                AccountMeta.Writable(matchAccount, false),
                AccountMeta.Writable(world, false));
        }

        public static TransactionInstruction CreditProfile(
            PublicKey payer,
            PublicKey matchAccount,
            PublicKey profile)
        {
            return Ix(DiscriminatorCreditProfile,
                AccountMeta.Writable(payer, true),
                AccountMeta.ReadOnly(matchAccount, false),
                AccountMeta.Writable(profile, false),
                AccountMeta.ReadOnly(SystemProgramId, false));
        }

        public static byte[] U64Le(ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        static TransactionInstruction Ix(byte[] data, params AccountMeta[] keys)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>(keys),
                Data = data
            };
        }
    }
}

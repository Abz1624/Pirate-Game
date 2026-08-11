// Auto-generated style bindings for pirate_coop program.
// Regenerate with: dotnet tool install -g Solana.Unity.Anchor.Tool
//   dotnet anchorgen -i onchain/target/idl/pirate_coop.json -o "Pirate Game Unity/Assets/Scripts/Net/Generated/PirateCoop.cs"

using System;

namespace PirateGame.Net.Generated
{
    public static class PirateCoopProgram
    {
        public const string ProgramId = "PirCoop11111111111111111111111111111111111";

        public static readonly byte[] DiscriminatorInitializeConfig = { 0x01 };
        public static readonly byte[] DiscriminatorCreateMatch = { 0x02 };
        public static readonly byte[] DiscriminatorDelegateMatch = { 0x03 };
        public static readonly byte[] DiscriminatorJoinMatch = { 0x04 };
        public static readonly byte[] DiscriminatorSetReady = { 0x05 };
        public static readonly byte[] DiscriminatorStartMatch = { 0x06 };
        public static readonly byte[] DiscriminatorSubmitInput = { 0x07 };
        public static readonly byte[] DiscriminatorEndMatch = { 0x08 };
        public static readonly byte[] DiscriminatorCreditProfile = { 0x09 };
        public static readonly byte[] DiscriminatorDraftPick = { 0x0A };

        public static byte[] BuildSubmitInput(sbyte throttle, sbyte steer, byte buttons)
        {
            var data = new byte[1 + 1 + 1 + 1];
            data[0] = DiscriminatorSubmitInput[0];
            data[1] = (byte)throttle;
            data[2] = (byte)steer;
            data[3] = buttons;
            return data;
        }

        public static byte[] BuildCreateMatch(ulong seed, byte tide, byte capacity)
        {
            var data = new byte[1 + 8 + 1 + 1];
            data[0] = DiscriminatorCreateMatch[0];
            BitConverter.TryWriteBytes(data.AsSpan(1, 8), seed);
            data[9] = tide;
            data[10] = capacity;
            return data;
        }
    }
}

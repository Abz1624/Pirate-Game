using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b (I9b.1): a bad link for the tests, with Unity Transport's simulator pipeline stage: each configured
    /// transport delays and drops the packets it sends (send side only, so 75 ms on both ends is a 150 ms round trip).
    /// Netcode 2.x no longer wires its own debug simulator (it moved to the Multiplayer Tools package), so this driver
    /// constructor adds the stage to the transport's default pipelines itself. Editor and test use only.
    /// </summary>
    public sealed class NetLinkSimulator : INetworkStreamDriverConstructor
    {
        public struct Link
        {
            public int sendDelayMs;
            public int sendJitterMs;
            public int sendDropPercent;
            public uint seed;
        }

        static readonly NetLinkSimulator Instance = new NetLinkSimulator();
        static readonly Dictionary<UnityTransport, Link> Links = new Dictionary<UnityTransport, Link>();

        /// <summary>Gives <paramref name="transport"/> a bad link from its next start (call before StartServer/StartClient).</summary>
        public static void Configure(UnityTransport transport, Link link)
        {
            Links[transport] = link;
            UnityTransport.s_DriverConstructor = Instance;
        }

        public static void Clear()
        {
            Links.Clear();
            if (UnityTransport.s_DriverConstructor == Instance) UnityTransport.s_DriverConstructor = null;
        }

        public void CreateDriver(UnityTransport transport, out NetworkDriver driver, out NetworkPipeline unreliableFragmentedPipeline,
            out NetworkPipeline unreliableSequencedFragmentedPipeline, out NetworkPipeline reliableSequencedPipeline)
        {
            if (!Links.TryGetValue(transport, out var link))
            {
                transport.CreateDriver(transport, out driver, out unreliableFragmentedPipeline, out unreliableSequencedFragmentedPipeline, out reliableSequencedPipeline);
                return;
            }
            var settings = transport.GetDefaultNetworkSettings();
            settings.WithSimulatorStageParameters(
                maxPacketCount: 2000,
                mode: ApplyMode.SentPacketsOnly,
                packetDelayMs: link.sendDelayMs,
                packetJitterMs: link.sendJitterMs,
                packetDropPercentage: link.sendDropPercent,
                randomSeed: link.seed == 0 ? 1u : link.seed);
            driver = transport.UseWebSockets
                ? NetworkDriver.Create(new WebSocketNetworkInterface(), settings)
                : NetworkDriver.Create(new UDPNetworkInterface(), settings);
            transport.GetDefaultPipelineConfigurations(ref driver, out var unreliable, out var unreliableSequenced, out var reliable);
            unreliableFragmentedPipeline = driver.CreatePipeline(WithSimulator(unreliable));
            unreliableSequencedFragmentedPipeline = driver.CreatePipeline(WithSimulator(unreliableSequenced));
            reliableSequencedPipeline = driver.CreatePipeline(WithSimulator(reliable));
        }

        static NativeArray<NetworkPipelineStageId> WithSimulator(NativeArray<NetworkPipelineStageId> stages)
        {
            var simulator = NetworkPipelineStageId.Get<SimulatorPipelineStage>();
            foreach (var stage in stages)
                if (stage.Equals(simulator)) return stages;
            var result = new NativeArray<NetworkPipelineStageId>(stages.Length + 1, Allocator.Temp);
            for (var i = 0; i < stages.Length; i++) result[i] = stages[i];
            result[stages.Length] = simulator;
            return result;
        }
    }
}

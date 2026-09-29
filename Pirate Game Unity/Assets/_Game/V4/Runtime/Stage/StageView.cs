using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// What the rest of V4 reads and asks of stage content (console, trace harness, dev tools): boss state and summon,
    /// the chest choice (the trace pilot skips chests), discovery. Commands here are dev/test entry points; players
    /// reach the same logic through the Beacon and the interaction prompt.
    /// </summary>
    public interface IStageContent
    {
        bool BossSummoned { get; }
        bool BossDefeated { get; }
        int PaidChestCount { get; }
        bool TrySummonBoss();
        /// <summary>The POI the interaction prompt is on, or -1.</summary>
        int PromptTarget { get; }
        /// <summary>Skip (R) the chest the prompt is showing.</summary>
        void SkipChest();
        /// <summary>Console: mark the Beacon discovered now.</summary>
        void RevealBeacon();
        StageContentModule Module { get; }
        InteractionModel Interaction { get; }
    }

    /// <summary>The M7 stage view: the V4 stage module and discovery (replaces the M6 view over POIManager and V3 discovery).</summary>
    public sealed class StageView : IStageView, IStageContent
    {
        readonly StageContentModule _stage;
        readonly InteractionModel _interaction;
        RoguePirates.V4.Foundation.World.StageLayout _lastLayout;
        int _layoutVersion;

        public StageView(StageContentModule stage, InteractionModel interaction)
        {
            _stage = stage;
            _interaction = interaction;
        }

        public StageContentModule Module => _stage;
        public InteractionModel Interaction => _interaction;

        // ---- IStageView ----------------------------------------------------------------------------------------
        public bool Exists => _stage.Layout != null;

        public int LayoutVersion
        {
            get
            {
                if (!ReferenceEquals(_stage.Layout, _lastLayout)) { _lastLayout = _stage.Layout; _layoutVersion++; }
                return _layoutVersion;
            }
        }

        public int PoiCount => _stage.Layout?.pois != null ? _stage.Layout.pois.Count : 0;

        public PoiMarker Poi(int index)
        {
            var slot = _stage.Layout.pois[index];
            return new PoiMarker { type = slot.type, position = slot.position, used = slot.used };
        }

        public bool HasDiscovery => _stage.Discovery.HasLayout;
        public BeaconReveal Beacon => _stage.Discovery.Beacon;
        public Vector3? BeaconPosition => _stage.Discovery.BeaconPosition;
        public bool IsPoiVisible(int index) => _stage.Discovery.IsPoiVisible(index);
        public bool IsCellExplored(Vector3 world) => _stage.Discovery.IsCellExplored(world);
        public int ExplorationRevision => _stage.Discovery.ExplorationRevision;
        public float ExploreCellSize => _stage.Discovery.ExploreCellSize;
        public float DiscoveryRadius => _stage.Discovery.DiscoveryRadius;
        public bool BossSummoned => _stage.BossSummoned;
        public bool BossDefeated => _stage.BossDefeated;

        // ---- IStageContent -------------------------------------------------------------------------------------
        public int PaidChestCount => _stage.PaidChestCount;
        public bool TrySummonBoss() => _stage.TrySummonBoss();
        public int PromptTarget => _interaction != null ? _interaction.Target : -1;
        public void SkipChest() => _interaction?.SkipChest();
        public void RevealBeacon() => _stage.Discovery.ForceDiscovered();
    }
}

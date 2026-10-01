using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    // What the stage content module (M7) needs from the run, the ship and the other modules. The run and ship halves are
    // implemented by the ports in Runtime/Ports (RunPort, ShipPort); the rest by V4 modules.

    /// <summary>The run as the legacy run manager keeps it (state, pause, gold, stats).</summary>
    public interface IStageRun
    {
        bool Exists { get; }
        bool IsRunEnded { get; }
        bool IsPaused { get; }
        PauseReason PauseReason { get; }
        RunState State { get; }
        float ElapsedSeconds { get; }
        int Gold { get; }
        int RunSeed { get; }
        float MaxHull { get; }
        float Luck { get; }
        void SetState(RunState state);
        void SetPause(PauseReason reason);
        void AddGold(int amount);
        bool TrySpendGold(int amount);
        void SecureObjective();
        void CompleteRun(bool victory);
        /// <summary>A permanent run modifier on a shared stat (Foundation StatId value).</summary>
        void AddStatModifier(int stat, float add, float multiply);
    }

    /// <summary>M14: the run's loot and map streams (chest rolls, Powder Cache items, Signal Buoy rewards).</summary>
    public interface IStageRandom
    {
        float LootFloat();
        int LootInt(int minInclusive, int maxExclusive);
        int MapInt(int minInclusive, int maxExclusive);
    }

    /// <summary>The player ship for landmarks (position, hull, repair).</summary>
    public interface IStageShip
    {
        bool Exists { get; }
        Vector3 Position { get; }
        float CurrentHp { get; }
        void Heal(float amount);
    }

    public readonly struct ChestPlan
    {
        public readonly bool Valid;
        public readonly int Price;
        public readonly string ItemId;
        public readonly string Error;

        public ChestPlan(bool valid, int price, string itemId, string error)
        {
            Valid = valid;
            Price = price;
            ItemId = itemId;
            Error = error;
        }

        public const string InsufficientGold = "insufficient gold";
        public const string NoEligibleLoot = "no eligible loot";
    }

    /// <summary>Chest prices and loot, items and the map random stream (legacy economy and inventory until M9/M12).</summary>
    public interface IStageEconomy
    {
        /// <summary>The price shown and charged for the next chest.</summary>
        int QuoteChestPrice();
        /// <summary>The price the progressive chest spawns compare run gold against (V3Economy.GetChestPrice).</summary>
        int ProgressChestPrice(int paidChests);
        /// <summary>Price check and loot roll (24 attempts at an eligible item) on the loot stream.</summary>
        ChestPlan PlanChest(IReadOnlyList<string> equippedWeaponIds);
        void RegisterChestPurchase();
        /// <summary>V5 M5: the last planned chest was free (Skeleton Key): not counted as a purchase.</summary>
        bool LastPlanFree => false;
        bool TryAddItem(string itemId);
        string ItemName(string itemId);
        /// <summary>A uniformly drawn chest-pool item (map stream), for the Powder Cache.</summary>
        string RollChestPoolItem();
        /// <summary>A uniformly drawn chest-pool item of at least <paramref name="minRarity"/> (map stream), for the Powder Cache's Rare Treasure Chest.</summary>
        string RollChestPoolItem(int minRarity);
        /// <summary>A map-stream draw in [0, maxExclusive) (Signal Buoy reward).</summary>
        int MapInt(int maxExclusive);
        /// <summary>The equipped weapons' ids (R-47 fix only).</summary>
        IReadOnlyList<string> EquippedWeaponIds { get; }
    }

    /// <summary>Telemetry, the home seed and the chest trigger. M12: Rum listens to LandmarkUsed / BossState events instead.</summary>
    public interface IStageRewards
    {
        void LockHomeSeed(int runSeed);
        void ChestOpened();
        void PoiUsedTelemetry(POIType type);
        void BossSummonedTelemetry(int encounterId);
        void BossDefeatedTelemetry(int encounterId);
    }

    /// <summary>Toasts (the kept toast view until M14).</summary>
    public interface IStageToasts
    {
        void Show(string text, float seconds);
    }

    /// <summary>
    /// The POI markers as the marker presenter draws them (M14.12; the stage module held marker objects through
    /// IPoiMarkers before): which POIs have a marker, its look, and whether it is hidden, used, charging or telegraphing.
    /// </summary>
    public interface IPoiMarkerView
    {
        /// <summary>Changes when a new layout is set (a new run).</summary>
        int LayoutVersion { get; }
        int PoiCount { get; }
        bool UsePlaceholderVisuals { get; }
        POIType PoiType(int index);
        Vector3 PoiPosition(int index);
        bool IsMarkerSpawned(int index);
        POIType VisualType(int index);
        bool IsHidden(int index);
        bool IsConsumed(int index);
        bool ChargeRingShown(int index);
        float SignalBuoyCharge01(int index);
        float ChargeRingRadius { get; }
        bool TelegraphRingShown(int index);
    }
}

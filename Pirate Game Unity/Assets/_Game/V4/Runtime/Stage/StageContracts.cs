using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    // What the stage content module (M7) needs from systems that are still legacy. Each is implemented in
    // Runtime/Legacy/LegacyStageAdapters.cs and replaced by the V4 module named in its adapter's deletion phase.

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
        /// <summary>A permanent run modifier on a shared stat (HighSeas StatId value).</summary>
        void AddStatModifier(int stat, float add, float multiply);
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
    /// The POI marker objects. V3 created them in POIManager.Initialize, and the chest's bobbing draws from Unity's
    /// global random state and moves the chest (interaction range is measured to the marker), so they are created at
    /// the same moment and in the same order as V3, with the per-POI tick on the marker (POIInteractable's place).
    /// Legacy objects until M14 (R-95).
    /// </summary>
    public interface IPoiMarkers
    {
        void Clear();
        /// <summary>Creates POI <paramref name="index"/>'s marker; <paramref name="tick"/> runs in the marker's Update.</summary>
        void Spawn(int index, POIType type, Vector3 position, System.Action<int, float> tick);
        bool IsSpawned(int index);
        /// <summary>The marker's current world position (chests bob), which interaction range is measured to.</summary>
        Vector3 Position(int index);
        void Hide(int index);
        /// <summary>Replaces POI <paramref name="index"/>'s marker with a visible one of <paramref name="visualType"/> (same tick).</summary>
        void Replace(int index, POIType visualType, Vector3 position, System.Action<int, float> tick);
        void Consume(int index);
        void SetChargeRing(int index, float charge01, float radius);
        void ShowTelegraph(int index);
        void PulseTelegraph(int index);
        void HideTelegraph(int index);
    }
}

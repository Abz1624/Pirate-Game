using System;

namespace RoguePirates.Simulation
{
    public enum SimRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum SimPauseReason
    {
        Choice = 1,
        Manual = 2,
        Focus = 4,
        Suspend = 8,
        Connectivity = 16,
        Loading = 32
    }

    public enum SimRunState
    {
        Playing,
        BossFight,
        ObjectiveSecured,
        PlayerDead,
        RunComplete
    }

    public enum SimEventKind
    {
        DamageDealt,
        ActorDied,
        XpGained,
        LevelUpOffer,
        ObjectiveSecured,
        ProjectileSpawned,
        PickupCollected
    }

    public readonly struct SimEvent
    {
        public SimEventKind Kind { get; }
        public int ActorId { get; }
        public int TargetId { get; }
        public SimFixed Value { get; }
        public int Tick { get; }

        public SimEvent(SimEventKind kind, int tick, int actorId = 0, int targetId = 0, SimFixed value = default)
        {
            Kind = kind;
            Tick = tick;
            ActorId = actorId;
            TargetId = targetId;
            Value = value;
        }
    }
}

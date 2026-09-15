using System;
using System.Collections.Generic;
using RoguePirates.Simulation;

namespace RoguePirates.Presentation
{
    /// <summary>Event-driven bridge from deterministic simulation to Unity presentation.</summary>
    public sealed class SimulationBridge
    {
        readonly CombatSimulator _sim;
        readonly List<Action<SimEvent>> _handlers = new();

        public CombatSimulator Simulation => _sim;
        public float Accumulator { get; private set; }

        public SimulationBridge(int seed) => _sim = new CombatSimulator(seed);

        public void Subscribe(Action<SimEvent> handler) => _handlers.Add(handler);

        public void SetMoveInput(float x, float y) => _sim.SetMoveInput(x, y);

        public void Update(float wallDeltaSeconds)
        {
            Accumulator += wallDeltaSeconds;
            while (Accumulator >= SimConstants.TickDurationSeconds)
            {
                Accumulator -= SimConstants.TickDurationSeconds;
                var eventCountBefore = _sim.Events.Count;
                _sim.Step(SimConstants.TickDurationSeconds);
                DispatchNewEvents(eventCountBefore);
            }
        }

        void DispatchNewEvents(int fromIndex)
        {
            for (var i = fromIndex; i < _sim.Events.Count; i++)
            {
                var evt = _sim.Events[i];
                for (var h = 0; h < _handlers.Count; h++)
                    _handlers[h]?.Invoke(evt);
            }
        }
    }
}

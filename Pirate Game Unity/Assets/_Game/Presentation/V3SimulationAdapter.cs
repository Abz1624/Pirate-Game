using System.Collections.Generic;
using HighSeas.Core;
using RoguePirates.Simulation;
using UnityEngine;

namespace RoguePirates.Presentation
{
    /// <summary>Adapts V3 run bootstrap to deterministic simulation clock while preserving legacy presentation.</summary>
    public sealed class V3SimulationAdapter : MonoBehaviour
    {
        [SerializeField] int seed = GameConstants.DemoRunSeed;
        [SerializeField] bool driveSimulation = true;

        SimulationBridge _bridge;

        public SimulationBridge Bridge => _bridge;

        void Awake()
        {
            var runSeed = RunManager.Instance != null ? RunManager.Instance.RunSeed : seed;
            _bridge = new SimulationBridge(runSeed);
            _bridge.Subscribe(OnSimEvent);
        }

        void Update()
        {
            if (!driveSimulation || _bridge == null) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            _bridge.SetMoveInput(1f, 0f);
            _bridge.Update(Time.deltaTime);
        }

        void OnSimEvent(SimEvent evt)
        {
            if (evt.Kind == SimEventKind.ObjectiveSecured && RunManager.Instance != null)
                RunManager.Instance.SecureObjective();
        }
    }
}

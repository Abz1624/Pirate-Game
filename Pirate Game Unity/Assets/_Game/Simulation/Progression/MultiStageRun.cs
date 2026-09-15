using System.Collections.Generic;

namespace RoguePirates.Simulation.Progression
{
    public sealed class MultiStageRun
    {
        public int CurrentStageIndex { get; private set; }
        public bool PortalAvailable { get; private set; }
        public bool FinalStage { get; private set; }
        public int TotalStages { get; }

        public MultiStageRun(int totalStages = 3)
        {
            TotalStages = totalStages;
            FinalStage = totalStages <= 1;
        }

        public void OnBossSecured()
        {
            PortalAvailable = !FinalStage;
        }

        public bool TryEnterPortal()
        {
            if (!PortalAvailable || FinalStage) return false;
            CurrentStageIndex++;
            PortalAvailable = false;
            FinalStage = CurrentStageIndex >= TotalStages - 1;
            return true;
        }
    }
}

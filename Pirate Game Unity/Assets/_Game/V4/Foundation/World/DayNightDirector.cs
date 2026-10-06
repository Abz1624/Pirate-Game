using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    public sealed class DayNightDirector : MonoBehaviour
    {
        [SerializeField] Light sun;
        [SerializeField] float stageDurationSeconds = 600f;

        float _startHour;
        bool _initialized;

        public void BeginRun(int runSeed)
        {
            var rng = new RunRng(runSeed ^ 0x4E19);
            _startHour = rng.DirectorFloat() * 24f;
            _initialized = true;
        }

        void Update()
        {
            if (!_initialized || RunManager.Instance == null) return;
            // Playtest (2 Oct 2026): the arc spans the stage's real length (the timeline's deadline, 15:00 now), not a fixed 10:00.
            var duration = RoguePirates.V4.Foundation.Balance.SaltwindFinaleLogic.ResolveStageDeadlineSeconds(RoguePirates.V4.Foundation.Balance.StageBalanceProfileContext.Export);
            if (duration <= 0f) duration = stageDurationSeconds;
            var t = Mathf.Clamp01(RunManager.Instance.ElapsedSeconds / duration);
            var hour = (_startHour + t * 8f) % 24f;
            var daylight = Mathf.Clamp01(Mathf.Sin((hour / 24f) * Mathf.PI * 2f) * 0.5f + 0.5f);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(15f + (1f - daylight) * 55f, hour * 15f, 0f);
                sun.intensity = Mathf.Lerp(0.35f, 1.1f, daylight);
            }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.12f, 0.14f, 0.22f), new Color(0.55f, 0.58f, 0.62f), daylight);
        }
    }
}

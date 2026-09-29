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
            var t = Mathf.Clamp01(RunManager.Instance.ElapsedSeconds / stageDurationSeconds);
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

using System.Collections.Generic;
using UnityEngine;

namespace TornadoFX
{
    /// <summary>
    /// Minimal pool for Tornado_VFX. Instances are created once in Awake (prewarm) and recycled:
    /// Spawn() -> PlayTornado(); when the tornado finishes its stop animation it returns itself.
    /// </summary>
    public class TornadoVFXPool : MonoBehaviour
    {
        public TornadoVFX prefab;
        [Min(0)] public int prewarmCount = 2;
        [Tooltip("Create extra instances if the pool runs dry (logs a warning so you can raise the prewarm count).")]
        public bool allowExpand = true;

        readonly Stack<TornadoVFX> free = new Stack<TornadoVFX>();
        readonly List<TornadoVFX> all = new List<TornadoVFX>();

        void Awake()
        {
            for (int i = 0; i < prewarmCount; i++) free.Push(Create());
        }

        TornadoVFX Create()
        {
            var t = Instantiate(prefab, transform);
            t.name = $"{prefab.name}_{all.Count}";
            t.playOnEnable = false;
            t.Finished += Return;
            t.gameObject.SetActive(false);
            all.Add(t);
            return t;
        }

        /// <summary>Get a tornado, place it and start it. duration &lt; 0 keeps the prefab value.</summary>
        public TornadoVFX Spawn(Vector3 position, float intensity = 1f, float duration = -1f)
        {
            TornadoVFX t;
            if (free.Count > 0) t = free.Pop();
            else if (allowExpand) { Debug.LogWarning("[TornadoVFXPool] Pool empty, expanding. Raise prewarmCount.", this); t = Create(); }
            else return null;

            t.transform.SetPositionAndRotation(position, Quaternion.identity);
            t.gameObject.SetActive(true);
            if (duration >= 0f) t.duration = duration;
            t.SetIntensity(intensity);
            t.PlayTornado();
            return t;
        }

        /// <summary>Graceful stop (plays the end animation, then auto-returns). Use immediate for hard resets.</summary>
        public void Despawn(TornadoVFX t, bool immediate = false)
        {
            if (t == null) return;
            if (immediate) { t.StopImmediate(); Return(t); }
            else t.StopTornado();
        }

        void Return(TornadoVFX t)
        {
            if (!t.gameObject.activeSelf) return;
            t.gameObject.SetActive(false);
            t.transform.SetParent(transform, false);
            free.Push(t);
        }

        public void DespawnAll(bool immediate = true)
        {
            foreach (var t in all) if (t.gameObject.activeSelf) Despawn(t, immediate);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>Tick-keyed snapshot buffer for remote entity interpolation (~2 ticks behind).</summary>
    public class SnapshotBuffer : MonoBehaviour
    {
        [SerializeField] int maxBuffered = 8;
        [SerializeField] float renderDelayTicks = 2f;
        [SerializeField] int tickHz = 10;

        readonly SortedList<uint, MatchSnapshot> buffer = new();

        public void Push(MatchSnapshot snap)
        {
            if (snap == null) return;
            buffer[snap.tick] = snap;
            while (buffer.Count > maxBuffered)
                buffer.RemoveAt(0);
        }

        public bool TryGetInterpolated(out MatchSnapshot from, out MatchSnapshot to, out float t)
        {
            from = null;
            to = null;
            t = 0f;
            if (buffer.Count == 0) return false;
            if (buffer.Count == 1)
            {
                from = to = buffer.Values[buffer.Count - 1];
                return true;
            }

            var newestTick = buffer.Keys[buffer.Count - 1];
            var renderTick = newestTick - renderDelayTicks;
            if (renderTick < buffer.Keys[0])
            {
                from = to = buffer.Values[0];
                return true;
            }

            uint prevKey = buffer.Keys[0];
            for (var i = 1; i < buffer.Count; i++)
            {
                var key = buffer.Keys[i];
                if (key >= renderTick)
                {
                    from = buffer[prevKey];
                    to = buffer[key];
                    var span = Mathf.Max(1f, key - prevKey);
                    t = Mathf.Clamp01((renderTick - prevKey) / span);
                    return true;
                }
                prevKey = key;
            }

            from = to = buffer.Values[buffer.Count - 1];
            return true;
        }

        public void Clear() => buffer.Clear();
    }
}

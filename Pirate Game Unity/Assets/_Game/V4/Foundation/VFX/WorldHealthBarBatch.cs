using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.VFX
{
    /// <summary>
    /// One world-space canvas for the health bars that share a parent (the pool's root), so they batch into a few draw
    /// calls, and one LateUpdate that keeps each bar over its ship facing the camera and, every few frames, sorts them far
    /// to near. A bar whose ship is gone is destroyed, as before. (Against particles and smoke the bars now sort as one
    /// transparent object: checked in the playable check's screenshots.)
    /// </summary>
    public sealed class WorldHealthBarBatch : MonoBehaviour
    {
        readonly List<WorldHealthBar> _bars = new List<WorldHealthBar>();
        readonly List<(float distance, WorldHealthBar bar)> _order = new List<(float, WorldHealthBar)>();

        /// <summary>How often (frames) the bars are re-sorted far to near, so a near bar draws over a far one as separate canvases did.</summary>
        public const int SortEveryFrames = 8;

        /// <summary>Bars currently placed by this batch (tests).</summary>
        public int Count => _bars.Count;

        internal static void Add(WorldHealthBar bar)
        {
            // The batch the bar used before (kept across pool releases), else the one under its pool root.
            var batch = bar.Batch;
            if (batch == null) batch = bar.transform.parent != null ? bar.transform.parent.GetComponent<WorldHealthBarBatch>() : null;
            if (batch == null) batch = For(bar.transform.parent);
            // V5 M8z (7 Oct, playtest: "not seeing any health bars on top of minibosses, elites, and bosses"). The bar's own
            // canvas is removed, then the bar is parented under the shared canvas. It used to be parented first and its
            // canvas only switched off: the bar's images stayed in the switched-off canvas's draw list (CanvasRenderer
            // absoluteDepth -1), so no enemy, elite, miniboss or boss bar (nor the player's) was drawn since V5 M7c. A
            // reparent is what makes Unity move the images to the shared canvas, so a bar that lost its canvas is
            // reparented even when it already sits under the batch.
            var removed = RemoveOwnCanvas(bar.gameObject);
            if (bar.transform.parent != batch.transform || removed)
            {
                if (bar.transform.parent == batch.transform) bar.transform.SetParent(batch.transform.parent, false);
                bar.transform.SetParent(batch.transform, false); // keeps the prefab's local scale (0.01)
            }
            bar.Batch = batch;
            if (!batch._bars.Contains(bar)) batch._bars.Add(bar);
            batch.Place(bar, Camera.main);
        }

        /// <summary>Removes the bar's own canvas (raycaster and scaler first: they require it). True when there was one.</summary>
        static bool RemoveOwnCanvas(GameObject bar)
        {
            if (!bar.TryGetComponent<Canvas>(out var own)) return false;
            if (bar.TryGetComponent<GraphicRaycaster>(out var raycaster)) DestroyImmediate(raycaster);
            if (bar.TryGetComponent<CanvasScaler>(out var scaler)) DestroyImmediate(scaler);
            DestroyImmediate(own);
            return true;
        }

        /// <summary>V5 M8z (7 Oct, playtest): true when the bar is drawn by a canvas (its images are in a canvas's draw
        /// list, not left in a switched-off one), active, at a non-zero scale and in front of <paramref name="cam"/> (tests).</summary>
        public static bool IsDrawn(WorldHealthBar bar, Camera cam, out string why)
        {
            why = null;
            if (bar == null || !bar.isActiveAndEnabled) { why = "inactive"; return false; }
            var scale = bar.transform.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-5f || Mathf.Abs(scale.y) < 1e-5f) { why = "zero scale"; return false; }
            if (bar.TryGetComponent<Canvas>(out var own) && !own.enabled) { why = "under its own switched-off canvas"; return false; }
            var images = bar.GetComponentsInChildren<Image>(false);
            if (images.Length == 0) { why = "no images"; return false; }
            foreach (var image in images)
            {
                if (!image.enabled || image.canvas == null || !image.canvas.isActiveAndEnabled) { why = image.name + " has no live canvas"; return false; }
                if (image.canvasRenderer.absoluteDepth < 0) { why = image.name + " is not in a canvas's draw list"; return false; }
                if (image.canvasRenderer.GetAlpha() <= 0f || image.color.a <= 0f) { why = image.name + " is transparent"; return false; }
            }
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(bar.transform.position);
                if (vp.z <= cam.nearClipPlane || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) { why = $"off screen (viewport {vp})"; return false; }
                if ((cam.cullingMask & (1 << bar.gameObject.layer)) == 0) { why = "on a layer the camera does not draw"; return false; }
            }
            return true;
        }

        internal static void Remove(WorldHealthBar bar)
        {
            if (bar.Batch != null) bar.Batch._bars.Remove(bar); // the reference is kept for the next Bind
        }

        /// <summary>The shared canvas under <paramref name="parent"/> (created on first use; the scene root when null).</summary>
        public static WorldHealthBarBatch For(Transform parent)
        {
            if (parent != null)
                for (var i = 0; i < parent.childCount; i++)
                    if (parent.GetChild(i).TryGetComponent<WorldHealthBarBatch>(out var existing)) return existing;
            var go = new GameObject("SharedHealthBars", typeof(RectTransform), typeof(Canvas), typeof(WorldHealthBarBatch));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.position = Vector3.zero;
            rt.rotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.sizeDelta = Vector2.zero;
            return go.GetComponent<WorldHealthBarBatch>();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            for (var i = _bars.Count - 1; i >= 0; i--)
            {
                var bar = _bars[i];
                if (bar == null || !bar.isActiveAndEnabled) { _bars.RemoveAt(i); continue; }
                if (bar.Target == null)
                {
                    _bars.RemoveAt(i);
                    Destroy(bar.gameObject);
                    continue;
                }
                Place(bar, cam);
            }
            if (cam != null && Time.frameCount % SortEveryFrames == 0) SortFarToNear(cam.transform.position);
        }

        /// <summary>Draw order inside the shared canvas is sibling order: far bars first, near bars last (on top).</summary>
        public void SortFarToNear(Vector3 cameraPosition)
        {
            _order.Clear();
            foreach (var bar in _bars) _order.Add(((bar.transform.position - cameraPosition).sqrMagnitude, bar));
            _order.Sort((a, b) => b.distance.CompareTo(a.distance));
            for (var i = 0; i < _order.Count; i++)
                if (_order[i].bar.transform.GetSiblingIndex() != i) _order[i].bar.transform.SetSiblingIndex(i);
        }

        void Place(WorldHealthBar bar, Camera cam)
        {
            if (bar.Target == null) return;
            var t = bar.transform;
            var pos = bar.Target.position + Vector3.up * bar.HeightOffset;
            if (cam != null) t.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - cam.transform.position));
            else t.position = pos;
        }
    }
}

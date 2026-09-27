using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Pickups;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// V4 pickups drawn with V3's PickupViewPool, exactly as V3's PickupWorld drew them: uniform crystal scale, the
    /// flying coin/gem scale when homing, the spin, and barrels floated to their submerged depth from the model's
    /// renderer bounds (register R-74).
    /// </summary>
    [DeletedInPhase(14, "Pickup presenter on the pool service")]
    public sealed class LegacyPickupViews : IPickupViews
    {
        readonly PickupViewPool _pool;
        readonly DemoJuiceSettings _juice;

        public LegacyPickupViews(Transform root, DemoJuiceSettings juice)
        {
            _juice = juice != null ? juice : Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _pool = new PickupViewPool(root, _juice);
        }

        public object Acquire(PickupType type, Vector3 position, int value)
        {
            var view = _pool.Acquire(type, position, value);
            UniformScale(type, view);
            return view;
        }

        void UniformScale(PickupType type, Transform view)
        {
            if (view != null && type is PickupType.Infamy or PickupType.FlyingXp)
                view.localScale = Vector3.one * _pool.GetXpCrystalVisualScale();
        }

        public Vector3 AlignBarrel(object view)
        {
            var t = (Transform)view;
            var submerged = _juice != null ? _juice.barrelSubmergedFraction : 0.15f;
            var waterY = _juice != null ? _juice.pickupWaterSurfaceY : 0f;
            if (!TryGetBounds(t, out var bounds)) return t.position;
            var aligned = t.position + Vector3.up * (waterY - submerged * bounds.size.y - bounds.min.y);
            t.position = aligned;
            return aligned;
        }

        public void BeginHoming(object view, PickupType type)
        {
            var t = (Transform)view;
            if ((type == PickupType.Gold || type == PickupType.FlyingGold || type == PickupType.FlyingXp) && _juice != null)
                t.localScale = Vector3.one * (type == PickupType.FlyingXp ? _pool.GetXpFlyingGemScale() : _juice.goldFlyingCoinScale);
            else if (type == PickupType.Infamy)
                UniformScale(type, t);
        }

        public void Update(object view, PickupType type, Vector3 position, bool homing, float dt)
        {
            var t = (Transform)view;
            t.position = position;
            UniformScale(type, t);
            if (!homing && type != PickupType.GoldBarrel && type != PickupType.XpBarrel && type != PickupType.Powerup)
                t.Rotate(0f, 180f * dt, 0f, Space.World);
        }

        public void Release(object view, PickupType type, int value) => _pool.Release((Transform)view, type, value);

        // V3 PickupWorld.TryGetBarrelVisualBounds: mesh renderers (non-empty), else skinned renderers, else mesh filters.
        static bool TryGetBounds(Transform view, out Bounds bounds)
        {
            bounds = default;
            var has = false;
            var root = view.Find("Visuals");
            if (root == null) root = view;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                if (b.size.sqrMagnitude < 0.0001f) continue;
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (has) return true;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var b = MeshBounds(f.transform, f.sharedMesh.bounds);
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            return has;
        }

        static Bounds MeshBounds(Transform t, Bounds local)
        {
            var e = local.extents;
            var x = t.TransformVector(e.x, 0f, 0f);
            var y = t.TransformVector(0f, e.y, 0f);
            var z = t.TransformVector(0f, 0f, e.z);
            var world = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(t.TransformPoint(local.center), world * 2f);
        }
    }
}

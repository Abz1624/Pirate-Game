using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>Runtime playable water bounds derived from spawned island content.</summary>
    public static class PlayBounds
    {
        public const float DefaultHalfExtent = 110f;
        public const float StormEdgeMargin = 8f;
        public const float EdgeDamagePerSecond = 18f;

        static Vector3 _center;
        static float _halfExtentX = DefaultHalfExtent;
        static float _halfExtentZ = DefaultHalfExtent;
        static bool _configured;

        public static Vector3 Center => _center;
        public static float HalfExtentX => _halfExtentX;
        public static float HalfExtentZ => _halfExtentZ;
        public static float HalfExtent => Mathf.Max(_halfExtentX, _halfExtentZ);
        public static bool IsConfigured => _configured;

        static float _stormSafeScale = 1f;
        static bool _useProfileStormSafe;

        public static float StormSafeScale => _stormSafeScale;
        public static bool UsesProfileStormSafe => _useProfileStormSafe;

        public static void ResetStormSafeRegion()
        {
            _stormSafeScale = 1f;
            _useProfileStormSafe = false;
        }

        public static void SetProfileStormSafeScale(float safeScale)
        {
            _useProfileStormSafe = true;
            _stormSafeScale = Mathf.Clamp(safeScale, 0.05f, 1f);
        }

        public static void ResetToDefaults()
        {
            _center = Vector3.zero;
            _halfExtentX = DefaultHalfExtent;
            _halfExtentZ = DefaultHalfExtent;
            _configured = false;
            ResetStormSafeRegion();
        }

        /// <summary>Fit playable water around spawned island exclusion zones plus open-water padding.</summary>
        public static void ConfigureFromExclusionRegistry(float waterPadding = 22f, float minHalfExtent = 72f)
        {
            if (!IslandSpawnExclusionRegistry.TryGetExtents(out var minX, out var maxX, out var minZ, out var maxZ))
            {
                ResetToDefaults();
                return;
            }

            _center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            _halfExtentX = Mathf.Max(minHalfExtent, (maxX - minX) * 0.5f + waterPadding);
            _halfExtentZ = Mathf.Max(minHalfExtent, (maxZ - minZ) * 0.5f + waterPadding);
            _configured = true;
        }

        public static bool IsInside(Vector3 position, float margin = 0f)
        {
            var local = position - _center;
            return Mathf.Abs(local.x) <= _halfExtentX - margin &&
                   Mathf.Abs(local.z) <= _halfExtentZ - margin;
        }

        public static bool IsInsideStormSafeZone(Vector3 position)
        {
            if (!_useProfileStormSafe)
                return IsInside(position, StormEdgeMargin);

            var local = position - _center;
            var hx = (_halfExtentX - StormEdgeMargin) * _stormSafeScale;
            var hz = (_halfExtentZ - StormEdgeMargin) * _stormSafeScale;
            return Mathf.Abs(local.x) <= hx && Mathf.Abs(local.z) <= hz;
        }

        public static float GetStormDepth(Vector3 position)
        {
            var local = position - _center;
            if (_useProfileStormSafe)
            {
                var hx = (_halfExtentX - StormEdgeMargin) * _stormSafeScale;
                var hz = (_halfExtentZ - StormEdgeMargin) * _stormSafeScale;
                return Mathf.Max(Mathf.Abs(local.x) - hx, Mathf.Abs(local.z) - hz);
            }

            var limitX = _halfExtentX - StormEdgeMargin;
            var limitZ = _halfExtentZ - StormEdgeMargin;
            return Mathf.Max(Mathf.Abs(local.x) - limitX, Mathf.Abs(local.z) - limitZ);
        }

        public static Vector3 Clamp(Vector3 position, float margin = 1.5f)
        {
            var local = position - _center;
            local.x = Mathf.Clamp(local.x, -(_halfExtentX - margin), _halfExtentX - margin);
            local.z = Mathf.Clamp(local.z, -(_halfExtentZ - margin), _halfExtentZ - margin);
            position.x = _center.x + local.x;
            position.z = _center.z + local.z;
            return position;
        }

        public static bool IsOpenWater(Vector3 position, float clearance = 3f)
        {
            if (!IsInside(position, clearance))
                return false;

            if (IslandSpawnExclusionRegistry.Count > 0 &&
                !IslandSpawnExclusionRegistry.IsClear(position, clearance))
                return false;

            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, clearance))
                return false;

            return true;
        }

        public static void GetStormRectCorners(float margin, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
            var y = 0.12f;
            var hx = _halfExtentX - margin;
            var hz = _halfExtentZ - margin;
            if (_useProfileStormSafe)
            {
                hx *= _stormSafeScale;
                hz *= _stormSafeScale;
            }

            a = new Vector3(_center.x - hx, y, _center.z - hz);
            b = new Vector3(_center.x + hx, y, _center.z - hz);
            c = new Vector3(_center.x + hx, y, _center.z + hz);
            d = new Vector3(_center.x - hx, y, _center.z + hz);
        }
    }
}

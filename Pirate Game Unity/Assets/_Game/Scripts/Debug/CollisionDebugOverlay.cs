using System.Collections.Generic;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HighSeas.Debugging
{
    /// <summary>
    /// Press C during a run to toggle wireframe overlays for island, player, and enemy collision volumes.
    /// </summary>
    public sealed class CollisionDebugOverlay : MonoBehaviour
    {
        const int CircleSegments = 24;

        static readonly Color IslandGroundColor = new(0.25f, 0.95f, 0.4f, 0.9f);
        static readonly Color IslandFoliageColor = new(0.55f, 0.85f, 0.2f, 0.85f);
        static readonly Color PlayerColor = new(0.25f, 0.8f, 1f, 0.95f);
        static readonly Color EnemyColor = new(1f, 0.45f, 0.2f, 0.9f);
        static readonly Color BossColor = new(1f, 0.25f, 0.95f, 0.95f);

        readonly List<MeshCollider> _islandMeshColliders = new(32);

        Material _lineMaterial;
        bool _visible;
        bool _islandsCached;

        public static CollisionDebugOverlay Ensure()
        {
            var existing = FindFirstObjectByType<CollisionDebugOverlay>();
            if (existing != null)
                return existing;

            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("CollisionDebugOverlay");
            return host.GetComponent<CollisionDebugOverlay>() ?? host.AddComponent<CollisionDebugOverlay>();
        }

        void Awake()
        {
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.cKey.wasPressedThisFrame)
                return;

            _visible = !_visible;
            if (_visible && !_islandsCached)
                CacheIslandColliders();

            var message = _visible ? "Collision debug ON (C)" : "Collision debug OFF (C)";
            V3ToastHud.Instance?.Show(message, 2f);
            Debug.Log(message);
        }

        void CacheIslandColliders()
        {
            _islandMeshColliders.Clear();
            var islandLayer = IslandObstacles.Layer;
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;
                if (islandLayer >= 0 && collider.gameObject.layer != islandLayer)
                    continue;
                _islandMeshColliders.Add(collider);
            }

            _islandsCached = true;
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_visible || _lineMaterial == null || camera == null)
                return;
            if (camera.cameraType != CameraType.Game)
                return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;

            GL.Begin(GL.LINES);
            DrawIslands();
            DrawPlayer();
            DrawEnemies();
            GL.End();

            GL.PopMatrix();
        }

        void DrawIslands()
        {
            for (var i = 0; i < _islandMeshColliders.Count; i++)
            {
                var collider = _islandMeshColliders[i];
                if (collider == null || !collider.enabled)
                    continue;
                var color = collider.convex ? IslandGroundColor : IslandFoliageColor;
                DrawBoundsBox(collider.bounds, color);
            }
        }

        static void DrawBoundsBox(Bounds bounds, Color color)
        {
            GL.Color(color);
            var min = bounds.min;
            var max = bounds.max;

            var a = new Vector3(min.x, min.y, min.z);
            var b = new Vector3(max.x, min.y, min.z);
            var c = new Vector3(max.x, min.y, max.z);
            var d = new Vector3(min.x, min.y, max.z);
            var e = new Vector3(min.x, max.y, min.z);
            var f = new Vector3(max.x, max.y, min.z);
            var g = new Vector3(max.x, max.y, max.z);
            var h = new Vector3(min.x, max.y, max.z);

            DrawLine(a, b); DrawLine(b, c); DrawLine(c, d); DrawLine(d, a);
            DrawLine(e, f); DrawLine(f, g); DrawLine(g, h); DrawLine(h, e);
            DrawLine(a, e); DrawLine(b, f); DrawLine(c, g); DrawLine(d, h);
        }

        static void DrawLine(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        void DrawPlayer()
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null)
                return;

            var meshCollider = player.GetComponentInChildren<MeshCollider>(true);
            if (meshCollider != null && meshCollider.enabled)
            {
                var bounds = meshCollider.bounds;
                DrawBoundsBox(bounds, PlayerColor);
                return;
            }

            var radius = player.IslandCollisionRadius;
            var height = 4.5f;
            DrawVerticalCapsule(player.Position, radius, height, PlayerColor);
        }

        void DrawEnemies()
        {
            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (enemyWorld == null)
                return;

            var agents = enemyWorld.Agents;
            if (agents == null)
                return;

            for (var i = 0; i < agents.Length; i++)
            {
                ref readonly var agent = ref agents[i];
                if (!agent.active)
                    continue;

                var radius = enemyWorld.GetCollisionRadiusForAgent(agent);
                var height = IslandObstacles.GetCollisionHeight(radius);
                var color = agent.isBoss ? BossColor : EnemyColor;
                DrawVerticalCapsule(agent.position, radius, height, color);
            }
        }

        static void DrawCapsuleCollider(CapsuleCollider collider, Color color)
        {
            GetWorldCapsule(collider, out var center, out var axis, out var radius, out var halfCylinder);
            var top = center + axis * halfCylinder;
            var bottom = center - axis * halfCylinder;
            DrawCapsule(top, bottom, axis, radius, color);
        }

        static void DrawVerticalCapsule(Vector3 position, float radius, float height, Color color)
        {
            var halfHeight = Mathf.Max(radius, height * 0.5f - radius);
            var top = position + Vector3.up * halfHeight;
            var bottom = position - Vector3.up * halfHeight;
            DrawCapsule(top, bottom, Vector3.up, radius, color);
        }

        static void DrawCapsule(Vector3 top, Vector3 bottom, Vector3 axis, float radius, Color color)
        {
            GL.Color(color);
            DrawCircle(top, axis, radius);
            DrawCircle(bottom, axis, radius);

            var tangent = Vector3.Cross(axis, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(axis, Vector3.right);
            tangent.Normalize();
            var bitangent = Vector3.Cross(axis, tangent);

            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * 0.5f;
                var offset = tangent * (Mathf.Cos(angle) * radius) + bitangent * (Mathf.Sin(angle) * radius);
                GL.Vertex(top + offset);
                GL.Vertex(bottom + offset);
            }
        }

        static void DrawCircle(Vector3 center, Vector3 normal, float radius)
        {
            var tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            var bitangent = Vector3.Cross(normal, tangent);

            var prev = center + tangent * radius;
            for (var i = 1; i <= CircleSegments; i++)
            {
                var angle = i / (float)CircleSegments * Mathf.PI * 2f;
                var next = center + tangent * (Mathf.Cos(angle) * radius) + bitangent * (Mathf.Sin(angle) * radius);
                GL.Vertex(prev);
                GL.Vertex(next);
                prev = next;
            }
        }

        static void GetWorldCapsule(
            CapsuleCollider collider,
            out Vector3 center,
            out Vector3 axis,
            out float radius,
            out float halfCylinder)
        {
            var transform = collider.transform;
            center = transform.TransformPoint(collider.center);

            var lossyScale = transform.lossyScale;
            var direction = collider.direction;
            var height = collider.height * Mathf.Abs(direction == 0 ? lossyScale.x : direction == 1 ? lossyScale.y : lossyScale.z);
            var radiusScale = direction switch
            {
                0 => Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)),
                1 => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)),
                _ => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y))
            };

            radius = collider.radius * radiusScale;
            halfCylinder = Mathf.Max(0f, height * 0.5f - radius);

            var localAxis = direction switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };
            axis = transform.TransformDirection(localAxis).normalized;
        }
    }
}

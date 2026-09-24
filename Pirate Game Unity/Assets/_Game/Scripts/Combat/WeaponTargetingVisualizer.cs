using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Playtest;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HighSeas.Combat
{
    /// <summary>Per-weapon filled ground overlays for aim debug (replaces line-only debug).</summary>
    public sealed class WeaponTargetingVisualizer : MonoBehaviour
    {
        public static WeaponTargetingVisualizer Instance { get; private set; }

        const int MaxWeaponSlots = 6;
        const float GroundHeight = 0.42f;
        const float TargetDiscRadius = 0.85f;

        readonly WeaponVisualSet[] _slots = new WeaponVisualSet[MaxWeaponSlots];
        MaterialPropertyBlock _mpb;
        bool _initialized;
        float _firePulseTimer;
        float _firePulseDuration = 0.18f;
        string _lastFiredWeaponId;
        WeaponController _weapons;
        PlayerShipController _player;
        EnemyWorld _enemies;
        Material _areaMaterial;

        readonly Vector3[] _mortarPosScratch = new Vector3[4];
        readonly float[] _mortarRadiusScratch = new float[4];
        readonly float[] _mortarTimeScratch = new float[4];

        struct WeaponVisualSet
        {
            public MeshFilter area;
            public MeshRenderer areaRenderer;
            public MeshFilter marker;
            public MeshRenderer markerRenderer;
        }

        public static WeaponTargetingVisualizer Ensure()
        {
            if (Instance != null && Instance._initialized) return Instance;

            var systems = GameObject.Find("Systems");
            var host = systems != null ? systems : new GameObject("WeaponTargetingVisualizer");
            var visualizer = host.GetComponent<WeaponTargetingVisualizer>();
            if (visualizer == null)
                visualizer = host.AddComponent<WeaponTargetingVisualizer>();
            visualizer.EnsureInitialized();
            return visualizer;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (_initialized) return;

            Instance = this;
            _mpb = new MaterialPropertyBlock();
            _areaMaterial = CreateAreaMaterial();

            for (var i = 0; i < MaxWeaponSlots; i++)
            {
                var areaGo = new GameObject($"WeaponArea{i}");
                areaGo.transform.SetParent(transform, false);
                var areaFilter = areaGo.AddComponent<MeshFilter>();
                areaFilter.sharedMesh = new Mesh { name = $"WeaponAreaMesh{i}" };
                var areaRenderer = areaGo.AddComponent<MeshRenderer>();
                areaRenderer.sharedMaterial = _areaMaterial;
                areaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                areaRenderer.receiveShadows = false;

                var markerGo = new GameObject($"WeaponTarget{i}");
                markerGo.transform.SetParent(transform, false);
                var markerFilter = markerGo.AddComponent<MeshFilter>();
                markerFilter.sharedMesh = new Mesh { name = $"WeaponTargetMesh{i}" };
                var markerRenderer = markerGo.AddComponent<MeshRenderer>();
                markerRenderer.sharedMaterial = _areaMaterial;
                markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;

                _slots[i] = new WeaponVisualSet
                {
                    area = areaFilter,
                    areaRenderer = areaRenderer,
                    marker = markerFilter,
                    markerRenderer = markerRenderer
                };
            }

            _initialized = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_areaMaterial != null)
                Destroy(_areaMaterial);
        }

        void Update()
        {
            if (!WasDebugOverlayTogglePressed()) return;
            if (RunManager.Instance == null || RunManager.Instance.IsPaused || RunManager.Instance.IsRunEnded)
                return;

            ToggleDebugOverlays();
        }

        public static void ToggleAimVisuals() => ToggleDebugOverlays();

        public static void ToggleDebugOverlays()
        {
            Ensure();
            var enabled = CombatDebugOverlays.Toggle();
            if (Instance != null && !enabled)
                Instance.SetAllVisible(false);
            V3ToastHud.Instance?.Show(
                enabled ? "Combat debug overlays ON (O)" : "Combat debug overlays OFF (O)",
                2.5f);
        }

        static bool WasDebugOverlayTogglePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.oKey.wasPressedThisFrame || keyboard.vKey.wasPressedThisFrame)
                return true;
            if (WeaponEnemyPlaytestSession.Active)
                return false;
            return keyboard.backslashKey.wasPressedThisFrame;
        }

        void LateUpdate()
        {
            if (!_initialized) return;

            if (!CombatDebugOverlays.Visible
                || RunManager.Instance == null
                || RunManager.Instance.IsPaused)
            {
                SetAllVisible(false);
                return;
            }

            EnsureRefs();
            if (_player == null || _weapons == null)
            {
                SetAllVisible(false);
                return;
            }

            _firePulseTimer = Mathf.Max(0f, _firePulseTimer - Time.deltaTime);
            for (var i = 0; i < MaxWeaponSlots; i++)
            {
                if (i < _weapons.Weapons.Count)
                    DrawWeaponSlot(i, _weapons.Weapons[i]);
                else
                    HideSlot(i);
            }
        }

        public void NotifyWeaponFired(WeaponDefinition def, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget)
        {
            _firePulseDuration = def != null && def.id == "lightning_mast" ? 0.55f : 0.18f;
            _firePulseTimer = _firePulseDuration;
            _lastFiredWeaponId = def != null ? def.id : null;
        }

        void DrawWeaponSlot(int slotIndex, WeaponInstance weapon)
        {
            if (weapon?.def == null)
            {
                HideSlot(slotIndex);
                return;
            }

            var def = weapon.def;
            var stats = RunManager.Instance?.Stats;
            var sizeMult = weapon.sizeMult * (stats != null ? stats.Size : 1f);
            var effectiveRange = def.range * sizeMult;
            var pulse = _firePulseTimer > 0f && def.id == _lastFiredWeaponId;
            var color = pulse ? WeaponDebugPalette.GetLocked(def.id) : WeaponDebugPalette.GetIdle(def.id);
            if (pulse && def.id == "lightning_mast")
                color.a *= Mathf.Clamp01(_firePulseTimer / _firePulseDuration);
            var origin = _player.Position;
            origin.y = GroundHeight;
            var forward = _player.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = _player.transform.forward;
            forward.Normalize();

            var set = _slots[slotIndex];
            var hasTarget = WeaponTargeting.TryResolveTarget(_enemies, _player, def, out var targetIndex, out var aimDirection);
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = forward;
            else aimDirection.Normalize();

            var targetPos = hasTarget ? _enemies.Agents[targetIndex].position : origin + forward * 4f;
            targetPos.y = GroundHeight;

            var showArea = true;
            var showMarker = false;
            var useDirectional = WeaponTargeting.UsesDirectionalGeometry(def)
                                 || def.targeting is TargetingMode.ForwardArc or TargetingMode.ForwardCone or TargetingMode.TetherTarget;

            if (def.pattern == WeaponPattern.PredictedAoE || def.id == "mortar")
            {
                var point = WeaponTargeting.PredictClusterGround(_enemies, _player, def.range);
                var radius = ResolveAoERadius(def, weapon, sizeMult);
                BuildDiscMesh(set.area.sharedMesh, point, radius, 32);
                targetPos = point;
                showMarker = true;
                if (_weapons != null)
                {
                    var pending = _weapons.CopyMortarTelegraphs(_mortarPosScratch, _mortarRadiusScratch, _mortarTimeScratch, _mortarPosScratch.Length);
                    for (var pi = 0; pi < pending; pi++)
                    {
                        if (Vector3.Distance(_mortarPosScratch[pi], point) > 0.35f) continue;
                        var pulseAlpha = 0.35f + 0.65f * Mathf.PingPong(_mortarTimeScratch[pi] * 2.4f, 1f);
                        color.a *= pulseAlpha;
                        break;
                    }
                }
            }
            else if (def.id == "swivel_guns")
            {
                var aim = hasTarget ? aimDirection : forward;
                BuildFanMesh(set.area.sharedMesh, origin, aim, effectiveRange, hasTarget ? 22f : 360f);
                showMarker = hasTarget;
            }
            else if (def.id == "long_nine")
            {
                if (!useDirectional)
                    showArea = false;
                else
                {
                    BuildFanMesh(set.area.sharedMesh, origin, forward, effectiveRange, WeaponTargeting.ResolveArcDegrees(def));
                    showMarker = hasTarget;
                }
            }
            else if (def.id == "lightning_mast" || def.pattern == WeaponPattern.ChainLightning)
            {
                if (hasTarget)
                {
                    var strikeRadius = (3.5f + weapon.quantityBonus * 0.5f) * weapon.sizeMult;
                    BuildDiscMesh(set.area.sharedMesh, targetPos, strikeRadius, 28);
                    showMarker = true;
                }
                else
                {
                    showArea = false;
                }
            }
            else switch (def.targeting)
            {
                case TargetingMode.ForwardArc:
                case TargetingMode.TetherTarget:
                case TargetingMode.ForwardCone:
                    if (!useDirectional)
                        showArea = false;
                    else
                    {
                        BuildFanMesh(
                            set.area.sharedMesh,
                            origin,
                            forward,
                            effectiveRange,
                            WeaponTargeting.ResolveArcDegrees(def));
                        showMarker = hasTarget;
                    }
                    break;
                case TargetingMode.PortStarboard:
                    if (!useDirectional)
                        showArea = false;
                    else
                    {
                        var right = Vector3.Cross(Vector3.up, forward).normalized;
                        var sideDir = right;
                        if (hasTarget)
                        {
                            var toTarget = targetPos - origin;
                            toTarget.y = 0f;
                            if (toTarget.sqrMagnitude > 0.01f && Vector3.Dot(toTarget.normalized, right) < 0f)
                                sideDir = -right;
                        }

                        BuildFanMesh(set.area.sharedMesh, origin, sideDir, effectiveRange, 18f);
                        showMarker = hasTarget;
                    }
                    break;
                case TargetingMode.AftDeploy:
                case TargetingMode.PlayerTrail when def.pattern == WeaponPattern.MineDrop:
                    if (!useDirectional)
                        showArea = false;
                    else
                        BuildFanMesh(set.area.sharedMesh, origin, -forward, 2.8f * sizeMult, 110f);
                    break;
                default:
                    if (def.pattern == WeaponPattern.PredictedAoE)
                    {
                        var point = WeaponTargeting.PredictClusterGround(_enemies, _player, effectiveRange);
                        BuildDiscMesh(set.area.sharedMesh, point, def.size * sizeMult, 24);
                    }
                    else if (def.pattern == WeaponPattern.Orbit)
                    {
                        BuildRingMesh(set.area.sharedMesh, origin, 5f * sizeMult, 32, 0.55f);
                        if (def.id == "flying_cutlasses")
                            color = new Color(1f, 0.88f, 0.28f, color.a);
                    }
                    else if (def.pattern == WeaponPattern.AutoProjectile || def.targeting == TargetingMode.Nearest)
                    {
                        if (hasTarget)
                        {
                            BuildFanMesh(set.area.sharedMesh, origin, aimDirection, effectiveRange, 18f);
                            showMarker = true;
                        }
                        else
                        {
                            BuildDiscMesh(set.area.sharedMesh, origin, effectiveRange, 36);
                        }
                    }
                    else if (def.pattern == WeaponPattern.FanVolley)
                    {
                        if (!useDirectional)
                            showArea = false;
                        else
                            BuildFanMesh(set.area.sharedMesh, origin, forward, effectiveRange, def.fanSpreadDegrees);
                    }
                    else
                    {
                        showArea = false;
                    }
                    break;
            }

            SetRendererColor(set.areaRenderer, color, showArea);
            if (set.area != null)
                set.area.gameObject.SetActive(showArea);

            var showMarkerFinal = showMarker && def.pattern != WeaponPattern.Orbit;
            if (showMarkerFinal && set.marker != null)
            {
                BuildDiscMesh(set.marker.sharedMesh, targetPos, TargetDiscRadius, 16);
                var markerColor = WeaponDebugPalette.GetLocked(def.id);
                markerColor.a = 0.75f;
                SetRendererColor(set.markerRenderer, markerColor, true);
                set.marker.gameObject.SetActive(true);
            }
            else if (set.marker != null)
            {
                set.marker.gameObject.SetActive(false);
            }
        }

        void HideSlot(int slotIndex)
        {
            if (!_initialized || slotIndex < 0 || slotIndex >= MaxWeaponSlots) return;
            if (_slots[slotIndex].area != null)
                _slots[slotIndex].area.gameObject.SetActive(false);
            if (_slots[slotIndex].marker != null)
                _slots[slotIndex].marker.gameObject.SetActive(false);
        }

        void EnsureRefs()
        {
            if (_weapons == null) _weapons = FindFirstObjectByType<WeaponController>();
            if (_player == null) _player = FindFirstObjectByType<PlayerShipController>();
            if (_enemies == null) _enemies = FindFirstObjectByType<EnemyWorld>();
        }

        void SetRendererColor(MeshRenderer renderer, Color color, bool visible)
        {
            if (renderer == null || _mpb == null) return;
            renderer.enabled = visible;
            _mpb.SetColor("_BaseColor", color);
            _mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(_mpb);
        }

        static float ResolveAoERadius(WeaponDefinition def, WeaponInstance weapon, float sizeMult)
        {
            var scaled = def.size * sizeMult;
            if (def.id == "mortar")
                return Mathf.Max(4f, scaled);
            return Mathf.Max(2f, scaled);
        }

        static void BuildFanMesh(Mesh mesh, Vector3 origin, Vector3 forward, float range, float arcDegrees)
        {
            if (mesh == null) return;
            mesh.Clear();
            const int segments = 24;
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = origin;
            var half = arcDegrees * 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(-half, half, t);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var point = origin + dir.normalized * range;
                point.y = GroundHeight;
                verts[i + 1] = point;
            }

            var tri = 0;
            for (var i = 0; i < segments; i++)
            {
                tris[tri++] = 0;
                tris[tri++] = i + 1;
                tris[tri++] = i + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void BuildDiscMesh(Mesh mesh, Vector3 center, float radius, int segments)
        {
            if (mesh == null) return;
            mesh.Clear();
            center.y = GroundHeight;
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = center;
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }

            var tri = 0;
            for (var i = 0; i < segments; i++)
            {
                tris[tri++] = 0;
                tris[tri++] = i + 1;
                tris[tri++] = i + 1 == segments ? 1 : i + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void BuildRingMesh(Mesh mesh, Vector3 center, float radius, int segments, float thickness)
        {
            if (mesh == null) return;
            mesh.Clear();
            center.y = GroundHeight;
            var inner = Mathf.Max(0.1f, radius - thickness);
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                verts[i * 2] = center + dir * inner;
                verts[i * 2 + 1] = center + dir * radius;
            }

            var tri = 0;
            for (var i = 0; i < segments; i++)
            {
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = ((i + 1) % segments) * 2;
                var i3 = ((i + 1) % segments) * 2 + 1;
                tris[tri++] = i0;
                tris[tri++] = i1;
                tris[tri++] = i3;
                tris[tri++] = i0;
                tris[tri++] = i3;
                tris[tri++] = i2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static Material CreateAreaMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;
            return mat;
        }

        void SetAllVisible(bool visible)
        {
            if (!_initialized) return;
            for (var i = 0; i < MaxWeaponSlots; i++)
                if (!visible) HideSlot(i);
        }
    }
}

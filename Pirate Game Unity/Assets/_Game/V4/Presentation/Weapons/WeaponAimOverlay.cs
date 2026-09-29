using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Config;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using Pattern = RoguePirates.V4.Config.ConfigEnums.WeaponPattern;
using Targeting = RoguePirates.V4.Config.ConfigEnums.TargetingMode;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// The combat debug aim overlay (M6): one filled ground shape and one target disc per weapon slot, shown while the
    /// combat debug overlays are on. Port of V3's WeaponTargetingVisualizer: same shapes, colours (each weapon config's aim colour, M16),
    /// ground height and fire pulse (<see cref="NotifyAimed"/>, forwarded by <see cref="WeaponVfxPresenter.Aimed"/>).
    /// <para>What V4 changes: it reads the V4 weapon slots and configs, the V4 targeting service and enemy query and the
    /// ship view instead of finding WeaponController, EnemyWorld and PlayerShipController; the key toggle is injected
    /// (<see cref="TogglePressed"/>) because this assembly has no input reference. V3 dimmed the mortar area in a pulse
    /// while a shell was pending, reading WeaponController's strike list; in V4 that list is empty (shells are the V4
    /// manager's), so the pulse already did not show in M5 and is left out here.</para>
    /// </summary>
    public sealed class WeaponAimOverlay : MonoBehaviour
    {
        const int MaxWeaponSlots = 6;
        const float GroundHeight = 0.42f;
        const float TargetDiscRadius = 0.85f;

        // V3's aim shapes keyed by weapon id (WeaponTargetingVisualizer special-cased these weapons by id). Kept in this
        // one place; the colours come from each weapon config (M16; were V3's palette keyed by id).

        struct SlotVisual
        {
            public MeshFilter Area;
            public MeshRenderer AreaRenderer;
            public MeshFilter Marker;
            public MeshRenderer MarkerRenderer;
        }

        readonly SlotVisual[] _slots = new SlotVisual[MaxWeaponSlots];
        readonly Dictionary<int, Vector3[]> _vertexCache = new Dictionary<int, Vector3[]>();
        readonly Dictionary<int, int[]> _triangleCache = new Dictionary<int, int[]>();
        MaterialPropertyBlock _mpb;
        Material _areaMaterial;
        bool _initialized;
        float _firePulseTimer;
        float _firePulseDuration = 0.18f;
        string _lastFiredWeaponId;
        Vector3 _lastForward = Vector3.forward;

        IWeaponManager _weapons;
        IShipView _ship;
        IWeaponAimView _aim;
        Func<bool> _paused;
        IToastSink _toasts;

        /// <summary>
        /// True on the frame the overlay toggle is pressed (V3: O or V, or backslash outside the weapon-enemy playtest,
        /// ignored while paused or after the run ended). Set by the wiring, which can read input.
        /// </summary>
        public Func<bool> TogglePressed;

        /// <summary>Slots from the weapon manager; targets, cluster points and the Size stat from the read-only aim view.</summary>
        public void Bind(IWeaponManager weapons, IShipView ship, IWeaponAimView aim, Func<bool> paused = null, IToastSink toasts = null)
        {
            _weapons = weapons;
            _ship = ship;
            _aim = aim;
            _paused = paused;
            _toasts = toasts;
            EnsureInitialized();
        }

        public bool IsSlotAreaVisible(int slot) => _initialized && _slots[slot].Area != null && _slots[slot].Area.gameObject.activeSelf;
        public bool IsSlotMarkerVisible(int slot) => _initialized && _slots[slot].Marker != null && _slots[slot].Marker.gameObject.activeSelf;

        /// <summary>V3 NotifyWeaponFired: the fired weapon's shape shows its locked colour briefly (Lightning Mast fades).</summary>
        public void NotifyAimed(string weaponId)
        {
            _firePulseDuration = VisualOf(weaponId)?.firePulseSeconds ?? 0.18f;
            _firePulseTimer = _firePulseDuration;
            _lastFiredWeaponId = weaponId;
        }

        static readonly WeaponEffectsVisual DefaultVisual = new WeaponEffectsVisual();

        WeaponEffectsVisual VisualOf(string weaponId)
        {
            var slots = _weapons != null ? _weapons.Slots : null;
            if (slots == null || weaponId == null) return null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Config != null && slots[i].Config.weaponId == weaponId) return slots[i].Config.visual;
            return null;
        }

        /// <summary>V3 ToggleDebugOverlays (the shared flag also shows the enemy stat labels).</summary>
        public void ToggleOverlays()
        {
            var enabled = CombatDebugOverlays.Toggle();
            if (!enabled) SetAllVisible(false);
            if (_toasts != null) _toasts.Show(enabled ? "Combat debug overlays ON (O)" : "Combat debug overlays OFF (O)", 2.5f);
        }

        void Update()
        {
            if (TogglePressed == null || !TogglePressed()) return;
            if (_paused != null && _paused()) return;
            ToggleOverlays();
        }

        void LateUpdate() => Draw(Time.deltaTime);

        /// <summary>V3 LateUpdate: hidden unless the overlays are on and the run is live; then one shape per slot.</summary>
        public void Draw(float dt)
        {
            EnsureInitialized();
            if (!CombatDebugOverlays.Visible || (_paused != null && _paused()) || _weapons == null || _ship == null || !_ship.Exists)
            {
                SetAllVisible(false);
                return;
            }

            _firePulseTimer = Mathf.Max(0f, _firePulseTimer - dt);
            var slots = _weapons.Slots;
            for (var i = 0; i < MaxWeaponSlots; i++)
            {
                if (slots != null && i < slots.Count) DrawWeaponSlot(i, slots[i]);
                else HideSlot(i);
            }
        }

        void DrawWeaponSlot(int slotIndex, WeaponSlot weapon)
        {
            if (weapon == null || weapon.Config == null)
            {
                HideSlot(slotIndex);
                return;
            }

            var def = weapon.Config;
            var id = def.weaponId;
            var sizeMult = weapon.SizeMult * (_aim != null ? _aim.SizeMultiplier : 1f);
            var effectiveRange = def.range * sizeMult;
            var pulse = _firePulseTimer > 0f && id == _lastFiredWeaponId;
            var color = AimColor(def, pulse ? 0.9f : 0.32f);
            var visual = def.visual ?? DefaultVisual;
            if (pulse && visual.firePulseFades)
                color.a *= Mathf.Clamp01(_firePulseTimer / _firePulseDuration);
            var origin = _ship.Position;
            origin.y = GroundHeight;
            var forward = _ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = _lastForward; // V3: the ship transform's forward
            forward.Normalize();
            _lastForward = forward;

            var set = _slots[slotIndex];
            var aimDirection = forward;
            var targetWorld = Vector3.zero;
            var hasTarget = _aim != null && _aim.TryResolveTarget(def, out targetWorld, out aimDirection);
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.001f) aimDirection = forward;
            else aimDirection.Normalize();

            var targetPos = hasTarget ? targetWorld : origin + forward * 4f;
            targetPos.y = GroundHeight;

            var showArea = true;
            var showMarker = false;
            var useDirectional = UsesDirectionalGeometry(def)
                                 || def.targeting is Targeting.ForwardArc or Targeting.ForwardCone or Targeting.TetherTarget;

            // V3 chose these shapes by weapon id; the choice is weapon config now (WeaponConfig.visual.aimShape, M6).
            var shape = visual.aimShape;
            if (def.pattern == Pattern.PredictedAoE || shape == ConfigEnums.AimShape.PredictedArea)
            {
                var point = PredictClusterGround(def.range);
                var radius = ResolveAoERadius(def, sizeMult);
                BuildDiscMesh(set.Area.sharedMesh, point, radius, 32);
                targetPos = point;
                showMarker = true;
                // V3's pending-shell pulse is left out: see the class summary.
            }
            else if (shape == ConfigEnums.AimShape.TargetFan)
            {
                var aim = hasTarget ? aimDirection : forward;
                BuildFanMesh(set.Area.sharedMesh, origin, aim, effectiveRange, hasTarget ? visual.aimTargetFanDegrees : 360f);
                showMarker = hasTarget;
            }
            else if (shape == ConfigEnums.AimShape.ForwardFan)
            {
                if (!useDirectional)
                    showArea = false;
                else
                {
                    BuildFanMesh(set.Area.sharedMesh, origin, forward, effectiveRange, (_aim != null ? _aim.ArcDegrees(def) : 360f));
                    showMarker = hasTarget;
                }
            }
            else if (shape == ConfigEnums.AimShape.StrikeDisc || def.pattern == Pattern.ChainLightning)
            {
                if (hasTarget)
                {
                    // V3: (3.5 + quantity × 0.5) × weapon size; the config's strike radius has the same defaults.
                    var strikeRadius = (def.areaStrike.radiusBase + weapon.QuantityBonus * def.areaStrike.radiusPerQuantity) * weapon.SizeMult;
                    BuildDiscMesh(set.Area.sharedMesh, targetPos, strikeRadius, 28);
                    showMarker = true;
                }
                else
                {
                    showArea = false;
                }
            }
            else switch (def.targeting)
            {
                case Targeting.ForwardArc:
                case Targeting.TetherTarget:
                case Targeting.ForwardCone:
                    if (!useDirectional)
                        showArea = false;
                    else
                    {
                        BuildFanMesh(set.Area.sharedMesh, origin, forward, effectiveRange, (_aim != null ? _aim.ArcDegrees(def) : 360f));
                        showMarker = hasTarget;
                    }
                    break;
                case Targeting.PortStarboard:
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
                        BuildFanMesh(set.Area.sharedMesh, origin, sideDir, effectiveRange, 18f);
                        showMarker = hasTarget;
                    }
                    break;
                case Targeting.AftDeploy:
                case Targeting.PlayerTrail when def.pattern == Pattern.MineDrop:
                    if (!useDirectional)
                        showArea = false;
                    else
                        BuildFanMesh(set.Area.sharedMesh, origin, -forward, 2.8f * sizeMult, 110f);
                    break;
                default:
                    if (def.pattern == Pattern.PredictedAoE)
                    {
                        var point = PredictClusterGround(effectiveRange);
                        BuildDiscMesh(set.Area.sharedMesh, point, def.size * sizeMult, 24);
                    }
                    else if (def.pattern == Pattern.Orbit)
                    {
                        // V3: a 5 m ring × size; the config's orbit radius has the same default.
                        BuildRingMesh(set.Area.sharedMesh, origin, def.orbit.orbitRadius * sizeMult, 32, 0.55f);
                        if (visual.orbitRingTinted)
                            color = new Color(visual.orbitRingColor.r, visual.orbitRingColor.g, visual.orbitRingColor.b, color.a);
                    }
                    else if (def.pattern == Pattern.AutoProjectile || def.targeting == Targeting.Nearest)
                    {
                        if (hasTarget)
                        {
                            BuildFanMesh(set.Area.sharedMesh, origin, aimDirection, effectiveRange, 18f);
                            showMarker = true;
                        }
                        else
                        {
                            BuildDiscMesh(set.Area.sharedMesh, origin, effectiveRange, 36);
                        }
                    }
                    else if (def.pattern == Pattern.FanVolley)
                    {
                        if (!useDirectional)
                            showArea = false;
                        else
                            BuildFanMesh(set.Area.sharedMesh, origin, forward, effectiveRange, def.fanSpreadDegrees);
                    }
                    else
                    {
                        showArea = false;
                    }
                    break;
            }

            SetRendererColor(set.AreaRenderer, color, showArea);
            if (set.Area != null)
                set.Area.gameObject.SetActive(showArea);

            var showMarkerFinal = showMarker && def.pattern != Pattern.Orbit;
            if (showMarkerFinal && set.Marker != null)
            {
                BuildDiscMesh(set.Marker.sharedMesh, targetPos, TargetDiscRadius, 16);
                var markerColor = AimColor(def, 0.75f);
                SetRendererColor(set.MarkerRenderer, markerColor, true);
                set.Marker.gameObject.SetActive(true);
            }
            else if (set.Marker != null)
            {
                set.Marker.gameObject.SetActive(false);
            }
        }

        /// <summary>V3 WeaponTargeting.UsesDirectionalGeometry on the V4 config (the experiment flag via the targeting service).</summary>
        bool UsesDirectionalGeometry(WeaponConfig def)
        {
            if (def == null) return false;
            var directional = _aim == null || _aim.DirectionalTargeting;
            if (!directional) return false;
            switch (def.targeting)
            {
                case Targeting.ForwardArc:
                case Targeting.ForwardCone:
                case Targeting.AftDeploy:
                case Targeting.PortStarboard:
                case Targeting.TetherTarget:
                    return true;
                case Targeting.PlayerTrail when def.pattern == Pattern.MineDrop:
                    return true;
                case Targeting.TrailZone:
                    return true;
                default:
                    return def.targetArcDegrees > 0f && def.targeting == Targeting.Nearest;
            }
        }

        Vector3 PredictClusterGround(float range)
        {
            if (_aim != null) return _aim.PredictClusterGround(range);
            return _ship.Position + _ship.MovementForward * 6f;
        }

        static float ResolveAoERadius(WeaponConfig def, float sizeMult)
        {
            var scaled = def.size * sizeMult;
            return Mathf.Max(def.visual != null ? def.visual.aimMinAreaRadius : 2f, scaled);
        }

        // ---- Meshes (V3's builders; arrays reused instead of allocated per frame) --------------------------------

        Vector3[] Vertices(int count)
        {
            if (!_vertexCache.TryGetValue(count, out var array)) _vertexCache[count] = array = new Vector3[count];
            return array;
        }

        int[] Triangles(int count)
        {
            if (!_triangleCache.TryGetValue(count, out var array)) _triangleCache[count] = array = new int[count];
            return array;
        }

        void BuildFanMesh(Mesh mesh, Vector3 origin, Vector3 forward, float range, float arcDegrees)
        {
            if (mesh == null) return;
            mesh.Clear();
            const int segments = 24;
            var verts = Vertices(segments + 2);
            var tris = Triangles(segments * 3);
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

        void BuildDiscMesh(Mesh mesh, Vector3 center, float radius, int segments)
        {
            if (mesh == null) return;
            mesh.Clear();
            center.y = GroundHeight;
            var verts = Vertices(segments + 1);
            var tris = Triangles(segments * 3);
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

        void BuildRingMesh(Mesh mesh, Vector3 center, float radius, int segments, float thickness)
        {
            if (mesh == null) return;
            mesh.Clear();
            center.y = GroundHeight;
            var inner = Mathf.Max(0.1f, radius - thickness);
            var verts = Vertices(segments * 2);
            var tris = Triangles(segments * 6);
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

        // ---- Objects --------------------------------------------------------------------------------------------

        void EnsureInitialized()
        {
            if (_initialized) return;
            _mpb = new MaterialPropertyBlock();
            _areaMaterial = CreateAreaMaterial();
            for (var i = 0; i < MaxWeaponSlots; i++)
            {
                _slots[i] = new SlotVisual();
                CreateShape($"WeaponArea{i}", $"WeaponAreaMesh{i}", out _slots[i].Area, out _slots[i].AreaRenderer);
                CreateShape($"WeaponTarget{i}", $"WeaponTargetMesh{i}", out _slots[i].Marker, out _slots[i].MarkerRenderer);
                _slots[i].Area.gameObject.SetActive(false);
                _slots[i].Marker.gameObject.SetActive(false);
            }
            _initialized = true;
        }

        void CreateShape(string name, string meshName, out MeshFilter filter, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = meshName };
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _areaMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void HideSlot(int slotIndex)
        {
            if (!_initialized || slotIndex < 0 || slotIndex >= MaxWeaponSlots) return;
            if (_slots[slotIndex].Area != null) _slots[slotIndex].Area.gameObject.SetActive(false);
            if (_slots[slotIndex].Marker != null) _slots[slotIndex].Marker.gameObject.SetActive(false);
        }

        void SetAllVisible(bool visible)
        {
            if (!_initialized || visible) return;
            for (var i = 0; i < MaxWeaponSlots; i++) HideSlot(i);
        }

        void SetRendererColor(MeshRenderer renderer, Color color, bool visible)
        {
            if (renderer == null || _mpb == null) return;
            renderer.enabled = visible;
            _mpb.SetColor("_BaseColor", color);
            _mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(_mpb);
        }

        static Material CreateAreaMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;
            return mat;
        }

        void OnDestroy()
        {
            for (var i = 0; i < MaxWeaponSlots; i++)
            {
                DestroyMesh(_slots[i].Area);
                DestroyMesh(_slots[i].Marker);
            }
            if (_areaMaterial != null) Kill(_areaMaterial);
        }

        static void DestroyMesh(MeshFilter filter)
        {
            if (filter != null && filter.sharedMesh != null) Kill(filter.sharedMesh);
        }

        static void Kill(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>The weapon's aim colour at an overlay alpha (locked 0.9, idle 0.32, target disc 0.75, as V3).</summary>
        static Color AimColor(WeaponConfig def, float alpha)
        {
            var c = def != null ? def.aimOverlayColor : new Color(0.7f, 0.7f, 0.7f);
            c.a = alpha;
            return c;
        }
    }
}

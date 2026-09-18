using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace StormOcean
{
    /// <summary>
    /// Reads the existing ship (Transform + optional NavMeshAgent, and ShipBuoyancy's
    /// hull length/width) to drive two purely-visual, alpha-blended effects that sit
    /// on top of the ocean surface: hull contact foam (a small procedural quad hugging
    /// the hull footprint) and a boat wake (a Unity TrailRenderer trailing the stern).
    /// This does NOT touch ShipBuoyancy, the ship's Rigidbody/NavMeshAgent, or the
    /// ship's own model/materials in any way - it only reads their current state and
    /// positions its own child renderer objects accordingly. All per-frame CPU cost is
    /// a handful of OceanController.SampleSurfaceHeight calls (the same cheap call
    /// ShipBuoyancy already makes) plus simple vector math - no per-vertex ocean tests.
    ///
    /// Contact-foam visibility bug fix: the previous version compared each hull edge's
    /// local water height against a single flat shipPos.y for every point, ignoring
    /// that the hull's own pitch/roll (correctly, physically) lifts the bow/stern/port/
    /// starboard corners to different actual world heights. On a ship with any
    /// meaningful pitch/roll (or during the buoyancy lag ShipBuoyancy itself now fixes)
    /// that flat comparison could be off by more than a meter at the bow/stern, which
    /// pushed the corner submersion values to 0 and made the foam mask multiply out to
    /// nothing everywhere - foam simply never appeared. Submersion is now sampled at
    /// each corner's TRUE world position (ship.TransformPoint, which folds in the
    /// ship's actual current rotation), so it reads correctly regardless of how much
    /// the hull is pitching or rolling.
    /// </summary>
    [ExecuteAlways]
    public class ShipFoamWake : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The existing ship Transform (e.g. the Brigg). Read-only - never modified.")]
        public Transform ship;
        [Tooltip("The existing OceanController, reused for SampleSurfaceHeight (same call ShipBuoyancy uses).")]
        public OceanController ocean;
        [Tooltip("Optional. Auto-found on the ship if left empty; used read-only to get current speed.")]
        public NavMeshAgent shipAgent;
        [Tooltip("Hull bow-to-stern length. Auto-copied from the ship's ShipBuoyancy component if found.")]
        public float hullLength = 12f;
        [Tooltip("Hull port-to-starboard width. Auto-copied from the ship's ShipBuoyancy component if found.")]
        public float hullWidth = 4f;

        [Header("Hull Contact Foam")]
        public bool contactFoamEnabled = true;
        [Range(0f, 2f)] public float contactFoamAmount = 1f;
        [Tooltip("Meters the foam footprint extends beyond the hull outline.")]
        public float contactFoamWidth = 1.2f;
        [Tooltip("Meters the foam band extends inward from the hull boundary.")]
        public float contactFoamThickness = 1.6f;
        [Range(0f, 3f)] public float contactFoamBrightness = 1.3f;
        [Range(0f, 1f)] public float contactFoamOpacity = 0.85f;
        public float contactFoamNoiseScale = 0.5f;
        [Range(0f, 2f)] public float contactFoamNoiseStrength = 0.8f;
        [Range(0f, 1f)] public float contactFoamBreakup = 0.55f;
        public float contactFoamAnimationSpeed = 0.6f;
        [Range(0f, 1f)] public float contactFoamEdgeSoftness = 0.4f;
        [Tooltip("Vertical distance (meters) from the local water surface within which a hull corner counts as \"at the waterline\" and shows foam. Larger = foam stays visible across more pitch/roll and wave variation; smaller = foam hugs the exact waterline more tightly.")]
        [FormerlySerializedAs("contactFoamSubmersionRange")]
        public float waterlineThreshold = 1.1f;

        [Header("Boat Wake")]
        public bool wakeEnabled = true;
        [Range(0f, 3f)] public float wakeStrength = 1f;
        [Tooltip("Target length of the wake trail in meters (approximate - converted to a trail lifetime from current speed).")]
        public float wakeLength = 28f;
        public float wakeWidth = 3.2f;
        [Range(0f, 2f)] public float wakeExpansion = 1f;
        [Range(0f, 1f)] public float wakeOpacity = 0.8f;
        [Range(0f, 3f)] public float wakeBrightness = 1.2f;
        public float wakeNoiseScale = 0.35f;
        [Range(0f, 2f)] public float wakeNoiseStrength = 0.9f;
        [Range(0f, 1f)] public float wakeBreakup = 0.6f;
        public float wakeAnimationSpeed = 0.5f;
        [Tooltip("Distance (meters) over which the wake fades out completely.")]
        public float wakeFadeDistance = 22f;
        [Range(0f, 1f)] public float wakeEdgeSoftness = 0.35f;

        [Header("Speed Response")]
        [Tooltip("Speed (m/s) below which the wake is considered off.")]
        public float minimumWakeSpeed = 0.3f;
        [Range(0f, 3f)] public float maximumWakeStrength = 1.5f;
        [Tooltip("Exponent shaping the speed->wake curve. <1 ramps up quickly, >1 ramps up slowly.")]
        public float speedResponse = 0.7f;
        [Tooltip("Speed range (m/s) above minimumWakeSpeed over which the wake reaches full strength.")]
        public float wakeSpeedMultiplier = 4f;

        [Header("Debug")]
        [Tooltip("Draws the waterline, the four foam corner sample points (colored by submersion: green = full foam, red = none), the foam footprint bounds and the ship's current velocity as gizmos in the Scene view. Off by default.")]
        public bool showBoatFoamDebug = false;

        // -- internal --
        GameObject contactFoamGO;
        MeshFilter contactFilter;
        MeshRenderer contactRenderer;
        Material contactMatInstance;
        float builtHalfLengthExt = -1f, builtHalfWidthExt = -1f;

        GameObject wakeGO;
        TrailRenderer trail;
        Material wakeMatInstance;

        Vector3 lastShipPos;
        bool havePrevPos;
        float smoothedSpeed;

        // last-computed debug values (updated every LateUpdate, drawn by OnDrawGizmos)
        Vector3 dbgBowPort, dbgBowStar, dbgSternPort, dbgSternStar;
        float dbgSubBowPort, dbgSubBowStar, dbgSubSternPort, dbgSubSternStar;
        float dbgWaterAtShip;
        Vector3 dbgVelocity;

        static readonly int CornerSternPortID = Shader.PropertyToID("_CornerSternPort");
        static readonly int CornerSternStarID = Shader.PropertyToID("_CornerSternStar");
        static readonly int CornerBowPortID = Shader.PropertyToID("_CornerBowPort");
        static readonly int CornerBowStarID = Shader.PropertyToID("_CornerBowStar");
        static readonly int HalfLengthExtID = Shader.PropertyToID("_HalfLengthExt");
        static readonly int HalfWidthExtID = Shader.PropertyToID("_HalfWidthExt");

        void OnEnable()
        {
            if (ship != null)
            {
                if (shipAgent == null) shipAgent = ship.GetComponent<NavMeshAgent>();
                var buoyancy = ship.GetComponent<ShipBuoyancy>();
                if (buoyancy != null)
                {
                    hullLength = buoyancy.length;
                    hullWidth = buoyancy.width;
                }
            }
            EnsureContactFoam();
            EnsureWake();
            havePrevPos = false;
        }

        void EnsureContactFoam()
        {
            if (contactFoamGO == null)
            {
                contactFoamGO = GameObject.Find("Ship Contact Foam");
                if (contactFoamGO == null)
                {
                    contactFoamGO = new GameObject("Ship Contact Foam");
                }
            }
            contactFilter = contactFoamGO.GetComponent<MeshFilter>();
            if (contactFilter == null) contactFilter = contactFoamGO.AddComponent<MeshFilter>();
            contactRenderer = contactFoamGO.GetComponent<MeshRenderer>();
            if (contactRenderer == null) contactRenderer = contactFoamGO.AddComponent<MeshRenderer>();

            if (contactMatInstance == null)
            {
                var shader = Shader.Find("StormOcean/ContactFoam");
                if (shader != null)
                {
                    contactMatInstance = new Material(shader);
                    contactMatInstance.name = "StormOcean_ContactFoam_Runtime";
                }
            }
            if (contactMatInstance != null) contactRenderer.sharedMaterial = contactMatInstance;
            contactRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            contactRenderer.receiveShadows = false;

            RebuildContactMeshIfNeeded(force: true);
        }

        void RebuildContactMeshIfNeeded(bool force)
        {
            float halfLengthExt = hullLength * 0.5f + contactFoamWidth;
            float halfWidthExt = hullWidth * 0.5f + contactFoamWidth;
            if (!force && Mathf.Approximately(halfLengthExt, builtHalfLengthExt) && Mathf.Approximately(halfWidthExt, builtHalfWidthExt) && contactFilter.sharedMesh != null)
            {
                return;
            }

            const int res = 10;
            var verts = new Vector3[(res + 1) * (res + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[res * res * 6];

            for (int z = 0; z <= res; z++)
            {
                float vz = (float)z / res; // 0 stern -> 1 bow
                float worldZ = Mathf.Lerp(-halfLengthExt, halfLengthExt, vz);
                for (int x = 0; x <= res; x++)
                {
                    float vx = (float)x / res; // 0 port -> 1 starboard
                    float worldX = Mathf.Lerp(-halfWidthExt, halfWidthExt, vx);
                    int idx = z * (res + 1) + x;
                    verts[idx] = new Vector3(worldX, 0f, worldZ);
                    uvs[idx] = new Vector2(vx, vz);
                }
            }

            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i0 = z * (res + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (res + 1);
                    int i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.name = "ShipContactFoamMesh";
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            contactFilter.sharedMesh = mesh;

            builtHalfLengthExt = halfLengthExt;
            builtHalfWidthExt = halfWidthExt;
        }

        void EnsureWake()
        {
            if (wakeGO == null)
            {
                wakeGO = GameObject.Find("Ship Wake");
                if (wakeGO == null)
                {
                    wakeGO = new GameObject("Ship Wake");
                }
            }
            trail = wakeGO.GetComponent<TrailRenderer>();
            if (trail == null) trail = wakeGO.AddComponent<TrailRenderer>();

            if (wakeMatInstance == null)
            {
                var shader = Shader.Find("StormOcean/Wake");
                if (shader != null)
                {
                    wakeMatInstance = new Material(shader);
                    wakeMatInstance.name = "StormOcean_Wake_Runtime";
                }
            }
            if (wakeMatInstance != null) trail.sharedMaterial = wakeMatInstance;

            trail.textureMode = LineTextureMode.Tile;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.generateLightingData = false;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.minVertexDistance = 0.15f;

            if (trail.widthCurve == null || trail.widthCurve.length < 2)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.35f, 1.0f),
                    new Keyframe(1f, 1.6f)
                );
                trail.widthCurve = curve;
            }

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            trail.colorGradient = grad;
        }

        void LateUpdate()
        {
            if (ship == null || ocean == null) return;

            RebuildContactMeshIfNeeded(force: false);

            float t = Time.time;
            Vector3 shipPos = ship.position;

            Vector3 flatFwd = new Vector3(ship.forward.x, 0f, ship.forward.z);
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            Vector3 flatRight = new Vector3(flatFwd.z, 0f, -flatFwd.x);

            // ---- speed (read-only: prefer the ship's own NavMeshAgent velocity) ----
            float rawSpeed;
            if (shipAgent != null && shipAgent.enabled)
            {
                rawSpeed = new Vector3(shipAgent.velocity.x, 0f, shipAgent.velocity.z).magnitude;
            }
            else if (havePrevPos && Time.deltaTime > 0.0001f)
            {
                rawSpeed = new Vector3(shipPos.x - lastShipPos.x, 0f, shipPos.z - lastShipPos.z).magnitude / Time.deltaTime;
            }
            else
            {
                rawSpeed = 0f;
            }
            Vector3 velocityForDebug = havePrevPos && Time.deltaTime > 0.0001f
                ? (shipPos - lastShipPos) / Time.deltaTime
                : Vector3.zero;
            if (shipAgent != null && shipAgent.enabled) velocityForDebug = shipAgent.velocity;
            dbgVelocity = velocityForDebug;
            lastShipPos = shipPos;
            havePrevPos = true;
            smoothedSpeed = Application.isPlaying ? Mathf.Lerp(smoothedSpeed, rawSpeed, 0.15f) : rawSpeed;

            float speedNorm = Mathf.Clamp01((smoothedSpeed - minimumWakeSpeed) / Mathf.Max(0.01f, wakeSpeedMultiplier));
            float speedCurve = Mathf.Pow(speedNorm, Mathf.Max(0.01f, speedResponse));
            float wakeStrength01 = Mathf.Clamp01(speedCurve);

            // ---- hull contact foam ----
            if (contactFoamGO != null)
            {
                bool showContact = contactFoamEnabled;
                contactFoamGO.SetActive(showContact);
                if (showContact)
                {
                    float waterHere = ocean.SampleSurfaceHeight(shipPos.x, shipPos.z, t);
                    dbgWaterAtShip = waterHere;
                    Vector3 pos = new Vector3(shipPos.x, waterHere + 0.03f, shipPos.z);
                    contactFoamGO.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(flatFwd, Vector3.up));

                    float halfLengthExt = hullLength * 0.5f + contactFoamWidth;
                    float halfWidthExt = hullWidth * 0.5f + contactFoamWidth;

                    // Sample submersion at each corner's TRUE world position (folds in the
                    // ship's actual current pitch/roll via TransformPoint) rather than
                    // comparing against a single flat shipPos.y for every corner - a flat
                    // comparison is wrong the moment the hull is pitching or rolling at all,
                    // since the bow/stern (or port/starboard) corners are then physically at
                    // different world heights than the hull's center.
                    Vector3 bowPortWS = ship.TransformPoint(new Vector3(-hullWidth * 0.5f, 0f, hullLength * 0.5f));
                    Vector3 bowStarWS = ship.TransformPoint(new Vector3(hullWidth * 0.5f, 0f, hullLength * 0.5f));
                    Vector3 sternPortWS = ship.TransformPoint(new Vector3(-hullWidth * 0.5f, 0f, -hullLength * 0.5f));
                    Vector3 sternStarWS = ship.TransformPoint(new Vector3(hullWidth * 0.5f, 0f, -hullLength * 0.5f));

                    float subBowPort = SubmersionAtWorldPoint(bowPortWS, t);
                    float subBowStar = SubmersionAtWorldPoint(bowStarWS, t);
                    float subSternPort = SubmersionAtWorldPoint(sternPortWS, t);
                    float subSternStar = SubmersionAtWorldPoint(sternStarWS, t);

                    dbgBowPort = bowPortWS; dbgBowStar = bowStarWS;
                    dbgSternPort = sternPortWS; dbgSternStar = sternStarWS;
                    dbgSubBowPort = subBowPort; dbgSubBowStar = subBowStar;
                    dbgSubSternPort = subSternPort; dbgSubSternStar = subSternStar;

                    if (contactMatInstance != null)
                    {
                        contactMatInstance.SetFloat("_Amount", contactFoamAmount * Mathf.Lerp(1f, 1.4f, wakeStrength01));
                        contactMatInstance.SetFloat("_Thickness", contactFoamThickness);
                        contactMatInstance.SetFloat("_Brightness", contactFoamBrightness);
                        contactMatInstance.SetFloat("_Opacity", contactFoamOpacity);
                        contactMatInstance.SetFloat("_NoiseScale", contactFoamNoiseScale);
                        contactMatInstance.SetFloat("_NoiseStrength", contactFoamNoiseStrength);
                        contactMatInstance.SetFloat("_Breakup", contactFoamBreakup);
                        contactMatInstance.SetFloat("_AnimSpeed", contactFoamAnimationSpeed);
                        contactMatInstance.SetFloat("_EdgeSoftness", contactFoamEdgeSoftness);
                        contactMatInstance.SetFloat(HalfLengthExtID, halfLengthExt);
                        contactMatInstance.SetFloat(HalfWidthExtID, halfWidthExt);
                        contactMatInstance.SetFloat(CornerSternPortID, subSternPort);
                        contactMatInstance.SetFloat(CornerSternStarID, subSternStar);
                        contactMatInstance.SetFloat(CornerBowPortID, subBowPort);
                        contactMatInstance.SetFloat(CornerBowStarID, subBowStar);
                    }
                }
            }

            // ---- boat wake ----
            if (trail != null)
            {
                bool showWake = wakeEnabled;
                bool emitting = showWake && smoothedSpeed > minimumWakeSpeed * 0.5f;
                trail.emitting = emitting;

                float waterAtStern = ocean.SampleSurfaceHeight(shipPos.x - flatFwd.x * (hullLength * 0.5f), shipPos.z - flatFwd.z * (hullLength * 0.5f), t);
                Vector3 sternPos = shipPos - flatFwd * (hullLength * 0.5f);
                sternPos.y = waterAtStern + 0.03f;
                wakeGO.transform.position = sternPos;

                float safeSpeed = Mathf.Max(0.5f, smoothedSpeed);
                trail.time = Mathf.Clamp(wakeFadeDistance / safeSpeed, 1.5f, 40f);
                trail.widthMultiplier = wakeWidth * wakeExpansion * Mathf.Lerp(0.15f, 1f, wakeStrength01);

                if (wakeMatInstance != null)
                {
                    wakeMatInstance.SetFloat("_Brightness", wakeBrightness);
                    wakeMatInstance.SetFloat("_Opacity", wakeOpacity);
                    wakeMatInstance.SetFloat("_NoiseScale", wakeNoiseScale);
                    wakeMatInstance.SetFloat("_NoiseStrength", wakeNoiseStrength);
                    wakeMatInstance.SetFloat("_Breakup", wakeBreakup);
                    wakeMatInstance.SetFloat("_AnimSpeed", wakeAnimationSpeed);
                    wakeMatInstance.SetFloat("_EdgeSoftness", wakeEdgeSoftness);
                    wakeMatInstance.SetFloat("_StrengthMul", wakeStrength * wakeStrength01);
                }
            }
        }

        /// <summary>
        /// 1 when the given world point is at/below the local water surface directly
        /// under it, fading to 0 over waterlineThreshold meters as that point rises
        /// above the local water. Reuses ocean.SampleSurfaceHeight - the same cheap
        /// call ShipBuoyancy already makes every frame - so this adds no meaningful
        /// CPU cost and never duplicates the buoyancy physics itself.
        /// </summary>
        float SubmersionAtWorldPoint(Vector3 worldPos, float t)
        {
            float localWater = ocean.SampleSurfaceHeight(worldPos.x, worldPos.z, t);
            float diff = worldPos.y - localWater; // >0 means this point is above local water here
            return 1f - Mathf.Clamp01(diff / Mathf.Max(0.05f, waterlineThreshold));
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!showBoatFoamDebug || ship == null || ocean == null) return;

            // waterline at the ship's own position
            UnityEditor.Handles.color = Color.cyan;
            Vector3 waterPoint = new Vector3(ship.position.x, dbgWaterAtShip, ship.position.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(waterPoint, 0.35f);
            UnityEditor.Handles.Label(waterPoint + Vector3.up * 0.6f, $"water Y = {dbgWaterAtShip:F2}");

            // the four foam corner sample points, colored red (no foam) -> green (full foam)
            DrawCornerGizmo(dbgBowPort, dbgSubBowPort, "BP");
            DrawCornerGizmo(dbgBowStar, dbgSubBowStar, "BS");
            DrawCornerGizmo(dbgSternPort, dbgSubSternPort, "SP");
            DrawCornerGizmo(dbgSternStar, dbgSubSternStar, "SS");

            // foam footprint bounds (the contact-foam quad outline)
            if (contactFoamGO != null)
            {
                Gizmos.color = Color.white;
                Matrix4x4 prevMatrix = Gizmos.matrix;
                Gizmos.matrix = contactFoamGO.transform.localToWorldMatrix;
                float hl = hullLength * 0.5f + contactFoamWidth;
                float hw = hullWidth * 0.5f + contactFoamWidth;
                Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(hw * 2f, 0.02f, hl * 2f));
                Gizmos.matrix = prevMatrix;
            }

            // ship velocity
            Gizmos.color = Color.yellow;
            Vector3 from = ship.position + Vector3.up * 1.5f;
            Gizmos.DrawLine(from, from + dbgVelocity);
            UnityEditor.Handles.Label(from + dbgVelocity, $"vel = {dbgVelocity.magnitude:F2} m/s");
        }

        void DrawCornerGizmo(Vector3 worldPos, float submersion, string label)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, submersion);
            Gizmos.DrawSphere(worldPos, 0.25f);
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.3f, $"{label} {submersion:F2}");
        }
#endif
    }
}

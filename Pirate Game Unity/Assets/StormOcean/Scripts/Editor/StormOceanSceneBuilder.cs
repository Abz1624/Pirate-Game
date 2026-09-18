using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StormOcean.Editor
{
    /// <summary>
    /// One-click automatic assembly of the StormOcean_Demo scene: camera, ocean,
    /// sky, lighting/lightning, rain, spray, atmosphere and post-processing.
    /// Run via Tools/Storm Ocean/Build Demo Scene. Fully self-contained: does not
    /// touch any existing scene or asset outside Assets/StormOcean/.
    /// </summary>
    public static class StormOceanSceneBuilder
    {
        const string ScenesFolder = "Assets/StormOcean/Scenes";
        const string MaterialsFolder = "Assets/StormOcean/Materials";
        const string GeneratedFolder = "Assets/StormOcean/Generated";
        const string ScenePath = ScenesFolder + "/StormOcean_Demo.unity";

        [MenuItem("Tools/Storm Ocean/Build Demo Scene")]
        public static void BuildScene()
        {
            // Safety guard: a full rebuild does EditorSceneManager.NewScene(EmptyScene),
            // which would silently destroy the user's live ship + buoyancy setup (added by
            // hand, not by this builder, and explicitly protected - it must never be
            // removed/recreated). If the currently open scene already has a ship with a
            // ShipBuoyancy component, refuse and tell the user to work on the live scene
            // directly (or use Force Rebuild (Destroys Ship) if they really mean to start over).
            var existingBuoyancy = Object.FindObjectOfType<ShipBuoyancy>();
            if (existingBuoyancy != null)
            {
                Debug.LogError("[StormOcean] Aborted: a ship with ShipBuoyancy ('" + existingBuoyancy.gameObject.name + "') already exists in the open scene. " +
                    "Rebuilding would destroy it. Edit the ocean/shader/material directly on the live scene instead. " +
                    "If you intentionally want a from-scratch scene, remove the ship GameObject first, then re-run this command.");
                return;
            }

            EnsureFolders();

            Material oceanMat = GetOrCreateMaterial(MaterialsFolder + "/StormOcean_Ocean.mat", "StormOcean/Ocean");
            Material skyMat = GetOrCreateMaterial(MaterialsFolder + "/StormOcean_Sky.mat", "StormOcean/Sky");
            Material rainMat = GetOrCreateMaterial(MaterialsFolder + "/StormOcean_RainStreak.mat", "StormOcean/RainStreak");
            Material sprayMat = GetOrCreateMaterial(MaterialsFolder + "/StormOcean_FoamSprite.mat", "StormOcean/FoamSprite");
            Material boltMat = GetOrCreateMaterial(MaterialsFolder + "/StormOcean_LightningBolt.mat", "StormOcean/LightningBolt");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Camera ----------------
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0f, 3.0f, 0f);
            camGO.transform.rotation = Quaternion.Euler(-6f, 0f, 0f);
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 1f);
            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // ---------------- Storm Ocean ----------------
            var oceanParent = new GameObject("Storm Ocean");
            var oceanMeshGO = new GameObject("Ocean Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            oceanMeshGO.transform.SetParent(oceanParent.transform);
            oceanMeshGO.transform.position = Vector3.zero;
            var oceanController = oceanMeshGO.AddComponent<OceanController>();
            oceanController.oceanMaterial = oceanMat;
            oceanMeshGO.GetComponent<MeshRenderer>().sharedMaterial = oceanMat;

            var waterFollow = camGO.AddComponent<CameraWaterFollow>();
            waterFollow.ocean = oceanController;
            waterFollow.heightOffset = 3.2f;
            waterFollow.followSmoothing = 0.08f;
            waterFollow.sampleRadius = 10f;


            // ---------------- Storm Sky ----------------
            var skyGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skyGO.name = "Storm Sky";
            Object.DestroyImmediate(skyGO.GetComponent<Collider>());
            skyGO.transform.localScale = Vector3.one * 1800f;
            var skyRenderer = skyGO.GetComponent<MeshRenderer>();
            skyRenderer.sharedMaterial = skyMat;
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;
            skyRenderer.lightProbeUsage = LightProbeUsage.Off;
            var cloudController = skyGO.AddComponent<StormCloudController>();
            cloudController.skyRenderer = skyRenderer;

            // ---------------- Storm Lighting ----------------
            var lightingParent = new GameObject("Storm Lighting");

            var sunGO = new GameObject("Directional Light");
            sunGO.transform.SetParent(lightingParent.transform);
            sunGO.transform.rotation = Quaternion.Euler(22f, -50f, 0f);
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.65f, 0.72f, 0.8f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;

            var skyFlashGO = new GameObject("Sky Flash Light");
            skyFlashGO.transform.SetParent(lightingParent.transform);
            skyFlashGO.transform.position = new Vector3(0f, 300f, 0f);
            var skyFlash = skyFlashGO.AddComponent<Light>();
            skyFlash.type = LightType.Point;
            skyFlash.range = 2500f;
            skyFlash.intensity = 0f;
            skyFlash.color = new Color(0.8f, 0.88f, 1f);
            skyFlash.shadows = LightShadows.None;
            skyFlash.enabled = false;

            var lightningGO = new GameObject("Lightning Controller");
            lightningGO.transform.SetParent(lightingParent.transform);
            var line = lightningGO.AddComponent<LineRenderer>();
            line.sharedMaterial = boltMat;
            line.useWorldSpace = true;
            line.widthCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 0.2f);
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.positionCount = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            var lightning = lightningGO.AddComponent<LightningController>();
            lightning.mainLight = sun;
            lightning.skyFlashLight = skyFlash;
            lightning.boltLine = line;

            // ---------------- Rain ----------------
            var rainGO = new GameObject("Rain", typeof(ParticleSystem));
            var rainPS = rainGO.GetComponent<ParticleSystem>();
            rainGO.GetComponent<ParticleSystemRenderer>().sharedMaterial = rainMat;
            var rainController = rainGO.AddComponent<RainController>();

            // ---------------- Ocean Spray ----------------
            var sprayGO = new GameObject("Ocean Spray", typeof(ParticleSystem));
            sprayGO.GetComponent<ParticleSystemRenderer>().sharedMaterial = sprayMat;
            var sprayController = sprayGO.AddComponent<OceanSprayController>();
            sprayController.oceanReference = oceanMeshGO.transform;

            // ---------------- Atmosphere (fog + wind) ----------------
            var atmosphereGO = new GameObject("Atmosphere");
            var atmosphere = atmosphereGO.AddComponent<StormAtmosphereController>();
            var wind = atmosphereGO.AddComponent<StormWindController>();

            rainController.wind = wind;
            sprayController.wind = wind;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.13f);
            RenderSettings.skybox = null;

            // ---------------- Post Processing ----------------
            var postGO = new GameObject("Post Processing");
            var volume = postGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = GetOrCreatePostProcessProfile(GeneratedFolder + "/StormOcean_PostProcess.asset");

            // Force everything to (re)push its parameters now that all references exist.
            oceanController.ApplyMaterialParams();

            // Search a spread of yaw/pitch directions from the camera's approximate
            // resting position for the one with the longest unobstructed sightline
            // over the (very large, steep) storm waves, so the auto-built scene never
            // opens with the camera nose-first into a wave wall.
            GameObject shipGO = SpawnShip(oceanController);
            AimCameraAtClearView(camGO, oceanController, waterFollow, shipGO != null ? shipGO.transform.position : new Vector3(0f, 0f, 40f));



            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[StormOcean] Demo scene built at " + ScenePath);
        }

static void AimCameraAtClearView(GameObject camGO, OceanController ocean, CameraWaterFollow waterFollow, Vector3 shipWorldPos)
        {
            float aimY = ocean.SampleSwellHeightSmoothed(0f, 0f, 0f, waterFollow.sampleRadius) + waterFollow.heightOffset;
            Vector3 aimPos = new Vector3(0f, aimY, 0f);

            Vector3 toShipFlat = new Vector3(shipWorldPos.x - aimPos.x, 0f, shipWorldPos.z - aimPos.z);
            float baseYaw = toShipFlat.sqrMagnitude > 0.01f ? Mathf.Atan2(toShipFlat.x, toShipFlat.z) * Mathf.Rad2Deg : 0f;

            float bestScore = float.NegativeInfinity;
            float bestDist = -1f;
            float bestPitch = -2f;
            float bestYaw = baseYaw;

            // Search a yaw cone centred on the ship's bearing (so it stays in frame) and
            // a pitch range, picking whichever has the clearest sightline over the waves;
            // yaw drift away from the ship's bearing is penalised so framing wins ties.
            for (float yawOffset = -20f; yawOffset <= 20f; yawOffset += 10f)
            {
                float yaw = baseYaw + yawOffset;
                for (float pitch = -2f; pitch <= 8f; pitch += 2f)
                {
                    Vector3 dir = (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward).normalized;
                    float dist = RaymarchToSurface(ocean, aimPos, dir, 0f, 150f, 1.5f);
                    float score = dist - Mathf.Abs(yawOffset) * 2f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDist = dist;
                        bestPitch = pitch;
                        bestYaw = yaw;
                    }
                }
            }

            camGO.transform.position = aimPos;
            camGO.transform.rotation = Quaternion.Euler(bestPitch, bestYaw, 0f);
            Debug.Log("[StormOcean] Auto-aimed camera at ship: yaw=" + bestYaw + " pitch=" + bestPitch + " clearDist=" + bestDist.ToString("F1") + "m");
        }

        static float RaymarchToSurface(OceanController ocean, Vector3 origin, Vector3 dir, float t, float maxDist, float step)
        {
            for (float d = step; d <= maxDist; d += step)
            {
                Vector3 p = origin + dir * d;
                float h = ocean.SampleSurfaceHeight(p.x, p.z, t);
                if (p.y <= h) return d;
            }
            return maxDist;
        }

        
const string ShipPrefabPath = "Assets/GameObject/Brigg.prefab";

static GameObject SpawnShip(OceanController ocean)
        {
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
            if (shipPrefab == null)
            {
                Debug.LogWarning("[StormOcean] Ship prefab not found at " + ShipPrefabPath + " - skipping ship spawn.");
                return null;
            }

            GameObject shipGO = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
            shipGO.name = "Brigg";
            // Brigg's bow/stern axis is already local +Z (Unity's default forward),
            // matching ShipBuoyancy's sampling axes - no correction rotation needed.
            shipGO.transform.rotation = Quaternion.identity;
            shipGO.transform.localScale = Vector3.one;
            shipGO.transform.position = new Vector3(17.63f, 0f, 25.35f);

            var buoyancy = shipGO.AddComponent<ShipBuoyancy>();
            buoyancy.ocean = ocean;
            buoyancy.length = 8f;
            buoyancy.width = 4f;
            // Brigg's pivot sits near the keel already, so only a small positive offset
            // is needed to keep the hull riding on top of the surface, not buried in it.
            buoyancy.floatHeight = 0.5f;
            buoyancy.maxPitchDegrees = 15f;
            buoyancy.maxRollDegrees = 18f;

            return shipGO;
        }

        
static void EnsureFolders()
        {
            EnsureFolder("Assets/StormOcean", "Scenes");
            EnsureFolder("Assets/StormOcean", "Materials");
            EnsureFolder("Assets/StormOcean", "Generated");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static Material GetOrCreateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[StormOcean] Shader not found: " + shaderName);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (shader != null) existing.shader = shader;
                return existing;
            }

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static VolumeProfile GetOrCreatePostProcessProfile(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            VolumeProfile profile = existing;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                profile.components.Clear();
            }

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.6f;
            bloom.intensity.value = 0.10f;
            bloom.scatter.value = 0.3f;
            bloom.tint.value = new Color(0.75f, 0.85f, 1f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.0f;
            color.contrast.value = 6f;
            color.saturation.value = -18f;
            color.colorFilter.value = new Color(0.85f, 0.92f, 1f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.32f;
            vignette.smoothness.value = 0.6f;
            vignette.color.value = new Color(0.01f, 0.01f, 0.02f);

            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}

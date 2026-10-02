using System.Collections.Generic;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trailer
{
    /// <summary>
    /// Film-set helpers for the trailer's planned-feature shots (Arena, Hunt, Island Race, raids): water colour swaps,
    /// stand-in rival ships cloned from the player's ship model, a ghost ship, scripted cannon exchanges and rain.
    /// None of this is gameplay; the stand-ins have no colliders, health or AI. Editor and development builds only.
    /// </summary>
    public static class TrailerStage
    {
        static readonly List<GameObject> Puppets = new List<GameObject>();
        static GameObject _rain;
        static readonly Dictionary<Renderer, Material> OriginalWater = new Dictionary<Renderer, Material>();

        static PlayerShipController Ship => Object.FindFirstObjectByType<PlayerShipController>();

        static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        /// <summary>blue (Saltwind), green (Gravewater, Water_Level_2), red (Devil's Maw, Water_Level_3), storm.</summary>
        public static string Water(string look)
        {
            var path = look switch
            {
                "green" => "Assets/Material/Water_Level_2.mat",
                "red" => "Assets/Material/Water_Level_3.mat",
                "storm" => "Assets/Material/Water_Storm.mat",
                _ => null,
            };
            var mat = path != null ? Load<Material>(path) : null;
            var count = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var current = r.sharedMaterial;
                if (current == null) continue;
                var isWater = OriginalWater.ContainsKey(r) || current.name.StartsWith("Water_Level") || current.name == "Water_Storm";
                if (!isWater) continue;
                if (!OriginalWater.ContainsKey(r)) OriginalWater[r] = current;
                r.sharedMaterial = mat != null ? mat : OriginalWater[r];
                count++;
            }
            return $"water {look} on {count} tiles";
        }

        /// <summary>
        /// Clones the player's ship model as a stand-in rival. Behaviour: orbit (circles the player), escort (sails
        /// alongside at the player's heading), hold (stays put, facing the player). Sail: rook, roxie, ember, richie,
        /// sparky or empty to keep the model's own.
        /// </summary>
        public static string Rival(string behaviour, float radius, float angleDeg, string sail = "", bool ghost = false, float speed = 0f)
        {
            var ship = Ship;
            if (ship == null || ship.VisualModel == null) return "no ship";
            var model = Object.Instantiate(ship.VisualModel.gameObject);
            model.name = "TrailerRival_" + Puppets.Count;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            foreach (var ps in model.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            model.transform.localScale = ship.VisualModel.lossyScale;
            if (!string.IsNullOrEmpty(sail)) SetSail(model, sail);
            if (ghost) MakeGhost(model);
            var puppet = model.AddComponent<TrailerPuppet>();
            puppet.Begin(ship.transform, behaviour, radius, angleDeg, speed);
            Puppets.Add(model);
            return $"{model.name} {behaviour} r={radius} a={angleDeg} sail={sail} ghost={ghost}";
        }

        static void SetSail(GameObject model, string captain)
        {
            var file = captain.ToLowerInvariant() == "rook" ? "rook" : char.ToUpperInvariant(captain[0]) + captain.Substring(1).ToLowerInvariant();
            var tex = Load<Texture2D>($"Assets/_Game/Art/Sails/{file}_sail_colour.png");
            if (tex == null) return;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.ToLowerInvariant().Contains("sail") || r.sharedMaterial == null) continue;
                var m = new Material(r.sharedMaterial);
                foreach (var prop in new[] { "_BaseTexture", "_BaseMap", "_MainTex" })
                    if (m.HasProperty(prop)) m.SetTexture(prop, tex);
                r.sharedMaterial = m;
            }
        }

        static void MakeGhost(GameObject model)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            var ghost = new Material(shader) { color = new Color(0.55f, 1f, 0.95f, 0.38f) };
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < mats.Length; i++) mats[i] = ghost;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Scripted cannon exchange between the player and every rival (every interval seconds, both ways).</summary>
        public static string Duel(bool on, float interval = 0.7f)
        {
            var ship = Ship;
            if (ship == null) return "no ship";
            var duel = ship.GetComponent<TrailerDuel>();
            if (!on) { if (duel != null) Object.Destroy(duel); return "duel off"; }
            if (duel == null) duel = ship.gameObject.AddComponent<TrailerDuel>();
            duel.Begin(Puppets, interval,
                Load<GameObject>("Assets/GameObject/Guns/VFX/Broadside_Muzzle_VFX.prefab"),
                Load<GameObject>("Assets/GameObject/Guns/VFX/Broadside_Cannonball_VFX.prefab"),
                Load<GameObject>("Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab"),
                Load<GameObject>("Assets/Prefabs/VFX/19_WaterImpactSplash.prefab"));
            return $"duel on with {Puppets.Count} rivals";
        }

        /// <summary>A cannon island (or any prefab under Assets/GameObject) at a fixed spot for raid and home-island shots; it joins the scripted exchange.</summary>
        public static string Tower(float distance, float angleDeg, float scale = 1f, string prefabName = "Cannon_Islands/island_Cannon_1")
        {
            var ship = Ship;
            if (ship == null) return "no ship";
            var prefab = Load<GameObject>($"Assets/GameObject/{prefabName}.prefab");
            var rad = angleDeg * Mathf.Deg2Rad;
            var pos = ship.transform.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * distance;
            var tower = prefab != null ? Object.Instantiate(prefab, pos, Quaternion.identity) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.name = "TrailerTower_" + Puppets.Count;
            foreach (var c in tower.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var mb in tower.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            tower.transform.localScale *= scale;
            tower.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(ship.transform.position - pos, Vector3.up));
            Puppets.Add(tower);
            return $"{tower.name} at {pos.x:0},{pos.z:0}";
        }

        static readonly Dictionary<Light, (float, Color)> OriginalLights = new Dictionary<Light, (float, Color)>();
        static (bool, Color, float, Color)? _originalFog;

        /// <summary>day (as authored), gravewater (dim green night light) or maw (red-orange light). No fog: the game's toon shaders render fogged geometry cyan.</summary>
        public static string Mood(string look)
        {
            if (_originalFog == null) _originalFog = (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.ambientLight);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (!OriginalLights.ContainsKey(l)) OriginalLights[l] = (l.intensity, l.color);
                var (i, c) = OriginalLights[l];
                switch (look)
                {
                    case "gravewater": l.intensity = i * 0.35f; l.color = new Color(0.55f, 0.85f, 0.7f); break;
                    case "maw": l.intensity = i * 0.7f; l.color = new Color(1f, 0.55f, 0.35f); break;
                    default: l.intensity = i; l.color = c; break;
                }
            }
            var f = _originalFog.Value;
            switch (look)
            {
                default: RenderSettings.fog = f.Item1; RenderSettings.fogColor = f.Item2; RenderSettings.fogDensity = f.Item3; break;
            }
            return $"mood {look}";
        }

        /// <summary>Rain that follows the camera.</summary>
        public static string Rain(bool on)
        {
            if (!on) { if (_rain != null) Object.Destroy(_rain); _rain = null; return "rain off"; }
            if (_rain != null) return "rain on";
            var prefab = Load<GameObject>("Assets/RainMaker/Prefab/RainPrefab.prefab");
            if (prefab == null) return "no rain prefab";
            _rain = Object.Instantiate(prefab);
            return "rain on";
        }

        public static string Clear()
        {
            foreach (var p in Puppets) if (p != null) Object.Destroy(p);
            Puppets.Clear();
            Duel(false);
            Rain(false);
            return "stage cleared";
        }
    }
}

using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Playtest (3 Oct, owner: "do not use cannonballs as placeholders for weapons that are not cannons"): stand-in models
    /// built from coloured primitives for the weapons whose data still points at a cannon asset (or at nothing, which
    /// falls back to the default cannonball): the Parrot Squadron's birds, the Monkey Gunners' turret and its shots, the
    /// Anchor Flail's anchors, the Lighthouse Lantern's lanterns and the Cursed Doubloons' coins. Each template is built
    /// once per weapon (inactive, under the pool staging) and pooled by the presenters like any model; colours are shared
    /// materials (one per colour, no per-instance copies). Once real art is set in the weapon's data (a model whose name
    /// is not a cannon's) it is used instead. V5 M8z (7 Oct): which stand-in a weapon gets is its data
    /// (WeaponEffectsVisual.placeholderLook), no longer a switch on its weapon id here.
    /// </summary>
    public static class WeaponPlaceholderModels
    {

        static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        static Transform _root;

        /// <summary>True when a model is missing or a cannon asset standing in for a weapon that is not a cannon.</summary>
        public static bool IsCannonStandIn(GameObject model) =>
            model == null || model.name.IndexOf("Cannon", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The summon model to draw: the placeholder when the data's model is a cannon stand-in, else the data's.
        /// Hackathon (12 Oct): the art team's parrot or monkey buccaneer (<paramref name="art"/>) before the coloured stand-in.</summary>
        public static GameObject Summon(WeaponPlaceholderLook look, GameObject dataModel, Transform staging, DemoJuiceSettings art = null)
        {
            if (!IsCannonStandIn(dataModel)) return dataModel;
            if (art != null && look == WeaponPlaceholderLook.Parrots && art.parrotArtModel != null) return art.parrotArtModel;
            if (art != null && look == WeaponPlaceholderLook.MonkeyGunners && art.monkeyArtModel != null) return art.monkeyArtModel;
            return look switch
            {
                WeaponPlaceholderLook.Parrots => Get("Placeholder_Parrot", BuildParrot, staging),
                WeaponPlaceholderLook.MonkeyGunners => Get("Placeholder_Monkey", BuildMonkey, staging),
                _ => dataModel,
            };
        }

        /// <summary>The orbit model to draw: the placeholder when the data's model is a cannon stand-in, else the data's.
        /// Hackathon (12 Oct): the art team's lantern (<paramref name="art"/>) before the coloured stand-in.</summary>
        public static GameObject Orbit(WeaponPlaceholderLook look, GameObject dataModel, Transform staging, DemoJuiceSettings art = null)
        {
            if (dataModel == null || !IsCannonStandIn(dataModel)) return dataModel; // no model: drawn as cutlass blades
            if (art != null && look == WeaponPlaceholderLook.Lanterns && art.lanternArtModel != null) return art.lanternArtModel;
            return look switch
            {
                WeaponPlaceholderLook.Anchors => Get("Placeholder_Anchor", BuildAnchor, staging),
                WeaponPlaceholderLook.Lanterns => Get("Placeholder_Lantern", BuildLantern, staging),
                _ => dataModel,
            };
        }

        /// <summary>A projectile model for a weapon with none in its data (which would fall back to the cannonball); null
        /// for cannon weapons, which keep the cannonball.</summary>
        public static GameObject Projectile(WeaponPlaceholderLook look, Transform staging) => look switch
        {
            WeaponPlaceholderLook.MonkeyGunners => Get("Placeholder_MonkeyShot", BuildMonkeyShot, staging),
            WeaponPlaceholderLook.Parrots => Get("Placeholder_ParrotShot", BuildParrotShot, staging),
            _ => null,
        };

        /// <summary>A gold coin (the Cursed Doubloons' strike).</summary>
        public static GameObject Coin(Transform staging) => Get("Placeholder_Doubloon", BuildCoin, staging);

        /// <summary>Hackathon (13 Oct, owner): the gold barrel's collect-particle coin (its CoinParticles material) on a
        /// unit quad, for the Cursed Doubloons; the presenter turns it to the camera.</summary>
        public static GameObject CoinSprite(Transform staging, Material coinMaterial) => Get("Doubloon_CoinSprite", () =>
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = coinMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }, staging);

        /// <summary>Hackathon (12 Oct): a spiked iron ball (the Anchor Flail's chained mace), or the art team's anchor
        /// (<paramref name="art"/>) when set.</summary>
        public static GameObject Mace(Transform staging, DemoJuiceSettings art = null) =>
            art != null && art.maceArtModel != null ? art.maceArtModel : Get("Placeholder_Mace", BuildMace, staging);

        static GameObject Get(string name, Func<GameObject> build, Transform staging)
        {
            if (_root == null)
            {
                // The staging went with its scene: every template goes with it (the shared materials stay).
                Templates.Clear();
                _root = new GameObject("WeaponPlaceholderTemplates").transform;
                _root.gameObject.SetActive(false);
                if (staging != null) _root.SetParent(staging, false);
                else _root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            if (Templates.TryGetValue(name, out var template) && template != null) return template;
            template = build();
            template.name = name;
            template.transform.SetParent(_root, false);
            Templates[name] = template;
            return template;
        }

        // ---- Templates (unit sized; the presenters scale them) ---------------------------------------------------

        static readonly Color ParrotGreen = new Color(0.15f, 0.8f, 0.25f);
        static readonly Color ParrotRed = new Color(0.95f, 0.18f, 0.12f);
        static readonly Color ParrotYellow = new Color(1f, 0.82f, 0.15f);
        static readonly Color ParrotBlue = new Color(0.15f, 0.45f, 0.95f);
        static readonly Color MonkeyBrown = new Color(0.45f, 0.27f, 0.12f);
        static readonly Color MonkeyTan = new Color(0.85f, 0.65f, 0.42f);
        static readonly Color RaftWood = new Color(0.32f, 0.2f, 0.1f);
        static readonly Color Iron = new Color(0.33f, 0.36f, 0.42f);
        static readonly Color LanternGlow = new Color(1f, 0.86f, 0.4f);
        static readonly Color LanternCap = new Color(0.18f, 0.16f, 0.14f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
        static readonly Color ShotOrange = new Color(1f, 0.7f, 0.2f);
        static readonly Color ShotGreen = new Color(0.4f, 1f, 0.35f);

        static GameObject BuildParrot()
        {
            var root = new GameObject();
            var bird = new GameObject("Bird").transform; // flies above the water
            bird.SetParent(root.transform, false);
            bird.localPosition = new Vector3(0f, 1.1f, 0f);
            bird.localScale = Vector3.one * 1.7f; // the data's summon scale (0.6) left the birds hard to see from the camera
            Part(bird, PrimitiveType.Capsule, ParrotGreen, new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.55f, 0.45f), new Vector3(90f, 0f, 0f), 0.35f);
            Part(bird, PrimitiveType.Sphere, ParrotRed, new Vector3(0f, 0.15f, 0.55f), Vector3.one * 0.42f, Vector3.zero, 0.45f);
            Part(bird, PrimitiveType.Cube, ParrotYellow, new Vector3(0f, 0.08f, 0.82f), new Vector3(0.12f, 0.12f, 0.2f), Vector3.zero, 0.4f);
            Part(bird, PrimitiveType.Cube, ParrotBlue, new Vector3(0.5f, 0.08f, 0f), new Vector3(0.75f, 0.06f, 0.38f), new Vector3(0f, 0f, 12f), 0.3f);
            Part(bird, PrimitiveType.Cube, ParrotBlue, new Vector3(-0.5f, 0.08f, 0f), new Vector3(0.75f, 0.06f, 0.38f), new Vector3(0f, 0f, -12f), 0.3f);
            Part(bird, PrimitiveType.Cube, ParrotRed, new Vector3(0f, -0.02f, -0.62f), new Vector3(0.22f, 0.05f, 0.45f), Vector3.zero, 0.3f);
            return root;
        }

        static GameObject BuildMonkey()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Cube, RaftWood, new Vector3(0f, 0.05f, 0f), new Vector3(1.3f, 0.16f, 1.3f), Vector3.zero, 0f);
            Part(root.transform, PrimitiveType.Cube, MonkeyBrown, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.7f, 0.45f), Vector3.zero, 0.15f);
            Part(root.transform, PrimitiveType.Cube, MonkeyTan, new Vector3(0f, 1.05f, 0.04f), new Vector3(0.42f, 0.4f, 0.4f), Vector3.zero, 0.15f);
            Part(root.transform, PrimitiveType.Cube, MonkeyBrown, new Vector3(0.28f, 1.15f, 0f), new Vector3(0.12f, 0.16f, 0.06f), Vector3.zero, 0.1f);
            Part(root.transform, PrimitiveType.Cube, MonkeyBrown, new Vector3(-0.28f, 1.15f, 0f), new Vector3(0.12f, 0.16f, 0.06f), Vector3.zero, 0.1f);
            Part(root.transform, PrimitiveType.Cylinder, LanternCap, new Vector3(0.18f, 0.62f, 0.4f), new Vector3(0.12f, 0.32f, 0.12f), new Vector3(90f, 0f, 0f), 0f); // the musket
            return root;
        }

        static GameObject BuildMonkeyShot()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Capsule, ShotOrange, Vector3.zero, new Vector3(0.45f, 0.7f, 0.45f), new Vector3(90f, 0f, 0f), 1.2f);
            return root;
        }

        static GameObject BuildParrotShot()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Capsule, ShotGreen, Vector3.zero, new Vector3(0.35f, 0.6f, 0.35f), new Vector3(90f, 0f, 0f), 1.2f);
            return root;
        }

        static GameObject BuildAnchor()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Cube, Iron, new Vector3(0f, 0.55f, 0f), new Vector3(0.16f, 1.05f, 0.16f), Vector3.zero, 0.15f);   // shank
            Part(root.transform, PrimitiveType.Cube, Iron, new Vector3(0f, 1.0f, 0f), new Vector3(0.7f, 0.12f, 0.12f), Vector3.zero, 0.15f);    // stock
            Part(root.transform, PrimitiveType.Cube, Iron, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.14f, 0.14f), Vector3.zero, 0.15f);    // arms
            Part(root.transform, PrimitiveType.Cube, Iron, new Vector3(0.45f, 0.24f, 0f), new Vector3(0.12f, 0.36f, 0.14f), new Vector3(0f, 0f, 20f), 0.15f);
            Part(root.transform, PrimitiveType.Cube, Iron, new Vector3(-0.45f, 0.24f, 0f), new Vector3(0.12f, 0.36f, 0.14f), new Vector3(0f, 0f, -20f), 0.15f);
            Part(root.transform, PrimitiveType.Sphere, Iron, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.2f, Vector3.zero, 0.15f);              // ring
            return root;
        }

        static GameObject BuildLantern()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Cylinder, LanternGlow, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.35f, 0.5f), Vector3.zero, 2.2f);
            Part(root.transform, PrimitiveType.Cylinder, LanternCap, new Vector3(0f, 0.95f, 0f), new Vector3(0.6f, 0.06f, 0.6f), Vector3.zero, 0f);
            Part(root.transform, PrimitiveType.Cylinder, LanternCap, new Vector3(0f, 0.16f, 0f), new Vector3(0.6f, 0.06f, 0.6f), Vector3.zero, 0f);
            Part(root.transform, PrimitiveType.Cube, LanternCap, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 0.06f, 0.06f), Vector3.zero, 0f);
            return root;
        }

        static GameObject BuildCoin()
        {
            var root = new GameObject();
            // Standing on its edge (it spins about the vertical axis as it flies).
            Part(root.transform, PrimitiveType.Cylinder, Gold, Vector3.zero, new Vector3(0.7f, 0.04f, 0.7f), new Vector3(90f, 0f, 0f), 1.3f);
            return root;
        }

        static GameObject BuildMace()
        {
            var root = new GameObject();
            Part(root.transform, PrimitiveType.Sphere, Iron, Vector3.zero, Vector3.one * 0.95f, Vector3.zero, 0.2f); // the ball
            // Six spikes along the axes and eight on the diagonals (small cubes turned to stand out of the ball).
            var axes = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1),
                new Vector3(1, 1, -1), new Vector3(-1, 1, -1), new Vector3(1, -1, -1), new Vector3(-1, -1, -1) };
            foreach (var a in axes)
            {
                var d = a.normalized;
                Part(root.transform, PrimitiveType.Cube, SpikeIron, d * 0.52f, new Vector3(0.16f, 0.16f, 0.32f), Quaternion.LookRotation(d).eulerAngles, 0.25f);
            }
            Part(root.transform, PrimitiveType.Sphere, SpikeIron, new Vector3(0f, 0f, -0.55f), new Vector3(0.22f, 0.22f, 0.12f), Vector3.zero, 0.2f); // the chain's ring
            return root;
        }

        static readonly Color SpikeIron = new Color(0.5f, 0.52f, 0.58f);

        static void Part(Transform parent, PrimitiveType type, Color color, Vector3 position, Vector3 scale, Vector3 euler, float emission)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider); // before the pool clones the template
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color, emission);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material MaterialFor(Color color, float emission)
        {
            var key = new Color(color.r, color.g, color.b, emission); // alpha carries the emission strength in the key
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { name = "WeaponPlaceholder", hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", color * emission);
            }
            Materials[key] = m;
            return m;
        }
    }
}

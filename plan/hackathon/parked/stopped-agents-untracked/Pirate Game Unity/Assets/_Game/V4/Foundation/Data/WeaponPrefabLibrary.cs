using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    /// <summary>
    /// Hackathon (13 Oct, owner: tweak every weapon's look in the Editor with real prefabs): the weapon visuals that were
    /// built in code at run time (coloured placeholder shots, the aura and ink pulse rings, the flail's chain line, the
    /// shells' landing ring) as prefabs, per weapon id, under Assets/_Game/V4/Demo/Art/WeaponPrefabs (built by
    /// Tools/Rogue Pirates/Demo/Build Weapon Prefabs (hackathon), HackathonWeaponPrefabs). The presenters pool and prewarm
    /// these prefabs, so edits to them show in the game. Presentation only: nothing here is read by the simulation, and an
    /// empty slot keeps the old code-built look. The full per-weapon list is plan/hackathon/WEAPON-PREFABS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "V4WeaponPrefabs", menuName = "RoguePirates/Weapon Prefab Library")]
    public sealed class WeaponPrefabLibrary : ScriptableObject
    {
        public const string ResourcePath = "V4WeaponPrefabs";

        [Tooltip("The landing ring of a delayed shell or whirlpool (Mortar, Maelstrom Charge, Squid Ink Pots; the Leviathan Heart item): " +
                 "a LineRenderer at its root. Code sets its points, colour and width.")]
        public GameObject landingRing;

        [Tooltip("Per weapon id: the prefabs that replace a code-built visual.")]
        public WeaponPrefabEntry[] weapons;

        static WeaponPrefabLibrary _loaded;

        /// <summary>The library in Resources (null when there is none: every visual keeps its code-built look).</summary>
        public static WeaponPrefabLibrary Load()
        {
            if (_loaded == null) _loaded = Resources.Load<WeaponPrefabLibrary>(ResourcePath);
            return _loaded;
        }

        public WeaponPrefabEntry Find(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || weapons == null) return null;
            for (var i = 0; i < weapons.Length; i++)
                if (weapons[i] != null && weapons[i].weaponId == weaponId) return weapons[i];
            return null;
        }

        public GameObject Projectile(string weaponId) => Find(weaponId)?.projectile;
        public GameObject PulseRing(string weaponId) => Find(weaponId)?.pulseRing;
        public GameObject Chain(string weaponId) => Find(weaponId)?.chain;
    }

    /// <summary>A weapon id and its prefabs for the visuals that were built in code (empty: the code-built look).</summary>
    [Serializable]
    public sealed class WeaponPrefabEntry
    {
        public string weaponId;
        [Tooltip("Its projectile (or its summons' shots), drawn in its own colours instead of the config's model or the coloured placeholder. " +
                 "Code sets its position, facing and uniform scale (from the shot's size).")]
        public GameObject projectile;
        [Tooltip("The pulse ring (an aura's pulse, the ink's landing and slick ticks): a LineRenderer at its root. Code sets its points, " +
                 "colour (the weapon's aim overlay colour, fading) and width multiplier.")]
        public GameObject pulseRing;
        [Tooltip("The chained mace's chain line (a LineRenderer at its root): the curve the chain links are laid along, drawn itself only " +
                 "when there are no chain link models. Code sets its points and texture tiling.")]
        public GameObject chain;
    }
}

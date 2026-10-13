using System;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>The visuals a weapon draws, one prefab each (<see cref="WeaponVisualSet"/>).</summary>
    public enum WeaponVisualSlot
    {
        Projectile, Muzzle, Hit, CritHit, WaterSplash, Beam, Orbit, Stream, PatchRing, Mine, Blast, Strike, Coin,
        PulseRing, Aura, LandingRing, Impact, Lob, Launch, Whirlpool, Summon, Mace, ChainLink, Chain,
    }

    /// <summary>
    /// Hackathon (13 Oct, owner: "tweak all weapons and their VFX in the Unity Editor without code"): which prefab each of
    /// a weapon's visuals is drawn from, per weapon id. The prefabs live one folder per weapon under
    /// Assets/_Game/V4/Weapons/Prefabs/&lt;weapon_id&gt;/ (built by Tools/Rogue Pirates/Weapons/Build Weapon Prefabs,
    /// HackathonWeaponPrefabs, as copies of what the game drew before, so the look is unchanged). The presenters
    /// (<see cref="WeaponVfxPresenter"/>, <see cref="ProjectileViewPresenter"/>, <see cref="FlameStreamPresenter"/>, the
    /// combat feedback's hit sparks and the hazard rings) pool and prewarm these prefabs before the run, so an edit to a
    /// prefab shows in the game. An empty slot keeps the shared look from the juice settings. Each prefab can carry a
    /// <see cref="WeaponVisualTuning"/> for the numbers the code applies (scale, lifetime, spin, arc, width, colour).
    /// Presentation only: nothing here is read by the simulation. The per-weapon list is plan/hackathon/WEAPON-PREFABS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "V4WeaponVisuals", menuName = "RoguePirates/Weapon Visual Catalog")]
    public sealed class WeaponVisualCatalog : ScriptableObject
    {
        /// <summary>Resources path (the asset is Assets/_Game/V4/Weapons/Resources/V4WeaponVisuals.asset).</summary>
        public const string ResourcePath = "V4WeaponVisuals";

        [Tooltip("One entry per weapon id: the prefab each of its visuals is drawn from (empty: the shared juice settings look).")]
        public WeaponVisualSet[] weapons = Array.Empty<WeaponVisualSet>();

        static WeaponVisualCatalog _loaded;

        /// <summary>The catalog in Resources (null when there is none: every visual keeps its shared look).</summary>
        public static WeaponVisualCatalog Load()
        {
            if (_loaded == null) _loaded = Resources.Load<WeaponVisualCatalog>(ResourcePath);
            return _loaded;
        }

        public WeaponVisualSet Find(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || weapons == null) return null;
            for (var i = 0; i < weapons.Length; i++)
                if (weapons[i] != null && weapons[i].weaponId == weaponId) return weapons[i];
            return null;
        }

        /// <summary>The weapon's prefab for a visual (null: none set).</summary>
        public GameObject Get(string weaponId, WeaponVisualSlot slot) => Find(weaponId)?.Get(slot);

        /// <summary>The weapon's prefab for a visual, else <paramref name="fallback"/>.</summary>
        public GameObject Pick(string weaponId, WeaponVisualSlot slot, GameObject fallback)
        {
            var prefab = Get(weaponId, slot);
            return prefab != null ? prefab : fallback;
        }
    }

    /// <summary>One weapon's prefabs, a field per visual (see plan/hackathon/WEAPON-PREFABS.md for what each is).</summary>
    [Serializable]
    public sealed class WeaponVisualSet
    {
        public string weaponId;

        [Header("Shots")]
        [Tooltip("The shot model (Projectile and Fan weapons, evolution pellets, a summon's shots).")] public GameObject projectile;
        [Tooltip("The muzzle flash on each shot (a gun prefab with a CannonSmoke child shows only that child).")] public GameObject muzzle;
        [Tooltip("The Long Nine's beam (a LineRenderer with LongNineBeamVisual).")] public GameObject beam;

        [Header("Hits")]
        [Tooltip("The spark (or cannon impact) on each enemy hit.")] public GameObject hit;
        [Tooltip("The spark on a critical hit (cannon weapons scale their impact instead).")] public GameObject critHit;
        [Tooltip("The splash when a shot, shell, coin, summon or anchor meets the water.")] public GameObject waterSplash;

        [Header("Close range")]
        [Tooltip("An orbiting blade or model (Flying Cutlasses, Lighthouse Lantern).")] public GameObject orbit;
        [Tooltip("The flame stream (its Flame particle system is used).")] public GameObject stream;
        [Tooltip("The ring around a Hellfire Nozzle burning patch (a LineRenderer).")] public GameObject patchRing;

        [Header("Kegs and strikes")]
        [Tooltip("A dropped powder keg.")] public GameObject mine;
        [Tooltip("A keg's blast (its 'Impact small', 'Ring' and 'Smoke' children).")] public GameObject blast;
        [Tooltip("A lightning strike.")] public GameObject strike;
        [Tooltip("A thrown coin (Cursed Doubloons).")] public GameObject coin;

        [Header("Areas")]
        [Tooltip("The ring an aura pulse or an ink splash sweeps out (a LineRenderer).")] public GameObject pulseRing;
        [Tooltip("The aura shown around the ship while the weapon is equipped (sized to the pulse radius).")] public GameObject aura;
        [Tooltip("A shell's landing ring (outline and filling ring; a LineRenderer).")] public GameObject landingRing;
        [Tooltip("The bursts when a shell lands or a whirlpool implodes.")] public GameObject impact;
        [Tooltip("The model thrown from the ship to each shell's ring.")] public GameObject lob;
        [Tooltip("The burst at the ship when a shell is thrown.")] public GameObject launch;
        [Tooltip("The whirlpool on the water while it lasts (sized to its radius).")] public GameObject whirlpool;

        [Header("Summons and maces")]
        [Tooltip("A summon (parrot, monkey turret).")] public GameObject summon;
        [Tooltip("The thrown anchor or mace (a ChainAttach child is where the chain meets it).")] public GameObject mace;
        [Tooltip("One chain link (its mesh and material are drawn instanced along the chain).")] public GameObject chainLink;
        [Tooltip("The chain's curve (a LineRenderer; drawn itself only when there is no chain link).")] public GameObject chain;

        public GameObject Get(WeaponVisualSlot slot) => slot switch
        {
            WeaponVisualSlot.Projectile => projectile,
            WeaponVisualSlot.Muzzle => muzzle,
            WeaponVisualSlot.Hit => hit,
            WeaponVisualSlot.CritHit => critHit,
            WeaponVisualSlot.WaterSplash => waterSplash,
            WeaponVisualSlot.Beam => beam,
            WeaponVisualSlot.Orbit => orbit,
            WeaponVisualSlot.Stream => stream,
            WeaponVisualSlot.PatchRing => patchRing,
            WeaponVisualSlot.Mine => mine,
            WeaponVisualSlot.Blast => blast,
            WeaponVisualSlot.Strike => strike,
            WeaponVisualSlot.Coin => coin,
            WeaponVisualSlot.PulseRing => pulseRing,
            WeaponVisualSlot.Aura => aura,
            WeaponVisualSlot.LandingRing => landingRing,
            WeaponVisualSlot.Impact => impact,
            WeaponVisualSlot.Lob => lob,
            WeaponVisualSlot.Launch => launch,
            WeaponVisualSlot.Whirlpool => whirlpool,
            WeaponVisualSlot.Summon => summon,
            WeaponVisualSlot.Mace => mace,
            WeaponVisualSlot.ChainLink => chainLink,
            WeaponVisualSlot.Chain => chain,
            _ => null,
        };

        public void Set(WeaponVisualSlot slot, GameObject prefab)
        {
            switch (slot)
            {
                case WeaponVisualSlot.Projectile: projectile = prefab; break;
                case WeaponVisualSlot.Muzzle: muzzle = prefab; break;
                case WeaponVisualSlot.Hit: hit = prefab; break;
                case WeaponVisualSlot.CritHit: critHit = prefab; break;
                case WeaponVisualSlot.WaterSplash: waterSplash = prefab; break;
                case WeaponVisualSlot.Beam: beam = prefab; break;
                case WeaponVisualSlot.Orbit: orbit = prefab; break;
                case WeaponVisualSlot.Stream: stream = prefab; break;
                case WeaponVisualSlot.PatchRing: patchRing = prefab; break;
                case WeaponVisualSlot.Mine: mine = prefab; break;
                case WeaponVisualSlot.Blast: blast = prefab; break;
                case WeaponVisualSlot.Strike: strike = prefab; break;
                case WeaponVisualSlot.Coin: coin = prefab; break;
                case WeaponVisualSlot.PulseRing: pulseRing = prefab; break;
                case WeaponVisualSlot.Aura: aura = prefab; break;
                case WeaponVisualSlot.LandingRing: landingRing = prefab; break;
                case WeaponVisualSlot.Impact: impact = prefab; break;
                case WeaponVisualSlot.Lob: lob = prefab; break;
                case WeaponVisualSlot.Launch: launch = prefab; break;
                case WeaponVisualSlot.Whirlpool: whirlpool = prefab; break;
                case WeaponVisualSlot.Summon: summon = prefab; break;
                case WeaponVisualSlot.Mace: mace = prefab; break;
                case WeaponVisualSlot.ChainLink: chainLink = prefab; break;
                case WeaponVisualSlot.Chain: chain = prefab; break;
            }
        }
    }
}

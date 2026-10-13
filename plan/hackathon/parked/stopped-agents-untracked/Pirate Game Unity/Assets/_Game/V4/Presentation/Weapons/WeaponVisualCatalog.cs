using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (13 Oct, owner: tweak every weapon's look in the Editor with real prefabs): which prefab each of a
    /// weapon's visuals is drawn from, resolved the way the presenters pick them (<see cref="ProjectileViewPresenter"/>,
    /// <see cref="WeaponVfxPresenter"/>, <see cref="FlameStreamPresenter"/>, the combat feedback's hit sparks and the
    /// hazard rings' landing ring), with what the code sets on it at run time. Read only; it builds nothing (a visual that
    /// would still be built in code has a null prefab). Used by the weapon prefab tests and the WEAPON-PREFABS.md list.
    /// </summary>
    public static class WeaponVisualCatalog
    {
        public readonly struct Entry
        {
            public readonly string Role;
            public readonly GameObject Prefab;
            /// <summary>What the presenter sets on the instance at run time (prefab values it overrides).</summary>
            public readonly string Overrides;
            /// <summary>The prefab is shared with other weapons or effects (editing it changes them too).</summary>
            public readonly bool Shared;

            public Entry(string role, GameObject prefab, string overrides, bool shared)
            {
                Role = role;
                Prefab = prefab;
                Overrides = overrides;
                Shared = shared;
            }
        }

        const string GunChild = "CannonSmoke";
        const string ReticleChild = "Visuals/WaterReticle";

        public static List<Entry> Of(WeaponConfig w, DemoJuiceSettings s, WeaponPrefabLibrary lib)
        {
            var list = new List<Entry>();
            if (w == null || s == null) return list;
            var id = w.weaponId;
            var art = s.WeaponArt(id);
            var own = lib != null ? lib.Projectile(id) : null;
            void Add(string role, GameObject prefab, string overrides, bool shared = false) => list.Add(new Entry(role, prefab, overrides, shared));

            switch (w.behavior)
            {
                case ConfigEnums.WeaponBehavior.Projectile:
                case ConfigEnums.WeaponBehavior.Fan:
                    if (w.behavior == ConfigEnums.WeaponBehavior.Projectile && w.lane.enabled)
                    {
                        Muzzle(w, s, list);
                        if (w.lane.beam)
                            Add("beam", s.laserBeamPrefab, "both end points (muzzle to range, 0.32 m up), width multiplier = beam half-width x 2.4 (0.12-1.4); shown for laserBeamDuration; a LongNineBeamVisual is added if missing", true);
                    }
                    else
                    {
                        Add("projectile", ProjectileModel(w, s, art, own),
                            ProjectileOverrides(art != null || own != null || w.visual.placeholderLook != WeaponPlaceholderLook.None && (w.projectileVisual.prefab == null)));
                        Muzzle(w, s, list);
                        Add("water splash (shot ends on the water)", s.waterImpactSplashPrefab, "position, scale waterImpactSplashScale; merged with a splash within 1.35 m / 0.14 s", true);
                    }
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.Orbit:
                    if (w.visual.orbitModel == null)
                        Add("orbit blade", s.cutlassPrefab, "position (the simulation's blade), lying flat along the ship's heading, scale cutlassScale; only its Blade child and particles show (root mesh hidden); particle start colours tinted gold every frame", true);
                    else
                        Add("orbit model", OrbitModel(w, s), "position (the simulation's blade), spin 540 deg/s about Y, scale = config orbitModelScale; colliders and rigidbodies removed");
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.ConeStream:
                    Add("flame stream (its Flame particle system)", s.flamethrowerStreamPrefab != null ? s.flamethrowerStreamPrefab : s.flamethrowerPrefab,
                        "posed ahead of the ship (stream forwardOffset, 0.45 m up); scale flamethrowerScale x 1.35; start speed scaled so the flame reaches the cone's range, cone angle = half the arc; looping, emission switched on/off while streaming (authored rate kept)");
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.Trail:
                    Add("burning patch", s.burningTrailPrefab, "position, scale burningTrailScale x patch radius / 2.8 (0.75-1.6), 1.6 s", true);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.Mine:
                    Add("keg (dropped mine)", s.mineBarrelPrefab, "position, scale mineBarrelScale, physics removed; an unblown keg sinks and tilts", true);
                    var blast = s.powderKegExplosionPrefab != null ? s.powderKegExplosionPrefab : s.weaponExplosionPrefab;
                    Add("keg blast (only its 'Impact small', 'Ring', 'Smoke' children)", blast, "position, scale from powderKegExplosionScale and the blast radius; particles restarted; 1.5 s", true);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.AreaStrike:
                    if (w.visual.placeholderLook == WeaponPlaceholderLook.Doubloons)
                        Add("coin (3 a strike, thrown from the ship)", s.goldPickupPrefab != null ? s.goldPickupPrefab : null,
                            "position along its arc, spin, scale goldFlyingCoinScale; physics removed (it is the gold pickup's coin)", true);
                    else
                        Add("strike", s.lightningStrikePrefab, "position on the water, scale lightningStrikeScale x radius / 3.5 (0.85-1.35); particles restarted; 1.15 s", true);
                    if (w.visual.placeholderLook == WeaponPlaceholderLook.Doubloons)
                        Add("water splash (first coin lands)", s.waterImpactSplashPrefab, "position, scale waterImpactSplashScale", true);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.Aura:
                    Add("pulse ring (two a pulse)", lib != null ? lib.PulseRing(id) : null, PulseOverrides);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.PredictedArea:
                    var p = w.predictedArea;
                    if (p.telegraphMatchesImpact)
                        Add("landing ring (outline + filling ring)", lib != null ? lib.landingRing : null, RingOverrides, true);
                    else
                    {
                        var template = s.mortarTelegraphPrefab != null ? s.mortarTelegraphPrefab : s.lightningStrikePrefab;
                        var reticle = template != null ? template.transform.Find(ReticleChild) : null;
                        Add("telegraph reticle (" + ReticleChild + " child)", reticle != null ? template : null, "position, scale from the radius, particle colours tinted mortarTelegraphColor", true);
                    }
                    if (art != null && art.lobModel != null)
                        Add("lobbed model (thrown to each shell's ring)", art.lobModel, "position along its arc (lobArcHeight), scale lobScale (juice settings weaponArtModels)");
                    if (w.visual.pulseImpact)
                        Add("impact pulse ring (shell lands, slick ticks)", lib != null ? lib.PulseRing(id) : null, PulseOverrides);
                    else
                        Add("impact bursts (4 a shell)", MortarBurst(s), "position (spread inside the ring), scale weaponExplosionScale x mortarExplosionScaleMult x radius; particles replayed; 1.05 s", true);
                    if (p.whirlpoolSeconds > 0f)
                    {
                        Add("whirlpool ring", lib != null ? lib.landingRing : null, RingOverrides, true);
                        if (p.implodeDamageMultiplier > 0f)
                            Add("whirlpool implosion bursts", MortarBurst(s), "as the impact bursts", true);
                    }
                    Add("water splash (shell lands)", s.waterImpactSplashPrefab, "position, scale waterImpactSplashScale", true);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.Summon:
                    Add("summon model", SummonModel(w, s), "position and facing (the simulation's), scale = config summonModelScale; physics removed");
                    if (w.summon.shotSpeed > 0f)
                    {
                        var shot = own != null ? own : w.projectileVisual.prefab != null ? w.projectileVisual.prefab
                            : w.visual.placeholderLook == WeaponPlaceholderLook.Parrots || w.visual.placeholderLook == WeaponPlaceholderLook.MonkeyGunners ? null : s.projectilePrefab;
                        Add("projectile (its summons' shots)", shot, ProjectileOverrides(true));
                    }
                    Add("water splash (summon appears)", s.waterImpactSplashPrefab, "position, scale waterImpactSplashScale", true);
                    Hit(w, s, list);
                    break;

                case ConfigEnums.WeaponBehavior.ChainedMace:
                    Add("mace (the thrown anchor)", s.maceArtModel, "position (the simulation's mace), facing away from the ship and rolling 420 deg/s; scale 1; physics removed; its ChainAttach child is where the chain meets it");
                    Add("chain link (drawn instanced along the chain)", s.maceChainLinkModel, "only its mesh and material are drawn (GPU instancing): one link every maceChainLinkSpacing m along the chain curve, each turned 90 deg from the last, scaled to spacing x 1.3; the prefab's root scale and other components are not used");
                    Add("chain line (the curve; drawn itself only without chain links)", lib != null ? lib.Chain(id) : null, "its 12 points (ship rail to the anchor, sagging), texture tiling (one tile per 0.35 m), shown/hidden; hidden while the chain links are drawn");
                    Add("water splash (mace hits the water)", s.waterImpactSplashPrefab, "position, scale waterImpactSplashScale", true);
                    Hit(w, s, list);
                    break;
            }
            return list;
        }

        const string PulseOverrides = "its 48 points (a circle sweeping out to the pulse radius), start/end colour (the weapon's aimOverlayColor, fading), width multiplier (0.5 / 0.3 shrinking); world space, looped";
        const string RingOverrides = "its 40 points (a circle of the shell's radius; the fill ring grows over the shell's delay), colour (orange, fill alpha rising), width multiplier (0.3 outline, 0.45 fill); world space, looped";

        static string ProjectileOverrides(bool ownColours) =>
            "position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scale" +
            (ownColours ? "; drawn in its own colours" : "; every material tinted with the weapon's projectileColor (colour and emission)") +
            "; physics removed; cannonball trails cleared and boosted";

        static GameObject ProjectileModel(WeaponConfig w, DemoJuiceSettings s, ArtWeaponModel art, GameObject own)
        {
            if (art != null && art.projectileModel != null) return art.projectileModel;
            if (own != null) return own;
            if (w.projectileVisual.prefab != null) return w.projectileVisual.prefab;
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Parrots || w.visual.placeholderLook == WeaponPlaceholderLook.MonkeyGunners) return null; // code-built
            return s.projectilePrefab;
        }

        static GameObject OrbitModel(WeaponConfig w, DemoJuiceSettings s)
        {
            var model = w.visual.orbitModel;
            if (!WeaponPlaceholderModels.IsCannonStandIn(model)) return model;
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Lanterns) return s.lanternArtModel; // null: the code-built lantern
            return w.visual.placeholderLook == WeaponPlaceholderLook.Anchors ? null : model;
        }

        static GameObject SummonModel(WeaponConfig w, DemoJuiceSettings s)
        {
            var model = w.visual.summonModel;
            if (model == null) return null;
            if (!WeaponPlaceholderModels.IsCannonStandIn(model)) return model;
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Parrots) return s.parrotArtModel;
            if (w.visual.placeholderLook == WeaponPlaceholderLook.MonkeyGunners) return s.monkeyArtModel;
            return model;
        }

        static GameObject MortarBurst(DemoJuiceSettings s) => s.mortarExplosionPrefab != null ? s.mortarExplosionPrefab : s.weaponExplosionPrefab;

        /// <summary>The muzzle prefab as <see cref="WeaponVfxPresenter"/> resolves it (a VFX profile's muzzle prefab would replace it).</summary>
        public static GameObject MuzzlePrefab(WeaponConfig w, DemoJuiceSettings s)
        {
            if (s.TryGetWeaponVfx(w.weaponId, out var binding) && binding.muzzleVfxPrefab != null) return binding.muzzleVfxPrefab;
            if (s.cannonMuzzleVfxPrefab != null && w.visual.cannonMuzzle) return s.cannonMuzzleVfxPrefab;
            return s.shootingVfxPrefab;
        }

        static void Muzzle(WeaponConfig w, DemoJuiceSettings s, List<Entry> list)
        {
            var prefab = MuzzlePrefab(w, s);
            var gun = prefab != null && prefab.transform.Find(GunChild) != null;
            var side = w.behavior == ConfigEnums.WeaponBehavior.Fan && !w.fan.muzzlePerPellet;
            list.Add(new Entry((side ? "side salvo muzzle" : "muzzle") + (gun ? " (only its CannonSmoke child shows)" : " (the whole prefab)"), prefab,
                "position and facing (the firing direction), scale shootingVfxScale x muzzle scale (juice binding or config fallbackMuzzleScale); particles restarted; " + (side ? "0.4 s" : "0.35 s"), true));
        }

        static void Hit(WeaponConfig w, DemoJuiceSettings s, List<Entry> list)
        {
            switch (w.hitEffect)
            {
                case ConfigEnums.WeaponHitEffect.CannonImpact:
                    list.Add(new Entry("hit (cannon impact)", s.cannonImpactPrefab != null ? s.cannonImpactPrefab : s.weaponExplosionPrefab,
                        "position, scale cannonImpactScale (x1.12 on a crit), 0.55 s", true));
                    break;
                case ConfigEnums.WeaponHitEffect.None:
                    list.Add(new Entry("hit (small spark)", s.enemyHitPrefab, "position, scale enemyHitScale x 0.8", true));
                    break;
                default:
                    list.Add(new Entry("hit", s.enemyHitPrefab, "position, scale enemyHitScale", true));
                    list.Add(new Entry("hit (crit)", s.critHitPrefab, "position, scale critHitScale", true));
                    break;
            }
        }
    }
}

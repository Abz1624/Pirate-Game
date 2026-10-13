using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (13 Oct, owner: every weapon visual as an editable prefab): for each weapon, every visual it draws and
    /// what the presenters drew it from before the weapon prefabs (the juice settings' shared prefabs, the art team's
    /// models, the coloured stand-ins and the lines built in code), resolved exactly as the presenters resolve them with
    /// no <see cref="WeaponVisualCatalog"/>. The prefab builder (HackathonWeaponPrefabs) copies these into each weapon's
    /// folder, so the copies look the same; the tests and plan/hackathon/WEAPON-PREFABS.md list them. Read only.
    /// </summary>
    public static class WeaponVisualSources
    {
        /// <summary>How a weapon prefab is made: a copy of an asset, or built the way the code built it.</summary>
        public enum Build { Copy, MonkeyShot, ParrotShot, CoinSprite, Coin, PulseRing, Ring, Chain, Mace, Parrot, Monkey, Lantern, Anchor }

        public readonly struct Source
        {
            public readonly WeaponVisualSlot Slot;
            /// <summary>The asset copied (Build.Copy), else null.</summary>
            public readonly GameObject Prefab;
            public readonly Build How;
            /// <summary>What the code still sets on it at run time.</summary>
            public readonly string Code;
            /// <summary>Shots only: drawn in its own colours (false: the code tints it with the weapon's projectile colour).</summary>
            public readonly bool OwnColours;

            public Source(WeaponVisualSlot slot, GameObject prefab, Build how, string code, bool ownColours = true)
            {
                Slot = slot;
                Prefab = prefab;
                How = how;
                Code = code;
                OwnColours = ownColours;
            }
        }

        const string ReticleChild = "Visuals/WaterReticle";

        /// <summary>Every visual of <paramref name="w"/> and its pre-catalog source (empty without settings).</summary>
        public static List<Source> For(WeaponConfig w, DemoJuiceSettings s)
        {
            var list = new List<Source>();
            if (w == null || s == null) return list;
            void Copy(WeaponVisualSlot slot, GameObject prefab, string code, bool own = true)
            {
                if (prefab != null) list.Add(new Source(slot, prefab, Build.Copy, code, own));
            }
            void Made(WeaponVisualSlot slot, Build how, string code, bool own = true) => list.Add(new Source(slot, null, how, code, own));

            switch (w.behavior)
            {
                case ConfigEnums.WeaponBehavior.Projectile:
                case ConfigEnums.WeaponBehavior.Fan:
                    Shot(w, s, list);
                    Copy(WeaponVisualSlot.Muzzle, MuzzlePrefab(w, s), w.behavior == ConfigEnums.WeaponBehavior.Fan
                        ? "position 1.1 m out on the firing side, facing out; scale shootingVfxScale x 1.15 x the juice binding's muzzle scale; particles restarted; 0.4 s (a gun prefab shows only its CannonSmoke child)"
                        : "position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child)");
                    if (w.lane.enabled && w.lane.beam)
                        Copy(WeaponVisualSlot.Beam, s.laserBeamPrefab, "both end points (muzzle to the weapon's range, 0.32 m up) and width (beam half-width x 2.4, 0.12-1.4 m); shown laserBeamDuration (0.22 s)");
                    Copy(WeaponVisualSlot.WaterSplash, s.waterImpactSplashPrefab, Splash);
                    break;

                case ConfigEnums.WeaponBehavior.Orbit:
                    if (w.visual.orbitModel == null)
                        Copy(WeaponVisualSlot.Orbit, s.cutlassPrefab, "position (each blade where the simulation puts it), lying flat along the ship's heading; scale cutlassScale; its Blade child and particles shown, its root mesh hidden; particle colours tinted gold every frame");
                    else
                    {
                        var (prefab, how) = OrbitModel(w, s);
                        if (how == Build.Copy) Copy(WeaponVisualSlot.Orbit, prefab, OrbitCode);
                        else Made(WeaponVisualSlot.Orbit, how, OrbitCode);
                    }
                    break;

                case ConfigEnums.WeaponBehavior.ConeStream:
                    Copy(WeaponVisualSlot.Stream, s.flamethrowerStreamPrefab != null ? s.flamethrowerStreamPrefab : s.flamethrowerPrefab,
                        "its Flame particle system only (else the first): posed stream forwardOffset ahead of the ship, 0.45 m up; scale flamethrowerScale x 1.35; start speed scaled so the flame reaches the cone's range and cone angle = half the arc (from the Size stat and items); looping, emission switched on while streaming");
                    Made(WeaponVisualSlot.PatchRing, Build.Ring, "Hellfire Nozzle patches: the outline (radius) and a ring shrinking as the patch burns out; orange, widths 0.3 and 0.45 (x the tuning's width)");
                    break;

                case ConfigEnums.WeaponBehavior.Mine:
                    Copy(WeaponVisualSlot.Mine, s.mineBarrelPrefab, "position (where the keg drops), scale mineBarrelScale; physics removed; an unblown keg sinks 1.4 m and tilts as it times out");
                    Copy(WeaponVisualSlot.Blast, s.powderKegExplosionPrefab != null ? s.powderKegExplosionPrefab : s.weaponExplosionPrefab,
                        "position (0.45 m up), only its 'Impact small', 'Ring' and 'Smoke' children; scale max(1.15, powderKegExplosionScale x 2.35 x binding x blast radius / 2.8 (0.9-1.35)); particles restarted; 1.5 s");
                    break;

                case ConfigEnums.WeaponBehavior.AreaStrike:
                    if (w.visual.placeholderLook == WeaponPlaceholderLook.Doubloons)
                    {
                        if (CoinSpriteMaterial(s) != null)
                            Made(WeaponVisualSlot.Coin, Build.CoinSprite, CoinCode + "; faces the camera, its width following the spin; size 1.1");
                        else if (s.goldPickupPrefab != null)
                            Copy(WeaponVisualSlot.Coin, s.goldPickupPrefab, CoinCode + "; scale goldFlyingCoinScale; physics removed");
                        else Made(WeaponVisualSlot.Coin, Build.Coin, CoinCode);
                        Copy(WeaponVisualSlot.WaterSplash, s.waterImpactSplashPrefab, Splash);
                    }
                    else
                        Copy(WeaponVisualSlot.Strike, s.lightningStrikePrefab, "position on the water, scale lightningStrikeScale x strike radius / 3.5 (0.85-1.35); particles restarted; 1.15 s");
                    break;

                case ConfigEnums.WeaponBehavior.Aura:
                    Made(WeaponVisualSlot.PulseRing, Build.PulseRing, PulseCode);
                    break;

                case ConfigEnums.WeaponBehavior.PredictedArea:
                    if (w.predictedArea.telegraphMatchesImpact)
                        Made(WeaponVisualSlot.LandingRing, Build.Ring, "the outline (the shell's radius) and a ring filling over the shell's delay; for a whirlpool or ink slick the same over its seconds; orange (alpha rising), widths 0.3 and 0.45 (x the tuning's width)");
                    if (w.visual.pulseImpact) Made(WeaponVisualSlot.PulseRing, Build.PulseRing, PulseCode);
                    else Copy(WeaponVisualSlot.Impact, MortarBurst(s), "4 bursts a shell spread inside the ring (no random draw); scale weaponExplosionScale x mortarExplosionScaleMult x radius / 11 (0.18-0.34); particles replayed; 1.05 s; also the whirlpool's implosion");
                    var art = s.WeaponArt(w.weaponId);
                    if (art != null && art.lobModel != null)
                        Copy(WeaponVisualSlot.Lob, art.lobModel, "thrown from the ship's rail (1.6 m up) to each shell's ring, landing as the shell lands; scale lobScale (juice settings), arc lobArcHeight m; trails cleared");
                    Copy(WeaponVisualSlot.WaterSplash, s.waterImpactSplashPrefab, Splash);
                    break;

                case ConfigEnums.WeaponBehavior.Summon:
                    var (model, made) = SummonModel(w, s);
                    if (made == Build.Copy) Copy(WeaponVisualSlot.Summon, model, SummonCode);
                    else Made(WeaponVisualSlot.Summon, made, SummonCode);
                    if (w.summon.shotSpeed > 0f) Shot(w, s, list);
                    Copy(WeaponVisualSlot.WaterSplash, s.waterImpactSplashPrefab, Splash);
                    break;

                case ConfigEnums.WeaponBehavior.ChainedMace:
                    if (s.maceArtModel != null)
                        Copy(WeaponVisualSlot.Mace, s.maceArtModel, "position (the simulation's mace), flukes first away from the ship, rolling 420 deg/s; scale 1 (1.25 without a ChainAttach child); physics removed; its ChainAttach child is where the chain meets it");
                    else Made(WeaponVisualSlot.Mace, Build.Mace, "position (the simulation's mace), spinning; scale 1.25");
                    Copy(WeaponVisualSlot.ChainLink, s.maceChainLinkModel, "only its mesh and material (GPU instanced): one link every maceChainLinkSpacing m along the sagging chain, each turned 90 deg from the last, sized spacing x 1.3 (x the tuning's scale)");
                    Made(WeaponVisualSlot.Chain, Build.Chain, "its 12 points (the ship's rail to the anchor, sagging up to 0.45 m) and texture tiling (one tile per 0.35 m); hidden while chain links are drawn");
                    Copy(WeaponVisualSlot.WaterSplash, s.waterImpactSplashPrefab, Splash);
                    break;
            }
            Hits(w, s, list);
            return list;
        }

        const string Splash = "position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s";
        const string OrbitCode = "position (each blade where the simulation puts it), spinning 540 deg/s about Y; scale = config orbitModelScale; physics removed";
        const string SummonCode = "position and facing (the simulation's summon), 0.2 m up; scale = config summonModelScale; physics removed";
        const string CoinCode = "3 a strike thrown from the ship (1.1 m up) to the enemy in a fast low arc (0.4 m + 3% of the distance), spinning; they glance off and fall; 0.45 s after landing";
        const string PulseCode = "its 48 points (a circle sweeping out to the pulse radius, 0.2 m up), colour (the weapon's aimOverlayColor, fading), width multiplier (shrinking); a second ring at 80% follows";

        static void Shot(WeaponConfig w, DemoJuiceSettings s, List<Source> list)
        {
            var art = s.WeaponArt(w.weaponId);
            const string code = "position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted";
            if (art != null && art.projectileModel != null)
            {
                list.Add(new Source(WeaponVisualSlot.Projectile, art.projectileModel, Build.Copy, code + " x projectileScale (juice settings art)", true));
                return;
            }
            if (w.projectileVisual != null && w.projectileVisual.prefab != null)
            {
                list.Add(new Source(WeaponVisualSlot.Projectile, w.projectileVisual.prefab, Build.Copy, code + "; every material tinted with the weapon's projectileColor", false));
                return;
            }
            var look = w.visual.placeholderLook;
            if (look == WeaponPlaceholderLook.MonkeyGunners) { list.Add(new Source(WeaponVisualSlot.Projectile, null, Build.MonkeyShot, code, true)); return; }
            if (look == WeaponPlaceholderLook.Parrots) { list.Add(new Source(WeaponVisualSlot.Projectile, null, Build.ParrotShot, code, true)); return; }
            if (s.projectilePrefab != null)
                list.Add(new Source(WeaponVisualSlot.Projectile, s.projectilePrefab, Build.Copy, code + "; every material tinted with the weapon's projectileColor", false));
        }

        static void Hits(WeaponConfig w, DemoJuiceSettings s, List<Source> list)
        {
            switch (w.hitEffect)
            {
                case ConfigEnums.WeaponHitEffect.CannonImpact:
                    var impact = s.cannonImpactPrefab != null ? s.cannonImpactPrefab : s.weaponExplosionPrefab;
                    if (impact != null) list.Add(new Source(WeaponVisualSlot.Hit, impact, Build.Copy, "position (0.45 m up), scale cannonImpactScale (x1.12 on a crit); 0.55 s"));
                    break;
                case ConfigEnums.WeaponHitEffect.None:
                    if (s.enemyHitPrefab != null) list.Add(new Source(WeaponVisualSlot.Hit, s.enemyHitPrefab, Build.Copy, "position (0.35 m up), scale enemyHitScale x 0.8; its particles' own length"));
                    break;
                default:
                    if (s.enemyHitPrefab != null) list.Add(new Source(WeaponVisualSlot.Hit, s.enemyHitPrefab, Build.Copy, "position (0.35 m up), scale enemyHitScale; its particles' own length"));
                    if (s.critHitPrefab != null) list.Add(new Source(WeaponVisualSlot.CritHit, s.critHitPrefab, Build.Copy, "position (0.35 m up), scale critHitScale; its particles' own length"));
                    break;
            }
        }

        /// <summary>The muzzle prefab as <see cref="WeaponVfxPresenter"/> resolves it before the catalog.</summary>
        public static GameObject MuzzlePrefab(WeaponConfig w, DemoJuiceSettings s)
        {
            if (s.TryGetWeaponVfx(w.weaponId, out var binding) && binding.muzzleVfxPrefab != null) return binding.muzzleVfxPrefab;
            if (s.cannonMuzzleVfxPrefab != null && w.visual.cannonMuzzle) return s.cannonMuzzleVfxPrefab;
            return s.shootingVfxPrefab;
        }

        static (GameObject, Build) OrbitModel(WeaponConfig w, DemoJuiceSettings s)
        {
            var model = w.visual.orbitModel;
            if (!WeaponPlaceholderModels.IsCannonStandIn(model)) return (model, Build.Copy);
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Lanterns)
                return s.lanternArtModel != null ? (s.lanternArtModel, Build.Copy) : (null, Build.Lantern);
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Anchors) return (null, Build.Anchor);
            return (model, Build.Copy);
        }

        static (GameObject, Build) SummonModel(WeaponConfig w, DemoJuiceSettings s)
        {
            var model = w.visual.summonModel;
            if (model == null) return (null, Build.Copy);
            if (!WeaponPlaceholderModels.IsCannonStandIn(model)) return (model, Build.Copy);
            if (w.visual.placeholderLook == WeaponPlaceholderLook.Parrots)
                return s.parrotArtModel != null ? (s.parrotArtModel, Build.Copy) : (null, Build.Parrot);
            if (w.visual.placeholderLook == WeaponPlaceholderLook.MonkeyGunners)
                return s.monkeyArtModel != null ? (s.monkeyArtModel, Build.Copy) : (null, Build.Monkey);
            return (model, Build.Copy);
        }

        static GameObject MortarBurst(DemoJuiceSettings s) => s.mortarExplosionPrefab != null ? s.mortarExplosionPrefab : s.weaponExplosionPrefab;

        /// <summary>The material of the gold barrel's coin particles (the one named for coins), as the presenter finds it.</summary>
        public static Material CoinSpriteMaterial(DemoJuiceSettings s)
        {
            var barrel = s != null ? s.goldBarrelPickupPrefab : null;
            if (barrel == null) return null;
            foreach (var r in barrel.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0) return m;
            }
            return null;
        }
    }
}

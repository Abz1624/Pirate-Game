using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.Presentation
{
    /// <summary>
    /// The V4 presentation root, one per run (M4): owns the view pool built from the pool catalog. Installed on
    /// <see cref="V4Game.RunStarted"/> on its own object, like the console (not /Systems, which the legacy enemy view
    /// pool deactivates around new views, R-72). Presenters that render through the pool arrive from M5 on; the HUD,
    /// minimap, beacon guide, results screen and spawn warnings from M6 (they read <see cref="Views.IRunViews"/> and the
    /// event stream only).
    /// </summary>
    public sealed class V4PresentationRoot : MonoBehaviour
    {
        public PoolService Pools { get; private set; }
        public Weapons.ProjectileViewPresenter Projectiles { get; private set; }
        public V4Game Game { get; private set; }
        public RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter Hud { get; private set; }
        public RoguePirates.V4.Presentation.Hud.SaltWindHudPresenter SaltWindHud { get; private set; }
        public RoguePirates.V4.Presentation.Hud.MinimapPresenter Minimap { get; private set; }
        public RoguePirates.V4.Presentation.Hud.CaptainActivePresenter CaptainActive { get; private set; }
        public RoguePirates.V4.Presentation.Hud.ItemRewardPresenter ItemReward { get; private set; }
        public RoguePirates.V4.Presentation.Hud.CaptainActiveEffectPresenter CaptainActiveEffect { get; private set; }
        /// <summary>V5: the Captain ability's round touch button (touch devices only).</summary>
        public RoguePirates.V4.Presentation.Hud.CaptainAbilityButtonPresenter CaptainAbilityButton { get; private set; }
        public RoguePirates.V4.Presentation.Hud.BeaconGuidePresenter BeaconGuide { get; private set; }
        public RoguePirates.V4.Presentation.Hud.ResultsPresenter Results { get; private set; }
        public RoguePirates.V4.Presentation.Hud.SpawnWarningPresenter SpawnWarnings { get; private set; }
        /// <summary>M13: barks and tutorial prompts.</summary>
        public RoguePirates.V4.Presentation.Hud.NarrativePresenter Narrative { get; private set; }
        public Weapons.WeaponVfxPresenter WeaponEffects { get; private set; }
        public Weapons.WeaponAimOverlay AimOverlay { get; private set; }
        public Stage.InteractionPromptPresenter InteractionPrompt { get; private set; }
        public Stage.BossTelegraphPresenter BossTelegraph { get; private set; }
        /// <summary>M14.12: chest and landmark markers (were made by LegacyPoiMarkers at run start).</summary>
        public Stage.PoiMarkerPresenter PoiMarkers { get; private set; }
        /// <summary>M14: toasts, combat feedback (hit/kill effects, flashes, level-up burst), damage numbers, camera shake.</summary>
        public RoguePirates.V4.Presentation.Hud.ToastPresenter Toasts { get; private set; }
        public Effects.CombatFeedbackPresenter CombatFeedback { get; private set; }
        public Effects.DamageNumberPresenter DamageNumbers { get; private set; }
        public Effects.CameraShakePresenter CameraShake { get; private set; }
        /// <summary>M14.9b: world views (were V3's PickupViewPool, ProjectileViewPool, placeholder powerups, the kept hazard
        /// ring view, PlayerHealthBarBinder, V3SaltwindStormRingVisual and V3PlayBoundsWallVisual).</summary>
        public Effects.HazardRingPresenter HazardRings { get; private set; }
        /// <summary>M14.11: the level-up cards (was V3's LevelUpModal).</summary>
        public RoguePirates.V4.Presentation.Hud.LevelUpPresenter LevelUp { get; private set; }
        /// <summary>M14.10: enemy models and health bars on the view pool (intended change 2).</summary>
        public Effects.EnemyViewPresenter EnemyViews { get; private set; }
        public Effects.HostileShotPresenter HostileShots { get; private set; }
        public Effects.PickupPresenter Pickups { get; private set; }
        public Effects.PowerupPresenter Powerups { get; private set; }
        public World.ShipHealthBarPresenter ShipHealthBar { get; private set; }
        public World.ShipWakePresenter ShipWake { get; private set; }
        public World.StormRingPresenter StormRing { get; private set; }
        public World.BoundsWallPresenter BoundsWalls { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRunStarted;
            V4Game.RunStarted += OnRunStarted;
        }

        static void OnRunStarted(V4Game game)
        {
            if (game == null) return;
            var root = FindFirstObjectByType<V4PresentationRoot>();
            if (root == null) root = new GameObject("V4Presentation").AddComponent<V4PresentationRoot>();
            root.Bind(game);
        }

        public void Bind(V4Game game)
        {
            if (Game == game && Pools != null) return;
            Game = game;
            DisposeWorldTemplates();
            Pools?.Dispose();
            var poolRoot = transform.Find("Pools");
            if (poolRoot == null)
            {
                poolRoot = new GameObject("Pools").transform;
                poolRoot.SetParent(transform, false);
            }
            Pools = new PoolService(poolRoot);
            foreach (var definition in PoolCatalog.Build(game.Config, game.IsModuleLab)) Pools.Register(definition);
            Pools.PrewarmAll();
            // M5: player projectile views (the weapon module looks them up in the run context).
            Projectiles = new Weapons.ProjectileViewPresenter(Pools, game.Config, Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings"));
            game.Context?.Replace<RoguePirates.V4.Weapons.IProjectileViews>(Projectiles);
            // M5 playtest fix: the flamethrower stream (the existing stream effect at its authored emission).
            var flame = GetComponent<Weapons.FlameStreamPresenter>();
            if (flame == null) flame = gameObject.AddComponent<Weapons.FlameStreamPresenter>();
            if (game.Context != null && game.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons))
                flame.Bind(weapons, Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings"),
                    game.Context.TryGet<Views.IRunViews>(out var shipViews) ? shipViews.Ship : null);
            // M6: HUD presenters, weapon effects and the aim overlay.
            if (game.Context != null && game.Context.TryGet<Views.IRunViews>(out var views))
            {
                RoguePirates.V4.Presentation.Hud.RunCanvasSetup.Apply(); // M14.14: the run canvas groundwork (was the run shell's)
                InstallToasts(views); // first: the results screen and the interaction prompt are layered around it
                InstallFeedback(game, views);
                InstallHud(views, game.Events);
                InstallWeaponEffects(game, views);
                InstallWorld(game, views);
                InstallLevelUp(game);
                InstallVfxProfiles(views);
                // Demo: the chosen Captain's sail and flag on the player ship.
                if (views.Ship != null && views.Captain != null)
                    RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Load()?.ApplyShipLook(views.Ship.Anchor, views.Captain.CaptainId);
            }
            if (game.Context != null) InstallStage(game);
            if (game.Context != null) InstallNarrative(game);
            // V5 M7: once every panel exists, the run canvas's content goes inside the safe area and the UI scale applies.
            var run = GameObject.Find("RunCanvas");
            if (run != null && run.TryGetComponent<Canvas>(out var runCanvasForSafeArea)) RoguePirates.V4.Foundation.UI.SafeAreaFitter.Wrap(runCanvasForSafeArea);
            RoguePirates.V4.Foundation.UI.UiScale.ApplyAll();
        }

        /// <summary>Demo (30 Sep 2026): the artists' VFX profiles, handed to the presenters that play each moment.</summary>
        public Effects.VfxProfilePlayer VfxProfiles { get; private set; }

        void InstallVfxProfiles(Views.IRunViews views)
        {
            VfxProfiles = GetComponent<Effects.VfxProfilePlayer>();
            if (VfxProfiles == null) VfxProfiles = gameObject.AddComponent<Effects.VfxProfilePlayer>();
            VfxProfiles.Bind(Pools, RoguePirates.V4.Foundation.Data.VfxProfileLibrary.Load(), () => views.Ship != null ? views.Ship.Anchor : null);
            if (Projectiles != null) Projectiles.Profiles = VfxProfiles;
            if (WeaponEffects != null) WeaponEffects.Profiles = VfxProfiles;
            if (CombatFeedback != null) CombatFeedback.Profiles = VfxProfiles;
            if (EnemyViews != null) EnemyViews.Profiles = VfxProfiles;
        }

        /// <summary>M14: the toast presenter on the run canvas (V3ToastHud's place); the run's toast forwarder shows through it.</summary>
        void InstallToasts(Views.IRunViews views)
        {
            var runCanvas = FindRunCanvas();
            if (runCanvas == null) return;
            Toasts = RoguePirates.V4.Presentation.Hud.ToastPresenter.Create(runCanvas.transform);
            (views.Toasts as Views.ToastForwarder)?.SetTarget(Toasts);
        }

        /// <summary>M14: combat feedback from the event stream and the enemy simulation's hit feedback (was V3's
        /// CombatVfxController, JuiceVfxBroker, FloatingDamageNumbers and CameraShakeService).</summary>
        void InstallFeedback(V4Game game, Views.IRunViews views)
        {
            CameraShake = GetComponent<Effects.CameraShakePresenter>();
            if (CameraShake == null) CameraShake = gameObject.AddComponent<Effects.CameraShakePresenter>();
            System.Func<Transform> ship = () => views.Ship != null ? views.Ship.Anchor : null;
            DamageNumbers = GetComponent<Effects.DamageNumberPresenter>();
            if (DamageNumbers == null) DamageNumbers = gameObject.AddComponent<Effects.DamageNumberPresenter>();
            DamageNumbers.Bind(Pools, () => { var t = ship(); return t != null ? t.position : (Vector3?)null; });
            CombatFeedback = GetComponent<Effects.CombatFeedbackPresenter>();
            if (CombatFeedback == null) CombatFeedback = gameObject.AddComponent<Effects.CombatFeedbackPresenter>();
            CombatFeedback.Bind(Pools, game.Events, game.Config, Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings"), ship, CameraShake, DamageNumbers);
            game.Context.Replace<RoguePirates.V4.Enemies.IEnemyHitFeedback>(CombatFeedback);
        }

        /// <summary>
        /// M14.9b: the world views: hazard rings, enemy shots, pickups, floor powerups, the ship health bar, the storm ring
        /// and the edge walls. Blasts and splashes are the weapon presenter's (the enemy world's blast sink points at it).
        /// </summary>
        void InstallWorld(V4Game game, Views.IRunViews views)
        {
            var juice = Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings");
            var run = views.Run;
            EnemyViews = new Effects.EnemyViewPresenter(Pools, juice);
            game.Context.Replace<RoguePirates.V4.Enemies.IEnemyViewSink>(EnemyViews);
            HazardRings = Child<Effects.HazardRingPresenter>("V4HazardRings");
            HazardRings.Bind(game.Context.TryGet<RoguePirates.V4.Enemies.IHazardView>(out var hazards) ? hazards : null,
                game.Context.TryGet<RoguePirates.V4.Enemies.IBurningPatchView>(out var patches) ? patches : null,
                () => run != null && run.IsPaused);
            if (WeaponEffects != null)
            {
                WeaponEffects.HazardRings = HazardRings;
                game.Context.Replace<RoguePirates.V4.Enemies.IEnemyEffects>(WeaponEffects);
            }
            var splash = WeaponEffects;
            HostileShots = new Effects.HostileShotPresenter(Pools, juice, position => { if (splash != null) splash.WaterSplash(position); });
            game.Context.Replace<RoguePirates.V4.Enemies.IHostileShotViews>(HostileShots);
            Pickups = new Effects.PickupPresenter(Pools, juice);
            game.Context.Replace<RoguePirates.V4.Progression.IPickupViews>(Pickups);
            Powerups = new Effects.PowerupPresenter(Pools);
            game.Context.Replace<RoguePirates.V4.Progression.IPowerupViews>(Powerups);
            ShipHealthBar = Child<World.ShipHealthBarPresenter>("V4ShipHealthBar");
            ShipHealthBar.Bind(Pools, views.Ship, juice);
            ShipWake = Child<World.ShipWakePresenter>("V4ShipWake");
            ShipWake.Bind(views.Ship);
            var world = game.Context.TryGet<Views.IWorldView>(out var w) ? w : null;
            StormRing = Child<World.StormRingPresenter>("V4StormRing");
            StormRing.Bind(world);
            BoundsWalls = Child<World.BoundsWallPresenter>("V4BoundsWalls");
            BoundsWalls.Bind(world, views.Ship);
        }

        /// <summary>M14.11: the level-up cards on the run canvas; the progression module and the tools reach them through the context.</summary>
        void InstallLevelUp(V4Game game)
        {
            LevelUp = RoguePirates.V4.Presentation.Hud.LevelUpPresenter.Create(FindRunCanvas(), game.Config);
            if (LevelUp == null) return;
            RoguePirates.V4.Progression.OfferGenerator.WeaponCardsOnly = RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Hides(s => s.weaponCardsOnly);
            RoguePirates.V4.Progression.OfferGenerator.ExcludedWeapons.Clear();
            var demo = RoguePirates.V4.Foundation.Core.DemoMode.Off ? null : RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Load();
            if (demo != null && demo.excludedWeapons != null)
                foreach (var id in demo.excludedWeapons) if (!string.IsNullOrEmpty(id)) RoguePirates.V4.Progression.OfferGenerator.ExcludedWeapons.Add(id);
            if (SaltWindHud != null && SaltWindHud.UpgradeSelection != null)
                LevelUp.UseArtCards(SaltWindHud.UpgradeSelection, RoguePirates.V4.Presentation.Hud.IconCatalog.Load());
            game.Context.Replace<RoguePirates.V4.Progression.IOfferPresenter>(LevelUp);
            game.Context.Replace<RoguePirates.V4.Progression.IOfferChoice>(LevelUp);
        }

        /// <summary>A presenter on its own child object (created once, kept across runs of this root).</summary>
        T Child<T>(string name) where T : Component
        {
            var child = transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(transform, false);
            }
            var component = child.GetComponent<T>();
            return component != null ? component : child.gameObject.AddComponent<T>();
        }

        void DisposeWorldTemplates()
        {
            EnemyViews?.Dispose();
            HostileShots?.Dispose();
            Pickups?.Dispose();
            Powerups?.Dispose();
            EnemyViews = null;
            HostileShots = null;
            Pickups = null;
            Powerups = null;
        }

        /// <summary>M13: barks and tutorial prompts, on the authored HUD canvas (or the run canvas).</summary>
        void InstallNarrative(V4Game game)
        {
            if (!game.Context.TryGet<RoguePirates.V4.Narrative.INarrativeView>(out var narrative)) return;
            if (RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Hides(s => s.hideNarrative)) return; // demo: no barks or tutorial prompts
            // The run canvas (above the authored HUD) and its last child, so a prompt also shows over the level-up cards.
            var run = FindRunCanvas();
            var parent = run != null ? run.transform : RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter.FindAuthoredCanvas()?.transform;
            if (parent == null) return;
            Narrative = RoguePirates.V4.Presentation.Hud.NarrativePresenter.Create(parent);
            Narrative.transform.SetAsLastSibling();
            Narrative.Bind(narrative);
        }

        /// <summary>
        /// M7: the interaction prompt (drawn where V3's was: just above the toast view on the run canvas) and the Barnacle
        /// King's marked telegraph.
        /// </summary>
        void InstallStage(V4Game game)
        {
            var runCanvas = FindRunCanvas();
            if (runCanvas != null && game.Context.TryGet<RoguePirates.V4.Stage.IInteractionPromptView>(out var prompt))
            {
                InteractionPrompt = Stage.InteractionPromptPresenter.Create(runCanvas.transform);
                game.Context.TryGet<RoguePirates.V4.Stage.IInteractionTouch>(out var touch);
                InteractionPrompt.Bind(prompt, touch);
                PlaceAboveToast(runCanvas.transform, InteractionPrompt.transform);
            }
            if (game.Context.TryGet<RoguePirates.V4.Stage.IBossView>(out var boss))
            {
                BossTelegraph = GetComponent<Stage.BossTelegraphPresenter>();
                if (BossTelegraph == null) BossTelegraph = gameObject.AddComponent<Stage.BossTelegraphPresenter>();
                var config = game.Config;
                BossTelegraph.Bind(boss, () => config != null ? config.Single<RoguePirates.V4.Config.BossConfig>() : null);
            }
            if (game.Context.TryGet<RoguePirates.V4.Stage.IPoiMarkerView>(out var markers))
            {
                PoiMarkers = Child<Stage.PoiMarkerPresenter>("V4PoiMarkers");
                PoiMarkers.Bind(markers);
            }
        }

        static void PlaceAboveToast(Transform canvas, Transform prompt)
        {
            for (var i = 0; i < canvas.childCount; i++)
                if (canvas.GetChild(i).name == RoguePirates.V4.Presentation.Hud.ToastPresenter.ObjectName)
                {
                    prompt.SetSiblingIndex(Mathf.Min(i + 1, canvas.childCount - 1));
                    return;
                }
        }

        /// <summary>
        /// The weapon effects presenter replaces V3's juice controller visuals, the aim overlay V3's targeting visualizer
        /// (both M6). The weapon module reaches the presenter through the run context (WeaponEffectsForwarder).
        /// </summary>
        void InstallWeaponEffects(V4Game game, Views.IRunViews views)
        {
            if (!game.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons)) return;
            AimOverlay = GetComponent<Weapons.WeaponAimOverlay>();
            if (AimOverlay == null) AimOverlay = gameObject.AddComponent<Weapons.WeaponAimOverlay>();
            var run = views.Run;
            AimOverlay.Bind(weapons, views.Ship, views.WeaponAim, () => run != null && run.IsPaused, views.Toasts);
            AimOverlay.TogglePressed = () => OverlayTogglePressed(run);

            WeaponEffects = GetComponent<Weapons.WeaponVfxPresenter>();
            if (WeaponEffects == null) WeaponEffects = gameObject.AddComponent<Weapons.WeaponVfxPresenter>();
            var summon = Weapons.WeaponVfxPresenter.FindSummonModel(game.Config);
            var config = game.Config;
            RoguePirates.V4.Config.WeaponRulesConfig rulesCache = null;
            WeaponEffects.Bind(Pools, weapons, views.Ship, Resources.Load<RoguePirates.V4.Foundation.Data.DemoJuiceSettings>("DemoJuiceSettings"),
                summon.model, AimOverlay, null,
                () => rulesCache != null ? rulesCache : (rulesCache = config != null ? System.Linq.Enumerable.FirstOrDefault(config.All<RoguePirates.V4.Config.WeaponRulesConfig>()) : null),
                summon.scale);
            if (config != null) WeaponEffects.PrewarmModels(config.All<RoguePirates.V4.Config.WeaponConfig>()); // V5 M4
            game.Context.Replace<RoguePirates.V4.Weapons.IWeaponEffects>(WeaponEffects);
        }

        /// <summary>V3 WeaponTargetingVisualizer's toggle: O or V, or backslash outside the weapon-enemy playtest; not after the run ended.</summary>
        static bool OverlayTogglePressed(Views.IRunStatusView run)
        {
            if (run != null && run.IsRunEnded) return false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.oKey.wasPressedThisFrame || keyboard.vKey.wasPressedThisFrame) return true;
            return (run == null || !run.WeaponPlaytestActive) && keyboard.backslashKey.wasPressedThisFrame;
        }

        /// <summary>
        /// The minimap, beacon guide, results screen and spawn warnings go on the run canvas (where V3 put its minimap,
        /// guide and top results screen); the HUD presenter goes on the authored HUD canvas. Scenes without these
        /// canvases (a scene without one of them) get what fits.
        /// </summary>
        void InstallHud(Views.IRunViews views, Events.ICombatEventReader events)
        {
            var runCanvas = FindRunCanvas();
            if (runCanvas != null)
            {
                Minimap = RoguePirates.V4.Presentation.Hud.MinimapPresenter.Create(runCanvas.transform);
                Minimap.Bind(views);
                if (!RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Hides(s => s.hideBeaconGuide))
                {
                    BeaconGuide = RoguePirates.V4.Presentation.Hud.BeaconGuidePresenter.Create(runCanvas.transform);
                    BeaconGuide.Bind(views);
                }
            }

            var authored = RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter.FindAuthoredCanvas();
            if (authored != null)
            {
                Hud = authored.GetComponent<RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter>();
                if (Hud == null) Hud = authored.gameObject.AddComponent<RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter>();
                Hud.Bind(views, Minimap, RoguePirates.V4.Presentation.Hud.IconCatalog.Load());
                // Demo (30 Sep 2026): the art team's HUD over the authored canvas, when its settings are present.
                SaltWindHud = RoguePirates.V4.Presentation.Hud.SaltWindHudPresenter.Install(authored, Hud, views, RoguePirates.V4.Presentation.Hud.IconCatalog.Load(), Minimap);
                if (SaltWindHud != null && Minimap != null) Minimap.FollowTarget = SaltWindHud.ArtMinimap;
                // M9 playtest: the Captain's active and its cooldown, beside the boost meter.
                // Playtest (3 Oct): an item won from a chest is shown with its icon, name and effect.
                ItemReward = RoguePirates.V4.Presentation.Hud.ItemRewardPresenter.Create(authored.transform);
                ItemReward.Bind(views, RoguePirates.V4.Presentation.Hud.IconCatalog.Load(), ItemDescription);
                if (!RoguePirates.V4.Presentation.Hud.SaltWindHudSettings.Hides(s => s.hideCaptainActive))
                {
                    CaptainActive = RoguePirates.V4.Presentation.Hud.CaptainActivePresenter.Create(authored.transform);
                    CaptainActive.Bind(views);
                    CaptainActiveEffect = RoguePirates.V4.Presentation.Hud.CaptainActiveEffectPresenter.Create(authored.transform);
                    CaptainActiveEffect.Bind(views);
                    // V5: the ability's round touch button (touch devices only; it hides itself elsewhere).
                    CaptainAbilityButton = RoguePirates.V4.Presentation.Hud.CaptainAbilityButtonPresenter.Create(authored.transform);
                    CaptainAbilityButton.Bind(views);
                }
                // V5 M7: touch boost and the pause button.
                TouchControls = RoguePirates.V4.Presentation.Hud.TouchControlsPresenter.Create(authored.transform, CaptainAbilityButton);
            }

            if (runCanvas == null) return;
            Results = RoguePirates.V4.Presentation.Hud.ResultsPresenter.Create(runCanvas.transform);
            Results.Bind(views, events, Hud);
            PlaceBelowPrompts(runCanvas.transform, Results.transform);
            SpawnWarnings = RoguePirates.V4.Presentation.Hud.SpawnWarningPresenter.Create(runCanvas.transform);
            SpawnWarnings.Bind(views, events, Minimap);
            // V5 M7: the player's pause and settings, every panel inside the safe area, the UI scale, the quality tier.
            PauseMenu = RoguePirates.V4.Presentation.Hud.PauseMenuPresenter.Create(runCanvas.transform);
            PauseMenu.Bind(views);
            RoguePirates.V4.Foundation.Platform.AutoQuality.Begin(gameObject);
        }

        public RoguePirates.V4.Presentation.Hud.TouchControlsPresenter TouchControls { get; private set; }
        public RoguePirates.V4.Presentation.Hud.PauseMenuPresenter PauseMenu { get; private set; }

        /// <summary>
        /// V3's results panel was built before the toast and interaction prompt views, so toasts (unlock goal messages)
        /// drew over it. The presenter is created later, so move it just below those views (found by their object names).
        /// </summary>
        static void PlaceBelowPrompts(Transform canvas, Transform results)
        {
            var index = -1;
            for (var i = 0; i < canvas.childCount; i++)
            {
                var name = canvas.GetChild(i).name;
                if (name == RoguePirates.V4.Presentation.Hud.ToastPresenter.ObjectName || name == "PLACEHOLDER_v3_ui_interaction_prompt") { index = i; break; }
            }
            if (index >= 0 && results.GetSiblingIndex() > index) results.SetSiblingIndex(index);
        }

        static Canvas FindRunCanvas()
        {
            var go = GameObject.Find("RunCanvas");
            return go != null && go.TryGetComponent(out Canvas canvas) ? canvas : null;
        }

        void OnDestroy()
        {
            RoguePirates.V4.Progression.OfferGenerator.WeaponCardsOnly = false; // the demo switches never outlive the run
            RoguePirates.V4.Progression.OfferGenerator.ExcludedWeapons.Clear();
            if (WeaponEffects != null) WeaponEffects.Clear();
            DisposeWorldTemplates();
            Pools?.Dispose();
            Pools = null;
        }

        string ItemDescription(string itemId)
        {
            var config = Game != null ? Game.Config : null;
            if (config == null) return null;
            foreach (var item in config.All<RoguePirates.V4.Config.ItemConfig>())
                if (item != null && item.itemId == itemId) return item.description;
            return null;
        }
    }
}

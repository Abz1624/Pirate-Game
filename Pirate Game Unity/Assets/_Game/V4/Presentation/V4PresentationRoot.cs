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
        public RoguePirates.V4.Presentation.Hud.MinimapPresenter Minimap { get; private set; }
        public RoguePirates.V4.Presentation.Hud.BeaconGuidePresenter BeaconGuide { get; private set; }
        public RoguePirates.V4.Presentation.Hud.ResultsPresenter Results { get; private set; }
        public RoguePirates.V4.Presentation.Hud.SpawnWarningPresenter SpawnWarnings { get; private set; }
        public Weapons.WeaponVfxPresenter WeaponEffects { get; private set; }
        public Weapons.WeaponAimOverlay AimOverlay { get; private set; }

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
            Projectiles = new Weapons.ProjectileViewPresenter(Pools, game.Config, Resources.Load<HighSeas.Data.DemoJuiceSettings>("DemoJuiceSettings"));
            game.Context?.Replace<RoguePirates.V4.Weapons.IProjectileViews>(Projectiles);
            // M5 playtest fix: the flamethrower stream (the existing stream effect at its authored emission).
            var flame = GetComponent<Weapons.FlameStreamPresenter>();
            if (flame == null) flame = gameObject.AddComponent<Weapons.FlameStreamPresenter>();
            if (game.Context != null && game.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons))
                flame.Bind(weapons, Resources.Load<HighSeas.Data.DemoJuiceSettings>("DemoJuiceSettings"),
                    game.Context.TryGet<Views.IRunViews>(out var shipViews) ? shipViews.Ship : null);
            // M6: HUD presenters, weapon effects and the aim overlay.
            if (game.Context != null && game.Context.TryGet<Views.IRunViews>(out var views))
            {
                InstallHud(views, game.Events);
                InstallWeaponEffects(game, views);
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
            WeaponEffects.Bind(Pools, weapons, views.Ship, Resources.Load<HighSeas.Data.DemoJuiceSettings>("DemoJuiceSettings"),
                summon.model, AimOverlay, null,
                () => rulesCache != null ? rulesCache : (rulesCache = config != null ? System.Linq.Enumerable.FirstOrDefault(config.All<RoguePirates.V4.Config.WeaponRulesConfig>()) : null),
                summon.scale);
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
                BeaconGuide = RoguePirates.V4.Presentation.Hud.BeaconGuidePresenter.Create(runCanvas.transform);
                BeaconGuide.Bind(views);
            }

            var authored = RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter.FindAuthoredCanvas();
            if (authored != null)
            {
                Hud = authored.GetComponent<RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter>();
                if (Hud == null) Hud = authored.gameObject.AddComponent<RoguePirates.V4.Presentation.Hud.AuthoredHudPresenter>();
                Hud.Bind(views, Minimap, RoguePirates.V4.Presentation.Hud.IconCatalog.Load());
            }

            if (runCanvas == null) return;
            Results = RoguePirates.V4.Presentation.Hud.ResultsPresenter.Create(runCanvas.transform);
            Results.Bind(views, events, Hud);
            PlaceBelowPrompts(runCanvas.transform, Results.transform);
            SpawnWarnings = RoguePirates.V4.Presentation.Hud.SpawnWarningPresenter.Create(runCanvas.transform);
            SpawnWarnings.Bind(views, events, Minimap);
        }

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
                if (name == "PLACEHOLDER_v3_ui_toast_hud" || name == "PLACEHOLDER_v3_ui_interaction_prompt") { index = i; break; }
            }
            if (index >= 0 && results.GetSiblingIndex() > index) results.SetSiblingIndex(index);
        }

        static Canvas FindRunCanvas()
        {
            var go = GameObject.Find("RunCanvas");
            if (go != null && go.TryGetComponent(out Canvas canvas)) return canvas;
            var runHud = FindFirstObjectByType<HighSeas.UI.RunHud>();
            return runHud != null ? runHud.GetComponentInParent<Canvas>() : null;
        }

        void OnDestroy()
        {
            if (WeaponEffects != null) WeaponEffects.Clear();
            Pools?.Dispose();
            Pools = null;
        }
    }
}

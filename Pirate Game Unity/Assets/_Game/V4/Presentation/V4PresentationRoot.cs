using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.Presentation
{
    /// <summary>
    /// The V4 presentation root, one per run (M4): owns the view pool built from the pool catalog. Installed on
    /// <see cref="V4Game.RunStarted"/> on its own object, like the console (not /Systems, which the legacy enemy view
    /// pool deactivates around new views, R-72). Presenters that render through the pool arrive from M5 on.
    /// </summary>
    public sealed class V4PresentationRoot : MonoBehaviour
    {
        public PoolService Pools { get; private set; }
        public Weapons.ProjectileViewPresenter Projectiles { get; private set; }
        public V4Game Game { get; private set; }

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
        }

        void OnDestroy()
        {
            Pools?.Dispose();
            Pools = null;
        }
    }
}

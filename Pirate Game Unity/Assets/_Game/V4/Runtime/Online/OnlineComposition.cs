using RoguePirates.V4.Foundation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// V5 M8z (7 Oct, architecture rules: no singletons of their own): composes the app-lifetime online objects once and
    /// registers them on the app root, which owns them (they live and die with it): the <see cref="OnlineServices"/> (was
    /// the static <see cref="OnlineService"/>'s own state) and the 1v1 <see cref="PvpSession"/> (was a lazy
    /// DontDestroyOnLoad object of its own), a component on the app root's object, given its scene navigation here.
    /// </summary>
    public static class OnlineComposition
    {
        /// <summary>The app root's online services (composed on first use).</summary>
        public static OnlineServices Online
        {
            get
            {
                var root = Root();
                if (!root.TryGet<OnlineServices>(out var online)) root.Register(online = new OnlineServices());
                return online;
            }
        }

        /// <summary>The app root's 1v1 session (composed on first use).</summary>
        public static PvpSession Pvp
        {
            get
            {
                var root = Root();
                if (root.TryGet<PvpSession>(out var session) && session != null) return session;
                session = root.gameObject.AddComponent<PvpSession>();
                session.Bind(Online, new PvpSceneNavigation(
                    () => SceneManager.GetActiveScene().name,
                    scene => SceneManager.LoadScene(scene)));
                root.Register(session);
                return session;
            }
        }

        /// <summary>Whether the app root has composed a 1v1 session (nothing is created).</summary>
        public static bool HasPvp => AppRoot.Instance != null && AppRoot.Instance.TryGet<PvpSession>(out var s) && s != null;

        static AppRoot Root()
        {
            AppRoot.EnsureInitialized();
            return AppRoot.Instance;
        }
    }

    /// <summary>V5 M8z (7 Oct): the scene changes the 1v1 session asks for, given by the composition root (the session
    /// no longer reaches the scene manager itself).</summary>
    public sealed class PvpSceneNavigation
    {
        readonly System.Func<string> _active;
        readonly System.Action<string> _load;

        public PvpSceneNavigation(System.Func<string> active, System.Action<string> load)
        {
            _active = active;
            _load = load;
        }

        public string ActiveScene => _active != null ? _active() : string.Empty;
        public void Load(string scene) => _load?.Invoke(scene);
    }
}

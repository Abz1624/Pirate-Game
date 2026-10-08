using RoguePirates.V4;
using RoguePirates.V4.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2b: the way into a server voyage, only in the V5_NET WebGL client build (the assembly is compiled into
    /// no other game build): a "Server voyage (test)" button on the main menu, and the page's <c>?netvoyage=1</c> (start at
    /// once) or <c>?netvoyage=auto</c> (the scripted voyage, with <c>&amp;seconds=N</c>). The match server comes from the
    /// API's ticket answer; the API from the page's <c>?api=</c> as for every online feature. In the Editor the entry is
    /// off unless <see cref="EditorEnabled"/> is set (a dev session), and the Linux server build never shows it.
    /// </summary>
    public static class NetVoyageEntry
    {
        public const string Caption = "Server voyage (test)";
        /// <summary>The Editor's switch (PlayerPrefs, so a play session can turn it on).</summary>
        public const string EditorPrefsKey = "rp.v5net.voyageEntry";

        public static bool EditorEnabled
        {
            get => PlayerPrefs.GetInt(EditorPrefsKey, 0) == 1;
            set { PlayerPrefs.SetInt(EditorPrefsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Enabled
        {
            get
            {
#if UNITY_SERVER
                return false;
#elif UNITY_EDITOR
                return EditorEnabled;
#elif V5_NET
                return true;
#else
                return false;
#endif
            }
        }

        static bool _autoStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _autoStarted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Enabled || Application.isBatchMode) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            var active = SceneManager.GetActiveScene();
            OnSceneLoaded(active, LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != V4SceneNames.MainMenu || NetVoyageClient.Instance != null) return;
            AddButton();
            var query = Query("netvoyage");
            if (_autoStarted || string.IsNullOrEmpty(query) || query == "0") return;
            _autoStarted = true;
            var seconds = float.TryParse(Query("seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 120f;
            NetVoyageClient.Begin(query == "auto", seconds);
        }

        static string Query(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return OnlineRules.QueryValue(Application.absoluteURL, key);
#else
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++) if (args[i] == "-" + key) return args[i + 1];
            return null;
#endif
        }

        static void AddButton()
        {
            var go = new GameObject("V5NetVoyageEntry");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(go.transform, false);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-30, -30);
            rt.sizeDelta = new Vector2(380, 76);
            button.GetComponent<Image>().color = new Color(0.12f, 0.45f, 0.65f, 0.95f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(button.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = Caption;
            button.GetComponent<Button>().onClick.AddListener(() => { Object.Destroy(go); NetVoyageClient.Begin(); });
        }
    }
}

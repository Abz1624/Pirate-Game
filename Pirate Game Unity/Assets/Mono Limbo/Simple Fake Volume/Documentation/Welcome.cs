using UnityEditor;
using UnityEngine;

namespace MonoLimbo
{
    [InitializeOnLoad]
    public static class SimpleFakeVolume_WelcomeLauncher
    {
        static SimpleFakeVolume_WelcomeLauncher()
        {
            if (!EditorPrefs.HasKey(SimpleFakeVolume_WelcomeWindow.DontShowKey))
                EditorApplication.update += OpenWindowOnce;
        }

        private static void OpenWindowOnce()
        {
            EditorApplication.update -= OpenWindowOnce;
            SimpleFakeVolume_WelcomeWindow.ShowWindow();
        }
    }

    public class SimpleFakeVolume_WelcomeWindow : EditorWindow
    {
        public const string DontShowKey = "SimpleFakeVolume_Welcome_DontShow";
        
        private Texture2D banner;
        private bool dontShowAgain;

        private const string assetUrl = "https://assetstore.unity.com/packages/vfx/shaders/simple-fake-volume-fog-299560#reviews "; 
        private const string publisherUrl = "https://assetstore.unity.com/publishers/98904";

        [MenuItem("Tools/MonoLimbo/Simple Fake Volume")]
        [MenuItem("Window/MonoLimbo/Simple Fake Volume")]
        public static void ShowWindow()
        {
            var window = GetWindow<SimpleFakeVolume_WelcomeWindow>("Fake Volume Fog", true);
            window.minSize = new Vector2(460, 560);
            window.maxSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            banner = Resources.Load<Texture2D>("SimpleFakeVolume_Banner");
            dontShowAgain = EditorPrefs.HasKey(DontShowKey);
        }

        private void OnGUI()
        {
            // Set up clean text styles
            GUIStyle bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                richText = true
            };

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 8, 4) // Tightened spacing below headers
            };

            GUIStyle paddedBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 12, 12),
                margin = new RectOffset(0, 0, 0, 8) // Tightened spacing below boxes
            };

            // Global Window Margin
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.BeginVertical();
            GUILayout.Space(12);

            // Banner
            if (banner != null)
            {
                GUILayout.Label(banner, GUILayout.Height(140));
                GUILayout.Space(8);
            }
            else
            {
                GUILayout.Label("SIMPLE FAKE VOLUME FOG", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
                GUILayout.Space(12);
            }

            // Overview Section
            EditorGUILayout.LabelField("Overview", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "Achieve beautiful, lightning-fast volumetric lighting in your project. Built entirely with standard Shader Graph for maximum speed and optimization.\n\n" +
                "<b>Includes:</b>\n" +
                "• Point and Spot light volumetrics\n" +
                "• 10 ready-to-use prefabs\n" +
                "• 9 sample scenes\n" +
                "• Procedural light and camera scripts",
                bodyStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Quick Start Section
            EditorGUILayout.LabelField("Quick Start", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "<b>1.</b> Open the Prefab folder.\n" +
                "<b>2.</b> Drag and drop the desired volume into your scene hierarchy.\n" +
                "<b>3.</b> Position the prefab so its origin aligns exactly with your Light source.\n" +
                "<b>4.</b> Scale the transform to match the range and angle of your actual light.\n\n" +
                "<i>Select the Material component to adjust opacity, color, and procedural wind.</i>",
                bodyStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Support & Links Section
            GUILayout.BeginHorizontal();
            
            GUILayout.BeginVertical(paddedBox, GUILayout.Width(200));
            EditorGUILayout.LabelField("<b>Need Help?</b>", bodyStyle);
            GUILayout.Space(2);
            EditorGUILayout.LabelField("monolimbostudio@gmail.com", bodyStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(paddedBox, GUILayout.Width(200));
            EditorGUILayout.LabelField("<b>More by MonoLimbo</b>", bodyStyle);
            GUILayout.Space(2);
            if (GUILayout.Button("🌐 View Publisher Page", GUILayout.Height(24)))
            {
                Application.OpenURL(publisherUrl);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Review Call to Action
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Space(8);
            EditorGUILayout.LabelField("<color=#f39c12><b>Enjoying the asset?</b></color>", new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.LabelField("If this tool saved you time, leaving a review helps immensely!", new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8);
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
            if (GUILayout.Button("⭐ Leave a Review on the Asset Store", GUILayout.Height(30), GUILayout.Width(250)))
            {
                Application.OpenURL(assetUrl);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Do Not Show Again Toggle
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool newToggle = EditorGUILayout.ToggleLeft("Do not show this window again", dontShowAgain, GUILayout.Width(190));
            GUILayout.EndHorizontal();

            if (newToggle != dontShowAgain)
            {
                dontShowAgain = newToggle;
                if (dontShowAgain)
                    EditorPrefs.SetInt(DontShowKey, 1);
                else
                    EditorPrefs.DeleteKey(DontShowKey);
            }

            // End Global Window Margin
            GUILayout.Space(8);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            GUILayout.EndHorizontal();
        }
    }
}
using System;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>
    /// V5 M7: the player's settings (UI scale, volumes, quality, reduced effects, screen shake, the stick's side and dead
    /// zone), saved in PlayerPrefs as one JSON value and applied to the app's config. Read anywhere through
    /// <see cref="Current"/>; <see cref="Changed"/> fires after a save.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const string Key = "v5_settings";
        public const float MinUiScale = 1f, MaxUiScale = 1.4f; // 1 keeps every 72-unit target at 48 px or more on a 720p screen

        public float uiScale = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        [Tooltip("-1 = automatic (from the frame time at the start of a run), else Low 0, Mid 1, High 2.")]
        public int quality = -1;
        public bool reducedEffects;
        public bool screenShake = true;
        [Tooltip("The movement stick on the right half of the screen (the buttons move to the left).")]
        public bool stickOnRight;
        [Tooltip("Share of the stick's reach that does nothing (0.05-0.4).")]
        public float stickDeadZone = 0.12f;

        static GameSettings _current;

        /// <summary>The settings in force (loaded on first use).</summary>
        public static GameSettings Current => _current ??= Load();

        /// <summary>After a save (UI scale, volumes, the stick).</summary>
        public static event Action Changed;

        public static GameSettings Load()
        {
            GameSettings s = null;
            try
            {
                var json = PlayerPrefs.GetString(Key, string.Empty);
                if (!string.IsNullOrEmpty(json)) s = JsonUtility.FromJson<GameSettings>(json);
            }
            catch (Exception) { s = null; } // a damaged value falls back to the defaults
            s ??= new GameSettings();
            s.Clamp();
            return s;
        }

        public void Clamp()
        {
            uiScale = Mathf.Clamp(float.IsNaN(uiScale) ? 1f : uiScale, MinUiScale, MaxUiScale);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            quality = Mathf.Clamp(quality, -1, 2);
            stickDeadZone = Mathf.Clamp(stickDeadZone, 0.05f, 0.4f);
        }

        /// <summary>Saves, makes these the current settings, applies them and tells listeners.</summary>
        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
            _current = this;
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Pushes the settings into the app's config (quality, UI scale, screen shake).</summary>
        public void Apply()
        {
            var config = AppRoot.Instance != null ? AppRoot.Instance.Config : null;
            if (config == null) return;
            config.UIScale = uiScale;
            config.ScreenShakeEnabled = screenShake;
            if (quality >= 0) config.Quality = (QualityTier)quality;
        }

        public GameSettings Copy() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));

        /// <summary>Tests: use these settings without touching PlayerPrefs (null = load again on next use).</summary>
        public static void UseForTests(GameSettings settings) => _current = settings;
    }
}

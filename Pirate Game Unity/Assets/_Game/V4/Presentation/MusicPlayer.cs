using UnityEngine;

namespace RoguePirates.V4.Presentation
{
    /// <summary>
    /// Background music (demo, 30 Sep 2026): one looping source that lives across every scene (boot, menu, run), playing
    /// the clip set on V4SaltWindHudSettings. Not in tests of the regular game (DemoMode.Off).
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer _instance;
        AudioSource _source;

        float _baseVolume = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null || RoguePirates.V4.Foundation.Core.DemoMode.Off) return;
            var settings = Hud.SaltWindHudSettings.Load();
            if (settings == null || settings.music == null) return;
            var go = new GameObject("V4Music");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MusicPlayer>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.clip = settings.music;
            _instance._source.loop = true;
            _instance._source.playOnAwake = false;
            _instance._source.spatialBlend = 0f;
            _instance._baseVolume = settings.musicVolume;
            _instance._source.volume = settings.musicVolume * RoguePirates.V4.Foundation.Platform.GameSettings.Current.musicVolume;
            _instance._source.Play();
        }

        void Update()
        {
            // Tests switch the demo off at their start: stop the music then.
            if (RoguePirates.V4.Foundation.Core.DemoMode.Off && _source.isPlaying) _source.Stop();
            var volume = _baseVolume * RoguePirates.V4.Foundation.Platform.GameSettings.Current.musicVolume; // V5 M7: Settings
            if (!Mathf.Approximately(_source.volume, volume)) _source.volume = volume;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}

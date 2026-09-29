using UnityEngine;

namespace RoguePirates.V4.Foundation.Audio
{
    public sealed class SfxService : MonoBehaviour
    {
        public static SfxService Instance { get; private set; }

        [SerializeField] AudioSource source;

        void Awake()
        {
            Instance = this;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, volume);
        }

        public static void PlayPlaceholder(string id) =>
            Debug.Log($"[SfxService] placeholder:{id}");
    }
}

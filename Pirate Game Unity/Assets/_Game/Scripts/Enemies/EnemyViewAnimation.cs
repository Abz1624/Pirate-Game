using UnityEngine;

namespace HighSeas.Enemies
{
    /// <summary>Desyncs pooled enemy swim cycles so groups do not look like clones.</summary>
    public static class EnemyViewAnimation
    {
        public static void ApplySpawnVariation(Transform view, int variationSeed)
        {
            if (view == null) return;

            var phase = HashTo01(variationSeed);
            var speed = 0.9f + HashTo01(variationSeed * 31 + 7) * 0.2f;

            var animators = view.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null) continue;

                animator.speed = speed;
                animator.Update(0f);

                var layerCount = animator.layerCount;
                for (var layer = 0; layer < layerCount; layer++)
                {
                    var state = animator.GetCurrentAnimatorStateInfo(layer);
                    if (state.shortNameHash == 0) continue;

                    var layerPhase = (phase + i * 0.137f + layer * 0.089f) % 1f;
                    animator.Play(state.shortNameHash, layer, layerPhase);
                }

                animator.Update(0f);
            }

            var legacyAnimations = view.GetComponentsInChildren<Animation>(true);
            for (var i = 0; i < legacyAnimations.Length; i++)
            {
                var animation = legacyAnimations[i];
                if (animation == null || animation.clip == null) continue;

                var clipPhase = (phase + i * 0.211f) % 1f;
                var state = animation[animation.clip.name];
                if (state == null) continue;

                state.normalizedTime = clipPhase;
                state.speed = speed;
                animation.Play();
            }
        }

        static float HashTo01(int seed)
        {
            unchecked
            {
                var u = (uint)seed;
                u ^= u >> 16;
                u *= 0x7feb352d;
                u ^= u >> 15;
                u *= 0x846ca68b;
                u ^= u >> 16;
                return (u & 0xFFFF) / 65535f;
            }
        }
    }
}

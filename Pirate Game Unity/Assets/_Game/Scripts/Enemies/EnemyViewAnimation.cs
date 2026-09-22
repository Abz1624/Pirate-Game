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
                SafeResetTrigger(animator, "Attack");
                SafeResetTrigger(animator, "WindUp");
                SafeResetTrigger(animator, "Swim");

                var swimHash = Animator.StringToHash("Armature_Swim");
                var layerPhase = (phase + i * 0.137f) % 1f;
                if (animator.HasState(0, swimHash))
                    animator.Play(swimHash, 0, layerPhase);
                else
                {
                    animator.Update(0f);
                    var layerCount = animator.layerCount;
                    for (var layer = 0; layer < layerCount; layer++)
                    {
                        var state = animator.GetCurrentAnimatorStateInfo(layer);
                        if (state.shortNameHash == 0) continue;
                        var p = (layerPhase + layer * 0.089f) % 1f;
                        animator.Play(state.shortNameHash, layer, p);
                    }
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

        public static void TickSwordfish(Transform view, ref bool wasSpringing, bool isSpringing)
        {
            if (view == null) return;
            if (isSpringing == wasSpringing) return;

            var animators = view.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null) continue;

                if (isSpringing)
                {
                    SafeResetTrigger(animator, "Swim");
                    SafeSetTrigger(animator, "WindUp");
                }
                else
                {
                    SafeResetTrigger(animator, "WindUp");
                    SafeResetTrigger(animator, "Attack");
                    SafeSetTrigger(animator, "Swim");
                }
            }

            wasSpringing = isSpringing;
        }

        static void SafeResetTrigger(Animator animator, string name)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return;
            foreach (var param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger && param.name == name)
                {
                    animator.ResetTrigger(name);
                    return;
                }
            }
        }

        static void SafeSetTrigger(Animator animator, string name)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return;
            foreach (var param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger && param.name == name)
                {
                    animator.SetTrigger(name);
                    return;
                }
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

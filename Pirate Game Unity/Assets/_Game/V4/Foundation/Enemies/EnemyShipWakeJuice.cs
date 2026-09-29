using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.VFX;
using UnityEngine;



namespace RoguePirates.V4.Foundation.Enemies

{

    /// <summary>Enables ship prefab wake/foam (particles + trails; many prefabs ship with wake roots inactive).</summary>

    public static class EnemyShipWakeJuice

    {

        const float MoveSpeedSqrThreshold = 0.35f;



        public static void Tick(Transform view, EnemyCategory category, Vector3 velocity)

        {

            if (view == null || category != EnemyCategory.Ship)

                return;



            var moving = velocity.sqrMagnitude > MoveSpeedSqrThreshold;

            var root = view.gameObject;



            var transforms = root.GetComponentsInChildren<Transform>(true);

            for (var i = 0; i < transforms.Length; i++)

            {

                var t = transforms[i];

                if (t == view) continue;

                if (!IsWakeOrFoam(t.name)) continue;



                if (moving)

                {

                    if (!t.gameObject.activeSelf)

                        t.gameObject.SetActive(true);

                }

            }



            if (moving)

            {

                VfxParticleUtil.SetTrailEmitting(root, true);

                var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);

                for (var i = 0; i < particleSystems.Length; i++)

                {

                    var ps = particleSystems[i];

                    if (ps == null || !IsWakeOrFoam(ps.gameObject.name)) continue;

                    if (!ps.isPlaying)

                        ps.Play(true);

                }

            }

            else

            {

                VfxParticleUtil.SetTrailEmitting(root, false);

                var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);

                for (var i = 0; i < particleSystems.Length; i++)

                {

                    var ps = particleSystems[i];

                    if (ps == null || !IsWakeOrFoam(ps.gameObject.name)) continue;

                    if (ps.isPlaying)

                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                }

            }

        }



        static bool IsWakeOrFoam(string objectName)

        {

            if (string.IsNullOrEmpty(objectName)) return false;

            return objectName.Contains("Foam") || objectName.Contains("Wake");

        }

    }

}



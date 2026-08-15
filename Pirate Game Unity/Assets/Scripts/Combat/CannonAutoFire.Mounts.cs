using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.Combat
{
    public partial class CannonAutoFire
    {
        static readonly (string name, Vector3 localPos, Vector3 euler)[] DefaultMountDefs =
        {
            ("Cannon_Port_Fore", new Vector3(-6f, 0f, 5f), new Vector3(0f, -90f, 0f)),
            ("Cannon_Starboard_Fore", new Vector3(6f, 0f, 5f), new Vector3(0f, 90f, 0f)),
            ("Cannon_Port_Mid", new Vector3(-6f, 0f, 0f), new Vector3(0f, -90f, 0f)),
            ("Cannon_Starboard_Mid", new Vector3(6f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            ("Cannon_Port_Aft", new Vector3(-6f, 0f, -5f), new Vector3(0f, -90f, 0f)),
            ("Cannon_Starboard_Aft", new Vector3(6f, 0f, -5f), new Vector3(0f, 90f, 0f)),
            ("Cannon_Port_Rear", new Vector3(-6f, 0f, -9f), new Vector3(0f, -90f, 0f)),
            ("Cannon_Starboard_Rear", new Vector3(6f, 0f, -9f), new Vector3(0f, 90f, 0f)),
        };

        public float EffectiveFireRate => fireRate * fireRateMultiplier;
        public float EffectiveDamage => damage * damageMultiplier;
        public float EffectiveRange => range * rangeMultiplier;

        void Awake()
        {
            EnsureMinimumMounts(DefaultMountDefs.Length);
        }

        public void EnsureMinimumMounts(int minimum)
        {
            minimum = Mathf.Clamp(minimum, 2, DefaultMountDefs.Length);

            var mountsParent = transform.Find("CannonMounts");
            if (mountsParent == null)
            {
                var go = new GameObject("CannonMounts");
                go.transform.SetParent(transform, false);
                mountsParent = go.transform;
            }

            var list = new List<Transform>(DefaultMountDefs.Length);
            for (var i = 0; i < DefaultMountDefs.Length; i++)
            {
                var def = DefaultMountDefs[i];
                var existing = mountsParent.Find(def.name);
                if (existing == null)
                {
                    var mountGo = new GameObject(def.name);
                    mountGo.transform.SetParent(mountsParent, false);
                    mountGo.transform.localPosition = def.localPos;
                    mountGo.transform.localRotation = Quaternion.Euler(def.euler);
                    if (mountGo.GetComponent<CannonMount>() == null)
                        mountGo.AddComponent<CannonMount>();
                    existing = mountGo.transform;
                }
                else if (existing.GetComponent<CannonMount>() == null)
                {
                    existing.gameObject.AddComponent<CannonMount>();
                }

                list.Add(existing);
            }

            cannonMounts = list.ToArray();
            activeCannonCount = Mathf.Clamp(activeCannonCount, 1, cannonMounts.Length);
        }
    }
}

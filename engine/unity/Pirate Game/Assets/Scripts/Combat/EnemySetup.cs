using UnityEngine;

namespace PirateGame.Combat
{
    /// <summary>
    /// Add to enemy ship models. Ensures collider, Health, and Enemy tag for cannon targeting.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class EnemySetup : MonoBehaviour
    {
        void Reset()
        {
            tag = "Enemy";
            EnsureCollider();
        }

        void Awake()
        {
            if (!CompareTag("Enemy"))
                tag = "Enemy";

            EnsureCollider();
        }

        void EnsureCollider()
        {
            if (GetComponent<Collider>() != null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                gameObject.AddComponent<BoxCollider>();
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = Vector3.Max(bounds.size, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }
}

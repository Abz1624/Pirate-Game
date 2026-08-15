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
        [SerializeField] float minWorldWidth = 1.2f;
        [SerializeField] float minWorldHeight = 0.9f;
        [SerializeField] float minWorldDepth = 1.2f;

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
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                if (GetComponent<Collider>() == null)
                    gameObject.AddComponent<BoxCollider>();
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            bounds.size = new Vector3(
                Mathf.Max(bounds.size.x, minWorldWidth),
                Mathf.Max(bounds.size.y, minWorldHeight),
                Mathf.Max(bounds.size.z, minWorldDepth));

            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            var lossy = transform.lossyScale;
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(0.001f, lossy.x),
                bounds.size.y / Mathf.Max(0.001f, lossy.y),
                bounds.size.z / Mathf.Max(0.001f, lossy.z));
            box.isTrigger = false;
        }
    }
}

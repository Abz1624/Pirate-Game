using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateGame.Environment
{
    /// <summary>
    /// Marks island terrain, rocks, and trees as obstacles on the Island physics layer.
    /// Trees without colliders get a simple capsule at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class IslandObstacle : MonoBehaviour
    {
        [SerializeField] bool addTreeColliderIfMissing = true;
        [SerializeField] float treeColliderRadius = 0.35f;
        [SerializeField] float treeColliderHeight = 2.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ConfigureActiveScene()
        {
            ConfigureScene();
        }

        void Awake()
        {
            Configure(gameObject, addTreeColliderIfMissing, treeColliderRadius, treeColliderHeight);
        }

        public static bool ShouldBeObstacle(GameObject go)
        {
            var name = go.name;
            return name.StartsWith("Ground_Sand")
                   || name.Contains("RockMoon")
                   || name.StartsWith("Tree_");
        }

        public static void ConfigureScene()
        {
            if (!IslandObstacles.IsAvailable)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded)
                return;

            foreach (var root in scene.GetRootGameObjects())
                ConfigureRecursive(root.transform);
        }

        public static void Configure(
            GameObject go,
            bool addTreeCollider = true,
            float treeRadius = 0.35f,
            float treeHeight = 2.5f)
        {
            if (go == null || !IslandObstacles.IsAvailable)
                return;

            SetLayerRecursive(go.transform, IslandObstacles.Layer);

            if (!addTreeCollider || go.GetComponentInChildren<Collider>() != null || !IsTree(go))
                return;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = treeRadius;
            capsule.height = treeHeight;
            capsule.center = new Vector3(0f, treeHeight * 0.5f, 0f);
        }

        static void ConfigureRecursive(Transform transform)
        {
            if (ShouldBeObstacle(transform.gameObject))
            {
                if (transform.GetComponent<IslandObstacle>() == null)
                    transform.gameObject.AddComponent<IslandObstacle>();
                else
                    Configure(transform.gameObject);
            }

            for (var i = 0; i < transform.childCount; i++)
                ConfigureRecursive(transform.GetChild(i));
        }

        static bool IsTree(GameObject go) => go.name.StartsWith("Tree_");

        static void SetLayerRecursive(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (var i = 0; i < transform.childCount; i++)
                SetLayerRecursive(transform.GetChild(i), layer);
        }
    }
}

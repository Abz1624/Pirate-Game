#if UNITY_EDITOR
using HighSeas.Data;
using HighSeas.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class PlayerShipSceneSetup
    {
        const string ShipDefinitionPath = "Assets/_Game/Data/Ships/cutlass_sloop.asset";

        [MenuItem("HighSeas/Setup Player Ship In Scene")]
        public static void SetupActiveScene()
        {
            var player = Object.FindFirstObjectByType<PlayerShipController>();
            if (player == null)
            {
                Debug.LogError("PlayerShipSceneSetup: No PlayerShipController found in the active scene.");
                return;
            }

            SetupPlayer(player);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Player ship setup complete. The CutlassSloop visual is now a scene child of PlayerShip.");
        }

        public static void SetupPlayer(PlayerShipController player)
        {
            var go = player.gameObject;
            go.transform.localScale = Vector3.one;

            RemovePlaceholderMesh(go);
            RemoveLegacyComponents(go);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            var visualRoot = EnsureVisualInstance(go.transform);
            if (visualRoot == null)
            {
                Debug.LogError("PlayerShipSceneSetup: Could not find or create a ship visual. Assign visualPrefab on cutlass_sloop.asset.");
                return;
            }

            SanitizeVisualInstance(visualRoot.gameObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(visualRoot.gameObject);
            EnsureEditorVisualPivot(go.transform);

            var pivot = go.transform.Find("VisualTiltPivot");
            var controllerSo = new SerializedObject(player);
            controllerSo.FindProperty("visualModel").objectReferenceValue = pivot;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            ConfigurePhysics(go);
        }

        static void EnsureEditorVisualPivot(Transform playerTransform)
        {
            if (playerTransform.Find("VisualTiltPivot") != null)
                return;

            Transform visualChild = null;
            for (var i = 0; i < playerTransform.childCount; i++)
            {
                var child = playerTransform.GetChild(i);
                if (child.GetComponentInChildren<MeshRenderer>(true) != null)
                {
                    visualChild = child;
                    break;
                }
            }

            if (visualChild == null)
                return;

            var pivot = new GameObject("VisualTiltPivot").transform;
            pivot.SetParent(playerTransform, false);
            visualChild.SetParent(pivot, true);
        }

        static Transform EnsureVisualInstance(Transform playerTransform)
        {
            for (var i = 0; i < playerTransform.childCount; i++)
            {
                var child = playerTransform.GetChild(i);
                if (child.GetComponentInChildren<MeshRenderer>(true) != null)
                    return child;
            }

            var shipDef = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);
            if (shipDef == null || shipDef.visualPrefab == null)
                return null;

            var instance = PrefabUtility.InstantiatePrefab(shipDef.visualPrefab, playerTransform) as GameObject;
            if (instance == null)
                return null;

            instance.name = shipDef.visualPrefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance.transform;
        }

        static void ConfigurePhysics(GameObject player)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb == null)
                rb = player.AddComponent<Rigidbody>();

            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                             RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;

            var hull = player.GetComponent<CapsuleCollider>();
            if (hull == null)
                hull = player.AddComponent<CapsuleCollider>();

            hull.radius = 1.15f;
            hull.height = 4.5f;
            hull.direction = 2;
            hull.center = Vector3.zero;
        }

        static void RemovePlaceholderMesh(GameObject player)
        {
            var meshRenderer = player.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                Object.DestroyImmediate(meshRenderer);

            var meshFilter = player.GetComponent<MeshFilter>();
            if (meshFilter != null)
                Object.DestroyImmediate(meshFilter);
        }

        static void RemoveLegacyComponents(GameObject player)
        {
            var components = player.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    // Missing script from removed PlayerShipVisual.
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);
                    break;
                }
            }
        }

        static void SanitizeVisualInstance(GameObject visualRoot)
        {
            foreach (var rb in visualRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in visualRoot.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            DisableByName(visualRoot.transform, "HealthBar");
            DisableByName(visualRoot.transform, "Aim");
        }

        static void DisableByName(Transform root, string objectName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    t.gameObject.SetActive(false);
            }
        }

        static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            for (var i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif

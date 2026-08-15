using PirateGame.Combat;
using PirateGame.Enemies;
using PirateGame.Net;
using PirateGame.Progression;
using PirateGame.Ship;
using UnityEngine;

namespace PirateGame
{
    /// <summary>
    /// Single-player focus: local ship movement, survivor waves, no blockchain co-op.
    /// Attach to the same bootstrap object as CoopMatchBootstrap (or any scene root).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class SoloRunBootstrap : MonoBehaviour
    {
        [SerializeField] bool soloMode = true;
        [SerializeField] bool hideRemoteShip = true;
        [SerializeField] bool ensureWaveSystems = true;
        [SerializeField] bool logStatus = true;

        void Awake()
        {
            if (!soloMode) return;

            DisableNetworkingUi();
            ConfigurePlayerShip();
            ConfigureCamera();
            ConfigurePlayerProgression();
            HideRemoteShip();

            if (ensureWaveSystems)
                EnsureWaveSystems();

            EnsurePickupDrops();
            EnsureHealthHud();
            EnsureScoreHud();

            if (logStatus)
                Debug.Log("[Solo] Local survivor mode — WASD ship, waves driven by WaveDirector.");
        }

        void DisableNetworkingUi()
        {
            foreach (var hotkeys in FindObjectsByType<CoopLocalHotkeys>(FindObjectsSortMode.None))
                hotkeys.enabled = false;

            foreach (var er in FindObjectsByType<ErClient>(FindObjectsSortMode.None))
                er.enabled = false;

            foreach (var lobby in FindObjectsByType<LobbyManager>(FindObjectsSortMode.None))
                lobby.enabled = false;
        }

        void ConfigurePlayerShip()
        {
            foreach (var net in FindObjectsByType<NetworkedShipController>(FindObjectsSortMode.None))
                net.enabled = false;

            foreach (var dual in FindObjectsByType<LocalDualShipController>(FindObjectsSortMode.None))
                dual.enabled = false;

            foreach (var recon in FindObjectsByType<ShipPredictionReconciler>(FindObjectsSortMode.None))
                recon.enabled = false;

            foreach (var movement in FindObjectsByType<ShipMovement>(FindObjectsSortMode.None))
                movement.enabled = true;

            var player = GameObject.FindGameObjectWithTag("Player");
            player?.GetComponent<CannonAutoFire>()?.EnsureMinimumMounts(8);
        }

        void ConfigureCamera()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            var follow = cam.GetComponent<PirateGame.Camera.TopDownCameraFollow>();
            if (follow == null) return;

            follow.followShipRotation = false;
            follow.offset = new Vector3(0f, 18f, 12f);
        }

        void ConfigurePlayerProgression()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            if (player.GetComponent<Health>() == null)
            {
                var hp = player.AddComponent<Health>();
                hp.Initialize(100f);
            }

            if (player.GetComponent<PlayerRunProgress>() == null)
                player.AddComponent<PlayerRunProgress>();

            if (player.GetComponent<PickupMagnet>() == null)
                player.AddComponent<PickupMagnet>();

            var draftUi = FindFirstObjectByType<SoloDraftUI>();
            if (draftUi == null)
            {
                var uiGo = new GameObject("SoloDraftUI");
                uiGo.AddComponent<SoloDraftUI>();
            }
        }

        void HideRemoteShip()
        {
            if (!hideRemoteShip) return;

            var remote = GameObject.Find("RemoteShip");
            if (remote != null)
                remote.SetActive(false);
        }

        void EnsureWaveSystems()
        {
            var director = FindFirstObjectByType<WaveDirector>();
            if (director != null) return;

            var systems = GameObject.Find("GameSystems") ?? new GameObject("GameSystems");
            var spawner = systems.GetComponent<EnemySpawner>();
            if (spawner == null)
                spawner = systems.AddComponent<EnemySpawner>();

            director = systems.GetComponent<WaveDirector>();
            if (director == null)
                director = systems.AddComponent<WaveDirector>();

            spawner.SetAutoSpawn(false);
            spawner.SetSpawnOnStart(false);
        }

        void EnsurePickupDrops()
        {
            var spawner = FindFirstObjectByType<EnemySpawner>();
            spawner?.EnsurePickupPrefabs();
        }

        void EnsureHealthHud()
        {
            if (FindFirstObjectByType<UI.PlayerHealthHud>() != null)
                return;

            var hudGo = new GameObject("PlayerHealthHud");
            hudGo.AddComponent<UI.PlayerHealthHud>();
        }

        void EnsureScoreHud()
        {
            if (FindFirstObjectByType<UI.SoloScoreHud>() != null)
                return;

            var hudGo = new GameObject("SoloScoreHud");
            hudGo.AddComponent<UI.SoloScoreHud>();
        }
    }
}

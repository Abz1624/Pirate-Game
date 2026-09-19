using System.Collections;
using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Debugging;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Platform;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.V3;
using HighSeas.VFX;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

namespace HighSeas.Promo
{
    /// <summary>
    /// Promo shot director: randomize islands / player / swarm separately, then record.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class PromoTeaserDirector : MonoBehaviour
    {
        struct PlacedSpawn
        {
            public Vector3 position;
            public float radius;
        }

        [Header("Trigger")]
        [SerializeField] bool enterPreviewOnPlay = true;
        [SerializeField] bool showRuntimeControls = true;
        [SerializeField] Key restartKey = Key.R;

        [Header("World")]
        [SerializeField] StageBuilder stageBuilder;
        [SerializeField] IslandLayoutDefinition islandLayout;
        [SerializeField] int layoutSeed = 90210;
        [SerializeField] int playerSpawnSalt = 31;
        [SerializeField] int swarmSeed = 404;

        [Header("Horde")]
        [SerializeField] int enemyCount = 500;
        [SerializeField] int enemyCountStep = 50;
        [SerializeField] int enemyCountMin = 50;
        [SerializeField] int enemyCountMax = 750;
        [SerializeField] float playerSpawnClearance = 0.1f;
        [SerializeField] float playerClearanceStep = 0.25f;
        [SerializeField] float playerClearanceMin = 0.1f;
        [SerializeField] float playerClearanceMax = 10f;
        [SerializeField] float mapScatterFraction = 0.08f;
        [SerializeField] int mapScatterMin = 20;
        [SerializeField] int mapScatterMax = 80;
        [SerializeField] float mapScatterMinDistFromPlayer = 28f;
        [SerializeField] float openWaterFillFraction = 0.2f;
        [SerializeField] int openWaterFillMin = 35;
        [SerializeField] int openWaterFillMax = 140;
        [SerializeField] float openWaterMinDistFromPlayer = 30f;
        [SerializeField] float coreRadiusMin = 1.6f;
        [SerializeField] float coreRadiusMax = 6.5f;
        [SerializeField] float innerRingRadiusMin = 6.5f;
        [SerializeField] float innerRingRadiusMax = 11f;
        [SerializeField] float midRingRadiusMin = 11f;
        [SerializeField] float midRingRadiusMax = 16f;
        [SerializeField] float outerRingRadiusMin = 16f;
        [SerializeField] float outerRingRadiusMax = 22f;
        [SerializeField] float positionJitter = 0.45f;
        [SerializeField] float spawnSeparationPadding = 0.32f;
        [SerializeField] float spawnOverlapRelax = 0.48f;
        [SerializeField] int promoAgentCapacity = 800;
        [SerializeField] int armadaClusterCount = 4;
        [SerializeField] float armadaClusterDistanceMin = 48f;
        [SerializeField] float armadaClusterDistanceMax = 68f;
        [SerializeField] float armadaClusterRadiusMin = 10f;
        [SerializeField] float armadaClusterRadiusMax = 20f;
        [SerializeField] float armadaBudgetFraction = 0.24f;
        [SerializeField] bool enableOverwhelmJuice = true;
        [SerializeField] float islandSpawnClearance = 0.55f;
        [SerializeField] int spawnAttemptsPerSlot = 32;
        [SerializeField] bool skipBossesAndMinibosses = true;
        [SerializeField] bool hideEnemyHealthBars = true;

        [Header("Camera")]
        [SerializeField] float zoomDurationSeconds = 7.5f;
        [SerializeField] Vector3 gameplayCameraOffset = new(0f, 18f, 12f);
        [SerializeField] Vector3 endCameraOffset = new(0f, 10f, 6.5f);
        [SerializeField] float startHeight = 76f;
        [SerializeField] float startPullback = 54f;
        [SerializeField] AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Presentation")]
        [SerializeField] bool hideScreenSpaceUi = true;
        [SerializeField] bool freezeGameplayDuringShot = true;
        [SerializeField] bool releaseGameplayAfterShot;
        [FormerlySerializedAs("disableSpawnDirector")]
        [SerializeField] bool disableEncounterSpawner = true;
        [SerializeField] bool stripPoisAndPickups = true;
        [SerializeField] bool hideMapBoundaryVisuals = true;

        const float DefaultPlayerHullRadius = 1.15f;

        EnemyWorld _enemyWorld;
        PlayerShipController _player;
        FollowCamera _followCamera;
        EncounterSpawner _encounterSpawner;
        WeaponController _weapons;
        ProjectileWorld _projectiles;
        Camera _camera;
        POIManager _poiManager;
        PickupWorld _pickupWorld;
        StageLayout _layout;

        bool _previewActive;
        bool _shotActive;
        bool _hordeReady;
        float _shotTime;
        float _cameraYaw;
        Vector3 _startOffset;
        Vector3 _endOffset;
        readonly List<GameObject> _disabledUiRoots = new(8);
        readonly List<PlacedSpawn> _placedSpawns = new(800);
        readonly List<MonoBehaviour> _disabledGameplay = new(8);
        bool _encounterSpawnerWasEnabled;
        bool _poiManagerWasEnabled;
        bool _pickupWorldWasEnabled;
        int _spawnWarnings;
        PromoTeaserOverwhelmJuice _overwhelmJuice;

        void Start()
        {
            CacheReferences();
            if (enterPreviewOnPlay)
                StartCoroutine(EnterPreviewAfterBootstrap());
        }

        IEnumerator EnterPreviewAfterBootstrap()
        {
            yield return null;
            yield return null;
            EnterPreviewMode();
            for (var i = 0; i < 8; i++)
            {
                yield return null;
                HideMapBoundaryVisuals();
            }
        }

        void Update()
        {
            if (_previewActive && !_shotActive && RunManager.Instance != null)
                RunManager.Instance.SetPause(PauseReason.UserPause);

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[restartKey].wasPressedThisFrame && _shotActive)
                StartCameraShot();
        }

        void LateUpdate()
        {
            if (!_shotActive || _player == null || _camera == null)
                return;

            _shotTime += Time.deltaTime;
            var t = zoomDurationSeconds > 0f ? Mathf.Clamp01(_shotTime / zoomDurationSeconds) : 1f;
            if (zoomCurve != null && zoomCurve.length > 0)
                t = zoomCurve.Evaluate(t);

            var anchor = _player.Position;
            var offset = LerpCameraOffset(_startOffset, _endOffset, t);
            ApplyPromoCameraPose(anchor, offset);

            if (_shotTime < zoomDurationSeconds)
                return;

            FinishShot();
        }

        void OnGUI()
        {
            if (!showRuntimeControls || !Application.isPlaying || _shotActive)
                return;

            const int buttonWidth = 240;
            const int buttonHeight = 36;
            const int gap = 6;
            const int rows = 11;
            var totalHeight = rows * buttonHeight + (rows - 1) * gap;
            var y = Screen.height - totalHeight - 20;
            var x = (Screen.width - buttonWidth) / 2;
            var halfWidth = (buttonWidth - gap) / 2;

            GUI.skin.button.fontSize = 15;
            if (GUI.Button(new Rect(x, y, halfWidth, buttonHeight), $"Swarm −{enemyCountStep}"))
                AdjustEnemyCount(-enemyCountStep);
            if (GUI.Button(new Rect(x + halfWidth + gap, y, halfWidth, buttonHeight), $"Swarm +{enemyCountStep}"))
                AdjustEnemyCount(enemyCountStep);
            y += buttonHeight + gap;
            GUI.Label(new Rect(x, y, buttonWidth, buttonHeight), $"Enemy count: {enemyCount}", GUI.skin.box);
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, halfWidth, buttonHeight), $"Safety −{playerClearanceStep:0.##}"))
                AdjustPlayerSpawnClearance(-playerClearanceStep);
            if (GUI.Button(new Rect(x + halfWidth + gap, y, halfWidth, buttonHeight), $"Safety +{playerClearanceStep:0.##}"))
                AdjustPlayerSpawnClearance(playerClearanceStep);
            y += buttonHeight + gap;
            GUI.Label(new Rect(x, y, buttonWidth, buttonHeight),
                $"Ship safety radius: {playerSpawnClearance:0.##} m", GUI.skin.box);
            y += buttonHeight + gap;
            EnsureOverwhelmJuice();
            var flipLabel = _overwhelmJuice != null && _overwhelmJuice.FlipDamageFacing ? "ON" : "OFF";
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), $"Flip damage numbers ({flipLabel})"))
                ToggleDamageNumberFlip();
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, halfWidth, buttonHeight), "Dmg size −"))
                AdjustDamageNumberSize(-1);
            if (GUI.Button(new Rect(x + halfWidth + gap, y, halfWidth, buttonHeight), "Dmg size +"))
                AdjustDamageNumberSize(1);
            y += buttonHeight + gap;
            var dmgSize = _overwhelmJuice != null ? _overwhelmJuice.DamageCharacterSize : 0f;
            GUI.Label(new Rect(x, y, buttonWidth, buttonHeight), $"Damage number size: {dmgSize:0.###}", GUI.skin.box);
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Randomize islands"))
                RandomizeIslands();
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Randomize player position"))
                RandomizePlayerPosition();
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Randomize swarm"))
                RandomizeSwarm();
            y += buttonHeight + gap;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Start recording"))
                StartRecording();
        }

        public void AdjustEnemyCount(int delta)
        {
            enemyCount = Mathf.Clamp(enemyCount + delta, enemyCountMin, enemyCountMax);
            if (_hordeReady)
                RandomizeSwarm();
        }

        public void AdjustPlayerSpawnClearance(float delta)
        {
            playerSpawnClearance = Mathf.Clamp(
                playerSpawnClearance + delta,
                playerClearanceMin,
                playerClearanceMax);
            if (_hordeReady)
                RandomizeSwarm();
        }

        public void ToggleDamageNumberFlip()
        {
            EnsureOverwhelmJuice();
            _overwhelmJuice?.ToggleFlipDamageFacing();
        }

        public void AdjustDamageNumberSize(int steps)
        {
            EnsureOverwhelmJuice();
            _overwhelmJuice?.AdjustDamageCharacterSizeBySteps(steps);
        }

        public void EnterPreviewMode()
        {
            CacheReferences();
            _previewActive = true;
            _shotActive = false;
            _hordeReady = false;

            EnsurePlayerAlive();
            DisableGameplaySystems();
            if (stripPoisAndPickups)
                StripPoisAndPickups();

            if (hideScreenSpaceUi)
                HideScreenSpaceUi();

            HideMapBoundaryVisuals();

            if (_followCamera != null)
            {
                _followCamera.enabled = true;
                _followCamera.SnapToTarget();
            }

            RunManager.Instance?.SetPause(PauseReason.UserPause);
        }

        public void RandomizeIslands()
        {
            if (!Application.isPlaying) return;
            CacheReferences();
            AbortShotIfNeeded();

            layoutSeed = Random.Range(1, int.MaxValue);
            ClearHorde();
            RebuildIslands(layoutSeed);
            RandomizePlayerPositionInternal();
            _hordeReady = false;
            Debug.Log($"Promo: islands seed={layoutSeed}");
        }

        public void RandomizePlayerPosition()
        {
            if (!Application.isPlaying) return;
            CacheReferences();
            AbortShotIfNeeded();

            playerSpawnSalt = Random.Range(1, int.MaxValue);
            RandomizePlayerPositionInternal();
            ClearHorde();
            _hordeReady = false;
            Debug.Log($"Promo: player spawn salt={playerSpawnSalt}");
        }

        public void RandomizeSwarm()
        {
            if (!Application.isPlaying) return;
            CacheReferences();
            AbortShotIfNeeded();

            swarmSeed = Random.Range(1, int.MaxValue);
            PrepareHorde();
            _hordeReady = true;
            Debug.Log($"Promo: swarm seed={swarmSeed}, active enemies={_enemyWorld?.ActiveCount ?? 0}");
        }

        public void StartRecording()
        {
            if (!Application.isPlaying) return;
            CacheReferences();

            if (!_hordeReady)
                RandomizeSwarm();

            if (stripPoisAndPickups)
                StripPoisAndPickups();

            if (hideEnemyHealthBars)
                HideEnemyHealthBars();

            StartCameraShot();
        }

        void AbortShotIfNeeded()
        {
            _shotActive = false;
            if (_followCamera != null)
            {
                _followCamera.enabled = true;
                _followCamera.SnapToTarget();
            }

            if (hideScreenSpaceUi)
                HideScreenSpaceUi();

            RunManager.Instance?.SetPause(PauseReason.UserPause);
        }

        void RebuildIslands(int seed)
        {
            if (stageBuilder == null)
                stageBuilder = FindFirstObjectByType<StageBuilder>();

            if (stageBuilder == null)
                return;

            var layoutDef = ResolveIslandLayout();
            stageBuilder.ConfigureV3Layout(layoutDef);
            _layout = stageBuilder.Build(seed, 0);
            V3RunContext.Layout = _layout;
            Physics.SyncTransforms();
        }

        IslandLayoutDefinition ResolveIslandLayout()
        {
            if (islandLayout != null && islandLayout.HasIslands)
                return islandLayout;

            var fromResources = Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            return fromResources != null && fromResources.HasIslands ? fromResources : islandLayout;
        }

        void RandomizePlayerPositionInternal()
        {
            if (_player == null || _layout == null)
                return;

            var spawn = V3SpawnAnchorValidator.ResolveRuntimeSpawn(
                _layout,
                ResolveIslandLayout(),
                layoutSeed ^ playerSpawnSalt,
                1.15f);
            _player.SetWorldPosition(spawn);
            Physics.SyncTransforms();
        }

        void PrepareHorde()
        {
            if (_enemyWorld == null || _player == null)
                return;

            ClearHorde();
            _enemyWorld.EnsureAgentCapacity(Mathf.Max(promoAgentCapacity, enemyCount + 64));
            var center = _player.Position;
            var rng = new RunRng(swarmSeed);
            _spawnWarnings = 0;
            SpawnHorde(center, rng);
            _enemyWorld.ResolveSpawnOverlapsOnly(1);
            _enemyWorld.SnapAllActiveViewsToward(center);

            if (enableOverwhelmJuice)
            {
                EnsureOverwhelmJuice();
                _overwhelmJuice.Build(_player, _enemyWorld, new RunRng(swarmSeed ^ 0x7A31));
            }

            if (hideEnemyHealthBars)
                HideEnemyHealthBars();

            var cap = Platform.QualityProfile.Current.enemyCap;
            if (_enemyWorld.ActiveCount < enemyCount && _enemyWorld.ActiveCount >= cap - 2)
                Debug.LogWarning($"Promo: enemy cap {cap} reached — raise Quality tier or lower enemy count (wanted {enemyCount}, spawned {_enemyWorld.ActiveCount}).");

            if (_spawnWarnings > 0)
                Debug.LogWarning($"Promo: {_spawnWarnings} enemy slots could not be placed (cap or water/island conflict). Spawned {_enemyWorld.ActiveCount}.");
        }

        void ClearHorde()
        {
            _overwhelmJuice?.Clear();
            _enemyWorld?.ResetAllAgentsForPromo();
            _placedSpawns.Clear();
            _hordeReady = false;
        }

        void EnsureOverwhelmJuice()
        {
            if (_overwhelmJuice != null) return;
            _overwhelmJuice = GetComponent<PromoTeaserOverwhelmJuice>();
            if (_overwhelmJuice == null)
                _overwhelmJuice = gameObject.AddComponent<PromoTeaserOverwhelmJuice>();
        }

        void StartCameraShot()
        {
            if (_player == null || _camera == null)
                return;

            if (_followCamera != null)
                _followCamera.enabled = false;

            if (freezeGameplayDuringShot && RunManager.Instance != null)
                RunManager.Instance.SetPause(PauseReason.UserPause);

            if (hideScreenSpaceUi)
                HideScreenSpaceUi();

            HideMapBoundaryVisuals();

            _enemyWorld?.SnapAllActiveViewsToward(_player.Position);

            _startOffset = new Vector3(gameplayCameraOffset.x, startHeight, -Mathf.Abs(startPullback));
            _endOffset = BuildEndOffset();
            _shotTime = 0f;
            _shotActive = true;
            ApplyPromoCameraPose(_player.Position, _startOffset);
        }

        void FinishShot()
        {
            _shotActive = false;

            if (releaseGameplayAfterShot && RunManager.Instance != null)
                RunManager.Instance.SetPause(PauseReason.None);
            else
                EnterPreviewMode();
        }

        void EnsurePlayerAlive()
        {
            if (_player?.Damage == null) return;
            if (_player.Damage.IsAlive) return;

            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded)
                RunManager.Instance.EnterPlaying();

            _player.Damage.Heal(_player.Damage.MaxHP);
        }

        void DisableGameplaySystems()
        {
            _disabledGameplay.Clear();

            if (disableEncounterSpawner && _encounterSpawner != null)
            {
                _encounterSpawnerWasEnabled = _encounterSpawner.enabled;
                _encounterSpawner.enabled = false;
            }

            DisableIfPresent(_weapons);
            DisableIfPresent(_projectiles);
            DisableIfPresent(FindFirstObjectByType<HostileProjectileWorld>());
            DisableIfPresent(FindFirstObjectByType<WeaponTargetingVisualizer>());
        }

        static void DisableIfPresent(MonoBehaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled)
                return;
            behaviour.enabled = false;
        }

        Vector3 BuildEndOffset() =>
            new(endCameraOffset.x, endCameraOffset.y, -Mathf.Abs(endCameraOffset.z));

        static Vector3 LerpCameraOffset(Vector3 start, Vector3 end, float t)
        {
            var y = Mathf.Lerp(start.y, end.y, t);
            var startHoriz = new Vector2(start.x, start.z).magnitude;
            var endHoriz = new Vector2(end.x, end.z).magnitude;
            var horiz = Mathf.Lerp(startHoriz, endHoriz, t);
            var x = Mathf.Lerp(start.x, end.x, t);
            var zSign = Mathf.Sign(start.z);
            if (Mathf.Approximately(zSign, 0f))
                zSign = -1f;
            var z = zSign * horiz;
            return new Vector3(x, y, z);
        }

        void ApplyPromoCameraPose(Vector3 anchor, Vector3 offset)
        {
            var camPos = anchor + offset;
            _camera.transform.position = camPos;

            var toAnchor = anchor - camPos;
            var flat = new Vector3(toAnchor.x, 0f, toAnchor.z);
            if (flat.sqrMagnitude > 0.01f)
                _cameraYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;

            var horiz = flat.magnitude;
            if (horiz < 0.01f)
                horiz = 0.01f;
            var pitch = Mathf.Atan2(-toAnchor.y, horiz) * Mathf.Rad2Deg;
            _camera.transform.rotation = Quaternion.Euler(pitch, _cameraYaw, 0f);
        }

        void CacheReferences()
        {
            _enemyWorld ??= FindFirstObjectByType<EnemyWorld>();
            _player ??= FindFirstObjectByType<PlayerShipController>();
            _followCamera ??= FindFirstObjectByType<FollowCamera>();
            _encounterSpawner ??= FindFirstObjectByType<EncounterSpawner>();
            _weapons ??= FindFirstObjectByType<WeaponController>();
            _projectiles ??= FindFirstObjectByType<ProjectileWorld>();
            _camera ??= Camera.main;
            _poiManager ??= FindFirstObjectByType<POIManager>();
            _pickupWorld ??= FindFirstObjectByType<PickupWorld>();
            stageBuilder ??= FindFirstObjectByType<StageBuilder>();
            _layout ??= V3RunContext.Layout;
        }

        void StripPoisAndPickups()
        {
            _pickupWorld ??= FindFirstObjectByType<PickupWorld>();
            if (_pickupWorld != null)
            {
                _pickupWorld.ClearAllActive();
                _pickupWorldWasEnabled = _pickupWorld.enabled;
                _pickupWorld.enabled = false;
            }

            if (_poiManager != null)
            {
                _poiManagerWasEnabled = _poiManager.enabled;
                _poiManager.enabled = false;
            }

            DestroyAllPoiMarkers();
        }

        static void DestroyAllPoiMarkers()
        {
            var interactables = Object.FindObjectsByType<POIInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < interactables.Length; i++)
            {
                if (interactables[i] != null)
                    Object.Destroy(interactables[i].gameObject);
            }
        }

        void SpawnHorde(Vector3 center, RunRng rng)
        {
            var defs = _enemyWorld.Definitions;
            if (defs == null || defs.Length == 0)
                return;

            var pool = new List<int>(defs.Length);
            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                if (skipBossesAndMinibosses && (def.isBoss || def.isMiniboss)) continue;
                pool.Add(i);
            }

            if (pool.Count == 0)
            {
                for (var i = 0; i < defs.Length; i++)
                    pool.Add(i);
            }

            Shuffle(pool, rng);

            _placedSpawns.Clear();
            var count = Mathf.Max(1, enemyCount);
            var scatterCount = Mathf.Clamp(
                Mathf.RoundToInt(count * mapScatterFraction),
                mapScatterMin,
                mapScatterMax);
            var openWaterCount = Mathf.Clamp(
                Mathf.RoundToInt(count * openWaterFillFraction),
                openWaterFillMin,
                openWaterFillMax);
            var exteriorBudget = scatterCount + openWaterCount;
            exteriorBudget = Mathf.Min(exteriorBudget, count / 2);
            scatterCount = Mathf.Min(scatterCount, exteriorBudget / 2 + exteriorBudget % 2);
            openWaterCount = exteriorBudget - scatterCount;
            var armadaBudget = Mathf.RoundToInt(count * armadaBudgetFraction);
            var ringCount = count - exteriorBudget - armadaBudget;

            var coreCount = Mathf.RoundToInt(ringCount * 0.58f);
            var innerCount = Mathf.RoundToInt(ringCount * 0.34f);
            var midCount = Mathf.RoundToInt(ringCount * 0.05f);
            var outerCount = ringCount - coreCount - innerCount - midCount;

            var spawnIndex = 0;
            SpawnAnnulus(pool, center, defs, coreRadiusMin, coreRadiusMax, coreCount, ref spawnIndex, rng,
                biasTowardInner: true, separationScale: 0.22f, innerBiasPower: 4f);
            SpawnAnnulus(pool, center, defs, innerRingRadiusMin, innerRingRadiusMax, innerCount, ref spawnIndex, rng,
                biasTowardInner: true, separationScale: 0.32f, innerBiasPower: 3.5f);
            SpawnAnnulus(pool, center, defs, midRingRadiusMin, midRingRadiusMax, midCount, ref spawnIndex, rng,
                biasTowardInner: false, separationScale: 0.55f, innerBiasPower: 1.5f);
            SpawnAnnulus(pool, center, defs, outerRingRadiusMin, outerRingRadiusMax, outerCount, ref spawnIndex, rng,
                biasTowardInner: false, separationScale: 0.75f, innerBiasPower: 1f);
            SpawnConvergingArmadas(pool, center, defs, armadaBudget, ref spawnIndex, rng);
            SpawnMapScatter(pool, defs, center, scatterCount, ref spawnIndex, rng);
            SpawnOpenWaterOutsideSwarm(pool, defs, center, openWaterCount, ref spawnIndex, rng);
        }

        void SpawnConvergingArmadas(
            List<int> pool,
            Vector3 playerCenter,
            EnemyDefinition[] defs,
            int totalCount,
            ref int spawnIndex,
            RunRng rng)
        {
            if (totalCount <= 0 || armadaClusterCount <= 0)
                return;

            var perCluster = Mathf.Max(1, totalCount / armadaClusterCount);
            var angleStep = 360f / armadaClusterCount;
            var baseAngle = rng.DirectorFloat() * 30f;

            for (var c = 0; c < armadaClusterCount; c++)
            {
                var angle = (baseAngle + c * angleStep) * Mathf.Deg2Rad;
                var dist = Mathf.Lerp(armadaClusterDistanceMin, armadaClusterDistanceMax, rng.DirectorFloat());
                var clusterCenter = playerCenter + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                SpawnAnnulus(
                    pool,
                    clusterCenter,
                    defs,
                    armadaClusterRadiusMin,
                    armadaClusterRadiusMax,
                    perCluster,
                    ref spawnIndex,
                    rng,
                    biasTowardInner: true,
                    separationScale: 0.35f,
                    innerBiasPower: 3f);
            }
        }

        static void Shuffle(List<int> list, RunRng rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.DirectorInt(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void SpawnMapScatter(
            List<int> pool,
            EnemyDefinition[] defs,
            Vector3 playerCenter,
            int count,
            ref int spawnIndex,
            RunRng rng)
        {
            if (count <= 0) return;

            var margin = 14f;
            var boundsCenter = PlayBounds.Center;
            var limitX = Mathf.Max(8f, PlayBounds.HalfExtentX - margin);
            var limitZ = Mathf.Max(8f, PlayBounds.HalfExtentZ - margin);

            for (var i = 0; i < count; i++)
            {
                var defIndex = pool[(spawnIndex + i) % pool.Count];
                var def = defs[defIndex];
                var agentRadius = def != null ? def.radius + spawnSeparationPadding : 1.2f;

                var placed = false;
                for (var attempt = 0; attempt < spawnAttemptsPerSlot; attempt++)
                {
                    var offsetX = (rng.DirectorFloat() * 2f - 1f) * limitX;
                    var offsetZ = (rng.DirectorFloat() * 2f - 1f) * limitZ;
                    var pos = boundsCenter + new Vector3(offsetX, 0f, offsetZ);

                    var toPlayer = pos - playerCenter;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude < mapScatterMinDistFromPlayer * mapScatterMinDistFromPlayer)
                        continue;

                    if (!PlayBounds.IsOpenWater(pos, agentRadius + islandSpawnClearance))
                        continue;

                    if (!IsValidSpawnPosition(pos, agentRadius))
                        continue;

                    if (_enemyWorld.Spawn(defIndex, pos) < 0)
                        continue;

                    _placedSpawns.Add(new PlacedSpawn { position = pos, radius = agentRadius });
                    placed = true;
                    break;
                }

                if (!placed)
                    _spawnWarnings++;
            }

            spawnIndex += count;
        }

        void SpawnOpenWaterOutsideSwarm(
            List<int> pool,
            EnemyDefinition[] defs,
            Vector3 playerCenter,
            int count,
            ref int spawnIndex,
            RunRng rng)
        {
            if (count <= 0) return;

            var margin = 12f;
            var boundsCenter = PlayBounds.Center;
            var limitX = Mathf.Max(10f, PlayBounds.HalfExtentX - margin);
            var limitZ = Mathf.Max(10f, PlayBounds.HalfExtentZ - margin);
            var minDist = Mathf.Max(openWaterMinDistFromPlayer, outerRingRadiusMax + 2f);

            for (var i = 0; i < count; i++)
            {
                var defIndex = pool[(spawnIndex + i) % pool.Count];
                var def = defs[defIndex];
                var agentRadius = def != null ? def.radius + spawnSeparationPadding : 1.2f;
                var waterClearance = agentRadius + islandSpawnClearance;

                var placed = false;
                for (var attempt = 0; attempt < spawnAttemptsPerSlot; attempt++)
                {
                    var offsetX = (rng.DirectorFloat() * 2f - 1f) * limitX;
                    var offsetZ = (rng.DirectorFloat() * 2f - 1f) * limitZ;
                    var pos = boundsCenter + new Vector3(offsetX, 0f, offsetZ);

                    var toPlayer = pos - playerCenter;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude < minDist * minDist)
                        continue;

                    if (!PlayBounds.IsOpenWater(pos, waterClearance))
                        continue;

                    if (!IsValidSpawnPosition(pos, agentRadius))
                        continue;

                    if (_enemyWorld.Spawn(defIndex, pos) < 0)
                        continue;

                    _placedSpawns.Add(new PlacedSpawn { position = pos, radius = agentRadius });
                    placed = true;
                    break;
                }

                if (!placed)
                    _spawnWarnings++;
            }

            spawnIndex += count;
        }

        void SpawnAnnulus(
            List<int> pool,
            Vector3 center,
            EnemyDefinition[] defs,
            float radiusMin,
            float radiusMax,
            int count,
            ref int spawnIndex,
            RunRng rng,
            bool biasTowardInner,
            float separationScale,
            float innerBiasPower)
        {
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var defIndex = pool[(spawnIndex + i) % pool.Count];
                var def = defs[defIndex];
                var agentRadius = def != null
                    ? def.radius + spawnSeparationPadding * separationScale
                    : 1.2f;

                var placed = false;
                for (var attempt = 0; attempt < spawnAttemptsPerSlot; attempt++)
                {
                    var t = count > 1 ? i / (float)(count - 1) : 0.5f;
                    if (biasTowardInner && innerBiasPower > 1f)
                        t = Mathf.Pow(t, innerBiasPower);

                    var angle = rng.DirectorFloat() * 360f;
                    var radius = Mathf.Lerp(radiusMin, radiusMax, t) + attempt * 0.4f;
                    var rad = angle * Mathf.Deg2Rad;
                    var jitter = Vector3.zero;
                    if (positionJitter > 0f)
                    {
                        var wobble = (rng.DirectorFloat() * 2f - 1f) * positionJitter;
                        var wobbleZ = (rng.DirectorFloat() * 2f - 1f) * positionJitter;
                        jitter = new Vector3(wobble, 0f, wobbleZ);
                    }

                    var minRadius = GetMinSpawnRadiusFromPlayer(agentRadius);
                    radius = Mathf.Max(radius, minRadius);
                    var pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius) + jitter;
                    if (!IsValidSpawnPosition(pos, agentRadius))
                        continue;

                    if (_enemyWorld.Spawn(defIndex, pos) < 0)
                        continue;

                    _placedSpawns.Add(new PlacedSpawn { position = pos, radius = agentRadius });
                    placed = true;
                    break;
                }

                if (!placed)
                    _spawnWarnings++;
            }

            spawnIndex += count;
        }

        float GetMinSpawnRadiusFromPlayer(float agentRadius)
        {
            return DefaultPlayerHullRadius + playerSpawnClearance + agentRadius;
        }

        bool IsValidSpawnPosition(Vector3 position, float agentRadius)
        {
            if (_player != null)
            {
                var toPlayer = position - _player.Position;
                toPlayer.y = 0f;
                var minDist = GetMinSpawnRadiusFromPlayer(agentRadius);
                if (toPlayer.sqrMagnitude < minDist * minDist)
                    return false;
            }

            var islandRadius = agentRadius + islandSpawnClearance;
            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, islandRadius))
                return false;

            if (!PlayBounds.IsInside(position, islandRadius + 1f))
                return false;

            for (var i = 0; i < _placedSpawns.Count; i++)
            {
                var other = _placedSpawns[i];
                var delta = position - other.position;
                delta.y = 0f;
                var minDist = (agentRadius + other.radius) * spawnOverlapRelax;
                if (delta.sqrMagnitude < minDist * minDist)
                    return false;
            }

            return true;
        }

        void HideEnemyHealthBars()
        {
            var bars = FindObjectsByType<WorldHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < bars.Length; i++)
            {
                if (bars[i] != null)
                    bars[i].gameObject.SetActive(false);
            }
        }

        void HideMapBoundaryVisuals()
        {
            if (!hideMapBoundaryVisuals)
                return;

            var stormEdge = FindFirstObjectByType<V3StormEdgeVisual>();
            if (stormEdge != null)
                stormEdge.gameObject.SetActive(false);

            var boundsWalls = FindFirstObjectByType<V3PlayBoundsWallVisual>();
            if (boundsWalls != null)
                boundsWalls.gameObject.SetActive(false);

            var debugOverlay = FindFirstObjectByType<CollisionDebugOverlay>();
            if (debugOverlay != null)
                debugOverlay.enabled = false;
        }

        void HideScreenSpaceUi()
        {
            _disabledUiRoots.Clear();
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                    continue;
                if (!canvas.gameObject.activeSelf)
                    continue;
                _disabledUiRoots.Add(canvas.gameObject);
                canvas.gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            if (_encounterSpawner != null && disableEncounterSpawner)
                _encounterSpawner.enabled = _encounterSpawnerWasEnabled;

            if (_poiManager != null && stripPoisAndPickups)
                _poiManager.enabled = _poiManagerWasEnabled;

            if (_pickupWorld != null && stripPoisAndPickups)
                _pickupWorld.enabled = _pickupWorldWasEnabled;
        }
    }
}

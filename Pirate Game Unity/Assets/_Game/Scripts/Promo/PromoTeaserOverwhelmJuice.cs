using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.Promo
{
    /// <summary>Fake combat chaos for promo shots: damage numbers, hit flashes on the horde only.</summary>
    public sealed class PromoTeaserOverwhelmJuice : MonoBehaviour
    {
        struct DamageLabel
        {
            public Transform transform;
            public TextMesh mesh;
            public bool isCrit;
        }

        [SerializeField] int damageNumberCount = 240;
        [SerializeField] int hitFlashCount = 95;
        [SerializeField] float burnEnemyFraction = 0.28f;
        [SerializeField] bool flipDamageFacing = true;
        [SerializeField] float damageCharacterSize = 0.12f;
        [SerializeField] float damageSizeStep = 0.012f;
        [SerializeField] float damageCharacterSizeMin = 0.04f;
        [SerializeField] float damageCharacterSizeMax = 0.22f;

        readonly List<GameObject> _spawned = new(512);
        readonly List<DamageLabel> _damageLabels = new(256);
        Transform _root;
        Camera _camera;

        public bool FlipDamageFacing => flipDamageFacing;
        public float DamageCharacterSize => damageCharacterSize;

        public void Clear()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            }

            _spawned.Clear();
            _damageLabels.Clear();
        }

        public void ToggleFlipDamageFacing()
        {
            flipDamageFacing = !flipDamageFacing;
            RefreshDamageFacing();
        }

        public void AdjustDamageCharacterSizeBySteps(int steps)
        {
            if (steps == 0)
                return;

            damageCharacterSize = Mathf.Clamp(
                damageCharacterSize + steps * damageSizeStep,
                damageCharacterSizeMin,
                damageCharacterSizeMax);
            ApplyDamageLabelSizes();
        }

        public void Build(PlayerShipController player, EnemyWorld enemyWorld, RunRng rng)
        {
            Clear();
            if (player == null || enemyWorld == null)
                return;

            _root = new GameObject("PromoOverwhelmJuice").transform;
            _spawned.Add(_root.gameObject);
            _camera = Camera.main;

            var playerPos = player.Position;
            var agents = enemyWorld.Agents;
            var inner = new List<int>(128);
            var outer = new List<int>(256);
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var dist = Vector3.Distance(agents[i].position, playerPos);
                if (dist < 32f) inner.Add(i);
                else outer.Add(i);
            }

            SpawnDamageNumbers(enemyWorld, playerPos, rng, inner);
            SpawnHitFlashes(enemyWorld, rng, inner, outer);
            ApplyBurnVisuals(enemyWorld, rng, agents);
        }

        void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null || _damageLabels.Count == 0)
                return;

            for (var i = 0; i < _damageLabels.Count; i++)
            {
                var label = _damageLabels[i];
                if (label.transform == null)
                    continue;
                label.transform.rotation = FacingRotation(label.transform.position);
            }
        }

        Quaternion FacingRotation(Vector3 worldPosition)
        {
            if (_camera == null)
                return Quaternion.identity;

            var toCamera = _camera.transform.position - worldPosition;
            if (toCamera.sqrMagnitude < 0.0001f)
                return _camera.transform.rotation;

            var rot = Quaternion.LookRotation(toCamera.normalized, _camera.transform.up);
            if (flipDamageFacing)
                rot *= Quaternion.Euler(0f, 180f, 0f);
            return rot;
        }

        void RefreshDamageFacing()
        {
            for (var i = 0; i < _damageLabels.Count; i++)
            {
                var label = _damageLabels[i];
                if (label.transform == null)
                    continue;
                label.transform.rotation = FacingRotation(label.transform.position);
            }
        }

        void ApplyDamageLabelSizes()
        {
            for (var i = 0; i < _damageLabels.Count; i++)
            {
                var label = _damageLabels[i];
                if (label.mesh == null)
                    continue;
                label.mesh.characterSize = CritScaledSize(label.isCrit);
            }
        }

        float CritScaledSize(bool isCrit) => damageCharacterSize * (isCrit ? 1.08f : 1f);

        void SpawnDamageNumbers(EnemyWorld enemyWorld, Vector3 playerPos, RunRng rng, List<int> inner)
        {
            var agents = enemyWorld.Agents;

            for (var i = 0; i < damageNumberCount; i++)
            {
                var angle = rng.DirectorFloat() * Mathf.PI * 2f;
                var radius = 1.2f + rng.DirectorFloat() * 14f;
                var pos = playerPos + new Vector3(Mathf.Cos(angle) * radius, 1.8f + rng.DirectorFloat() * 2.2f, Mathf.Sin(angle) * radius);

                if (inner.Count > 0 && rng.DirectorFloat() < 0.82f)
                {
                    var agentIndex = inner[rng.DirectorInt(0, inner.Count)];
                    if (agentIndex >= 0 && agentIndex < agents.Length && agents[agentIndex].active)
                        pos = agents[agentIndex].position + Vector3.up * (1.5f + rng.DirectorFloat());
                }

                var value = rng.DirectorInt(18, 180);
                var isCrit = rng.DirectorFloat() < 0.18f;
                var go = new GameObject(isCrit ? "Dmg_Crit" : "Dmg_Num");
                go.transform.SetParent(_root, false);
                go.transform.position = pos;
                go.transform.rotation = FacingRotation(pos);
                var text = go.AddComponent<TextMesh>();
                text.text = value.ToString();
                text.fontSize = isCrit ? 64 : 48;
                text.characterSize = CritScaledSize(isCrit);
                text.anchor = TextAnchor.MiddleCenter;
                text.color = isCrit ? new Color(1f, 0.85f, 0.2f) : Color.white;
                text.fontStyle = isCrit ? FontStyle.Bold : FontStyle.Normal;
                _spawned.Add(go);
                _damageLabels.Add(new DamageLabel { transform = go.transform, mesh = text, isCrit = isCrit });
            }
        }

        void SpawnHitFlashes(EnemyWorld enemyWorld, RunRng rng, List<int> inner, List<int> outer)
        {
            var broker = JuiceVfxBroker.Instance;
            var settings = broker?.Settings ?? Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (settings == null || broker == null)
                return;

            var pool = inner.Count > 0 ? inner : outer;
            if (pool.Count == 0)
                return;

            for (var i = 0; i < hitFlashCount; i++)
            {
                var idx = pool[rng.DirectorInt(0, pool.Count)];
                var agents = enemyWorld.Agents;
                if (idx < 0 || idx >= agents.Length || !agents[idx].active)
                    continue;

                var pos = agents[idx].position + Vector3.up * 1.2f;
                var prefab = rng.DirectorFloat() < 0.25f ? settings.critHitPrefab : settings.enemyHitPrefab;
                if (prefab == null) continue;
                var scale = prefab == settings.critHitPrefab ? settings.critHitScale : settings.enemyHitScale;
                broker.Play(prefab, pos, Quaternion.identity, scale * (0.85f + rng.DirectorFloat() * 0.35f), 2.5f);
            }
        }

        void ApplyBurnVisuals(EnemyWorld enemyWorld, RunRng rng, EnemyAgent[] agents)
        {
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (rng.DirectorFloat() > burnEnemyFraction) continue;
                enemyWorld.SetBurnVisual(i, true);
            }
        }
    }
}

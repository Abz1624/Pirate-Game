using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Placeholder explosive barrels for Saltwind alternate reward routes (Step 06).</summary>
    public sealed class V3ExplosiveBarrelWorld : MonoBehaviour
    {
        sealed class BarrelState
        {
            public Vector3 Position;
            public bool Consumed;
            public float Fuse;
            public bool Armed;
        }

        readonly List<BarrelState> _barrels = new();

        public static V3ExplosiveBarrelWorld Ensure(StageLayout layout)
        {
            var existing = FindFirstObjectByType<V3ExplosiveBarrelWorld>();
            if (existing != null)
            {
                existing.ResetForLayout(layout);
                return existing;
            }

            var go = new GameObject("V3ExplosiveBarrelWorld");
            var world = go.AddComponent<V3ExplosiveBarrelWorld>();
            world.ResetForLayout(layout);
            return world;
        }

        public void ResetForLayout(StageLayout layout)
        {
            for (var c = transform.childCount - 1; c >= 0; c--)
                Destroy(transform.GetChild(c).gameObject);

            _barrels.Clear();
            if (layout?.explosiveBarrelPositions == null) return;

            var export = StageBalanceProfileContext.Export;
            var fuse = export?.Exploration?.ExplosiveBarrelFuseSeconds ?? 1.2f;
            for (var i = 0; i < layout.explosiveBarrelPositions.Count; i++)
            {
                _barrels.Add(new BarrelState { Position = layout.explosiveBarrelPositions[i], Fuse = fuse });
                V3PlaceholderFactory.CreatePoiMarker(POIType.SalvageChest, layout.explosiveBarrelPositions[i], transform);
            }
        }

        void Update()
        {
            if (!StageBalanceProfileContext.IsActive || RunManager.Instance == null || RunManager.Instance.IsPaused)
                return;
            if (RunManager.Instance.IsRunEnded) return;

            var player = FindFirstObjectByType<PlayerShipController>();
            var export = StageBalanceProfileContext.Export;
            var radius = export?.Exploration?.ExplosiveBarrelRadius ?? 4f;
            var damage = export?.Exploration?.ExplosiveBarrelDamage ?? 20;

            for (var i = 0; i < _barrels.Count; i++)
            {
                var barrel = _barrels[i];
                if (barrel.Consumed) continue;

                if (!barrel.Armed)
                {
                    if (player != null && Vector3.Distance(player.Position, barrel.Position) <= 2.5f)
                        barrel.Armed = true;
                    continue;
                }

                barrel.Fuse -= Time.deltaTime;
                if (!SaltwindExplosiveBarrelLogic.ShouldDetonate(barrel.Fuse))
                    continue;

                Detonate(barrel, player, radius, damage);
                barrel.Consumed = true;
            }
        }

        void Detonate(BarrelState barrel, PlayerShipController player, float radius, int damage)
        {
            if (player != null && Vector3.Distance(player.Position, barrel.Position) <= radius)
                player.Damage?.TakeDamage(damage, barrel.Position, false);

            var enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (enemyWorld?.Agents != null)
            {
                for (var i = 0; i < enemyWorld.Agents.Length; i++)
                {
                    if (!enemyWorld.Agents[i].active) continue;
                    if (Vector3.Distance(enemyWorld.Agents[i].position, barrel.Position) <= radius)
                        enemyWorld.ApplyDamage(i, damage, false, false);
                }
            }

            V3ToastHud.Instance?.Show("Powder barrel detonation!", 1.5f);
        }
    }
}

using System;
using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Weapons
{
    public sealed class WeaponInstance
    {
        public WeaponDefinition def;
        public int level = 1;
        public float cooldownTimer;
        public float damageBonus = 1f;
        public int quantityBonus;
        public float cooldownMult = 1f;
        public int orbitIndex;
    }

    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] int maxSlots = 2;

        readonly List<WeaponInstance> _weapons = new();
        PlayerShipController _player;
        EnemyWorld _enemies;
        ProjectileWorld _projectiles;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;

        void Awake()
        {
            _player = FindFirstObjectByType<PlayerShipController>();
            _enemies = FindFirstObjectByType<EnemyWorld>();
            _projectiles = FindFirstObjectByType<ProjectileWorld>();
        }

        public void Initialize(GameDatabase db, string startingWeaponId)
        {
            database = db;
            _weapons.Clear();
            EquipWeapon(startingWeaponId);
        }

        public bool EquipWeapon(string weaponId)
        {
            if (_weapons.Count >= maxSlots) return false;
            var def = database.GetWeapon(weaponId);
            if (def == null) return false;
            _weapons.Add(new WeaponInstance { def = def, level = 1 });
            return true;
        }

        public void UpgradeWeapon(int index, Rarity rarity)
        {
            if (index < 0 || index >= _weapons.Count) return;
            var w = _weapons[index];
            if (w.level >= w.def.maxLevel) return;
            w.level++;
            var mult = GameConstants.RarityMultipliers[(int)rarity];
            w.damageBonus *= 1f + 0.08f * mult;
            w.quantityBonus += w.level % 2 == 0 ? 1 : 0;
            w.cooldownMult *= 1f - 0.05f * mult;
            _weapons[index] = w;
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (_player == null || _enemies == null || _projectiles == null) return;
            TickWeapons(Time.deltaTime);
        }

        public void TickWeapons(float dt)
        {
            var stats = RunManager.Instance.Stats;
            var rng = RunManager.Instance.Rng;
            for (var wi = 0; wi < _weapons.Count; wi++)
            {
                var w = _weapons[wi];
                w.cooldownTimer -= dt;
                if (w.cooldownTimer > 0f) { _weapons[wi] = w; continue; }

                var cd = w.def.baseCooldown * w.cooldownMult / stats.AttackSpeed;
                w.cooldownTimer = cd;

                switch (w.def.pattern)
                {
                    case WeaponPattern.AutoProjectile:
                        FireProjectile(ref w, stats, rng, 1);
                        break;
                    case WeaponPattern.FanVolley:
                        FireFan(ref w, stats, rng, 3 + w.quantityBonus);
                        break;
                    case WeaponPattern.Orbit:
                        FireOrbit(ref w, stats, rng);
                        break;
                    case WeaponPattern.TrailZone:
                        FireTrail(ref w, stats, rng);
                        break;
                    case WeaponPattern.MineDrop:
                        FireMine(ref w, stats, rng);
                        break;
                    case WeaponPattern.ChainLightning:
                        FireLightning(ref w, stats, rng);
                        break;
                    case WeaponPattern.AuraPulse:
                        FireAura(ref w, stats, rng);
                        break;
                }
                _weapons[wi] = w;
            }
        }

        void FireProjectile(ref WeaponInstance w, PlayerStats stats, RunRng rng, int count)
        {
            if (!_enemies.TryGetNearest(_player.Position, w.def.range, out var target, w.def.targeting == TargetingMode.NearestElite))
                return;
            var enemy = _enemies.Agents[target];
            var dir = (enemy.position - _player.Position).normalized;
            var qty = w.def.baseQuantity + w.quantityBonus + Mathf.FloorToInt(stats.Get(StatId.Quantity));
            for (var i = 0; i < qty; i++)
            {
                var spread = Quaternion.Euler(0f, (i - qty * 0.5f) * 4f, 0f) * dir;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus * w.level * 0.1f + 1f, stats, enemy.isElite, 0f, rng, w.def.procCoefficient);
                _projectiles.TrySpawn(_player.Position + Vector3.up, spread, w.def.projectileSpeed * stats.ProjectileSpeed, dmg, 3f, 0.35f * stats.Size, 0, dmg > w.def.baseDamage * 1.4f, w.def.projectileColor);
            }
        }

        void FireFan(ref WeaponInstance w, PlayerStats stats, RunRng rng, int pellets)
        {
            if (!_enemies.TryGetNearest(_player.Position, w.def.range, out var target, false)) return;
            var enemy = _enemies.Agents[target];
            var baseDir = (enemy.position - _player.Position).normalized;
            for (var i = 0; i < pellets; i++)
            {
                var angle = -25f + (50f / Mathf.Max(1, pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.6f, w.damageBonus, stats, enemy.isElite, 0f, rng, w.def.procCoefficient);
                _projectiles.TrySpawn(_player.Position + Vector3.up, dir, w.def.projectileSpeed * stats.ProjectileSpeed, dmg, 2.5f, 0.25f * stats.Size, 0, false, w.def.projectileColor);
            }
        }

        void FireOrbit(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            w.orbitIndex++;
            var count = 2 + w.quantityBonus;
            for (var i = 0; i < count; i++)
            {
                var angle = (w.orbitIndex * 20f + i * (360f / count)) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (2.5f * stats.Size);
                var pos = _player.Position + offset;
                if (_enemies.TryGetNearest(pos, 2f, out var target, false))
                {
                    var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, _enemies.Agents[target].isElite, 0f, rng, w.def.procCoefficient);
                    _enemies.ApplyDamage(target, dmg, false);
                }
            }
        }

        void FireTrail(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (_enemies.TryGetNearest(_player.Position, 3f, out var target, false))
            {
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.5f, w.damageBonus, stats, _enemies.Agents[target].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(target, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(target, Core.StatusType.Burn, 1f);
            }
        }

        void FireMine(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var pos = _player.Position - _player.Velocity.normalized * 1.5f;
            if (_enemies.TryGetNearest(pos, 2.5f * stats.Size, out var target, false))
            {
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 1.5f, w.damageBonus, stats, _enemies.Agents[target].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(target, dmg, false);
            }
        }

        void FireLightning(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (!_enemies.TryGetNearest(_player.Position, w.def.range, out var target, false)) return;
            var chains = 2 + w.quantityBonus;
            var visited = new HashSet<int>();
            var current = target;
            for (var c = 0; c < chains; c++)
            {
                if (current < 0 || visited.Contains(current)) break;
                visited.Add(current);
                var agent = _enemies.Agents[current];
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, agent.isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(current, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(current, Core.StatusType.Shock, 0.5f);
                current = -1;
                var list = _enemies.Agents;
                var best = float.MaxValue;
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].active || visited.Contains(i)) continue;
                    var d = Vector3.Distance(agent.position, list[i].position);
                    if (d < 6f && d < best) { best = d; current = i; }
                }
            }
        }

        void FireAura(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var list = _enemies.Agents;
            for (var i = 0; i < list.Length; i++)
            {
                if (!list[i].active) continue;
                if (Vector3.Distance(list[i].position, _player.Position) > 4f * stats.Size) continue;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.35f, w.damageBonus, stats, list[i].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(i, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(i, Core.StatusType.Curse, 0.4f);
            }
        }
    }
}

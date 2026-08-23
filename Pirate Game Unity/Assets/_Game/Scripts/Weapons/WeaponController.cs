using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.VFX;
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
        public float speedMult = 1f;
        public float sizeMult = 1f;
        public float durationMult = 1f;
        public float knockbackMult = 1f;
        public int orbitIndex;
        public readonly Dictionary<UpgradeAxis, float> axisAccumulated = new();
    }

    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] int maxSlots = 2;
        [SerializeField] DemoPacingSettings demoPacing;

        readonly List<WeaponInstance> _weapons = new();
        PlayerShipController _player;
        EnemyWorld _enemies;
        ProjectileWorld _projectiles;
        WeaponJuiceController _juice;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;

        void Awake()
        {
            _player = FindFirstObjectByType<PlayerShipController>();
            _enemies = FindFirstObjectByType<EnemyWorld>();
            _projectiles = FindFirstObjectByType<ProjectileWorld>();
            _juice = FindFirstObjectByType<WeaponJuiceController>();
            if (demoPacing == null)
                demoPacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
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
            _weapons.Add(new WeaponInstance
            {
                def = def,
                level = 1,
                damageBonus = demoPacing != null ? demoPacing.startingDamageMultiplier : 1f
            });
            return true;
        }

        public int FindWeaponIndex(string weaponId)
        {
            for (var i = 0; i < _weapons.Count; i++)
                if (_weapons[i].def.id == weaponId) return i;
            return -1;
        }

        public void UpgradeWeapon(int index, Rarity rarity)
        {
            if (index < 0 || index >= _weapons.Count) return;
            var w = _weapons[index];
            if (w.level >= w.def.maxLevel) return;
            var mult = GameConstants.RarityMultipliers[(int)rarity];
            var upgradeMult = demoPacing != null ? demoPacing.upgradePowerMultiplier : 1f;
            ApplyAxisValue(ref w, UpgradeAxis.Damage, 0.08f * mult * upgradeMult);
            w.level++;
            if (w.level % 2 == 0)
                ApplyAxisValue(ref w, UpgradeAxis.Quantity, 1f);
            ApplyAxisValue(ref w, UpgradeAxis.Cooldown, 0.05f * mult);
            _weapons[index] = w;
        }

        public void ApplyUpgradePackage(int index, WeaponUpgradeLine[] lines)
        {
            if (index < 0 || index >= _weapons.Count || lines == null) return;
            var w = _weapons[index];
            if (w.level >= w.def.maxLevel) return;
            for (var i = 0; i < lines.Length; i++)
                ApplyAxisValue(ref w, lines[i].axis, lines[i].value);
            w.level++;
            _weapons[index] = w;
        }

        void ApplyAxisValue(ref WeaponInstance w, UpgradeAxis axis, float value)
        {
            if (value <= 0f) return;
            w.axisAccumulated.TryGetValue(axis, out var accumulated);
            if (w.def.TryGetAxisConfig(axis, out var cfg) && cfg.maxAccumulated > 0f)
            {
                var room = Mathf.Max(0f, cfg.maxAccumulated - accumulated);
                value = Mathf.Min(value, room);
                if (value <= 0f) return;
            }

            w.axisAccumulated[axis] = accumulated + value;
            switch (axis)
            {
                case UpgradeAxis.Damage:
                    w.damageBonus *= 1f + value;
                    break;
                case UpgradeAxis.Cooldown:
                    w.cooldownMult *= 1f - Mathf.Clamp01(value);
                    break;
                case UpgradeAxis.Quantity:
                    w.quantityBonus += Mathf.Max(1, Mathf.RoundToInt(value));
                    break;
                case UpgradeAxis.Speed:
                    w.speedMult *= 1f + value;
                    break;
                case UpgradeAxis.Size:
                    w.sizeMult *= 1f + value;
                    break;
                case UpgradeAxis.Duration:
                    w.durationMult *= 1f + value;
                    break;
                case UpgradeAxis.Knockback:
                    w.knockbackMult *= 1f + value;
                    break;
            }
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
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            var size = 0.35f * w.sizeMult * stats.Size;
            for (var i = 0; i < qty; i++)
            {
                var spread = Quaternion.Euler(0f, (i - qty * 0.5f) * 4f, 0f) * dir;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus * w.level * 0.1f + 1f, stats, enemy.isElite, 0f, rng, w.def.procCoefficient);
                var origin = _player.Position + Vector3.up * 0.35f;
                _juice?.OnProjectileFired(origin, spread);
                _projectiles.TrySpawn(origin, spread, speed, dmg, 3f, size, 0, dmg > w.def.baseDamage * 1.4f, w.def.projectileColor);
            }
        }

        void FireFan(ref WeaponInstance w, PlayerStats stats, RunRng rng, int pellets)
        {
            if (!_enemies.TryGetNearest(_player.Position, w.def.range, out var target, false)) return;
            var enemy = _enemies.Agents[target];
            var baseDir = (enemy.position - _player.Position).normalized;
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            var size = 0.25f * w.sizeMult * stats.Size;
            for (var i = 0; i < pellets; i++)
            {
                var angle = -25f + (50f / Mathf.Max(1, pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.6f, w.damageBonus, stats, enemy.isElite, 0f, rng, w.def.procCoefficient);
                var origin = _player.Position + Vector3.up * 0.35f;
                _juice?.OnProjectileFired(origin, dir);
                _projectiles.TrySpawn(origin, dir, speed, dmg, 2.5f, size, 0, false, w.def.projectileColor);
            }
        }

        void FireOrbit(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            w.orbitIndex++;
            var count = 2 + w.quantityBonus;
            var radius = 2.5f * w.sizeMult * stats.Size;
            for (var i = 0; i < count; i++)
            {
                var angle = (w.orbitIndex * 20f + i * (360f / count)) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                var pos = _player.Position + offset;
                if (_enemies.TryGetNearest(pos, 2f * w.sizeMult, out var target, false))
                {
                    var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, _enemies.Agents[target].isElite, 0f, rng, w.def.procCoefficient);
                    _enemies.ApplyDamage(target, dmg, false);
                }
            }
        }

        void FireTrail(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var radius = 3f * w.sizeMult * stats.Size;
            if (_enemies.TryGetNearest(_player.Position, radius, out var target, false))
            {
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.5f, w.damageBonus, stats, _enemies.Agents[target].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(target, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(target, Core.StatusType.Burn, w.def.duration * w.durationMult);
            }
        }

        void FireMine(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var back = _player.Velocity.sqrMagnitude > 0.01f
                ? -_player.Velocity.normalized
                : -_player.MovementForward;
            var pos = _player.Position + back * 1.5f;
            pos.y = 0.15f;
            _juice?.OnMineDropped(pos);
            if (_enemies.TryGetNearest(pos, 2.5f * w.sizeMult * stats.Size, out var target, false))
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
                _juice?.OnLightningStrike(agent.position);
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, agent.isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(current, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(current, Core.StatusType.Shock, 0.5f * w.durationMult);
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
            var radius = 4f * w.sizeMult * stats.Size;
            var list = _enemies.Agents;
            for (var i = 0; i < list.Length; i++)
            {
                if (!list[i].active) continue;
                if (Vector3.Distance(list[i].position, _player.Position) > radius) continue;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.35f, w.damageBonus, stats, list[i].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(i, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(i, Core.StatusType.Curse, 0.4f * w.durationMult);
            }
        }
    }
}

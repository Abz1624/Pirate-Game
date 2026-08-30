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
        readonly List<PowderKeg> _powderKegs = new();
        float _cutlassHitTimer;
        PlayerShipController _player;
        EnemyWorld _enemies;
        ProjectileWorld _projectiles;
        WeaponJuiceController _juice;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;

        struct PowderKeg
        {
            public bool active;
            public Vector3 position;
            public float armTime;
            public float lifeTime;
            public float triggerRadius;
            public float blastRadius;
            public float baseDamage;
            public float damageBonus;
            public float procCoefficient;
            public Transform visual;
        }

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
            TickCutlassDamage(Time.deltaTime);
        }

        public void TickWeapons(float dt)
        {
            var stats = RunManager.Instance.Stats;
            var rng = RunManager.Instance.Rng;
            TickPowderKegs(dt, stats, rng);
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
                        if (w.def.id == "flamethrower" || w.def.id == "burning_oil")
                            FireFlamethrower(ref w, stats, rng);
                        else
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
            var spread = w.def.fanSpreadDegrees;
            var halfSpread = spread * 0.5f;
            for (var i = 0; i < pellets; i++)
            {
                var angle = pellets <= 1
                    ? 0f
                    : -halfSpread + (spread / (pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.6f, w.damageBonus, stats, enemy.isElite, 0f, rng, w.def.procCoefficient);
                var origin = _player.Position + Vector3.up * 0.35f;
                _juice?.OnProjectileFired(origin, dir);
                _projectiles.TrySpawn(origin, dir, speed, dmg, 2.5f, size, 0, false, w.def.projectileColor);
            }
        }

        void FireOrbit(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            // Cutlass damage is applied continuously in TickCutlassDamage.
        }

        void TickCutlassDamage(float dt)
        {
            if (_juice == null || _enemies == null || RunManager.Instance == null) return;

            var hits = _juice.CutlassHits;
            if (hits.Count == 0) return;

            WeaponInstance cutlassWeapon = null;
            for (var wi = 0; wi < _weapons.Count; wi++)
            {
                if (_weapons[wi].def.id != "flying_cutlasses") continue;
                cutlassWeapon = _weapons[wi];
                break;
            }

            if (cutlassWeapon == null) return;

            _cutlassHitTimer -= dt;
            if (_cutlassHitTimer > 0f) return;
            _cutlassHitTimer = 0.12f;

            var w = cutlassWeapon;
            var stats = RunManager.Instance.Stats;
            var rng = RunManager.Instance.Rng;
            var agents = _enemies.Agents;
            var hitThisTick = new HashSet<int>();

            for (var h = 0; h < hits.Count; h++)
            {
                var sphere = hits[h];
                var radius = sphere.radius * w.sizeMult * stats.Size;
                var radiusSq = radius * radius;
                for (var i = 0; i < agents.Length; i++)
                {
                    if (!agents[i].active || hitThisTick.Contains(i)) continue;
                    var delta = agents[i].position - sphere.position;
                    if (delta.sqrMagnitude > radiusSq) continue;

                    hitThisTick.Add(i);
                    var dmg = DamageSystem.RollDamage(
                        w.def.baseDamage * 0.35f,
                        w.damageBonus,
                        stats,
                        agents[i].isElite,
                        0f,
                        rng,
                        w.def.procCoefficient);
                    _enemies.ApplyDamage(i, dmg, false);
                }
            }
        }

        void FireFlamethrower(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var forward = _player.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var origin = _player.Position + forward * 0.8f + right * 1.4f;
            var range = w.def.range * 0.65f * w.sizeMult * stats.Size;
            var halfAngle = 22f;
            var burnDuration = w.def.duration * w.durationMult * stats.Duration;
            _juice?.OnFlamethrowerBurst(origin, right);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var toEnemy = agents[i].position - origin;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude < 0.01f) continue;
                if (toEnemy.magnitude > range) continue;
                if (Vector3.Angle(right, toEnemy) > halfAngle) continue;

                var dmg = DamageSystem.RollDamage(
                    w.def.baseDamage,
                    w.damageBonus,
                    stats,
                    agents[i].isElite,
                    0f,
                    rng,
                    w.def.procCoefficient);
                _enemies.ApplyDamage(i, dmg, false);
                StatusController.Instance?.ApplyStatus(i, StatusType.Burn, burnDuration);
            }
        }

        void FireTrail(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var back = _player.Velocity.sqrMagnitude > 0.01f
                ? -_player.Velocity.normalized
                : -_player.MovementForward;
            var pos = _player.Position + back * 1.4f;
            pos.y = 0.2f;

            var radius = 2.8f * w.sizeMult * stats.Size;
            var burnDuration = w.def.duration * w.durationMult * stats.Duration;
            _juice?.OnBurningOilDropped(pos, radius);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (Vector3.Distance(agents[i].position, pos) > radius) continue;
                var dmg = DamageSystem.RollDamage(
                    w.def.baseDamage * 0.65f,
                    w.damageBonus,
                    stats,
                    agents[i].isElite,
                    0f,
                    rng,
                    w.def.procCoefficient);
                _enemies.ApplyDamage(i, dmg, false);
                Combat.StatusController.Instance?.ApplyStatus(i, Core.StatusType.Burn, burnDuration);
            }
        }

        void FireMine(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var back = _player.Velocity.sqrMagnitude > 0.01f
                ? -_player.Velocity.normalized
                : -_player.MovementForward;
            var pos = _player.Position + back * 1.5f;
            pos.y = 0.55f;
            var visual = _juice?.OnMineDropped(pos);
            _powderKegs.Add(new PowderKeg
            {
                active = true,
                position = pos,
                armTime = 0.2f,
                lifeTime = 45f,
                triggerRadius = 3f * w.sizeMult * stats.Size,
                blastRadius = 2.8f * w.sizeMult * stats.Size,
                baseDamage = w.def.baseDamage,
                damageBonus = w.damageBonus,
                procCoefficient = w.def.procCoefficient,
                visual = visual
            });
        }

        void TickPowderKegs(float dt, PlayerStats stats, RunRng rng)
        {
            if (_enemies == null) return;

            for (var i = _powderKegs.Count - 1; i >= 0; i--)
            {
                var keg = _powderKegs[i];
                if (!keg.active)
                {
                    _powderKegs.RemoveAt(i);
                    continue;
                }

                keg.armTime -= dt;
                keg.lifeTime -= dt;
                if (keg.lifeTime <= 0f)
                {
                    if (keg.visual != null)
                        Destroy(keg.visual.gameObject);
                    _powderKegs.RemoveAt(i);
                    continue;
                }

                if (keg.armTime > 0f)
                {
                    _powderKegs[i] = keg;
                    continue;
                }

                var agents = _enemies.Agents;
                var triggered = false;
                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (Vector3.Distance(agents[ai].position, keg.position) > keg.triggerRadius) continue;
                    triggered = true;
                    break;
                }

                if (!triggered)
                {
                    _powderKegs[i] = keg;
                    continue;
                }

                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (Vector3.Distance(agents[ai].position, keg.position) > keg.blastRadius) continue;
                    var dmg = DamageSystem.RollDamage(
                        keg.baseDamage * 1.5f,
                        keg.damageBonus,
                        stats,
                        agents[ai].isElite,
                        0f,
                        rng,
                        keg.procCoefficient);
                    _enemies.ApplyDamage(ai, dmg, false);
                }

                _juice?.OnMineExploded(keg.position, keg.blastRadius);
                if (keg.visual != null)
                    Destroy(keg.visual.gameObject);
                _powderKegs.RemoveAt(i);
            }
        }

        void FireLightning(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (!_enemies.TryGetNearest(_player.Position, w.def.range, out var target, false)) return;

            var strikePos = _enemies.Agents[target].position;
            strikePos.y = 0f;
            var radius = (3.5f + w.quantityBonus * 0.5f) * w.sizeMult * stats.Size;
            _juice?.OnLightningStrike(strikePos, radius);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (Vector3.Distance(agents[i].position, strikePos) > radius) continue;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, agents[i].isElite, 0f, rng, w.def.procCoefficient);
                _enemies.ApplyDamage(i, dmg, false);
                StatusController.Instance?.ApplyStatus(i, StatusType.Shock, 0.5f * w.durationMult);
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

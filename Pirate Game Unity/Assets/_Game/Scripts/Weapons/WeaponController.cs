using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3;
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
        public bool swivelAwaitingSecond;
    }

    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] int maxSlots = GameConstants.DefaultWeaponSlots;
        [SerializeField] DemoPacingSettings demoPacing;

        readonly List<WeaponInstance> _weapons = new();
        readonly List<PowderKeg> _powderKegs = new();
        readonly List<DelayedStrike> _delayedStrikes = new();
        readonly List<BoardingSkiff> _boardingSkiffs = new();
        readonly List<PendingBroadside> _pendingBroadsides = new();
        readonly List<Vector3> _laneHitScratch = new();
        readonly List<int> _laneIndexScratch = new();
        float _cutlassHitTimer;
        float _orbitVolleyTimer;
        Transform _skiffVisualRoot;
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

        struct DelayedStrike
        {
            public bool active;
            public Vector3 position;
            public float timer;
            public float radius;
            public float baseDamage;
            public float damageBonus;
            public float procCoefficient;
            public string weaponId;
        }

        struct BoardingSkiff
        {
            public bool active;
            public int targetIndex;
            public Vector3 position;
            public Vector3 spawnStart;
            public float spawnAnimTime;
            public float spawnAnimDuration;
            public float strikeCooldown;
            public float lifeTime;
            public float baseDamage;
            public float damageBonus;
            public float procCoefficient;
            public string weaponId;
            public Transform visual;
        }

        struct PendingBroadside
        {
            public bool active;
            public float timer;
            public Vector3 sideDirection;
            public WeaponInstance weapon;
        }

        void Awake()
        {
            maxSlots = GameConstants.DefaultWeaponSlots;
            _player = FindFirstObjectByType<PlayerShipController>();
            _enemies = FindFirstObjectByType<EnemyWorld>();
            _projectiles = FindFirstObjectByType<ProjectileWorld>();
            _juice = FindFirstObjectByType<WeaponJuiceController>();
            if (demoPacing == null)
                demoPacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
        }

        public void ConfigureV3() => ApplyCombatExperiment(CombatExperimentContext.Active);

        public void ApplyCombatExperiment(CombatExperimentProfile profile)
        {
            profile ??= CombatExperimentContext.Active;
            maxSlots = profile.weaponSlotCap;
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
            _weapons.Add(CreateWeaponInstance(def));
            return true;
        }

        /// <summary>Playtest: one weapon equipped, clears traps/summons.</summary>
        public void SetSoloWeapon(string weaponId)
        {
            if (database == null || string.IsNullOrEmpty(weaponId)) return;
            _weapons.Clear();
            _powderKegs.Clear();
            _boardingSkiffs.Clear();
            _delayedStrikes.Clear();
            _pendingBroadsides.Clear();
            EquipWeapon(weaponId);
        }

        public bool ReplaceWeapon(int index, string weaponId)
        {
            if (index < 0 || index >= _weapons.Count) return false;
            var def = database.GetWeapon(weaponId);
            if (def == null) return false;
            _weapons[index] = CreateWeaponInstance(def);
            return true;
        }

        WeaponInstance CreateWeaponInstance(WeaponDefinition def) => new()
        {
            def = def,
            level = 1,
            damageBonus = demoPacing != null ? demoPacing.startingDamageMultiplier : 1f
        };

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
            _orbitVolleyTimer = Mathf.Max(0f, _orbitVolleyTimer - dt);
            TickPowderKegs(dt, stats, rng);
            TickDelayedStrikes(dt, stats, rng);
            TickBoardingSkiffs(dt, stats, rng);
            TickPendingBroadsides(dt, stats, rng);
            var itemAttackSpeed = V3RunItemEffects.Instance?.GetAttackSpeedMultiplier() ?? 1f;
            for (var wi = 0; wi < _weapons.Count; wi++)
            {
                var w = _weapons[wi];
                w.cooldownTimer -= dt * itemAttackSpeed;
                if (w.cooldownTimer > 0f) { _weapons[wi] = w; continue; }

                var cd = w.def.baseCooldown * w.cooldownMult / (stats.AttackSpeed * itemAttackSpeed);

                if (w.def.id == "swivel_guns" && w.swivelAwaitingSecond)
                {
                    FireSwivelShot(ref w, stats, rng);
                    w.swivelAwaitingSecond = false;
                    w.cooldownTimer = cd;
                    WeaponDamageLedger.RecordEmission(w.def.id);
                    _weapons[wi] = w;
                    continue;
                }

                w.cooldownTimer = cd;

                switch (w.def.pattern)
                {
                    case WeaponPattern.AutoProjectile:
                        if (w.def.id == "swivel_guns")
                            BeginSwivelBurst(ref w, stats, rng);
                        else
                            FireProjectile(ref w, stats, rng, 1);
                        break;
                    case WeaponPattern.FanVolley:
                        FireFan(ref w, stats, rng, ResolveFanPelletCount(w));
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
                    case WeaponPattern.PredictedAoE:
                        FireMortar(ref w, stats, rng);
                        break;
                    case WeaponPattern.SummonChase:
                        FireBoardingParty(ref w, stats, rng);
                        break;
                }
                WeaponDamageLedger.RecordEmission(w.def.id);
                _weapons[wi] = w;
            }
        }

        void FireSwivelShot(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out var target, out var dir))
                return;

            var enemy = _enemies.Agents[target];
            NotifyFire(w.def, dir, target);
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            var settings = JuiceVfxBroker.Instance != null ? JuiceVfxBroker.Instance.Settings : null;
            var sizeMult = settings != null ? settings.swivelProjectileSizeMult : 0.55f;
            var size = 0.35f * w.sizeMult * stats.Size * sizeMult;
            var damageMult = settings != null ? settings.swivelDamageMult : 0.55f;
            var dmg = DamageSystem.RollDamageDetailed(
                w.def.baseDamage * damageMult,
                w.damageBonus * w.level * 0.1f + 1f,
                stats,
                enemy.isElite,
                0f,
                rng,
                w.def.procCoefficient,
                w.def.id);
            var origin = _player.Position + Vector3.up * 0.35f;
            _juice?.OnWeaponFired(w.def.id, origin, dir);
            _projectiles.TrySpawn(origin, dir, speed, dmg.amount, 2.4f, size, 0, dmg.isCrit, w.def.projectileColor, w.def.id);
        }

        void BeginSwivelBurst(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            FireSwivelShot(ref w, stats, rng);
            var gap = JuiceVfxBroker.Instance?.Settings != null
                ? JuiceVfxBroker.Instance.Settings.swivelBetweenShotDelay
                : 0.18f;
            w.swivelAwaitingSecond = true;
            w.cooldownTimer = gap;
        }

        public void CaptainFireAllWeapons()
        {
            if (RunManager.Instance == null || _enemies == null || _player == null) return;
            var stats = RunManager.Instance.Stats;
            var rng = RunManager.Instance.Rng;
            for (var wi = 0; wi < _weapons.Count; wi++)
            {
                var w = _weapons[wi];
                if (w.def.pattern == WeaponPattern.Orbit)
                {
                    _orbitVolleyTimer = 2f;
                    continue;
                }
                FireWeaponOnce(ref w, stats, rng);
                WeaponDamageLedger.RecordEmission(w.def.id);
                _weapons[wi] = w;
            }
        }

        public void CaptainArmPowderKegs()
        {
            for (var i = 0; i < _powderKegs.Count; i++)
            {
                var keg = _powderKegs[i];
                if (!keg.active) continue;
                keg.armTime = 0f;
                _powderKegs[i] = keg;
            }
        }

        public void CaptainFireLightningSalvo()
        {
            if (RunManager.Instance == null || _enemies == null || _player == null) return;
            var stats = RunManager.Instance.Stats;
            var rng = RunManager.Instance.Rng;
            for (var wi = 0; wi < _weapons.Count; wi++)
            {
                var w = _weapons[wi];
                if (w.def.id != "chain_lightning" && w.def.id != "lightning_rod") continue;
                FireLightning(ref w, stats, rng);
                WeaponDamageLedger.RecordEmission(w.def.id);
                _weapons[wi] = w;
                return;
            }

            if (!_enemies.TryGetNearest(_player.Position, 18f, out var target, false)) return;
            var strikePos = _enemies.Agents[target].position;
            strikePos.y = 0f;
            const float radius = 3.5f;
            _juice?.OnLightningStrike(strikePos, radius);
            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (Vector3.Distance(agents[i].position, strikePos) > radius) continue;
                var dmg = DamageSystem.RollDamage(12f, 0f, stats, agents[i].isElite, 0f, rng, 0.35f, "captain_roxie_salvo");
                _enemies.ApplyDamage(i, dmg, false);
            }
        }

        void FireWeaponOnce(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            switch (w.def.pattern)
            {
                case WeaponPattern.AutoProjectile:
                    if (w.def.id == "swivel_guns")
                        BeginSwivelBurst(ref w, stats, rng);
                    else
                        FireProjectile(ref w, stats, rng, 1);
                    break;
                case WeaponPattern.FanVolley:
                    FireFan(ref w, stats, rng, ResolveFanPelletCount(w));
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
                case WeaponPattern.PredictedAoE:
                    FireMortar(ref w, stats, rng);
                    break;
                case WeaponPattern.SummonChase:
                    FireBoardingParty(ref w, stats, rng);
                    break;
            }
        }

        void FireProjectile(ref WeaponInstance w, PlayerStats stats, RunRng rng, int count)
        {
            if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out var target, out var dir))
                return;

            if (w.def.id == "long_nine" || WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) == "stormshot_cannon")
            {
                FirePierceLane(ref w, stats, rng, dir, target);
                return;
            }

            var enemy = _enemies.Agents[target];
            NotifyFire(w.def, dir, target);
            var qty = w.def.baseQuantity + w.quantityBonus + Mathf.FloorToInt(stats.Get(StatId.Quantity));
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            var size = 0.35f * w.sizeMult * stats.Size;
            for (var i = 0; i < qty; i++)
            {
                var spread = Quaternion.Euler(0f, (i - qty * 0.5f) * 4f, 0f) * dir;
                var dmg = DamageSystem.RollDamageDetailed(w.def.baseDamage, w.damageBonus * w.level * 0.1f + 1f, stats, enemy.isElite, 0f, rng, w.def.procCoefficient, w.def.id);
                var origin = _player.Position + Vector3.up * 0.35f;
                _juice?.OnWeaponFired(w.def.id, origin, spread);
                _projectiles.TrySpawn(origin, spread, speed, dmg.amount, 3f, size, 0, dmg.isCrit, w.def.projectileColor, w.def.id);
                if (w.def.targeting == TargetingMode.TetherTarget)
                {
                    StatusController.Instance?.ApplyStatus(target, StatusType.Tarred, 1.2f * w.durationMult);
                    WeaponDamageLedger.RecordControl(w.def.id, 1.2f * w.durationMult);
                }
            }
        }

        static int ResolveFanPelletCount(WeaponInstance w)
        {
            if (w.def != null && w.def.id == "broadside_battery")
                return 3;
            return 3 + w.quantityBonus;
        }

        void FireFan(ref WeaponInstance w, PlayerStats stats, RunRng rng, int pellets)
        {
            Vector3 baseDir;
            int notifyTarget = -1;
            if (w.def.targeting == TargetingMode.PortStarboard)
            {
                if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out notifyTarget, out baseDir))
                    return;
            }
            else if (w.def.targeting == TargetingMode.ForwardCone)
            {
                baseDir = _player.MovementForward;
                baseDir.y = 0f;
                if (baseDir.sqrMagnitude < 0.0001f) return;
                baseDir.Normalize();
                var arc = WeaponTargeting.ResolveArcDegrees(w.def);
                var origin = _player.Position;
                if (!WeaponTargeting.HasEnemyInForwardArc(_enemies, origin, baseDir, w.def.range, arc))
                    return;
            }
            else if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out var fanTarget, out baseDir))
            {
                return;
            }
            else
            {
                notifyTarget = fanTarget;
            }

            NotifyFire(w.def, baseDir, notifyTarget);

            if (w.def.id == "broadside_battery")
                _juice?.OnBroadsideSalvo(_player.Position, baseDir);

            var isBroadside = w.def.id == "broadside_battery";
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            if (isBroadside)
                speed = Mathf.Max(speed, 42f);
            var size = 0.25f * w.sizeMult * stats.Size;
            var spread = w.def.fanSpreadDegrees;
            var halfSpread = spread * 0.5f;
            var shotBasis = baseDir;

            var hits = 0;
            for (var i = 0; i < pellets; i++)
            {
                var angle = pellets <= 1 ? 0f : -halfSpread + (spread / (pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * shotBasis;
                var origin = _player.Position + Vector3.up * 0.35f;
                if (w.def.targeting == TargetingMode.PortStarboard)
                {
                    var sideSign = Vector3.Dot(dir, Vector3.Cross(Vector3.up, _player.MovementForward).normalized) >= 0f ? 1f : -1f;
                    origin += Vector3.Cross(Vector3.up, _player.MovementForward).normalized * sideSign * 0.85f;
                }

                var end = origin + dir * w.def.range;
                var hitRadius = isBroadside ? 1.05f : 0.8f;
                var hitIndex = FindFirstEnemyAlongRay(origin, end, hitRadius);
                var isElite = hitIndex >= 0 && _enemies.Agents[hitIndex].active && _enemies.Agents[hitIndex].isElite;
                var dmg = DamageSystem.RollDamageDetailed(
                    w.def.baseDamage * 0.6f,
                    w.damageBonus,
                    stats,
                    isElite,
                    0f,
                    rng,
                    w.def.procCoefficient,
                    w.def.id);
                if (!isBroadside)
                    _juice?.OnWeaponFired(w.def.id, origin, dir);
                if (isBroadside && hitIndex >= 0)
                {
                    ApplyWeaponDamage(hitIndex, w.def.id, dmg, isElite, _enemies.Agents[hitIndex].isBoss);
                    hits++;
                }

                var projectileLife = isBroadside ? 0.55f : 2.5f;
                if (_projectiles.TrySpawn(origin, dir, speed, isBroadside ? 0f : dmg.amount, projectileLife, size, 0, dmg.isCrit, w.def.projectileColor, w.def.id))
                {
                    if (!isBroadside && hitIndex >= 0)
                        hits++;
                }
            }
            WeaponDamageLedger.RecordConcurrentTargets(w.def.id, hits);

            if (w.def.id == "broadside_battery"
                && WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) == "kings_broadside")
            {
                var forward = _player.MovementForward;
                forward.y = 0f;
                forward.Normalize();
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                var opposite = Vector3.Dot(baseDir, right) >= 0f ? -right : right;
                _pendingBroadsides.Add(new PendingBroadside
                {
                    active = true,
                    timer = 0.12f,
                    sideDirection = opposite,
                    weapon = w
                });
            }
        }

        void FirePierceLane(ref WeaponInstance w, PlayerStats stats, RunRng rng, Vector3 dir, int primaryTarget)
        {
            NotifyFire(w.def, dir, primaryTarget);
            var origin = _player.Position;
            origin.y = 0f;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            var muzzle = _player.Position + Vector3.up * 0.35f;
            if (w.def.id == "long_nine")
            {
                var halfWidth = (_juice != null ? _juice.LongNineBeamHalfWidthBase : 0.32f) * w.sizeMult * stats.Size;
                CollectEnemiesInForwardBeam(origin, dir, w.def.range, halfWidth, _laneIndexScratch);
                _juice?.OnWeaponFired(w.def.id, muzzle, dir);
                _juice?.OnLongNineBeam(muzzle, dir, w.def.range, halfWidth);
            }
            else
            {
                var arc = WeaponTargeting.ResolveArcDegrees(w.def);
                CollectEnemiesInForwardLane(origin, dir, w.def.range, arc, _laneIndexScratch);
            }

            var agents = _enemies.Agents;
            var hitCount = 0;
            for (var i = 0; i < _laneIndexScratch.Count; i++)
            {
                var index = _laneIndexScratch[i];
                if (!agents[index].active) continue;
                hitCount++;
                var dmg = DamageSystem.RollDamageDetailed(
                    w.def.baseDamage,
                    w.damageBonus * w.level * 0.1f + 1f,
                    stats,
                    agents[index].isElite,
                    0f,
                    rng,
                    w.def.procCoefficient,
                    w.def.id);
                ApplyWeaponDamage(index, w.def.id, dmg, agents[index].isElite, agents[index].isBoss);
                if (w.def.id != "long_nine")
                {
                    var burstOrigin = agents[index].position + Vector3.up * 0.35f;
                    _juice?.OnWeaponFired(w.def.id, burstOrigin, dir);
                }

                if (i == 0 && WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) == "stormshot_cannon")
                    EmitStormshotBurst(ref w, stats, rng, agents[index].position, dir);
            }

            WeaponDamageLedger.RecordConcurrentTargets(w.def.id, hitCount);
        }

        void CollectEnemiesInForwardBeam(Vector3 origin, Vector3 forward, float range, float halfWidth, List<int> results)
        {
            results.Clear();
            _laneHitScratch.Clear();
            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (!WeaponBeamTargeting.IsInsideBeam(origin, forward, range, halfWidth, agents[i].position, agents[i].radius))
                    continue;
                results.Add(i);
                _laneHitScratch.Add(agents[i].position);
            }

            for (var a = 0; a < results.Count - 1; a++)
            {
                for (var b = a + 1; b < results.Count; b++)
                {
                    var da = Vector3.Dot(_laneHitScratch[a] - origin, forward);
                    var db = Vector3.Dot(_laneHitScratch[b] - origin, forward);
                    if (da <= db) continue;
                    (results[a], results[b]) = (results[b], results[a]);
                    (_laneHitScratch[a], _laneHitScratch[b]) = (_laneHitScratch[b], _laneHitScratch[a]);
                }
            }
        }

        void EmitStormshotBurst(ref WeaponInstance w, PlayerStats stats, RunRng rng, Vector3 hitPoint, Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();
            var pellets = 5;
            var spread = 55f;
            var halfSpread = spread * 0.5f;
            var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
            var size = 0.22f * w.sizeMult * stats.Size;
            for (var i = 0; i < pellets; i++)
            {
                var angle = pellets <= 1 ? 0f : -halfSpread + (spread / (pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var origin = hitPoint + Vector3.up * 0.35f;
                var end = origin + dir * 6f;
                var hitIndex = FindFirstEnemyAlongRay(origin, end, 0.75f);
                if (hitIndex < 0) continue;
                var enemy = _enemies.Agents[hitIndex];
                var dmg = DamageSystem.RollDamageDetailed(
                    w.def.baseDamage * 0.35f,
                    w.damageBonus,
                    stats,
                    enemy.isElite,
                    0f,
                    rng,
                    0.7f,
                    w.def.id);
                _projectiles.TrySpawn(origin, dir, speed, dmg.amount, 2f, size, 0, dmg.isCrit, w.def.projectileColor);
            }
        }

        void CollectEnemiesInForwardLane(Vector3 origin, Vector3 forward, float range, float arcDegrees, List<int> results)
        {
            results.Clear();
            _laneHitScratch.Clear();
            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var delta = agents[i].position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (!WeaponTargeting.IsWithinForwardArc(origin, forward, agents[i].position, arcDegrees)) continue;
                results.Add(i);
                _laneHitScratch.Add(agents[i].position);
            }

            for (var a = 0; a < results.Count - 1; a++)
            {
                for (var b = a + 1; b < results.Count; b++)
                {
                    var da = Vector3.Dot(_laneHitScratch[a] - origin, forward);
                    var db = Vector3.Dot(_laneHitScratch[b] - origin, forward);
                    if (da <= db) continue;
                    (results[a], results[b]) = (results[b], results[a]);
                    (_laneHitScratch[a], _laneHitScratch[b]) = (_laneHitScratch[b], _laneHitScratch[a]);
                }
            }
        }

        void TickPendingBroadsides(float dt, PlayerStats stats, RunRng rng)
        {
            for (var i = _pendingBroadsides.Count - 1; i >= 0; i--)
            {
                var pending = _pendingBroadsides[i];
                if (!pending.active)
                {
                    _pendingBroadsides.RemoveAt(i);
                    continue;
                }

                pending.timer -= dt;
                if (pending.timer > 0f)
                {
                    _pendingBroadsides[i] = pending;
                    continue;
                }

                var w = pending.weapon;
                if (w.def == null)
                {
                    _pendingBroadsides.RemoveAt(i);
                    continue;
                }

                var pellets = Mathf.Max(2, 1 + w.quantityBonus / 2);
                var speed = w.def.projectileSpeed * w.speedMult * stats.ProjectileSpeed;
                var size = 0.2f * w.sizeMult * stats.Size;
                var hits = 0;
                for (var p = 0; p < pellets; p++)
                {
                    var angle = (p - pellets * 0.5f) * 6f;
                    var dir = Quaternion.Euler(0f, angle, 0f) * pending.sideDirection;
                    var origin = _player.Position + Vector3.up * 0.35f;
                    var end = origin + dir * w.def.range;
                    var hitIndex = FindFirstEnemyAlongRay(origin, end, 0.75f);
                    if (hitIndex < 0) continue;
                    hits++;
                    var enemy = _enemies.Agents[hitIndex];
                    var dmg = DamageSystem.RollDamageDetailed(
                        w.def.baseDamage * 0.45f,
                        w.damageBonus,
                        stats,
                        enemy.isElite,
                        0f,
                        rng,
                        w.def.procCoefficient,
                        w.def.id);
                    _projectiles.TrySpawn(origin, dir, speed, dmg.amount, 2.2f, size, 0, dmg.isCrit, w.def.projectileColor);
                }

                WeaponDamageLedger.RecordConcurrentTargets(w.def.id, hits);
                _pendingBroadsides.RemoveAt(i);
            }
        }

        void TickBoardingSkiffs(float dt, PlayerStats stats, RunRng rng)
        {
            for (var i = _boardingSkiffs.Count - 1; i >= 0; i--)
            {
                var skiff = _boardingSkiffs[i];
                if (!skiff.active)
                {
                    _boardingSkiffs.RemoveAt(i);
                    continue;
                }

                skiff.lifeTime -= dt;
                if (skiff.lifeTime <= 0f || skiff.targetIndex < 0 || !_enemies.Agents[skiff.targetIndex].active)
                {
                    DestroySkiffVisual(ref skiff);
                    _boardingSkiffs.RemoveAt(i);
                    continue;
                }

                if (skiff.spawnAnimTime > 0f)
                {
                    skiff.spawnAnimTime -= dt;
                    var duration = Mathf.Max(0.01f, skiff.spawnAnimDuration);
                    var t = 1f - Mathf.Clamp01(skiff.spawnAnimTime / duration);
                    var eased = t * t * (3f - 2f * t);
                    var surface = skiff.spawnStart;
                    surface.y = 0.12f;
                    skiff.position = Vector3.Lerp(skiff.spawnStart, surface, eased);
                }

                var targetPos = _enemies.Agents[skiff.targetIndex].position;
                targetPos.y = 0f;
                var toTarget = targetPos - skiff.position;
                toTarget.y = 0f;
                var dist = toTarget.magnitude;
                if (skiff.spawnAnimTime <= 0f && dist > 0.05f)
                    skiff.position += toTarget / dist * (9.5f * dt);

                if (skiff.visual != null)
                {
                    skiff.visual.position = skiff.position + Vector3.up * 0.2f;
                    if (dist > 0.05f)
                        skiff.visual.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                }

                skiff.strikeCooldown -= dt;
                if (dist <= 1.35f && skiff.strikeCooldown <= 0f)
                {
                    skiff.strikeCooldown = 0.55f;
                    var enemy = _enemies.Agents[skiff.targetIndex];
                    var dmg = DamageSystem.RollDamageDetailed(
                        skiff.baseDamage,
                        skiff.damageBonus,
                        stats,
                        enemy.isElite,
                        0f,
                        rng,
                        skiff.procCoefficient,
                        skiff.weaponId);
                    ApplyWeaponDamage(skiff.targetIndex, skiff.weaponId, dmg, enemy.isElite, enemy.isBoss);
                }

                _boardingSkiffs[i] = skiff;
            }
        }

        int FindFirstEnemyAlongRay(Vector3 origin, Vector3 end, float radius)
        {
            var agents = _enemies.Agents;
            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var closest = ClosestPointOnSegment(origin, end, agents[i].position);
                if (Vector3.Distance(closest, agents[i].position) > radius) continue;
                var dist = (closest - origin).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = i;
            }
            return best;
        }

        static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            ab.y = 0f;
            if (ab.sqrMagnitude < 0.0001f) return a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
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
            _cutlassHitTimer = _orbitVolleyTimer > 0f ? 0.096f : 0.12f;

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
                        w.def.procCoefficient, w.def.id);
                    _enemies.ApplyDamage(i, dmg, false);
                }
            }
        }

        void FireFlamethrower(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var forward = _player.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            const float forwardOffset = 1.65f;
            var origin = _player.Position + forward * forwardOffset;
            var range = w.def.range * w.sizeMult * stats.Size;
            if (V3CaptainActiveController.Instance != null)
                range *= V3CaptainActiveController.Instance.FlameRangeMultiplier;
            var arc = WeaponTargeting.ResolveArcDegrees(w.def);
            if (WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) == "hellfire_nozzle")
                arc *= 1.2f;
            var halfAngle = arc * 0.5f;
            var burnDuration = w.def.duration * w.durationMult * stats.Duration;
            if (!WeaponTargeting.HasEnemyInForwardArc(_enemies, _player.Position, forward, range, arc))
                return;

            _juice?.OnFlamethrowerBurst(origin, forward);
            NotifyFire(w.def, forward);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                var toEnemy = agents[i].position - origin;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude < 0.01f) continue;
                if (toEnemy.magnitude > range) continue;
                if (Vector3.Angle(forward, toEnemy) > halfAngle) continue;

                var hpBefore = agents[i].hp;
                var killPos = agents[i].position;
                var dmg = DamageSystem.RollDamage(
                    w.def.baseDamage,
                    w.damageBonus,
                    stats,
                    agents[i].isElite,
                    0f,
                    rng,
                    w.def.procCoefficient, w.def.id);
                if (V3CaptainActiveController.Instance != null)
                    dmg *= V3CaptainActiveController.Instance.BurnDamageMultiplier;
                _enemies.ApplyDamage(i, dmg, false);
                if (hpBefore > 0f && hpBefore <= dmg)
                    HellfirePatchWorld.TrySpawnOnDirectFlameKill(killPos, w.def.id);
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
                    w.def.procCoefficient, w.def.id);
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
            NotifyFire(w.def, back);
            AddPowderKeg(w, stats, pos, visual);
            if (WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) == "devils_wake")
            {
                var right = Vector3.Cross(Vector3.up, back).normalized;
                var sidePos = pos + right * 1.6f;
                AddPowderKeg(w, stats, sidePos, _juice?.OnMineDropped(sidePos));
            }
        }

        void AddPowderKeg(WeaponInstance w, PlayerStats stats, Vector3 pos, Transform visual)
        {
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

                var blastRadius = GetPowderKegBlastRadius(keg.blastRadius);
                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (Vector3.Distance(agents[ai].position, keg.position) > blastRadius) continue;
                    var dmg = DamageSystem.RollDamage(
                        keg.baseDamage * 1.5f,
                        keg.damageBonus,
                        stats,
                        agents[ai].isElite,
                        0f,
                        rng,
                        keg.procCoefficient,
                        "powder_kegs",
                        DamageProcKind.TriggeredItem);
                    _enemies.ApplyDamage(ai, dmg, false);
                }

                _juice?.OnMineExploded(keg.position, blastRadius);
                if (WeaponEvolutionCatalog.GetBehaviorModifier("powder_kegs") == "devils_wake")
                    ChainNearbyPowderKegs(keg.position, 4.2f);
                if (keg.visual != null)
                    Destroy(keg.visual.gameObject);
                _powderKegs.RemoveAt(i);
            }
        }

        void ChainNearbyPowderKegs(Vector3 origin, float radius)
        {
            for (var i = 0; i < _powderKegs.Count; i++)
            {
                var keg = _powderKegs[i];
                if (!keg.active) continue;
                if (Vector3.Distance(keg.position, origin) > radius) continue;
                keg.armTime = Mathf.Min(keg.armTime, 0.08f);
                _powderKegs[i] = keg;
            }
        }

        void FireLightning(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out var target, out var aimDir))
            {
                if (!_enemies.TryGetNearest(_player.Position, w.def.range, out target, false))
                    return;
                aimDir = (_enemies.Agents[target].position - _player.Position).normalized;
            }

            NotifyFire(w.def, aimDir, target);
            var strikePos = _enemies.Agents[target].position;
            strikePos.y = 0f;
            var radius = (3.5f + w.quantityBonus * 0.5f) * w.sizeMult * stats.Size;
            _juice?.OnLightningStrike(strikePos, radius);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (Vector3.Distance(agents[i].position, strikePos) > radius) continue;
                var dmg = DamageSystem.RollDamage(w.def.baseDamage, w.damageBonus, stats, agents[i].isElite, 0f, rng, w.def.procCoefficient, w.def.id);
                _enemies.ApplyDamage(i, dmg, false);
                StatusController.Instance?.ApplyStatus(i, StatusType.Shock, 0.5f * w.durationMult);
            }

            if (WeaponEvolutionCatalog.GetBehaviorModifier(w.def.id) != "tempest_mast") return;

            var jumpTarget = -1;
            var bestDist = float.MaxValue;
            for (var j = 0; j < agents.Length; j++)
            {
                if (!agents[j].active || j == target) continue;
                var dist = Vector3.Distance(agents[j].position, strikePos);
                if (dist > 9f || dist >= bestDist) continue;
                bestDist = dist;
                jumpTarget = j;
            }

            if (jumpTarget >= 0)
            {
                var jumpPos = agents[jumpTarget].position;
                jumpPos.y = 0f;
                var jumpRadius = radius * 0.75f;
                _juice?.OnLightningStrike(jumpPos, jumpRadius);
                for (var i = 0; i < agents.Length; i++)
                {
                    if (!agents[i].active) continue;
                    if (Vector3.Distance(agents[i].position, jumpPos) > jumpRadius) continue;
                    var dmg = DamageSystem.RollDamage(w.def.baseDamage * 0.65f, w.damageBonus, stats, agents[i].isElite, 0f, rng, w.def.procCoefficient * 0.8f, w.def.id);
                    _enemies.ApplyDamage(i, dmg, false);
                }
            }

            var cluster = WeaponTargeting.PredictClusterGround(_enemies, _player, w.def.range);
            _delayedStrikes.Add(new DelayedStrike
            {
                active = true,
                position = cluster,
                timer = 0.85f,
                radius = radius * 0.8f,
                baseDamage = w.def.baseDamage * 0.55f,
                damageBonus = w.damageBonus,
                procCoefficient = w.def.procCoefficient * 0.5f,
                weaponId = w.def.id
            });
        }

        void FireAura(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var radius = 4f * w.sizeMult * stats.Size;
            var list = _enemies.Agents;
            for (var i = 0; i < list.Length; i++)
            {
                if (!list[i].active) continue;
                if (Vector3.Distance(list[i].position, _player.Position) > radius) continue;
                var dmg = DamageSystem.RollDamageDetailed(w.def.baseDamage * 0.35f, w.damageBonus, stats, list[i].isElite, 0f, rng, w.def.procCoefficient, w.def.id);
                ApplyWeaponDamage(i, w.def.id, dmg, list[i].isElite, list[i].isBoss);
                Combat.StatusController.Instance?.ApplyStatus(i, Core.StatusType.Curse, 0.4f * w.durationMult);
            }
        }

        void FireMortar(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            var target = WeaponTargeting.PredictClusterGround(_enemies, _player, w.def.range);
            target.y = 0.2f;
            NotifyFire(w.def, (target - _player.Position).normalized);
            _juice?.OnMortarTelegraph(target, w.def.size * w.sizeMult * stats.Size);
            _delayedStrikes.Add(new DelayedStrike
            {
                active = true,
                position = target,
                timer = 1.1f,
                radius = w.def.size * w.sizeMult * stats.Size,
                baseDamage = w.def.baseDamage,
                damageBonus = w.damageBonus,
                procCoefficient = w.def.procCoefficient,
                weaponId = w.def.id
            });
        }

        public int CopyMortarTelegraphs(Vector3[] positions, float[] radii, float[] timeLeft, int maxCount)
        {
            if (positions == null || radii == null || timeLeft == null || maxCount <= 0) return 0;
            var count = 0;
            for (var i = 0; i < _delayedStrikes.Count && count < maxCount; i++)
            {
                var strike = _delayedStrikes[i];
                if (!strike.active || strike.weaponId != "mortar") continue;
                positions[count] = strike.position;
                radii[count] = strike.radius;
                timeLeft[count] = strike.timer;
                count++;
            }

            return count;
        }

        void FireBoardingParty(ref WeaponInstance w, PlayerStats stats, RunRng rng)
        {
            if (!WeaponTargeting.TryResolveTarget(_enemies, _player, w.def, out var target, out var aim))
                return;
            var enemy = _enemies.Agents[target];
            NotifyFire(w.def, aim, target);
            var back = -_player.MovementForward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = -aim;
            back.Normalize();
            var lateral = Vector3.Cross(Vector3.up, back).normalized;
            var side = rng.CombatFloat() >= 0.5f ? 1f : -1f;
            var spawn = _player.Position + back * 5.5f + lateral * side * 2.4f;
            spawn.y = -0.7f;
            JuiceVfxBroker.Instance?.PlayWaterSplash(spawn + Vector3.up * 0.35f);
            var visual = CreateBoardingSkiffVisual(spawn, aim);
            const float spawnDuration = 0.55f;
            _boardingSkiffs.Add(new BoardingSkiff
            {
                active = true,
                targetIndex = target,
                position = spawn,
                spawnStart = spawn,
                spawnAnimTime = spawnDuration,
                spawnAnimDuration = spawnDuration,
                strikeCooldown = 0.15f,
                lifeTime = 9f,
                baseDamage = w.def.baseDamage,
                damageBonus = w.damageBonus,
                procCoefficient = w.def.procCoefficient,
                weaponId = w.def.id,
                visual = visual
            });
        }

        static float GetPowderKegBlastRadius(float baseRadius)
        {
            var mult = V3CaptainActiveController.Instance != null
                ? V3CaptainActiveController.Instance.PowderKegBlastRadiusMultiplier
                : 1f;
            return baseRadius * mult;
        }

        Transform EnsureSkiffVisualRoot()
        {
            if (_skiffVisualRoot != null) return _skiffVisualRoot;
            var go = new GameObject("BoardingSkiffs");
            go.transform.SetParent(transform, false);
            _skiffVisualRoot = go.transform;
            return _skiffVisualRoot;
        }

        Transform CreateBoardingSkiffVisual(Vector3 position, Vector3 forward)
        {
            EnsureSkiffVisualRoot();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var def = database != null ? database.GetEnemy("cannonboat") : null;
            GameObject go;
            if (def?.visualPrefab != null)
            {
                go = Instantiate(def.visualPrefab, _skiffVisualRoot);
                go.name = "BoardingSkiff";
                go.transform.localScale = Vector3.one * 0.42f;
                var colliders = go.GetComponentsInChildren<Collider>(true);
                for (var i = 0; i < colliders.Length; i++)
                    Destroy(colliders[i]);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "BoardingSkiff";
                go.transform.SetParent(_skiffVisualRoot, false);
                go.transform.localScale = new Vector3(1.3f, 0.35f, 2.2f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.55f, 0.3f, 0.15f);
            }

            go.transform.position = position + Vector3.up * 0.2f;
            go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            return go.transform;
        }

        static void DestroySkiffVisual(ref BoardingSkiff skiff)
        {
            if (skiff.visual != null)
                Destroy(skiff.visual.gameObject);
            skiff.visual = null;
        }

        void TickDelayedStrikes(float dt, PlayerStats stats, RunRng rng)
        {
            for (var i = _delayedStrikes.Count - 1; i >= 0; i--)
            {
                var strike = _delayedStrikes[i];
                if (!strike.active)
                {
                    _delayedStrikes.RemoveAt(i);
                    continue;
                }

                strike.timer -= dt;
                if (strike.timer > 0f)
                {
                    _delayedStrikes[i] = strike;
                    continue;
                }

                var hits = 0;
                var agents = _enemies.Agents;
                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (strike.weaponId == "mortar")
                    {
                        if (!WithinHorizontalRadius(strike.position, agents[ai].position, strike.radius))
                            continue;
                    }
                    else if (Vector3.Distance(agents[ai].position, strike.position) > strike.radius)
                        continue;
                    hits++;
                    var dmg = DamageSystem.RollDamageDetailed(
                        strike.baseDamage,
                        strike.damageBonus,
                        stats,
                        agents[ai].isElite,
                        0f,
                        rng,
                        strike.procCoefficient,
                        strike.weaponId);
                    ApplyWeaponDamage(ai, strike.weaponId, dmg, agents[ai].isElite, agents[ai].isBoss);
                }
                WeaponDamageLedger.RecordConcurrentTargets(strike.weaponId, hits);
                if (strike.weaponId == "mortar")
                {
                    _juice?.OnMortarImpact(strike.position, strike.radius, rng);
                    JuiceVfxBroker.Instance?.PlayWaterSplash(strike.position);
                }
                else
                    _juice?.OnMineExploded(strike.position, strike.radius);
                _delayedStrikes.RemoveAt(i);
            }
        }

        static bool WithinHorizontalRadius(Vector3 center, Vector3 target, float radius)
        {
            var dx = target.x - center.x;
            var dz = target.z - center.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        void ApplyWeaponDamage(int enemyIndex, string weaponId, DamageSystem.DamageRoll dmg, bool isElite, bool isBoss)
        {
            WeaponDamageLedger.RecordHit(weaponId, dmg.amount, dmg.isCrit, isElite, isBoss);
            _enemies.ApplyDamage(enemyIndex, dmg.amount, dmg.isCrit);
        }

        public int CountDistinctWeaponFamilies()
        {
            var families = new HashSet<string>();
            for (var i = 0; i < _weapons.Count; i++)
            {
                var tags = _weapons[i].def?.tags;
                if (tags == null || tags.Length == 0)
                {
                    families.Add(_weapons[i].def?.id ?? "unknown");
                    continue;
                }

                families.Add(tags[0]);
            }

            return families.Count;
        }

        void NotifyFire(WeaponDefinition def, Vector3 aimDirection, int targetIndex = -1)
        {
            if (def == null || _player == null) return;
            var hasTarget = targetIndex >= 0 && targetIndex < _enemies.Agents.Length && _enemies.Agents[targetIndex].active;
            var targetPos = hasTarget ? _enemies.Agents[targetIndex].position : _player.Position + aimDirection * def.range;
            if (def.pattern == WeaponPattern.PredictedAoE)
                targetPos = WeaponTargeting.PredictClusterGround(_enemies, _player, def.range);
            WeaponTargetingVisualizer.Instance?.NotifyWeaponFired(def, aimDirection, targetPos, hasTarget || def.pattern == WeaponPattern.PredictedAoE);
        }
    }
}

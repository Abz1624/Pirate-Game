using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Combat feedback from the event stream (M14; port of V3's CombatVfxController, JuiceVfxBroker hit/kill/highlight effects
    /// and PlayerDamageFlash, which listened to TriggerBus): hit sparks (cannon weapons a cannon impact; a weapon whose hit
    /// effect is None — the powder kegs — plays nothing), kill explosions (elite and boss bursts), the player's damage flash
    /// and highlight, the level-up burst with its shake; and the enemy simulation's hit feedback (enemy highlight, shake,
    /// damage numbers). All effects come from prewarmed pools.
    /// </summary>
    public sealed class CombatFeedbackPresenter : MonoBehaviour, RoguePirates.V4.Enemies.IEnemyHitFeedback
    {
        DemoJuiceSettings _s;
        PooledEffects _fx;
        CameraShakePresenter _shake;
        DamageNumberPresenter _numbers;
        ConfigSetRuntime _config;
        ICombatEventReader _events;
        Func<Transform> _ship;
        readonly Dictionary<string, ConfigEnums.WeaponHitEffect> _hitEffects = new Dictionary<string, ConfigEnums.WeaponHitEffect>();
        Renderer[] _flashRenderers;
        Color[] _baseColors;
        Transform _flashShip;
        float _flashTimer;

        public int EffectsPlayed { get; private set; }
        /// <summary>Playtest (3 Oct, owner): the live enemy view nearest a point within a radius (the enemy view presenter's
        /// lookup), so damage without its own hit feedback (burn ticks, flame patches, barrels) can flash its enemy.</summary>
        public Func<Vector3, float, Transform> NearestEnemyView { get; set; }
        /// <summary>Playtest (3 Oct, owner): grouped numbers shown so far (tests and the playable check).</summary>
        public int GroupedNumbersShown { get; private set; }

        // Playtest (3 Oct, owner: "every damage should show a number and a flash"). The simulation calls DamageNumber for the
        // hits that have their own feedback; every hit also arrives as a Hit event at the end of the tick. A Hit event with
        // no DamageNumber call of its own (burn ticks, Hellfire flame patches, barrel blasts, hits under half a point, and
        // numbers over the per-frame cap) is grouped by place: one number and one flash per enemy every half second at most.
        readonly struct HitKey : IEquatable<HitKey>
        {
            readonly float _x, _z, _damage;
            public HitKey(float x, float z, float damage) { _x = x; _z = z; _damage = damage; }
            public bool Equals(HitKey o) => _x == o._x && _z == o._z && _damage == o._damage;
            public override bool Equals(object obj) => obj is HitKey k && Equals(k);
            public override int GetHashCode() => (_x.GetHashCode() * 397 ^ _z.GetHashCode()) * 397 ^ _damage.GetHashCode();
        }

        struct Group
        {
            public Vector3 Position;
            public float Damage, Age, Quiet;
        }

        const float GroupRadius = 2.2f;      // a moving enemy stays in its group for the window
        const float GroupWindow = 0.5f;      // a steady burn shows one number every half second
        const float GroupQuietFlush = 0.08f; // a one-off hit (a flame patch tick, a barrel) shows almost at once
        const int MaxGroups = 96;
        readonly Dictionary<HitKey, int> _covered = new Dictionary<HitKey, int>(64);
        readonly List<Group> _groups = new List<Group>(MaxGroups);
        /// <summary>Demo: the artists' VFX profiles (weapon hits and kills, enemy spawns and deaths, the ship's hits and sinking).</summary>
        public VfxProfilePlayer Profiles { get; set; }
        /// <summary>Hackathon (13 Oct): the weapon prefab catalog (each weapon's hit and crit sparks; null: the shared ones). Set before Bind.</summary>
        public Weapons.WeaponVisualCatalog Visuals { get; set; }

        GameObject PickHit(string weaponId, Weapons.WeaponVisualSlot slot, GameObject fallback) =>
            Visuals != null && weaponId != null ? Visuals.Pick(weaponId, slot, fallback) : fallback;

        public void Bind(PoolService pools, ICombatEventReader events, ConfigSetRuntime config, DemoJuiceSettings settings, Func<Transform> ship,
            CameraShakePresenter shake, DamageNumberPresenter numbers)
        {
            Unbind();
            _s = settings;
            _config = config;
            _ship = ship;
            _shake = shake;
            _numbers = numbers;
            _fx = new PooledEffects(pools);
            if (_s != null)
            {
                // Prewarm for peak load (M14.15: six weapons and ~70 enemies peaked at 33 hit sparks, 11 highlights).
                _fx.Register(_s.enemyHitPrefab, 40);
                // Hackathon (13 Oct): with the weapon prefab catalog the weapons' crits and cannon impacts are their own copies
                // (prewarmed below); the shared ones stay for hits without a weapon prefab (items).
                _fx.Register(_s.critHitPrefab, Visuals != null ? 4 : 12);
                _fx.Register(_s.cannonImpactPrefab != null ? _s.cannonImpactPrefab : _s.weaponExplosionPrefab, Visuals != null ? 2 : 24);
                _fx.Register(_s.enemyKillPrefab, 16);
                _fx.Register(_s.eliteKillExplosionPrefab, 4);
                _fx.Register(_s.bossKillExplosionPrefab, Mathf.Max(1, _s.bossDeathBurstCount));
                _fx.Register(_s.damageHighlightPrefab, 16);
                _fx.Register(_s.levelUpBurstPrefab, 2);
                // Hackathon (13 Oct): each weapon's own hit and crit sparks (a run has at most six weapons; they grow past this).
                if (Visuals != null && config != null)
                    foreach (var w in config.All<WeaponConfig>())
                    {
                        var set = w != null ? Visuals.Find(w.weaponId) : null;
                        if (set == null) continue;
                        _fx.Register(set.hit, 12);
                        _fx.Register(set.critHit, 3);
                    }
                _fx.Prewarm();
            }
            if (_config != null)
                foreach (var w in _config.All<WeaponConfig>())
                    if (!string.IsNullOrEmpty(w.weaponId)) _hitEffects[w.weaponId] = w.hitEffect;
            _events = events;
            if (_events != null) _events.TickPublished += OnEvents;
        }

        void Unbind()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _events = null;
            _fx?.ReleaseAll();
            _covered.Clear();
            _groups.Clear();
        }

        ConfigEnums.WeaponHitEffect HitEffect(string weaponId) =>
            weaponId != null && _hitEffects.TryGetValue(weaponId, out var e) ? e : ConfigEnums.WeaponHitEffect.Standard;

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                switch (e.Kind)
                {
                    case CombatEventKind.Hit:
                        if (Uncover(new HitKey(e.X, e.Y, e.Z))) OnHit(new Vector3(e.X, 0f, e.Y), e.A == 1, e.Tag);
                        else AddToGroup(new Vector3(e.X, 0f, e.Y), e.Z);
                        break;
                    case CombatEventKind.Killed:
                        // A profile may replace the kill effect, but a boss keeps its multi-burst.
                        if (Profiles != null && Profiles.Killed(e.A, e.Tag, new Vector3(e.X, 0.25f, e.Y)) && (e.B & 2) == 0) break;
                        OnKill(new Vector3(e.X, 0f, e.Y), (e.B & 1) != 0 && (e.B & 4) == 0, (e.B & 2) != 0, e.Tag);
                        break;
                    case CombatEventKind.Spawned: Profiles?.Spawned(e.A, e.Tag, new Vector3(e.X, 0f, e.Y)); break;
                    case CombatEventKind.BossArriving: OnBossArriving(new Vector3(e.X, 0f, e.Y)); break;
                    case CombatEventKind.RunState:
                        if (e.A == (int)RoguePirates.V4.Foundation.Core.RunState.PlayerDead) Profiles?.ShipDied();
                        break;
                    case CombatEventKind.PlayerDamaged:
                        if (e.Z > 0f) Profiles?.ShipDamaged();
                        OnPlayerDamaged(e.Z);
                        break;
                    case CombatEventKind.LevelUp: OnLevelUp(); break;
                }
            }
            _covered.Clear(); // every DamageNumber call of this tick has met its Hit event
        }

        bool Uncover(HitKey key)
        {
            if (!_covered.TryGetValue(key, out var count)) return false;
            if (count <= 1) _covered.Remove(key); else _covered[key] = count - 1;
            return true;
        }

        void AddToGroup(Vector3 position, float damage)
        {
            if (damage <= 0f) return;
            var best = -1;
            var bestSqr = GroupRadius * GroupRadius;
            for (var i = 0; i < _groups.Count; i++)
            {
                var d = (_groups[i].Position - position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = i; }
            }
            if (best < 0)
            {
                if (_groups.Count >= MaxGroups) return;
                _groups.Add(new Group { Position = position, Damage = damage });
                return;
            }
            var g = _groups[best];
            g.Position = position;
            g.Damage += damage;
            g.Quiet = 0f;
            _groups[best] = g;
        }

        void TickGroups(float dt)
        {
            for (var i = _groups.Count - 1; i >= 0; i--)
            {
                var g = _groups[i];
                g.Age += dt;
                g.Quiet += dt;
                if (g.Age < GroupWindow && !(g.Age >= GroupQuietFlush && g.Quiet >= GroupQuietFlush))
                {
                    _groups[i] = g;
                    continue;
                }
                _groups.RemoveAt(i);
                if (g.Damage < 0.5f) continue; // a tick too small to read yet; the next window starts afresh
                GroupedNumbersShown++;
                _numbers?.ShowGroupedDamage(g.Position, g.Damage);
                var view = NearestEnemyView?.Invoke(g.Position, GroupRadius);
                if (view != null) DamageHighlight(view);
                else if (_s != null && _s.enemyHitPrefab != null) Play(_s.enemyHitPrefab, g.Position + Vector3.up * 0.35f, _s.enemyHitScale * 0.75f);
            }
        }

        void Play(GameObject prefab, Vector3 position, float scale, float lifetime = 0f)
        {
            if (_fx.Play(prefab, position, Quaternion.identity, scale, lifetime) != null) EffectsPlayed++;
        }

        /// <summary>A weapon hit prefab with its tuning (scale, lifetime, height; 1 and 0 without one).</summary>
        void PlayTuned(GameObject prefab, Vector3 position, float scale, float lifetime = 0f)
        {
            position.y += Weapons.WeaponVisualTuning.Height(prefab);
            if (lifetime > 0f) lifetime *= Weapons.WeaponVisualTuning.Lifetime(prefab);
            Play(prefab, position, scale * Weapons.WeaponVisualTuning.Scale(prefab), lifetime);
        }

        /// <summary>Demo: the whirlpool where the boss is about to rise (it swirls until just after the boss appears).</summary>
        void OnBossArriving(Vector3 position)
        {
            if (_s == null || _s.bossArrivalPrefab == null || Profiles == null) return;
            var slot = new VfxSlot { prefab = _s.bossArrivalPrefab, scale = _s.bossArrivalScale, lifetime = _s.bossArrivalSeconds + 2.5f, replaceDefault = false };
            Profiles.Play(slot, position, Quaternion.identity);
            _shake?.Shake(0.1f, _s.bossArrivalSeconds);
        }

        /// <summary>V3 CombatVfxController.OnHit → JuiceVfxBroker.PlayHit / PlayCannonImpact.</summary>
        void OnHit(Vector3 position, bool crit, string weaponId)
        {
            if (_s == null) return;
            var profile = Profiles != null ? Profiles.Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Hit) : null;
            if (profile != null && profile.HasPrefab)
            {
                Profiles.Play(profile, position + Vector3.up * 0.35f, Quaternion.identity, crit ? 1.15f : 1f);
                if (profile.SuppressesDefault) return;
            }
            var effect = HitEffect(weaponId);
            // Playtest (3 Oct, owner): a weapon with no hit effect (the Powder Kegs, which have their own blast) still
            // shows a small spark on each enemy it hits, so every hit reads.
            // Hackathon (13 Oct): the weapon's own hit and crit prefabs (WeaponVisualCatalog); their tuning scales them.
            if (effect == ConfigEnums.WeaponHitEffect.None)
            {
                var spark = PickHit(weaponId, Weapons.WeaponVisualSlot.Hit, _s.enemyHitPrefab);
                if (spark != null) PlayTuned(spark, position + Vector3.up * 0.35f, _s.enemyHitScale * 0.8f);
                return;
            }
            var mult = profile != null && !profile.HasPrefab ? profile.ScaleOrOne : 1f;
            if (effect == ConfigEnums.WeaponHitEffect.CannonImpact)
            {
                var prefab = PickHit(weaponId, Weapons.WeaponVisualSlot.Hit, _s.cannonImpactPrefab != null ? _s.cannonImpactPrefab : _s.weaponExplosionPrefab);
                if (prefab != null) { PlayTuned(prefab, position + Vector3.up * 0.45f, _s.cannonImpactScale * (crit ? 1.12f : 1f) * mult, 0.55f); return; }
            }
            var hit = crit && _s.critHitPrefab != null ? PickHit(weaponId, Weapons.WeaponVisualSlot.CritHit, _s.critHitPrefab) : PickHit(weaponId, Weapons.WeaponVisualSlot.Hit, _s.enemyHitPrefab);
            PlayTuned(hit, position + Vector3.up * 0.35f, (crit ? _s.critHitScale : _s.enemyHitScale) * mult);
        }

        /// <summary>V3 CombatVfxController.OnKill → JuiceVfxBroker.PlayKill.</summary>
        void OnKill(Vector3 position, bool elite, bool boss, string weaponId)
        {
            // Playtest (3 Oct, owner: "some enemies don't play the death explosion"): every kill explodes; a weapon's
            // "no hit effect" (Powder Kegs, which have their own blast) only silences its hit sparks, not the kill.
            if (_s == null) return;
            if (boss)
            {
                var prefab = _s.bossKillExplosionPrefab != null ? _s.bossKillExplosionPrefab : _s.enemyKillPrefab;
                var count = Mathf.Max(1, _s.bossDeathBurstCount);
                for (var i = 0; i < count; i++)
                {
                    var offset = UnityEngine.Random.insideUnitSphere * 1.5f;
                    offset.y = Mathf.Abs(offset.y) * 0.5f;
                    Play(prefab, position + offset, _s.bossKillExplosionScale * (0.85f + i * 0.15f));
                }
                return;
            }
            var explosion = elite && _s.eliteKillExplosionPrefab != null ? _s.eliteKillExplosionPrefab : _s.enemyKillPrefab;
            if (explosion == null) return;
            var mult = Profiles != null ? Profiles.KillScale(weaponId) : 1f;
            Play(explosion, position + Vector3.up * 0.25f, (elite ? _s.eliteKillExplosionScale : _s.enemyKillScale) * mult);
        }

        /// <summary>V3 CombatVfxController.OnDamageTaken: the ship flashes and gets a highlight; the number rises above it.</summary>
        void OnPlayerDamaged(float amount)
        {
            if (amount <= 0f) return;
            _numbers?.ShowPlayerDamage(amount);
            var ship = _ship?.Invoke();
            if (ship == null) return;
            Flash(ship);
            DamageHighlight(ship);
        }

        /// <summary>V3 CombatVfxController.OnLevelChanged.</summary>
        void OnLevelUp()
        {
            var ship = _ship?.Invoke();
            if (_s == null || _s.levelUpBurstPrefab == null || ship == null) return;
            Play(_s.levelUpBurstPrefab, ship.position + Vector3.up * 0.5f, _s.levelUpBurstScale > 0f ? _s.levelUpBurstScale : 1f);
            _shake?.Shake(0.12f, 0.25f);
        }

        /// <summary>V3 JuiceVfxBroker.PlayDamageHighlight + DamageHighlightPulse: a highlight on the target for a moment.</summary>
        public void DamageHighlight(Transform view)
        {
            Profiles?.EnemyHit(view);
            if (_s == null || _s.damageHighlightPrefab == null || view == null) return;
            var go = _fx.Play(_s.damageHighlightPrefab, view.position, Quaternion.identity, _s.damageHighlightScale, _s.damageHighlightDuration, view);
            if (go == null) return;
            go.transform.localPosition = Vector3.up * 0.5f;
            go.transform.localRotation = Quaternion.identity;
            EffectsPlayed++;
        }

        /// <summary>V3 enemy hit feedback: a small shake (the hit sound was a placeholder log in V3).</summary>
        public void HitShakeAndSound(float damage) => _shake?.Shake(Mathf.Min(0.08f, damage * 0.01f), 0.1f);

        public void DamageNumber(Vector3 position, float damage, bool isCrit)
        {
            // Playtest (3 Oct, owner): a number over the per-frame cap is not lost; its hit joins the grouped numbers.
            if (_numbers == null || !_numbers.ShowEnemyDamage(position, damage, isCrit)) return;
            var key = new HitKey(position.x, position.z, damage);
            _covered[key] = _covered.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        /// <summary>V3 PlayerDamageFlash: the ship's renderers tint towards the flash colour and back.</summary>
        void Flash(Transform ship)
        {
            if (_flashShip != ship || _flashRenderers == null)
            {
                _flashShip = ship;
                _flashRenderers = ship.GetComponentsInChildren<Renderer>(true);
                _baseColors = new Color[_flashRenderers.Length];
                for (var i = 0; i < _flashRenderers.Length; i++)
                {
                    var m = _flashRenderers[i].sharedMaterial;
                    _baseColors[i] = m != null && m.HasProperty("_Color") ? m.color : Color.white;
                }
            }
            _flashTimer = _s.playerDamageFlashDuration;
            ApplyFlash(1f);
        }

        void ApplyFlash(float strength)
        {
            if (_flashRenderers == null) return;
            var flash = _s != null ? _s.playerDamageFlashColor : Color.red;
            for (var i = 0; i < _flashRenderers.Length; i++)
            {
                var r = _flashRenderers[i];
                if (r == null) continue;
                var material = r.material;
                if (!material.HasProperty("_Color")) continue;
                material.color = Color.Lerp(_baseColors[i], flash, strength);
            }
        }

        void LateUpdate()
        {
            _fx?.Tick();
            TickGroups(Time.deltaTime);
            if (_flashTimer <= 0f) return;
            _flashTimer -= Time.deltaTime;
            ApplyFlash(_s != null && _s.playerDamageFlashDuration > 0f ? Mathf.Clamp01(_flashTimer / _s.playerDamageFlashDuration) : 0f);
        }

        void OnDestroy() => Unbind();
    }
}

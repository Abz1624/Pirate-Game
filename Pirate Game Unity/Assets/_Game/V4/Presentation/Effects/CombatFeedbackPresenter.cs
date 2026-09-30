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
        /// <summary>Demo: the artists' VFX profiles (weapon hits and kills, enemy spawns and deaths, the ship's hits and sinking).</summary>
        public VfxProfilePlayer Profiles { get; set; }

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
                _fx.Register(_s.critHitPrefab, 12);
                _fx.Register(_s.cannonImpactPrefab != null ? _s.cannonImpactPrefab : _s.weaponExplosionPrefab, 24);
                _fx.Register(_s.enemyKillPrefab, 16);
                _fx.Register(_s.eliteKillExplosionPrefab, 4);
                _fx.Register(_s.bossKillExplosionPrefab, Mathf.Max(1, _s.bossDeathBurstCount));
                _fx.Register(_s.damageHighlightPrefab, 16);
                _fx.Register(_s.levelUpBurstPrefab, 2);
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
                    case CombatEventKind.Hit: OnHit(new Vector3(e.X, 0f, e.Y), e.A == 1, e.Tag); break;
                    case CombatEventKind.Killed:
                        // A profile may replace the kill effect, but a boss keeps its multi-burst.
                        if (Profiles != null && Profiles.Killed(e.A, e.Tag, new Vector3(e.X, 0.25f, e.Y)) && (e.B & 2) == 0) break;
                        OnKill(new Vector3(e.X, 0f, e.Y), (e.B & 1) != 0 && (e.B & 4) == 0, (e.B & 2) != 0, e.Tag);
                        break;
                    case CombatEventKind.Spawned: Profiles?.Spawned(e.A, e.Tag, new Vector3(e.X, 0f, e.Y)); break;
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
        }

        void Play(GameObject prefab, Vector3 position, float scale, float lifetime = 0f)
        {
            if (_fx.Play(prefab, position, Quaternion.identity, scale, lifetime) != null) EffectsPlayed++;
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
            if (effect == ConfigEnums.WeaponHitEffect.None) return;
            var mult = profile != null && !profile.HasPrefab ? profile.ScaleOrOne : 1f;
            if (effect == ConfigEnums.WeaponHitEffect.CannonImpact)
            {
                var prefab = _s.cannonImpactPrefab != null ? _s.cannonImpactPrefab : _s.weaponExplosionPrefab;
                if (prefab != null) { Play(prefab, position + Vector3.up * 0.45f, _s.cannonImpactScale * (crit ? 1.12f : 1f) * mult, 0.55f); return; }
            }
            var hit = crit && _s.critHitPrefab != null ? _s.critHitPrefab : _s.enemyHitPrefab;
            Play(hit, position + Vector3.up * 0.35f, (crit ? _s.critHitScale : _s.enemyHitScale) * mult);
        }

        /// <summary>V3 CombatVfxController.OnKill → JuiceVfxBroker.PlayKill.</summary>
        void OnKill(Vector3 position, bool elite, bool boss, string weaponId)
        {
            if (_s == null || HitEffect(weaponId) == ConfigEnums.WeaponHitEffect.None) return;
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

        public void DamageNumber(Vector3 position, float damage, bool isCrit) => _numbers?.ShowEnemyDamage(position, damage, isCrit);

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
            if (_flashTimer <= 0f) return;
            _flashTimer -= Time.deltaTime;
            ApplyFlash(_s != null && _s.playerDamageFlashDuration > 0f ? Mathf.Clamp01(_flashTimer / _s.playerDamageFlashDuration) : 0f);
        }

        void OnDestroy() => Unbind();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>The run's items as other modules read them (V3 V3RunItemInventory.HasItem / Items / GetStackCount).</summary>
    public interface IItemInventory
    {
        IReadOnlyList<string> Items { get; }
        bool HasItem(string itemId);
        int StackCount(string itemId);
    }

    /// <summary>What item effects read from the ship and the run each frame.</summary>
    public interface IItemWorld
    {
        bool RunActive { get; }
        bool ShipExists { get; }
        Vector3 ShipForward { get; }
        Vector3 ShipPosition { get; }
        Vector3 ShipVelocity { get; }
        int Gold { get; }
        /// <summary>The weapons the ship carries (for Admiral's Ledger's weapon families).</summary>
        IEnumerable<WeaponConfig> Weapons { get; }
        /// <summary>V5 M5: enemies within this distance of the ship (Barnacle Crown).</summary>
        int EnemiesNear(float radius) => 0;
    }

    /// <summary>
    /// Run items (M9 step 9.3): port of V3's V3RunItemInventory (stacks, stat changes per stack) and V3RunItemEffects
    /// (triggered effects per item: Gunner's Gloves, Ghost Lantern, Reinforced Keel, Captain's Bell, Admiral's Ledger,
    /// Golden Figurehead, Powder Fuse), reading <see cref="ItemConfig"/>. Effects V3 computes but never reads stay
    /// inactive (register R-40). Values and order are V3's.
    /// </summary>
    public sealed class ItemModule : IItemInventory
    {
        readonly Func<string, ItemConfig> _item;
        readonly IStatModifierSink _stats;
        readonly IItemWorld _world;
        readonly List<string> _items = new List<string>();
        readonly Dictionary<string, int> _stacks = new Dictionary<string, int>();

        int _bellKills;
        float _turnBuffTimer, _nearMissBuffTimer;
        float _lastHeading;
        bool _headingInitialized;
        float _explosionCharge, _distanceSinceHit;
        Vector3 _lastPosition;

        /// <summary>An item was gained (id): telemetry and the toast.</summary>
        public event Action<string> Acquired;
        /// <summary>Captain's Bell rang: every weapon fires once.</summary>
        public event Action FireAllWeapons;
        /// <summary>V5 M5 Leviathan Heart: (ledger id, damage, radius, telegraph seconds) on the densest crowd.</summary>
        public event Action<string, float, float, float> PulseRequested;
        /// <summary>V5 M5 Captain's Last Rum saved the ship (item id).</summary>
        public event Action<string> CheatedDeath;

        int _streakKills, _streakStacks;
        bool _lastRumUsed;
        float _frenzyTimer, _frenzyBonus, _pulseTimer, _surroundStacks;

        public ItemModule(Func<string, ItemConfig> item, IStatModifierSink stats, IItemWorld world)
        {
            _item = item;
            _stats = stats;
            _world = world;
        }

        readonly HashSet<string> _families = new HashSet<string>(); // Admiral's Ledger, reused per damage roll

        public IReadOnlyList<string> Items => _items;
        public bool HasItem(string itemId) => !string.IsNullOrEmpty(itemId) && _items.Contains(itemId);
        public int StackCount(string itemId) => itemId != null && _stacks.TryGetValue(itemId, out var n) ? n : 0;

        /// <summary>V3 ClearForNewRun + ResetForNewRun (the run's stats are new, so no change is removed).</summary>
        public void ResetForRun()
        {
            _items.Clear();
            _stacks.Clear();
            _bellKills = 0;
            _turnBuffTimer = 0f;
            _nearMissBuffTimer = 0f;
            _headingInitialized = false;
            _explosionCharge = 0f;
            _distanceSinceHit = 0f;
            _lastPosition = Vector3.zero;
            _streakKills = 0;
            _streakStacks = 0;
            _lastRumUsed = false;
            _frenzyTimer = 0f;
            _frenzyBonus = 0f;
            _pulseTimer = 0f;
            _surroundStacks = 0f;
        }

        /// <summary>V3 TryAddItem: another stack (uncapped) and its stat changes; always succeeds.</summary>
        public bool TryAddItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            _stacks.TryGetValue(itemId, out var n);
            _stacks[itemId] = n + 1;
            if (!_items.Contains(itemId)) _items.Add(itemId);
            var config = _item(itemId);
            if (config != null)
                foreach (var m in config.statModifiers) _stats.Add("item:" + itemId, m.stat, m.additive, m.multiplicative);
            Acquired?.Invoke(itemId);
            return true;
        }

        ItemEffect Effect(ItemEffectKind kind) => Effect(kind, out _);

        ItemEffect Effect(ItemEffectKind kind, out string itemId)
        {
            foreach (var id in _items)
            {
                var config = _item(id);
                if (config?.effects == null) continue;
                foreach (var e in config.effects)
                    if (e != null && e.kind == kind && e.active) { itemId = id; return e; }
            }
            itemId = null;
            return null;
        }


        /// <summary>V3 Update (skipped while paused or after the run ends).</summary>
        public void Tick(float dt)
        {
            if (!_world.RunActive) return;
            _turnBuffTimer = Mathf.Max(0f, _turnBuffTimer - dt);
            _nearMissBuffTimer = Mathf.Max(0f, _nearMissBuffTimer - dt);
            _frenzyTimer = Mathf.Max(0f, _frenzyTimer - dt);
            TrackHeadingAndMovement();
            TickPulse(dt);
            TickSurrounded(dt);
        }

        /// <summary>V5 M5 Leviathan Heart: a pulse every threshold seconds.</summary>
        void TickPulse(float dt)
        {
            var e = Effect(ItemEffectKind.PeriodicPulse, out var id);
            if (e == null || e.threshold <= 0f) return;
            _pulseTimer += dt;
            if (_pulseTimer < e.threshold) return;
            _pulseTimer = 0f;
            var copies = Mathf.Max(1, StackCount(id));
            PulseRequested?.Invoke(id, e.value * Mathf.Min(2f, 1f + 0.2f * (copies - 1)), e.cap, e.seconds);
        }

        /// <summary>V5 M5 Barnacle Crown: stacks while surrounded, decays otherwise.</summary>
        void TickSurrounded(float dt)
        {
            var e = Effect(ItemEffectKind.SurroundedDamage, out var id);
            if (e == null) { _surroundStacks = 0f; return; }
            var max = Mathf.Min(20f, e.cap + 2f * (Mathf.Max(1, StackCount(id)) - 1));
            var surrounded = _world.ShipExists && _world.EnemiesNear(e.threshold) >= Mathf.Max(1f, e.seconds);
            _surroundStacks = surrounded ? Mathf.Min(max, _surroundStacks + dt) : Mathf.Max(0f, _surroundStacks - 2f * dt);
        }

        /// <summary>V5 M5 Captain's Last Rum: once a run, a fatal hit restores the ship instead (DamageReceiver hook).</summary>
        public bool TryCheatDeath(out float restoreFraction, out float immunitySeconds)
        {
            restoreFraction = 0f;
            immunitySeconds = 0f;
            var e = Effect(ItemEffectKind.CheatDeath, out var id);
            if (e == null || _lastRumUsed) return false;
            _lastRumUsed = true;
            restoreFraction = e.value;
            immunitySeconds = e.threshold;
            _frenzyBonus = e.cap;
            _frenzyTimer = e.seconds;
            CheatedDeath?.Invoke(id);
            return true;
        }

        /// <summary>The Last Rum's once-a-run save is spent (HUD and tests).</summary>
        public bool LastRumUsed => _lastRumUsed;
        /// <summary>Jolly Roger's and Barnacle Crown's stacks now (HUD and tests).</summary>
        public int StreakStacks => _streakStacks;
        public float SurroundStacks => _surroundStacks;

        /// <summary>V5 M5 Shark Tooth Necklace: damage × this against enemies above the threshold share of their hull.</summary>
        public float HealthyTargetDamageMultiplier(out float threshold)
        {
            var e = Effect(ItemEffectKind.HealthyTargetDamage, out var id);
            threshold = e != null ? e.threshold : 1f;
            return e == null ? 1f : 1f + Mathf.Min(e.cap, e.value * Mathf.Max(1, StackCount(id)));
        }

        /// <summary>V5 M5 Skeleton Key: the chance a chest is free.</summary>
        public float FreeChestChance()
        {
            var e = Effect(ItemEffectKind.FreeChestChance, out var id);
            return e == null ? 0f : Mathf.Min(e.cap, 1f - Mathf.Pow(1f - e.value, Mathf.Max(1, StackCount(id))));
        }

        /// <summary>V5 M5 Smuggler's Ledger: chest prices × this (once, whatever the copies).</summary>
        public float ChestPriceMultiplier() => Effect(ItemEffectKind.ChestPriceAndRarity)?.value ?? 1f;

        /// <summary>V5 M5 Smuggler's Ledger: non-Common chest rarity weights × this.</summary>
        public float ChestRarityBoost()
        {
            var e = Effect(ItemEffectKind.ChestPriceAndRarity, out var id);
            return e == null ? 1f : Mathf.Min(e.cap, 1f + e.threshold * Mathf.Max(1, StackCount(id)));
        }

        /// <summary>V5 M5 Black Spot: elite kill gold and XP × this.</summary>
        public float EliteRewardMultiplier()
        {
            var e = Effect(ItemEffectKind.EliteRewards, out var id);
            return e == null ? 1f : 1f + Mathf.Min(e.cap, e.value * Mathf.Max(1, StackCount(id)));
        }

        void TrackHeadingAndMovement()
        {
            if (!_world.ShipExists) return;
            var forward = _world.ShipForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();
            var angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _lastHeading = angle;
            }
            else
            {
                var turn = Effect(ItemEffectKind.AttackSpeedAfterTurn);
                if (turn != null && Mathf.Abs(Mathf.DeltaAngle(_lastHeading, angle)) >= turn.threshold)
                {
                    _turnBuffTimer = turn.seconds;
                    _lastHeading = angle;
                }
            }

            var pos = _world.ShipPosition;
            if (_lastPosition == Vector3.zero) _lastPosition = pos;
            _distanceSinceHit += Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_lastPosition.x, 0f, _lastPosition.z));
            _lastPosition = pos;
            var fuse = Effect(ItemEffectKind.ExplosionDamageAfterDistance);
            if (fuse != null && _distanceSinceHit > fuse.threshold)
            {
                _explosionCharge = 1f;
                _distanceSinceHit = 0f;
            }
        }

        /// <summary>V3 NotifyPlayerTookDamage: the distance without a hit starts again.</summary>
        public void NotifyPlayerTookDamage()
        {
            _distanceSinceHit = 0f;
            _explosionCharge = 0f;
            _streakStacks -= (_streakStacks + 1) / 2; // V5 M5 Jolly Roger: a hit takes half the streak, rounded up
        }

        /// <summary>V3 NotifyNearMissDodge (V3 never calls it).</summary>
        public void NotifyNearMiss()
        {
            var e = Effect(ItemEffectKind.CritAfterNearMiss);
            if (e != null) _nearMissBuffTimer = e.seconds;
        }

        /// <summary>V3 NotifyKill: Captain's Bell counts kills.</summary>
        public void NotifyKill()
        {
            var streak = Effect(ItemEffectKind.KillStreakDamage, out var streakId);
            if (streak != null && streak.threshold >= 1f && ++_streakKills >= streak.threshold)
            {
                _streakKills = 0;
                var max = Mathf.Min(40f, streak.cap + streak.seconds * (Mathf.Max(1, StackCount(streakId)) - 1));
                _streakStacks = Mathf.Min(Mathf.FloorToInt(max), _streakStacks + 1);
            }
            var bell = Effect(ItemEffectKind.FireAllEveryKills);
            if (bell == null) return;
            _bellKills++;
            if (_bellKills < bell.threshold) return;
            _bellKills = 0;
            FireAllWeapons?.Invoke();
        }

        // ---- R-40 (V3 computed these in RefreshDynamicMultipliers but nothing read them; each counts only while its
        // item effect is active, which the v3-parity preset switches off) ----------------------------------------------

        /// <summary>Black Powder Keg ×1.12 on explosions, and Powder Fuse's one-shot ×1.35 once charged (V3 RefreshDynamicMultipliers).</summary>
        public float ExplosionDamageMultiplier()
        {
            var mult = 1f;
            var keg = Effect(ItemEffectKind.ExplosionDamage);
            if (keg != null) mult *= keg.value;
            var fuse = Effect(ItemEffectKind.ExplosionDamageAfterDistance);
            if (fuse != null && _explosionCharge > 0f) mult *= fuse.value;
            return mult;
        }

        /// <summary>V3 NotifyExplosionResolved: an explosion used Powder Fuse's charge.</summary>
        public void NotifyExplosionResolved() => _explosionCharge = 0f;

        /// <summary>Sea Witch's Eye ×1.15 on status (burn) damage.</summary>
        public float StatusDamageMultiplier() => Effect(ItemEffectKind.StatusDamage)?.value ?? 1f;

        /// <summary>Leviathan's Tooth ×1.2 against controlled (slowed) enemies.</summary>
        public float ControlledTargetDamageMultiplier() => Effect(ItemEffectKind.ControlledTargetDamage)?.value ?? 1f;

        /// <summary>Stormglass: extra chain-lightning jumps.</summary>
        public int ExtraLightningJumps() => Mathf.RoundToInt(Effect(ItemEffectKind.ExtraLightningJumps)?.value ?? 0f);

        /// <summary>Cursed Compass ×1.18 on the ambient spawn rate.</summary>
        public float SpawnDensityMultiplier() => Effect(ItemEffectKind.SpawnDensity)?.value ?? 1f;

        /// <summary>V3 GetIncomingDamageMultiplier (Reinforced Keel).</summary>
        public float IncomingDamageMultiplier()
        {
            var keel = Effect(ItemEffectKind.IncomingDamageAtSpeed);
            if (keel == null || !_world.ShipExists) return 1f;
            var v = _world.ShipVelocity;
            return new Vector3(v.x, 0f, v.z).magnitude >= keel.threshold ? keel.value : 1f;
        }

        /// <summary>V3 GetAttackSpeedMultiplier (Gunner's Gloves).</summary>
        public float AttackSpeedMultiplier()
        {
            if (_turnBuffTimer <= 0f) return 1f;
            return Effect(ItemEffectKind.AttackSpeedAfterTurn)?.value ?? 1f;
        }

        /// <summary>V3 GetCritChanceBonus (Ghost Lantern).</summary>
        public float CritChanceBonus()
        {
            if (_nearMissBuffTimer <= 0f) return 0f;
            return Effect(ItemEffectKind.CritAfterNearMiss)?.value ?? 0f;
        }

        /// <summary>V3 GetGlobalDamageMultiplier: Admiral's Ledger (weapon families), then Golden Figurehead (gold).</summary>
        public float GlobalDamageMultiplier()
        {
            var mult = 1f;
            var ledger = Effect(ItemEffectKind.DamagePerWeaponFamily);
            if (ledger != null)
            {
                _families.Clear();
                foreach (var w in _world.Weapons)
                    _families.Add(w?.tags != null && w.tags.Length > 0 ? w.tags[0] : w?.weaponId ?? "unknown");
                mult *= 1f + Mathf.Min(ledger.cap, _families.Count * ledger.value);
            }
            var figurehead = Effect(ItemEffectKind.DamagePerGold);
            if (figurehead != null) mult *= 1f + Mathf.Min(figurehead.cap, _world.Gold / figurehead.threshold);
            // V5 M5: Jolly Roger's streak, Barnacle Crown's stacks, the Last Rum's frenzy.
            if (_streakStacks > 0) mult *= 1f + _streakStacks * (Effect(ItemEffectKind.KillStreakDamage)?.value ?? 0f);
            if (_surroundStacks > 0f) mult *= 1f + Mathf.Floor(_surroundStacks + 1e-3f) * (Effect(ItemEffectKind.SurroundedDamage)?.value ?? 0f);
            if (_frenzyTimer > 0f) mult *= 1f + _frenzyBonus;
            return mult;
        }
    }
}

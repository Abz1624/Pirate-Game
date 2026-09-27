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

        public ItemModule(Func<string, ItemConfig> item, IStatModifierSink stats, IItemWorld world)
        {
            _item = item;
            _stats = stats;
            _world = world;
        }

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

        ItemEffect Effect(ItemEffectKind kind)
        {
            foreach (var id in _items)
            {
                var config = _item(id);
                if (config?.effects == null) continue;
                foreach (var e in config.effects) if (e != null && e.kind == kind && e.active) return e;
            }
            return null;
        }

        /// <summary>V3 Update (skipped while paused or after the run ends).</summary>
        public void Tick(float dt)
        {
            if (!_world.RunActive) return;
            _turnBuffTimer = Mathf.Max(0f, _turnBuffTimer - dt);
            _nearMissBuffTimer = Mathf.Max(0f, _nearMissBuffTimer - dt);
            TrackHeadingAndMovement();
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
            var bell = Effect(ItemEffectKind.FireAllEveryKills);
            if (bell == null) return;
            _bellKills++;
            if (_bellKills < bell.threshold) return;
            _bellKills = 0;
            FireAllWeapons?.Invoke();
        }

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
                var families = new HashSet<string>();
                foreach (var w in _world.Weapons)
                    families.Add(w?.tags != null && w.tags.Length > 0 ? w.tags[0] : w?.weaponId ?? "unknown");
                mult *= 1f + Mathf.Min(ledger.cap, families.Count * ledger.value);
            }
            var figurehead = Effect(ItemEffectKind.DamagePerGold);
            if (figurehead != null) mult *= 1f + Mathf.Min(figurehead.cap, _world.Gold / figurehead.threshold);
            return mult;
        }
    }
}

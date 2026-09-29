using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;

namespace RoguePirates.V4
{
    /// <summary>
    /// Fixed-capacity storage for simulation records (enemies, projectiles, pickups): a struct array, a free-list
    /// stack and a dense list of active slots. Allocate and free are O(1), iteration touches active slots only, and
    /// nothing is allocated after construction. A slot index stays valid until it is freed; <see cref="Generation"/>
    /// changes on every allocation, so a stored (index, generation) pair can tell a reused slot from the original.
    /// </summary>
    public sealed class SlotPool<T> where T : struct
    {
        readonly T[] _items;
        readonly int[] _free;          // stack of free slot indices
        readonly int[] _active;        // dense list of active slot indices
        readonly int[] _activePos;     // slot -> position in _active, -1 when free
        readonly int[] _generation;
        int _freeCount;

        public SlotPool(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be positive");
            _items = new T[capacity];
            _free = new int[capacity];
            _active = new int[capacity];
            _activePos = new int[capacity];
            _generation = new int[capacity];
            Clear();
        }

        public int Capacity => _items.Length;
        /// <summary>Slots in use.</summary>
        public int Count { get; private set; }
        /// <summary>Most slots in use at once since construction or <see cref="ResetPeak"/>.</summary>
        public int Peak { get; private set; }
        /// <summary>Allocations refused because every slot was in use.</summary>
        public int Refused { get; private set; }
        public bool IsFull => _freeCount == 0;

        /// <summary>Takes a free slot, reset to default(T). Returns -1 (and counts a refusal) when full.</summary>
        public int Allocate()
        {
            if (_freeCount == 0) { Refused++; return -1; }
            var index = _free[--_freeCount];
            _items[index] = default;
            _generation[index]++;
            _activePos[index] = Count;
            _active[Count++] = index;
            if (Count > Peak) Peak = Count;
            return index;
        }

        /// <summary>Returns a slot to the pool. Freeing a free or out-of-range slot is ignored and returns false.</summary>
        public bool Free(int index)
        {
            if (!IsActive(index)) return false;
            // Swap-remove from the dense active list.
            var pos = _activePos[index];
            var last = _active[--Count];
            _active[pos] = last;
            _activePos[last] = pos;
            _activePos[index] = -1;
            _items[index] = default;
            _free[_freeCount++] = index;
            return true;
        }

        public bool IsActive(int index) => index >= 0 && index < _items.Length && _activePos[index] >= 0;
        public int Generation(int index) => _generation[index];

        /// <summary>The record in a slot, by reference (write through it to change the record).</summary>
        public ref T this[int index] => ref _items[index];

        /// <summary>The slot index of the i-th active record (0 ≤ i &lt; Count). Order changes when slots are freed.</summary>
        public int ActiveAt(int i) => _active[i];

        /// <summary>Frees every slot (generations are kept, so old handles stay invalid).</summary>
        public void Clear()
        {
            Count = 0;
            _freeCount = _items.Length;
            // Hand out low indices first.
            for (var i = 0; i < _items.Length; i++)
            {
                _free[i] = _items.Length - 1 - i;
                _activePos[i] = -1;
                _items[i] = default;
            }
        }

        public void ResetPeak() { Peak = Count; Refused = 0; }
    }

    /// <summary>Slot pool sizes for the simulation, from the quality tier (V3's QualityProfile caps).</summary>
    public readonly struct SlotPoolCapacities
    {
        public readonly int Enemies, PlayerProjectiles, HostileProjectiles, Pickups;

        public SlotPoolCapacities(int enemies, int playerProjectiles, int hostileProjectiles, int pickups)
        {
            Enemies = enemies;
            PlayerProjectiles = playerProjectiles;
            HostileProjectiles = hostileProjectiles;
            Pickups = pickups;
        }

        /// <summary>V3's HostileProjectileWorld holds 96 shots on every tier.</summary>
        public const int HostileProjectileCap = 96;

        public static SlotPoolCapacities For(QualityTier tier)
        {
            var caps = QualityProfile.Get(tier);
            return new SlotPoolCapacities(caps.enemyCap, caps.projectileCap, HostileProjectileCap, caps.pickupCap);
        }

        public static SlotPoolCapacities Current
        {
            get
            {
                var caps = QualityProfile.Current;
                return new SlotPoolCapacities(caps.enemyCap, caps.projectileCap, HostileProjectileCap, caps.pickupCap);
            }
        }
    }
}

using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    /// <summary>What a VFX profile belongs to.</summary>
    public enum VfxSubject { Weapon, PlayerShip, Enemy }

    /// <summary>The moments a profile can dress (not every subject uses every slot; the VFX editor shows the ones it does).</summary>
    public enum VfxSlotKind { Muzzle, Projectile, Trail, Hit, Death, Spawn }

    /// <summary>
    /// One moment's effect. With a prefab, it plays that prefab (instead of the game's default effect when
    /// <see cref="replaceDefault"/> is on, else on top of it). Without a prefab, the default effect plays with this slot's
    /// scale. Tint multiplies the prefab's particle and material colours.
    /// </summary>
    [Serializable]
    public sealed class VfxSlot
    {
        public bool enabled = true;
        public GameObject prefab;
        [Tooltip("On: the prefab replaces the game's default effect. Off: it plays on top of the default.")]
        public bool replaceDefault = true;
        [Min(0.01f)] public float scale = 1f;
        public bool useTint;
        public Color tint = Color.white;
        [Tooltip("Local offset: x right, y up, z forward of the subject (or of the firing direction).")]
        public Vector3 offset;
        [Tooltip("Seconds before the effect returns to the pool (0 = as long as its particles last).")]
        [Min(0f)] public float lifetime;

        public bool HasPrefab => enabled && prefab != null;
        public float ScaleOrOne => enabled && scale > 0f ? scale : 1f;
        public bool SuppressesDefault => enabled && (prefab != null ? replaceDefault : false);
    }

    [Serializable]
    public sealed class VfxProfile
    {
        public VfxSubject subject;
        [Tooltip("weaponId, enemy id, or the ship id (PlayerShipId).")]
        public string id;
        public string displayName;
        public VfxSlot muzzle = new VfxSlot();
        public VfxSlot projectile = new VfxSlot();
        public VfxSlot trail = new VfxSlot();
        public VfxSlot hit = new VfxSlot();
        public VfxSlot death = new VfxSlot();
        public VfxSlot spawn = new VfxSlot();

        public VfxSlot Slot(VfxSlotKind kind)
        {
            switch (kind)
            {
                case VfxSlotKind.Muzzle: return muzzle;
                case VfxSlotKind.Projectile: return projectile;
                case VfxSlotKind.Trail: return trail;
                case VfxSlotKind.Hit: return hit;
                case VfxSlotKind.Death: return death;
                default: return spawn;
            }
        }

        /// <summary>The slots this subject uses (the editor shows only these).</summary>
        public static VfxSlotKind[] SlotsFor(VfxSubject subject)
        {
            switch (subject)
            {
                case VfxSubject.Weapon: return new[] { VfxSlotKind.Muzzle, VfxSlotKind.Projectile, VfxSlotKind.Trail, VfxSlotKind.Hit, VfxSlotKind.Death };
                case VfxSubject.PlayerShip: return new[] { VfxSlotKind.Spawn, VfxSlotKind.Trail, VfxSlotKind.Hit, VfxSlotKind.Death };
                default: return new[] { VfxSlotKind.Spawn, VfxSlotKind.Trail, VfxSlotKind.Hit, VfxSlotKind.Death };
            }
        }

        public static string SlotHelp(VfxSubject subject, VfxSlotKind kind)
        {
            switch (kind)
            {
                case VfxSlotKind.Muzzle: return "Plays at the gun when the weapon fires.";
                case VfxSlotKind.Projectile: return "The projectile model (replaces the weapon's projectile).";
                case VfxSlotKind.Trail: return subject == VfxSubject.Weapon ? "Attached to each projectile while it flies." : "Attached to the model while it is alive.";
                case VfxSlotKind.Hit: return subject == VfxSubject.Weapon ? "Plays where this weapon hits an enemy." : subject == VfxSubject.PlayerShip ? "Plays on the ship when it takes damage." : "Plays on this enemy when it is hit.";
                case VfxSlotKind.Death: return subject == VfxSubject.Weapon ? "Plays when this weapon kills an enemy (an enemy's own death effect wins)." : subject == VfxSubject.PlayerShip ? "Plays when the ship sinks." : "Plays when this enemy dies.";
                default: return "Plays when it appears.";
            }
        }
    }

    /// <summary>
    /// Per-weapon, player-ship and per-enemy effects that artists edit in Tools ▸ Rogue Pirates ▸ VFX Editor (demo, 30 Sep
    /// 2026). Loaded from Resources/V4VfxProfiles; read by the V4 presenters at the moment each effect plays, so changes
    /// made in Play mode show at once.
    /// </summary>
    [CreateAssetMenu(fileName = "V4VfxProfiles", menuName = "RoguePirates/V4 VFX Profiles")]
    public sealed class VfxProfileLibrary : ScriptableObject
    {
        public const string ResourcePath = "V4VfxProfiles";
        public const string PlayerShipId = "player_ship";

        public VfxProfile[] profiles = Array.Empty<VfxProfile>();

        static VfxProfileLibrary _loaded;
        public static VfxProfileLibrary Load()
        {
            if (_loaded == null) _loaded = Resources.Load<VfxProfileLibrary>(ResourcePath);
            return _loaded;
        }

        public VfxProfile Find(VfxSubject subject, string id)
        {
            if (string.IsNullOrEmpty(id) || profiles == null) return null;
            for (var i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                if (p != null && p.subject == subject && p.id == id) return p;
            }
            return null;
        }

        public VfxSlot Slot(VfxSubject subject, string id, VfxSlotKind kind) => Find(subject, id)?.Slot(kind);
    }
}

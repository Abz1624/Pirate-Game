using System;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>The art team's in-run HUD (demo, 30 Sep 2026): the prefab and the Captain portraits it shows.</summary>
    [CreateAssetMenu(fileName = "V4SaltWindHudSettings", menuName = "RoguePirates/V4 SaltWind HUD Settings")]
    public sealed class SaltWindHudSettings : ScriptableObject
    {
        public const string ResourcePath = "V4SaltWindHudSettings";

        [Serializable]
        public struct Portrait
        {
            [Tooltip("CaptainConfig.legacyId.")] public int captainId;
            public Sprite sprite;
            [Tooltip("The Captain's sail and flag on the player ship (replace the ship's sail and flag materials).")]
            public Material sail;
            public Material flag;
        }

        public GameObject hudPrefab;
        public Portrait[] portraits = Array.Empty<Portrait>();
        [Tooltip("Objective pip, done / not done.")]
        public Sprite markerDone;
        public Sprite markerOpen;

        public Sprite PortraitOf(int captainId)
        {
            foreach (var p in portraits) if (p.captainId == captainId) return p.sprite;
            return null;
        }

        [Tooltip("Material names on the player ship that the Captain's sail and flag replace.")]
        public string shipSailMaterial = "EnemySail";
        public string shipFlagMaterial = "SkullBandanaFlag";

        public Portrait? EntryOf(int captainId)
        {
            foreach (var p in portraits) if (p.captainId == captainId) return p;
            return null;
        }

        /// <summary>Puts the Captain's sail and flag on the ship's renderers (demo).</summary>
        public void ApplyShipLook(Transform ship, int captainId)
        {
            var entry = EntryOf(captainId);
            if (ship == null || entry == null) return;
            foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var name = mats[i].name.Replace(" (Instance)", "");
                    if (entry.Value.sail != null && name == shipSailMaterial) { mats[i] = entry.Value.sail; changed = true; }
                    else if (entry.Value.flag != null && name == shipFlagMaterial) { mats[i] = entry.Value.flag; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public static SaltWindHudSettings Load() => Resources.Load<SaltWindHudSettings>(ResourcePath);
    }
}

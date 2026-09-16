using UnityEngine;

namespace RoguePirates.Presentation
{
    /// <summary>Placeholder audio hooks until approved clips exist (see plan/placeholder_registry.json).</summary>
    public static class PlaceholderAudio
    {
        public static void PlayCannonFire() => Debug.Log("[PlaceholderAudio] sfx_cannon_fire");
        public static void PlayPickupInfamy() => Debug.Log("[PlaceholderAudio] sfx_pickup_infamy");
        public static void PlayFloorPowerup(string kind) => Debug.Log($"[PlaceholderAudio] sfx_floor_powerup:{kind}");
        public static void PlayBossSummon() => Debug.Log("[PlaceholderAudio] sfx_boss_summon");
        public static void PlayStormEdge() => Debug.Log("[PlaceholderAudio] sfx_storm_edge");
        public static void PlayMusicSaltwind(string stem) => Debug.Log($"[PlaceholderAudio] music_run_saltwind:{stem}");
    }
}

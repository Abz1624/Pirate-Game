using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Combat
{
    /// <summary>Doc §7.5 time-based enemy weight multipliers for Director V2 continuous spawns.</summary>
    public static class CombatDirectorV2Composition
    {
        public static float GetWeightMultiplier(string enemyId, float elapsedMinutes)
        {
            if (string.IsNullOrEmpty(enemyId)) return 0f;
            var id = enemyId.ToLowerInvariant();

            if (elapsedMinutes < 2f)
                return Early(id);

            if (elapsedMinutes < 4f)
                return MidEarly(id);

            if (elapsedMinutes < 6f)
                return Mid(id);

            if (elapsedMinutes < 8f)
                return Late(id);

            return Endgame(id);
        }

        static float Early(string id) => id switch
        {
            "shark" => 1.2f,
            "cannonboat" => 1.1f,
            "swordfish" => 0.9f,
            "manta_ray" or "manta" => 0f,
            "hammerhead" => 0f,
            "kraken_spawn" => 0f,
            "reef_crab" or "reef_crab_miniboss" => 0f,
            _ => 0.35f
        };

        static float MidEarly(string id) => id switch
        {
            "shark" => 1f,
            "cannonboat" => 1f,
            "swordfish" => 1.15f,
            "manta_ray" or "manta" => 0.85f,
            "hammerhead" => 0.25f,
            "kraken_spawn" => 0f,
            "reef_crab" or "reef_crab_miniboss" => 0.15f,
            _ => 0.3f
        };

        static float Mid(string id) => id switch
        {
            "shark" => 0.9f,
            "cannonboat" => 0.85f,
            "swordfish" => 1f,
            "manta_ray" or "manta" => 1f,
            "hammerhead" => 0.85f,
            "kraken_spawn" => 0.35f,
            "reef_crab" or "reef_crab_miniboss" => 0.75f,
            _ => 0.25f
        };

        static float Late(string id) => id switch
        {
            "shark" => 0.85f,
            "cannonboat" => 0.8f,
            "swordfish" => 0.95f,
            "manta_ray" or "manta" => 0.9f,
            "hammerhead" => 1.1f,
            "kraken_spawn" => 1f,
            "reef_crab" or "reef_crab_miniboss" => 0.6f,
            _ => 0.2f
        };

        static float Endgame(string id) => id switch
        {
            "shark" => 0.75f,
            "cannonboat" => 0.75f,
            "swordfish" => 0.85f,
            "manta_ray" or "manta" => 0.85f,
            "hammerhead" => 1.15f,
            "kraken_spawn" => 1.15f,
            "reef_crab" or "reef_crab_miniboss" => 0.5f,
            _ => 0.15f
        };
    }
}

using System.Collections.Generic;
using System.Linq;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Loadout changes for the console and Module Lab. Since M9 the loadout is the V4 progression module's (it replaced
    /// the legacy level system and the switched-off WeaponController record); the Weapon Manager picks the change up on
    /// its next tick.
    /// </summary>
    [DeletedInPhase(14, "The console calls the progression module directly")]
    public static class LegacyWeaponLab
    {
        static Progression.ProgressionModule Levels(V4Game game) => game?.Build?.Progression;

        public static IReadOnlyList<string> Loadout(V4Game game) =>
            Levels(game)?.Loadout.Weapons.Select(w => w.Id).ToList() ?? new List<string>();

        public static int SlotCap(V4Game game) => System.Math.Max(Levels(game)?.Loadout.Count ?? 0, 6);

        public static void SetLoadout(V4Game game, IReadOnlyList<string> weaponIds)
        {
            var levels = Levels(game);
            if (levels == null) return;
            levels.DevSetWeapons(weaponIds);
            // V3's SetWeaponLoadout cleared the controller's kegs, strikes, skiffs and pending broadsides; V4 does the same.
            if (game.Context != null && game.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons)) weapons.ClearInFlight();
        }

        public static void Add(V4Game game, string weaponId)
        {
            var ids = Loadout(game).ToList();
            if (ids.Contains(weaponId)) return;
            ids.Add(weaponId);
            SetLoadout(game, ids);
        }

        public static void Remove(V4Game game, int index)
        {
            var ids = Loadout(game).ToList();
            if (index < 0 || index >= ids.Count) return;
            ids.RemoveAt(index);
            SetLoadout(game, ids);
        }

        public static void Solo(V4Game game, string weaponId) => SetLoadout(game, new[] { weaponId });

        /// <summary>One level up with an Uncommon package.</summary>
        public static void RankUp(V4Game game, int index) => Levels(game)?.DevRankUp(index);

        /// <summary>Evolves a weapon as taking its evolution card does.</summary>
        public static void Evolve(V4Game game, string weaponId, string evolutionId) => Levels(game)?.DevEvolve(weaponId, evolutionId);

        public static bool IsEvolved(V4Game game, string weaponId) => Levels(game)?.EvolutionOf(weaponId) != null;
    }
}
